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
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Mono.Addins;

namespace OpenSim.Region.CoreModules.Avatar.Friends
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "XCallingCard")]
    public class CallingCardModule : ISharedRegionModule, ICallingCardModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected List<Scene> m_Scenes = new List<Scene>();
        protected bool m_Enabled = true;

        public void Initialise(IConfigSource source)
        {
            IConfig ccConfig = source.Configs["XCallingCard"];
            if (ccConfig != null)
                m_Enabled = ccConfig.GetBoolean("Enabled", true);
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_Scenes.Add(scene);

            scene.RegisterModuleInterface<ICallingCardModule>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_Scenes.Remove(scene);

            scene.EventManager.OnNewClient -= OnNewClient;
            scene.EventManager.OnIncomingInstantMessage +=
                    OnIncomingInstantMessage;

            scene.UnregisterModuleInterface<ICallingCardModule>(this);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;
            scene.EventManager.OnNewClient += OnNewClient;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "XCallingCardModule"; }
        }

        private void OnNewClient(IClientAPI client)
        {
            client.OnOfferCallingCard += OnOfferCallingCard;
            client.OnAcceptCallingCard += OnAcceptCallingCard;
            client.OnDeclineCallingCard += OnDeclineCallingCard;
        }

        private void OnOfferCallingCard(IClientAPI client, UUID destID, UUID transactionID)
        {
            ScenePresence sp = GetClientPresence(client.AgentId);
            if (sp != null)
            {
                // If we're in god mode, we reverse the meaning. Offer
                // calling card becomes "Take a calling card" for that
                // person, no matter if they agree or not.
                if (sp.GodLevel >= 200)
                {
                    CreateCallingCard(client.AgentId, destID, UUID.Zero, true);
                    return;
                }
            }

            IClientAPI dest = FindClientObject(destID);
            if (dest != null)
            {
                DoCallingCardOffer(dest, client.AgentId);
                return;
            }

            IMessageTransferModule transferModule =
                    m_Scenes[0].RequestModuleInterface<IMessageTransferModule>();

            if (transferModule != null)
            {
                transferModule.SendInstantMessage(new GridInstantMessage(
                        client.Scene, client.AgentId,
                        client.FirstName+" "+client.LastName,
                        destID, (byte)211, false,
                        String.Empty,
                        transactionID, false, new Vector3(), new byte[0], true),
                        delegate(bool success) {} );
            }
        }

        private void DoCallingCardOffer(IClientAPI dest, UUID from)
        {
            UUID itemID = CreateCallingCard(dest.AgentId, from, UUID.Zero, false);

            dest.SendOfferCallingCard(from, itemID);
        }

        // Create a calling card in the user's inventory. This is called
        // from direct calling card creation, when the offer is forwarded,
        // and from the friends module when the friend is confirmed.
        // Because of the latter, it will send a bulk inventory update
        // if the receiving user is in the same simulator.
        public UUID CreateCallingCard(UUID userID, UUID creatorID, UUID folderID)
        {
            return CreateCallingCard(userID, creatorID, folderID, false);
        }

        private UUID CreateCallingCard(UUID userID, UUID creatorID, UUID folderID, bool isGod)
        {
            IUserAccountService userv = m_Scenes[0].UserAccountService;
            if (userv == null)
                return UUID.Zero;

            UserAccount info = userv.GetUserAccount(UUID.Zero, creatorID);
            if (info == null)
                return UUID.Zero;

            IInventoryService inv = m_Scenes[0].InventoryService;
            if (inv == null)
                return UUID.Zero;

            if (folderID == UUID.Zero)
            {
                InventoryFolderBase folder = inv.GetFolderForType(userID,
                        AssetType.CallingCard);

                if (folder == null) // Nowhere to put it
                    return UUID.Zero;

                folderID = folder.ID;
            }

            m_log.DebugFormat("[XCALLINGCARD]: Creating calling card for {0} in inventory of {1}", info.Name, userID);

            InventoryItemBase item = new InventoryItemBase();
            item.AssetID = UUID.Zero;
            item.AssetType = (int)AssetType.CallingCard;
            item.BasePermissions = (uint)(PermissionMask.Copy | PermissionMask.Modify);
            if (isGod)
                item.BasePermissions = (uint)(PermissionMask.Copy | PermissionMask.Modify | PermissionMask.Transfer | PermissionMask.Move);

            item.EveryOnePermissions = (uint)PermissionMask.None;
            item.CurrentPermissions = item.BasePermissions;
            item.NextPermissions = (uint)(PermissionMask.Copy | PermissionMask.Modify);

            item.ID = UUID.Random();
            item.CreatorId = creatorID.ToString();
            item.Owner = userID;
            item.GroupID = UUID.Zero;
            item.GroupOwned = false;
            item.Folder = folderID;

            item.CreationDate = Util.UnixTimeSinceEpoch();
            item.InvType = (int)InventoryType.CallingCard;
            item.Flags = 0;

            item.Name = info.Name;
            item.Description = "";

            item.SalePrice = 10;
            item.SaleType = (byte)SaleType.Not;

            inv.AddItem(item);

            IClientAPI client = FindClientObject(userID);
            if (client != null)
                client.SendBulkUpdateInventory(item);

            return item.ID;
        }

        private void OnAcceptCallingCard(IClientAPI client, UUID transactionID, UUID folderID)
        {
        }

        private void OnDeclineCallingCard(IClientAPI client, UUID transactionID)
        {
            IInventoryService invService = m_Scenes[0].InventoryService;

            InventoryFolderBase trashFolder =
                    invService.GetFolderForType(client.AgentId, AssetType.TrashFolder);

            InventoryItemBase item = new InventoryItemBase(transactionID, client.AgentId);
            item = invService.GetItem(item);

            if (item != null && trashFolder != null)
            {
                item.Folder = trashFolder.ID;
                List<UUID> uuids = new List<UUID>();
                uuids.Add(item.ID);
                invService.DeleteItems(item.Owner, uuids);
                m_Scenes[0].AddInventoryItem(client, item);
            }
        }

        public IClientAPI FindClientObject(UUID agentID)
        {
            Scene scene = GetClientScene(agentID);
            if (scene == null)
                return null;

            ScenePresence presence = scene.GetScenePresence(agentID);
            if (presence == null)
                return null;

            return presence.ControllingClient;
        }

        private Scene GetClientScene(UUID agentId)
        {
            lock (m_Scenes)
            {
                foreach (Scene scene in m_Scenes)
                {
                    ScenePresence presence = scene.GetScenePresence(agentId);
                    if (presence != null)
                    {
                        if (!presence.IsChildAgent)
                            return scene;
                    }
                }
            }
            return null;
        }

        private ScenePresence GetClientPresence(UUID agentId)
        {
            lock (m_Scenes)
            {
                foreach (Scene scene in m_Scenes)
                {
                    ScenePresence presence = scene.GetScenePresence(agentId);
                    if (presence != null)
                    {
                        if (!presence.IsChildAgent)
                            return presence;
                    }
                }
            }
            return null;
        }

        private void OnIncomingInstantMessage(GridInstantMessage msg)
        {
            if (msg.dialog == (uint)211)
            {
                IClientAPI client = FindClientObject(new UUID(msg.toAgentID));
                if (client == null)
                    return;

                DoCallingCardOffer(client, new UUID(msg.fromAgentID));
            }
        }
    }
}
