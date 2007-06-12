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
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using Nwc.XmlRpc;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Timers;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Utilities;
using OpenSim.Assets;

namespace OpenSim
{
    public partial class ClientView
    {
        protected override void ProcessInPacket(Packet Pack)
        {
            ack_pack(Pack);
            if (debug)
            {
                if (Pack.Type != PacketType.AgentUpdate)
                {
                    Console.WriteLine(Pack.Type.ToString());
                }
            }

            if (this.ProcessPacketMethod(Pack))
            {
                //there is a handler registered that handled this packet type 
                return;
            }
            else
            {
                System.Text.Encoding _enc = System.Text.Encoding.ASCII;
               
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

                    #region  World/Avatar
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
                    case PacketType.RezObject:
                        RezObjectPacket rezPacket = (RezObjectPacket)Pack;
                        AgentInventory inven = this.m_inventoryCache.GetAgentsInventory(this.AgentID);
                        if (inven != null)
                        {
                            if (inven.InventoryItems.ContainsKey(rezPacket.InventoryData.ItemID))
                            {
                                AssetBase asset = this.m_assetCache.GetAsset(inven.InventoryItems[rezPacket.InventoryData.ItemID].AssetID);
                                if (asset != null)
                                {
                                    if (OnRezObject != null)
                                    {
                                        this.OnRezObject(asset, rezPacket.RezData.RayEnd);
                                        this.m_inventoryCache.DeleteInventoryItem(this, rezPacket.InventoryData.ItemID);
                                    }
                                }
                            }
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
                                OnModifyTerrain(modify.ModifyBlock.Action, modify.ParcelData[0].North, modify.ParcelData[0].West);
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
                        if (this.m_child) this.UpgradeClient();
                        if (OnCompleteMovementToRegion != null)
                        {
                            OnCompleteMovementToRegion();
                        }
                       // this.EnableNeighbours();
                        break;
                    case PacketType.AgentUpdate:
                        if (OnAgentUpdate != null)
                        {
                            OnAgentUpdate(Pack);
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
                                        OnStartAnim(AgentAni.AnimationList[i].AnimID, 1);
                                    }
                                }
                            }
                        }
                        break;

                    #endregion

