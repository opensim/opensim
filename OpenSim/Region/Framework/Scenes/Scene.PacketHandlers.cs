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
using System.Threading;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;

namespace OpenSim.Region.Framework.Scenes
{
    public partial class Scene
    {
        protected void SimChat(byte[] message, ChatTypeEnum type, int channel, Vector3 fromPos, string fromName,
                               UUID fromID, bool fromAgent, bool broadcast)
        {
            OSChatMessage args = new OSChatMessage();

            args.Message = Utils.BytesToString(message);
            args.Channel = channel;
            args.Type = type;
            args.Position = fromPos;
            args.SenderUUID = fromID;
            args.Scene = this;

            if (fromAgent)
            {
                ScenePresence user = GetScenePresence(fromID);
                if (user != null)
                    args.Sender = user.ControllingClient;
            }
            else
            {
                SceneObjectPart obj = GetSceneObjectPart(fromID);
                args.SenderObject = obj;
            }

            args.From = fromName;
            //args.

            if (broadcast)
                EventManager.TriggerOnChatBroadcast(this, args);
            else
                EventManager.TriggerOnChatFromWorld(this, args);
        }
        
        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="fromPos"></param>
        /// <param name="fromName"></param>
        /// <param name="fromAgentID"></param>
        public void SimChat(byte[] message, ChatTypeEnum type, int channel, Vector3 fromPos, string fromName,
                            UUID fromID, bool fromAgent)
        {
            SimChat(message, type, channel, fromPos, fromName, fromID, fromAgent, false);
        }

        public void SimChat(string message, ChatTypeEnum type, Vector3 fromPos, string fromName, UUID fromID, bool fromAgent)
        {
            SimChat(Utils.StringToBytes(message), type, 0, fromPos, fromName, fromID, fromAgent);
        }

