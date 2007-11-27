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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;

namespace OpenSim.Region.ClientStack
{
    public partial class ClientView
    {
        private int m_moneyBalance;

        public int MoneyBalance
        {
            get { return m_moneyBalance; }
        }

        public bool AddMoney(int debit)
        {
            if (m_moneyBalance + debit >= 0)
            {
                m_moneyBalance += debit;
                SendMoneyBalance(LLUUID.Zero, true, Helpers.StringToField("Poof Poof!"), m_moneyBalance);
                return true;
            }
            else
            {
                return false;
            }
        }

        protected void ProcessInPacket(Packet Pack)
        {
            ack_pack(Pack);

            if (ProcessPacketMethod(Pack))
            {
                //there is a handler registered that handled this packet type 
                return;
            }
            else
            {
                Encoding _enc = Encoding.ASCII;

                switch (Pack.Type)
                {
                        #region  Scene/Avatar

                    case PacketType.AvatarPropertiesRequest:
                        AvatarPropertiesRequestPacket avatarProperties = (AvatarPropertiesRequestPacket) Pack;
                        if (OnRequestAvatarProperties != null)
                        {
                            OnRequestAvatarProperties(this, avatarProperties.AgentData.AvatarID);
                        }
                        break;
                    case PacketType.ChatFromViewer:
                        ChatFromViewerPacket inchatpack = (ChatFromViewerPacket) Pack;

                        string fromName = ""; //ClientAvatar.firstname + " " + ClientAvatar.lastname;
                        byte[] message = inchatpack.ChatData.Message;
                        byte type = inchatpack.ChatData.Type;
                        LLVector3 fromPos = new LLVector3(); // ClientAvatar.Pos;
                        LLUUID fromAgentID = AgentId;

                        int channel = inchatpack.ChatData.Channel;

                        if (OnChatFromViewer != null)
                        {
                            ChatFromViewerArgs args = new ChatFromViewerArgs();
                            args.Channel = channel;
                            args.From = fromName;
                            args.Message = Helpers.FieldToUTF8String(message);
                            args.Type = (ChatTypeEnum) type;
                            args.Position = fromPos;

                            args.Scene = Scene;
                            args.Sender = this;

                            OnChatFromViewer(this, args);
                        }
                        break;
                    case PacketType.ImprovedInstantMessage:
                        ImprovedInstantMessagePacket msgpack = (ImprovedInstantMessagePacket) Pack;
                        string IMfromName = Util.FieldToString(msgpack.MessageBlock.FromAgentName);
                        string IMmessage = Helpers.FieldToUTF8String(msgpack.MessageBlock.Message);
                        if (OnInstantMessage != null)
                        {
                            OnInstantMessage(msgpack.AgentData.AgentID, msgpack.AgentData.SessionID,
                                             msgpack.MessageBlock.ToAgentID, msgpack.MessageBlock.ID,
                                             msgpack.MessageBlock.Timestamp, IMfromName, IMmessage,
                                             msgpack.MessageBlock.Dialog);
                        }
                        break;
                    case PacketType.RezObject:
                        RezObjectPacket rezPacket = (RezObjectPacket) Pack;
                        if (OnRezObject != null)
                        {
                            OnRezObject(this, rezPacket.InventoryData.ItemID, rezPacket.RezData.RayEnd);
                        }
                        break;
                    case PacketType.DeRezObject:
                        if (OnDeRezObject != null)
                        {
                            OnDeRezObject(Pack, this);
                        }
                        break;
                    case PacketType.ModifyLand:
                        ModifyLandPacket modify = (ModifyLandPacket) Pack;
                        if (modify.ParcelData.Length > 0)
                        {
                            if (OnModifyTerrain != null)
                            {
                                OnModifyTerrain(modify.ModifyBlock.Height, modify.ModifyBlock.Seconds,
                                                modify.ModifyBlock.BrushSize,
                                                modify.ModifyBlock.Action, modify.ParcelData[0].North,
                                                modify.ParcelData[0].West, this);
                            }
                        }
                        break;
                    case PacketType.RegionHandshakeReply:
                        if (OnRegionHandShakeReply != null)
                        {
                            OnRegionHandShakeReply(this);
                        }
                        break;
                    case PacketType.AgentWearablesRequest:
                        if (OnRequestWearables != null)
                        {
                            OnRequestWearables( );
                        }
                        if (OnRequestAvatarsData != null)
                        {
                            OnRequestAvatarsData(this);
                        }
                        break;
                    case PacketType.AgentSetAppearance:
                        //OpenSim.Framework.Console.MainLog.Instance.Verbose("set appear", Pack.ToString());
                        AgentSetAppearancePacket appear = (AgentSetAppearancePacket) Pack;
                        if (OnSetAppearance != null)
                        {
                            OnSetAppearance(appear.ObjectData.TextureEntry, appear.VisualParam);
                        }
                        break;
                    case PacketType.SetAlwaysRun:
                        SetAlwaysRunPacket run = (SetAlwaysRunPacket)Pack;

                        if (OnSetAlwaysRun != null)
                            OnSetAlwaysRun(this,run.AgentData.AlwaysRun);

                        break;
                    case PacketType.CompleteAgentMovement:
                        if (OnCompleteMovementToRegion != null)
                        {
                            OnCompleteMovementToRegion();
                        }
                        break;
                    case PacketType.AgentUpdate:
                        if (OnAgentUpdate != null)
                        {
                            AgentUpdatePacket agenUpdate = (AgentUpdatePacket) Pack;

                            OnAgentUpdate(this, agenUpdate); //agenUpdate.AgentData.ControlFlags, agenUpdate.AgentData.BodyRotationa);
                        }
                        break;
                    case PacketType.AgentAnimation:
                        AgentAnimationPacket AgentAni = (AgentAnimationPacket) Pack;
                        for (int i = 0; i < AgentAni.AnimationList.Length; i++)
                        {
                            if (AgentAni.AnimationList[i].StartAnim)
                            {
                                if (OnStartAnim != null)
                                {
                                    OnStartAnim(this, AgentAni.AnimationList[i].AnimID, 1);
                                }
                            }
                        }
                        break;
                    case PacketType.AgentRequestSit:
                        if (OnAgentRequestSit != null)
                        {
                            AgentRequestSitPacket agentRequestSit = (AgentRequestSitPacket) Pack;
                            OnAgentRequestSit(this, agentRequestSit.AgentData.AgentID,
                                              agentRequestSit.TargetObject.TargetID, agentRequestSit.TargetObject.Offset);
                        }
                        break;
                    case PacketType.AgentSit:
                        if (OnAgentSit != null)
                        {
                            AgentSitPacket agentSit = (AgentSitPacket) Pack;
                            OnAgentSit(this, agentSit.AgentData.AgentID);
                        }
                        break;
                    case PacketType.AvatarPickerRequest:
                            AvatarPickerRequestPacket avRequestQuery = (AvatarPickerRequestPacket)Pack;
                            AvatarPickerRequestPacket.AgentDataBlock Requestdata = avRequestQuery.AgentData;
                            AvatarPickerRequestPacket.DataBlock querydata = avRequestQuery.Data;
                            //System.Console.WriteLine("Agent Sends:" + Helpers.FieldToUTF8String(querydata.Name));
                        if (OnAvatarPickerRequest != null)
                        {
                            OnAvatarPickerRequest(this, Requestdata.AgentID, Requestdata.QueryID, Helpers.FieldToUTF8String(querydata.Name));
                        }
                        break;
                        #endregion

                        #region Objects/m_sceneObjects

                    case PacketType.ObjectLink:
                        //OpenSim.Framework.Console.MainLog.Instance.Verbose( Pack.ToString());
                        ObjectLinkPacket link = (ObjectLinkPacket) Pack;
                        uint parentprimid = 0;
                        List<uint> childrenprims = new List<uint>();
                        if (link.ObjectData.Length > 1)
                        {
                            parentprimid = link.ObjectData[0].ObjectLocalID;

                            for (int i = 1; i < link.ObjectData.Length; i++)
                            {
                                childrenprims.Add(link.ObjectData[i].ObjectLocalID);
                            }
                        }
                        if (OnLinkObjects != null)
                        {
                            OnLinkObjects(parentprimid, childrenprims);
                        }
                        break;
                    case PacketType.ObjectDelink:
                        //OpenSim.Framework.Console.MainLog.Instance.Verbose( Pack.ToString());
                        ObjectDelinkPacket delink = (ObjectDelinkPacket) Pack;
                        
                        // It appears the prim at index 0 is not always the root prim (for
                        // instance, when one prim of a link set has been edited independently
                        // of the others).  Therefore, we'll pass all the ids onto the delink
                        // method for it to decide which is the root.
                        List<uint> prims = new List<uint>();
                        for (int i = 0; i < delink.ObjectData.Length; i++)
                        {
                            prims.Add(delink.ObjectData[i].ObjectLocalID);                        
                        }
                        
                        if (OnDelinkObjects != null)
                        {                        
                            OnDelinkObjects(prims);
                        }

                        break;
                    case PacketType.ObjectAdd:
                        if (OnAddPrim != null)
                        {
                            ObjectAddPacket addPacket = (ObjectAddPacket) Pack;
                            PrimitiveBaseShape shape = GetShapeFromAddPacket(addPacket);
                            OnAddPrim(AgentId, addPacket.ObjectData.RayEnd, addPacket.ObjectData.Rotation, shape);
                        }
                        break;
                    case PacketType.ObjectShape:
                        ObjectShapePacket shapePacket = (ObjectShapePacket) Pack;
                        for (int i = 0; i < shapePacket.ObjectData.Length; i++)
                        {
                            if (OnUpdatePrimShape != null)
                            {
                                OnUpdatePrimShape(shapePacket.ObjectData[i].ObjectLocalID, shapePacket.ObjectData[i]);
                            }
                        }
                        break;
                    case PacketType.ObjectExtraParams:
                        ObjectExtraParamsPacket extraPar = (ObjectExtraParamsPacket) Pack;
                        if (OnUpdateExtraParams != null)
                        {
                            OnUpdateExtraParams(extraPar.ObjectData[0].ObjectLocalID, extraPar.ObjectData[0].ParamType,
                                                extraPar.ObjectData[0].ParamInUse, extraPar.ObjectData[0].ParamData);
                        }
                        break;
                    case PacketType.ObjectDuplicate:
                        ObjectDuplicatePacket dupe = (ObjectDuplicatePacket) Pack;
                        ObjectDuplicatePacket.AgentDataBlock AgentandGroupData = dupe.AgentData;
                        for (int i = 0; i < dupe.ObjectData.Length; i++)
                        {
                            if (OnObjectDuplicate != null)
                            {
                                OnObjectDuplicate(dupe.ObjectData[i].ObjectLocalID, dupe.SharedData.Offset,
                                                  dupe.SharedData.DuplicateFlags, AgentandGroupData.AgentID, AgentandGroupData.GroupID);
                            }
                        }

                        break;

                    case PacketType.ObjectSelect:
                        ObjectSelectPacket incomingselect = (ObjectSelectPacket) Pack;
                        for (int i = 0; i < incomingselect.ObjectData.Length; i++)
                        {
                            if (OnObjectSelect != null)
                            {
                                OnObjectSelect(incomingselect.ObjectData[i].ObjectLocalID, this);
                            }
                        }
                        break;
                    case PacketType.ObjectDeselect:
                        ObjectDeselectPacket incomingdeselect = (ObjectDeselectPacket) Pack;
                        for (int i = 0; i < incomingdeselect.ObjectData.Length; i++)
                        {
                            if (OnObjectDeselect != null)
                            {
                                OnObjectDeselect(incomingdeselect.ObjectData[i].ObjectLocalID, this);
                            }
                        }
                        break;
                    case PacketType.ObjectFlagUpdate:
                        ObjectFlagUpdatePacket flags = (ObjectFlagUpdatePacket) Pack;
                        if (OnUpdatePrimFlags != null)
                        {
                            OnUpdatePrimFlags(flags.AgentData.ObjectLocalID, Pack, this);
                        }
                        break;
                    case PacketType.ObjectImage:
                        ObjectImagePacket imagePack = (ObjectImagePacket) Pack;
                        for (int i = 0; i < imagePack.ObjectData.Length; i++)
                        {
                            if (OnUpdatePrimTexture != null)
                            {
                                OnUpdatePrimTexture(imagePack.ObjectData[i].ObjectLocalID,
                                                    imagePack.ObjectData[i].TextureEntry, this);
                            }
                        }
                        break;
                    case PacketType.ObjectGrab:
                        ObjectGrabPacket grab = (ObjectGrabPacket) Pack;
                        if (OnGrabObject != null)
                        {
                            OnGrabObject(grab.ObjectData.LocalID, grab.ObjectData.GrabOffset, this);
                        }
                        break;
                    case PacketType.ObjectGrabUpdate:
                        ObjectGrabUpdatePacket grabUpdate = (ObjectGrabUpdatePacket) Pack;
                        if (OnGrabUpdate != null)
                        {
                            OnGrabUpdate(grabUpdate.ObjectData.ObjectID, grabUpdate.ObjectData.GrabOffsetInitial,
                                         grabUpdate.ObjectData.GrabPosition, this);
                        }
                        break;
                    case PacketType.ObjectDeGrab:
                        ObjectDeGrabPacket deGrab = (ObjectDeGrabPacket) Pack;
                        if (OnDeGrabObject != null)
                        {
                            OnDeGrabObject(deGrab.ObjectData.LocalID, this);
                        }
                        break;
                    case PacketType.ObjectDescription:
                        ObjectDescriptionPacket objDes = (ObjectDescriptionPacket) Pack;
                        for (int i = 0; i < objDes.ObjectData.Length; i++)
                        {
                            if (OnObjectDescription != null)
                            {
                                OnObjectDescription(objDes.ObjectData[i].LocalID,
                                                    enc.GetString(objDes.ObjectData[i].Description));
                            }
                        }
                        break;
                    case PacketType.ObjectName:
                        ObjectNamePacket objName = (ObjectNamePacket) Pack;
                        for (int i = 0; i < objName.ObjectData.Length; i++)
                        {
                            if (OnObjectName != null)
                            {
                                OnObjectName(objName.ObjectData[i].LocalID, enc.GetString(objName.ObjectData[i].Name));
                            }
                        }
                        break;
                    case PacketType.ObjectPermissions:
                        OpenSim.Framework.Console.MainLog.Instance.Verbose("CLIENT", "unhandled packet " + Pack.ToString());
                        break;

                    case PacketType.RequestObjectPropertiesFamily:
                        //This powers the little tooltip that appears when you move your mouse over an object
                        RequestObjectPropertiesFamilyPacket packToolTip = (RequestObjectPropertiesFamilyPacket)Pack;
                        

                        RequestObjectPropertiesFamilyPacket.ObjectDataBlock packObjBlock = packToolTip.ObjectData;

                        if (OnRequestObjectPropertiesFamily != null)
                        {
                            OnRequestObjectPropertiesFamily(this, this.m_agentId, packObjBlock.RequestFlags, packObjBlock.ObjectID);


                        }

                        break;

                        #endregion

                        #region Inventory/Asset/Other related packets

                    case PacketType.RequestImage:
                        RequestImagePacket imageRequest = (RequestImagePacket) Pack;
                        //Console.WriteLine("image request: " + Pack.ToString());
                        for (int i = 0; i < imageRequest.RequestImage.Length; i++)
                        {
                            // still working on the Texture download module so for now using old method
                            //  TextureRequestArgs args = new TextureRequestArgs();
                            //  args.RequestedAssetID = imageRequest.RequestImage[i].Image;
                            //  args.DiscardLevel = imageRequest.RequestImage[i].DiscardLevel;
                            //  args.PacketNumber = imageRequest.RequestImage[i].Packet;

                            //  if (OnRequestTexture != null)
                            //  {
                            //      OnRequestTexture(this, args);
                            //  }

                            m_assetCache.AddTextureRequest(this, imageRequest.RequestImage[i].Image,
                                                           imageRequest.RequestImage[i].Packet,
                                                           imageRequest.RequestImage[i].DiscardLevel);
                        }
                        break;
                    case PacketType.TransferRequest:
                        //Console.WriteLine("ClientView.ProcessPackets.cs:ProcessInPacket() - Got transfer request");
                        TransferRequestPacket transfer = (TransferRequestPacket) Pack;
                        m_assetCache.AddAssetRequest(this, transfer);
                        break;
                    case PacketType.AssetUploadRequest:
                        AssetUploadRequestPacket request = (AssetUploadRequestPacket) Pack;
                        // Console.WriteLine("upload request " + Pack.ToString());
                        // Console.WriteLine("upload request was for assetid: " + request.AssetBlock.TransactionID.Combine(this.SecureSessionID).ToStringHyphenated());
                        if (OnAssetUploadRequest != null)
                        {
                            OnAssetUploadRequest(this, request.AssetBlock.TransactionID.Combine(SecureSessionID),
                                                 request.AssetBlock.TransactionID, request.AssetBlock.Type,
                                                 request.AssetBlock.AssetData, request.AssetBlock.StoreLocal);
                        }
                        break;
                    case PacketType.RequestXfer:
                        RequestXferPacket xferReq = (RequestXferPacket) Pack;
                        if (OnRequestXfer != null)
                        {
                            OnRequestXfer(this, xferReq.XferID.ID, Util.FieldToString(xferReq.XferID.Filename));
                        }
                        break;
                    case PacketType.SendXferPacket:
                        SendXferPacketPacket xferRec = (SendXferPacketPacket) Pack;
                        if (OnXferReceive != null)
                        {
                            OnXferReceive(this, xferRec.XferID.ID, xferRec.XferID.Packet, xferRec.DataPacket.Data);
                        }
                        break;
                    case PacketType.ConfirmXferPacket:
                        ConfirmXferPacketPacket confirmXfer = (ConfirmXferPacketPacket) Pack;
                        if (OnConfirmXfer != null)
                        {
                            OnConfirmXfer(this, confirmXfer.XferID.ID, confirmXfer.XferID.Packet);
                        }
                        break;
                    case PacketType.CreateInventoryFolder:
                        if (OnCreateNewInventoryFolder != null)
                        {
                            CreateInventoryFolderPacket invFolder = (CreateInventoryFolderPacket) Pack;
                            OnCreateNewInventoryFolder(this, invFolder.FolderData.FolderID,
                                                       (ushort) invFolder.FolderData.Type,
                                                       Util.FieldToString(invFolder.FolderData.Name),
                                                       invFolder.FolderData.ParentID);
                        }
                        break;
                    case PacketType.CreateInventoryItem:
                        CreateInventoryItemPacket createItem = (CreateInventoryItemPacket) Pack;
                        if (OnCreateNewInventoryItem != null)
                        {
                            OnCreateNewInventoryItem(this, createItem.InventoryBlock.TransactionID,
                                                     createItem.InventoryBlock.FolderID,
                                                     createItem.InventoryBlock.CallbackID,
                                                     Util.FieldToString(createItem.InventoryBlock.Description),
                                                     Util.FieldToString(createItem.InventoryBlock.Name),
                                                     createItem.InventoryBlock.InvType,
                                                     createItem.InventoryBlock.Type,
                                                     createItem.InventoryBlock.WearableType,
                                                     createItem.InventoryBlock.NextOwnerMask);
                        }
                        break;
                    case PacketType.FetchInventory:
                        if (OnFetchInventory != null)
                        {
                            FetchInventoryPacket FetchInventory = (FetchInventoryPacket) Pack;
                            for (int i = 0; i < FetchInventory.InventoryData.Length; i++)
                            {
                                OnFetchInventory(this, FetchInventory.InventoryData[i].ItemID,
                                                 FetchInventory.InventoryData[i].OwnerID);
                            }
                        }
                        break;
                    case PacketType.FetchInventoryDescendents:
                        if (OnFetchInventoryDescendents != null)
                        {
                            FetchInventoryDescendentsPacket Fetch = (FetchInventoryDescendentsPacket) Pack;
                            OnFetchInventoryDescendents(this, Fetch.InventoryData.FolderID, Fetch.InventoryData.OwnerID,
                                                        Fetch.InventoryData.FetchFolders, Fetch.InventoryData.FetchItems,
                                                        Fetch.InventoryData.SortOrder);
                        }
                        break;
                    case PacketType.UpdateInventoryItem:
                        UpdateInventoryItemPacket update = (UpdateInventoryItemPacket) Pack;
                        if (OnUpdateInventoryItem != null)
                        {
                            for (int i = 0; i < update.InventoryData.Length; i++)
                            {
                                if (update.InventoryData[i].TransactionID != LLUUID.Zero)
                                {
                                    OnUpdateInventoryItem(this, update.InventoryData[i].TransactionID,
                                                          update.InventoryData[i].TransactionID.Combine(SecureSessionID),
                                                          update.InventoryData[i].ItemID);
                                }
                            }
                        }
                        //Console.WriteLine(Pack.ToString());
                        /*for (int i = 0; i < update.InventoryData.Length; i++)
                        {
                            if (update.InventoryData[i].TransactionID != LLUUID.Zero)
                            {
                                AssetBase asset = m_assetCache.GetAsset(update.InventoryData[i].TransactionID.Combine(this.SecureSessionID));
                                if (asset != null)
                                {
                                    // Console.WriteLine("updating inventory item, found asset" + asset.FullID.ToStringHyphenated() + " already in cache");
                                    m_inventoryCache.UpdateInventoryItemAsset(this, update.InventoryData[i].ItemID, asset);
                                }
                                else
                                {
                                    asset = this.UploadAssets.AddUploadToAssetCache(update.InventoryData[i].TransactionID);
                                    if (asset != null)
                                    {
                                        //Console.WriteLine("updating inventory item, adding asset" + asset.FullID.ToStringHyphenated() + " to cache");
                                        m_inventoryCache.UpdateInventoryItemAsset(this, update.InventoryData[i].ItemID, asset);
                                    }
                                    else
                                    {
                                        //Console.WriteLine("trying to update inventory item, but asset is null");
                                    }
                                }
                            }
                            else
                            {
                                m_inventoryCache.UpdateInventoryItemDetails(this, update.InventoryData[i].ItemID, update.InventoryData[i]); ;
                            }
                        }*/
                        break;
                    case PacketType.CopyInventoryItem:
                        CopyInventoryItemPacket copyitem = (CopyInventoryItemPacket) Pack;
                        if (OnCopyInventoryItem != null)
                        {
                            foreach (CopyInventoryItemPacket.InventoryDataBlock datablock in copyitem.InventoryData)
                            {
                                OnCopyInventoryItem(this, datablock.CallbackID, datablock.OldAgentID, datablock.OldItemID, datablock.NewFolderID, Util.FieldToString(datablock.NewName));
                            }
                        }
                        break;
                    case PacketType.RequestTaskInventory:
                        RequestTaskInventoryPacket requesttask = (RequestTaskInventoryPacket) Pack;
                        if (OnRequestTaskInventory != null)
                        {
                            OnRequestTaskInventory(this, requesttask.InventoryData.LocalID);
                        }
                        break;
                    case PacketType.UpdateTaskInventory:
                        //Console.WriteLine(Pack.ToString());
                        UpdateTaskInventoryPacket updatetask = (UpdateTaskInventoryPacket) Pack;
                        if (OnUpdateTaskInventory != null)
                        {
                            if (updatetask.UpdateData.Key == 0)
                            {
                                OnUpdateTaskInventory(this, updatetask.InventoryData.ItemID,
                                                      updatetask.InventoryData.FolderID, updatetask.UpdateData.LocalID);
                            }
                        }
                        break;
                    case PacketType.RemoveTaskInventory:
                        RemoveTaskInventoryPacket removeTask = (RemoveTaskInventoryPacket) Pack;
                        if (OnRemoveTaskItem != null)
                        {
                            OnRemoveTaskItem(this, removeTask.InventoryData.ItemID, removeTask.InventoryData.LocalID);
                        }
                        break;
                    case PacketType.MoveTaskInventory:
                        OpenSim.Framework.Console.MainLog.Instance.Verbose("CLIENT", "unhandled packet " + Pack.ToString());
                        break;
                    case PacketType.RezScript:
                        //Console.WriteLine(Pack.ToString());
                        RezScriptPacket rezScript = (RezScriptPacket) Pack;
                        if (OnRezScript != null)
                        {
                            OnRezScript(this, rezScript.InventoryBlock.ItemID, rezScript.UpdateBlock.ObjectLocalID);
                        }
                        break;
                    case PacketType.MapLayerRequest:
                        RequestMapLayer();
                        break;
                    case PacketType.MapBlockRequest:
                        MapBlockRequestPacket MapRequest = (MapBlockRequestPacket) Pack;
                        if (OnRequestMapBlocks != null)
                        {
                            OnRequestMapBlocks(this, MapRequest.PositionData.MinX, MapRequest.PositionData.MinY,
                                               MapRequest.PositionData.MaxX, MapRequest.PositionData.MaxY);
                        }
                        break;
                    case PacketType.MapNameRequest:
                        MapNameRequestPacket map = (MapNameRequestPacket) Pack;
                        string mapName = UTF8Encoding.UTF8.GetString(map.NameData.Name, 0,
                                                map.NameData.Name.Length - 1);
                        if (OnMapNameRequest != null)
                        {
                            OnMapNameRequest(this, mapName);
                        }
                        break;
                    case PacketType.TeleportLandmarkRequest:
                        TeleportLandmarkRequestPacket tpReq = (TeleportLandmarkRequestPacket) Pack;

                        TeleportStartPacket tpStart = new TeleportStartPacket();
                        tpStart.Info.TeleportFlags = 8; // tp via lm
                        OutPacket(tpStart, ThrottleOutPacketType.Task);

                        TeleportProgressPacket tpProgress = new TeleportProgressPacket();
                        tpProgress.Info.Message = (new ASCIIEncoding()).GetBytes("sending_landmark");
                        tpProgress.Info.TeleportFlags = 8;
                        tpProgress.AgentData.AgentID = tpReq.Info.AgentID;
                        OutPacket(tpProgress, ThrottleOutPacketType.Task);

                        // Fetch landmark
                        LLUUID lmid = tpReq.Info.LandmarkID;
                        AssetBase lma = m_assetCache.GetAsset(lmid);
                        if (lma != null)
                        {
                            AssetLandmark lm = new AssetLandmark(lma);

                            if (lm.RegionID == m_scene.RegionInfo.RegionID)
                            {
                                TeleportLocalPacket tpLocal = new TeleportLocalPacket();

                                tpLocal.Info.AgentID = tpReq.Info.AgentID;
                                tpLocal.Info.TeleportFlags = 8; // Teleport via landmark
                                tpLocal.Info.LocationID = 2;
                                tpLocal.Info.Position = lm.Position;
                                OutPacket(tpLocal, ThrottleOutPacketType.Task);
                            }
                            else
                            {
                                TeleportCancelPacket tpCancel = new TeleportCancelPacket();
                                tpCancel.Info.AgentID = tpReq.Info.AgentID;
                                tpCancel.Info.SessionID = tpReq.Info.SessionID;
                                OutPacket(tpCancel, ThrottleOutPacketType.Task);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Cancelling Teleport - fetch asset not yet implemented");

                            TeleportCancelPacket tpCancel = new TeleportCancelPacket();
                            tpCancel.Info.AgentID = tpReq.Info.AgentID;
                            tpCancel.Info.SessionID = tpReq.Info.SessionID;
                            OutPacket(tpCancel, ThrottleOutPacketType.Task);
                        }
                        break;
                    case PacketType.TeleportLocationRequest:
                        TeleportLocationRequestPacket tpLocReq = (TeleportLocationRequestPacket) Pack;
                        // Console.WriteLine(tpLocReq.ToString());

                        if (OnTeleportLocationRequest != null)
                        {
                            OnTeleportLocationRequest(this, tpLocReq.Info.RegionHandle, tpLocReq.Info.Position,
                                                      tpLocReq.Info.LookAt, 16);
                        }
                        else
                        {
                            //no event handler so cancel request
                            TeleportCancelPacket tpCancel = new TeleportCancelPacket();
                            tpCancel.Info.SessionID = tpLocReq.AgentData.SessionID;
                            tpCancel.Info.AgentID = tpLocReq.AgentData.AgentID;
                            OutPacket(tpCancel, ThrottleOutPacketType.Task);
                        }
                        break;

                        #endregion

                    case PacketType.MoneyBalanceRequest:
                        SendMoneyBalance(LLUUID.Zero, true, new byte[0], MoneyBalance);
                        break;
                    case PacketType.UUIDNameRequest:
                        UUIDNameRequestPacket incoming = (UUIDNameRequestPacket) Pack;
                        foreach (UUIDNameRequestPacket.UUIDNameBlockBlock UUIDBlock in incoming.UUIDNameBlock)
                        {
                            OnNameFromUUIDRequest(UUIDBlock.ID, this);
                        }
                        break;

                        #region Parcel related packets

                    case PacketType.ParcelPropertiesRequest:
                        ParcelPropertiesRequestPacket propertiesRequest = (ParcelPropertiesRequestPacket) Pack;
                        if (OnParcelPropertiesRequest != null)
                        {
                            OnParcelPropertiesRequest((int) Math.Round(propertiesRequest.ParcelData.West),
                                                      (int) Math.Round(propertiesRequest.ParcelData.South),
                                                      (int) Math.Round(propertiesRequest.ParcelData.East),
                                                      (int) Math.Round(propertiesRequest.ParcelData.North),
                                                      propertiesRequest.ParcelData.SequenceID,
                                                      propertiesRequest.ParcelData.SnapSelection, this);
                        }
                        break;
                    case PacketType.ParcelDivide:
                        ParcelDividePacket landDivide = (ParcelDividePacket) Pack;
                        if (OnParcelDivideRequest != null)
                        {
                            OnParcelDivideRequest((int) Math.Round(landDivide.ParcelData.West),
                                                  (int) Math.Round(landDivide.ParcelData.South),
                                                  (int) Math.Round(landDivide.ParcelData.East),
                                                  (int) Math.Round(landDivide.ParcelData.North), this);
                        }
                        break;
                    case PacketType.ParcelJoin:
                        ParcelJoinPacket landJoin = (ParcelJoinPacket) Pack;
                        if (OnParcelJoinRequest != null)
                        {
                            OnParcelJoinRequest((int) Math.Round(landJoin.ParcelData.West),
                                                (int) Math.Round(landJoin.ParcelData.South),
                                                (int) Math.Round(landJoin.ParcelData.East),
                                                (int) Math.Round(landJoin.ParcelData.North), this);
                        }
                        break;
                    case PacketType.ParcelPropertiesUpdate:
                        ParcelPropertiesUpdatePacket updatePacket = (ParcelPropertiesUpdatePacket) Pack;
                        if (OnParcelPropertiesUpdateRequest != null)
                        {
                            OnParcelPropertiesUpdateRequest(updatePacket, this);
                        }
                        break;
                    case PacketType.ParcelSelectObjects:
                        ParcelSelectObjectsPacket selectPacket = (ParcelSelectObjectsPacket) Pack;
                        if (OnParcelSelectObjects != null)
                        {
                            OnParcelSelectObjects(selectPacket.ParcelData.LocalID,
                                                  Convert.ToInt32(selectPacket.ParcelData.ReturnType), this);
                        }
                        break;
                    case PacketType.ParcelObjectOwnersRequest:
                        //System.Console.WriteLine(Pack.ToString());
                        ParcelObjectOwnersRequestPacket reqPacket = (ParcelObjectOwnersRequestPacket) Pack;
                        if (OnParcelObjectOwnerRequest != null)
                        {
                            OnParcelObjectOwnerRequest(reqPacket.ParcelData.LocalID, this);
                        }
                        break;

                        #endregion

                        #region Estate Packets

                    case PacketType.EstateOwnerMessage:
                        EstateOwnerMessagePacket messagePacket = (EstateOwnerMessagePacket) Pack;
                        if (OnEstateOwnerMessage != null)
                        {
                            OnEstateOwnerMessage(messagePacket, this);
                        }
                        break;

                    case PacketType.AgentThrottle:

                        //OpenSim.Framework.Console.MainLog.Instance.Verbose("CLIENT", "unhandled packet " + Pack.ToString());

                        AgentThrottlePacket atpack = (AgentThrottlePacket)Pack;

                        byte[] throttle = atpack.Throttle.Throttles;
                        int tResend = -1;
                        int tLand = -1;
                        int tWind = -1;
                        int tCloud = -1;
                        int tTask = -1;
                        int tTexture = -1;
                        int tAsset = -1;
                        int tall = -1;
                        int singlefloat = 4;

                        //Agent Throttle Block contains 7 single floatingpoint values.
                        int j = 0;

                        // Some Systems may be big endian...  
                        // it might be smart to do this check more often... 
                        if (!BitConverter.IsLittleEndian)
                            for (int i = 0; i < 7; i++)
                                Array.Reverse(throttle, j + i * singlefloat, singlefloat);

                        // values gotten from libsecondlife.org/wiki/Throttle.  Thanks MW_
                        // bytes
                        // Convert to integer, since..   the full fp space isn't used.
                        tResend = (int)BitConverter.ToSingle(throttle, j);
                        j += singlefloat;
                        tLand = (int)BitConverter.ToSingle(throttle, j);
                        j += singlefloat;
                        tWind = (int)BitConverter.ToSingle(throttle, j);
                        j += singlefloat;
                        tCloud = (int)BitConverter.ToSingle(throttle, j);
                        j += singlefloat;
                        tTask = (int)BitConverter.ToSingle(throttle, j);
                        j += singlefloat;
                        tTexture = (int)BitConverter.ToSingle(throttle, j);
                        j += singlefloat;
                        tAsset = (int)BitConverter.ToSingle(throttle, j);

                        tall = tResend + tLand + tWind + tCloud + tTask + tTexture + tAsset;
                        /*
                        OpenSim.Framework.Console.MainLog.Instance.Verbose("CLIENT", "Client AgentThrottle - Got throttle:resendbytes=" + tResend +
                            " landbytes=" + tLand +
                            " windbytes=" + tWind +
                            " cloudbytes=" + tCloud +
                            " taskbytes=" + tTask +
                            " texturebytes=" + tTexture +
                            " Assetbytes=" + tAsset +
                            " Allbytes=" + tall);
                         */

                        // Total Sanity
                        // Make sure that the client sent sane total values.

                        // If the client didn't send acceptable values....
                        // Scale the clients values down until they are acceptable.

                        if (tall <= throttleOutboundMax)
                        {
                            // Sanity
                            // Making sure the client sends sane values
                            // This gives us a measure of control of the comms
                            // Check Max of Type
                            // Then Check Min of type

                            // Resend throttle
                            if (tResend <= ResendthrottleMAX)
                                ResendthrottleOutbound = tResend;

                            if (tResend < ResendthrottleMin)
                                ResendthrottleOutbound = ResendthrottleMin;

                            // Land throttle
                            if (tLand <= LandthrottleMax)
                                LandthrottleOutbound = tLand;

                            if (tLand < LandthrottleMin)
                                LandthrottleOutbound = LandthrottleMin;

                            // Wind throttle 
                            if (tWind <= WindthrottleMax)
                                WindthrottleOutbound = tWind;

                            if (tWind < WindthrottleMin)
                                WindthrottleOutbound = WindthrottleMin;

                            // Cloud throttle 
                            if (tCloud <= CloudthrottleMax)
                                CloudthrottleOutbound = tCloud;

                            if (tCloud < CloudthrottleMin)
                                CloudthrottleOutbound = CloudthrottleMin;

                            // Task throttle 
                            if (tTask <= TaskthrottleMax)
                                TaskthrottleOutbound = tTask;

                            if (tTask < TaskthrottleMin)
                                TaskthrottleOutbound = TaskthrottleMin;

                            // Texture throttle 
                            if (tTexture <= TexturethrottleMax)
                                TexturethrottleOutbound = tTexture;

                            if (tTexture < TexturethrottleMin)
                                TexturethrottleOutbound = TexturethrottleMin;

                            //Asset throttle
                            if (tAsset <= AssetthrottleMax)
                                AssetthrottleOutbound = tAsset;

                            if (tAsset < AssetthrottleMin)
                                AssetthrottleOutbound = AssetthrottleMin;

                          /*  OpenSim.Framework.Console.MainLog.Instance.Verbose("THROTTLE", "Using:resendbytes=" + ResendthrottleOutbound +
                            " landbytes=" + LandthrottleOutbound +
                            " windbytes=" + WindthrottleOutbound +
                            " cloudbytes=" + CloudthrottleOutbound +
                            " taskbytes=" + TaskthrottleOutbound +
                            " texturebytes=" + TexturethrottleOutbound +
                            " Assetbytes=" + AssetthrottleOutbound +
                            " Allbytes=" + tall);
                           */
                        }
                        else
                        {
                            // The client didn't send acceptable values..  
                            // so it's our job now to turn them into acceptable values
                            // We're going to first scale the values down
                            // After that we're going to check if the scaled values are sane

                            // We're going to be dividing by a user value..   so make sure
                            // we don't get a divide by zero error.
                            if (tall > 0)
                            {
                                // Find out the percentage of all communications 
                                // the client requests for each type.  We'll keep resend at 
                                // it's client recommended level (won't scale it down)
                                // unless it's beyond sane values itself.

                                if (tResend <= ResendthrottleMAX)
                                {
                                    // This is nexted because we only want to re-set the values
                                    // the packet throttler uses once.

                                    if (tResend >= ResendthrottleMin)
                                    {
                                        ResendthrottleOutbound = tResend;
                                    }
                                    else
                                    {
                                        ResendthrottleOutbound = ResendthrottleMin;
                                    }
                                }
                                else
                                {
                                    ResendthrottleOutbound = ResendthrottleMAX;
                                }


                                // Getting Percentages of communication for each type of data
                                float LandPercent = (float)(tLand / tall);
                                float WindPercent = (float)(tWind / tall);
                                float CloudPercent = (float)(tCloud / tall);
                                float TaskPercent = (float)(tTask / tall);
                                float TexturePercent = (float)(tTexture / tall);
                                float AssetPercent = (float)(tAsset / tall);

                                // Okay..  now we've got the percentages of total communication.
                                // Apply them to a new max total

                                int tLandResult = (int)(LandPercent * throttleOutboundMax);
                                int tWindResult = (int)(WindPercent * throttleOutboundMax);
                                int tCloudResult = (int)(CloudPercent * throttleOutboundMax);
                                int tTaskResult = (int)(TaskPercent * throttleOutboundMax);
                                int tTextureResult = (int)(TexturePercent * throttleOutboundMax);
                                int tAssetResult = (int)(AssetPercent * throttleOutboundMax);

                                // Now we have to check our scaled values for sanity

                                // Check Max of Type
                                // Then Check Min of type

                                // Land throttle
                                if (tLandResult <= LandthrottleMax)
                                    LandthrottleOutbound = tLandResult;

                                if (tLandResult < LandthrottleMin)
                                    LandthrottleOutbound = LandthrottleMin;

                                // Wind throttle 
                                if (tWindResult <= WindthrottleMax)
                                    WindthrottleOutbound = tWindResult;

                                if (tWindResult < WindthrottleMin)
                                    WindthrottleOutbound = WindthrottleMin;

                                // Cloud throttle 
                                if (tCloudResult <= CloudthrottleMax)
                                    CloudthrottleOutbound = tCloudResult;

                                if (tCloudResult < CloudthrottleMin)
                                    CloudthrottleOutbound = CloudthrottleMin;

                                // Task throttle 
                                if (tTaskResult <= TaskthrottleMax)
                                    TaskthrottleOutbound = tTaskResult;

                                if (tTaskResult < TaskthrottleMin)
                                    TaskthrottleOutbound = TaskthrottleMin;

                                // Texture throttle 
                                if (tTextureResult <= TexturethrottleMax)
                                    TexturethrottleOutbound = tTextureResult;

                                if (tTextureResult < TexturethrottleMin)
                                    TexturethrottleOutbound = TexturethrottleMin;

                                //Asset throttle
                                if (tAssetResult <= AssetthrottleMax)
                                    AssetthrottleOutbound = tAssetResult;

                                if (tAssetResult < AssetthrottleMin)
                                    AssetthrottleOutbound = AssetthrottleMin;

                                /* OpenSim.Framework.Console.MainLog.Instance.Verbose("THROTTLE", "Using:resendbytes=" + ResendthrottleOutbound +
                                    " landbytes=" + LandthrottleOutbound +
                                    " windbytes=" + WindthrottleOutbound +
                                    " cloudbytes=" + CloudthrottleOutbound +
                                    " taskbytes=" + TaskthrottleOutbound +
                                    " texturebytes=" + TexturethrottleOutbound +
                                    " Assetbytes=" + AssetthrottleOutbound +
                                    " Allbytes=" + tall);
                                  */

                            }
                            else
                            {

                                // The client sent a stupid value..   
                                // We're going to set the throttles to the minimum possible
                                ResendthrottleOutbound = ResendthrottleMin;
                                LandthrottleOutbound = LandthrottleMin;
                                WindthrottleOutbound = WindthrottleMin;
                                CloudthrottleOutbound = CloudthrottleMin;
                                TaskthrottleOutbound = TaskthrottleMin;
                                TexturethrottleOutbound = TexturethrottleMin;
                                AssetthrottleOutbound = AssetthrottleMin;
                                OpenSim.Framework.Console.MainLog.Instance.Verbose("THROTTLE", "ClientSentBadThrottle Using:resendbytes=" + ResendthrottleOutbound +
                                    " landbytes=" + LandthrottleOutbound +
                                    " windbytes=" + WindthrottleOutbound +
                                    " cloudbytes=" + CloudthrottleOutbound +
                                    " taskbytes=" + TaskthrottleOutbound +
                                    " texturebytes=" + TexturethrottleOutbound +
                                    " Assetbytes=" + AssetthrottleOutbound +
                                    " Allbytes=" + tall);
                            }

                        }
                        // Reset Client Throttles
                        // This has the effect of 'wiggling the slider 
                        // causes prim and stuck textures that didn't download to download

                        ResendBytesSent = 0;
                        LandBytesSent = 0;
                        WindBytesSent = 0;
                        CloudBytesSent = 0;
                        TaskBytesSent = 0;
                        AssetBytesSent = 0;
                        TextureBytesSent = 0;

                        //Yay, we've finally handled the agent Throttle packet!
                   


                        break;

                        #endregion

                        #region unimplemented handlers
                    case PacketType.RequestGodlikePowers:
                        RequestGodlikePowersPacket rglpPack = (RequestGodlikePowersPacket) Pack;
                        RequestGodlikePowersPacket.RequestBlockBlock rblock = rglpPack.RequestBlock;
                        LLUUID token = rblock.Token;
                        RequestGodlikePowersPacket.AgentDataBlock ablock = rglpPack.AgentData;

                        OnRequestGodlikePowers(ablock.AgentID, ablock.SessionID, token, this);
                        
                        break;
                    case PacketType.GodKickUser:
                        OpenSim.Framework.Console.MainLog.Instance.Verbose("CLIENT", "unhandled packet " + Pack.ToString());
                        
                        GodKickUserPacket gkupack = (GodKickUserPacket) Pack;
                        
                        if (gkupack.UserInfo.GodSessionID == SessionId && this.AgentId == gkupack.UserInfo.GodID)
                        {
                            OnGodKickUser(gkupack.UserInfo.GodID, gkupack.UserInfo.GodSessionID, gkupack.UserInfo.AgentID, (uint) 0, gkupack.UserInfo.Reason);
                        }
                        else
                        {
                            SendAgentAlertMessage("Kick request denied", false);
                        }
                        //KickUserPacket kupack = new KickUserPacket();
                        //KickUserPacket.UserInfoBlock kupackib = kupack.UserInfo;

                        //kupack.UserInfo.AgentID = gkupack.UserInfo.AgentID;
                        //kupack.UserInfo.SessionID = gkupack.UserInfo.GodSessionID;

                        //kupack.TargetBlock.TargetIP = (uint)0;
                        //kupack.TargetBlock.TargetPort = (ushort)0;
                        //kupack.UserInfo.Reason = gkupack.UserInfo.Reason;


                        //OutPacket(kupack, ThrottleOutPacketType.Task);
                        break;

                    case PacketType.StartPingCheck:
                        // Send the client the ping response back
                        // Pass the same PingID in the matching packet
                        // Handled In the packet processing
                        OpenSim.Framework.Console.MainLog.Instance.Debug("CLIENT", "possibly unhandled packet " + Pack.ToString());
                        break;
                    case PacketType.CompletePingCheck:
                        // Parhaps this should be processed on the Sim to determine whether or not to drop a dead client
                        // Dumping it to the verbose console until it's handled properly.

                        OpenSim.Framework.Console.MainLog.Instance.Verbose("CLIENT", "unhandled packet " + Pack.ToString());
                        break;
                    case PacketType.AgentIsNowWearing:
                        // AgentIsNowWearingPacket wear = (AgentIsNowWearingPacket)Pack;
                        OpenSim.Framework.Console.MainLog.Instance.Verbose("CLIENT", "unhandled packet " + Pack.ToString());
                        break;
                    case PacketType.ObjectScale:
                        OpenSim.Framework.Console.MainLog.Instance.Verbose("CLIENT", "unhandled packet " + Pack.ToString());
                        break;
                    default:
                        OpenSim.Framework.Console.MainLog.Instance.Verbose("CLIENT", "unhandled packet " + Pack.ToString());
                        break;
                    
                        #endregion
                }
            }
        }

        private static PrimitiveBaseShape GetShapeFromAddPacket(ObjectAddPacket addPacket)
        {
            PrimitiveBaseShape shape = new PrimitiveBaseShape();

            shape.PCode = addPacket.ObjectData.PCode;
            shape.PathBegin = addPacket.ObjectData.PathBegin;
            shape.PathEnd = addPacket.ObjectData.PathEnd;
            shape.PathScaleX = addPacket.ObjectData.PathScaleX;
            shape.PathScaleY = addPacket.ObjectData.PathScaleY;
            shape.PathShearX = addPacket.ObjectData.PathShearX;
            shape.PathShearY = addPacket.ObjectData.PathShearY;
            shape.PathSkew = addPacket.ObjectData.PathSkew;
            shape.ProfileBegin = addPacket.ObjectData.ProfileBegin;
            shape.ProfileEnd = addPacket.ObjectData.ProfileEnd;
            shape.Scale = addPacket.ObjectData.Scale;
            shape.PathCurve = addPacket.ObjectData.PathCurve;
            shape.ProfileCurve = addPacket.ObjectData.ProfileCurve;
            shape.ProfileHollow = addPacket.ObjectData.ProfileHollow;
            shape.PathRadiusOffset = addPacket.ObjectData.PathRadiusOffset;
            shape.PathRevolutions = addPacket.ObjectData.PathRevolutions;
            shape.PathTaperX = addPacket.ObjectData.PathTaperX;
            shape.PathTaperY = addPacket.ObjectData.PathTaperY;
            shape.PathTwist = addPacket.ObjectData.PathTwist;
            shape.PathTwistBegin = addPacket.ObjectData.PathTwistBegin;
            LLObject.TextureEntry ntex = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-9999-000000000005"));
            shape.TextureEntry = ntex.ToBytes();
            return shape;
        }

        public void SendLogoutPacket()
        {
            LogoutReplyPacket logReply = new LogoutReplyPacket();
            logReply.AgentData.AgentID = AgentId;
            logReply.AgentData.SessionID = SessionId;
            logReply.InventoryData = new LogoutReplyPacket.InventoryDataBlock[1];
            logReply.InventoryData[0] = new LogoutReplyPacket.InventoryDataBlock();
            logReply.InventoryData[0].ItemID = LLUUID.Zero;

            OutPacket(logReply, ThrottleOutPacketType.Task);
        }
    }
}
