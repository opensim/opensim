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
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.Framework.Scenes
{
    public partial class Scene
    {
        /// <summary>
        /// Send chat to listeners.
        /// </summary>
        /// <param name='message'></param>
        /// <param name='type'>/param>
        /// <param name='channel'></param>
        /// <param name='fromPos'></param>
        /// <param name='fromName'></param>
        /// <param name='fromID'></param>
        /// <param name='targetID'></param>
        /// <param name='fromAgent'></param>
        /// <param name='broadcast'></param>
        protected void SimChat(byte[] message, ChatTypeEnum type, int channel, Vector3 fromPos, string fromName,
                               UUID fromID, UUID targetID, bool fromAgent, bool broadcast)
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
            args.TargetUUID = targetID;

//            m_log.DebugFormat(
//                "[SCENE]: Sending message {0} on channel {1}, type {2} from {3}, broadcast {4}",
//                args.Message.Replace("\n", "\\n"), args.Channel, args.Type, fromName, broadcast);

            if (broadcast)
                EventManager.TriggerOnChatBroadcast(this, args);
            else
                EventManager.TriggerOnChatFromWorld(this, args);
        }

        protected void SimChat(byte[] message, ChatTypeEnum type, int channel, Vector3 fromPos, string fromName,
                               UUID fromID, bool fromAgent, bool broadcast)
        {
            SimChat(message, type, channel, fromPos, fromName, fromID, UUID.Zero, fromAgent, broadcast);
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
        ///
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="fromPos"></param>
        /// <param name="fromName"></param>
        /// <param name="fromAgentID"></param>
        /// <param name="targetID"></param>
        public void SimChatToAgent(UUID targetID, byte[] message, Vector3 fromPos, string fromName, UUID fromID, bool fromAgent)
        {
            SimChat(message, ChatTypeEnum.Say, 0, fromPos, fromName, fromID, targetID, fromAgent, false);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        /// <param name="type"></param>
        /// <param name="channel"></param>
        /// <param name="fromPos"></param>
        /// <param name="fromName"></param>
        /// <param name="fromAgentID"></param>
        /// <param name="targetID"></param>
        public void SimChatToAgent(UUID targetID, byte[] message, int channel, Vector3 fromPos, string fromName, UUID fromID, bool fromAgent)
        {
            SimChat(message, ChatTypeEnum.Region, channel, fromPos, fromName, fromID, targetID, fromAgent, false);
        }

        /// <summary>
        /// Invoked when the client requests a prim.
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="remoteClient"></param>
        public void RequestPrim(uint primLocalID, IClientAPI remoteClient)
        {
            SceneObjectGroup sog = GetGroupByPrim(primLocalID);

            if (sog != null)
                sog.SendFullUpdateToClient(remoteClient);
        }

        /// <summary>
        /// Invoked when the client selects a prim.
        /// </summary>
        /// <param name="primLocalID"></param>
        /// <param name="remoteClient"></param>
        public void SelectPrim(uint primLocalID, IClientAPI remoteClient)
        {
            SceneObjectPart part = GetSceneObjectPart(primLocalID);

            if (null == part)
                return;

            if (part.IsRoot)
            {
                SceneObjectGroup sog = part.ParentGroup;
                sog.SendPropertiesToClient(remoteClient);
                sog.IsSelected = true;

                // A prim is only tainted if it's allowed to be edited by the person clicking it.
                if (Permissions.CanEditObject(sog.UUID, remoteClient.AgentId)
                    || Permissions.CanMoveObject(sog.UUID, remoteClient.AgentId))
                {
                    EventManager.TriggerParcelPrimCountTainted();
                }
            }
            else
            {
                 part.SendPropertiesToClient(remoteClient);
            }
        }

        /// <summary>
        /// Handle the update of an object's user group.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="groupID"></param>
        /// <param name="objectLocalID"></param>
        /// <param name="Garbage"></param>
        private void HandleObjectGroupUpdate(
            IClientAPI remoteClient, UUID groupID, uint objectLocalID, UUID Garbage)
        {
            if (m_groupsModule == null)
                return;

            // XXX: Might be better to get rid of this special casing and have GetMembershipData return something
            // reasonable for a UUID.Zero group.
            if (groupID != UUID.Zero)
            {
                GroupMembershipData gmd = m_groupsModule.GetMembershipData(groupID, remoteClient.AgentId);
    
                if (gmd == null)
                {
//                    m_log.WarnFormat(
//                        "[GROUPS]: User {0} is not a member of group {1} so they can't update {2} to this group",
//                        remoteClient.Name, GroupID, objectLocalID);
    
                    return;
                }
            }

            SceneObjectGroup so = ((Scene)remoteClient.Scene).GetGroupByPrim(objectLocalID);
            if (so != null)
            {
                if (so.OwnerID == remoteClient.AgentId)
                {
                    so.SetGroup(groupID, remoteClient);
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
            
            // A deselect packet contains all the local prims being deselected.  However, since selection is still
            // group based we only want the root prim to trigger a full update - otherwise on objects with many prims
            // we end up sending many duplicate ObjectUpdates
            if (part.ParentGroup.RootPart.LocalId != part.LocalId)
                return;

            // This is wrong, wrong, wrong. Selection should not be
            // handled by group, but by prim. Legacy cruft.
            // TODO: Make selection flagging per prim!
            //
            part.ParentGroup.IsSelected = false;
            
            part.ParentGroup.ScheduleGroupForFullUpdate();

            // If it's not an attachment, and we are allowed to move it,
            // then we might have done so. If we moved across a parcel
            // boundary, we will need to recount prims on the parcels.
            // For attachments, that makes no sense.
            //
            if (!part.ParentGroup.IsAttachment)
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
            SceneObjectPart part = GetSceneObjectPart(localID);
            
            if (part == null)
                return;

            SceneObjectGroup obj = part.ParentGroup;

            SurfaceTouchEventArgs surfaceArg = null;
            if (surfaceArgs != null && surfaceArgs.Count > 0)
                surfaceArg = surfaceArgs[0];

            // Currently only grab/touch for the single prim
            // the client handles rez correctly
            obj.ObjectGrabHandler(localID, offsetPos, remoteClient);
    
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
        }

        public virtual void ProcessObjectGrabUpdate(
            UUID objectID, Vector3 offset, Vector3 pos, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs)
        {
            SceneObjectPart part = GetSceneObjectPart(objectID);
            if (part == null)
                return;

            SceneObjectGroup obj = part.ParentGroup;

            SurfaceTouchEventArgs surfaceArg = null;
            if (surfaceArgs != null && surfaceArgs.Count > 0)
                surfaceArg = surfaceArgs[0];

            // If the touched prim handles touches, deliver it
            // If not, deliver to root prim
            if ((part.ScriptEvents & scriptEvents.touch) != 0)
                EventManager.TriggerObjectGrabbing(part.LocalId, 0, part.OffsetPosition, remoteClient, surfaceArg);
            // Deliver to the root prim if the touched prim doesn't handle touches
            // or if we're meant to pass on touches anyway. Don't send to root prim
            // if prim touched is the root prim as we just did it
            if (((part.ScriptEvents & scriptEvents.touch) == 0) ||
                (part.PassTouches && (part.LocalId != obj.RootPart.LocalId)))
            {
                EventManager.TriggerObjectGrabbing(obj.RootPart.LocalId, part.LocalId, part.OffsetPosition, remoteClient, surfaceArg);
            }
        }

        public virtual void ProcessObjectDeGrab(uint localID, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs)
        {
            SceneObjectPart part = GetSceneObjectPart(localID);
            if (part == null)
                return;

            SceneObjectGroup obj = part.ParentGroup;

            SurfaceTouchEventArgs surfaceArg = null;
            if (surfaceArgs != null && surfaceArgs.Count > 0)
                surfaceArg = surfaceArgs[0];

            // If the touched prim handles touches, deliver it
            // If not, deliver to root prim
            if ((part.ScriptEvents & scriptEvents.touch_end) != 0)
                EventManager.TriggerObjectDeGrab(part.LocalId, 0, remoteClient, surfaceArg);
            else
                EventManager.TriggerObjectDeGrab(obj.RootPart.LocalId, part.LocalId, remoteClient, surfaceArg);
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

        void ProcessViewerEffect(IClientAPI remoteClient, List<ViewerEffectEventHandlerArg> args)
        {
            // TODO: don't create new blocks if recycling an old packet
            bool discardableEffects = true;
            ViewerEffectPacket.EffectBlock[] effectBlockArray = new ViewerEffectPacket.EffectBlock[args.Count];
            for (int i = 0; i < args.Count; i++)
            {
                ViewerEffectPacket.EffectBlock effect = new ViewerEffectPacket.EffectBlock();
                effect.AgentID = args[i].AgentID;
                effect.Color = args[i].Color;
                effect.Duration = args[i].Duration;
                effect.ID = args[i].ID;
                effect.Type = args[i].Type;
                effect.TypeData = args[i].TypeData;
                effectBlockArray[i] = effect;

                if ((EffectType)effect.Type != EffectType.LookAt && (EffectType)effect.Type != EffectType.Beam)
                    discardableEffects = false;

                //m_log.DebugFormat("[YYY]: VE {0} {1} {2}", effect.AgentID, effect.Duration, (EffectType)effect.Type);
            }

            ForEachScenePresence(sp =>
                {
                    if (sp.ControllingClient.AgentId != remoteClient.AgentId)
                    {
                        if (!discardableEffects ||
                            (discardableEffects && ShouldSendDiscardableEffect(remoteClient, sp)))
                        {
                            //m_log.DebugFormat("[YYY]: Sending to {0}", sp.UUID);
                            sp.ControllingClient.SendViewerEffect(effectBlockArray);
                        }
                        //else
                        //    m_log.DebugFormat("[YYY]: Not sending to {0}", sp.UUID);
                    }
                });
        }

        private bool ShouldSendDiscardableEffect(IClientAPI thisClient, ScenePresence other)
        {
            return Vector3.Distance(other.CameraPosition, thisClient.SceneAgent.AbsolutePosition) < 10;
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
//            m_log.DebugFormat(
//                "[USER INVENTORY]: HandleFetchInventoryDescendents() for {0}, folder={1}, fetchFolders={2}, fetchItems={3}, sortOrder={4}",
//                remoteClient.Name, folderID, fetchFolders, fetchItems, sortOrder);

            if (folderID == UUID.Zero)
                return;

            // FIXME MAYBE: We're not handling sortOrder!

            // TODO: This code for looking in the folder for the library should be folded somewhere else
            // so that this class doesn't have to know the details (and so that multiple libraries, etc.
            // can be handled transparently).
            InventoryFolderImpl fold = null;
            if (LibraryService != null && LibraryService.LibraryRootFolder != null)
            {
                if ((fold = LibraryService.LibraryRootFolder.FindFolder(folderID)) != null)
                {
                    remoteClient.SendInventoryFolderDetails(
                        fold.Owner, folderID, fold.RequestListOfItems(),
                        fold.RequestListOfFolders(), fold.Version, fetchFolders, fetchItems);
                    return;
                }
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
            try
            {
                SendInventoryUpdate(remoteClient, new InventoryFolderBase(folderID), fetchFolders, fetchItems);
            }
            catch (Exception e)
            {
                m_log.Error(
                    string.Format(
                        "[AGENT INVENTORY]: Error in SendInventoryAsync() for {0} with folder ID {1}.  Exception  ", e));
            }
        }

        void SendInventoryComplete(IAsyncResult iar)
        {
            SendInventoryDelegate d = (SendInventoryDelegate)iar.AsyncState;
            d.EndInvoke(iar);
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
                     "[AGENT INVENTORY]: Failed to create folder for user {0} {1}",
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

        delegate void PurgeFolderDelegate(UUID userID, UUID folder);

        /// <summary>
        /// This should delete all the items and folders in the given directory.
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="folderID"></param>
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
