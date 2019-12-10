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
using Mono.Addins;

namespace OpenSim.Region.CoreModules.Framework.InventoryAccess
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "HGInventoryAccessModule")]
    public class HGInventoryAccessModule : BasicInventoryAccessModule, INonSharedRegionModule, IInventoryAccessModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static HGAssetMapper m_assMapper;
        public static HGAssetMapper AssetMapper
        {
            get { return m_assMapper; }
        }

        private string m_HomeURI;
        private bool m_OutboundPermission;
        private string m_ThisGatekeeper;
        private bool m_RestrictInventoryAccessAbroad;

        private bool m_bypassPermissions = true;

        // This simple check makes it possible to support grids in which all the simulators
        // share all central services of the Robust server EXCEPT assets. In other words,
        // grids where the simulators' assets are kept in one DB and the users' inventory assets
        // are kept on another. When users rez items from inventory or take objects from world,
        // an HG-like asset copy takes place between the 2 servers, the world asset server and
        // the user's asset server.
        private bool m_CheckSeparateAssets = false;
        private string m_LocalAssetsURL = string.Empty;

//        private bool m_Initialized = false;

        #region INonSharedRegionModule

        public override string Name
        {
            get { return "HGInventoryAccessModule"; }
        }

        public override void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("InventoryAccessModule", "");
                if (name == Name)
                {
                    m_Enabled = true;

                    InitialiseCommon(source);

                    m_log.InfoFormat("[HG INVENTORY ACCESS MODULE]: {0} enabled.", Name);

                    IConfig thisModuleConfig = source.Configs["HGInventoryAccessModule"];
                    if (thisModuleConfig != null)
                    {
                        m_HomeURI = Util.GetConfigVarFromSections<string>(source, "HomeURI",
                            new string[] { "Startup", "Hypergrid", "HGInventoryAccessModule" }, String.Empty);
                        m_ThisGatekeeper = Util.GetConfigVarFromSections<string>(source, "GatekeeperURI",
                            new string[] { "Startup", "Hypergrid", "HGInventoryAccessModule" }, String.Empty);
                        // Legacy. Renove soon!
                        m_ThisGatekeeper = thisModuleConfig.GetString("Gatekeeper", m_ThisGatekeeper);

                        m_OutboundPermission = thisModuleConfig.GetBoolean("OutboundPermission", true);
                        m_RestrictInventoryAccessAbroad = thisModuleConfig.GetBoolean("RestrictInventoryAccessAbroad", true);
                        m_CheckSeparateAssets = thisModuleConfig.GetBoolean("CheckSeparateAssets", false);
                        m_LocalAssetsURL = thisModuleConfig.GetString("RegionHGAssetServerURI", string.Empty);
                        m_LocalAssetsURL = m_LocalAssetsURL.Trim(new char[] { '/' });

                    }
                    else
                        m_log.Warn("[HG INVENTORY ACCESS MODULE]: HGInventoryAccessModule configs not found");

                    m_bypassPermissions = !Util.GetConfigVarFromSections<bool>(source, "serverside_object_permissions",
                                            new string[] { "Startup", "Permissions" }, true);

                }
            }
        }

        public override void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            base.AddRegion(scene);
            m_assMapper = new HGAssetMapper(scene, m_HomeURI);
            scene.EventManager.OnNewInventoryItemUploadComplete += PostInventoryAsset;
            scene.EventManager.OnTeleportStart += TeleportStart;
            scene.EventManager.OnTeleportFail += TeleportFail;

            // We're fgoing to enforce some stricter permissions if Outbound is false
            scene.Permissions.OnTakeObject += CanTakeObject;
            scene.Permissions.OnTakeCopyObject += CanTakeObject;
            scene.Permissions.OnTransferUserInventory += OnTransferUserInventory;
        }

        #endregion

        #region Event handlers

        protected override void OnNewClient(IClientAPI client)
        {
            base.OnNewClient(client);
            client.OnCompleteMovementToRegion += new Action<IClientAPI, bool>(OnCompleteMovementToRegion);
        }

        protected void OnCompleteMovementToRegion(IClientAPI client, bool arg2)
        {
            //m_log.DebugFormat("[HG INVENTORY ACCESS MODULE]: OnCompleteMovementToRegion of user {0}", client.Name);
            object sp = null;
            if (client.Scene.TryGetScenePresence(client.AgentId, out sp))
            {
                if (sp is ScenePresence)
                {
                    AgentCircuitData aCircuit = ((ScenePresence)sp).Scene.AuthenticateHandler.GetAgentCircuitData(client.AgentId);
                    if (aCircuit != null &&  (aCircuit.teleportFlags & (uint)Constants.TeleportFlags.ViaHGLogin) != 0)
                    {
                        if (m_RestrictInventoryAccessAbroad)
                        {
                            IUserManagement uMan = m_Scene.RequestModuleInterface<IUserManagement>();
                            if (uMan.IsLocalGridUser(client.AgentId))
                                ProcessInventoryForComingHome(client);
                            else
                                ProcessInventoryForArriving(client);
                        }
                    }
                }
            }
        }

        protected void TeleportStart(IClientAPI client, GridRegion destination, GridRegion finalDestination, uint teleportFlags, bool gridLogout)
        {
            if (gridLogout && m_RestrictInventoryAccessAbroad)
            {
                IUserManagement uMan = m_Scene.RequestModuleInterface<IUserManagement>();
                if (uMan != null && uMan.IsLocalGridUser(client.AgentId))
                {
                    // local grid user
                    ProcessInventoryForHypergriding(client);
                }
                else
                {
                    // Foreigner
                    ProcessInventoryForLeaving(client);
                }
            }

        }

        protected void TeleportFail(IClientAPI client, bool gridLogout)
        {
            if (gridLogout && m_RestrictInventoryAccessAbroad)
            {
                IUserManagement uMan = m_Scene.RequestModuleInterface<IUserManagement>();
                if (uMan.IsLocalGridUser(client.AgentId))
                {
                    ProcessInventoryForComingHome(client);
                }
                else
                {
                    ProcessInventoryForArriving(client);
                }
            }
        }

        private void PostInventoryAsset(InventoryItemBase item, int userlevel)
        {
            InventoryFolderBase f = m_Scene.InventoryService.GetFolderForType(item.Owner, FolderType.Trash);
            if (f == null || (f != null && item.Folder != f.ID))
                PostInventoryAsset(item.Owner, (AssetType)item.AssetType, item.AssetID, item.Name, userlevel);
        }

        private void PostInventoryAsset(UUID avatarID, AssetType type, UUID assetID, string name, int userlevel)
        {
            if (type == AssetType.Link)
                return;

            string userAssetServer = string.Empty;
            if (IsForeignUser(avatarID, out userAssetServer) && userAssetServer != string.Empty && m_OutboundPermission)
            {
                m_assMapper.Post(assetID, avatarID, userAssetServer);
            }
        }

        #endregion

        #region Overrides of Basic Inventory Access methods

        protected override string GenerateLandmark(ScenePresence presence, out string prefix, out string suffix)
        {
            if (UserManagementModule != null && !UserManagementModule.IsLocalGridUser(presence.UUID))
                prefix = "HG ";
            else
                prefix = string.Empty;
            suffix = " @ " + m_ThisGatekeeper;
            Vector3 pos = presence.AbsolutePosition;
            return String.Format(Culture.FormatProvider, "Landmark version 2\nregion_id {0}\nlocal_pos {1} {2} {3}\nregion_handle {4}\ngatekeeper {5}\n",
                                presence.Scene.RegionInfo.RegionID,
                                pos.X, pos.Y, pos.Z,
                                presence.RegionHandle,
                                m_ThisGatekeeper);
        }


        ///
        /// CapsUpdateInventoryItemAsset
        ///
        public override UUID CapsUpdateInventoryItemAsset(IClientAPI remoteClient, UUID itemID, byte[] data)
        {
            UUID newAssetID = base.CapsUpdateInventoryItemAsset(remoteClient, itemID, data);

            // We need to construct this here to satisfy the calling convention.
            // Better this in two places than five formal params in all others.
            InventoryItemBase item = new InventoryItemBase();
            item.Owner = remoteClient.AgentId;
            item.AssetType = (int)AssetType.Unknown;
            item.AssetID = newAssetID;
            item.Name = String.Empty;

            PostInventoryAsset(item, 0);

            return newAssetID;
        }

        ///
        /// UpdateInventoryItemAsset
        ///
        public override bool UpdateInventoryItemAsset(UUID ownerID, InventoryItemBase item, AssetBase asset)
        {
            if (base.UpdateInventoryItemAsset(ownerID, item, asset))
            {
                PostInventoryAsset(item, 0);
                return true;
            }

            return false;
        }

        ///
        /// Used in DeleteToInventory
        ///
        protected override void ExportAsset(UUID agentID, UUID assetID)
        {
            if (!assetID.Equals(UUID.Zero))
            {
                InventoryItemBase item = new InventoryItemBase();
                item.Owner = agentID;
                item.AssetType = (int)AssetType.Unknown;
                item.AssetID = assetID;
                item.Name = String.Empty;

                PostInventoryAsset(item, 0);
            }
            else
            {
                m_log.Debug("[HGScene]: Scene.Inventory did not create asset");
            }
        }

        ///
        /// RezObject
        ///
        // compatibility do not use
        public override SceneObjectGroup RezObject(
            IClientAPI remoteClient, UUID itemID, Vector3 RayEnd, Vector3 RayStart,
            UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
            bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment)
        {
            return RezObject(remoteClient, itemID, UUID.Zero, RayEnd, RayStart,
                    RayTargetID, BypassRayCast, RayEndIsIntersection,
                    RezSelected, RemoveItem, fromTaskID, attachment);
        }

        public override SceneObjectGroup RezObject(IClientAPI remoteClient, UUID itemID,
                            UUID groupID, Vector3 RayEnd, Vector3 RayStart,
                            UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                            bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment)
        {
            //m_log.DebugFormat("[HGScene]: RezObject itemID={0} fromTaskID={1}", itemID, fromTaskID);

            //if (fromTaskID.Equals(UUID.Zero))
            //{
            InventoryItemBase item = m_Scene.InventoryService.GetItem(remoteClient.AgentId, itemID);
            //if (item == null)
            //{ // Fetch the item
            //    item = new InventoryItemBase();
            //    item.Owner = remoteClient.AgentId;
            //    item.ID = itemID;
            //    item = m_assMapper.Get(item, userInfo.RootFolder.ID, userInfo);
            //}
            string userAssetServer = string.Empty;
            if (item != null && IsForeignUser(remoteClient.AgentId, out userAssetServer))
            {
                m_assMapper.Get(item.AssetID, remoteClient.AgentId, userAssetServer);

            }
            //}

            // OK, we're done fetching. Pass it up to the default RezObject
            SceneObjectGroup sog = base.RezObject(remoteClient, itemID, groupID, RayEnd, RayStart, RayTargetID, BypassRayCast, RayEndIsIntersection,
                                   RezSelected, RemoveItem, fromTaskID, attachment);

            return sog;

        }

        public override void TransferInventoryAssets(InventoryItemBase item, UUID sender, UUID receiver)
        {
            string senderAssetServer = string.Empty;
            string receiverAssetServer = string.Empty;
            bool isForeignSender, isForeignReceiver;
            isForeignSender = IsForeignUser(sender, out senderAssetServer);
            isForeignReceiver = IsForeignUser(receiver, out receiverAssetServer);

            // They're both local. Nothing to do.
            if (!isForeignSender && !isForeignReceiver)
                return;

            // At least one of them is foreign.
            // If both users have the same asset server, no need to transfer the asset
            if (senderAssetServer.Equals(receiverAssetServer))
            {
                m_log.DebugFormat("[HGScene]: Asset transfer between foreign users, but they have the same server. No transfer.");
                return;
            }

            if (isForeignSender && senderAssetServer != string.Empty)
                m_assMapper.Get(item.AssetID, sender, senderAssetServer);

            if (isForeignReceiver && receiverAssetServer != string.Empty && m_OutboundPermission)
                m_assMapper.Post(item.AssetID, receiver, receiverAssetServer);
        }

        public override bool IsForeignUser(UUID userID, out string assetServerURL)
        {
            assetServerURL = string.Empty;

            if (UserManagementModule != null)
            {
                if (!m_CheckSeparateAssets)
                {
                    if (!UserManagementModule.IsLocalGridUser(userID))
                    { // foreign
                        ScenePresence sp = null;
                        if (m_Scene.TryGetScenePresence(userID, out sp))
                        {
                            AgentCircuitData aCircuit = m_Scene.AuthenticateHandler.GetAgentCircuitData(sp.ControllingClient.CircuitCode);
                            if (aCircuit != null && aCircuit.ServiceURLs != null && aCircuit.ServiceURLs.ContainsKey("AssetServerURI"))
                            {
                                assetServerURL = aCircuit.ServiceURLs["AssetServerURI"].ToString();
                                assetServerURL = assetServerURL.Trim(new char[] { '/' });
                            }
                        }
                        else
                        {
                            assetServerURL = UserManagementModule.GetUserServerURL(userID, "AssetServerURI");
                            assetServerURL = assetServerURL.Trim(new char[] { '/' });
                        }
                        return true;
                    }
                }
                else
                {
                    if (IsLocalInventoryAssetsUser(userID, out assetServerURL))
                    {
                        m_log.DebugFormat("[HGScene]: user {0} has local assets {1}", userID, assetServerURL);
                        return false;
                    }
                    else
                    {
                        m_log.DebugFormat("[HGScene]: user {0} has foreign assets {1}", userID, assetServerURL);
                        return true;
                    }
                }
            }
            return false;
        }

        private bool IsLocalInventoryAssetsUser(UUID uuid, out string assetsURL)
        {
            assetsURL = UserManagementModule.GetUserServerURL(uuid, "AssetServerURI");
            if (assetsURL == string.Empty)
            {
                AgentCircuitData agent = m_Scene.AuthenticateHandler.GetAgentCircuitData(uuid);
                if (agent != null)
                {
                    assetsURL = agent.ServiceURLs["AssetServerURI"].ToString();
                    assetsURL = assetsURL.Trim(new char[] { '/' });
                }
            }
            return m_LocalAssetsURL.Equals(assetsURL);
        }


        protected override InventoryItemBase GetItem(UUID agentID, UUID itemID)
        {
            InventoryItemBase item = base.GetItem(agentID, itemID);
            if (item == null)
                return null;

            string userAssetServer = string.Empty;
            if (IsForeignUser(agentID, out userAssetServer))
                m_assMapper.Get(item.AssetID, agentID, userAssetServer);

            return item;
        }

        #endregion

        #region Inventory manipulation upon arriving/leaving

        //
        // These 2 are for local and foreign users coming back, respectively
        //

        private void ProcessInventoryForComingHome(IClientAPI client)
        {
            m_log.DebugFormat("[HG INVENTORY ACCESS MODULE]: Restoring root folder for local user {0}", client.Name);
            if (client is IClientCore)
            {
                IClientCore core = (IClientCore)client;
                IClientInventory inv;

                if (core.TryGet<IClientInventory>(out inv))
                {
                    InventoryFolderBase root = m_Scene.InventoryService.GetRootFolder(client.AgentId);
                    InventoryCollection content = m_Scene.InventoryService.GetFolderContent(client.AgentId, root.ID);

                    List<InventoryFolderBase> keep = new List<InventoryFolderBase>();

                    foreach (InventoryFolderBase f in content.Folders)
                    {
                        if (f.Name != "My Suitcase" && f.Name != "Current Outfit")
                            keep.Add(f);
                    }

                    inv.SendBulkUpdateInventory(keep.ToArray(), content.Items.ToArray());
                }
            }
        }

        private void ProcessInventoryForArriving(IClientAPI client)
        {
            // No-op for now, but we may need to do something for freign users inventory
        }

        //
        // These 2 are for local and foreign users going away respectively
        //

        private void ProcessInventoryForHypergriding(IClientAPI client)
        {
            if (client is IClientCore)
            {
                IClientCore core = (IClientCore)client;
                IClientInventory inv;

                if (core.TryGet<IClientInventory>(out inv))
                {
                    InventoryFolderBase root = m_Scene.InventoryService.GetRootFolder(client.AgentId);
                    if (root != null)
                    {
                        m_log.DebugFormat("[HG INVENTORY ACCESS MODULE]: Changing root inventory for user {0}", client.Name);
                        InventoryCollection content = m_Scene.InventoryService.GetFolderContent(client.AgentId, root.ID);

                        List<InventoryFolderBase> keep = new List<InventoryFolderBase>();

                        foreach (InventoryFolderBase f in content.Folders)
                        {
                            if (f.Name != "My Suitcase" && f.Name != "Current Outfit")
                            {
                                f.Name = f.Name + " (Unavailable)";
                                keep.Add(f);
                            }
                        }

                        // items directly under the root folder
                        foreach (InventoryItemBase it in content.Items)
                            it.Name = it.Name + " (Unavailable)"; ;

                        // Send the new names
                        inv.SendBulkUpdateInventory(keep.ToArray(), content.Items.ToArray());

                    }
                }
            }
        }

        private void ProcessInventoryForLeaving(IClientAPI client)
        {
            // No-op for now
        }

        #endregion

        #region Permissions

        private bool CanTakeObject(SceneObjectGroup sog, ScenePresence sp)
        {
            if (m_bypassPermissions) return true;

            if(sp == null || sog == null)
                return false;

            if (!m_OutboundPermission && !UserManagementModule.IsLocalGridUser(sp.UUID))
            {
                if (sog.OwnerID == sp.UUID)
                    return true;
                return false;
            }

            return true;
        }

        private bool OnTransferUserInventory(UUID itemID, UUID userID, UUID recipientID)
        {
            if (m_bypassPermissions) return true;

            if (!m_OutboundPermission && !UserManagementModule.IsLocalGridUser(recipientID))
                return false;

            return true;
        }


        #endregion
    }
}
