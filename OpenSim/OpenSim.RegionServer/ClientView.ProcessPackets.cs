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
using OpenSim.world;
using OpenSim.RegionServer.world;
using OpenSim.Assets;

namespace OpenSim
{
    public partial class ClientView
    {
        public delegate void GenericCall(ClientView remoteClient);
        public delegate void GenericCall2();
        public delegate void GenericCall3(Packet packet); // really don't want to be passing packets in these events, so this is very temporary.
        public delegate void GenericCall4(Packet packet, ClientView remoteClient);
        public delegate void UpdateShape(uint localID, ObjectShapePacket.ObjectDataBlock shapeBlock);
        public delegate void ObjectSelect(uint localID, ClientView remoteClient);
        public delegate void UpdatePrimFlags(uint localID, Packet packet, ClientView remoteClient);
        public delegate void UpdatePrimTexture(uint localID, byte[] texture, ClientView remoteClient);
        public delegate void UpdatePrimVector(uint localID, LLVector3 pos, ClientView remoteClient);
        public delegate void UpdatePrimRotation(uint localID, LLQuaternion rot, ClientView remoteClient);
        public delegate void StatusChange(bool status);


        public event ChatFromViewer OnChatFromViewer;
        public event RezObject OnRezObject;
        public event GenericCall4 OnDeRezObject;
        public event ModifyTerrain OnModifyTerrain;
        public event GenericCall OnRegionHandShakeReply;
        public event GenericCall OnRequestWearables;
        public event SetAppearance OnSetAppearance;
        public event GenericCall2 OnCompleteMovementToRegion;
        public event GenericCall3 OnAgentUpdate;
        public event StartAnim OnStartAnim;
        public event GenericCall OnRequestAvatarsData;
        public event LinkObjects OnLinkObjects;
        public event GenericCall4 OnAddPrim;
        public event UpdateShape OnUpdatePrimShape;
        public event ObjectSelect OnObjectSelect;
        public event UpdatePrimFlags OnUpdatePrimFlags;
        public event UpdatePrimTexture OnUpdatePrimTexture;
        public event UpdatePrimVector OnUpdatePrimPosition;
        public event UpdatePrimRotation OnUpdatePrimRotation;
        public event UpdatePrimVector OnUpdatePrimScale;
        public event StatusChange OnChildAgentStatus;
        public event ParcelPropertiesRequest OnParcelPropertiesRequest;

        protected override void ProcessInPacket(Packet Pack)
        {
            ack_pack(Pack);
            if (debug)
            {
                if (Pack.Type != PacketType.AgentUpdate)
                {
                    Console.WriteLine("IN: " + Pack.Type.ToString());
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

                    #region New Event System - World/Avatar
                    case PacketType.ChatFromViewer:
                        ChatFromViewerPacket inchatpack = (ChatFromViewerPacket)Pack;
                        if (Util.FieldToString(inchatpack.ChatData.Message) == "")
                        {
                            //empty message so don't bother with it
                            break;
                        }
                        string fromName = ClientAvatar.firstname + " " + ClientAvatar.lastname;
                        byte[] message = inchatpack.ChatData.Message;
                        byte type = inchatpack.ChatData.Type;
                        LLVector3 fromPos = ClientAvatar.Pos;
                        LLUUID fromAgentID = AgentID;
                        this.OnChatFromViewer(message, type, fromPos, fromName, fromAgentID);
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
                                    this.OnRezObject(asset, rezPacket.RezData.RayEnd);
                                    this.m_inventoryCache.DeleteInventoryItem(this, rezPacket.InventoryData.ItemID);
                                }
                            }
                        }
                        break;
                    case PacketType.DeRezObject:
                        OnDeRezObject(Pack, this);
                        break;
                    case PacketType.ModifyLand:
                        ModifyLandPacket modify = (ModifyLandPacket)Pack;
                        if (modify.ParcelData.Length > 0)
                        {
                            OnModifyTerrain(modify.ModifyBlock.Action, modify.ParcelData[0].North, modify.ParcelData[0].West);
                        }
                        break;
                    case PacketType.RegionHandshakeReply:
                        OnRegionHandShakeReply(this);
                        break;
                    case PacketType.AgentWearablesRequest:
                        OnRequestWearables(this);
                        OnRequestAvatarsData(this);
                        break;
                    case PacketType.AgentSetAppearance:
                        AgentSetAppearancePacket appear = (AgentSetAppearancePacket)Pack;
                        OnSetAppearance(appear.ObjectData.TextureEntry, appear.VisualParam);
                        break;
                    case PacketType.CompleteAgentMovement:
                        if (this.m_child) this.UpgradeClient();
                        OnCompleteMovementToRegion();
                        this.EnableNeighbours();
                        break;
                    case PacketType.AgentUpdate:
                        OnAgentUpdate(Pack);
                        break;
                    case PacketType.AgentAnimation:
                        if (!m_child)
                        {
                            AgentAnimationPacket AgentAni = (AgentAnimationPacket)Pack;
                            for (int i = 0; i < AgentAni.AnimationList.Length; i++)
                            {
                                if (AgentAni.AnimationList[i].StartAnim)
                                {
                                    OnStartAnim(AgentAni.AnimationList[i].AnimID, 1);
                                }
                            }
                        }
                        break;

