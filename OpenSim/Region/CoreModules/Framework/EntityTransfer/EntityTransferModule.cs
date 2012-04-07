/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Threading;

using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Physics.Manager;
using OpenSim.Services.Interfaces;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.CoreModules.Framework.EntityTransfer
{
    public class EntityTransferModule : ISharedRegionModule, IEntityTransferModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The maximum distance, in standard region units (256m) that an agent is allowed to transfer.
        /// </summary>
        private int m_MaxTransferDistance = 4095;
        public int MaxTransferDistance
        {
            get { return m_MaxTransferDistance; }
            set { m_MaxTransferDistance = value; }
        }

        private int m_levelHGTeleport = 0;

        protected bool m_Enabled = false;
        protected Scene m_aScene;
        protected List<Scene> m_Scenes = new List<Scene>();
        protected List<UUID> m_agentsInTransit;
        private ExpiringCache<UUID, ExpiringCache<ulong, DateTime>> m_bannedRegions =
                new ExpiringCache<UUID, ExpiringCache<ulong, DateTime>>();

        #region ISharedRegionModule

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public virtual string Name
        {
            get { return "BasicEntityTransferModule"; }
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("EntityTransferModule", "");
                if (name == Name)
                {
                    InitialiseCommon(source);
                    m_log.DebugFormat("[ENTITY TRANSFER MODULE]: {0} enabled.", Name);
                }
            }
        }

        /// <summary>
        /// Initialize config common for this module and any descendents.
        /// </summary>
        /// <param name="source"></param>
        protected virtual void InitialiseCommon(IConfigSource source)
        {
            IConfig transferConfig = source.Configs["EntityTransfer"];
            if (transferConfig != null)
            {
                MaxTransferDistance = transferConfig.GetInt("max_distance", 4095);
                m_levelHGTeleport = transferConfig.GetInt("LevelHGTeleport", 0);
            }

            m_agentsInTransit = new List<UUID>();
            m_Enabled = true;
        }

        public virtual void PostInitialise()
        {
        }

        public virtual void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            if (m_aScene == null)
                m_aScene = scene;

            m_Scenes.Add(scene);
            scene.RegisterModuleInterface<IEntityTransferModule>(this);
            scene.EventManager.OnNewClient += OnNewClient;
        }

        protected virtual void OnNewClient(IClientAPI client)
        {
            client.OnTeleportHomeRequest += TriggerTeleportHome;
            client.OnTeleportLandmarkRequest += RequestTeleportLandmark;
        }

        public virtual void Close()
        {
            if (!m_Enabled)
                return;
        }

        public virtual void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;
            if (scene == m_aScene)
                m_aScene = null;

            m_Scenes.Remove(scene);
        }

        public virtual void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;
        }

        #endregion

        #region Agent Teleports

        public void Teleport(ScenePresence sp, ulong regionHandle, Vector3 position, Vector3 lookAt, uint teleportFlags)
        {
            if (!sp.Scene.Permissions.CanTeleport(sp.UUID))
                return;

            IEventQueue eq = sp.Scene.RequestModuleInterface<IEventQueue>();

            // Reset animations; the viewer does that in teleports.
            sp.Animator.ResetAnimations();

            try
            {
                if (regionHandle == sp.Scene.RegionInfo.RegionHandle)
                {
                    m_log.DebugFormat(
                        "[ENTITY TRANSFER MODULE]: RequestTeleportToLocation {0} within {1}",
                        position, sp.Scene.RegionInfo.RegionName);

                    // Teleport within the same region
                    if (IsOutsideRegion(sp.Scene, position) || position.Z < 0)
                    {
                        Vector3 emergencyPos = new Vector3(128, 128, 128);

                        m_log.WarnFormat(
                            "[ENTITY TRANSFER MODULE]: RequestTeleportToLocation() was given an illegal position of {0} for avatar {1}, {2}.  Substituting {3}",
                            position, sp.Name, sp.UUID, emergencyPos);
                        position = emergencyPos;
                    }

                    // TODO: Get proper AVG Height
                    float localAVHeight = 1.56f;
                    float posZLimit = 22;

                    // TODO: Check other Scene HeightField
                    if (position.X > 0 && position.X <= (int)Constants.RegionSize && position.Y > 0 && position.Y <= (int)Constants.RegionSize)
                    {
                        posZLimit = (float)sp.Scene.Heightmap[(int)position.X, (int)position.Y];
                    }

                    float newPosZ = posZLimit + localAVHeight;
                    if (posZLimit >= (position.Z - (localAVHeight / 2)) && !(Single.IsInfinity(newPosZ) || Single.IsNaN(newPosZ)))
                    {
                        position.Z = newPosZ;
                    }

                    sp.ControllingClient.SendTeleportStart(teleportFlags);

                    sp.ControllingClient.SendLocalTeleport(position, lookAt, teleportFlags);
                    sp.TeleportFlags = (Constants.TeleportFlags)teleportFlags;
                    sp.Teleport(position);

                    foreach (SceneObjectGroup grp in sp.GetAttachments())
                    {
                        sp.Scene.EventManager.TriggerOnScriptChangedEvent(grp.LocalId, (uint)Changed.TELEPORT);
                    }
                }
                else // Another region possibly in another simulator
                {
                    uint x = 0, y = 0;
                    Utils.LongToUInts(regionHandle, out x, out y);
                    GridRegion reg = m_aScene.GridService.GetRegionByPosition(sp.Scene.RegionInfo.ScopeID, (int)x, (int)y);

                    if (reg != null)
                    {
                        GridRegion finalDestination = GetFinalDestination(reg);
                        if (finalDestination == null)
                        {
                            m_log.WarnFormat("[ENTITY TRANSFER MODULE]: Final destination is having problems. Unable to teleport agent.");
                            sp.ControllingClient.SendTeleportFailed("Problem at destination");
                            return;
                        }

                        // check if HyperGrid teleport is allowed, based on user level
                        int flags = m_aScene.GridService.GetRegionFlags(sp.Scene.RegionInfo.ScopeID, reg.RegionID);

                        if (((flags & (int)OpenSim.Data.RegionFlags.Hyperlink) != 0) && (sp.UserLevel < m_levelHGTeleport))
                        {
                            m_log.WarnFormat("[ENTITY TRANSFER MODULE]: Final destination link is non permitted hypergrid region. Unable to teleport agent.");
                            sp.ControllingClient.SendTeleportFailed("HyperGrid teleport not permitted");
                            return;
                        }

                        uint curX = 0, curY = 0;
                        Utils.LongToUInts(sp.Scene.RegionInfo.RegionHandle, out curX, out curY);
                        int curCellX = (int)(curX / Constants.RegionSize);
                        int curCellY = (int)(curY / Constants.RegionSize);
                        int destCellX = (int)(finalDestination.RegionLocX / Constants.RegionSize);
                        int destCellY = (int)(finalDestination.RegionLocY / Constants.RegionSize);

//                        m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Source co-ords are x={0} y={1}", curRegionX, curRegionY);
//
//                        m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Final dest is x={0} y={1} {2}@{3}",
//                            destRegionX, destRegionY, finalDestination.RegionID, finalDestination.ServerURI);

                        // Check that these are not the same coordinates
                        if (finalDestination.RegionLocX == sp.Scene.RegionInfo.RegionLocX &&
                            finalDestination.RegionLocY == sp.Scene.RegionInfo.RegionLocY)
                        {
                            // Can't do. Viewer crashes
                            sp.ControllingClient.SendTeleportFailed("Space warp! You would crash. Move to a different region and try again.");
                            return;
                        }

                        if (Math.Abs(curCellX - destCellX) > MaxTransferDistance || Math.Abs(curCellY - destCellY) > MaxTransferDistance)
                        {
                            sp.ControllingClient.SendTeleportFailed(
                                string.Format(
                                  "Can't teleport to {0} ({1},{2}) from {3} ({4},{5}), destination is more than {6} regions way",
                                  finalDestination.RegionName, destCellX, destCellY,
                                  sp.Scene.RegionInfo.RegionName, curCellX, curCellY,
                                  MaxTransferDistance));

                            return;
                        }

                        //
                        // This is it
                        //
                        DoTeleport(sp, reg, finalDestination, position, lookAt, teleportFlags, eq);
                        //
                        //
                        //
                    }
                    else
                    {
                        // TP to a place that doesn't exist (anymore)
                        // Inform the viewer about that
                        sp.ControllingClient.SendTeleportFailed("The region you tried to teleport to doesn't exist anymore");

                        // and set the map-tile to '(Offline)'
                        uint regX, regY;
                        Utils.LongToUInts(regionHandle, out regX, out regY);

                        MapBlockData block = new MapBlockData();
                        block.X = (ushort)(regX / Constants.RegionSize);
                        block.Y = (ushort)(regY / Constants.RegionSize);
                        block.Access = 254; // == not there

                        List<MapBlockData> blocks = new List<MapBlockData>();
                        blocks.Add(block);
                        sp.ControllingClient.SendMapBlock(blocks, 0);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[ENTITY TRANSFER MODULE]: Exception on teleport: {0} {1}", e.Message, e.StackTrace);
                sp.ControllingClient.SendTeleportFailed("Internal error");
            }
        }

        public void DoTeleport(ScenePresence sp, GridRegion reg, GridRegion finalDestination, Vector3 position, Vector3 lookAt, uint teleportFlags, IEventQueue eq)
        {
            if (reg == null || finalDestination == null)
            {
                sp.ControllingClient.SendTeleportFailed("Unable to locate destination");
                return;
            }

            if (IsInTransit(sp.UUID)) // Avie is already on the way. Caller shouldn't do this.
                return;

            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: Request Teleport to {0} ({1}) {2}/{3}",
                reg.ServerURI, finalDestination.ServerURI, finalDestination.RegionName, position);

            uint newRegionX = (uint)(reg.RegionHandle >> 40);
            uint newRegionY = (((uint)(reg.RegionHandle)) >> 8);
            uint oldRegionX = (uint)(sp.Scene.RegionInfo.RegionHandle >> 40);
            uint oldRegionY = (((uint)(sp.Scene.RegionInfo.RegionHandle)) >> 8);

            ulong destinationHandle = finalDestination.RegionHandle;

            // Let's do DNS resolution only once in this process, please!
            // This may be a costly operation. The reg.ExternalEndPoint field is not a passive field,
            // it's actually doing a lot of work.
            IPEndPoint endPoint = finalDestination.ExternalEndPoint;
            if (endPoint != null && endPoint.Address != null)
            {
                // Fixing a bug where teleporting while sitting results in the avatar ending up removed from
                // both regions
                if (sp.ParentID != (uint)0)
                    sp.StandUp();

                if (!sp.ValidateAttachments())
                    m_log.DebugFormat(
                        "[ENTITY TRANSFER MODULE]: Failed validation of all attachments for teleport of {0} from {1} to {2}.  Continuing.",
                        sp.Name, sp.Scene.RegionInfo.RegionName, finalDestination.RegionName);

//                if (!sp.ValidateAttachments())
//                {
//                    sp.ControllingClient.SendTeleportFailed("Inconsistent attachment state");
//                    return;
//                }

                string reason;
                string version;
                if (!m_aScene.SimulationService.QueryAccess(finalDestination, sp.ControllingClient.AgentId, Vector3.Zero, out version, out reason))
                {
                    sp.ControllingClient.SendTeleportFailed("Teleport failed: " + reason);
                    return;
                }
                m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Destination is running version {0}", version);

                sp.ControllingClient.SendTeleportStart(teleportFlags);

                // the avatar.Close below will clear the child region list. We need this below for (possibly)
                // closing the child agents, so save it here (we need a copy as it is Clear()-ed).
                //List<ulong> childRegions = avatar.KnownRegionHandles;
                // Compared to ScenePresence.CrossToNewRegion(), there's no obvious code to handle a teleport
                // failure at this point (unlike a border crossing failure).  So perhaps this can never fail
                // once we reach here...
                //avatar.Scene.RemoveCapsHandler(avatar.UUID);

                string capsPath = String.Empty;

                AgentCircuitData currentAgentCircuit = sp.Scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);
                AgentCircuitData agentCircuit = sp.ControllingClient.RequestClientInfo();
                agentCircuit.startpos = position;
                agentCircuit.child = true;
                agentCircuit.Appearance = sp.Appearance;
                if (currentAgentCircuit != null)
                {
                    agentCircuit.ServiceURLs = currentAgentCircuit.ServiceURLs;
                    agentCircuit.IPAddress = currentAgentCircuit.IPAddress;
                    agentCircuit.Viewer = currentAgentCircuit.Viewer;
                    agentCircuit.Channel = currentAgentCircuit.Channel;
                    agentCircuit.Mac = currentAgentCircuit.Mac;
                    agentCircuit.Id0 = currentAgentCircuit.Id0;
                }

                if (NeedsNewAgent(sp.DrawDistance, oldRegionX, newRegionX, oldRegionY, newRegionY))
                {
                    // brand new agent, let's create a new caps seed
                    agentCircuit.CapsPath = CapsUtil.GetRandomCapsObjectPath();
                }

                // Let's create an agent there if one doesn't exist yet. 
                bool logout = false;
                if (!CreateAgent(sp, reg, finalDestination, agentCircuit, teleportFlags, out reason, out logout))
                {
                    sp.ControllingClient.SendTeleportFailed(String.Format("Destination refused: {0}",
                                                                              reason));
                    return;
                }

                // OK, it got this agent. Let's close some child agents
                sp.CloseChildAgents(newRegionX, newRegionY);
                IClientIPEndpoint ipepClient;  
                if (NeedsNewAgent(sp.DrawDistance, oldRegionX, newRegionX, oldRegionY, newRegionY))
                {
                    //sp.ControllingClient.SendTeleportProgress(teleportFlags, "Creating agent...");
                    #region IP Translation for NAT
                    // Uses ipepClient above
                    if (sp.ClientView.TryGet(out ipepClient))
                    {
                        endPoint.Address = NetworkUtil.GetIPFor(ipepClient.EndPoint, endPoint.Address);
                    }
                    #endregion
                    capsPath = finalDestination.ServerURI + CapsUtil.GetCapsSeedPath(agentCircuit.CapsPath);

                    if (eq != null)
                    {
                        eq.EnableSimulator(destinationHandle, endPoint, sp.UUID);

                        // ES makes the client send a UseCircuitCode message to the destination, 
                        // which triggers a bunch of things there.
                        // So let's wait
                        Thread.Sleep(200);

                        eq.EstablishAgentCommunication(sp.UUID, endPoint, capsPath);

                    }
                    else
                    {
                        sp.ControllingClient.InformClientOfNeighbour(destinationHandle, endPoint);
                    }
                }
                else
                {
                    agentCircuit.CapsPath = sp.Scene.CapsModule.GetChildSeed(sp.UUID, reg.RegionHandle);
                    capsPath = finalDestination.ServerURI + CapsUtil.GetCapsSeedPath(agentCircuit.CapsPath);
                }


                SetInTransit(sp.UUID);

                // Let's send a full update of the agent. This is a synchronous call.
                AgentData agent = new AgentData();
                sp.CopyTo(agent);
                agent.Position = position;
                SetCallbackURL(agent, sp.Scene.RegionInfo);

                //sp.ControllingClient.SendTeleportProgress(teleportFlags, "Updating agent...");

                if (!UpdateAgent(reg, finalDestination, agent))
                {
                    // Region doesn't take it
                    m_log.WarnFormat(
                        "[ENTITY TRANSFER MODULE]: UpdateAgent failed on teleport of {0} to {1}.  Returning avatar to source region.", 
                        sp.Name, finalDestination.RegionName);
                    
                    Fail(sp, finalDestination, logout);
                    return;
                }

                sp.ControllingClient.SendTeleportProgress(teleportFlags | (uint)TeleportFlags.DisableCancel, "sending_dest");

                m_log.DebugFormat(
                    "[ENTITY TRANSFER MODULE]: Sending new CAPS seed url {0} to client {1}", capsPath, sp.UUID);

                if (eq != null)
                {
                    eq.TeleportFinishEvent(destinationHandle, 13, endPoint,
                                           0, teleportFlags, capsPath, sp.UUID);
                }
                else
                {
                    sp.ControllingClient.SendRegionTeleport(destinationHandle, 13, endPoint, 4,
                                                                teleportFlags, capsPath);
                }

                // Let's set this to true tentatively. This does not trigger OnChildAgent
                sp.IsChildAgent = true;

                // TeleportFinish makes the client send CompleteMovementIntoRegion (at the destination), which
                // trigers a whole shebang of things there, including MakeRoot. So let's wait for confirmation
                // that the client contacted the destination before we close things here.
                if (!WaitForCallback(sp.UUID))
                {
                    m_log.WarnFormat(
                        "[ENTITY TRANSFER MODULE]: Teleport of {0} to {1} failed due to no callback from destination region.  Returning avatar to source region.", 
                        sp.Name, finalDestination.RegionName);
                    
                    Fail(sp, finalDestination, logout);                   
                    return;
                }

                // For backwards compatibility
                if (version == "Unknown" || version == string.Empty)
                {
                    // CrossAttachmentsIntoNewRegion is a synchronous call. We shouldn't need to wait after it
                    m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Old simulator, sending attachments one by one...");
                    CrossAttachmentsIntoNewRegion(finalDestination, sp, true);
                }

                // May need to logout or other cleanup
                AgentHasMovedAway(sp, logout);

                // Well, this is it. The agent is over there.
                KillEntity(sp.Scene, sp.LocalId);

                // Now let's make it officially a child agent
                sp.MakeChildAgent();

//                sp.Scene.CleanDroppedAttachments();

                // Finally, let's close this previously-known-as-root agent, when the jump is outside the view zone

                if (NeedsClosing(sp.DrawDistance, oldRegionX, newRegionX, oldRegionY, newRegionY, reg))
                {
                    Thread.Sleep(5000);
                    sp.Close();
                    sp.Scene.IncomingCloseAgent(sp.UUID);
                }
                else
                {
                    // now we have a child agent in this region. 
                    sp.Reset();
                }

                // REFACTORING PROBLEM. Well, not a problem, but this method is HORRIBLE!
                if (sp.Scene.NeedSceneCacheClear(sp.UUID))
                {
                    m_log.DebugFormat(
                        "[ENTITY TRANSFER MODULE]: User {0} is going to another region, profile cache removed",
                        sp.UUID);
                }
            }
            else
            {
                sp.ControllingClient.SendTeleportFailed("Remote Region appears to be down");
            }
        }

        protected virtual void Fail(ScenePresence sp, GridRegion finalDestination, bool logout)
        {
            // Client never contacted destination. Let's restore everything back
            sp.ControllingClient.SendTeleportFailed("Problems connecting to destination.");

            // Fail. Reset it back
            sp.IsChildAgent = false;
            ReInstantiateScripts(sp);
            ResetFromTransit(sp.UUID);

            EnableChildAgents(sp);

            // Finally, kill the agent we just created at the destination.
            m_aScene.SimulationService.CloseAgent(finalDestination, sp.UUID);

            sp.Scene.EventManager.TriggerTeleportFail(sp.ControllingClient, logout);
        }

        protected virtual bool CreateAgent(ScenePresence sp, GridRegion reg, GridRegion finalDestination, AgentCircuitData agentCircuit, uint teleportFlags, out string reason, out bool logout)
        {
            logout = false;
            bool success = m_aScene.SimulationService.CreateAgent(finalDestination, agentCircuit, teleportFlags, out reason);

            if (success)
                sp.Scene.EventManager.TriggerTeleportStart(sp.ControllingClient, reg, finalDestination, teleportFlags, logout);

            return success;
        }

        protected virtual bool UpdateAgent(GridRegion reg, GridRegion finalDestination, AgentData agent)
        {
            return m_aScene.SimulationService.UpdateAgent(finalDestination, agent);
        }

        protected virtual void SetCallbackURL(AgentData agent, RegionInfo region)
        {
            agent.CallbackURI = region.ServerURI + "agent/" + agent.AgentID.ToString() + "/" + region.RegionID.ToString() + "/release/";
            m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Set callback URL to {0}", agent.CallbackURI);

        }

        protected virtual void AgentHasMovedAway(ScenePresence sp, bool logout)
        {
            sp.Scene.AttachmentsModule.DeleteAttachmentsFromScene(sp, true);
        }

        protected void KillEntity(Scene scene, uint localID)
        {
            scene.SendKillObject(new List<uint> { localID });
        }

        protected virtual GridRegion GetFinalDestination(GridRegion region)
        {
            return region;
        }

        protected virtual bool NeedsNewAgent(float drawdist, uint oldRegionX, uint newRegionX, uint oldRegionY, uint newRegionY)
        {
            return Util.IsOutsideView(drawdist, oldRegionX, newRegionX, oldRegionY, newRegionY);
        }

        protected virtual bool NeedsClosing(float drawdist, uint oldRegionX, uint newRegionX, uint oldRegionY, uint newRegionY, GridRegion reg)
        {
            return Util.IsOutsideView(drawdist, oldRegionX, newRegionX, oldRegionY, newRegionY);
        }

        protected virtual bool IsOutsideRegion(Scene s, Vector3 pos)
        {

            if (s.TestBorderCross(pos, Cardinals.N))
                return true;
            if (s.TestBorderCross(pos, Cardinals.S))
                return true;
            if (s.TestBorderCross(pos, Cardinals.E))
                return true;
            if (s.TestBorderCross(pos, Cardinals.W))
                return true;

            return false;
        }


        #endregion

        #region Landmark Teleport
        /// <summary>
        /// Tries to teleport agent to landmark.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="regionHandle"></param>
        /// <param name="position"></param>
        public virtual void RequestTeleportLandmark(IClientAPI remoteClient, AssetLandmark lm)
        {
            GridRegion info = m_aScene.GridService.GetRegionByUUID(UUID.Zero, lm.RegionID);

            if (info == null)
            {
                // can't find the region: Tell viewer and abort
                remoteClient.SendTeleportFailed("The teleport destination could not be found.");
                return;
            }
            ((Scene)(remoteClient.Scene)).RequestTeleportLocation(remoteClient, info.RegionHandle, lm.Position, 
                Vector3.Zero, (uint)(Constants.TeleportFlags.SetLastToTarget | Constants.TeleportFlags.ViaLandmark));
        }

        #endregion 

        #region Teleport Home

        public virtual void TriggerTeleportHome(UUID id, IClientAPI client)
        {
            TeleportHome(id, client);
        }

        public virtual bool TeleportHome(UUID id, IClientAPI client)
        {
            m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Request to teleport {0} {1} home", client.FirstName, client.LastName);

            //OpenSim.Services.Interfaces.PresenceInfo pinfo = m_aScene.PresenceService.GetAgent(client.SessionId);
            GridUserInfo uinfo = m_aScene.GridUserService.GetGridUserInfo(client.AgentId.ToString());

            if (uinfo != null)
            {
                if (uinfo.HomeRegionID == UUID.Zero)
                {
                    // can't find the Home region: Tell viewer and abort
                    client.SendTeleportFailed("You don't have a home position set.");
                    return false;
                }
                GridRegion regionInfo = m_aScene.GridService.GetRegionByUUID(UUID.Zero, uinfo.HomeRegionID);
                if (regionInfo == null)
                {
                    // can't find the Home region: Tell viewer and abort
                    client.SendTeleportFailed("Your home region could not be found.");
                    return false;
                }
                
                m_log.DebugFormat("[ENTITY TRANSFER MODULE]: User's home region is {0} {1} ({2}-{3})", 
                    regionInfo.RegionName, regionInfo.RegionID, regionInfo.RegionLocX / Constants.RegionSize, regionInfo.RegionLocY / Constants.RegionSize);

                // a little eekie that this goes back to Scene and with a forced cast, will fix that at some point...
                ((Scene)(client.Scene)).RequestTeleportLocation(
                    client, regionInfo.RegionHandle, uinfo.HomePosition, uinfo.HomeLookAt,
                    (uint)(Constants.TeleportFlags.SetLastToTarget | Constants.TeleportFlags.ViaHome));
            }
            else
            {
                // can't find the Home region: Tell viewer and abort
                client.SendTeleportFailed("Your home region could not be found.");
                return false;
            }
            return true;
        }

        #endregion


        #region Agent Crossings

        public GridRegion GetDestination(Scene scene, UUID agentID, Vector3 pos, out uint xDest, out uint yDest, out string version, out Vector3 newpos)
        {
            version = String.Empty;
            newpos = new Vector3(pos.X, pos.Y, pos.Z);
            uint neighbourx = scene.RegionInfo.RegionLocX;
            uint neighboury = scene.RegionInfo.RegionLocY;
            const float boundaryDistance = 1.7f;
            Vector3 northCross = new Vector3(0, boundaryDistance, 0);
            Vector3 southCross = new Vector3(0, -1 * boundaryDistance, 0);
            Vector3 eastCross = new Vector3(boundaryDistance, 0, 0);
            Vector3 westCross = new Vector3(-1 * boundaryDistance, 0, 0);

            // distance into new region to place avatar
            const float enterDistance = 0.5f;

            if (scene.TestBorderCross(pos + westCross, Cardinals.W))
            {
                if (scene.TestBorderCross(pos + northCross, Cardinals.N))
                {
                    Border b = scene.GetCrossedBorder(pos + northCross, Cardinals.N);
                    neighboury += (uint)(int)(b.BorderLine.Z / (int)Constants.RegionSize);
                }
                else if (scene.TestBorderCross(pos + southCross, Cardinals.S))
                {
                    neighboury--;
                    newpos.Y = Constants.RegionSize - enterDistance;
                }

                neighbourx--;
                newpos.X = Constants.RegionSize - enterDistance;

            }
            else if (scene.TestBorderCross(pos + eastCross, Cardinals.E))
            {
                Border b = scene.GetCrossedBorder(pos + eastCross, Cardinals.E);
                neighbourx += (uint)(int)(b.BorderLine.Z / (int)Constants.RegionSize);
                newpos.X = enterDistance;

                if (scene.TestBorderCross(pos + southCross, Cardinals.S))
                {
                    neighboury--;
                    newpos.Y = Constants.RegionSize - enterDistance;
                }
                else if (scene.TestBorderCross(pos + northCross, Cardinals.N))
                {
                    Border c = scene.GetCrossedBorder(pos + northCross, Cardinals.N);
                    neighboury += (uint)(int)(c.BorderLine.Z / (int)Constants.RegionSize);
                    newpos.Y = enterDistance;
                }
            }
            else if (scene.TestBorderCross(pos + southCross, Cardinals.S))
            {
                Border b = scene.GetCrossedBorder(pos + southCross, Cardinals.S);
                neighboury--;
                newpos.Y = Constants.RegionSize - enterDistance;
            }
            else if (scene.TestBorderCross(pos + northCross, Cardinals.N))
            {
                Border b = scene.GetCrossedBorder(pos + northCross, Cardinals.N);
                neighboury += (uint)(int)(b.BorderLine.Z / (int)Constants.RegionSize);
                newpos.Y = enterDistance;
            }

            /*

            if (pos.X < boundaryDistance) //West
            {
                neighbourx--;
                newpos.X = Constants.RegionSize - enterDistance;
            }
            else if (pos.X > Constants.RegionSize - boundaryDistance) // East
            {
                neighbourx++;
                newpos.X = enterDistance;
            }

            if (pos.Y < boundaryDistance) // South
            {
                neighboury--;
                newpos.Y = Constants.RegionSize - enterDistance;
            }
            else if (pos.Y > Constants.RegionSize - boundaryDistance) // North
            {
                neighboury++;
                newpos.Y = enterDistance;
            }
            */

            xDest = neighbourx;
            yDest = neighboury;

            int x = (int)(neighbourx * Constants.RegionSize), y = (int)(neighboury * Constants.RegionSize);

            ulong neighbourHandle = Utils.UIntsToLong((uint)x, (uint)y);

            ExpiringCache<ulong, DateTime> r;
            DateTime banUntil;

            if (m_bannedRegions.TryGetValue(agentID, out r))
            {
                if (r.TryGetValue(neighbourHandle, out banUntil))
                {
                    if (DateTime.Now < banUntil)
                        return null;
                    r.Remove(neighbourHandle);
                }
            }
            else
            {
                r = null;
            }

            GridRegion neighbourRegion = scene.GridService.GetRegionByPosition(scene.RegionInfo.ScopeID, (int)x, (int)y);

            string reason;
            if (!scene.SimulationService.QueryAccess(neighbourRegion, agentID, newpos, out version, out reason))
            {
                if (r == null)
                {
                    r = new ExpiringCache<ulong, DateTime>();
                    r.Add(neighbourHandle, DateTime.Now + TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));

                    m_bannedRegions.Add(agentID, r, TimeSpan.FromSeconds(45));
                }
                else
                {
                    r.Add(neighbourHandle, DateTime.Now + TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
                }
                return null;
            }

            return neighbourRegion;
        }

        public bool Cross(ScenePresence agent, bool isFlying)
        {
            uint x;
            uint y;
            Vector3 newpos;
            string version;

            GridRegion neighbourRegion = GetDestination(agent.Scene, agent.UUID, agent.AbsolutePosition, out x, out y, out version, out newpos);
            if (neighbourRegion == null)
            {
                agent.ControllingClient.SendAlertMessage("Cannot region cross into banned parcel");
                return false;
            }

            agent.IsInTransit = true;

            CrossAgentToNewRegionDelegate d = CrossAgentToNewRegionAsync;
            d.BeginInvoke(agent, newpos, x, y, neighbourRegion, isFlying, version, CrossAgentToNewRegionCompleted, d);

            return true;
        }


        public delegate void InformClientToInitateTeleportToLocationDelegate(ScenePresence agent, uint regionX, uint regionY,
                                                            Vector3 position,
                                                            Scene initiatingScene);

        private void InformClientToInitateTeleportToLocation(ScenePresence agent, uint regionX, uint regionY, Vector3 position, Scene initiatingScene)
        {

            // This assumes that we know what our neighbours are.

            InformClientToInitateTeleportToLocationDelegate d = InformClientToInitiateTeleportToLocationAsync;
            d.BeginInvoke(agent, regionX, regionY, position, initiatingScene,
                          InformClientToInitiateTeleportToLocationCompleted,
                          d);
        }

        public void InformClientToInitiateTeleportToLocationAsync(ScenePresence agent, uint regionX, uint regionY, Vector3 position,
            Scene initiatingScene)
        {
            Thread.Sleep(10000);
            IMessageTransferModule im = initiatingScene.RequestModuleInterface<IMessageTransferModule>();
            if (im != null)
            {
                UUID gotoLocation = Util.BuildFakeParcelID(
                    Util.UIntsToLong(
                                              (regionX *
                                               (uint)Constants.RegionSize),
                                              (regionY *
                                               (uint)Constants.RegionSize)),
                    (uint)(int)position.X,
                    (uint)(int)position.Y,
                    (uint)(int)position.Z);
                GridInstantMessage m = new GridInstantMessage(initiatingScene, UUID.Zero,
                "Region", agent.UUID,
                (byte)InstantMessageDialog.GodLikeRequestTeleport, false,
                "", gotoLocation, false, new Vector3(127, 0, 0),
                new Byte[0]);
                im.SendInstantMessage(m, delegate(bool success)
                {
                    m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Client Initiating Teleport sending IM success = {0}", success);
                });

            }
        }

        private void InformClientToInitiateTeleportToLocationCompleted(IAsyncResult iar)
        {
            InformClientToInitateTeleportToLocationDelegate icon =
                (InformClientToInitateTeleportToLocationDelegate)iar.AsyncState;
            icon.EndInvoke(iar);
        }

        /// <summary>
        /// This Closes child agents on neighbouring regions
        /// Calls an asynchronous method to do so..  so it doesn't lag the sim.
        /// </summary>
        public ScenePresence CrossAgentToNewRegionAsync(
            ScenePresence agent, Vector3 pos, uint neighbourx, uint neighboury, GridRegion neighbourRegion,
            bool isFlying, string version)
        {
            try
            {
                ulong neighbourHandle = Utils.UIntsToLong((uint)(neighbourx * Constants.RegionSize), (uint)(neighboury * Constants.RegionSize));
    
                m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Crossing agent {0} {1} to {2}-{3} running version {4}", agent.Firstname, agent.Lastname, neighbourx, neighboury, version);
    
                Scene m_scene = agent.Scene;
    
                if (neighbourRegion != null)
                {
                    if (!agent.ValidateAttachments())
                        m_log.DebugFormat(
                            "[ENTITY TRANSFER MODULE]: Failed validation of all attachments for region crossing of {0} from {1} to {2}.  Continuing.",
                            agent.Name, agent.Scene.RegionInfo.RegionName, neighbourRegion.RegionName);

                    pos = pos + agent.Velocity;
                    Vector3 vel2 = new Vector3(agent.Velocity.X, agent.Velocity.Y, 0);

                    agent.RemoveFromPhysicalScene();
                    SetInTransit(agent.UUID);

                    AgentData cAgent = new AgentData(); 
                    agent.CopyTo(cAgent);
                    cAgent.Position = pos;
                    if (isFlying)
                        cAgent.ControlFlags |= (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY;

                    // We don't need the callback anymnore
                    cAgent.CallbackURI = String.Empty;

                    if (!m_scene.SimulationService.UpdateAgent(neighbourRegion, cAgent))
                    {
                        // region doesn't take it
                        ReInstantiateScripts(agent);
                        agent.AddToPhysicalScene(isFlying);
                        ResetFromTransit(agent.UUID);
                        return agent;
                    }
    
                    //AgentCircuitData circuitdata = m_controllingClient.RequestClientInfo();
                    agent.ControllingClient.RequestClientInfo();
    
                    //m_log.Debug("BEFORE CROSS");
                    //Scene.DumpChildrenSeeds(UUID);
                    //DumpKnownRegions();
                    string agentcaps;
                    if (!agent.KnownRegions.TryGetValue(neighbourRegion.RegionHandle, out agentcaps))
                    {
                        m_log.ErrorFormat("[ENTITY TRANSFER MODULE]: No ENTITY TRANSFER MODULE information for region handle {0}, exiting CrossToNewRegion.",
                                         neighbourRegion.RegionHandle);
                        return agent;
                    }
                    // No turning back
                    agent.IsChildAgent = true;

                    string capsPath = neighbourRegion.ServerURI + CapsUtil.GetCapsSeedPath(agentcaps);
    
                    m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Sending new CAPS seed url {0} to client {1}", capsPath, agent.UUID);

                    IEventQueue eq = agent.Scene.RequestModuleInterface<IEventQueue>();
                    if (eq != null)
                    {
                        eq.CrossRegion(neighbourHandle, pos, vel2 /* agent.Velocity */, neighbourRegion.ExternalEndPoint,
                                       capsPath, agent.UUID, agent.ControllingClient.SessionId);
                    }
                    else
                    {
                        agent.ControllingClient.CrossRegion(neighbourHandle, pos, agent.Velocity, neighbourRegion.ExternalEndPoint,
                                                    capsPath);
                    }

                    // SUCCESS! 
                    agent.MakeChildAgent();
                    ResetFromTransit(agent.UUID);

                    // now we have a child agent in this region. Request all interesting data about other (root) agents
                    agent.SendOtherAgentsAvatarDataToMe();
                    agent.SendOtherAgentsAppearanceToMe();

                    // Backwards compatibility. Best effort
                    if (version == "Unknown" || version == string.Empty)
                    {
                        m_log.DebugFormat("[ENTITY TRANSFER MODULE]: neighbor with old version, passing attachments one by one...");
                        Thread.Sleep(3000); // wait a little now that we're not waiting for the callback
                        CrossAttachmentsIntoNewRegion(neighbourRegion, agent, true);
                    }


                    // Next, let's close the child agent connections that are too far away.
                    agent.CloseChildAgents(neighbourx, neighboury);
    
                    AgentHasMovedAway(agent, false);
    
                    // the user may change their profile information in other region,
                    // so the userinfo in UserProfileCache is not reliable any more, delete it
                    // REFACTORING PROBLEM. Well, not a problem, but this method is HORRIBLE!
                    if (agent.Scene.NeedSceneCacheClear(agent.UUID))
                    {
                        m_log.DebugFormat(
                            "[ENTITY TRANSFER MODULE]: User {0} is going to another region", agent.UUID);
                    }
                }
    
                //m_log.Debug("AFTER CROSS");
                //Scene.DumpChildrenSeeds(UUID);
                //DumpKnownRegions();
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[ENTITY TRANSFER MODULE]: Problem crossing user {0} to new region {1} from {2}.  Exception {3}{4}",
                    agent.Name, neighbourRegion.RegionName, agent.Scene.RegionInfo.RegionName, e.Message, e.StackTrace);
            }

            return agent;
        }

        private void CrossAgentToNewRegionCompleted(IAsyncResult iar)
        {
            CrossAgentToNewRegionDelegate icon = (CrossAgentToNewRegionDelegate)iar.AsyncState;
            ScenePresence agent = icon.EndInvoke(iar);

            //// If the cross was successful, this agent is a child agent
            //if (agent.IsChildAgent)
            //    agent.Reset();
            //else // Not successful
            //    agent.RestoreInCurrentScene();

            // In any case
            agent.IsInTransit = false;

            m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Crossing agent {0} {1} completed.", agent.Firstname, agent.Lastname);
        }

        #endregion

        #region Enable Child Agent

        /// <summary>
        /// This informs a single neighbouring region about agent "avatar".
        /// Calls an asynchronous method to do so..  so it doesn't lag the sim.
        /// </summary>
        /// <param name="sp"></param>
        /// <param name="region"></param>
        public void EnableChildAgent(ScenePresence sp, GridRegion region)
        {
            m_log.DebugFormat("[ENTITY TRANSFER]: Enabling child agent in new neighbour {0}", region.RegionName);

            AgentCircuitData currentAgentCircuit = sp.Scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);
            AgentCircuitData agent = sp.ControllingClient.RequestClientInfo();
            agent.BaseFolder = UUID.Zero;
            agent.InventoryFolder = UUID.Zero;
            agent.startpos = new Vector3(128, 128, 70);
            agent.child = true;
            agent.Appearance = sp.Appearance;
            agent.CapsPath = CapsUtil.GetRandomCapsObjectPath();

            agent.ChildrenCapSeeds = new Dictionary<ulong, string>(sp.Scene.CapsModule.GetChildrenSeeds(sp.UUID));
            //m_log.DebugFormat("[XXX] Seeds 1 {0}", agent.ChildrenCapSeeds.Count);

            if (!agent.ChildrenCapSeeds.ContainsKey(sp.Scene.RegionInfo.RegionHandle))
                agent.ChildrenCapSeeds.Add(sp.Scene.RegionInfo.RegionHandle, sp.ControllingClient.RequestClientInfo().CapsPath);
            //m_log.DebugFormat("[XXX] Seeds 2 {0}", agent.ChildrenCapSeeds.Count);

            sp.AddNeighbourRegion(region.RegionHandle, agent.CapsPath);
            //foreach (ulong h in agent.ChildrenCapSeeds.Keys)
            //    m_log.DebugFormat("[XXX] --> {0}", h);
            //m_log.DebugFormat("[XXX] Adding {0}", region.RegionHandle);
            agent.ChildrenCapSeeds.Add(region.RegionHandle, agent.CapsPath);

            if (sp.Scene.CapsModule != null)
            {
                sp.Scene.CapsModule.SetChildrenSeed(sp.UUID, agent.ChildrenCapSeeds);
            }

            if (currentAgentCircuit != null)
            {
                agent.ServiceURLs = currentAgentCircuit.ServiceURLs;
                agent.IPAddress = currentAgentCircuit.IPAddress;
                agent.Viewer = currentAgentCircuit.Viewer;
                agent.Channel = currentAgentCircuit.Channel;
                agent.Mac = currentAgentCircuit.Mac;
                agent.Id0 = currentAgentCircuit.Id0;
            }

            IPEndPoint external = region.ExternalEndPoint;
            if (external != null)
            {
                InformClientOfNeighbourDelegate d = InformClientOfNeighbourAsync;
                d.BeginInvoke(sp, agent, region, external, true,
                          InformClientOfNeighbourCompleted,
                          d);
            }
        }
        #endregion

        #region Enable Child Agents

        private delegate void InformClientOfNeighbourDelegate(
            ScenePresence avatar, AgentCircuitData a, GridRegion reg, IPEndPoint endPoint, bool newAgent);

        /// <summary>
        /// This informs all neighbouring regions about agent "avatar".
        /// </summary>
        /// <param name="sp"></param>
        public void EnableChildAgents(ScenePresence sp)
        {
            List<GridRegion> neighbours = new List<GridRegion>();
            RegionInfo m_regionInfo = sp.Scene.RegionInfo;

            if (m_regionInfo != null)
            {
                neighbours = RequestNeighbours(sp, m_regionInfo.RegionLocX, m_regionInfo.RegionLocY);
            }
            else
            {
                m_log.Debug("[ENTITY TRANSFER MODULE]: m_regionInfo was null in EnableChildAgents, is this a NPC?");
            }

            /// We need to find the difference between the new regions where there are no child agents
            /// and the regions where there are already child agents. We only send notification to the former.
            List<ulong> neighbourHandles = NeighbourHandles(neighbours); // on this region
            neighbourHandles.Add(sp.Scene.RegionInfo.RegionHandle);  // add this region too
            List<ulong> previousRegionNeighbourHandles;

            if (sp.Scene.CapsModule != null)
            {
                previousRegionNeighbourHandles =
                    new List<ulong>(sp.Scene.CapsModule.GetChildrenSeeds(sp.UUID).Keys);
            }
            else
            {
                previousRegionNeighbourHandles = new List<ulong>();
            }

            List<ulong> newRegions = NewNeighbours(neighbourHandles, previousRegionNeighbourHandles);
            List<ulong> oldRegions = OldNeighbours(neighbourHandles, previousRegionNeighbourHandles);

            //Dump("Current Neighbors", neighbourHandles);
            //Dump("Previous Neighbours", previousRegionNeighbourHandles);
            //Dump("New Neighbours", newRegions);
            //Dump("Old Neighbours", oldRegions);

            /// Update the scene presence's known regions here on this region
            sp.DropOldNeighbours(oldRegions);

            /// Collect as many seeds as possible
            Dictionary<ulong, string> seeds;
            if (sp.Scene.CapsModule != null)
                seeds
                    = new Dictionary<ulong, string>(sp.Scene.CapsModule.GetChildrenSeeds(sp.UUID));
            else
                seeds = new Dictionary<ulong, string>();

            //m_log.Debug(" !!! No. of seeds: " + seeds.Count);
            if (!seeds.ContainsKey(sp.Scene.RegionInfo.RegionHandle))
                seeds.Add(sp.Scene.RegionInfo.RegionHandle, sp.ControllingClient.RequestClientInfo().CapsPath);

            /// Create the necessary child agents
            List<AgentCircuitData> cagents = new List<AgentCircuitData>();
            foreach (GridRegion neighbour in neighbours)
            {
                if (neighbour.RegionHandle != sp.Scene.RegionInfo.RegionHandle)
                {

                    AgentCircuitData currentAgentCircuit = sp.Scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);
                    AgentCircuitData agent = sp.ControllingClient.RequestClientInfo();
                    agent.BaseFolder = UUID.Zero;
                    agent.InventoryFolder = UUID.Zero;
                    agent.startpos = sp.AbsolutePosition + CalculateOffset(sp, neighbour);
                    agent.child = true;
                    agent.Appearance = sp.Appearance;
                    if (currentAgentCircuit != null)
                    {
                        agent.ServiceURLs = currentAgentCircuit.ServiceURLs;
                        agent.IPAddress = currentAgentCircuit.IPAddress;
                        agent.Viewer = currentAgentCircuit.Viewer;
                        agent.Channel = currentAgentCircuit.Channel;
                        agent.Mac = currentAgentCircuit.Mac;
                        agent.Id0 = currentAgentCircuit.Id0;
                    }

                    if (newRegions.Contains(neighbour.RegionHandle))
                    {
                        agent.CapsPath = CapsUtil.GetRandomCapsObjectPath();
                        sp.AddNeighbourRegion(neighbour.RegionHandle, agent.CapsPath);
                        seeds.Add(neighbour.RegionHandle, agent.CapsPath);
                    }
                    else
                        agent.CapsPath = sp.Scene.CapsModule.GetChildSeed(sp.UUID, neighbour.RegionHandle);

                    cagents.Add(agent);
                }
            }

            /// Update all child agent with everyone's seeds
            foreach (AgentCircuitData a in cagents)
            {
                a.ChildrenCapSeeds = new Dictionary<ulong, string>(seeds);
            }

            if (sp.Scene.CapsModule != null)
            {
                sp.Scene.CapsModule.SetChildrenSeed(sp.UUID, seeds);
            }
            sp.KnownRegions = seeds;
            //avatar.Scene.DumpChildrenSeeds(avatar.UUID);
            //avatar.DumpKnownRegions();

            bool newAgent = false;
            int count = 0;
            foreach (GridRegion neighbour in neighbours)
            {
                //m_log.WarnFormat("--> Going to send child agent to {0}", neighbour.RegionName);
                // Don't do it if there's already an agent in that region
                if (newRegions.Contains(neighbour.RegionHandle))
                    newAgent = true;
                else
                    newAgent = false;

                if (neighbour.RegionHandle != sp.Scene.RegionInfo.RegionHandle)
                {
                    try
                    {
                        // Let's put this back at sync, so that it doesn't clog 
                        // the network, especially for regions in the same physical server.
                        // We're really not in a hurry here.
                        InformClientOfNeighbourAsync(sp, cagents[count], neighbour, neighbour.ExternalEndPoint, newAgent);
                        //InformClientOfNeighbourDelegate d = InformClientOfNeighbourAsync;
                        //d.BeginInvoke(sp, cagents[count], neighbour, neighbour.ExternalEndPoint, newAgent,
                        //              InformClientOfNeighbourCompleted,
                        //              d);
                    }

                    catch (ArgumentOutOfRangeException)
                    {
                        m_log.ErrorFormat(
                           "[ENTITY TRANSFER MODULE]: Neighbour Regions response included the current region in the neighbour list.  The following region will not display to the client: {0} for region {1} ({2}, {3}).",
                           neighbour.ExternalHostName,
                           neighbour.RegionHandle,
                           neighbour.RegionLocX,
                           neighbour.RegionLocY);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[ENTITY TRANSFER MODULE]: Could not resolve external hostname {0} for region {1} ({2}, {3}).  {4}",
                            neighbour.ExternalHostName,
                            neighbour.RegionHandle,
                            neighbour.RegionLocX,
                            neighbour.RegionLocY,
                            e);

                        // FIXME: Okay, even though we've failed, we're still going to throw the exception on,
                        // since I don't know what will happen if we just let the client continue

                        // XXX: Well, decided to swallow the exception instead for now.  Let us see how that goes.
                        // throw e;

                    }
                }
                count++;
            }
        }

        Vector3 CalculateOffset(ScenePresence sp, GridRegion neighbour)
        {
            int rRegionX = (int)sp.Scene.RegionInfo.RegionLocX;
            int rRegionY = (int)sp.Scene.RegionInfo.RegionLocY;
            int tRegionX = neighbour.RegionLocX / (int)Constants.RegionSize;
            int tRegionY = neighbour.RegionLocY / (int)Constants.RegionSize;
            int shiftx = (rRegionX - tRegionX) * (int)Constants.RegionSize;
            int shifty = (rRegionY - tRegionY) * (int)Constants.RegionSize;
            return new Vector3(shiftx, shifty, 0f);
        }

        private void InformClientOfNeighbourCompleted(IAsyncResult iar)
        {
            InformClientOfNeighbourDelegate icon = (InformClientOfNeighbourDelegate)iar.AsyncState;
            icon.EndInvoke(iar);
            //m_log.WarnFormat(" --> InformClientOfNeighbourCompleted");
        }

        /// <summary>
        /// Async component for informing client of which neighbours exist
        /// </summary>
        /// <remarks>
        /// This needs to run asynchronously, as a network timeout may block the thread for a long while
        /// </remarks>
        /// <param name="remoteClient"></param>
        /// <param name="a"></param>
        /// <param name="regionHandle"></param>
        /// <param name="endPoint"></param>
        private void InformClientOfNeighbourAsync(ScenePresence sp, AgentCircuitData a, GridRegion reg,
                                                  IPEndPoint endPoint, bool newAgent)
        {
            // Let's wait just a little to give time to originating regions to catch up with closing child agents
            // after a cross here
            Thread.Sleep(500);

            Scene m_scene = sp.Scene;

            uint x, y;
            Utils.LongToUInts(reg.RegionHandle, out x, out y);
            x = x / Constants.RegionSize;
            y = y / Constants.RegionSize;
            m_log.Debug("[ENTITY TRANSFER MODULE]: Starting to inform client about neighbour " + x + ", " + y + "(" + endPoint + ")");

            string capsPath = reg.ServerURI + CapsUtil.GetCapsSeedPath(a.CapsPath);

            string reason = String.Empty;

            bool regionAccepted = m_scene.SimulationService.CreateAgent(reg, a, (uint)TeleportFlags.Default, out reason); 

            if (regionAccepted && newAgent)
            {
                IEventQueue eq = sp.Scene.RequestModuleInterface<IEventQueue>();
                if (eq != null)
                {
                    #region IP Translation for NAT
                    IClientIPEndpoint ipepClient;
                    if (sp.ClientView.TryGet(out ipepClient))
                    {
                        endPoint.Address = NetworkUtil.GetIPFor(ipepClient.EndPoint, endPoint.Address);
                    }
                    #endregion

                    m_log.DebugFormat("[ENTITY TRANSFER MODULE]: {0} is sending {1} EnableSimulator for neighbour region {2} @ {3} " +
                        "and EstablishAgentCommunication with seed cap {4}",
                        m_scene.RegionInfo.RegionName, sp.Name, reg.RegionName, reg.RegionHandle, capsPath);

                    eq.EnableSimulator(reg.RegionHandle, endPoint, sp.UUID);
                    eq.EstablishAgentCommunication(sp.UUID, endPoint, capsPath);
                }
                else
                {
                    sp.ControllingClient.InformClientOfNeighbour(reg.RegionHandle, endPoint);
                    // TODO: make Event Queue disablable!
                }

                m_log.Debug("[ENTITY TRANSFER MODULE]: Completed inform client about neighbour " + endPoint.ToString());
            }
        }

        /// <summary>
        /// Return the list of regions that are considered to be neighbours to the given scene.
        /// </summary>
        /// <param name="pScene"></param>
        /// <param name="pRegionLocX"></param>
        /// <param name="pRegionLocY"></param>
        /// <returns></returns>        
        protected List<GridRegion> RequestNeighbours(ScenePresence avatar, uint pRegionLocX, uint pRegionLocY)
        {
            Scene pScene = avatar.Scene;
            RegionInfo m_regionInfo = pScene.RegionInfo;

            Border[] northBorders = pScene.NorthBorders.ToArray();
            Border[] southBorders = pScene.SouthBorders.ToArray();
            Border[] eastBorders = pScene.EastBorders.ToArray();
            Border[] westBorders = pScene.WestBorders.ToArray();

            // Leaving this as a "megaregions" computation vs "non-megaregions" computation; it isn't
            // clear what should be done with a "far view" given that megaregions already extended the
            // view to include everything in the megaregion
            if (northBorders.Length <= 1 && southBorders.Length <= 1 && eastBorders.Length <= 1 && westBorders.Length <= 1)
            {
                int dd = avatar.DrawDistance < Constants.RegionSize ? (int)Constants.RegionSize : (int)avatar.DrawDistance;

                int startX = (int)pRegionLocX * (int)Constants.RegionSize - dd + (int)(Constants.RegionSize/2);
                int startY = (int)pRegionLocY * (int)Constants.RegionSize - dd + (int)(Constants.RegionSize/2);

                int endX = (int)pRegionLocX * (int)Constants.RegionSize + dd + (int)(Constants.RegionSize/2);
                int endY = (int)pRegionLocY * (int)Constants.RegionSize + dd + (int)(Constants.RegionSize/2);

                List<GridRegion> neighbours =
                    avatar.Scene.GridService.GetRegionRange(m_regionInfo.ScopeID, startX, endX, startY, endY);

                neighbours.RemoveAll(delegate(GridRegion r) { return r.RegionID == m_regionInfo.RegionID; });
                return neighbours;
            }
            else
            {
                Vector2 extent = Vector2.Zero;
                for (int i = 0; i < eastBorders.Length; i++)
                {
                    extent.X = (eastBorders[i].BorderLine.Z > extent.X) ? eastBorders[i].BorderLine.Z : extent.X;
                }
                for (int i = 0; i < northBorders.Length; i++)
                {
                    extent.Y = (northBorders[i].BorderLine.Z > extent.Y) ? northBorders[i].BorderLine.Z : extent.Y;
                }

                // Loss of fraction on purpose
                extent.X = ((int)extent.X / (int)Constants.RegionSize) + 1;
                extent.Y = ((int)extent.Y / (int)Constants.RegionSize) + 1;

                int startX = (int)(pRegionLocX - 1) * (int)Constants.RegionSize;
                int startY = (int)(pRegionLocY - 1) * (int)Constants.RegionSize;

                int endX = ((int)pRegionLocX + (int)extent.X) * (int)Constants.RegionSize;
                int endY = ((int)pRegionLocY + (int)extent.Y) * (int)Constants.RegionSize;

                List<GridRegion> neighbours = pScene.GridService.GetRegionRange(m_regionInfo.ScopeID, startX, endX, startY, endY);
                neighbours.RemoveAll(delegate(GridRegion r) { return r.RegionID == m_regionInfo.RegionID; });

                return neighbours;
            }
        }

        private List<ulong> NewNeighbours(List<ulong> currentNeighbours, List<ulong> previousNeighbours)
        {
            return currentNeighbours.FindAll(delegate(ulong handle) { return !previousNeighbours.Contains(handle); });
        }

        //        private List<ulong> CommonNeighbours(List<ulong> currentNeighbours, List<ulong> previousNeighbours)
        //        {
        //            return currentNeighbours.FindAll(delegate(ulong handle) { return previousNeighbours.Contains(handle); });
        //        }

        private List<ulong> OldNeighbours(List<ulong> currentNeighbours, List<ulong> previousNeighbours)
        {
            return previousNeighbours.FindAll(delegate(ulong handle) { return !currentNeighbours.Contains(handle); });
        }

        private List<ulong> NeighbourHandles(List<GridRegion> neighbours)
        {
            List<ulong> handles = new List<ulong>();
            foreach (GridRegion reg in neighbours)
            {
                handles.Add(reg.RegionHandle);
            }
            return handles;
        }

