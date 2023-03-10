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
using System.Globalization;
using System.Reflection;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;

using OpenMetaverse;
using log4net;
using Nini.Config;
using Mono.Addins;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.CoreModules.Framework.InventoryAccess
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "BasicInventoryAccessModule")]
    public class BasicInventoryAccessModule : INonSharedRegionModule, IInventoryAccessModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_Enabled = false;
        protected Scene m_Scene;

        protected GridInfo m_thisGridInfo;
        protected IUserManagement m_UserManagement;
        protected IUserManagement UserManagementModule
        {
            get
            {
                m_UserManagement ??= m_Scene.RequestModuleInterface<IUserManagement>();
                return m_UserManagement;
            }
        }

        public bool CoalesceMultipleObjectsToInventory { get; set; }

        #region INonSharedRegionModule

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public virtual string Name
        {
            get { return "BasicInventoryAccessModule"; }
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig is not null)
            {
                string name = moduleConfig.GetString("InventoryAccessModule", string.Empty);
                if (name == Name)
                {
                    m_Enabled = true;

                    InitialiseCommon(source);

                    m_log.Info($"[INVENTORY ACCESS MODULE]: {Name} enabled.");
                }
            }
        }

        /// <summary>
        /// Common module config for both this and descendant classes.
        /// </summary>
        /// <param name="source"></param>
        protected virtual void InitialiseCommon(IConfigSource source)
        {
            IConfig inventoryConfig = source.Configs["Inventory"];
            CoalesceMultipleObjectsToInventory = inventoryConfig is null || inventoryConfig.GetBoolean("CoalesceMultipleObjectsToInventory", true);
        }

        public virtual void PostInitialise()
        {
        }

        public virtual void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_Scene = scene;
            m_thisGridInfo ??= scene.SceneGridInfo;

            scene.RegisterModuleInterface<IInventoryAccessModule>(this);
            scene.EventManager.OnNewClient += OnNewClient;
        }

        protected virtual void OnNewClient(IClientAPI client)
        {
            client.OnCreateNewInventoryItem += CreateNewInventoryItem;
        }

        public virtual void Close()
        {
            if (!m_Enabled)
                return;
            m_thisGridInfo = null;
        }

        public virtual void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;
            m_Scene = null;
        }

        public virtual void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;
        }

        #endregion

        #region Inventory Access

        /// <summary>
        /// Create a new inventory item.  Called when the client creates a new item directly within their
        /// inventory (e.g. by selecting a context inventory menu option).
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="transactionID"></param>
        /// <param name="folderID"></param>
        /// <param name="callbackID"></param>
        /// <param name="description"></param>
        /// <param name="name"></param>
        /// <param name="invType"></param>
        /// <param name="type"></param>
        /// <param name="subType"></param>
        /// <param name="nextOwnerMask"></param>
        public void CreateNewInventoryItem(IClientAPI remoteClient, UUID transactionID, UUID folderID,
                                           uint callbackID, string description, string name, sbyte invType,
                                           sbyte assetType, byte subType,
                                           uint nextOwnerMask, int creationDate)
        {
            m_log.DebugFormat("[INVENTORY ACCESS MODULE]: Received request to create inventory item {0} in folder {1}, transactionID {2}", name,
                folderID, transactionID);

            if (!m_Scene.Permissions.CanCreateUserInventory(invType, remoteClient.AgentId))
                return;

            InventoryFolderBase folder = m_Scene.InventoryService.GetFolder(remoteClient.AgentId, folderID);

            if (folder is null && Enum.IsDefined(typeof(FolderType), invType))
            {
                folder = m_Scene.InventoryService.GetFolderForType(remoteClient.AgentId, (FolderType)invType);
                if (folder is not null)
                    m_log.DebugFormat("[INVENTORY ACCESS MODULE]: Requested folder not found but found folder for type {0}", invType);
            }

            if (folder is null || folder.Owner.NotEqual(remoteClient.AgentId))
                return;

            if (transactionID.IsNotZero() &&
                    assetType != (byte)AssetType.Settings &&
                    assetType != (byte)AssetType.Material)
            {
                IAgentAssetTransactions agentTransactions = m_Scene.AgentTransactionsModule;
                if (agentTransactions is not null)
                {
                    if (agentTransactions.HandleItemCreationFromTransaction(
                            remoteClient, transactionID, folderID, callbackID, description,
                            name, invType, assetType, subType, nextOwnerMask))
                        return;
                }
            }

            if (m_Scene.TryGetScenePresence(remoteClient.AgentId, out ScenePresence presence))
            {
                uint everyonemask = 0;
                uint groupmask = 0;
                uint flags = 0;

                if (invType == (sbyte)InventoryType.Landmark && presence is not null)
                {
                    string strdata = GenerateLandmark(presence, out string prefix, out string suffix);
                    byte[] data = osUTF8.GetASCIIBytes(strdata);
                    name = prefix + name;
                    description += suffix;
                    groupmask = (uint)PermissionMask.AllAndExport;
                    everyonemask = (uint)(PermissionMask.AllAndExport & ~PermissionMask.Modify);

                    AssetBase asset = m_Scene.CreateAsset(name, description, assetType, data, remoteClient.AgentId);
                    m_Scene.AssetService.Store(asset);
                    m_Scene.CreateNewInventoryItem(
                        remoteClient, remoteClient.AgentId.ToString(), string.Empty, folderID,
                        name, description, flags, callbackID, asset.FullID, asset.Type, invType,
                        (uint)PermissionMask.AllAndExport, (uint)PermissionMask.AllAndExport,
                        everyonemask, nextOwnerMask, groupmask,
                        creationDate, false); // Data from viewer
                    return;
                }
                switch (assetType)
                {
                    case (sbyte)AssetType.Settings:
                    {
                        flags = subType;
                        IEnvironmentModule envModule = m_Scene.RequestModuleInterface<IEnvironmentModule>();
                        if(envModule is null)
                            return;
                        UUID assetID = envModule.GetDefaultAsset(subType);
                        if(assetID.IsZero())
                        {
                            m_log.ErrorFormat(
                            "[INVENTORY ACCESS MODULE CreateNewInventoryItem]: failed to create default environment setting asset {0} for agent {1}", name, remoteClient.AgentId);
                            return;
                        }
                        m_Scene.CreateNewInventoryItem(
                                remoteClient, remoteClient.AgentId.ToString(), string.Empty, folderID,
                                name, description, flags, callbackID, assetID, (sbyte)AssetType.Settings, invType,
                                (uint)PermissionMask.AllAndExport, (uint)PermissionMask.AllAndExport,
                                everyonemask, nextOwnerMask, groupmask,
                                creationDate, false); // Data from viewer
                        return;
                    }
                    case (sbyte)AssetType.LSLText:
                    {
                        m_Scene.CreateNewInventoryItem(
                                remoteClient, remoteClient.AgentId.ToString(), string.Empty, folderID,
                                name, description, flags, callbackID, Constants.DefaultScriptID, (sbyte)AssetType.LSLText, invType,
                                (uint)PermissionMask.AllAndExport, (uint)PermissionMask.AllAndExport,
                                everyonemask, nextOwnerMask, groupmask,
                                creationDate, false); // Data from viewer
                        return;
                    }
                    case (sbyte)AssetType.Notecard:
                    {
                        m_Scene.CreateNewInventoryItem(
                                remoteClient, remoteClient.AgentId.ToString(), string.Empty, folderID,
                                name, description, flags, callbackID, Constants.EmptyNotecardID, (sbyte)AssetType.Notecard, invType,
                                (uint)PermissionMask.AllAndExport, (uint)PermissionMask.AllAndExport,
                                everyonemask, nextOwnerMask, groupmask,
                                creationDate, false); // Data from viewer
                        return;
                    }

                    case (sbyte)AssetType.Clothing:
                    case (sbyte)AssetType.Bodypart:
                        flags = subType;
                        break;
                    default:
                        break;
                }

                m_Scene.CreateNewInventoryItem(
                        remoteClient, remoteClient.AgentId.ToString(), string.Empty, folderID,
                        name, description, flags, callbackID, UUID.Zero, assetType, invType,
                        (uint)PermissionMask.AllAndExport, (uint)PermissionMask.AllAndExport,
                        everyonemask, nextOwnerMask, groupmask,
                        creationDate, false); // Data from viewer
            }
            else
            {
                m_log.ErrorFormat(
                    "[INVENTORY ACCESS MODULE]: ScenePresence for agent uuid {0} unexpectedly not found in CreateNewInventoryItem",
                    remoteClient.AgentId);
            }
        }

        protected virtual string GenerateLandmark(ScenePresence presence, out string prefix, out string suffix)
        {
            prefix = string.Empty;
            suffix = string.Empty;
            Vector3 pos = presence.AbsolutePosition;
            return String.Format(CultureInfo.InvariantCulture.NumberFormat, "Landmark version 2\nregion_id {0}\nlocal_pos {1} {2} {3}\nregion_handle {4}\n",
                                presence.Scene.RegionInfo.RegionID,
                                pos.X, pos.Y, pos.Z,
                                presence.RegionHandle);
        }

        /// <summary>
        /// Capability originating call to update the asset of an item in an agent's inventory
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public virtual UUID CapsUpdateInventoryItemAsset(IClientAPI remoteClient, UUID itemID, byte[] data)
        {
            InventoryItemBase item = m_Scene.InventoryService.GetItem(remoteClient.AgentId, itemID);

            if (item is null)
            {
                m_log.ErrorFormat(
                    "[INVENTORY ACCESS MODULE]: Could not find item {0} for caps inventory update", itemID);
                return UUID.Zero;
            }

            if (item.Owner.NotEqual(remoteClient.AgentId))
                return UUID.Zero;

            InventoryType itemType = (InventoryType)item.InvType;
            switch (itemType)
            {
                case InventoryType.Notecard:
                {
                    if (!m_Scene.Permissions.CanEditNotecard(itemID, UUID.Zero, remoteClient.AgentId))
                    {
                        remoteClient.SendAgentAlertMessage("Insufficient permissions to edit notecard", false);
                        return UUID.Zero;
                    }

                    remoteClient.SendAlertMessage("Notecard updated");
                    break;
                }
                case InventoryType.LSL:
                {
                    if (!m_Scene.Permissions.CanEditScript(itemID, UUID.Zero, remoteClient.AgentId))
                    {
                        remoteClient.SendAgentAlertMessage("Insufficient permissions to edit script", false);
                        return UUID.Zero;
                    }

                    remoteClient.SendAlertMessage("Script updated");
                    break;
                }
                case (InventoryType)CustomInventoryType.AnimationSet:
                {
                    AnimationSet animSet = new(data);
                    uint res = animSet.Validate(x => {
                        const int required = (int)(PermissionMask.Transfer | PermissionMask.Copy);
                        int perms = m_Scene.InventoryService.GetAssetPermissions(remoteClient.AgentId, x);
                        // enforce previus perm rule
                        if ((perms & required) != required)
                            return 0;
                        return (uint) perms;
                    });
                    if(res == 0)
                    {
                        remoteClient.SendAgentAlertMessage("Not enought permissions on asset(s) referenced by animation set '{0}', update failed", false);
                        return UUID.Zero;
                    }
                    break;
                }
                case InventoryType.Gesture:
                {
                    if ((item.CurrentPermissions & (uint)PermissionMask.Modify) == 0)
                    {
                        remoteClient.SendAgentAlertMessage("Insufficient permissions to edit gesture", false);
                        return UUID.Zero;
                    }

                    remoteClient.SendAlertMessage("gesture updated");
                    break;
                }
                case InventoryType.Settings:
                {
                    if ((item.CurrentPermissions & (uint)PermissionMask.Modify) == 0)
                    {
                        remoteClient.SendAgentAlertMessage("Insufficient permissions to edit setting", false);
                        return UUID.Zero;
                    }

                    remoteClient.SendAlertMessage("Setting updated");
                    break;
                }
                case InventoryType.Material:
                {
                    if ((item.CurrentPermissions & (uint)PermissionMask.Modify) == 0)
                    {
                        remoteClient.SendAgentAlertMessage("Insufficient permissions to edit setting", false);
                        return UUID.Zero;
                    }
                    break;
                }
            }

            AssetBase asset = CreateAsset(item.Name, item.Description, (sbyte)item.AssetType, data, remoteClient.AgentId.ToString());
            item.AssetID = asset.FullID;
            m_Scene.AssetService.Store(asset);

            m_Scene.InventoryService.UpdateItem(item);

            // remoteClient.SendInventoryItemCreateUpdate(item);
            return (asset.FullID);
        }

        public virtual bool UpdateInventoryItemAsset(UUID ownerID, InventoryItemBase item, AssetBase asset)
        {
            if (item is not null && asset is not null && item.Owner.Equals(ownerID))
            {
                //m_log.DebugFormat(
                //    "[INVENTORY ACCESS MODULE]: Updating item {0} {1} with new asset {2}",
                //    item.Name, item.ID, asset.ID);

                m_Scene.AssetService.Store(asset);
                m_Scene.InventoryService.UpdateItem(item);
                return true;
            }
            else
            {
                m_log.ErrorFormat("[INVENTORY ACCESS MODULE]: Given invalid item for inventory update: {0}",
                    (item is null || asset is null? "null item or asset" : "wrong owner"));
                return false;
            }
        }

        public virtual List<InventoryItemBase> CopyToInventory(
            DeRezAction action, UUID folderID,
            List<SceneObjectGroup> objectGroups, IClientAPI remoteClient, bool asAttachment)
        {
            Dictionary<UUID, List<SceneObjectGroup>> bundlesToCopy = new();

            if (CoalesceMultipleObjectsToInventory)
            {
                // The following code groups the SOG's by owner. No objects
                // belonging to different people can be coalesced, for obvious
                // reasons.
                foreach (SceneObjectGroup g in objectGroups)
                {
                    if (!bundlesToCopy.TryGetValue(g.OwnerID, out List<SceneObjectGroup> lst))
                    {
                        lst = new();
                        bundlesToCopy[g.OwnerID] = lst;
                    }
                    lst.Add(g);
                }
            }
            else
            {
                // If we don't want to coalesce then put every object in its own bundle.
                foreach (SceneObjectGroup g in objectGroups)
                {
                    bundlesToCopy[g.UUID] = new List<SceneObjectGroup>(){ g };
                }
            }

            //m_log.DebugFormat(
            //    "[INVENTORY ACCESS MODULE]: Copying {0} object bundles to folder {1} action {2} for {3}",
            //    bundlesToCopy.Count, folderID, action, remoteClient.Name);

            // Each iteration is really a separate asset being created,
            // with distinct destinations as well.
            List<InventoryItemBase> copiedItems = new();
            foreach (List<SceneObjectGroup> bundle in bundlesToCopy.Values)
                copiedItems.Add(CopyBundleToInventory(action, folderID, bundle, remoteClient, asAttachment));

            return copiedItems;
        }

        /// <summary>
        /// Copy a bundle of objects to inventory.  If there is only one object, then this will create an object
        /// item.  If there are multiple objects then these will be saved as a single coalesced item.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="folderID"></param>
        /// <param name="objlist"></param>
        /// <param name="remoteClient"></param>
        /// <param name="asAttachment">Should be true if the bundle is being copied as an attachment.  This prevents
        /// attempted serialization of any script state which would abort any operating scripts.</param>
        /// <returns>The inventory item created by the copy</returns>
        protected InventoryItemBase CopyBundleToInventory(
            DeRezAction action, UUID folderID, List<SceneObjectGroup> objlist, IClientAPI remoteClient,
            bool asAttachment)
        {
            CoalescedSceneObjects coa = new(UUID.Zero);
            Dictionary<UUID, Vector3> originalPositions = new();
            Dictionary<UUID, Quaternion> originalRotations = new();
            // this possible is not needed if keyframes are saved
            //Dictionary<UUID, KeyframeMotion> originalKeyframes = new Dictionary<UUID, KeyframeMotion>();

            foreach (SceneObjectGroup objectGroup in objlist)
            {
                objectGroup.RootPart.KeyframeMotion?.Suspend();

                objectGroup.RootPart.SetForce(Vector3.Zero);
                objectGroup.RootPart.SetAngularImpulse(Vector3.Zero, false);

                //originalKeyframes[objectGroup.UUID] = objectGroup.RootPart.KeyframeMotion;
                //objectGroup.RootPart.KeyframeMotion = null;

                Vector3 inventoryStoredPosition = objectGroup.AbsolutePosition;
                originalPositions[objectGroup.UUID] = inventoryStoredPosition;
                Quaternion inventoryStoredRotation = objectGroup.GroupRotation;
                originalRotations[objectGroup.UUID] = inventoryStoredRotation;

                // Restore attachment data after trip through the sim
                if (objectGroup.AttachmentPoint > 0)
                {
                    inventoryStoredPosition = objectGroup.RootPart.AttachedPos;
                    inventoryStoredRotation = objectGroup.RootPart.AttachRotation;
                    if (objectGroup.RootPart.Shape.PCode != (byte) PCode.Tree &&
                            objectGroup.RootPart.Shape.PCode != (byte) PCode.NewTree)
                        objectGroup.RootPart.Shape.LastAttachPoint = (byte)objectGroup.AttachmentPoint;
                }

                objectGroup.AbsolutePosition = inventoryStoredPosition;
                objectGroup.RootPart.RotationOffset = inventoryStoredRotation;

                // Make sure all bits but the ones we want are clear
                // on take.
                // This will be applied to the current perms, so
                // it will do what we want.
                objectGroup.RootPart.NextOwnerMask &=
                        ((uint)PermissionMask.Copy |
                         (uint)PermissionMask.Transfer |
                         (uint)PermissionMask.Modify |
                         (uint)PermissionMask.Export);
                objectGroup.RootPart.NextOwnerMask |=
                        (uint)PermissionMask.Move;

                coa.Add(objectGroup);
            }

            string itemXml;

            // If we're being called from a script, then trying to serialize that same script's state will not complete
            // in any reasonable time period.  Therefore, we'll avoid it.  The worst that can happen is that if
            // the client/server crashes rather than logging out normally, the attachment's scripts will resume
            // without state on relog.  Arguably, this is what we want anyway.
            if (objlist.Count > 1)
                itemXml = CoalescedSceneObjectsSerializer.ToXml(coa, !asAttachment);
            else
                itemXml = SceneObjectSerializer.ToOriginalXmlFormat(objlist[0], !asAttachment);

            // Restore the position of each group now that it has been stored to inventory.
            foreach (SceneObjectGroup objectGroup in objlist)
            {
                objectGroup.AbsolutePosition = originalPositions[objectGroup.UUID];
                objectGroup.RootPart.RotationOffset = originalRotations[objectGroup.UUID];
                //objectGroup.RootPart.KeyframeMotion = originalKeyframes[objectGroup.UUID];
                objectGroup.RootPart.KeyframeMotion?.Resume();
            }

            InventoryItemBase item = CreateItemForObject(action, remoteClient, objlist[0], folderID);

            //m_log.DebugFormat(
            //    "[INVENTORY ACCESS MODULE]: Created item is {0}",
            //    item != null ? item.ID.ToString() : "NULL");

            if (item is null)
                return null;

            item.CreatorId = objlist[0].RootPart.CreatorID.ToString();
            item.CreatorData = objlist[0].RootPart.CreatorData;

            if (objlist.Count > 1)
            {
                item.Flags = (uint)InventoryItemFlags.ObjectHasMultipleItems;

                // If the objects have different creators then don't specify a creator at all
                foreach (SceneObjectGroup objectGroup in objlist)
                {
                    if ((objectGroup.RootPart.CreatorID.ToString() != item.CreatorId)
                        || (objectGroup.RootPart.CreatorData.ToString() != item.CreatorData))
                    {
                        item.CreatorId = UUID.Zero.ToString();
                        item.CreatorData = string.Empty;
                        break;
                    }
                }
            }
            else
            {
                item.SaleType = objlist[0].RootPart.ObjectSaleType;
                item.SalePrice = objlist[0].RootPart.SalePrice;
            }

            string name = objlist[0].RootPart.Name;
            string desc = objlist[0].RootPart.Description;
            AssetBase asset = CreateAsset(
                name, desc,
                (sbyte)AssetType.Object,
                Utils.StringToBytes(itemXml),
                objlist[0].OwnerID.ToString());
            m_Scene.AssetService.Store(asset);

            item.Description = desc;
            item.Name = name;
            item.AssetType = (int)AssetType.Object;
            item.AssetID = asset.FullID;

            if (action == DeRezAction.SaveToExistingUserInventoryItem)
            {
                m_Scene.InventoryService.UpdateItem(item);
            }
            else
            {
                bool isowner = remoteClient != null && item.Owner == remoteClient.AgentId;
                if(action == DeRezAction.Return)
                    AddPermissions(item, objlist[0], objlist, null);
                else if(action == DeRezAction.Delete && !isowner)
                    AddPermissions(item, objlist[0], objlist, null);
                else
                   AddPermissions(item, objlist[0], objlist, remoteClient);

                m_Scene.AddInventoryItem(item);

                if (isowner)
                {
                    remoteClient.SendInventoryItemCreateUpdate(item, 0);
                }
                else
                {
                    ScenePresence notifyUser = m_Scene.GetScenePresence(item.Owner);
                    notifyUser?.ControllingClient.SendInventoryItemCreateUpdate(item, 0);
                }
            }

            /* this is already done under m_Scene.AddInventoryItem(item) that triggers TriggerOnNewInventoryItemUploadComplete calling HG 
            // This is a hook to do some per-asset post-processing for subclasses that need that
            if (remoteClient != null && action != DeRezAction.Delete)
                ExportAsset(remoteClient.AgentId, asset.FullID);
            */
            return item;
        }

        protected virtual void ExportAsset(UUID agentID, UUID assetID)
        {
            // nothing to do here
        }

        /// <summary>
        /// Add relevant permissions for an object to the item.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="so"></param>
        /// <param name="objsForEffectivePermissions"></param>
        /// <param name="remoteClient"></param>
        /// <returns></returns>
        protected InventoryItemBase AddPermissions(
            InventoryItemBase item, SceneObjectGroup so, List<SceneObjectGroup> objsForEffectivePermissions,
            IClientAPI remoteClient)
        {
            uint effectivePerms = (uint)(PermissionMask.Copy | PermissionMask.Transfer | PermissionMask.Modify | PermissionMask.Move | PermissionMask.Export | PermissionMask.FoldedMask);
       
            foreach (SceneObjectGroup grp in objsForEffectivePermissions)
            {
                effectivePerms &= grp.CurrentAndFoldedNextPermissions();
            }
 
            if (remoteClient is not null && (remoteClient.AgentId.NotEqual(so.RootPart.OwnerID)) && m_Scene.Permissions.PropagatePermissions())
            {
                // apply parts inventory items next owner
                PermissionsUtil.ApplyNoModFoldedPermissions(effectivePerms, ref effectivePerms);
                // change to next owner
                uint basePerms = effectivePerms & so.RootPart.NextOwnerMask;
                // fix and update folded
                basePerms = PermissionsUtil.FixAndFoldPermissions(basePerms);
               
                item.BasePermissions = basePerms;               
                item.CurrentPermissions = basePerms;
                item.NextPermissions = basePerms & so.RootPart.NextOwnerMask;
                item.EveryOnePermissions = basePerms & so.RootPart.EveryoneMask;
                item.GroupPermissions = basePerms & so.RootPart.GroupMask;

                // apply next owner perms on rez
                item.Flags |= (uint)InventoryItemFlags.ObjectSlamPerm;
            }
            else
            {
                item.BasePermissions = effectivePerms;
                item.CurrentPermissions = effectivePerms;
                item.NextPermissions = so.RootPart.NextOwnerMask & effectivePerms;
                item.EveryOnePermissions = so.RootPart.EveryoneMask & effectivePerms;
                item.GroupPermissions = so.RootPart.GroupMask & effectivePerms;

                item.CurrentPermissions &=
                        ((uint)PermissionMask.Copy |
                         (uint)PermissionMask.Transfer |
                         (uint)PermissionMask.Modify |
                         (uint)PermissionMask.Move |
                         (uint)PermissionMask.Export |
                         (uint)PermissionMask.FoldedMask); // Preserve folded permissions ??
            }

            return item;
        }

        /// <summary>
        /// Create an item using details for the given scene object.
        /// </summary>
        /// <param name="action"></param>
        /// <param name="remoteClient"></param>
        /// <param name="so"></param>
        /// <param name="folderID"></param>
        /// <returns></returns>
        protected InventoryItemBase CreateItemForObject(
            DeRezAction action, IClientAPI remoteClient, SceneObjectGroup so, UUID folderID)
        {
            //m_log.DebugFormat(
            //    "[BASIC INVENTORY ACCESS MODULE]: Creating item for object {0} {1} for folder {2}, action {3}",
            //    so.Name, so.UUID, folderID, action);

            // Get the user info of the item destination

            UUID userID;

            if (action == DeRezAction.Take || action == DeRezAction.TakeCopy ||
                action == DeRezAction.SaveToExistingUserInventoryItem)
            {
                // Take or take copy require a taker
                // Saving changes requires a local user
 
                if (remoteClient is null)
                    return null;

                userID = remoteClient.AgentId;

                //m_log.DebugFormat(
                //    "[INVENTORY ACCESS MODULE]: Target of {0} in CreateItemForObject() is {1} {2}",
                //    action, remoteClient.Name, userID);
            }
            else if (so.RootPart.OwnerID.Equals(so.RootPart.GroupID))
            {
                // Group owned objects go to the last owner before the object was transferred.
                userID = so.RootPart.LastOwnerID;
            }
            else
            {
                // Other returns / deletes go to the object owner
                //
                userID = so.RootPart.OwnerID;

                //m_log.DebugFormat(
                //    "[INVENTORY ACCESS MODULE]: Target of {0} in CreateItemForObject() is object owner {1}",
                //    action, userID);
            }

            if (userID.IsZero()) // Can't proceed
            {
                return null;
            }

            InventoryItemBase item;
            if (DeRezAction.SaveToExistingUserInventoryItem == action)
            {
                item = m_Scene.InventoryService.GetItem(userID, so.RootPart.FromUserInventoryItemID);

                //item = userInfo.RootFolder.FindItem(
                //        objectGroup.RootPart.FromUserInventoryItemID);

                if (item is null)
                {
                    m_log.Debug(
                        $"[INVENTORY ACCESS MODULE]:  Object {so.Name} {so.UUID} scheduled for save to inventory has already been deleted.");
                }
                return item;
            }

            // Folder magic
            //
            // If we're returning someone's item, it goes back to the
            // owner's Lost And Found folder.
            // Delete is treated like return in this case
            // Deleting your own items makes them go to trash
            //
            InventoryFolderBase folder = null;

            if (action == DeRezAction.Delete)
            {
                // Deleting someone else's item
                //
                if (remoteClient is null || so.OwnerID.NotEqual(remoteClient.AgentId))
                {
                    folder = m_Scene.InventoryService.GetFolderForType(userID, FolderType.LostAndFound);
                }
                else
                {
                    folder = m_Scene.InventoryService.GetFolderForType(userID, FolderType.Trash);
                }
            }
            else if (action == DeRezAction.Return)
            {
                // Dump to lost + found unconditionally
                folder = m_Scene.InventoryService.GetFolderForType(userID, FolderType.LostAndFound);
            }

            if (folderID.IsZero() && folder is null)
            {
                if (action == DeRezAction.Delete)
                {
                    // Deletes go to trash by default
                    folder = m_Scene.InventoryService.GetFolderForType(userID, FolderType.Trash);
                }
                else
                {
                    if (remoteClient is null || so.RootPart.OwnerID.NotEqual(remoteClient.AgentId))
                    {
                        // Taking copy of another person's item. Take to
                        // Objects folder.
                        folder = m_Scene.InventoryService.GetFolderForType(userID, FolderType.Object);
                        so.FromFolderID = UUID.Zero;
                    }
                    else
                    {
                        // Catch all. Use lost & found
                        folder = m_Scene.InventoryService.GetFolderForType(userID, FolderType.LostAndFound);
                    }
                }
            }

            if (action == DeRezAction.TakeCopy)
                folder = m_Scene.InventoryService.GetFolderForType(userID, FolderType.Object);
            else if (action == DeRezAction.Take)
            {
                // Override and put into where it came from, if it came
                // from anywhere in inventory and the owner is taking it back.
                if (!so.FromFolderID.IsZero() && so.RootPart.OwnerID.Equals(remoteClient.AgentId))
                {
                    folder = m_Scene.InventoryService.GetFolder(userID, so.FromFolderID);

                    if(folder is null || folder.Type == (int)FolderType.Trash || folder.Type == (int)FolderType.LostAndFound)
                    {
                        folder = m_Scene.InventoryService.GetFolderForType(userID, FolderType.Object);
                    }
                    else
                    {
                        InventoryFolderBase parent = folder;
                        while(true)
                        {
                            parent = m_Scene.InventoryService.GetFolder(userID, parent.ParentID);
                            if (parent is not null && (parent.ParentID.IsZero() || parent.ID.Equals(parent.ParentID)))
                                break;
                            if (parent is null || parent.Type == (int)FolderType.Trash || parent.Type == (int)FolderType.LostAndFound)
                            {
                                folder = m_Scene.InventoryService.GetFolderForType(userID, FolderType.Object);
                                break;
                            }
                        }
                    }
                }
            }

            if (folder is null) // None of the above
            {
                folder = new InventoryFolderBase(folderID);

                if (folder is null) // Nowhere to put it
                {
                    return null;
                }
            }

            item = new InventoryItemBase
            {
                ID = UUID.Random(),
                InvType = (int)InventoryType.Object,
                Folder = folder.ID,
                Owner = userID,
                CreationDate = Util.UnixTimeSinceEpoch()
            };

            return item;
        }

        // compatibility do not use
        public virtual SceneObjectGroup RezObject(
            IClientAPI remoteClient, UUID itemID, Vector3 RayEnd, Vector3 RayStart,
            UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
            bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment)
        {
            return RezObject(remoteClient, itemID, UUID.Zero, RayEnd, RayStart,
                    RayTargetID, BypassRayCast, RayEndIsIntersection,
                    RezSelected, RemoveItem, fromTaskID, attachment);
        }

        public virtual SceneObjectGroup RezObject(
            IClientAPI remoteClient, UUID itemID, UUID rezGroupID, Vector3 RayEnd, Vector3 RayStart,
            UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
            bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment)
        {
//            m_log.DebugFormat("[INVENTORY ACCESS MODULE]: RezObject for {0}, item {1}", remoteClient.Name, itemID);
            InventoryItemBase item = m_Scene.InventoryService.GetItem(remoteClient.AgentId, itemID);

            if (item is null)
                return null;

            if (attachment && (item.Flags & (uint)InventoryItemFlags.ObjectHasMultipleItems) != 0)
            {
                if (remoteClient is not null && remoteClient.IsActive)
                    remoteClient.SendAlertMessage("You can't attach multiple objects to one spot");
                return null;
            }

            item.Owner = remoteClient.AgentId;

            return RezObject(
                remoteClient, item, rezGroupID, item.AssetID,
                RayEnd, RayStart, RayTargetID, BypassRayCast, RayEndIsIntersection,
                RezSelected, RemoveItem, fromTaskID, attachment);
        }

        // compatility
        public virtual SceneObjectGroup RezObject(
            IClientAPI remoteClient, InventoryItemBase item, UUID assetID, Vector3 RayEnd, Vector3 RayStart,
            UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
            bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment)
        {
            return RezObject(remoteClient, item, UUID.Zero, assetID,
                    RayEnd, RayStart, RayTargetID,
                    BypassRayCast, RayEndIsIntersection,
                    RezSelected, RemoveItem, fromTaskID, attachment);
        }

        public virtual SceneObjectGroup RezObject(
            IClientAPI remoteClient, InventoryItemBase item, UUID groupID, UUID assetID,
                Vector3 RayEnd, Vector3 RayStart, UUID RayTargetID,
                byte BypassRayCast, bool RayEndIsIntersection,
                bool RezSelected, bool RemoveItem, UUID fromTaskID, bool attachment)
        {
            if(assetID.IsZero())
                return null;

            AssetBase rezAsset = m_Scene.AssetService.Get(assetID.ToString());
            if (rezAsset is null)
            {
                if (item is not null)
                {
                    m_log.Warn(
                        $"[InventoryAccessModule]: Could not find asset {assetID} for item {item.Name} {item.ID} for {remoteClient.Name} in RezObject()");
                    remoteClient.SendAgentAlertMessage($"Unable to rez: could not find asset {assetID} for item {item.Name}", false);
                }
                else
                {
                    m_log.Warn($"[INVENTORY ACCESS MODULE]: Could not find asset {assetID} for {remoteClient.Name} in RezObject()");
                    remoteClient.SendAgentAlertMessage($"Unable to rez: could not find asset {assetID}", false);
                }
                return null;
            }

            if(rezAsset.Data is null || rezAsset.Data.Length == 0)
            {
                m_log.Warn($"[INVENTORY ACCESS MODULE]: missing data in asset {assetID} to RezObject()");
                remoteClient.SendAgentAlertMessage($"Unable to rez: missing data in asset {assetID}", false);
                return null;
            }

            SceneObjectGroup group = null;

            byte bRayEndIsIntersection = (byte)(RayEndIsIntersection ? 1 : 0);
            Vector3 pos;

            bool single = m_Scene.GetObjectsToRez(
                    rezAsset.Data, attachment, out List<SceneObjectGroup> objlist,
                    out List<Vector3> veclist, out Vector3 bbox, out float offsetHeight);

            pos = m_Scene.GetNewRezLocation(RayStart, RayEnd,
                    RayTargetID, Quaternion.Identity,
                    BypassRayCast, bRayEndIsIntersection, true,
                    bbox, false);

            if (single)
                pos.Z += offsetHeight;
            else
                pos -= bbox / 2;

            int primcount = 0;
            if(attachment)
            {
                foreach (SceneObjectGroup g in objlist)
                {
                    if(g.RootPart.Shape is not null)
                    {
                        PCode code = (PCode)g.RootPart.Shape.PCode;
                        if(code == PCode.Grass || code == PCode.NewTree || code == PCode.Tree)
                        {
                            // dont wear vegetables
                            remoteClient.SendAgentAlertMessage("You cannot wear system plants. They could grow roots inside your avatar", false);
                            return null;
                        }
                    }
                    primcount += g.PrimCount;
                }
            }
            else
            {
                foreach (SceneObjectGroup g in objlist)
                    primcount += g.PrimCount;
            }

            if (!m_Scene.Permissions.CanRezObject(primcount, remoteClient.AgentId, pos) && !attachment)
            {
                // The client operates in no fail mode. It will
                // have already removed the item from the folder
                // if it's no copy.
                // Put it back if it's not an attachment

                if (item is not null)
                {
                    if (((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0) && (!attachment))
                        remoteClient.SendBulkUpdateInventory(item);
                }
                return null;
            }

            if (item is not null && !DoPreRezWhenFromItem(remoteClient, item, objlist, pos, veclist, attachment))
                return null;

            for (int i = 0; i < objlist.Count; i++)
            {
                group = objlist[i];
                SceneObjectPart rootPart = group.RootPart;

                //m_log.DebugFormat(
                //    "[INVENTORY ACCESS MODULE]: Preparing to rez {0} {1} {2} ownermask={3:X} nextownermask={4:X} groupmask={5:X} everyonemask={6:X} for {7}",
                //    group.Name, group.LocalId, group.UUID,
                //    group.RootPart.OwnerMask, group.RootPart.NextOwnerMask, group.RootPart.GroupMask, group.RootPart.EveryoneMask,
                //    remoteClient.Name);

                //Vector3 storedPosition = group.AbsolutePosition;
                if (group.UUID.IsZero())
                {
                    m_log.Debug("[INVENTORY ACCESS MODULE]: Object has UUID.Zero! Position 3");
                }

                // if this was previously an attachment and is now being rezzed,
                // save the old attachment info.
                if (group.IsAttachment == false && group.RootPart.Shape.State != 0)
                {
                    group.RootPart.AttachedPos = group.AbsolutePosition;
                    group.RootPart.Shape.LastAttachPoint = (byte)group.AttachmentPoint;
                }

                if (item is null)
                {
                    // Change ownership. Normally this is done in DoPreRezWhenFromItem(), but in this case we must do it here.
                    foreach (SceneObjectPart part in group.Parts)
                    {
                        // Make the rezzer the owner, as this is not necessarily set correctly in the serialized asset.
                        if(part.OwnerID.NotEqual(remoteClient.AgentId))
                        {
                            if(part.OwnerID.NotEqual(part.GroupID))
                                part.LastOwnerID = part.OwnerID;
                            part.OwnerID = remoteClient.AgentId;
                            part.RezzerID = remoteClient.AgentId;
                            part.Inventory.ChangeInventoryOwner(remoteClient.AgentId);
                        }
                    }
                }

                group.ResetIDs();

                if (!attachment)
                {
                    if(RezSelected)
                    {
                        group.IsSelected = true;
                        group.RootPart.CreateSelected = true;
                    }
                    if (rootPart.Shape.PCode == (byte)PCode.Prim)
                        group.ClearPartAttachmentData();
                }
                else
                {
                    group.IsAttachment = true;
                }

                group.SetGroup(groupID, remoteClient);

                // If we're rezzing an attachment then don't ask
                // AddNewSceneObject() to update the client since
                // we'll be doing that later on.  Scheduling more than
                // one full update during the attachment
                // process causes some clients to fail to display the
                // attachment properly.

                if (!attachment)
                {
                    group.AbsolutePosition = pos + veclist[i];
                    m_Scene.AddNewSceneObject(group, true, false);

                    // Fire on_rez
                    group.CreateScriptInstances(0, true, m_Scene.DefaultScriptEngine, 1);
                    rootPart.ParentGroup.ResumeScripts();

                    group.ScheduleGroupForFullAnimUpdate();
                }
                else
                    m_Scene.AddNewSceneObject(group, true, false);

                //m_log.DebugFormat(
                //    "[INVENTORY ACCESS MODULE]:  Rezzed {0} {1} {2} ownermask={3:X} nextownermask={4:X} groupmask={5:X} everyonemask={6:X} for {7}",
                //    group.Name, group.LocalId, group.UUID,
                //    group.RootPart.OwnerMask, group.RootPart.NextOwnerMask, group.RootPart.GroupMask, group.RootPart.EveryoneMask,
                //    remoteClient.Name);
            }

            //group.SetGroup(remoteClient.ActiveGroupId, remoteClient);

            if (item is not null)
                DoPostRezWhenFromItem(item, attachment);

            return group;
        }

        /// <summary>
        /// Do pre-rez processing when the object comes from an item.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="item"></param>
        /// <param name="objlist"></param>
        /// <param name="pos"></param>
        /// <param name="veclist">
        /// List of vector position adjustments for a coalesced objects.  For ordinary objects
        /// this list will contain just Vector3.Zero.  The order of adjustments must match the order of objlist
        /// </param>
        /// <param name="isAttachment"></param>
        /// <returns>true if we can processed with rezzing, false if we need to abort</returns>
        private bool DoPreRezWhenFromItem(
            IClientAPI remoteClient, InventoryItemBase item, List<SceneObjectGroup> objlist,
            Vector3 pos, List<Vector3> veclist, bool isAttachment)
        {
            UUID fromUserInventoryItemId = UUID.Zero;

            // If we have permission to copy then link the rezzed object back to the user inventory
            // item that it came from.  This allows us to enable 'save object to inventory'
            if (!m_Scene.Permissions.BypassPermissions())
            {
                if ((item.CurrentPermissions & (uint)PermissionMask.Copy) == (uint)PermissionMask.Copy &&
                        (item.Flags & (uint)InventoryItemFlags.ObjectHasMultipleItems) == 0)
                {
                    fromUserInventoryItemId = item.ID;
                }
            }
            else
            {
                if ((item.Flags & (uint)InventoryItemFlags.ObjectHasMultipleItems) == 0)
                {
                    // Brave new fullperm world
                    fromUserInventoryItemId = item.ID;
                }
            }

            for (int i = 0; i < objlist.Count; i++)
            {
                SceneObjectGroup g = objlist[i];

                if (!m_Scene.Permissions.CanRezObject(
                    g.PrimCount, remoteClient.AgentId, pos + veclist[i])
                    && !isAttachment)
                {
                    // The client operates in no fail mode. It will
                    // have already removed the item from the folder
                    // if it's no copy.
                    // Put it back if it's not an attachment
                    //
                    if (((item.CurrentPermissions & (uint)PermissionMask.Copy) == 0) && (!isAttachment))
                        remoteClient.SendBulkUpdateInventory(item);

                    ILandObject land = m_Scene.LandChannel.GetLandObject(pos.X, pos.Y);
                    remoteClient.SendAlertMessage(string.Format(
                        "Can't rez object '{0}' at <{1:F3}, {2:F3}, {3:F3}> on parcel '{4}' in region {5}.",
                        item.Name, pos.X, pos.Y, pos.Z, land != null ? land.LandData.Name : "Unknown", m_Scene.Name));

                    return false;
                }
            }

            for (int i = 0; i < objlist.Count; i++)
            {
                SceneObjectGroup so = objlist[i];
                SceneObjectPart rootPart = so.RootPart;

                // Since renaming the item in the inventory does not
                // affect the name stored in the serialization, transfer
                // the correct name from the inventory to the
                // object itself before we rez.
                //
                // Only do these for the first object if we are rezzing a coalescence.
                // nahh dont mess with coalescence objects,
                // the name in inventory can be change for inventory purpuses only
                if (objlist.Count == 1)
                {
                    rootPart.Name = item.Name;
                    rootPart.Description = item.Description;
                }

                if ((item.Flags & (uint)InventoryItemFlags.ObjectSlamSale) != 0)
                {
                    rootPart.ObjectSaleType = item.SaleType;
                    rootPart.SalePrice = item.SalePrice;
                }

                so.FromFolderID = item.Folder;

                //m_log.DebugFormat(
                //    "[INVENTORY ACCESS MODULE]: rootPart.OwnedID {0}, item.Owner {1}, item.CurrentPermissions {2:X}",
                //    rootPart.OwnerID, item.Owner, item.CurrentPermissions);

                if ((rootPart.OwnerID.NotEqual(item.Owner)) ||
                    (item.CurrentPermissions & (uint)PermissionMask.Slam) != 0 ||
                    (item.Flags & (uint)InventoryItemFlags.ObjectSlamPerm) != 0)
                {
                    //Need to kill the for sale here
                    rootPart.ObjectSaleType = 0;
                    rootPart.SalePrice = 10;

                    if (m_Scene.Permissions.PropagatePermissions())
                    {
                        foreach (SceneObjectPart part in so.Parts)
                        {
                            part.GroupMask = 0; // DO NOT propagate here
                            if( part.OwnerID.NotEqual(part.GroupID))
                                part.LastOwnerID = part.OwnerID;
                            part.OwnerID = item.Owner;
                            part.RezzerID = item.Owner;
                            part.Inventory.ChangeInventoryOwner(item.Owner);

                            // Reconstruct the original item's base permissions. They
                            // can be found in the lower (folded) bits.
                            if ((item.BasePermissions & (uint)PermissionMask.FoldedMask) != 0)
                            {
                                // We have permissions stored there so use them
                                part.NextOwnerMask = ((item.BasePermissions & (uint)PermissionMask.FoldedMask) << (int)PermissionMask.FoldingShift);
                                part.NextOwnerMask |= (uint)PermissionMask.Move;
                            }
                            else
                            {
                                // This is a legacy object and we can't avoid the issues that
                                // caused perms loss or escalation before, treat it the legacy
                                // way.
                                part.NextOwnerMask = item.NextPermissions;
                            }
                        }

                        so.ApplyNextOwnerPermissions();

                        // In case the user has changed flags on a received item
                        // we have to apply those changes after the slam. Else we
                        // get a net loss of permissions.
                        // On legacy objects, this opts for a loss of permissions rather
                        // than the previous handling that allowed escalation.
                        foreach (SceneObjectPart part in so.Parts)
                        {
                            if ((item.Flags & (uint)InventoryItemFlags.ObjectHasMultipleItems) == 0)
                            {
                                part.GroupMask = item.GroupPermissions & part.BaseMask;
                                part.EveryoneMask = item.EveryOnePermissions & part.BaseMask;
                                part.NextOwnerMask = item.NextPermissions & part.BaseMask;
                            }
                        }
                    }
                }
                else
                {
                    foreach (SceneObjectPart part in so.Parts)
                    {
                        part.FromUserInventoryItemID = fromUserInventoryItemId;

                        if ((item.Flags & (uint)InventoryItemFlags.ObjectOverwriteEveryone) != 0)
                            part.EveryoneMask = item.EveryOnePermissions;
                        if ((item.Flags & (uint)InventoryItemFlags.ObjectOverwriteNextOwner) != 0)
                            part.NextOwnerMask = item.NextPermissions;
                        if ((item.Flags & (uint)InventoryItemFlags.ObjectOverwriteGroup) != 0)
                            part.GroupMask = item.GroupPermissions;
                    }
                }

                rootPart.TrimPermissions();
                so.InvalidateDeepEffectivePerms();

                if (isAttachment)
                    so.FromItemID = item.ID;
            }

            return true;
        }

        /// <summary>
        /// Do post-rez processing when the object comes from an item.
        /// </summary>
        /// <param name="item"></param>
        /// <param name="isAttachment"></param>
        private void DoPostRezWhenFromItem(InventoryItemBase item, bool isAttachment)
        {
            if (!m_Scene.Permissions.BypassPermissions())
            {
                if (!isAttachment && (item.CurrentPermissions & (uint)PermissionMask.Copy) == 0)
                {
                    // If this is done on attachments, no
                    // copy ones will be lost, so avoid it
                    m_Scene.InventoryService.DeleteItems(item.Owner, new List<UUID>(){ item.ID });
                }
            }
        }

        protected void AddUserData(SceneObjectGroup sog)
        {
            UserManagementModule.AddCreatorUser(sog.RootPart.CreatorID, sog.RootPart.CreatorData);
            foreach (SceneObjectPart sop in sog.Parts)
                UserManagementModule.AddCreatorUser(sop.CreatorID, sop.CreatorData);
        }

        public virtual void TransferInventoryAssets(InventoryItemBase item, UUID sender, UUID receiver)
        {
        }

        public virtual bool CanGetAgentInventoryItem(IClientAPI remoteClient, UUID itemID, UUID requestID)
        {
            InventoryItemBase assetRequestItem = GetItem(remoteClient.AgentId, itemID);

            if (assetRequestItem is null)
            {
                ILibraryService lib = m_Scene.RequestModuleInterface<ILibraryService>();

                if (lib is not null)
                    assetRequestItem = lib.LibraryRootFolder.FindItem(itemID);

                if (assetRequestItem is null)
                    return false;
            }

            // At this point, we need to apply perms
            // only to notecards and scripts. All
            // other asset types are always available
            //
            if (assetRequestItem.AssetType == (int)AssetType.LSLText)
            {
                if (!m_Scene.Permissions.CanViewScript(itemID, UUID.Zero, remoteClient.AgentId))
                {
                    remoteClient.SendAgentAlertMessage("Insufficient permissions to view script", false);
                    return false;
                }
            }
            else if (assetRequestItem.AssetType == (int)AssetType.Notecard)
            {
                if (!m_Scene.Permissions.CanViewNotecard(itemID, UUID.Zero, remoteClient.AgentId))
                {
                    remoteClient.SendAgentAlertMessage("Insufficient permissions to view notecard", false);
                    return false;
                }
            }

            if (assetRequestItem.AssetID != requestID)
            {
                m_log.WarnFormat(
                    "[INVENTORY ACCESS MODULE]:  {0} requested asset {1} from item {2} but this does not match item's asset {3}",
                    Name, requestID, itemID, assetRequestItem.AssetID);

                return false;
            }

            return true;
        }


        public virtual bool IsForeignUser(UUID userID, out string assetServerURL)
        {
            assetServerURL = string.Empty;
            return false;
        }

        #endregion

        #region Misc

        /// <summary>
        /// Create a new asset data structure.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="invType"></param>
        /// <param name="assetType"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private AssetBase CreateAsset(string name, string description, sbyte assetType, byte[] data, string creatorID)
        {
            AssetBase asset = new(UUID.Random(), name, assetType, creatorID)
            {
                Description = description,
                Data = data ?? (new byte[1])
            };

            return asset;
        }

        protected virtual InventoryItemBase GetItem(UUID agentID, UUID itemID)
        {
            IInventoryService invService = m_Scene.RequestModuleInterface<IInventoryService>();
            InventoryItemBase item = invService.GetItem(agentID, itemID);

            if (item is not null && !string.IsNullOrEmpty(item.CreatorData))
                UserManagementModule.AddCreatorUser(item.CreatorIdAsUuid, item.CreatorData);

            return item;
        }

        #endregion
    }
}
