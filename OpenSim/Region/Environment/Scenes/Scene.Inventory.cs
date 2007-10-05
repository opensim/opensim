using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Caches;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
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

            if ( TryGetAvatar( avatarId, out avatar ))
            {
                AddInventoryItem(avatar.ControllingClient, item);
            }
        }

        public void AddInventoryItem(IClientAPI remoteClient, InventoryItemBase item)
        {
            CachedUserInfo userInfo = commsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
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
            CachedUserInfo userInfo = commsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                if (userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                    if (item != null)
                    {
                        AssetBase asset;
                        asset = new AssetBase();
                        asset.FullID = LLUUID.Random();
                        asset.Type = (sbyte) item.assetType;
                        asset.InvType = (sbyte) item.invType;
                        asset.Name = item.inventoryName;
                        asset.Data = data;
                        commsManager.AssetCache.AddAsset(asset);

                        item.assetID = asset.FullID;
                        userInfo.UpdateItem(remoteClient.AgentId, item);

                        // remoteClient.SendInventoryItemUpdate(item);
                        if (item.invType == 7)
                        {
                            //do we want to know about updated note cards?
                        }
                        else if (item.invType == 10)
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
            CachedUserInfo userInfo = commsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                if (userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                    if (item != null)
                    {
                        AgentAssetTransactions transactions =
                            commsManager.TransactionsManager.GetUserTransActions(remoteClient.AgentId);
                        if (transactions != null)
                        {
                            AssetBase asset = null;
                            bool addToCache = false;

                            asset = commsManager.AssetCache.GetAsset(assetID);
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
                                        commsManager.AssetCache.AddAsset(asset);
                                    }

                                    userInfo.UpdateItem(remoteClient.AgentId, item);
                                }
                            }
                        }
                    }
                }
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
                                           uint callbackID, string description, string name, sbyte invType, sbyte type,
                                           byte wearableType, uint nextOwnerMask)
        {
            if (transActionID == LLUUID.Zero)
            {
                CachedUserInfo userInfo = commsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
                if (userInfo != null)
                {
                    AssetBase asset = new AssetBase();
                    asset.Name = name;
                    asset.Description = description;
                    asset.InvType = invType;
                    asset.Type = type;
                    asset.FullID = LLUUID.Random();
                    asset.Data = new byte[1];
                    commsManager.AssetCache.AddAsset(asset);

                    InventoryItemBase item = new InventoryItemBase();
                    item.avatarID = remoteClient.AgentId;
                    item.creatorsID = remoteClient.AgentId;
                    item.inventoryID = LLUUID.Random();
                    item.assetID = asset.FullID;
                    item.inventoryDescription = description;
                    item.inventoryName = name;
                    item.assetType = invType;
                    item.invType = invType;
                    item.parentFolderID = folderID;
                    item.inventoryCurrentPermissions = 2147483647;
                    item.inventoryNextPermissions = nextOwnerMask;

                    userInfo.AddItem(remoteClient.AgentId, item);
                    remoteClient.SendInventoryItemUpdate(item);
                }
            }
            else
            {
                commsManager.TransactionsManager.HandleInventoryFromTransaction(remoteClient, transActionID, folderID,
                                                                                callbackID, description, name, invType,
                                                                                type, wearableType, nextOwnerMask);
                //System.Console.WriteLine("request to create inventory item from transaction " + transActionID);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="primLocalID"></param>
        public void RequestTaskInventory(IClientAPI remoteClient, uint primLocalID)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup) ent).HasChildPrim(primLocalID);
                    if (hasPrim != false)
                    {
                        bool fileChange = ((SceneObjectGroup) ent).GetPartInventoryFileName(remoteClient, primLocalID);
                        if (fileChange)
                        {
                            if (XferManager != null)
                            {
                                ((SceneObjectGroup) ent).RequestInventoryFile(primLocalID, XferManager);
                            }
                        }
                        break;
                    }
                }
            }
        }

        public void RemoveTaskInventory(IClientAPI remoteClient, LLUUID itemID, uint localID)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup) ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        int type = ((SceneObjectGroup) ent).RemoveInventoryItem(remoteClient, localID, itemID);
                        ((SceneObjectGroup) ent).GetProperites(remoteClient);
                        if (type == 10)
                        {
                            EventManager.TriggerRemoveScript(localID, itemID);
                        }
                    }
                }
            }
        }

        public void RezScript(IClientAPI remoteClient, LLUUID itemID, uint localID)
        {
            CachedUserInfo userInfo = commsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
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
                        AssetBase rezAsset = commsManager.AssetCache.GetAsset(item.assetID, isTexture);
                        if (rezAsset != null)
                        {
                            string script = Util.FieldToString(rezAsset.Data);
                            //Console.WriteLine("rez script "+script);
                            EventManager.TriggerRezScript(localID, copyID, script);
                            rezzed = true;
                        }
                        else
                        {
                            //lets try once more incase the asset cache is being slow getting the asset from server
                            rezAsset = commsManager.AssetCache.GetAsset(item.assetID, isTexture);
                            if (rezAsset != null)
                            {
                                string script = Util.FieldToString(rezAsset.Data);
                                // Console.WriteLine("rez script " + script);
                                EventManager.TriggerRezScript(localID, copyID, script);
                                rezzed = true;
                            }
                        }

                        if (rezzed)
                        {
                            bool hasPrim = false;
                            foreach (EntityBase ent in Entities.Values)
                            {
                                if (ent is SceneObjectGroup)
                                {
                                    hasPrim = ((SceneObjectGroup) ent).HasChildPrim(localID);
                                    if (hasPrim != false)
                                    {
                                        bool added =
                                            ((SceneObjectGroup) ent).AddInventoryItem(remoteClient, localID, item,
                                                                                      copyID);
                                        ((SceneObjectGroup) ent).GetProperites(remoteClient);
                                    }
                                }
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
        public void DeRezObject(Packet packet, IClientAPI remoteClient)
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
                            CachedUserInfo userInfo = commsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
                            if (userInfo != null)
                            {
                                AssetBase asset = new AssetBase();
                                asset.Name = ((SceneObjectGroup) selectedEnt).GetPartName(selectedEnt.LocalId);
                                asset.Description =
                                    ((SceneObjectGroup) selectedEnt).GetPartDescription(selectedEnt.LocalId);
                                asset.InvType = 6;
                                asset.Type = 6;
                                asset.FullID = LLUUID.Random();
                                asset.Data = Helpers.StringToField(sceneObjectXml);
                                commsManager.AssetCache.AddAsset(asset);


                                InventoryItemBase item = new InventoryItemBase();
                                item.avatarID = remoteClient.AgentId;
                                item.creatorsID = remoteClient.AgentId;
                                item.inventoryID = LLUUID.Random();
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

                            SceneObjectPart rootPart =
                                ((SceneObjectGroup) selectedEnt).GetChildPart(((SceneObjectGroup) selectedEnt).UUID);
                            if (rootPart.PhysActor != null)
                            {
                                phyScene.RemovePrim(rootPart.PhysActor);
                                rootPart.PhysActor = null;
                            }

                            storageManager.DataStore.RemoveObject(((SceneObjectGroup) selectedEnt).UUID,
                                                                  m_regInfo.SimUUID);
                            ((SceneObjectGroup) selectedEnt).DeleteGroup();

                            lock (Entities)
                            {
                                Entities.Remove(((SceneObjectGroup) selectedEnt).UUID);
                            }
                            ((SceneObjectGroup) selectedEnt).DeleteParts();
                        }
                    }
                }
            }
        }

        public void RezObject(IClientAPI remoteClient, LLUUID itemID, LLVector3 pos)
        {
            CachedUserInfo userInfo = commsManager.UserProfileCache.GetUserDetails(remoteClient.AgentId);
            if (userInfo != null)
            {
                if (userInfo.RootFolder != null)
                {
                    InventoryItemBase item = userInfo.RootFolder.HasItem(itemID);
                    if (item != null)
                    {
                        AssetBase rezAsset = commsManager.AssetCache.GetAsset(item.assetID, false);
                        if (rezAsset != null)
                        {
                            AddRezObject(Util.FieldToString(rezAsset.Data), pos);
                            userInfo.DeleteItem(remoteClient.AgentId, item);
                            remoteClient.SendRemoveInventoryItem(itemID);
                        }
                        else
                        {
                            //lets try once more incase the asset cache is being slow getting the asset from server
                            rezAsset = commsManager.AssetCache.GetAsset(item.assetID, false);
                            if (rezAsset != null)
                            {
                                AddRezObject(Util.FieldToString(rezAsset.Data), pos);
                                userInfo.DeleteItem(remoteClient.AgentId, item);
                                remoteClient.SendRemoveInventoryItem(itemID);
                            }
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
            if ((rootPart.ObjectFlags & (uint) LLObject.ObjectFlags.Phantom) == 0)
            {
                PrimitiveBaseShape pbs = rootPart.Shape;
                rootPart.PhysActor = phyScene.AddPrimShape(
                    rootPart.Name,
                    pbs,
                    new PhysicsVector(rootPart.AbsolutePosition.X, rootPart.AbsolutePosition.Y,
                                      rootPart.AbsolutePosition.Z),
                    new PhysicsVector(rootPart.Scale.X, rootPart.Scale.Y, rootPart.Scale.Z),
                    new Quaternion(rootPart.RotationOffset.W, rootPart.RotationOffset.X,
                                   rootPart.RotationOffset.Y, rootPart.RotationOffset.Z));
            }
        }
    }
}
