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
    public class HGEntityTransferModule
        : EntityTransferModule, INonSharedRegionModule, IEntityTransferModule, IUserAgentVerificationModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private int m_levelHGTeleport = 0;

        private GatekeeperServiceConnector m_GatekeeperConnector;

        protected bool m_RestrictAppearanceAbroad;
        protected string m_AccountName;
        protected List<AvatarAppearance> m_ExportedAppearances;
        protected List<AvatarAttachment> m_Attachs;

        protected List<AvatarAppearance> ExportedAppearance
        {
            get
            {
                if (m_ExportedAppearances != null)
                    return m_ExportedAppearances;

                m_ExportedAppearances = new List<AvatarAppearance>();
                m_Attachs = new List<AvatarAttachment>();

                string[] names = m_AccountName.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string name in names)
                {
                    string[] parts = name.Trim().Split();
                    if (parts.Length != 2)
                    {
                        m_log.WarnFormat("[HG ENTITY TRANSFER MODULE]: Wrong user account name format {0}. Specify 'First Last'", name);
                        return null;
                    }
                    UserAccount account = Scene.UserAccountService.GetUserAccount(UUID.Zero, parts[0], parts[1]);
                    if (account == null)
                    {
                        m_log.WarnFormat("[HG ENTITY TRANSFER MODULE]: Unknown account {0}", m_AccountName);
                        return null;
                    }
                    AvatarAppearance a = Scene.AvatarService.GetAppearance(account.PrincipalID);
                    if (a != null)
                        m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Successfully retrieved appearance for {0}", name);

                    foreach (AvatarAttachment att in a.GetAttachments())
                    {
                        InventoryItemBase item = new InventoryItemBase(att.ItemID, account.PrincipalID);
                        item = Scene.InventoryService.GetItem(item);
                        if (item != null)
                            a.SetAttachment(att.AttachPoint, att.ItemID, item.AssetID);
                        else
                            m_log.WarnFormat("[HG ENTITY TRANSFER MODULE]: Unable to retrieve item {0} from inventory {1}", att.ItemID, name);
                    }

                    m_ExportedAppearances.Add(a);
                    m_Attachs.AddRange(a.GetAttachments());
                }

                return m_ExportedAppearances;
            }
        }

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
                    IConfig transferConfig = source.Configs["EntityTransfer"];
                    if (transferConfig != null)
                    {
                        m_levelHGTeleport = transferConfig.GetInt("LevelHGTeleport", 0);

                        m_RestrictAppearanceAbroad = transferConfig.GetBoolean("RestrictAppearanceAbroad", false);
                        if (m_RestrictAppearanceAbroad)
                        {
                            m_AccountName = transferConfig.GetString("AccountForAppearance", string.Empty);
                            if (m_AccountName == string.Empty)
                                m_log.WarnFormat("[HG ENTITY TRANSFER MODULE]: RestrictAppearanceAbroad is on, but no account has been given for avatar appearance!");
                        }
                    }

                    InitialiseCommon(source);
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
                scene.EventManager.OnIncomingSceneObject += OnIncomingSceneObject;
            }
        }

        void OnIncomingSceneObject(SceneObjectGroup so)
        {
            if (!so.IsAttachment)
                return;

            if (so.Scene.UserManagementModule.IsLocalGridUser(so.AttachedAvatar))
                return;

            // foreign user
            AgentCircuitData aCircuit = so.Scene.AuthenticateHandler.GetAgentCircuitData(so.AttachedAvatar);
            if (aCircuit != null && (aCircuit.teleportFlags & (uint)Constants.TeleportFlags.ViaHGLogin) != 0)
            {
                if (aCircuit.ServiceURLs != null && aCircuit.ServiceURLs.ContainsKey("AssetServerURI"))
                {
                    string url = aCircuit.ServiceURLs["AssetServerURI"].ToString();
                    m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Incoming attachement {0} for HG user {1} with asset server {2}", so.Name, so.AttachedAvatar, url);
                    Dictionary<UUID, AssetType> ids = new Dictionary<UUID, AssetType>();
                    HGUuidGatherer uuidGatherer = new HGUuidGatherer(so.Scene.AssetService, url);
                    uuidGatherer.GatherAssetUuids(so, ids);

                    foreach (KeyValuePair<UUID, AssetType> kvp in ids)
                        uuidGatherer.FetchAsset(kvp.Key);
                }
            }
        }

        protected override void OnNewClient(IClientAPI client)
        {
            client.OnTeleportHomeRequest += TeleportHome;
            client.OnTeleportLandmarkRequest += RequestTeleportLandmark;
            client.OnConnectionClosed += new Action<IClientAPI>(OnConnectionClosed);
        }

        public override void RegionLoaded(Scene scene)
        {
            base.RegionLoaded(scene);

            if (m_Enabled)
                m_GatekeeperConnector = new GatekeeperServiceConnector(scene.AssetService);
        }

        public override void RemoveRegion(Scene scene)
        {
            base.AddRegion(scene);

            if (m_Enabled)
                scene.UnregisterModuleInterface<IUserAgentVerificationModule>(this);
        }

        #endregion

        #region HG overrides of IEntiryTransferModule

        protected override GridRegion GetFinalDestination(GridRegion region)
        {
            int flags = Scene.GridService.GetRegionFlags(Scene.RegionInfo.ScopeID, region.RegionID);
            m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: region {0} flags: {1}", region.RegionID, flags);

            if ((flags & (int)OpenSim.Framework.RegionFlags.Hyperlink) != 0)
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

            int flags = Scene.GridService.GetRegionFlags(Scene.RegionInfo.ScopeID, reg.RegionID);
            if (flags == -1 /* no region in DB */ || (flags & (int)OpenSim.Framework.RegionFlags.Hyperlink) != 0)
                return true;

            return false;
        }

        protected override void AgentHasMovedAway(ScenePresence sp, bool logout)
        {
            base.AgentHasMovedAway(sp, logout);
            if (logout)
            {
                // Log them out of this grid
                Scene.PresenceService.LogoutAgent(sp.ControllingClient.SessionId);
                string userId = Scene.UserManagementModule.GetUserUUI(sp.UUID);
                Scene.GridUserService.LoggedOut(userId, UUID.Zero, Scene.RegionInfo.RegionID, sp.AbsolutePosition, sp.Lookat);
            }
        }

        protected override bool CreateAgent(ScenePresence sp, GridRegion reg, GridRegion finalDestination, AgentCircuitData agentCircuit, uint teleportFlags, out string reason, out bool logout)
        {
            m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: CreateAgent {0} {1}", reg.ServerURI, finalDestination.ServerURI);
            reason = string.Empty;
            logout = false;
            int flags = Scene.GridService.GetRegionFlags(Scene.RegionInfo.ScopeID, reg.RegionID);
            if (flags == -1 /* no region in DB */ || (flags & (int)OpenSim.Framework.RegionFlags.Hyperlink) != 0)
            {
                // this user is going to another grid
                // check if HyperGrid teleport is allowed, based on user level
                if (sp.UserLevel < m_levelHGTeleport)
                {
                    m_log.WarnFormat("[HG ENTITY TRANSFER MODULE]: Unable to HG teleport agent due to insufficient UserLevel.");
                    reason = "Hypergrid teleport not allowed";
                    return false;
                }

                if (agentCircuit.ServiceURLs.ContainsKey("HomeURI"))
                {
                    string userAgentDriver = agentCircuit.ServiceURLs["HomeURI"].ToString();
                    IUserAgentService connector = new UserAgentServiceConnector(userAgentDriver);
                    bool success = connector.LoginAgentToGrid(agentCircuit, reg, finalDestination, out reason);
                    logout = success; // flag for later logout from this grid; this is an HG TP

                    if (success)
                        sp.Scene.EventManager.TriggerTeleportStart(sp.ControllingClient, reg, finalDestination, teleportFlags, logout);

                    return success;
                }
                else
                {
                    m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Agent does not have a HomeURI address");
                    return false;
                }
            }

            return base.CreateAgent(sp, reg, finalDestination, agentCircuit, teleportFlags, out reason, out logout);
        }

        protected override bool ValidateGenericConditions(ScenePresence sp, GridRegion reg, GridRegion finalDestination, uint teleportFlags, out string reason)
        {
            reason = "Please wear your grid's allowed appearance before teleporting to another grid";
            if (!m_RestrictAppearanceAbroad)
                return true;

            // The rest is only needed for controlling appearance

            int flags = Scene.GridService.GetRegionFlags(Scene.RegionInfo.ScopeID, reg.RegionID);
            if (flags == -1 /* no region in DB */ || (flags & (int)OpenSim.Framework.RegionFlags.Hyperlink) != 0)
            {
                // this user is going to another grid
                if (Scene.UserManagementModule.IsLocalGridUser(sp.UUID))
                {
                    m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: RestrictAppearanceAbroad is ON. Checking generic appearance");

                    // Check wearables
                    for (int i = 0; i < AvatarWearable.MAX_WEARABLES; i++)
                    {
                        for (int j = 0; j < sp.Appearance.Wearables[i].Count; j++)
                        {
                            if (sp.Appearance.Wearables[i] == null)
                                continue;

                            bool found = false;
                            foreach (AvatarAppearance a in ExportedAppearance)
                                if (a.Wearables[i] != null)
                                {
                                    found = true;
                                    break;
                                }

                            if (!found) 
                            {
                               m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Wearable not allowed to go outside {0}", i);
                               return false;
                            }

                            found = false;
                            foreach (AvatarAppearance a in ExportedAppearance)
                                if (sp.Appearance.Wearables[i][j].AssetID == a.Wearables[i][j].AssetID)
                                {
                                    found = true;
                                    break;
                                }

                            if (!found)
                            {
                                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Wearable not allowed to go outside {0}", i);
                                return false;
                            }
                        }
                    }

                    // Check attachments
                    foreach (AvatarAttachment att in sp.Appearance.GetAttachments())
                    {
                        bool found = false;
                        foreach (AvatarAttachment att2 in m_Attachs)
                        {
                            if (att2.AssetID == att.AssetID)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Attachment not allowed to go outside {0}", att.AttachPoint);
                            return false;
                        }
                    }
                }
            }

            reason = string.Empty;
            return true;
        }


        //protected override bool UpdateAgent(GridRegion reg, GridRegion finalDestination, AgentData agentData, ScenePresence sp)
        //{
        //    int flags = Scene.GridService.GetRegionFlags(Scene.RegionInfo.ScopeID, reg.RegionID);
        //    if (flags == -1 /* no region in DB */ || (flags & (int)OpenSim.Data.RegionFlags.Hyperlink) != 0)
        //    {
        //        // this user is going to another grid
        //        if (m_RestrictAppearanceAbroad && Scene.UserManagementModule.IsLocalGridUser(agentData.AgentID))
        //        {
        //            // We need to strip the agent off its appearance
        //            m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: RestrictAppearanceAbroad is ON. Sending generic appearance");

        //            // Delete existing npc attachments
        //            Scene.AttachmentsModule.DeleteAttachmentsFromScene(sp, false);

        //            // XXX: We can't just use IAvatarFactoryModule.SetAppearance() yet since it doesn't transfer attachments
        //            AvatarAppearance newAppearance = new AvatarAppearance(ExportedAppearance, true);
        //            sp.Appearance = newAppearance;

        //            // Rez needed npc attachments
        //            Scene.AttachmentsModule.RezAttachments(sp);

                   
        //            IAvatarFactoryModule module = Scene.RequestModuleInterface<IAvatarFactoryModule>();
        //            //module.SendAppearance(sp.UUID);
        //            module.RequestRebake(sp, false);

        //            Scene.AttachmentsModule.CopyAttachments(sp, agentData);
        //            agentData.Appearance = sp.Appearance;
        //        }
        //    }

        //    foreach (AvatarAttachment a in agentData.Appearance.GetAttachments())
        //        m_log.DebugFormat("[XXX]: {0}-{1}", a.ItemID, a.AssetID);


        //    return base.UpdateAgent(reg, finalDestination, agentData, sp);
        //}

        public override void TeleportHome(UUID id, IClientAPI client)
        {
            m_log.DebugFormat(
                "[ENTITY TRANSFER MODULE]: Request to teleport {0} {1} home", client.Name, client.AgentId);

            // Let's find out if this is a foreign user or a local user
            IUserManagement uMan = Scene.RequestModuleInterface<IUserManagement>();
            if (uMan != null && uMan.IsLocalGridUser(id))
            {
                // local grid user
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: User is local");
                base.TeleportHome(id, client);
                return;
            }

            // Foreign user wants to go home
            // 
            AgentCircuitData aCircuit = ((Scene)(client.Scene)).AuthenticateHandler.GetAgentCircuitData(client.CircuitCode);
            if (aCircuit == null || (aCircuit != null && !aCircuit.ServiceURLs.ContainsKey("HomeURI")))
            {
                client.SendTeleportFailed("Your information has been lost");
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Unable to locate agent's gateway information");
                return;
            }

            IUserAgentService userAgentService = new UserAgentServiceConnector(aCircuit.ServiceURLs["HomeURI"].ToString());
            Vector3 position = Vector3.UnitY, lookAt = Vector3.UnitY;
            GridRegion finalDestination = userAgentService.GetHomeRegion(aCircuit.AgentID, out position, out lookAt);
            if (finalDestination == null)
            {
                client.SendTeleportFailed("Your home region could not be found");
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Agent's home region not found");
                return;
            }

            ScenePresence sp = ((Scene)(client.Scene)).GetScenePresence(client.AgentId);
            if (sp == null)
            {
                client.SendTeleportFailed("Internal error");
                m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: Agent not found in the scene where it is supposed to be");
                return;
            }

            GridRegion homeGatekeeper = MakeRegion(aCircuit);
            
            m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: teleporting user {0} {1} home to {2} via {3}:{4}",
                aCircuit.firstname, aCircuit.lastname, finalDestination.RegionName, homeGatekeeper.ServerURI, homeGatekeeper.RegionName);

            DoTeleport(
                sp, homeGatekeeper, finalDestination,
                position, lookAt, (uint)(Constants.TeleportFlags.SetLastToTarget | Constants.TeleportFlags.ViaHome));
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

            GridRegion info = Scene.GridService.GetRegionByUUID(UUID.Zero, lm.RegionID);

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

                    if (transferMod != null && sp != null)
                        transferMod.DoTeleport(
                            sp, gatekeeper, finalDestination, lm.Position, Vector3.UnitX,
                            (uint)(Constants.TeleportFlags.SetLastToTarget | Constants.TeleportFlags.ViaLandmark));
                }

            }

            // can't find the region: Tell viewer and abort
            remoteClient.SendTeleportFailed("The teleport destination could not be found.");
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
            {
                m_log.DebugFormat(
                    "[HG ENTITY TRANSFER MODULE]: Agent {0} {1} does not have a HomeURI OH NO!",
                    aCircuit.firstname, aCircuit.lastname);
            }

            return false;
        }

        void OnConnectionClosed(IClientAPI obj)
        {
            if (obj.SceneAgent.IsChildAgent)
                return;

            // Let's find out if this is a foreign user or a local user
            IUserManagement uMan = Scene.RequestModuleInterface<IUserManagement>();
//          UserAccount account = Scene.UserAccountService.GetUserAccount(Scene.RegionInfo.ScopeID, obj.AgentId);

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
            {
                    m_log.DebugFormat("[HG ENTITY TRANSFER MODULE]: HomeURI not found for agent {0} logout", obj.AgentId);
            }
        }

        #endregion

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
    }
}