                    #region Objects/Prims
                    case PacketType.ObjectLink:
                        // OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, Pack.ToString());
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
                       // m_world.AddNewPrim((ObjectAddPacket)Pack, this);
                        if (OnAddPrim != null)
                        {
                            OnAddPrim(Pack, this);
                        }
                        break;
                    case PacketType.ObjectShape:
                        ObjectShapePacket shape = (ObjectShapePacket)Pack;
                        for (int i = 0; i < shape.ObjectData.Length; i++)
                        {
                            if (OnUpdatePrimShape != null)
                            {
                                OnUpdatePrimShape(shape.ObjectData[i].ObjectLocalID, shape.ObjectData[i]);
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
                    #endregion      
                   
                    #region Inventory/Asset/Other related packets
                    case PacketType.RequestImage:
                        RequestImagePacket imageRequest = (RequestImagePacket)Pack;
                        for (int i = 0; i < imageRequest.RequestImage.Length; i++)
                        {
                            m_assetCache.AddTextureRequest(this, imageRequest.RequestImage[i].Image);
                        }
                        break;
                    case PacketType.TransferRequest:
                        //Console.WriteLine("OpenSimClient.cs:ProcessInPacket() - Got transfer request");
                        TransferRequestPacket transfer = (TransferRequestPacket)Pack;
                        m_assetCache.AddAssetRequest(this, transfer);
                        break;
                    case PacketType.AssetUploadRequest:
                        AssetUploadRequestPacket request = (AssetUploadRequestPacket)Pack;
                        this.UploadAssets.HandleUploadPacket(request, request.AssetBlock.TransactionID.Combine(this.SecureSessionID));
                        break;
                    case PacketType.RequestXfer:
                        //Console.WriteLine(Pack.ToString());
                        break;
                    case PacketType.SendXferPacket:
                        this.UploadAssets.HandleXferPacket((SendXferPacketPacket)Pack);
                        break;
                    case PacketType.CreateInventoryFolder:
                        CreateInventoryFolderPacket invFolder = (CreateInventoryFolderPacket)Pack;
                        m_inventoryCache.CreateNewInventoryFolder(this, invFolder.FolderData.FolderID, (ushort)invFolder.FolderData.Type, Util.FieldToString(invFolder.FolderData.Name), invFolder.FolderData.ParentID);
                        //Console.WriteLine(Pack.ToString());
                        break;
                    case PacketType.CreateInventoryItem:
                        //Console.WriteLine(Pack.ToString());
                        CreateInventoryItemPacket createItem = (CreateInventoryItemPacket)Pack;
                        if (createItem.InventoryBlock.TransactionID != LLUUID.Zero)
                        {
                            this.UploadAssets.CreateInventoryItem(createItem);
                        }
                        else
                        {
                            // Console.Write(Pack.ToString());
                            this.CreateInventoryItem(createItem);
                        }
                        break;
                    case PacketType.FetchInventory:
                        //Console.WriteLine("fetch item packet");
                        FetchInventoryPacket FetchInventory = (FetchInventoryPacket)Pack;
                        m_inventoryCache.FetchInventory(this, FetchInventory);
                        break;
                    case PacketType.FetchInventoryDescendents:
                        FetchInventoryDescendentsPacket Fetch = (FetchInventoryDescendentsPacket)Pack;
                        m_inventoryCache.FetchInventoryDescendents(this, Fetch);
                        break;
                    case PacketType.UpdateInventoryItem:
                        UpdateInventoryItemPacket update = (UpdateInventoryItemPacket)Pack;
                        //Console.WriteLine(Pack.ToString());
                        for (int i = 0; i < update.InventoryData.Length; i++)
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
                        }
                        break;
                    case PacketType.RequestTaskInventory:
                        // Console.WriteLine(Pack.ToString());
                        RequestTaskInventoryPacket requesttask = (RequestTaskInventoryPacket)Pack;
                        ReplyTaskInventoryPacket replytask = new ReplyTaskInventoryPacket();
                        bool foundent = false;
                       /* foreach (Entity ent in m_world.Entities.Values)
                        {
                            if (ent.localid == requesttask.InventoryData.LocalID)
                            {
                                replytask.InventoryData.TaskID = ent.uuid;
                                replytask.InventoryData.Serial = 0;
                                replytask.InventoryData.Filename = new byte[0];
                                foundent = true;
                            }
                        }
                        if (foundent)
                        {
                            this.OutPacket(replytask);
                        }*/
                        break;
                    case PacketType.UpdateTaskInventory:
                        // Console.WriteLine(Pack.ToString());
                        UpdateTaskInventoryPacket updatetask = (UpdateTaskInventoryPacket)Pack;
                        AgentInventory myinventory = this.m_inventoryCache.GetAgentsInventory(this.AgentID);
                        /*if (myinventory != null)
                        {
                            if (updatetask.UpdateData.Key == 0)
                            {
                                if (myinventory.InventoryItems[updatetask.InventoryData.ItemID] != null)
                                {
                                    if (myinventory.InventoryItems[updatetask.InventoryData.ItemID].Type == 7)
                                    {
                                        LLUUID noteaid = myinventory.InventoryItems[updatetask.InventoryData.ItemID].AssetID;
                                        AssetBase assBase = this.m_assetCache.GetAsset(noteaid);
                                        if (assBase != null)
                                        {
                                            foreach (Entity ent in m_world.Entities.Values)
                                            {
                                                if (ent.localid == updatetask.UpdateData.LocalID)
                                                {
                                                    if (ent is OpenSim.world.Primitive)
                                                    {
                                                        this.m_world.AddScript(ent, Util.FieldToString(assBase.Data));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }*/
                        break;
                    case PacketType.MapLayerRequest:
                        this.RequestMapLayer();
                        break;
                    case PacketType.MapBlockRequest:
                        MapBlockRequestPacket MapRequest = (MapBlockRequestPacket)Pack;

                        this.RequestMapBlocks(MapRequest.PositionData.MinX, MapRequest.PositionData.MinY, MapRequest.PositionData.MaxX, MapRequest.PositionData.MaxY);
                        break;
                    case PacketType.TeleportLandmarkRequest:
                        TeleportLandmarkRequestPacket tpReq = (TeleportLandmarkRequestPacket)Pack;

                        TeleportStartPacket tpStart = new TeleportStartPacket();
                        tpStart.Info.TeleportFlags = 8; // tp via lm
                        this.OutPacket(tpStart);

                        TeleportProgressPacket tpProgress = new TeleportProgressPacket();
                        tpProgress.Info.Message = (new System.Text.ASCIIEncoding()).GetBytes("sending_landmark");
                        tpProgress.Info.TeleportFlags = 8;
                        tpProgress.AgentData.AgentID = tpReq.Info.AgentID;
                        this.OutPacket(tpProgress);

                        // Fetch landmark
                        LLUUID lmid = tpReq.Info.LandmarkID;
                        AssetBase lma = this.m_assetCache.GetAsset(lmid);
                        if (lma != null)
                        {
                            AssetLandmark lm = new AssetLandmark(lma);

                            if (lm.RegionID == m_regionData.SimUUID)
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
                        Console.WriteLine(tpLocReq.ToString());

                        tpStart = new TeleportStartPacket();
                        tpStart.Info.TeleportFlags = 16; // Teleport via location
                        Console.WriteLine(tpStart.ToString());
                        OutPacket(tpStart);

                        if (m_regionData.RegionHandle != tpLocReq.Info.RegionHandle)
                        {
                            /* m_gridServer.getRegion(tpLocReq.Info.RegionHandle); */
                            Console.WriteLine("Inter-sim teleport not yet implemented");
                            TeleportCancelPacket tpCancel = new TeleportCancelPacket();
                            tpCancel.Info.SessionID = tpLocReq.AgentData.SessionID;
                            tpCancel.Info.AgentID = tpLocReq.AgentData.AgentID;

                            OutPacket(tpCancel);
                        }
                        else
                        {
                            Console.WriteLine("Local teleport");
                            TeleportLocalPacket tpLocal = new TeleportLocalPacket();
                            tpLocal.Info.AgentID = tpLocReq.AgentData.AgentID;
                            tpLocal.Info.TeleportFlags = tpStart.Info.TeleportFlags;
                            tpLocal.Info.LocationID = 2;
                            tpLocal.Info.LookAt = tpLocReq.Info.LookAt;
                            tpLocal.Info.Position = tpLocReq.Info.Position;
                            OutPacket(tpLocal);

                        }
                        break;
                    #endregion

                    #region Parcel related packets
                    case PacketType.ParcelPropertiesRequest:
                        ParcelPropertiesRequestPacket propertiesRequest = (ParcelPropertiesRequestPacket)Pack;
                        if(OnParcelPropertiesRequest != null)
                        {
                        OnParcelPropertiesRequest((int)Math.Round(propertiesRequest.ParcelData.West), (int)Math.Round(propertiesRequest.ParcelData.South), (int)Math.Round(propertiesRequest.ParcelData.East), (int)Math.Round(propertiesRequest.ParcelData.North), propertiesRequest.ParcelData.SequenceID, propertiesRequest.ParcelData.SnapSelection, this);
                        }
                        break;
                    case PacketType.ParcelDivide:
                        ParcelDividePacket parcelDivide = (ParcelDividePacket)Pack;
                        if (OnParcelDivideRequest != null)
                        {
                            OnParcelDivideRequest((int)Math.Round(parcelDivide.ParcelData.West), (int)Math.Round(parcelDivide.ParcelData.South), (int)Math.Round(parcelDivide.ParcelData.East), (int)Math.Round(parcelDivide.ParcelData.North), this);
                        }
                        break;
                    case PacketType.ParcelJoin:
                        ParcelJoinPacket parcelJoin = (ParcelJoinPacket)Pack;
                        if (OnParcelJoinRequest != null)
                        {
                            OnParcelJoinRequest((int)Math.Round(parcelJoin.ParcelData.West), (int)Math.Round(parcelJoin.ParcelData.South), (int)Math.Round(parcelJoin.ParcelData.East), (int)Math.Round(parcelJoin.ParcelData.North), this);
                        }
                        break;
                    case PacketType.ParcelPropertiesUpdate:
                        ParcelPropertiesUpdatePacket updatePacket = (ParcelPropertiesUpdatePacket)Pack;
                        if (OnParcelPropertiesUpdateRequest != null)
                        {
                            OnParcelPropertiesUpdateRequest(updatePacket, this);
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
                        //OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, Pack.ToString());
                        break;
                    #endregion
                }
            }
        }
    }
}
