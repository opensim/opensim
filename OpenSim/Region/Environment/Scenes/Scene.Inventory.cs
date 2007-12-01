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
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public partial class Scene
    {
        //split these method into this partial as a lot of these (hopefully) are only temporary and won't be needed once Caps is more complete
        // or at least some of they can be moved somewhere else

        public void AddInventoryItem(LLUUID avatarId, InventoryItemBase item)
        {
            ScenePresence avatar;

            if (TryGetAvatar(avatarId, out avatar))
            {
                AddInventoryItem(avatar.ControllingClient, item);
            }
        }

        public void AddInventoryItem(IClientAPI remoteClient, InventoryItemBase item)
        {
            CachedUserInfo userInfo = CommsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                userInfo.AddItem(remoteClient.AgentId, item);
                remoteClient.SendInventoryItemUpdate(item);
            }
        }

        public LLUUID CapsUpdateInventoryItemAsset(LLUUID avatarId, LLUUID itemID, byte[] data)
        {
            ScenePresence avatar;

            if (TryGetAvatar(avatarId, out avatar))
            {
                return CapsUpdateInventoryItemAsset(avatar.ControllingClient, itemID, data);
            }

            return LLUUID.Zero;
        }

        public LLUUID CapsUpdateInventoryItemAsset(IClientAPI remoteClient, LLUUID itemID, byte[] data)
        {
            CachedUserInfo userInfo = CommsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                if (userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                    if (item != null)
                    {
                        AssetBase asset = CreateAsset(item.inventoryName, item.inventoryDescription, (sbyte) item.invType, (sbyte) item.assetType, data);
                        AssetCache.AddAsset(asset);

                        item.assetID = asset.FullID;
                        userInfo.UpdateItem(remoteClient.AgentId, item);

                        // remoteClient.SendInventoryItemUpdate(item);
                        if ((InventoryType) item.invType == InventoryType.Notecard)
                        {
                            //do we want to know about updated note cards?
                        }
                        else if ((InventoryType) item.invType == InventoryType.LSL)
                        {
                            // do we want to know about updated scripts
                        }

                        return (asset.FullID);
                    }
                }
            }
            return LLUUID.Zero;
        }

        public void UDPUpdateInventoryItemAsset(IClientAPI remoteClient, LLUUID transactionID, LLUUID assetID,
                                                LLUUID itemID)
        {
            CachedUserInfo userInfo = CommsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                if (userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                    if (item != null)
                    {
                        AgentAssetTransactions transactions =
                            CommsManager.TransactionsManager.GetUserTransActions(remoteClient.AgentId);
                        if (transactions != null)
                        {
                            AssetBase asset = null;
                            bool addToCache = false;

                            asset = AssetCache.GetAsset(assetID);
                            if (asset == null)
                            {
                                asset = transactions.GetTransactionAsset(transactionID);
                                addToCache = true;
                            }

                            if (asset != null)
                            {
                                if (asset.FullID == assetID)
                                {
                                    asset.Name = item.inventoryName;
                                    asset.Description = item.inventoryDescription;
                                    asset.InvType = (sbyte) item.invType;
                                    asset.Type = (sbyte) item.assetType;
                                    item.assetID = asset.FullID;

                                    if (addToCache)
                                    {
                                        AssetCache.AddAsset(asset);
                                    }

                                    userInfo.UpdateItem(remoteClient.AgentId, item);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void CopyInventoryItem(IClientAPI remoteClient, uint callbackID, LLUUID oldAgentID, LLUUID oldItemID, LLUUID newFolderID, string newName)
        {
            InventoryItemBase item = CommsManager.UserProfileCache.libraryRoot.HasItem(oldItemID);
            if (item == null)
            {
                CachedUserInfo userInfo = CommsManager.UserProfileCache.GetUserDetails(oldAgentID);
                if (userInfo == null)
                {
                    MainLog.Instance.Warn("INVENTORY", "Failed to find user " + oldAgentID.ToString());
                    return;
                }

                if (userInfo.RootFolder != null)
                {
                    item = userInfo.RootFolder.HasItem(oldItemID);
                    if (item == null)
                    {
                        MainLog.Instance.Warn("INVENTORY", "Failed to find item " + oldItemID.ToString());
                        return;
                    }
                }
                else
                {
                    MainLog.Instance.Warn("INVENTORY", "Failed to find item " + oldItemID.ToString());
                    return;
                }
            }


            AssetBase asset = AssetCache.CopyAsset(item.assetID);
            if (asset == null)
            {
                MainLog.Instance.Warn("INVENTORY", "Failed to find asset " + item.assetID.ToString());
                return;
            }

            asset.Name = (newName.Length == 0) ? item.inventoryName : newName;
            
            // TODO: preserve current permissions?
            CreateNewInventoryItem(remoteClient, newFolderID, callbackID, asset, item.inventoryNextPermissions);
        }

        private AssetBase CreateAsset(string name, string description, sbyte invType, sbyte assetType, byte[] data)
        {
            AssetBase asset = new AssetBase();
            asset.Name = name;
            asset.Description = description;
            asset.InvType = invType;
            asset.Type = assetType;
            asset.FullID = LLUUID.Random(); // TODO: check for conflicts
            asset.Data = (data == null) ? new byte[1] : data;
            return asset;
        }

        private void CreateNewInventoryItem(IClientAPI remoteClient, LLUUID folderID, uint callbackID,
                                            AssetBase asset, uint nextOwnerMask)
        {
            CachedUserInfo userInfo = CommsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                InventoryItemBase item = new InventoryItemBase();
                item.avatarID = remoteClient.AgentId;
                item.creatorsID = remoteClient.AgentId;
                item.inventoryID = LLUUID.Random();
                item.assetID = asset.FullID;
                item.inventoryDescription = asset.Description;
                item.inventoryName = asset.Name;
                item.assetType = asset.Type;
                item.invType = asset.InvType;
                item.parentFolderID = folderID;
                item.inventoryCurrentPermissions = 2147483647;
                item.inventoryNextPermissions = nextOwnerMask;

                userInfo.AddItem(remoteClient.AgentId, item);
                remoteClient.SendInventoryItemUpdate(item);
            }
        }

        /// <summary>
        /// temporary method to test out creating new inventory items
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="transActionID"></param>
        /// <param name="folderID"></param>
        /// <param name="callbackID"></param>
        /// <param name="description"></param>
        /// <param name="name"></param>
        /// <param name="invType"></param>
        /// <param name="type"></param>
        /// <param name="wearableType"></param>
        /// <param name="nextOwnerMask"></param>
        public void CreateNewInventoryItem(IClientAPI remoteClient, LLUUID transActionID, LLUUID folderID,
                                           uint callbackID, string description, string name, sbyte invType, sbyte assetType,
                                           byte wearableType, uint nextOwnerMask)
        {
            if (transActionID == LLUUID.Zero)
            {
                CachedUserInfo userInfo = CommsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
                if (userInfo != null)
                {
                    AssetBase asset = CreateAsset(name, description, invType, assetType, null);
                    AssetCache.AddAsset(asset);

                    CreateNewInventoryItem(remoteClient, folderID, callbackID, asset, nextOwnerMask);
                }
            }
            else
            {
                CommsManager.TransactionsManager.HandleInventoryFromTransaction(remoteClient, transActionID, folderID,
                                                                                callbackID, description, name, invType,
                                                                                assetType, wearableType, nextOwnerMask);
                //System.Console.WriteLine("request to create inventory item from transaction " + transActionID);
            }
        }

        private SceneObjectGroup GetGroupByPrim(uint localID)
        {
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).HasChildPrim(localID))
                        return (SceneObjectGroup)ent;
                }
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="primLocalID"></param>
        public void RequestTaskInventory(IClientAPI remoteClient, uint primLocalID)
        {
            SceneObjectGroup group = GetGroupByPrim(primLocalID);
            if (group != null)
            {
                bool fileChange = group.GetPartInventoryFileName(remoteClient, primLocalID);
                if (fileChange)
                {
                    if (XferManager != null)
                    {
                        group.RequestInventoryFile(primLocalID, XferManager);
                    }
                }
            }
        }

        public void RemoveTaskInventory(IClientAPI remoteClient, LLUUID itemID, uint localID)
        {
            SceneObjectGroup group = GetGroupByPrim(localID);
            if (group != null)
            {
                int type = group.RemoveInventoryItem(remoteClient, localID, itemID);
                group.GetProperites(remoteClient);
                if (type == 10)
                {
                    EventManager.TriggerRemoveScript(localID, itemID);
                }
            }
        }

        public void RezScript(IClientAPI remoteClient, LLUUID itemID, uint localID)
        {
            CachedUserInfo userInfo = CommsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
            LLUUID copyID = LLUUID.Random();
            if (userInfo != null)
            {
                if (userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                    if (item != null)
                    {
                        bool isTexture = false;
                        bool rezzed = false;
                        if (item.invType == 0)
                        {
                            isTexture = true;
                        }
                        AssetBase rezAsset = AssetCache.GetAsset(item.assetID, isTexture);

                        if (rezAsset == null)
                        {
                            // lets try once more in case the asset cache is being slow getting the asset from server
                            rezAsset = AssetCache.GetAsset(item.assetID, isTexture);
                        }

                        if (rezAsset != null)
                        {
                            string script = Util.FieldToString(rezAsset.Data);
                            //Console.WriteLine("rez script "+script);
                            EventManager.TriggerRezScript(localID, copyID, script);
                            rezzed = true;
                        }

                        if (rezzed)
                        {
                            SceneObjectGroup group = GetGroupByPrim(localID);
                            if (group != null)
                            {
                                // TODO: do we care about the value of this bool?
                                bool added = group.AddInventoryItem(remoteClient, localID, item, copyID);
                                group.GetProperites(remoteClient);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="simClient"></param>
        public virtual void DeRezObject(Packet packet, IClientAPI remoteClient)
        {
            DeRezObjectPacket DeRezPacket = (DeRezObjectPacket) packet;

            if (DeRezPacket.AgentBlock.DestinationID == LLUUID.Zero)
            {
                //currently following code not used (or don't know of any case of destination being zero
            }
            else
            {
                foreach (DeRezObjectPacket.ObjectDataBlock Data in DeRezPacket.ObjectData)
                {
                    EntityBase selectedEnt = null;
                    //OpenSim.Framework.Console.MainConsole.Instance.WriteLine("LocalID:" + Data.ObjectLocalID.ToString());
                    foreach (EntityBase ent in Entities.Values)
                    {
                        if (ent.LocalId == Data.ObjectLocalID)
                        {
                            selectedEnt = ent;
                            break;
                        }
                    }
                    if (selectedEnt != null)
                    {
                        if (PermissionsMngr.CanDeRezObject(remoteClient.AgentId, ((SceneObjectGroup) selectedEnt).UUID))
                        {
                            string sceneObjectXml = ((SceneObjectGroup) selectedEnt).ToXmlString();
                            CachedUserInfo userInfo = CommsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
                            if (userInfo != null)
                            {
                                AssetBase asset = CreateAsset(
                                    ((SceneObjectGroup) selectedEnt).GetPartName(selectedEnt.LocalId),
                                    ((SceneObjectGroup) selectedEnt).GetPartDescription(selectedEnt.LocalId),
                                    (sbyte) InventoryType.Object,
                                    (sbyte) AssetType.Object, // TODO: after libSL r1357, this becomes AssetType.Primitive
                                    Helpers.StringToField(sceneObjectXml));
                                AssetCache.AddAsset(asset);

                                InventoryItemBase item = new InventoryItemBase();
                                item.avatarID = remoteClient.AgentId;
                                item.creatorsID = remoteClient.AgentId;
                                item.inventoryID = LLUUID.Random(); // TODO: check for conflicts
                                item.assetID = asset.FullID;
                                item.inventoryDescription = asset.Description;
                                item.inventoryName = asset.Name;
                                item.assetType = asset.Type;
                                item.invType = asset.InvType;
                                item.parentFolderID = DeRezPacket.AgentBlock.DestinationID;
                                item.inventoryCurrentPermissions = 2147483647;
                                item.inventoryNextPermissions = 2147483647;

                                userInfo.AddItem(remoteClient.AgentId, item);
                                remoteClient.SendInventoryItemUpdate(item);
                            }

                            DeleteSceneObjectGroup((SceneObjectGroup) selectedEnt);
                        }
                    }
                }
            }
        }

        public void DeleteSceneObjectGroup(SceneObjectGroup group)
        {
            SceneObjectPart rootPart = (group).GetChildPart(group.UUID);
            if (rootPart.PhysActor != null)
            {
                PhysicsScene.RemovePrim(rootPart.PhysActor);
                rootPart.PhysActor = null;
            }

            m_storageManager.DataStore.RemoveObject(group.UUID, m_regInfo.RegionID);
            group.DeleteGroup();

            lock (Entities)
            {
                Entities.Remove(group.UUID);
            }
            group.DeleteParts();
        }

        public virtual void RezObject(IClientAPI remoteClient, LLUUID itemID, LLVector3 pos)
        {
            CachedUserInfo userInfo = CommsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                if (userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                    if (item != null)
                    {
                        AssetBase rezAsset = AssetCache.GetAsset(item.assetID, false);

                        if (rezAsset == null)
                        {
                            // lets try once more in case the asset cache is being slow getting the asset from server
                            rezAsset = AssetCache.GetAsset(item.assetID, false);
                        }

                        if (rezAsset != null)
                        {
                            AddRezObject(Util.FieldToString(rezAsset.Data), pos);
                            //userInfo.DeleteItem(remoteClient.AgentId, item);
                            //remoteClient.SendRemoveInventoryItem(itemID);
                        }
                    }
                }
            }
        }

        private void AddRezObject(string xmlData, LLVector3 pos)
        {
            SceneObjectGroup group = new SceneObjectGroup(this, m_regionHandle, xmlData);
            AddEntity(group);
            group.AbsolutePosition = pos;
            SceneObjectPart rootPart = group.GetChildPart(group.UUID);
            bool UsePhysics = (((rootPart.ObjectFlags & (uint)LLObject.ObjectFlags.Physics) > 0)&& m_physicalPrim);
            if ((rootPart.ObjectFlags & (uint) LLObject.ObjectFlags.Phantom) == 0)
            {
                PrimitiveBaseShape pbs = rootPart.Shape;
                rootPart.PhysActor = PhysicsScene.AddPrimShape(
                    rootPart.Name,
                    pbs,
                    new PhysicsVector(rootPart.AbsolutePosition.X, rootPart.AbsolutePosition.Y,
                                      rootPart.AbsolutePosition.Z),
                    new PhysicsVector(rootPart.Scale.X, rootPart.Scale.Y, rootPart.Scale.Z),
                    new Quaternion(rootPart.RotationOffset.W, rootPart.RotationOffset.X,
                                   rootPart.RotationOffset.Y, rootPart.RotationOffset.Z), UsePhysics);

                rootPart.DoPhysicsPropertyUpdate(UsePhysics, true);
                
            }
            rootPart.ScheduleFullUpdate();
        }
    }
}