                    #endregion

                    #region New Event System - Objects/Prims
                    case PacketType.ObjectLink:
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
                        OnLinkObjects(parentprimid, childrenprims);
                        break;
                    case PacketType.ObjectAdd:
                        m_world.AddNewPrim((ObjectAddPacket)Pack, this);
                        OnAddPrim(Pack, this);
                        break;
                    case PacketType.ObjectShape:
                        ObjectShapePacket shape = (ObjectShapePacket)Pack;
                        for (int i = 0; i < shape.ObjectData.Length; i++)
                        {
                            OnUpdatePrimShape(shape.ObjectData[i].ObjectLocalID, shape.ObjectData[i]);
                        }
                        break;
                    case PacketType.ObjectSelect:
                        ObjectSelectPacket incomingselect = (ObjectSelectPacket)Pack;
                        for (int i = 0; i < incomingselect.ObjectData.Length; i++)
                        {
                            OnObjectSelect(incomingselect.ObjectData[i].ObjectLocalID, this);
                        }
                        break;
                    case PacketType.ObjectFlagUpdate:
                        ObjectFlagUpdatePacket flags = (ObjectFlagUpdatePacket)Pack;
                        OnUpdatePrimFlags(flags.AgentData.ObjectLocalID, Pack, this);
                        break;
                    case PacketType.ObjectImage:
                        ObjectImagePacket imagePack = (ObjectImagePacket)Pack;
                        for (int i = 0; i < imagePack.ObjectData.Length; i++)
                        {
                            OnUpdatePrimTexture(imagePack.ObjectData[i].ObjectLocalID, imagePack.ObjectData[i].TextureEntry, this);

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
                        TransferRequestPacket transfer = (TransferRequestPacket)Pack;
                        m_assetCache.AddAssetRequest(this, transfer);
                        break;
                    case PacketType.AssetUploadRequest:
                        AssetUploadRequestPacket request = (AssetUploadRequestPacket)Pack;
                        this.UploadAssets.HandleUploadPacket(request, request.AssetBlock.TransactionID.Combine(this.SecureSessionID));
                        break;
                    case PacketType.RequestXfer:
                        break;
                    case PacketType.SendXferPacket:
                        this.UploadAssets.HandleXferPacket((SendXferPacketPacket)Pack);
                        break;
                    case PacketType.CreateInventoryFolder:
                        CreateInventoryFolderPacket invFolder = (CreateInventoryFolderPacket)Pack;
                        m_inventoryCache.CreateNewInventoryFolder(this, invFolder.FolderData.FolderID, (ushort)invFolder.FolderData.Type, Util.FieldToString(invFolder.FolderData.Name), invFolder.FolderData.ParentID);
                        break;
                    case PacketType.CreateInventoryItem:
                        CreateInventoryItemPacket createItem = (CreateInventoryItemPacket)Pack;
                        if (createItem.InventoryBlock.TransactionID != LLUUID.Zero)
                        {
                            this.UploadAssets.CreateInventoryItem(createItem);
                        }
                        else
                        {
                            this.CreateInventoryItem(createItem);
                        }
                        break;
                    case PacketType.FetchInventory:
                        FetchInventoryPacket FetchInventory = (FetchInventoryPacket)Pack;
                        m_inventoryCache.FetchInventory(this, FetchInventory);
                        break;
                    case PacketType.FetchInventoryDescendents:
                        FetchInventoryDescendentsPacket Fetch = (FetchInventoryDescendentsPacket)Pack;
                        m_inventoryCache.FetchInventoryDescendents(this, Fetch);
                        break;
                    case PacketType.UpdateInventoryItem:
                        UpdateInventoryItemPacket update = (UpdateInventoryItemPacket)Pack;
                        for (int i = 0; i < update.InventoryData.Length; i++)
                        {
                            if (update.InventoryData[i].TransactionID != LLUUID.Zero)
                            {
                                AssetBase asset = m_assetCache.GetAsset(update.InventoryData[i].TransactionID.Combine(this.SecureSessionID));
                                if (asset != null)
                                {
                                    m_inventoryCache.UpdateInventoryItemAsset(this, update.InventoryData[i].ItemID, asset);
                                }
                                else
                                {
                                    asset = this.UploadAssets.AddUploadToAssetCache(update.InventoryData[i].TransactionID);
                                    if (asset != null)
                                    {
                                        m_inventoryCache.UpdateInventoryItemAsset(this, update.InventoryData[i].ItemID, asset);
                                    }
                                    else
                                    {

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
                        RequestTaskInventoryPacket requesttask = (RequestTaskInventoryPacket)Pack;
                        ReplyTaskInventoryPacket replytask = new ReplyTaskInventoryPacket();
                        bool foundent = false;
                        foreach (Entity ent in m_world.Entities.Values)
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
                        }
                        break;
                    case PacketType.UpdateTaskInventory:
                        UpdateTaskInventoryPacket updatetask = (UpdateTaskInventoryPacket)Pack;
                        AgentInventory myinventory = this.m_inventoryCache.GetAgentsInventory(this.AgentID);
                        if (myinventory != null)
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
                        }
                        break;
                    case PacketType.MapLayerRequest:
                        // This be busted.
                        MapLayerRequestPacket MapRequest = (MapLayerRequestPacket)Pack;
                        this.RequestMapLayer();
                        this.RequestMapBlocks((int)this.m_regionData.RegionLocX - 5, (int)this.m_regionData.RegionLocY - 5, (int)this.m_regionData.RegionLocX + 5, (int)this.m_regionData.RegionLocY + 5);
                        break;

                    case PacketType.MapBlockRequest:
                        MapBlockRequestPacket MapBRequest = (MapBlockRequestPacket)Pack;
                        this.RequestMapBlocks(MapBRequest.PositionData.MinX, MapBRequest.PositionData.MinY, MapBRequest.PositionData.MaxX, MapBRequest.PositionData.MaxY);
                        break;

                    case PacketType.MapNameRequest:
                        // TODO.
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

                    #region Parcel Packets
                    case PacketType.ParcelPropertiesRequest:
                        ParcelPropertiesRequestPacket propertiesRequest = (ParcelPropertiesRequestPacket)Pack;
                        OnParcelPropertiesRequest((int)Math.Round(propertiesRequest.ParcelData.West), (int)Math.Round(propertiesRequest.ParcelData.South), (int)Math.Round(propertiesRequest.ParcelData.East), (int)Math.Round(propertiesRequest.ParcelData.North),propertiesRequest.ParcelData.SequenceID,propertiesRequest.ParcelData.SnapSelection, this);
                        break;
                    #endregion

                    #region unimplemented handlers
                    case PacketType.AgentIsNowWearing:
                        // AgentIsNowWearingPacket wear = (AgentIsNowWearingPacket)Pack;
                        break;
                    case PacketType.ObjectScale:
                        break;
                    case PacketType.MoneyBalanceRequest:
                        //This need to be actually done and not thrown back with fake info
                        MoneyBalanceRequestPacket incoming = (MoneyBalanceRequestPacket)Pack;
                        MoneyBalanceReplyPacket outgoing = new MoneyBalanceReplyPacket();
                        outgoing.MoneyData.AgentID = incoming.AgentData.AgentID;
                        outgoing.MoneyData.MoneyBalance = 31337;
                        outgoing.MoneyData.SquareMetersCommitted = 0;
                        outgoing.MoneyData.SquareMetersCredit = 100000000;
                        outgoing.MoneyData.TransactionID = incoming.MoneyData.TransactionID;
                        outgoing.MoneyData.TransactionSuccess = true;
                        outgoing.MoneyData.Description = libsecondlife.Helpers.StringToField("");
                        this.OutPacket((Packet)outgoing);
                        OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Sent Temporary Money packet (they have leet monies)");

                        break;

                    case PacketType.EstateCovenantRequest:
                        //This should be actually done and not thrown back with fake info
                        EstateCovenantRequestPacket estateCovenantRequest = (EstateCovenantRequestPacket)Pack;
                        EstateCovenantReplyPacket estateCovenantReply = new EstateCovenantReplyPacket();
                        estateCovenantReply.Data.EstateName = libsecondlife.Helpers.StringToField("Leet Estate");
                        estateCovenantReply.Data.EstateOwnerID = LLUUID.Zero;
                        estateCovenantReply.Data.CovenantID = LLUUID.Zero;
                        estateCovenantReply.Data.CovenantTimestamp = (uint)0;
                        this.OutPacket((Packet)estateCovenantReply);
                        OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Sent Temporary Estate packet (they are in leet estate)");
                        break;
                    #endregion
                }
            }
        }
    }
}