//        private void Dump(string msg, List<ulong> handles)
//        {
//            m_log.InfoFormat("-------------- HANDLE DUMP ({0}) ---------", msg);
//            foreach (ulong handle in handles)
//            {
//                uint x, y;
//                Utils.LongToUInts(handle, out x, out y);
//                x = x / Constants.RegionSize;
//                y = y / Constants.RegionSize;
//                m_log.InfoFormat("({0}, {1})", x, y);
//            }
//        }

        #endregion


        #region Agent Arrived
        public void AgentArrivedAtDestination(UUID id)
        {
            m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Agent {0} released", id);
            ResetFromTransit(id);
        }

        #endregion

        #region Object Transfers
        /// <summary>
        /// Move the given scene object into a new region depending on which region its absolute position has moved
        /// into.
        ///
        /// This method locates the new region handle and offsets the prim position for the new region
        /// </summary>
        /// <param name="attemptedPosition">the attempted out of region position of the scene object</param>
        /// <param name="grp">the scene object that we're crossing</param>
        public void Cross(SceneObjectGroup grp, Vector3 attemptedPosition, bool silent)
        {
            if (grp == null)
                return;
            if (grp.IsDeleted)
                return;

            Scene scene = grp.Scene;
            if (scene == null)
                return;

            if (grp.RootPart.DIE_AT_EDGE)
            {
                // We remove the object here
                try
                {
                    scene.DeleteSceneObject(grp, false);
                }
                catch (Exception)
                {
                    m_log.Warn("[DATABASE]: exception when trying to remove the prim that crossed the border.");
                }
                return;
            }

            int thisx = (int)scene.RegionInfo.RegionLocX;
            int thisy = (int)scene.RegionInfo.RegionLocY;
            Vector3 EastCross = new Vector3(0.1f, 0, 0);
            Vector3 WestCross = new Vector3(-0.1f, 0, 0);
            Vector3 NorthCross = new Vector3(0, 0.1f, 0);
            Vector3 SouthCross = new Vector3(0, -0.1f, 0);


            // use this if no borders were crossed!
            ulong newRegionHandle
                        = Util.UIntsToLong((uint)((thisx) * Constants.RegionSize),
                                           (uint)((thisy) * Constants.RegionSize));

            Vector3 pos = attemptedPosition;

            int changeX = 1;
            int changeY = 1;

            if (scene.TestBorderCross(attemptedPosition + WestCross, Cardinals.W))
            {
                if (scene.TestBorderCross(attemptedPosition + SouthCross, Cardinals.S))
                {

                    Border crossedBorderx = scene.GetCrossedBorder(attemptedPosition + WestCross, Cardinals.W);

                    if (crossedBorderx.BorderLine.Z > 0)
                    {
                        pos.X = ((pos.X + crossedBorderx.BorderLine.Z));
                        changeX = (int)(crossedBorderx.BorderLine.Z / (int)Constants.RegionSize);
                    }
                    else
                        pos.X = ((pos.X + Constants.RegionSize));

                    Border crossedBordery = scene.GetCrossedBorder(attemptedPosition + SouthCross, Cardinals.S);
                    //(crossedBorderx.BorderLine.Z / (int)Constants.RegionSize)

                    if (crossedBordery.BorderLine.Z > 0)
                    {
                        pos.Y = ((pos.Y + crossedBordery.BorderLine.Z));
                        changeY = (int)(crossedBordery.BorderLine.Z / (int)Constants.RegionSize);
                    }
                    else
                        pos.Y = ((pos.Y + Constants.RegionSize));



                    newRegionHandle
                        = Util.UIntsToLong((uint)((thisx - changeX) * Constants.RegionSize),
                                           (uint)((thisy - changeY) * Constants.RegionSize));
                    // x - 1
                    // y - 1
                }
                else if (scene.TestBorderCross(attemptedPosition + NorthCross, Cardinals.N))
                {
                    Border crossedBorderx = scene.GetCrossedBorder(attemptedPosition + WestCross, Cardinals.W);

                    if (crossedBorderx.BorderLine.Z > 0)
                    {
                        pos.X = ((pos.X + crossedBorderx.BorderLine.Z));
                        changeX = (int)(crossedBorderx.BorderLine.Z / (int)Constants.RegionSize);
                    }
                    else
                        pos.X = ((pos.X + Constants.RegionSize));


                    Border crossedBordery = scene.GetCrossedBorder(attemptedPosition + SouthCross, Cardinals.S);
                    //(crossedBorderx.BorderLine.Z / (int)Constants.RegionSize)

                    if (crossedBordery.BorderLine.Z > 0)
                    {
                        pos.Y = ((pos.Y + crossedBordery.BorderLine.Z));
                        changeY = (int)(crossedBordery.BorderLine.Z / (int)Constants.RegionSize);
                    }
                    else
                        pos.Y = ((pos.Y + Constants.RegionSize));

                    newRegionHandle
                        = Util.UIntsToLong((uint)((thisx - changeX) * Constants.RegionSize),
                                           (uint)((thisy + changeY) * Constants.RegionSize));
                    // x - 1
                    // y + 1
                }
                else
                {
                    Border crossedBorderx = scene.GetCrossedBorder(attemptedPosition + WestCross, Cardinals.W);

                    if (crossedBorderx.BorderLine.Z > 0)
                    {
                        pos.X = ((pos.X + crossedBorderx.BorderLine.Z));
                        changeX = (int)(crossedBorderx.BorderLine.Z / (int)Constants.RegionSize);
                    }
                    else
                        pos.X = ((pos.X + Constants.RegionSize));

                    newRegionHandle
                        = Util.UIntsToLong((uint)((thisx - changeX) * Constants.RegionSize),
                                           (uint)(thisy * Constants.RegionSize));
                    // x - 1
                }
            }
            else if (scene.TestBorderCross(attemptedPosition + EastCross, Cardinals.E))
            {
                if (scene.TestBorderCross(attemptedPosition + SouthCross, Cardinals.S))
                {

                    pos.X = ((pos.X - Constants.RegionSize));
                    Border crossedBordery = scene.GetCrossedBorder(attemptedPosition + SouthCross, Cardinals.S);
                    //(crossedBorderx.BorderLine.Z / (int)Constants.RegionSize)

                    if (crossedBordery.BorderLine.Z > 0)
                    {
                        pos.Y = ((pos.Y + crossedBordery.BorderLine.Z));
                        changeY = (int)(crossedBordery.BorderLine.Z / (int)Constants.RegionSize);
                    }
                    else
                        pos.Y = ((pos.Y + Constants.RegionSize));


                    newRegionHandle
                        = Util.UIntsToLong((uint)((thisx + changeX) * Constants.RegionSize),
                                           (uint)((thisy - changeY) * Constants.RegionSize));
                    // x + 1
                    // y - 1
                }
                else if (scene.TestBorderCross(attemptedPosition + NorthCross, Cardinals.N))
                {
                    pos.X = ((pos.X - Constants.RegionSize));
                    pos.Y = ((pos.Y - Constants.RegionSize));
                    newRegionHandle
                        = Util.UIntsToLong((uint)((thisx + changeX) * Constants.RegionSize),
                                           (uint)((thisy + changeY) * Constants.RegionSize));
                    // x + 1
                    // y + 1
                }
                else
                {
                    pos.X = ((pos.X - Constants.RegionSize));
                    newRegionHandle
                        = Util.UIntsToLong((uint)((thisx + changeX) * Constants.RegionSize),
                                           (uint)(thisy * Constants.RegionSize));
                    // x + 1
                }
            }
            else if (scene.TestBorderCross(attemptedPosition + SouthCross, Cardinals.S))
            {
                Border crossedBordery = scene.GetCrossedBorder(attemptedPosition + SouthCross, Cardinals.S);
                //(crossedBorderx.BorderLine.Z / (int)Constants.RegionSize)

                if (crossedBordery.BorderLine.Z > 0)
                {
                    pos.Y = ((pos.Y + crossedBordery.BorderLine.Z));
                    changeY = (int)(crossedBordery.BorderLine.Z / (int)Constants.RegionSize);
                }
                else
                    pos.Y = ((pos.Y + Constants.RegionSize));

                newRegionHandle
                    = Util.UIntsToLong((uint)(thisx * Constants.RegionSize), (uint)((thisy - changeY) * Constants.RegionSize));
                // y - 1
            }
            else if (scene.TestBorderCross(attemptedPosition + NorthCross, Cardinals.N))
            {

                pos.Y = ((pos.Y - Constants.RegionSize));
                newRegionHandle
                    = Util.UIntsToLong((uint)(thisx * Constants.RegionSize), (uint)((thisy + changeY) * Constants.RegionSize));
                // y + 1
            }

            // Offset the positions for the new region across the border
            Vector3 oldGroupPosition = grp.RootPart.GroupPosition;

            // If we fail to cross the border, then reset the position of the scene object on that border.
            uint x = 0, y = 0;
            Utils.LongToUInts(newRegionHandle, out x, out y);
            GridRegion destination = scene.GridService.GetRegionByPosition(scene.RegionInfo.ScopeID, (int)x, (int)y);

            if (destination != null)
            {
                if (CrossPrimGroupIntoNewRegion(destination, pos, grp, silent))
                    return; // we did it
            }

            // no one or failed lets go back and tell physics to go on
            oldGroupPosition.X = Util.Clamp<float>(oldGroupPosition.X, 0.5f, (float)Constants.RegionSize - 0.5f);
            oldGroupPosition.Y = Util.Clamp<float>(oldGroupPosition.Y, 0.5f, (float)Constants.RegionSize - 0.5f);
            oldGroupPosition.Z = Util.Clamp<float>(oldGroupPosition.Z, 0.5f, 4096.0f);

            grp.AbsolutePosition = oldGroupPosition;
            grp.Velocity = Vector3.Zero;

            if (grp.RootPart.PhysActor != null)
                grp.RootPart.PhysActor.CrossingFailure();

            grp.ScheduleGroupForFullUpdate();
        }



        /// <summary>
        /// Move the given scene object into a new region
        /// </summary>
        /// <param name="newRegionHandle"></param>
        /// <param name="grp">Scene Object Group that we're crossing</param>
        /// <returns>
        /// true if the crossing itself was successful, false on failure
        /// FIMXE: we still return true if the crossing object was not successfully deleted from the originating region
        /// </returns>
        protected bool CrossPrimGroupIntoNewRegion(GridRegion destination, Vector3 newPosition, SceneObjectGroup grp, bool silent)
        {
            //m_log.Debug("  >>> CrossPrimGroupIntoNewRegion <<<");

            bool successYN = false;
            grp.RootPart.ClearUpdateSchedule();
            //int primcrossingXMLmethod = 0;

            if (destination != null)
            {
                //string objectState = grp.GetStateSnapshot();

                //successYN
                //    = m_sceneGridService.PrimCrossToNeighboringRegion(
                //        newRegionHandle, grp.UUID, m_serialiser.SaveGroupToXml2(grp), primcrossingXMLmethod);
                //if (successYN && (objectState != "") && m_allowScriptCrossings)
                //{
                //    successYN = m_sceneGridService.PrimCrossToNeighboringRegion(
                //            newRegionHandle, grp.UUID, objectState, 100);
                //}

                //// And the new channel...
                //if (m_interregionCommsOut != null)
                //    successYN = m_interregionCommsOut.SendCreateObject(newRegionHandle, grp, true);
                if (m_aScene.SimulationService != null)
                    successYN = m_aScene.SimulationService.CreateObject(destination, newPosition, grp, true);

                if (successYN)
                {
                    // We remove the object here
                    try
                    {
                        grp.Scene.DeleteSceneObject(grp, silent);
                    }
                    catch (Exception e)
                    {
                        m_log.ErrorFormat(
                            "[ENTITY TRANSFER MODULE]: Exception deleting the old object left behind on a border crossing for {0}, {1}",
                            grp, e);
                    }
                }
                else
                {
                    if (!grp.IsDeleted)
                    {
                        PhysicsActor pa = grp.RootPart.PhysActor;
                        if (pa != null)
                        {
                            pa.CrossingFailure();
                            if (grp.RootPart.KeyframeMotion != null)
                            {
                                grp.RootPart.Velocity = Vector3.Zero;
                                grp.RootPart.KeyframeMotion.CrossingFailure();
                                grp.SendGroupRootTerseUpdate();
                            }
                        }
                    }

                    m_log.ErrorFormat("[ENTITY TRANSFER MODULE]: Prim crossing failed for {0}", grp);
                }
            }
            else
            {
                m_log.Error("[ENTITY TRANSFER MODULE]: destination was unexpectedly null in Scene.CrossPrimGroupIntoNewRegion()");
            }

            return successYN;
        }

        protected bool CrossAttachmentsIntoNewRegion(GridRegion destination, ScenePresence sp, bool silent)
        {
            List<SceneObjectGroup> m_attachments = sp.GetAttachments();

            // Validate
//            foreach (SceneObjectGroup gobj in m_attachments)
//            {
//                if (gobj == null || gobj.IsDeleted)
//                    return false;
//            }

            foreach (SceneObjectGroup gobj in m_attachments)
            {
                // If the prim group is null then something must have happened to it!
                if (gobj != null && !gobj.IsDeleted)
                {
                    // Set the parent localID to 0 so it transfers over properly.
                    gobj.RootPart.SetParentLocalId(0);
                    gobj.AbsolutePosition = gobj.RootPart.AttachedPos;
                    gobj.IsAttachment = false;
                    //gobj.RootPart.LastOwnerID = gobj.GetFromAssetID();
                    m_log.DebugFormat("[ENTITY TRANSFER MODULE]: Sending attachment {0} to region {1}", gobj.UUID, destination.RegionName);
                    CrossPrimGroupIntoNewRegion(destination, Vector3.Zero, gobj, silent);
                }
            }

            sp.ClearAttachments();

            return true;
        }

        #endregion

        #region Misc

        protected bool WaitForCallback(UUID id)
        {
            int count = 200;
            while (m_agentsInTransit.Contains(id) && count-- > 0)
            {
                //m_log.Debug("  >>> Waiting... " + count);
                Thread.Sleep(100);
            }

            if (count > 0)
                return true;
            else
                return false;
        }

        protected void SetInTransit(UUID id)
        {
            lock (m_agentsInTransit)
            {
                if (!m_agentsInTransit.Contains(id))
                    m_agentsInTransit.Add(id);
            }
        }

        protected bool IsInTransit(UUID id)
        {
            lock (m_agentsInTransit)
            {
                if (m_agentsInTransit.Contains(id))
                    return true;
            }
            return false;
        }

        protected bool ResetFromTransit(UUID id)
        {
            lock (m_agentsInTransit)
            {
                if (m_agentsInTransit.Contains(id))
                {
                    m_agentsInTransit.Remove(id);
                    return true;
                }
            }
            return false;
        }

        protected void ReInstantiateScripts(ScenePresence sp)
        {
            int i = 0;
            if (sp.InTransitScriptStates.Count > 0)
            {
                List<SceneObjectGroup> attachments = sp.GetAttachments();

                foreach (SceneObjectGroup sog in attachments)
                {
                    if (i < sp.InTransitScriptStates.Count)
                    {
                        sog.SetState(sp.InTransitScriptStates[i++], sp.Scene);
                        sog.CreateScriptInstances(0, false, sp.Scene.DefaultScriptEngine, 0);
                        sog.ResumeScripts();
                    }
                    else
                        m_log.ErrorFormat(
                            "[ENTITY TRANSFER MODULE]: InTransitScriptStates.Count={0} smaller than Attachments.Count={1}",
                            sp.InTransitScriptStates.Count, attachments.Count);
                }

                sp.InTransitScriptStates.Clear();
            }
        }
        #endregion

    }
}
