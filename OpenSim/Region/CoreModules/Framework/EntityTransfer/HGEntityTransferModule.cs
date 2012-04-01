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
using System.Reflection;

using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Connectors.Hypergrid;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.CoreModules.Framework.EntityTransfer
{
    public class HGEntityTransferModule : EntityTransferModule, ISharedRegionModule, IEntityTransferModule, IUserAgentVerificationModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Initialized = false;

        private bool m_RestrictInventoryAccessAbroad = false;

        private GatekeeperServiceConnector m_GatekeeperConnector;

        #region ISharedRegionModule

        public override string Name
        {
            get { return "HGEntityTransferModule"; }
        }

        public override void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("EntityTransferModule", "");
                if (name == Name)
                {
                    InitialiseCommon(source);
                    IConfig transferConfig = source.Configs["HGEntityTransferModule"];
                    if (transferConfig != null)
                        m_RestrictInventoryAccessAbroad = transferConfig.GetBoolean("RestrictInventoryAccessAbroad", false);

                    m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: {0} enabled.", Name);
                }
            }
        }

        public override void AddRegion(Scene scene)
        {
            base.AddRegion(scene);
            if (m_Enabled)
            {
                scene.RegisterModuleInterface<IUserAgentVerificationModule>(this);
            }
        }

        protected override void OnNewClient(IClientAPI client)
        {
            client.OnTeleportHomeRequest += TriggerTeleportHome;
            client.OnTeleportLandmarkRequest += RequestTeleportLandmark;
            client.OnConnectionClosed += new Action<IClientAPI>(OnConnectionClosed);
            client.OnCompleteMovementToRegion += new Action<IClientAPI, bool>(OnCompleteMovementToRegion);
        }

        protected void OnCompleteMovementToRegion(IClientAPI client, bool arg2)
        {
            // HACK HACK -- just seeing how the viewer responds
            // Let's send the Suitcase or the real root folder folder for incoming HG agents
            // Visiting agents get their suitcase contents; incoming local users get their real root folder's content
            m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: OnCompleteMovementToRegion of user {0}", client.AgentId);
            object sp = null;
            if (client.Scene.TryGetScenePresence(client.AgentId, out sp))
            {
                if (sp is ScenePresence)
                {
                    AgentCircuitData aCircuit = ((ScenePresence)sp).Scene.AuthenticateHandler.GetAgentCircuitData(client.AgentId);
                    if ((aCircuit.teleportFlags & (uint)Constants.TeleportFlags.ViaHGLogin) != 0)
                    {
                        m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: ViaHGLogin");
                        if (m_RestrictInventoryAccessAbroad)
                        {
                            RestoreRootFolderContents(client);
                        }
                    }
                }
            }
        }


        public override void RegionLoaded(Scene scene)
        {
            base.RegionLoaded(scene);
            if (m_Enabled)
                if (!m_Initialized)
                {
                    m_GatekeeperConnector = new GatekeeperServiceConnector(scene.AssetService);
                    m_Initialized = true;

                    scene.AddCommand(
                    "HG", this, "send inventory",
                    "send inventory",
                    "Don't use this",
                    HandleSendInventory);

                }

        }
        public override void RemoveRegion(Scene scene)
        {
            base.AddRegion(scene);
            if (m_Enabled)
            {
                scene.UnregisterModuleInterface<IUserAgentVerificationModule>(this);
            }
        }


        #endregion

        #region HG overrides of IEntiryTransferModule

        protected override GridRegion GetFinalDestination(GridRegion region)
        {
            int flags = m_aScene.GridService.GetRegionFlags(m_aScene.RegionInfo.ScopeID, region.RegionID);
            m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: region {0} flags: {1}", region.RegionID, flags);
            if ((flags & (int)OpenSim.Data.RegionFlags.Hyperlink) != 0)
            {
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Destination region {0} is hyperlink", region.RegionID);
                GridRegion real_destination = m_GatekeeperConnector.GetHyperlinkRegion(region, region.RegionID);
                if (real_destination != null)
                    m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: GetFinalDestination serveruri -> {0}", real_destination.ServerURI);
                else
                    m_log.WarnFormat("[HG ENTITY TRANSFER MODULE]: GetHyperlinkRegion to Gatekeeper {0} failed", region.ServerURI);
                return real_destination;
            }
            return region;
        }

        protected override bool NeedsClosing(float drawdist, uint oldRegionX, uint newRegionX, uint oldRegionY, uint newRegionY, GridRegion reg)
        {
            if (base.NeedsClosing(drawdist, oldRegionX, newRegionX, oldRegionY, newRegionY, reg))
                return true;

            int flags = m_aScene.GridService.GetRegionFlags(m_aScene.RegionInfo.ScopeID, reg.RegionID);
            if (flags == -1 /* no region in DB */ || (flags & (int)OpenSim.Data.RegionFlags.Hyperlink) != 0)
                return true;

            return false;
        }

        protected override void AgentHasMovedAway(ScenePresence sp, bool logout)
        {
            base.AgentHasMovedAway(sp, logout);
            if (logout)
            {
                // Log them out of this grid
                m_aScene.PresenceService.LogoutAgent(sp.ControllingClient.SessionId);
            }
        }

        protected override bool CreateAgent(ScenePresence sp, GridRegion reg, GridRegion finalDestination, AgentCircuitData agentCircuit, uint teleportFlags, out string reason, out bool logout)
        {
            m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: CreateAgent {0} {1}", reg.ServerURI, finalDestination.ServerURI);
            reason = string.Empty;
            logout = false;
            int flags = m_aScene.GridService.GetRegionFlags(m_aScene.RegionInfo.ScopeID, reg.RegionID);
            if (flags == -1 /* no region in DB */ || (flags & (int)OpenSim.Data.RegionFlags.Hyperlink) != 0)
            {
                // this user is going to another grid
                if (agentCircuit.ServiceURLs.ContainsKey("HomeURI"))
                {
                    string userAgentDriver = agentCircuit.ServiceURLs["HomeURI"].ToString();
                    IUserAgentService connector = new UserAgentServiceConnector(userAgentDriver);
                    bool success = connector.LoginAgentToGrid(agentCircuit, reg, finalDestination, out reason);
                    logout = success; // flag for later logout from this grid; this is an HG TP

                    if (success && m_RestrictInventoryAccessAbroad)
                        RemoveRootFolderContents(sp.ControllingClient);

                    return success;
                }
                else
                {
                    m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Agent does not have a HomeURI address");
                    return false;
                }
            }

            return m_aScene.SimulationService.CreateAgent(reg, agentCircuit, teleportFlags, out reason);
        }

        public void TriggerTeleportHome(UUID id, IClientAPI client)
        {
            TeleportHome(id, client);
        }

        public override bool TeleportHome(UUID id, IClientAPI client)
        {
            m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Request to teleport {0} {1} home", client.FirstName, client.LastName);

            // Let's find out if this is a foreign user or a local user
            IUserManagement uMan = m_aScene.RequestModuleInterface<IUserManagement>(); 
            if (uMan != null && uMan.IsLocalGridUser(id))
            {
                // local grid user
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: User is local");
                return base.TeleportHome(id, client);
            }

            // Foreign user wants to go home
            // 
            AgentCircuitData aCircuit = ((Scene)(client.Scene)).AuthenticateHandler.GetAgentCircuitData(client.CircuitCode);
            if (aCircuit == null || (aCircuit != null && !aCircuit.ServiceURLs.ContainsKey("HomeURI")))
            {
                client.SendTeleportFailed("Your information has been lost");
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Unable to locate agent's gateway information");
                return false;
            }

            IUserAgentService userAgentService = new UserAgentServiceConnector(aCircuit.ServiceURLs["HomeURI"].ToString());
            Vector3 position = Vector3.UnitY, lookAt = Vector3.UnitY;
            GridRegion finalDestination = userAgentService.GetHomeRegion(aCircuit.AgentID, out position, out lookAt);
            if (finalDestination == null)
            {
                client.SendTeleportFailed("Your home region could not be found");
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Agent's home region not found");
                return false;
            }

            ScenePresence sp = ((Scene)(client.Scene)).GetScenePresence(client.AgentId);
            if (sp == null)
            {
                client.SendTeleportFailed("Internal error");
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Agent not found in the scene where it is supposed to be");
                return false;
            }

            IEventQueue eq = sp.Scene.RequestModuleInterface<IEventQueue>();
            GridRegion homeGatekeeper = MakeRegion(aCircuit);
            
            m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: teleporting user {0} {1} home to {2} via {3}:{4}",
                aCircuit.firstname, aCircuit.lastname, finalDestination.RegionName, homeGatekeeper.ServerURI, homeGatekeeper.RegionName);

            DoTeleport(sp, homeGatekeeper, finalDestination, position, lookAt, (uint)(Constants.TeleportFlags.SetLastToTarget | Constants.TeleportFlags.ViaHome), eq);
            return true;
        }

        /// <summary>
        /// Tries to teleport agent to landmark.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="regionHandle"></param>
        /// <param name="position"></param>
        public override void RequestTeleportLandmark(IClientAPI remoteClient, AssetLandmark lm)
        {
            m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Teleporting agent via landmark to {0} region {1} position {2}", 
                (lm.Gatekeeper == string.Empty) ? "local" : lm.Gatekeeper, lm.RegionID, lm.Position);
            if (lm.Gatekeeper == string.Empty)
            {
                base.RequestTeleportLandmark(remoteClient, lm);
                return;
            }

            GridRegion info = m_aScene.GridService.GetRegionByUUID(UUID.Zero, lm.RegionID);

            // Local region?
            if (info != null)
            {
                ((Scene)(remoteClient.Scene)).RequestTeleportLocation(remoteClient, info.RegionHandle, lm.Position,
                    Vector3.Zero, (uint)(Constants.TeleportFlags.SetLastToTarget | Constants.TeleportFlags.ViaLandmark));
                return;
            }
            else 
            {
                // Foreign region
                Scene scene = (Scene)(remoteClient.Scene);
                GatekeeperServiceConnector gConn = new GatekeeperServiceConnector();
                GridRegion gatekeeper = new GridRegion();
                gatekeeper.ServerURI = lm.Gatekeeper;
                GridRegion finalDestination = gConn.GetHyperlinkRegion(gatekeeper, new UUID(lm.RegionID));
                if (finalDestination != null)
                {
                    ScenePresence sp = scene.GetScenePresence(remoteClient.AgentId);
                    IEntityTransferModule transferMod = scene.RequestModuleInterface<IEntityTransferModule>();
                    IEventQueue eq = sp.Scene.RequestModuleInterface<IEventQueue>();
                    if (transferMod != null && sp != null && eq != null)
                        transferMod.DoTeleport(sp, gatekeeper, finalDestination, lm.Position,
                            Vector3.UnitX, (uint)(Constants.TeleportFlags.SetLastToTarget | Constants.TeleportFlags.ViaLandmark), eq);
                }

            }

            // can't find the region: Tell viewer and abort
            remoteClient.SendTeleportFailed("The teleport destination could not be found.");

        }

        protected override void Fail(ScenePresence sp, GridRegion finalDestination, bool logout)
        {
            base.Fail(sp, finalDestination, logout);
            if (logout && m_RestrictInventoryAccessAbroad)
            {
                RestoreRootFolderContents(sp.ControllingClient);
            }
        }

        #endregion

        #region IUserAgentVerificationModule

        public bool VerifyClient(AgentCircuitData aCircuit, string token)
        {
            if (aCircuit.ServiceURLs.ContainsKey("HomeURI"))
            {
                string url = aCircuit.ServiceURLs["HomeURI"].ToString();
                IUserAgentService security = new UserAgentServiceConnector(url);
                return security.VerifyClient(aCircuit.SessionID, token);
            } 
            else 
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Agent {0} {1} does not have a HomeURI OH NO!", aCircuit.firstname, aCircuit.lastname);

            return false;
        }

        void OnConnectionClosed(IClientAPI obj)
        {
            if (obj.IsLoggingOut)
            {
                object sp = null;
                if (obj.Scene.TryGetScenePresence(obj.AgentId, out sp))
                {
                    if (((ScenePresence)sp).IsChildAgent)
                        return;
                }

                // Let's find out if this is a foreign user or a local user
                IUserManagement uMan = m_aScene.RequestModuleInterface<IUserManagement>();
                UserAccount account = m_aScene.UserAccountService.GetUserAccount(m_aScene.RegionInfo.ScopeID, obj.AgentId);
                if (uMan != null && uMan.IsLocalGridUser(obj.AgentId))
                {
                    // local grid user
                    return;
                }

                AgentCircuitData aCircuit = ((Scene)(obj.Scene)).AuthenticateHandler.GetAgentCircuitData(obj.CircuitCode);

                if (aCircuit.ServiceURLs.ContainsKey("HomeURI"))
                {
                    string url = aCircuit.ServiceURLs["HomeURI"].ToString();
                    IUserAgentService security = new UserAgentServiceConnector(url);
                    security.LogoutAgent(obj.AgentId, obj.SessionId);
                    //m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Sent logout call to UserAgentService @ {0}", url);
                }
                else
                    m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: HomeURI not found for agent {0} logout", obj.AgentId);
            }
        }

        #endregion

        private void RemoveRootFolderContents(IClientAPI client)
        {
            // TODO tell the viewer to remove the root folder's content
            if (client is IClientCore)
            {
                IClientCore core = (IClientCore)client;
                IClientInventory inv;

                if (core.TryGet<IClientInventory>(out inv))
                {
                    InventoryFolderBase root = m_Scenes[0].InventoryService.GetRootFolder(client.AgentId);
                    if (root != null)
                    {
                        m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Removing root inventory for user {0}", client.AgentId);
                        InventoryCollection content = m_Scenes[0].InventoryService.GetFolderContent(client.AgentId, root.ID);
                        UUID[] ids = new UUID[content.Folders.Count];
                        int i = 0;
                        foreach (InventoryFolderBase f in content.Folders)
                            ids[i++] = f.ID;
                        inv.SendRemoveInventoryFolders(ids);
                        ids = new UUID[content.Items.Count];
                        i = 0;
                        foreach (InventoryItemBase it in content.Items)
                            ids[i++] = it.ID;
                        inv.SendRemoveInventoryItems(ids);
                    }
                }
            }
        }

        private void RestoreRootFolderContents(IClientAPI client)
        {
            if (client is IClientCore)
            {
                IClientCore core = (IClientCore)client;
                IClientInventory inv;

                if (core.TryGet<IClientInventory>(out inv))
                {
                    InventoryFolderBase root = m_Scenes[0].InventoryService.GetRootFolder(client.AgentId);
                    client.SendBulkUpdateInventory(root);
                    //if (root != null)
                    //{
                    //    m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Restoring root inventory for user {0}", client.AgentId);
                    //    InventoryCollection content = m_Scenes[0].InventoryService.GetFolderContent(client.AgentId, root.ID);
                    //    m_log.DebugFormat("[XXX]: Folder name {0}, id {1}, parent {2}", root.Name, root.ID, root.ParentID);
                    //    foreach (InventoryItemBase i in content.Items)
                    //        m_log.DebugFormat("[XXX]:   Name={0}, folderID={1}", i.Name, i.Folder);

                    //    inv.SendBulkUpdateInventory(content.Folders.ToArray(), content.Items.ToArray());
                    //}
                }
            }
        }

        private GridRegion MakeRegion(AgentCircuitData aCircuit)
        {
            GridRegion region = new GridRegion();

            Uri uri = null;
            if (!aCircuit.ServiceURLs.ContainsKey("HomeURI") || 
                (aCircuit.ServiceURLs.ContainsKey("HomeURI") && !Uri.TryCreate(aCircuit.ServiceURLs["HomeURI"].ToString(), UriKind.Absolute, out uri)))
                return null;

            region.ExternalHostName = uri.Host;
            region.HttpPort = (uint)uri.Port;
            region.ServerURI = aCircuit.ServiceURLs["HomeURI"].ToString();
            region.RegionName = string.Empty;
            region.InternalEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Parse("0.0.0.0"), (int)0);
            return region;
        }

        protected void HandleSendInventory(string module, string[] cmd)
        {
            m_Scenes[0].ForEachClient(delegate(IClientAPI client)
            {
                RestoreRootFolderContents(client);
            });
        }

    }
}