        public void SimChat(string message, string fromName)
        {
            SimChat(message, ChatTypeEnum.Broadcast, Vector3.Zero, fromName, UUID.Zero, false);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="fromPos"></param>
        /// <param name="fromName"></param>
        /// <param name="fromAgentID"></param>
        public void SimChatBroadcast(byte[] message, ChatTypeEnum type, int channel, Vector3 fromPos, string fromName,
                                     UUID fromID, bool fromAgent)
        {
            SimChat(message, type, channel, fromPos, fromName, fromID, fromAgent, true);
        }

        /// <summary>
        /// Invoked when the client requests a prim.
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="remoteClient"></param>
        public void RequestPrim(uint primLocalID, IClientAPI remoteClient)
        {
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)ent).LocalId == primLocalID)
                    {
                        ((SceneObjectGroup)ent).SendFullUpdateToClient(remoteClient);
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Invoked when the client selects a prim.
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="remoteClient"></param>
        public void SelectPrim(uint primLocalID, IClientAPI remoteClient)
        {
            List<EntityBase> EntityList = GetEntities();

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    if (((SceneObjectGroup) ent).LocalId == primLocalID)
                    {
                        ((SceneObjectGroup) ent).GetProperties(remoteClient);
                        ((SceneObjectGroup) ent).IsSelected = true;
                        // A prim is only tainted if it's allowed to be edited by the person clicking it.
                        if (Permissions.CanEditObject(((SceneObjectGroup)ent).UUID, remoteClient.AgentId) 
                            || Permissions.CanMoveObject(((SceneObjectGroup)ent).UUID, remoteClient.AgentId))
                        {
                            EventManager.TriggerParcelPrimCountTainted();
                        }
                        break;
                    }
                   else 
                   {
                       // We also need to check the children of this prim as they
                       // can be selected as well and send property information
                       bool foundPrim = false;
                       foreach (KeyValuePair<UUID, SceneObjectPart> child in ((SceneObjectGroup) ent).Children)
                       {
                           if (child.Value.LocalId == primLocalID) 
                           {
                               child.Value.GetProperties(remoteClient);
                               foundPrim = true;
                               break;
                           }
                       }
                       if (foundPrim) break;
                   }
                }
            }
        }

        /// <summary>
        /// Handle the deselection of a prim from the client.
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="remoteClient"></param>
        public void DeselectPrim(uint primLocalID, IClientAPI remoteClient)
        {
            SceneObjectPart part = GetSceneObjectPart(primLocalID);
            if (part == null)
                return;
            
            // The prim is in the process of being deleted.
            if (null == part.ParentGroup.RootPart)
                return;
            
            // A deselect packet contains all the local prims being deselected.  However, since selection is still
            // group based we only want the root prim to trigger a full update - otherwise on objects with many prims
            // we end up sending many duplicate ObjectUpdates
            if (part.ParentGroup.RootPart.LocalId != part.LocalId)
                return;

            bool isAttachment = false;
            
            // This is wrong, wrong, wrong. Selection should not be
            // handled by group, but by prim. Legacy cruft.
            // TODO: Make selection flagging per prim!
            //
            part.ParentGroup.IsSelected = false;
            
            if (part.ParentGroup.IsAttachment)
                isAttachment = true;
            else
                part.ParentGroup.ScheduleGroupForFullUpdate();

            // If it's not an attachment, and we are allowed to move it,
            // then we might have done so. If we moved across a parcel
            // boundary, we will need to recount prims on the parcels.
            // For attachments, that makes no sense.
            //
            if (!isAttachment)
            {
                if (Permissions.CanEditObject(
                        part.UUID, remoteClient.AgentId) 
                        || Permissions.CanMoveObject(
                        part.UUID, remoteClient.AgentId))
                    EventManager.TriggerParcelPrimCountTainted();
            }
        }

        public virtual void ProcessMoneyTransferRequest(UUID source, UUID destination, int amount, 
                                                        int transactiontype, string description)
        {
            EventManager.MoneyTransferArgs args = new EventManager.MoneyTransferArgs(source, destination, amount, 
                                                                                     transactiontype, description);

            EventManager.TriggerMoneyTransfer(this, args);
        }

        public virtual void ProcessParcelBuy(UUID agentId, UUID groupId, bool final, bool groupOwned,
                bool removeContribution, int parcelLocalID, int parcelArea, int parcelPrice, bool authenticated)
        {
            EventManager.LandBuyArgs args = new EventManager.LandBuyArgs(agentId, groupId, final, groupOwned, 
                                                                         removeContribution, parcelLocalID, parcelArea, 
                                                                         parcelPrice, authenticated);

            // First, allow all validators a stab at it
            m_eventManager.TriggerValidateLandBuy(this, args);

            // Then, check validation and transfer
            m_eventManager.TriggerLandBuy(this, args);
        }

        public virtual void ProcessObjectGrab(uint localID, Vector3 offsetPos, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs)
        {
            List<EntityBase> EntityList = GetEntities();

            SurfaceTouchEventArgs surfaceArg = null;
            if (surfaceArgs != null && surfaceArgs.Count > 0)
                surfaceArg = surfaceArgs[0];

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup obj = ent as SceneObjectGroup;
                    if (obj != null)
                    {
                        // Is this prim part of the group
                        if (obj.HasChildPrim(localID))
                        {
                            // Currently only grab/touch for the single prim
                            // the client handles rez correctly
                            obj.ObjectGrabHandler(localID, offsetPos, remoteClient);

                            SceneObjectPart part = obj.GetChildPart(localID);

                            // If the touched prim handles touches, deliver it
                            // If not, deliver to root prim
                            if ((part.ScriptEvents & scriptEvents.touch_start) != 0) 
                                EventManager.TriggerObjectGrab(part.LocalId, 0, part.OffsetPosition, remoteClient, surfaceArg);
                            // Deliver to the root prim if the touched prim doesn't handle touches
                            // or if we're meant to pass on touches anyway. Don't send to root prim
                            // if prim touched is the root prim as we just did it
                            if (((part.ScriptEvents & scriptEvents.touch_start) == 0) ||
                                (part.PassTouches && (part.LocalId != obj.RootPart.LocalId))) 
                            {
                                EventManager.TriggerObjectGrab(obj.RootPart.LocalId, part.LocalId, part.OffsetPosition, remoteClient, surfaceArg);
                            }

                            return;
                        }
                    }
                }
            }
        }

        public virtual void ProcessObjectDeGrab(uint localID, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs)
        {
            List<EntityBase> EntityList = GetEntities();

            SurfaceTouchEventArgs surfaceArg = null;
            if (surfaceArgs != null && surfaceArgs.Count > 0)
                surfaceArg = surfaceArgs[0];

            foreach (EntityBase ent in EntityList)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectGroup obj = ent as SceneObjectGroup;

                    // Is this prim part of the group
                    if (obj.HasChildPrim(localID))
                    {
                        SceneObjectPart part=obj.GetChildPart(localID);
                        if (part != null)
                        {
                            // If the touched prim handles touches, deliver it
                            // If not, deliver to root prim
                            if ((part.ScriptEvents & scriptEvents.touch_end) != 0)
                                EventManager.TriggerObjectDeGrab(part.LocalId, 0, remoteClient, surfaceArg);
                            else
                                EventManager.TriggerObjectDeGrab(obj.RootPart.LocalId, part.LocalId, remoteClient, surfaceArg);

                            return;
                        }
                        return;
                    }
                }
            }
        }

        public void ProcessAvatarPickerRequest(IClientAPI client, UUID avatarID, UUID RequestID, string query)
        {
            //EventManager.TriggerAvatarPickerRequest();

            List<AvatarPickerAvatar> AvatarResponses = new List<AvatarPickerAvatar>();
            AvatarResponses = m_sceneGridService.GenerateAgentPickerRequestResponse(RequestID, query);

            AvatarPickerReplyPacket replyPacket = (AvatarPickerReplyPacket) PacketPool.Instance.GetPacket(PacketType.AvatarPickerReply);
            // TODO: don't create new blocks if recycling an old packet

            AvatarPickerReplyPacket.DataBlock[] searchData =
                new AvatarPickerReplyPacket.DataBlock[AvatarResponses.Count];
            AvatarPickerReplyPacket.AgentDataBlock agentData = new AvatarPickerReplyPacket.AgentDataBlock();

            agentData.AgentID = avatarID;
            agentData.QueryID = RequestID;
            replyPacket.AgentData = agentData;
            //byte[] bytes = new byte[AvatarResponses.Count*32];

            int i = 0;
            foreach (AvatarPickerAvatar item in AvatarResponses)
            {
                UUID translatedIDtem = item.AvatarID;
                searchData[i] = new AvatarPickerReplyPacket.DataBlock();
                searchData[i].AvatarID = translatedIDtem;
                searchData[i].FirstName = Utils.StringToBytes((string) item.firstName);
                searchData[i].LastName = Utils.StringToBytes((string) item.lastName);
                i++;
            }
            if (AvatarResponses.Count == 0)
            {
                searchData = new AvatarPickerReplyPacket.DataBlock[0];
            }
            replyPacket.Data = searchData;

            AvatarPickerReplyAgentDataArgs agent_data = new AvatarPickerReplyAgentDataArgs();
            agent_data.AgentID = replyPacket.AgentData.AgentID;
            agent_data.QueryID = replyPacket.AgentData.QueryID;

            List<AvatarPickerReplyDataArgs> data_args = new List<AvatarPickerReplyDataArgs>();
            for (i = 0; i < replyPacket.Data.Length; i++)
            {
                AvatarPickerReplyDataArgs data_arg = new AvatarPickerReplyDataArgs();
                data_arg.AvatarID = replyPacket.Data[i].AvatarID;
                data_arg.FirstName = replyPacket.Data[i].FirstName;
                data_arg.LastName = replyPacket.Data[i].LastName;
                data_args.Add(data_arg);
            }
            client.SendAvatarPickerReply(agent_data, data_args);
        }

        public void ProcessScriptReset(IClientAPI remoteClient, UUID objectID,
                UUID itemID)
        {
            SceneObjectPart part=GetSceneObjectPart(objectID);
            if (part == null)
                return;

            if (Permissions.CanResetScript(objectID, itemID, remoteClient.AgentId))
            {
                EventManager.TriggerScriptReset(part.LocalId, itemID);
            }
        }
        
        /// <summary>
        /// Handle a fetch inventory request from the client
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="ownerID"></param>
        public void HandleFetchInventory(IClientAPI remoteClient, UUID itemID, UUID ownerID)
        {
            if (ownerID == CommsManager.UserProfileCacheService.LibraryRoot.Owner)
            {
                //m_log.Debug("request info for library item");
                return;
            }
            
            InventoryItemBase item = new InventoryItemBase(itemID, remoteClient.AgentId);
            item = InventoryService.GetItem(item);
            
            if (item != null)
            {
                remoteClient.SendInventoryItemDetails(ownerID, item);
            }
            // else shouldn't we send an alert message?
        }
        
        /// <summary>
        /// Tell the client about the various child items and folders contained in the requested folder.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="ownerID"></param>
        /// <param name="fetchFolders"></param>
        /// <param name="fetchItems"></param>
        /// <param name="sortOrder"></param>
        public void HandleFetchInventoryDescendents(IClientAPI remoteClient, UUID folderID, UUID ownerID,
                                                    bool fetchFolders, bool fetchItems, int sortOrder)
        {
            // FIXME MAYBE: We're not handling sortOrder!

            // TODO: This code for looking in the folder for the library should be folded back into the
            // CachedUserInfo so that this class doesn't have to know the details (and so that multiple libraries, etc.
            // can be handled transparently).
            InventoryFolderImpl fold = null;
            if ((fold = CommsManager.UserProfileCacheService.LibraryRoot.FindFolder(folderID)) != null)
            {
                remoteClient.SendInventoryFolderDetails(
                    fold.Owner, folderID, fold.RequestListOfItems(),
                    fold.RequestListOfFolders(), fetchFolders, fetchItems);
                return;
            }

            // We're going to send the reply async, because there may be
            // an enormous quantity of packets -- basically the entire inventory!
            // We don't want to block the client thread while all that is happening.
            SendInventoryDelegate d = SendInventoryAsync;
            d.BeginInvoke(remoteClient, folderID, ownerID, fetchFolders, fetchItems, sortOrder, SendInventoryComplete, d);
        }

        delegate void SendInventoryDelegate(IClientAPI remoteClient, UUID folderID, UUID ownerID, bool fetchFolders, bool fetchItems, int sortOrder);

        void SendInventoryAsync(IClientAPI remoteClient, UUID folderID, UUID ownerID, bool fetchFolders, bool fetchItems, int sortOrder)
        {
            SendInventoryUpdate(remoteClient, new InventoryFolderBase(folderID), fetchFolders, fetchItems);
        }

        void SendInventoryComplete(IAsyncResult iar)
        {
            SendInventoryDelegate d = (SendInventoryDelegate)iar.AsyncState;
            d.EndInvoke(iar);
        }

        /// <summary>
        /// Handle the caps inventory descendents fetch.
        ///
        /// Since the folder structure is sent to the client on login, I believe we only need to handle items.
        /// Diva comment 8/13/2009: what if someone gave us a folder in the meantime??
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="folderID"></param>
        /// <param name="ownerID"></param>
        /// <param name="fetchFolders"></param>
        /// <param name="fetchItems"></param>
        /// <param name="sortOrder"></param>
        /// <returns>null if the inventory look up failed</returns>
        public InventoryCollection HandleFetchInventoryDescendentsCAPS(UUID agentID, UUID folderID, UUID ownerID,
                                                   bool fetchFolders, bool fetchItems, int sortOrder, out int version)
        {
//            m_log.DebugFormat(
//                "[INVENTORY CACHE]: Fetching folders ({0}), items ({1}) from {2} for agent {3}",
//                fetchFolders, fetchItems, folderID, agentID);

            // FIXME MAYBE: We're not handling sortOrder!

            // TODO: This code for looking in the folder for the library should be folded back into the
            // CachedUserInfo so that this class doesn't have to know the details (and so that multiple libraries, etc.
            // can be handled transparently).
            InventoryFolderImpl fold;
            if ((fold = CommsManager.UserProfileCacheService.LibraryRoot.FindFolder(folderID)) != null)
            {
                version = 0;
                InventoryCollection ret = new InventoryCollection();
                ret.Folders = new List<InventoryFolderBase>();
                ret.Items = fold.RequestListOfItems();

                return ret;
            }

            InventoryCollection contents = InventoryService.GetFolderContent(agentID, folderID);

            if (folderID != UUID.Zero)
            {
                InventoryFolderBase containingFolder = new InventoryFolderBase();
                containingFolder.ID = folderID;
                containingFolder.Owner = agentID;
                containingFolder = InventoryService.GetFolder(containingFolder);
                version = containingFolder.Version;
            }
            else
            {
                // Lost itemsm don't really need a version
                version = 1;
            }

            return contents;

        }
        
        /// <summary>
        /// Handle an inventory folder creation request from the client.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="folderType"></param>
        /// <param name="folderName"></param>
        /// <param name="parentID"></param>
        public void HandleCreateInventoryFolder(IClientAPI remoteClient, UUID folderID, ushort folderType,
                                                string folderName, UUID parentID)
        {
            InventoryFolderBase folder = new InventoryFolderBase(folderID, folderName, remoteClient.AgentId, (short)folderType, parentID, 1);
            if (!InventoryService.AddFolder(folder))
            {
                m_log.WarnFormat(
                     "[AGENT INVENTORY]: Failed to move create folder for user {0} {1}",
                     remoteClient.Name, remoteClient.AgentId);
            }
        }

        /// <summary>
        /// Handle a client request to update the inventory folder
        /// </summary>
        ///
        /// FIXME: We call add new inventory folder because in the data layer, we happen to use an SQL REPLACE
        /// so this will work to rename an existing folder.  Needless to say, to rely on this is very confusing,
        /// and needs to be changed.
        ///
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
        /// <param name="type"></param>
        /// <param name="name"></param>
        /// <param name="parentID"></param>
        public void HandleUpdateInventoryFolder(IClientAPI remoteClient, UUID folderID, ushort type, string name,
                                                UUID parentID)
        {
//            m_log.DebugFormat(
//                "[AGENT INVENTORY]: Updating inventory folder {0} {1} for {2} {3}", folderID, name, remoteClient.Name, remoteClient.AgentId);

            InventoryFolderBase folder = new InventoryFolderBase(folderID, remoteClient.AgentId);
            folder = InventoryService.GetFolder(folder);
            if (folder != null)
            {
                folder.Name = name;
                folder.Type = (short)type;
                folder.ParentID = parentID;
                if (!InventoryService.UpdateFolder(folder))
                {
                    m_log.ErrorFormat(
                         "[AGENT INVENTORY]: Failed to update folder for user {0} {1}",
                         remoteClient.Name, remoteClient.AgentId);
                }
            }
        }
        
        public void HandleMoveInventoryFolder(IClientAPI remoteClient, UUID folderID, UUID parentID)
        {
            InventoryFolderBase folder = new InventoryFolderBase(folderID, remoteClient.AgentId);
            folder = InventoryService.GetFolder(folder);
            if (folder != null)
            {
                folder.ParentID = parentID;
                if (!InventoryService.MoveFolder(folder))
                    m_log.WarnFormat("[AGENT INVENTORY]: could not move folder {0}", folderID);
                else
                    m_log.DebugFormat("[AGENT INVENTORY]: folder {0} moved to parent {1}", folderID, parentID);
            }
            else
            {
                m_log.WarnFormat("[AGENT INVENTORY]: request to move folder {0} but folder not found", folderID);
            }
        }

        /// <summary>
        /// This should delete all the items and folders in the given directory.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>

        delegate void PurgeFolderDelegate(UUID userID, UUID folder);

        public void HandlePurgeInventoryDescendents(IClientAPI remoteClient, UUID folderID)
        {
            PurgeFolderDelegate d = PurgeFolderAsync;
            try
            {
                d.BeginInvoke(remoteClient.AgentId, folderID, PurgeFolderCompleted, d);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[AGENT INVENTORY]: Exception on purge folder for user {0}: {1}", remoteClient.AgentId, e.Message);
            }
        }


        private void PurgeFolderAsync(UUID userID, UUID folderID)
        {
            InventoryFolderBase folder = new InventoryFolderBase(folderID, userID);

            if (InventoryService.PurgeFolder(folder))
                m_log.DebugFormat("[AGENT INVENTORY]: folder {0} purged successfully", folderID);
            else
                m_log.WarnFormat("[AGENT INVENTORY]: could not purge folder {0}", folderID);
        }

        private void PurgeFolderCompleted(IAsyncResult iar)
        {
            PurgeFolderDelegate d = (PurgeFolderDelegate)iar.AsyncState;
            d.EndInvoke(iar);
        }
    }
}
