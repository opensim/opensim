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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Grid.Framework;


namespace OpenSim.Grid.UserServer.Modules
{
    public class AvatarCreationModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private UserDataBaseService m_userDataBaseService;
        //  private BaseHttpServer m_httpServer;
        // TODO: unused: private UserConfig m_config;

        private string m_inventoryServerUrl;
        private IInterServiceInventoryServices m_inventoryService;

        public AvatarCreationModule(UserDataBaseService userDataBaseService, UserConfig config, IInterServiceInventoryServices inventoryService)
        {
            // TODO: unused: m_config = config;
            m_userDataBaseService = userDataBaseService;
            m_inventoryService = inventoryService;
            m_inventoryServerUrl = config.InventoryUrl.OriginalString;
        }

        public void Initialise(IGridServiceCore core)
        {
            CommandConsole console;
            if (core.TryGet<CommandConsole>(out console))
            {
                console.Commands.AddCommand("userserver", false, "clone avatar",
                    "clone avatar <TemplateAvatarFirstName> <TemplateAvatarLastName> <TargetAvatarFirstName> <TargetAvatarLastName>",
                    "Clone the template avatar's inventory into a target avatar", RunCommand);
            }
        }

        public void PostInitialise()
        {

        }

        public void RegisterHandlers(BaseHttpServer httpServer)
        {
        }

        public void RunCommand(string module, string[] cmd)
        {
            if ((cmd.Length == 6) && (cmd[0] == "clone") && (cmd[1] == "avatar"))
            {
                try
                {
                    string tFirst = cmd[2];
                    string tLast = cmd[3];

                    string nFirst = cmd[4];
                    string nLast = cmd[5];

                    UserProfileData templateAvatar = m_userDataBaseService.GetUserProfile(tFirst, tLast);
                    UserProfileData newAvatar = m_userDataBaseService.GetUserProfile(nFirst, nLast);

                    if (templateAvatar == null)
                    {
                        m_log.ErrorFormat("[AvatarAppearance] Clone Avatar: Could not find template avatar {0} , {1}", tFirst, tLast);
                        return;
                    }

                    if (newAvatar == null)
                    {
                        m_log.ErrorFormat("[AvatarAppearance] Clone Avatar: Could not find target avatar {0} , {1}", nFirst, nLast);
                        return;
                    }
                    Guid avatar = newAvatar.ID.Guid;
                    Guid template = templateAvatar.ID.Guid;
                    CloneAvatar(avatar, template, true, true);

                }
                catch (Exception e)
                {
                    m_log.Error("Error: " + e.ToString());
                }
            }
        }
        #region Avatar Appearance Creation

        public bool CloneAvatar(Guid avatarID, Guid templateID, bool modifyPermissions, bool removeTargetsClothes)
        {
            m_log.InfoFormat("[AvatarAppearance] Starting to clone avatar {0} inventory to avatar {1}", templateID.ToString(), avatarID.ToString());
            // TODO: unused: Guid bodyFolder = Guid.Empty;
            // TODO: unused: Guid clothesFolder = Guid.Empty;
            bool success = false;

            UUID avID = new UUID(avatarID);
            List<InventoryFolderBase> avatarInventory = m_inventoryService.GetInventorySkeleton(avID);
            if ((avatarInventory == null) || (avatarInventory.Count == 0))
            {
                m_log.InfoFormat("[AvatarAppearance] No inventory found for user {0} , so creating it", avID.ToString());
                m_inventoryService.CreateNewUserInventory(avID);
                Thread.Sleep(5000);
                avatarInventory = m_inventoryService.GetInventorySkeleton(avID);
            }

            if ((avatarInventory != null) && (avatarInventory.Count > 0))
            {
                UUID tempOwnID = new UUID(templateID);
                AvatarAppearance appearance = m_userDataBaseService.GetUserAppearance(tempOwnID);

                if (removeTargetsClothes)
                {
                    //remove clothes and attachments from target avatar so that the end result isn't a merger of its existing clothes 
                    // and the clothes from the template avatar. 
                    RemoveClothesAndAttachments(avID);
                }

                List<InventoryFolderBase> templateInventory = m_inventoryService.GetInventorySkeleton(tempOwnID);
                if ((templateInventory != null) && (templateInventory.Count != 0))
                {
                    for (int i = 0; i < templateInventory.Count; i++)
                    {
                        if (templateInventory[i].ParentID == UUID.Zero)
                        {
                            success = CloneFolder(avatarInventory, avID, UUID.Zero, appearance, templateInventory[i], templateInventory, modifyPermissions);
                            break;
                        }
                    }
                }
                else
                {
                    m_log.InfoFormat("[AvatarAppearance] Failed to find the template owner's {0} inventory", tempOwnID);
                }
            }
            m_log.InfoFormat("[AvatarAppearance] finished cloning avatar with result: {0}", success);
            return success;
        }

