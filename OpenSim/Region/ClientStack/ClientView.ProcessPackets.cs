/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;

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

        protected override void ProcessInPacket(Packet Pack)
        {
            ack_pack(Pack);
            if (debug)
            {
                if (Pack.Type != PacketType.AgentUpdate)
                {
                    Console.WriteLine(CircuitCode + ":IN: " + Pack.Type.ToString());
                }
            }

            if (this.ProcessPacketMethod(Pack))
            {
                //there is a handler registered that handled this packet type 
                return;
            }
            else
            {
                Encoding _enc = Encoding.ASCII;

                switch (Pack.Type)
                {
                    case PacketType.ViewerEffect:
                        ViewerEffectPacket viewer = (ViewerEffectPacket)Pack;
                        foreach (ClientView client in m_clientThreads.Values)
                        {
                            if (client.AgentID != this.AgentID)
                            {
                                viewer.AgentData.AgentID = client.AgentID;
                                viewer.AgentData.SessionID = client.SessionID;
                                client.OutPacket(viewer);
                            }
                        }
                        break;

                    #region  Scene/Avatar
                    case PacketType.AvatarPropertiesRequest:
                        AvatarPropertiesRequestPacket avatarProperties = (AvatarPropertiesRequestPacket)Pack;
                        if (OnRequestAvatarProperties != null)
                        {
                            OnRequestAvatarProperties(this, avatarProperties.AgentData.AvatarID);
                        }
                        break;
                    case PacketType.ChatFromViewer:
                        ChatFromViewerPacket inchatpack = (ChatFromViewerPacket)Pack;
                        if (Util.FieldToString(inchatpack.ChatData.Message) == "")
                        {
                            //empty message so don't bother with it
                            break;
                        }
                        string fromName = ""; //ClientAvatar.firstname + " " + ClientAvatar.lastname;
                        byte[] message = inchatpack.ChatData.Message;
                        byte type = inchatpack.ChatData.Type;
                        LLVector3 fromPos = new LLVector3(); // ClientAvatar.Pos;
                        LLUUID fromAgentID = AgentID;
                        if (OnChatFromViewer != null)
                        {
                            this.OnChatFromViewer(message, type, fromPos, fromName, fromAgentID);
                        }
                        break;
                    case PacketType.ImprovedInstantMessage:
                        ImprovedInstantMessagePacket msgpack = (ImprovedInstantMessagePacket)Pack;
                        string IMfromName = Util.FieldToString(msgpack.MessageBlock.FromAgentName);
                        string IMmessage = Util.FieldToString(msgpack.MessageBlock.Message);
                        if (OnInstantMessage != null)
                        {
                            this.OnInstantMessage(msgpack.AgentData.AgentID, msgpack.AgentData.SessionID, msgpack.MessageBlock.ToAgentID, msgpack.MessageBlock.ID,
                                msgpack.MessageBlock.Timestamp, IMfromName, IMmessage, msgpack.MessageBlock.Dialog);
                        }
                        break;
                    case PacketType.RezObject:
                        RezObjectPacket rezPacket = (RezObjectPacket)Pack;
                        if (OnRezObject != null)
                        {
                            this.OnRezObject(this, rezPacket.InventoryData.ItemID, rezPacket.RezData.RayEnd);
                        }
                        break;
                    case PacketType.DeRezObject:
                        if (OnDeRezObject != null)
                        {
                            OnDeRezObject(Pack, this);
                        }
                        break;
                    case PacketType.ModifyLand:
                        ModifyLandPacket modify = (ModifyLandPacket)Pack;
                        if (modify.ParcelData.Length > 0)
                        {
                            if (OnModifyTerrain != null)
                            {
                                OnModifyTerrain(modify.ModifyBlock.Height, modify.ModifyBlock.Seconds, modify.ModifyBlock.BrushSize,
                                    modify.ModifyBlock.Action, modify.ParcelData[0].North, modify.ParcelData[0].West, this);
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
                            OnRequestWearables(this);
                        }
                        if (OnRequestAvatarsData != null)
                        {
                            OnRequestAvatarsData(this);
                        }
                        break;
                    case PacketType.AgentSetAppearance:
                        AgentSetAppearancePacket appear = (AgentSetAppearancePacket)Pack;
                        if (OnSetAppearance != null)
                        {
                            OnSetAppearance(appear.ObjectData.TextureEntry, appear.VisualParam);
                        }
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
                            AgentUpdatePacket agenUpdate = (AgentUpdatePacket)Pack;
                            OnAgentUpdate(this, agenUpdate.AgentData.ControlFlags, agenUpdate.AgentData.BodyRotation);
                        }
                        break;
                    case PacketType.AgentAnimation:
                        if (!m_child)
                        {
                            AgentAnimationPacket AgentAni = (AgentAnimationPacket)Pack;
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
                        }
                        break;

                    #endregion

                    #region Objects/Prims
                    case PacketType.ObjectLink:
                        // OpenSim.Framework.Console.MainLog.Instance.Verbose( Pack.ToString());
                        ObjectLinkPacket link = (ObjectLinkPacket)Pack;
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
                    case PacketType.ObjectAdd:
                        if (OnAddPrim != null)
                        {
                            ObjectAddPacket addPacket = (ObjectAddPacket)Pack;
                            PrimitiveBaseShape shape = GetShapeFromAddPacket(addPacket);
                            OnAddPrim(this.AgentId, addPacket.ObjectData.RayEnd, shape);
                        }
                        break;
                    case PacketType.ObjectShape:
                        ObjectShapePacket shapePacket = (ObjectShapePacket)Pack;
                        for (int i = 0; i < shapePacket.ObjectData.Length; i++)
                        {
                            if (OnUpdatePrimShape != null)
                            {
                                OnUpdatePrimShape(shapePacket.ObjectData[i].ObjectLocalID, shapePacket.ObjectData[i]);
                            }
                        }
                        break;
                    case PacketType.ObjectExtraParams:
                        ObjectExtraParamsPacket extraPar = (ObjectExtraParamsPacket)Pack;
                        if (OnUpdateExtraParams != null)
                        {
                            OnUpdateExtraParams(extraPar.ObjectData[0].ObjectLocalID, extraPar.ObjectData[0].ParamType, extraPar.ObjectData[0].ParamInUse, extraPar.ObjectData[0].ParamData);
                        }
                        break;
                    case PacketType.ObjectDuplicate:
                        ObjectDuplicatePacket dupe = (ObjectDuplicatePacket)Pack;
                        for (int i = 0; i < dupe.ObjectData.Length; i++)
                        {
                            if (OnObjectDuplicate != null)
                            {
                                OnObjectDuplicate(dupe.ObjectData[i].ObjectLocalID, dupe.SharedData.Offset, dupe.SharedData.DuplicateFlags);
                            }
                        }

                        break;

                    case PacketType.ObjectSelect:
                        ObjectSelectPacket incomingselect = (ObjectSelectPacket)Pack;
                        for (int i = 0; i < incomingselect.ObjectData.Length; i++)
                        {
                            if (OnObjectSelect != null)
                            {
                                OnObjectSelect(incomingselect.ObjectData[i].ObjectLocalID, this);
                            }
                        }
                        break;
                    case PacketType.ObjectDeselect:
                        ObjectDeselectPacket incomingdeselect = (ObjectDeselectPacket)Pack;
                        for (int i = 0; i < incomingdeselect.ObjectData.Length; i++)
                        {
                            if (OnObjectDeselect != null)
                            {
                                OnObjectDeselect(incomingdeselect.ObjectData[i].ObjectLocalID, this);
                            }
                        }
                        break;
                    case PacketType.ObjectFlagUpdate:
                        ObjectFlagUpdatePacket flags = (ObjectFlagUpdatePacket)Pack;
                        if (OnUpdatePrimFlags != null)
                        {
                            OnUpdatePrimFlags(flags.AgentData.ObjectLocalID, Pack, this);
                        }
                        break;
                    case PacketType.ObjectImage:
                        ObjectImagePacket imagePack = (ObjectImagePacket)Pack;
                        for (int i = 0; i < imagePack.ObjectData.Length; i++)
                        {
                            if (OnUpdatePrimTexture != null)
                            {
                                OnUpdatePrimTexture(imagePack.ObjectData[i].ObjectLocalID, imagePack.ObjectData[i].TextureEntry, this);
                            }
                        }
                        break;
                    case PacketType.ObjectGrab:
                        ObjectGrabPacket grab = (ObjectGrabPacket)Pack;
                        if (OnGrabObject != null)
                        {
                            OnGrabObject(grab.ObjectData.LocalID, grab.ObjectData.GrabOffset, this);
                        }
                        break;
                    case PacketType.ObjectGrabUpdate:
                        ObjectGrabUpdatePacket grabUpdate = (ObjectGrabUpdatePacket)Pack;
                        if (OnGrabUpdate != null)
                        {
                            OnGrabUpdate(grabUpdate.ObjectData.ObjectID, grabUpdate.ObjectData.GrabOffsetInitial, grabUpdate.ObjectData.GrabPosition, this);
                        }
                        break;
                    case PacketType.ObjectDeGrab:
                        ObjectDeGrabPacket deGrab = (ObjectDeGrabPacket)Pack;
                        if (OnDeGrabObject != null)
                        {
                            OnDeGrabObject(deGrab.ObjectData.LocalID, this);
                        }
                        break;
                    case PacketType.ObjectDescription:
                        ObjectDescriptionPacket objDes = (ObjectDescriptionPacket)Pack;
                        for (int i = 0; i < objDes.ObjectData.Length; i++)
                        {
                            if (OnObjectDescription != null)
                            {
                                OnObjectDescription(objDes.ObjectData[i].LocalID, enc.GetString(objDes.ObjectData[i].Description));
                            }
                        }
                        break;
                    case PacketType.ObjectName:
                        ObjectNamePacket objName = (ObjectNamePacket)Pack;
                        for (int i = 0; i < objName.ObjectData.Length; i++)
                        {
                            if (OnObjectName != null)
                            {
                                OnObjectName(objName.ObjectData[i].LocalID, enc.GetString(objName.ObjectData[i].Name));
                            }
                        }
                        break;
                    case PacketType.ObjectPermissions:
                        //Console.WriteLine("permissions set " + Pack.ToString());
                        break;
                    #endregion

                    #region Inventory/Asset/Other related packets
                    case PacketType.RequestImage:
                        RequestImagePacket imageRequest = (RequestImagePacket)Pack;
                        //Console.WriteLine("image request: " + Pack.ToString());
                        for (int i = 0; i < imageRequest.RequestImage.Length; i++)
                        {
                            m_assetCache.AddTextureRequest(this, imageRequest.RequestImage[i].Image, imageRequest.RequestImage[i].Packet);
                        }
                        break;
                    case PacketType.TransferRequest:
                        //Console.WriteLine("OpenSimClient.cs:ProcessInPacket() - Got transfer request");
                        TransferRequestPacket transfer = (TransferRequestPacket)Pack;
                        m_assetCache.AddAssetRequest(this, transfer);
                        break;
                    case PacketType.AssetUploadRequest:
                        //Console.WriteLine("upload request " + Pack.ToString());
                        AssetUploadRequestPacket request = (AssetUploadRequestPacket)Pack;
                        if (OnAssetUploadRequest != null)
                        {
                            OnAssetUploadRequest(this, request.AssetBlock.TransactionID.Combine(this.SecureSessionID), request.AssetBlock.TransactionID, request.AssetBlock.Type, request.AssetBlock.AssetData, request.AssetBlock.StoreLocal);
                        }
                        break;
                    case PacketType.RequestXfer:
                        RequestXferPacket xferReq = (RequestXferPacket)Pack;
                        if (OnRequestXfer != null)
                        {
                            OnRequestXfer(this, xferReq.XferID.ID, Util.FieldToString(xferReq.XferID.Filename));
                        }
                        break;
                    case PacketType.SendXferPacket:
                        SendXferPacketPacket xferRec = (SendXferPacketPacket)Pack;
                        if (OnXferReceive != null)
                        {
                            OnXferReceive(this, xferRec.XferID.ID, xferRec.XferID.Packet, xferRec.DataPacket.Data);
                        }
                        break;
                    case PacketType.ConfirmXferPacket:
                        ConfirmXferPacketPacket confirmXfer = (ConfirmXferPacketPacket)Pack;
                        if (OnConfirmXfer != null)
                        {
                            OnConfirmXfer(this, confirmXfer.XferID.ID, confirmXfer.XferID.Packet);
                        }
                        break;
                    case PacketType.CreateInventoryFolder:
                        if (this.OnCreateNewInventoryFolder != null)
                        {
                            CreateInventoryFolderPacket invFolder = (CreateInventoryFolderPacket)Pack;
                            this.OnCreateNewInventoryFolder(this, invFolder.FolderData.FolderID, (ushort)invFolder.FolderData.Type, Util.FieldToString(invFolder.FolderData.Name), invFolder.FolderData.ParentID);
                        }
                        break;
                    case PacketType.CreateInventoryItem:
                        CreateInventoryItemPacket createItem = (CreateInventoryItemPacket)Pack;
                        if (this.OnCreateNewInventoryItem != null)
                        {
                            this.OnCreateNewInventoryItem(this, createItem.InventoryBlock.TransactionID, createItem.InventoryBlock.FolderID, createItem.InventoryBlock.CallbackID,
                                Util.FieldToString(createItem.InventoryBlock.Description), Util.FieldToString(createItem.InventoryBlock.Name), createItem.InventoryBlock.InvType,
                                createItem.InventoryBlock.Type, createItem.InventoryBlock.WearableType, createItem.InventoryBlock.NextOwnerMask);
                        }
                        break;
                    case PacketType.FetchInventory:
                        if (this.OnFetchInventory != null)
                        {
                            FetchInventoryPacket FetchInventory = (FetchInventoryPacket)Pack;
                            for (int i = 0; i < FetchInventory.InventoryData.Length; i++)
                            {
                                this.OnFetchInventory(this, FetchInventory.InventoryData[i].ItemID, FetchInventory.InventoryData[i].OwnerID);
                            }
                        }
                        break;
                    case PacketType.FetchInventoryDescendents:
                        if (this.OnFetchInventoryDescendents != null)
                        {
                            FetchInventoryDescendentsPacket Fetch = (FetchInventoryDescendentsPacket)Pack;
                            this.OnFetchInventoryDescendents(this, Fetch.InventoryData.FolderID, Fetch.InventoryData.OwnerID, Fetch.InventoryData.FetchFolders, Fetch.InventoryData.FetchItems, Fetch.InventoryData.SortOrder);
                        }
                        break;
                    case PacketType.UpdateInventoryItem:
                        UpdateInventoryItemPacket update = (UpdateInventoryItemPacket)Pack;
                        if (OnUpdateInventoryItem != null)
                        {
                            for (int i = 0; i < update.InventoryData.Length; i++)
                            {
                                if (update.InventoryData[i].TransactionID != LLUUID.Zero)
                                {
                                    OnUpdateInventoryItem(this, update.InventoryData[i].TransactionID, update.InventoryData[i].TransactionID.Combine(this.SecureSessionID), update.InventoryData[i].ItemID);
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
                    case PacketType.RequestTaskInventory:
                        RequestTaskInventoryPacket requesttask = (RequestTaskInventoryPacket)Pack;
                        if (this.OnRequestTaskInventory != null)
                        {
                            this.OnRequestTaskInventory(this, requesttask.InventoryData.LocalID);
                        }
                        break;
                    case PacketType.UpdateTaskInventory:
                        //Console.WriteLine(Pack.ToString());
                        UpdateTaskInventoryPacket updatetask = (UpdateTaskInventoryPacket)Pack;
                        if (OnUpdateTaskInventory != null)
                        {
                            if (updatetask.UpdateData.Key == 0)
                            {
                                OnUpdateTaskInventory(this, updatetask.InventoryData.ItemID, updatetask.InventoryData.FolderID, updatetask.UpdateData.LocalID);
                            }
                        }
                        break;
                    case PacketType.RemoveTaskInventory:
                        RemoveTaskInventoryPacket removeTask = (RemoveTaskInventoryPacket)Pack;
                        if (OnRemoveTaskItem != null)
                        {
                            OnRemoveTaskItem(this, removeTask.InventoryData.ItemID, removeTask.InventoryData.LocalID);
                        }
                        break;
                    case PacketType.MoveTaskInventory:
                        //Console.WriteLine(Pack.ToString());
                        break;
                    case PacketType.RezScript:
                        //Console.WriteLine(Pack.ToString());
                        RezScriptPacket rezScript = (RezScriptPacket)Pack;
                        if (OnRezScript != null)
                        {
                            OnRezScript(this, rezScript.InventoryBlock.ItemID, rezScript.UpdateBlock.ObjectLocalID);
                        }
                        break;
                    case PacketType.MapLayerRequest:
                        this.RequestMapLayer();
                        break;
                    case PacketType.MapBlockRequest:
                        MapBlockRequestPacket MapRequest = (MapBlockRequestPacket)Pack;
                        if (OnRequestMapBlocks != null)
                        {
                            OnRequestMapBlocks(this, MapRequest.PositionData.MinX, MapRequest.PositionData.MinY, MapRequest.PositionData.MaxX, MapRequest.PositionData.MaxY);
                        }
                        break;
                    case PacketType.TeleportLandmarkRequest:
                        TeleportLandmarkRequestPacket tpReq = (TeleportLandmarkRequestPacket)Pack;

                        TeleportStartPacket tpStart = new TeleportStartPacket();
                        tpStart.Info.TeleportFlags = 8; // tp via lm
                        this.OutPacket(tpStart);

                        TeleportProgressPacket tpProgress = new TeleportProgressPacket();
                        tpProgress.Info.Message = (new ASCIIEncoding()).GetBytes("sending_landmark");
                        tpProgress.Info.TeleportFlags = 8;
                        tpProgress.AgentData.AgentID = tpReq.Info.AgentID;
                        this.OutPacket(tpProgress);

                        // Fetch landmark
                        LLUUID lmid = tpReq.Info.LandmarkID;
                        AssetBase lma = this.m_assetCache.GetAsset(lmid);
                        if (lma != null)
                        {
                            AssetLandmark lm = new AssetLandmark(lma);

                            if (lm.RegionID == m_scene.RegionInfo.SimUUID)
                            {
                                TeleportLocalPacket tpLocal = new TeleportLocalPacket();

                                tpLocal.Info.AgentID = tpReq.Info.AgentID;
                                tpLocal.Info.TeleportFlags = 8;  // Teleport via landmark
                                tpLocal.Info.LocationID = 2;
                                tpLocal.Info.Position = lm.Position;
                                OutPacket(tpLocal);
                            }
                            else
                            {
                                TeleportCancelPacket tpCancel = new TeleportCancelPacket();
                                tpCancel.Info.AgentID = tpReq.Info.AgentID;
                                tpCancel.Info.SessionID = tpReq.Info.SessionID;
                                OutPacket(tpCancel);
                            }
                        }
                        else
                        {
                            Console.WriteLine("Cancelling Teleport - fetch asset not yet implemented");

                            TeleportCancelPacket tpCancel = new TeleportCancelPacket();
                            tpCancel.Info.AgentID = tpReq.Info.AgentID;
                            tpCancel.Info.SessionID = tpReq.Info.SessionID;
                            OutPacket(tpCancel);
                        }
                        break;
                    case PacketType.TeleportLocationRequest:
                        TeleportLocationRequestPacket tpLocReq = (TeleportLocationRequestPacket)Pack;
                        // Console.WriteLine(tpLocReq.ToString());

                        if (OnTeleportLocationRequest != null)
                        {
                            OnTeleportLocationRequest(this, tpLocReq.Info.RegionHandle, tpLocReq.Info.Position, tpLocReq.Info.LookAt, 16);
                        }
                        else
                        {
                            //no event handler so cancel request
                            TeleportCancelPacket tpCancel = new TeleportCancelPacket();
                            tpCancel.Info.SessionID = tpLocReq.AgentData.SessionID;
                            tpCancel.Info.AgentID = tpLocReq.AgentData.AgentID;
                            OutPacket(tpCancel);
                        }
                        break;
                    #endregion

                    case PacketType.MoneyBalanceRequest:
                        SendMoneyBalance(LLUUID.Zero, true, new byte[0], MoneyBalance);
                        break;
                    case PacketType.UUIDNameRequest:
                        UUIDNameRequestPacket incoming = (UUIDNameRequestPacket)Pack;
                        foreach (UUIDNameRequestPacket.UUIDNameBlockBlock UUIDBlock in incoming.UUIDNameBlock)
                        {
                            OnNameFromUUIDRequest(UUIDBlock.ID, this);
                        }
                        break;
                    #region Parcel related packets
                    case PacketType.ParcelPropertiesRequest:
                        ParcelPropertiesRequestPacket propertiesRequest = (ParcelPropertiesRequestPacket)Pack;
                        if (OnParcelPropertiesRequest != null)
                        {
                            OnParcelPropertiesRequest((int)Math.Round(propertiesRequest.ParcelData.West), (int)Math.Round(propertiesRequest.ParcelData.South), (int)Math.Round(propertiesRequest.ParcelData.East), (int)Math.Round(propertiesRequest.ParcelData.North), propertiesRequest.ParcelData.SequenceID, propertiesRequest.ParcelData.SnapSelection, this);
                        }
                        break;
                    case PacketType.ParcelDivide:
                        ParcelDividePacket landDivide = (ParcelDividePacket)Pack;
                        if (OnParcelDivideRequest != null)
                        {
                            OnParcelDivideRequest((int)Math.Round(landDivide.ParcelData.West), (int)Math.Round(landDivide.ParcelData.South), (int)Math.Round(landDivide.ParcelData.East), (int)Math.Round(landDivide.ParcelData.North), this);
                        }
                        break;
                    case PacketType.ParcelJoin:
                        ParcelJoinPacket landJoin = (ParcelJoinPacket)Pack;
                        if (OnParcelJoinRequest != null)
                        {
                            OnParcelJoinRequest((int)Math.Round(landJoin.ParcelData.West), (int)Math.Round(landJoin.ParcelData.South), (int)Math.Round(landJoin.ParcelData.East), (int)Math.Round(landJoin.ParcelData.North), this);
                        }
                        break;
                    case PacketType.ParcelPropertiesUpdate:
                        ParcelPropertiesUpdatePacket updatePacket = (ParcelPropertiesUpdatePacket)Pack;
                        if (OnParcelPropertiesUpdateRequest != null)
                        {
                            OnParcelPropertiesUpdateRequest(updatePacket, this);

                        }
                        break;
                    case PacketType.ParcelSelectObjects:
                        ParcelSelectObjectsPacket selectPacket = (ParcelSelectObjectsPacket)Pack;
                        if (OnParcelSelectObjects != null)
                        {
                            OnParcelSelectObjects(selectPacket.ParcelData.LocalID, Convert.ToInt32(selectPacket.ParcelData.ReturnType), this);
                        }
                        break;

                    case PacketType.ParcelObjectOwnersRequest:
                        ParcelObjectOwnersRequestPacket reqPacket = (ParcelObjectOwnersRequestPacket)Pack;
                        if (OnParcelObjectOwnerRequest != null)
                        {
                            OnParcelObjectOwnerRequest(reqPacket.ParcelData.LocalID, this);
                        }
                        break;
                    #endregion

                    #region Estate Packets
                    case PacketType.EstateOwnerMessage:
                        EstateOwnerMessagePacket messagePacket = (EstateOwnerMessagePacket)Pack;
                        if (OnEstateOwnerMessage != null)
                        {
                            OnEstateOwnerMessage(messagePacket, this);
                        }
                        break;
                    #endregion

                    #region unimplemented handlers
                    case PacketType.AgentIsNowWearing:
                        // AgentIsNowWearingPacket wear = (AgentIsNowWearingPacket)Pack;
                        //Console.WriteLine(Pack.ToString());
                        break;
                    case PacketType.ObjectScale:
                        //OpenSim.Framework.Console.MainLog.Instance.Verbose( Pack.ToString());
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
    }
}
