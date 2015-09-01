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
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using PermissionMask = OpenSim.Framework.PermissionMask;

namespace OpenSim.Region.CoreModules.World.Objects.BuySell
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "BuySellModule")]
    public class BuySellModule : IBuySellModule, INonSharedRegionModule
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        protected Scene m_scene = null;
        protected IDialogModule m_dialogModule;
        
        public string Name { get { return "Object BuySell Module"; } }
        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source) {}
        
        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IBuySellModule>(this);
            m_scene.EventManager.OnNewClient += SubscribeToClientEvents;
        }
        
        public void RemoveRegion(Scene scene) 
        {
            m_scene.EventManager.OnNewClient -= SubscribeToClientEvents;
        }
        
        public void RegionLoaded(Scene scene) 
        {
            m_dialogModule = scene.RequestModuleInterface<IDialogModule>();
        }
        
        public void Close() 
        {
            RemoveRegion(m_scene);
        }
        
        public void SubscribeToClientEvents(IClientAPI client)
        {
            client.OnObjectSaleInfo += ObjectSaleInfo;
        }

        protected void ObjectSaleInfo(
            IClientAPI client, UUID agentID, UUID sessionID, uint localID, byte saleType, int salePrice)
        {
            SceneObjectPart part = m_scene.GetSceneObjectPart(localID);
            if (part == null)
                return;

            if (part.ParentGroup.IsDeleted)
                return;

            if (part.OwnerID != client.AgentId && (!m_scene.Permissions.IsGod(client.AgentId)))
                return;

            part = part.ParentGroup.RootPart;

            part.ObjectSaleType = saleType;
            part.SalePrice = salePrice;

            part.ParentGroup.HasGroupChanged = true;

            part.SendPropertiesToClient(client);
        }

        public bool BuyObject(IClientAPI remoteClient, UUID categoryID, uint localID, byte saleType, int salePrice)
        {
            SceneObjectPart part = m_scene.GetSceneObjectPart(localID);

            if (part == null)
                return false;

            SceneObjectGroup group = part.ParentGroup;

            switch (saleType)
            {
            case 1: // Sell as original (in-place sale)
                uint effectivePerms = group.GetEffectivePermissions();

                if ((effectivePerms & (uint)PermissionMask.Transfer) == 0)
                {
                    if (m_dialogModule != null)
                        m_dialogModule.SendAlertToUser(remoteClient, "This item doesn't appear to be for sale");
                    return false;
                }

                group.SetOwnerId(remoteClient.AgentId);
                group.SetRootPartOwner(part, remoteClient.AgentId, remoteClient.ActiveGroupId);

                if (m_scene.Permissions.PropagatePermissions())
                {
                    foreach (SceneObjectPart child in group.Parts)
                    {
                        child.Inventory.ChangeInventoryOwner(remoteClient.AgentId);
                        child.TriggerScriptChangedEvent(Changed.OWNER);
                        child.ApplyNextOwnerPermissions();
                    }
                }

                part.ObjectSaleType = 0;
                part.SalePrice = 10;
                part.ClickAction = Convert.ToByte(0);

                group.HasGroupChanged = true;
                part.SendPropertiesToClient(remoteClient);
                part.TriggerScriptChangedEvent(Changed.OWNER);
                group.ResumeScripts();
                part.ScheduleFullUpdate();

                break;

            case 2: // Sell a copy
                Vector3 inventoryStoredPosition = new Vector3(
                        Math.Min(group.AbsolutePosition.X, m_scene.RegionInfo.RegionSizeX - 6),
                        Math.Min(group.AbsolutePosition.Y, m_scene.RegionInfo.RegionSizeY - 6),
                        group.AbsolutePosition.Z);

                Vector3 originalPosition = group.AbsolutePosition;

                group.AbsolutePosition = inventoryStoredPosition;

                string sceneObjectXml = SceneObjectSerializer.ToOriginalXmlFormat(group);
                group.AbsolutePosition = originalPosition;

                uint perms = group.GetEffectivePermissions();

                if ((perms & (uint)PermissionMask.Transfer) == 0)
                {
                    if (m_dialogModule != null)
                        m_dialogModule.SendAlertToUser(remoteClient, "This item doesn't appear to be for sale");
                    return false;
                }

                if ((perms & (uint)PermissionMask.Copy) == 0)
                {
                    if (m_dialogModule != null)
                        m_dialogModule.SendAlertToUser(remoteClient, "This sale has been blocked by the permissions system");
                    return false;
                }

                AssetBase asset = m_scene.CreateAsset(
                    group.GetPartName(localID),
                    group.GetPartDescription(localID),
                    (sbyte)AssetType.Object,
                    Utils.StringToBytes(sceneObjectXml),
                    group.OwnerID);
                m_scene.AssetService.Store(asset);

                InventoryItemBase item = new InventoryItemBase();
                item.CreatorId = part.CreatorID.ToString();
                item.CreatorData = part.CreatorData;

                item.ID = UUID.Random();
                item.Owner = remoteClient.AgentId;
                item.AssetID = asset.FullID;
                item.Description = asset.Description;
                item.Name = asset.Name;
                item.AssetType = asset.Type;
                item.InvType = (int)InventoryType.Object;
                item.Folder = categoryID;

                PermissionsUtil.ApplyFoldedPermissions(perms, ref perms);

                item.BasePermissions = perms & part.NextOwnerMask;
                item.CurrentPermissions = perms & part.NextOwnerMask;
                item.NextPermissions = part.NextOwnerMask;
                item.EveryOnePermissions = part.EveryoneMask &
                                           part.NextOwnerMask;
                item.GroupPermissions = part.GroupMask &
                                           part.NextOwnerMask;
                item.Flags |= (uint)InventoryItemFlags.ObjectSlamPerm;
                item.CreationDate = Util.UnixTimeSinceEpoch();

                if (m_scene.AddInventoryItem(item))
                {
                    remoteClient.SendInventoryItemCreateUpdate(item, 0);
                }
                else
                {
                    if (m_dialogModule != null)
                        m_dialogModule.SendAlertToUser(remoteClient, "Cannot buy now. Your inventory is unavailable");
                    return false;
                }
                break;

            case 3: // Sell contents
                List<UUID> invList = part.Inventory.GetInventoryList();

                bool okToSell = true;

                foreach (UUID invID in invList)
                {
                    TaskInventoryItem item1 = part.Inventory.GetInventoryItem(invID);
                    if ((item1.CurrentPermissions &
                            (uint)PermissionMask.Transfer) == 0)
                    {
                        okToSell = false;
                        break;
                    }
                }

                if (!okToSell)
                {
                    if (m_dialogModule != null)
                        m_dialogModule.SendAlertToUser(
                            remoteClient, "This item's inventory doesn't appear to be for sale");
                    return false;
                }

                if (invList.Count > 0)
                    m_scene.MoveTaskInventoryItems(remoteClient.AgentId, part.Name, part, invList);
                break;
            }

            return true;
        }
    }
}