        private bool CloneFolder(List<InventoryFolderBase> avatarInventory, UUID avID, UUID parentFolder, AvatarAppearance appearance, InventoryFolderBase templateFolder, List<InventoryFolderBase> templateFolders, bool modifyPermissions)
        {
            bool success = false;
            UUID templateFolderId = templateFolder.ID;
            if (templateFolderId != UUID.Zero)
            {
                InventoryFolderBase toFolder = FindFolder(templateFolder.Name, parentFolder.Guid, avatarInventory);
                if (toFolder == null)
                {
                    //create new folder
                    toFolder = new InventoryFolderBase();
                    toFolder.ID = UUID.Random();
                    toFolder.Name = templateFolder.Name;
                    toFolder.Owner = avID;
                    toFolder.Type = templateFolder.Type;
                    toFolder.Version = 1;
                    toFolder.ParentID = parentFolder;
                    if (!SynchronousRestObjectRequester.MakeRequest<InventoryFolderBase, bool>(
                "POST", m_inventoryServerUrl + "CreateFolder/", toFolder))
                    {
                        m_log.InfoFormat("[AvatarApperance] Couldn't make new folder {0} in users inventory", toFolder.Name);
                        return false;
                    }
                    else
                    {
                        // m_log.InfoFormat("made new folder {0} in users inventory", toFolder.Name);
                    }
                }

                List<InventoryItemBase> templateItems = SynchronousRestObjectRequester.MakeRequest<Guid, List<InventoryItemBase>>(
               "POST", m_inventoryServerUrl + "GetItems/", templateFolderId.Guid);
                if ((templateItems != null) && (templateItems.Count > 0))
                {
                    List<ClothesAttachment> wornClothes = new List<ClothesAttachment>();
                    List<ClothesAttachment> attachedItems = new List<ClothesAttachment>();

                    foreach (InventoryItemBase item in templateItems)
                    {

                        UUID clonedItemId = CloneInventoryItem(avID, toFolder.ID, item, modifyPermissions);
                        if (clonedItemId != UUID.Zero)
                        {
                            int appearanceType = ItemIsPartOfAppearance(item, appearance);
                            if (appearanceType >= 0)
                            {
                               // UpdateAvatarAppearance(avID, appearanceType, clonedItemId, item.AssetID);
                                wornClothes.Add(new ClothesAttachment(appearanceType, clonedItemId, item.AssetID));
                            }

                            if (appearance != null)
                            {
                                int attachment = appearance.GetAttachpoint(item.ID);
                                if (attachment > 0)
                                {
                                    //UpdateAvatarAttachment(avID, attachment, clonedItemId, item.AssetID);
                                    attachedItems.Add(new ClothesAttachment(attachment, clonedItemId, item.AssetID));
                                }
                            }
                            success = true;
                        }
                    }

                    if ((wornClothes.Count > 0) || (attachedItems.Count > 0))
                    {
                        //Update the worn clothes and attachments
                        AvatarAppearance targetAppearance = GetAppearance(avID);
                        if (targetAppearance != null)
                        {
                            foreach (ClothesAttachment wornItem in wornClothes)
                            {
                                targetAppearance.Wearables[wornItem.Type].AssetID = wornItem.AssetID;
                                targetAppearance.Wearables[wornItem.Type].ItemID = wornItem.ItemID;
                            }

                            foreach (ClothesAttachment wornItem in attachedItems)
                            {
                                targetAppearance.SetAttachment(wornItem.Type, wornItem.ItemID, wornItem.AssetID);
                            }

                            m_userDataBaseService.UpdateUserAppearance(avID, targetAppearance);
                            wornClothes.Clear();
                            attachedItems.Clear();
                        }
                    }
                }

                List<InventoryFolderBase> subFolders = FindSubFolders(templateFolder.ID.Guid, templateFolders);
                foreach (InventoryFolderBase subFolder in subFolders)
                {
                    if (subFolder.Name.ToLower() != "trash")
                    {
                        success = CloneFolder(avatarInventory, avID, toFolder.ID, appearance, subFolder, templateFolders, modifyPermissions);
                    }
                }
            }
            else
            {
                m_log.Info("[AvatarAppearance] Failed to find the template folder");
            }
            return success;
        }

