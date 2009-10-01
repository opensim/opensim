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
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Capabilities;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.Framework.Scenes.Hypergrid
{
    public class HGSceneCommunicationService : SceneCommunicationService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IHyperlinkService m_hg;
        IHyperlinkService HyperlinkService
        {
            get
            {
                if (m_hg == null)
                    m_hg = m_scene.RequestModuleInterface<IHyperlinkService>();
                return m_hg;
            }
        }

        public HGSceneCommunicationService(CommunicationsManager commsMan) : base(commsMan)
        {
        }


        /// <summary>
        /// Try to teleport an agent to a new region.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="RegionHandle"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="flags"></param>
        public override void RequestTeleportToLocation(ScenePresence avatar, ulong regionHandle, Vector3 position,
                                                      Vector3 lookAt, uint teleportFlags)
        {
            if (!avatar.Scene.Permissions.CanTeleport(avatar.UUID))
                return;

            bool destRegionUp = true;

            IEventQueue eq = avatar.Scene.RequestModuleInterface<IEventQueue>();

            // Reset animations; the viewer does that in teleports.
            avatar.ResetAnimations();

            if (regionHandle == m_regionInfo.RegionHandle)
            {
                // Teleport within the same region
                if (IsOutsideRegion(avatar.Scene, position) || position.Z < 0)
                {
                    Vector3 emergencyPos = new Vector3(128, 128, 128);

                    m_log.WarnFormat(
                        "[HGSceneCommService]: RequestTeleportToLocation() was given an illegal position of {0} for avatar {1}, {2}.  Substituting {3}",
                        position, avatar.Name, avatar.UUID, emergencyPos);
                    position = emergencyPos;
                }
                // TODO: Get proper AVG Height
                float localAVHeight = 1.56f;
                
                float posZLimit = 22;

                if (position.X > 0 && position.X <= (int)Constants.RegionSize && position.Y > 0 && position.Y <= (int)Constants.RegionSize)
                {
                    posZLimit = (float) avatar.Scene.Heightmap[(int) position.X, (int) position.Y];
                }

                float newPosZ = posZLimit + localAVHeight;
                if (posZLimit >= (position.Z - (localAVHeight / 2)) && !(Single.IsInfinity(newPosZ) || Single.IsNaN(newPosZ)))
                {
                    position.Z = newPosZ;
                }

                // Only send this if the event queue is null
                if (eq == null)
                    avatar.ControllingClient.SendTeleportLocationStart();


                avatar.ControllingClient.SendLocalTeleport(position, lookAt, teleportFlags);
                avatar.Teleport(position);
            }
            else
            {
                uint x = 0, y = 0;
                Utils.LongToUInts(regionHandle, out x, out y);
                GridRegion reg = m_scene.GridService.GetRegionByPosition(m_scene.RegionInfo.ScopeID, (int)x, (int)y); 

                if (reg != null)
                {

                    uint newRegionX = (uint)(reg.RegionHandle >> 40);
                    uint newRegionY = (((uint)(reg.RegionHandle)) >> 8);
                    uint oldRegionX = (uint)(m_regionInfo.RegionHandle >> 40);
                    uint oldRegionY = (((uint)(m_regionInfo.RegionHandle)) >> 8);

                    ///
                    /// Hypergrid mod start
                    /// 
                    ///
                    bool isHyperLink = (HyperlinkService.GetHyperlinkRegion(reg.RegionHandle) != null);
                    bool isHomeUser = true;
                    ulong realHandle = regionHandle;
                    CachedUserInfo uinfo = m_commsProvider.UserProfileCacheService.GetUserDetails(avatar.UUID);
                    if (uinfo != null)
                    {
                        isHomeUser = HyperlinkService.IsLocalUser(uinfo.UserProfile.ID);
                        realHandle = m_hg.FindRegionHandle(regionHandle);
                        m_log.Debug("XXX ---- home user? " + isHomeUser + " --- hyperlink? " + isHyperLink + " --- real handle: " + realHandle.ToString());
                    }
                    ///
                    /// Hypergrid mod stop
                    /// 
                    ///

                    if (eq == null)
                        avatar.ControllingClient.SendTeleportLocationStart();


                    // Let's do DNS resolution only once in this process, please!
                    // This may be a costly operation. The reg.ExternalEndPoint field is not a passive field,
                    // it's actually doing a lot of work.
                    IPEndPoint endPoint = reg.ExternalEndPoint;
                    if (endPoint.Address == null)
                    {
                        // Couldn't resolve the name. Can't TP, because the viewer wants IP addresses.
                        destRegionUp = false;
                    }

                    if (destRegionUp)
                    {
                        // Fixing a bug where teleporting while sitting results in the avatar ending up removed from
                        // both regions
                        if (avatar.ParentID != (uint)0)
                            avatar.StandUp();
                        
                        if (!avatar.ValidateAttachments())
                        {
                            avatar.ControllingClient.SendTeleportFailed("Inconsistent attachment state");
                            return;
                        }

                        // the avatar.Close below will clear the child region list. We need this below for (possibly)
                        // closing the child agents, so save it here (we need a copy as it is Clear()-ed).
                        //List<ulong> childRegions = new List<ulong>(avatar.GetKnownRegionList());
                        // Compared to ScenePresence.CrossToNewRegion(), there's no obvious code to handle a teleport
                        // failure at this point (unlike a border crossing failure).  So perhaps this can never fail
                        // once we reach here...
                        //avatar.Scene.RemoveCapsHandler(avatar.UUID);

                        string capsPath = String.Empty;
                        AgentCircuitData agentCircuit = avatar.ControllingClient.RequestClientInfo();
                        agentCircuit.BaseFolder = UUID.Zero;
                        agentCircuit.InventoryFolder = UUID.Zero;
                        agentCircuit.startpos = position;
                        agentCircuit.child = true;
                        if (Util.IsOutsideView(oldRegionX, newRegionX, oldRegionY, newRegionY))
                        {
                            // brand new agent, let's create a new caps seed
                            agentCircuit.CapsPath = CapsUtil.GetRandomCapsObjectPath();
                        }

                        string reason = String.Empty;

                        //if (!m_commsProvider.InterRegion.InformRegionOfChildAgent(reg.RegionHandle, agentCircuit))
                        if (!m_interregionCommsOut.SendCreateChildAgent(reg.RegionHandle, agentCircuit, out reason))
                        {
                            avatar.ControllingClient.SendTeleportFailed(String.Format("Destination is not accepting teleports: {0}",
                                                                                      reason));
                            return;
                        }

                        // Let's close some agents
                        if (isHyperLink) // close them all except this one
                        {
                            List<ulong> regions = new List<ulong>(avatar.KnownChildRegionHandles);
                            regions.Remove(avatar.Scene.RegionInfo.RegionHandle);
                            SendCloseChildAgentConnections(avatar.UUID, regions);
                        }
                        else // close just a few
                            avatar.CloseChildAgents(newRegionX, newRegionY);

                        if (Util.IsOutsideView(oldRegionX, newRegionX, oldRegionY, newRegionY) || isHyperLink)
                        {
                            capsPath 
                                = "http://" 
                                    + reg.ExternalHostName 
                                    + ":"
                                    + reg.HttpPort 
                                    + CapsUtil.GetCapsSeedPath(agentCircuit.CapsPath);

                            if (eq != null)
                            {
                                #region IP Translation for NAT
                                IClientIPEndpoint ipepClient;
                                if (avatar.ClientView.TryGet(out ipepClient))
                                {
                                    endPoint.Address = NetworkUtil.GetIPFor(ipepClient.EndPoint, endPoint.Address);
                                }
                                #endregion

                                eq.EnableSimulator(realHandle, endPoint, avatar.UUID);

                                // ES makes the client send a UseCircuitCode message to the destination, 
                                // which triggers a bunch of things there.
                                // So let's wait
                                Thread.Sleep(2000);

                                eq.EstablishAgentCommunication(avatar.UUID, endPoint, capsPath);
                            }
                            else
                            {
                                avatar.ControllingClient.InformClientOfNeighbour(realHandle, endPoint);
                                // TODO: make Event Queue disablable!
                            }
                        }
                        else
                        {
                            // child agent already there
                            agentCircuit.CapsPath = avatar.Scene.CapsModule.GetChildSeed(avatar.UUID, reg.RegionHandle);
                            capsPath = "http://" + reg.ExternalHostName + ":" + reg.HttpPort
                                        + "/CAPS/" + agentCircuit.CapsPath + "0000/";
                        }
                        
                        //m_commsProvider.InterRegion.ExpectAvatarCrossing(reg.RegionHandle, avatar.ControllingClient.AgentId,
                        //                                                      position, false);

                        //if (!m_commsProvider.InterRegion.ExpectAvatarCrossing(reg.RegionHandle, avatar.ControllingClient.AgentId,
                        //                                                      position, false))
                        //{
                        //    avatar.ControllingClient.SendTeleportFailed("Problem with destination.");
                        //    // We should close that agent we just created over at destination...
                        //    List<ulong> lst = new List<ulong>();
                        //    lst.Add(realHandle);
                        //    SendCloseChildAgentAsync(avatar.UUID, lst);
                        //    return;
                        //}

                        SetInTransit(avatar.UUID);
                        // Let's send a full update of the agent. This is a synchronous call.
                        AgentData agent = new AgentData();
                        avatar.CopyTo(agent);
                        agent.Position = position;
                        agent.CallbackURI = "http://" + m_regionInfo.ExternalHostName + ":" + m_regionInfo.HttpPort +
                            "/agent/" + avatar.UUID.ToString() + "/" + avatar.Scene.RegionInfo.RegionHandle.ToString() + "/release/";

                        m_interregionCommsOut.SendChildAgentUpdate(reg.RegionHandle, agent);

                        m_log.DebugFormat(
                            "[CAPS]: Sending new CAPS seed url {0} to client {1}", agentCircuit.CapsPath, avatar.UUID);


                        ///
                        /// Hypergrid mod: realHandle instead of reg.RegionHandle
                        /// 
                        ///
                        if (eq != null)
                        {
                            eq.TeleportFinishEvent(realHandle, 13, endPoint,
                                                   4, teleportFlags, capsPath, avatar.UUID);
                        }
                        else
                        {
                            avatar.ControllingClient.SendRegionTeleport(realHandle, 13, endPoint, 4,
                                                                        teleportFlags, capsPath);
                        }
                        ///
                        /// Hypergrid mod stop
                        /// 


                        // TeleportFinish makes the client send CompleteMovementIntoRegion (at the destination), which
                        // trigers a whole shebang of things there, including MakeRoot. So let's wait for confirmation
                        // that the client contacted the destination before we send the attachments and close things here.
                        if (!WaitForCallback(avatar.UUID))
                        {
                            // Client never contacted destination. Let's restore everything back
                            avatar.ControllingClient.SendTeleportFailed("Problems connecting to destination.");

                            ResetFromTransit(avatar.UUID);
                            // Yikes! We should just have a ref to scene here.
                            avatar.Scene.InformClientOfNeighbours(avatar);

                            // Finally, kill the agent we just created at the destination.
                            m_interregionCommsOut.SendCloseAgent(reg.RegionHandle, avatar.UUID);

                            return;
                        }

                        // Can't go back from here
                        if (KiPrimitive != null)
                        {
                            KiPrimitive(avatar.LocalId);
                        }

                        avatar.MakeChildAgent();

                        // CrossAttachmentsIntoNewRegion is a synchronous call. We shouldn't need to wait after it
                        avatar.CrossAttachmentsIntoNewRegion(reg.RegionHandle, true);

                        
                        // Finally, let's close this previously-known-as-root agent, when the jump is outside the view zone
                        ///
                        /// Hypergrid mod: extra check for isHyperLink
                        /// 
                        if (Util.IsOutsideView(oldRegionX, newRegionX, oldRegionY, newRegionY) || isHyperLink)
                        {
                            Thread.Sleep(5000);
                            avatar.Close();
                            CloseConnection(avatar.UUID);
                        }
                        // if (teleport success) // seems to be always success here
                        // the user may change their profile information in other region,
                        // so the userinfo in UserProfileCache is not reliable any more, delete it
                        if (avatar.Scene.NeedSceneCacheClear(avatar.UUID) || isHyperLink)
                        {
                            m_commsProvider.UserProfileCacheService.RemoveUser(avatar.UUID);
                            m_log.DebugFormat(
                                "[HGSceneCommService]: User {0} is going to another region, profile cache removed",
                                avatar.UUID);
                        }
                    }
                    else
                    {
                        avatar.ControllingClient.SendTeleportFailed("Remote Region appears to be down");
                    }
                }
                else
                {
                    // TP to a place that doesn't exist (anymore)
                    // Inform the viewer about that
                    avatar.ControllingClient.SendTeleportFailed("The region you tried to teleport to doesn't exist anymore");

                    // and set the map-tile to '(Offline)'
                    uint regX, regY;
                    Utils.LongToUInts(regionHandle, out regX, out regY);

                    MapBlockData block = new MapBlockData();
                    block.X = (ushort)(regX / Constants.RegionSize);
                    block.Y = (ushort)(regY / Constants.RegionSize);
                    block.Access = 254; // == not there

                    List<MapBlockData> blocks = new List<MapBlockData>();
                    blocks.Add(block);
                    avatar.ControllingClient.SendMapBlock(blocks, 0);
                }
            }
        }

    }
}