        private UUID CloneInventoryItem(UUID avatarID, UUID avatarFolder, InventoryItemBase item, bool modifyPerms)
        {
            if (avatarFolder != UUID.Zero)
            {
                InventoryItemBase clonedItem = new InventoryItemBase();
                clonedItem.Owner = avatarID;
                clonedItem.AssetID = item.AssetID;
                clonedItem.AssetType = item.AssetType;
                clonedItem.BasePermissions = item.BasePermissions;
                clonedItem.CreationDate = item.CreationDate;
                clonedItem.CreatorId = item.CreatorId;
                clonedItem.CreatorIdAsUuid = item.CreatorIdAsUuid;
                clonedItem.CurrentPermissions = item.CurrentPermissions;
                clonedItem.Description = item.Description;
                clonedItem.EveryOnePermissions = item.EveryOnePermissions;
                clonedItem.Flags = item.Flags;
                clonedItem.Folder = avatarFolder;
                clonedItem.GroupID = item.GroupID;
                clonedItem.GroupOwned = item.GroupOwned;
                clonedItem.GroupPermissions = item.GroupPermissions;
                clonedItem.ID = UUID.Random();
                clonedItem.InvType = item.InvType;
                clonedItem.Name = item.Name;
                clonedItem.NextPermissions = item.NextPermissions;
                clonedItem.SalePrice = item.SalePrice;
                clonedItem.SaleType = item.SaleType;

                if (modifyPerms)
                {
                    ModifyPermissions(ref clonedItem);
                }

                SynchronousRestObjectRequester.MakeRequest<InventoryItemBase, bool>(
                    "POST", m_inventoryServerUrl + "AddNewItem/", clonedItem);

                return clonedItem.ID;
            }

            return UUID.Zero;
        }

        // TODO: unused
        // private void UpdateAvatarAppearance(UUID avatarID, int wearableType, UUID itemID, UUID assetID)
        // {
        //     AvatarAppearance appearance = GetAppearance(avatarID);
        //     appearance.Wearables[wearableType].AssetID = assetID;
        //     appearance.Wearables[wearableType].ItemID = itemID;
        //     m_userDataBaseService.UpdateUserAppearance(avatarID, appearance);
        // }

        // TODO: unused
        // private void UpdateAvatarAttachment(UUID avatarID, int attachmentPoint, UUID itemID, UUID assetID)
        // {
        //     AvatarAppearance appearance = GetAppearance(avatarID);
        //     appearance.SetAttachment(attachmentPoint, itemID, assetID);
        //     m_userDataBaseService.UpdateUserAppearance(avatarID, appearance);
        // }

        private void RemoveClothesAndAttachments(UUID avatarID)
        {
            AvatarAppearance appearance = GetAppearance(avatarID);

            appearance.ClearWearables();
            appearance.ClearAttachments();
            m_userDataBaseService.UpdateUserAppearance(avatarID, appearance);

        }

        private AvatarAppearance GetAppearance(UUID avatarID)
        {
            AvatarAppearance appearance = m_userDataBaseService.GetUserAppearance(avatarID);
            if (appearance == null)
            {
                appearance = CreateDefaultAppearance(avatarID);
            }
            return appearance;
        }

        // TODO: unused
        // private UUID FindFolderID(string name, List<InventoryFolderBase> folders)
        // {
        //     foreach (InventoryFolderBase folder in folders)
        //     {
        //         if (folder.Name == name)
        //         {
        //             return folder.ID;
        //         }
        //     }
        //     return UUID.Zero;
        // }

        // TODO: unused
        // private InventoryFolderBase FindFolder(string name, List<InventoryFolderBase> folders)
        // {
        //     foreach (InventoryFolderBase folder in folders)
        //     {
        //         if (folder.Name == name)
        //         {
        //             return folder;
        //         }
        //     }
        //     return null;
        // }

        private InventoryFolderBase FindFolder(string name, Guid parentFolderID, List<InventoryFolderBase> folders)
        {
            foreach (InventoryFolderBase folder in folders)
            {
                if ((folder.Name == name) && (folder.ParentID.Guid == parentFolderID))
                {
                    return folder;
                }
            }
            return null;
        }

        // TODO: unused
        // private InventoryItemBase GetItem(string itemName, List<InventoryItemBase> items)
        // {
        //     foreach (InventoryItemBase item in items)
        //     {
        //         if (item.Name.ToLower() == itemName.ToLower())
        //         {
        //             return item;
        //         }
        //     }
        //     return null;
        // }

        private List<InventoryFolderBase> FindSubFolders(Guid parentFolderID, List<InventoryFolderBase> folders)
        {
            List<InventoryFolderBase> subFolders = new List<InventoryFolderBase>();
            foreach (InventoryFolderBase folder in folders)
            {
                if (folder.ParentID.Guid == parentFolderID)
                {
                    subFolders.Add(folder);
                }
            }
            return subFolders;
        }

        protected virtual void ModifyPermissions(ref InventoryItemBase item)
        {
            // Propagate Permissions
            item.BasePermissions = item.BasePermissions & item.NextPermissions;
            item.CurrentPermissions = item.BasePermissions;
            item.EveryOnePermissions = item.EveryOnePermissions & item.NextPermissions;
            item.GroupPermissions = item.GroupPermissions & item.NextPermissions;

        }

        private AvatarAppearance CreateDefaultAppearance(UUID avatarId)
        {
            AvatarAppearance appearance = null;
            AvatarWearable[] wearables;
            byte[] visualParams;
            GetDefaultAvatarAppearance(out wearables, out visualParams);
            appearance = new AvatarAppearance(avatarId, wearables, visualParams);

            return appearance;
        }

        private static void GetDefaultAvatarAppearance(out AvatarWearable[] wearables, out byte[] visualParams)
        {
            visualParams = GetDefaultVisualParams();
            wearables = AvatarWearable.DefaultWearables;
        }

        private static byte[] GetDefaultVisualParams()
        {
            byte[] visualParams;
            visualParams = new byte[218];
            for (int i = 0; i < 218; i++)
            {
                visualParams[i] = 100;
            }
            return visualParams;
        }

        private int ItemIsPartOfAppearance(InventoryItemBase item, AvatarAppearance appearance)
        {
            if (appearance != null)
            {
                if (appearance.BodyItem == item.ID)
                    return (int)WearableType.Shape;

                if (appearance.EyesItem == item.ID)
                    return (int)WearableType.Eyes;

                if (appearance.GlovesItem == item.ID)
                    return (int)WearableType.Gloves;

                if (appearance.HairItem == item.ID)
                    return (int)WearableType.Hair;

                if (appearance.JacketItem == item.ID)
                    return (int)WearableType.Jacket;

                if (appearance.PantsItem == item.ID)
                    return (int)WearableType.Pants;

                if (appearance.ShirtItem == item.ID)
                    return (int)WearableType.Shirt;

                if (appearance.ShoesItem == item.ID)
                    return (int)WearableType.Shoes;

                if (appearance.SkinItem == item.ID)
                    return (int)WearableType.Skin;

                if (appearance.SkirtItem == item.ID)
                    return (int)WearableType.Skirt;

                if (appearance.SocksItem == item.ID)
                    return (int)WearableType.Socks;

                if (appearance.UnderPantsItem == item.ID)
                    return (int)WearableType.Underpants;

                if (appearance.UnderShirtItem == item.ID)
                    return (int)WearableType.Undershirt;
            }
            return -1;
        }
        #endregion

        public enum PermissionMask
        {
            None = 0,
            Transfer = 8192,
            Modify = 16384,
            Copy = 32768,
            Move = 524288,
            Damage = 1048576,
            All = 2147483647,
        }

        public enum WearableType
        {
            Shape = 0,
            Skin = 1,
            Hair = 2,
            Eyes = 3,
            Shirt = 4,
            Pants = 5,
            Shoes = 6,
            Socks = 7,
            Jacket = 8,
            Gloves = 9,
            Undershirt = 10,
            Underpants = 11,
            Skirt = 12,
        }

        public class ClothesAttachment
        {
            public int Type;
            public UUID ItemID;
            public UUID AssetID;

            public ClothesAttachment(int type, UUID itemID, UUID assetID)
            {
                Type = type;
                ItemID = itemID;
                AssetID = assetID;
            }
        }
    }
}