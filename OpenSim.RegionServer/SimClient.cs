/*
Copyright (c) OpenSim project, http://osgrid.org/
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
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
using OpenSim.Framework.Assets;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Utilities;
using OpenSim.world;
using OpenSim.Assets;

namespace OpenSim
{
    public delegate bool PacketMethod(SimClient simClient, Packet packet);

    /// <summary>
    /// Handles new client connections
    /// Constructor takes a single Packet and authenticates everything
    /// </summary>
    public class SimClient
    {
        public LLUUID AgentID;
        public LLUUID SessionID;
        public LLUUID SecureSessionID = LLUUID.Zero;
	public bool m_child;
        public uint CircuitCode;
        public world.Avatar ClientAvatar;
        private UseCircuitCodePacket cirpack;
        public Thread ClientThread;
        public EndPoint userEP;
	public LLVector3 startpos;
        private BlockingQueue<QueItem> PacketQueue;
        private Dictionary<uint, uint> PendingAcks = new Dictionary<uint, uint>();
        private Dictionary<uint, Packet> NeedAck = new Dictionary<uint, Packet>();
        //private Dictionary<LLUUID, AssetBase> UploadedAssets = new Dictionary<LLUUID, AssetBase>();
        private System.Timers.Timer AckTimer;
        private uint Sequence = 0;
        private object SequenceLock = new object();
        private const int MAX_APPENDED_ACKS = 10;
        private const int RESEND_TIMEOUT = 4000;
        private const int MAX_SEQUENCE = 0xFFFFFF;
        private AgentAssetUpload UploadAssets;
        private LLUUID newAssetFolder = LLUUID.Zero;
        private bool debug = false;
        private World m_world;
        private Dictionary<uint, SimClient> m_clientThreads;
        private AssetCache m_assetCache;
        private IGridServer m_gridServer;
        private IUserServer m_userServer = null;
        private OpenSimNetworkHandler m_application;
        private InventoryCache m_inventoryCache;
        private bool m_sandboxMode;
        private int cachedtextureserial = 0;

        protected static Dictionary<PacketType, PacketMethod> PacketHandlers = new Dictionary<PacketType, PacketMethod>(); //Global/static handlers for all clients

        protected Dictionary<PacketType, PacketMethod> m_packetHandlers = new Dictionary<PacketType, PacketMethod>(); //local handlers for this instance 

        public IUserServer UserServer
        {
            set
            {
                this.m_userServer = value;
            }
        }

        public SimClient(EndPoint remoteEP, UseCircuitCodePacket initialcirpack, World world, Dictionary<uint, SimClient> clientThreads, AssetCache assetCache, IGridServer gridServer, OpenSimNetworkHandler application, InventoryCache inventoryCache, bool sandboxMode, bool child)
        {
            m_world = world;
            m_clientThreads = clientThreads;
            m_assetCache = assetCache;
            m_gridServer = gridServer;
            m_application = application;
            m_inventoryCache = inventoryCache;
            m_sandboxMode = sandboxMode;
	    m_child=child;

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenSimClient.cs - Started up new client thread to handle incoming request");
            cirpack = initialcirpack;
            userEP = remoteEP;
	    if(m_gridServer.GetName() == "Remote") {
		this.startpos=((RemoteGridBase)m_gridServer).agentcircuits[initialcirpack.CircuitCode.Code].startpos;
	    } else {
		this.startpos=new LLVector3(128.0f,128.0f,60f);
	    }
            PacketQueue = new BlockingQueue<QueItem>();

            this.UploadAssets = new AgentAssetUpload(this, m_assetCache, m_inventoryCache);
            AckTimer = new System.Timers.Timer(500);
            AckTimer.Elapsed += new ElapsedEventHandler(AckTimer_Elapsed);
            AckTimer.Start();

            this.RegisterLocalPacketHandlers();

            ClientThread = new Thread(new ThreadStart(AuthUser));
            ClientThread.IsBackground = true;
            ClientThread.Start();
        }

        protected virtual void RegisterLocalPacketHandlers()
        {
            this.AddLocalPacketHandler(PacketType.LogoutRequest, this.Logout);
            this.AddLocalPacketHandler(PacketType.AgentCachedTexture, this.AgentTextureCached);
            this.AddLocalPacketHandler(PacketType.MultipleObjectUpdate, this.MultipleObjUpdate);
        }

	public void CrossSimBorder(LLVector3 avatarpos) {		// VERY VERY BASIC
	    LLVector3 newpos = avatarpos;
	    uint neighbourx=((OpenSimMain)m_application).regionData.RegionLocX;
	    uint neighboury=((OpenSimMain)m_application).regionData.RegionLocY;

	    if(avatarpos.X<0) {
		neighbourx-=1;
		newpos.X=254;
	    }
	    if(avatarpos.X>255) {
		neighbourx+=1;
		newpos.X=1;
	    }
            if(avatarpos.Y<0) { 
		neighboury-=1;
		newpos.Y=254;
	    }
	    if(avatarpos.Y>255) {
		neighbourx+=1;
		newpos.Y=1;
	    }
	    OpenSim.Framework.Console.MainConsole.Instance.WriteLine("SimClient.cs:CrossSimBorder() - Crossing border to neighbouring sim at [" + neighbourx.ToString() + "," + neighboury.ToString() + "]");

	    Hashtable SimParams;
	    ArrayList SendParams;
	    XmlRpcRequest GridReq;
	    XmlRpcResponse GridResp;
	    foreach(Hashtable borderingSim in ((RemoteGridBase)m_gridServer).neighbours) {
		if(((string)borderingSim["region_locx"]).Equals(neighbourx.ToString()) && ((string)borderingSim["region_locy"]).Equals(neighboury.ToString())) {
			SimParams = new Hashtable();
                        SimParams["firstname"] = this.ClientAvatar.firstname;
                        SimParams["lastname"] = this.ClientAvatar.lastname;
                        SimParams["circuit_code"] = this.CircuitCode.ToString();
			SimParams["pos_x"] = newpos.X.ToString();
			SimParams["pos_y"] = newpos.Y.ToString();
			SimParams["pos_z"] = newpos.Z.ToString();
                        SendParams = new ArrayList();
                        SendParams.Add(SimParams);

                        GridReq = new XmlRpcRequest("agent_crossing", SendParams);
                        GridResp = GridReq.Send("http://" + borderingSim["sim_ip"] + ":" + borderingSim["sim_port"], 3000);

			CrossedRegionPacket NewSimPack = new CrossedRegionPacket();
			NewSimPack.AgentData = new CrossedRegionPacket.AgentDataBlock();
			NewSimPack.AgentData.AgentID=this.AgentID;
			NewSimPack.AgentData.SessionID=this.SessionID;
			NewSimPack.Info = new CrossedRegionPacket.InfoBlock();
			NewSimPack.Info.Position=newpos;
			NewSimPack.Info.LookAt=new LLVector3(0.99f, 0.042f, 0);	// copied from Avatar.cs - SHOULD BE DYNAMIC!!!!!!!!!!
			NewSimPack.RegionData = new libsecondlife.Packets.CrossedRegionPacket.RegionDataBlock();
			NewSimPack.RegionData.RegionHandle=Helpers.UIntsToLong((uint)(Convert.ToInt32(borderingSim["region_locx"]) * 256), (uint)(Convert.ToInt32(borderingSim["region_locy"]) * 256));

                        System.Net.IPAddress neighbourIP = System.Net.IPAddress.Parse((string)borderingSim["sim_ip"]);
                        byte[] byteIP = neighbourIP.GetAddressBytes();
                        NewSimPack.RegionData.SimIP = (uint)byteIP[3] << 24;
                        NewSimPack.RegionData.SimIP += (uint)byteIP[2] << 16;
                        NewSimPack.RegionData.SimIP += (uint)byteIP[1] << 8;
                        NewSimPack.RegionData.SimIP += (uint)byteIP[0];
                        NewSimPack.RegionData.SimPort = (ushort)Convert.ToInt32(borderingSim["sim_port"]);
			NewSimPack.RegionData.SeedCapability = new byte[0];
			lock(PacketQueue) {
				ProcessOutPacket(NewSimPack);
				DowngradeClient();
			}
		}
	    }
	}

	public void UpgradeClient() {
	        OpenSim.Framework.Console.MainConsole.Instance.WriteLine("SimClient.cs:UpgradeClient() - upgrading child to full agent");	
		this.m_child=false;
		this.m_world.RemoveViewerAgent(this);
		this.startpos=((RemoteGridBase)m_gridServer).agentcircuits[CircuitCode].startpos;
		((RemoteGridBase)m_gridServer).agentcircuits[CircuitCode].child=false;
		this.InitNewClient();
	}

	public void DowngradeClient() {
	        OpenSim.Framework.Console.MainConsole.Instance.WriteLine("SimClient.cs:UpgradeClient() - changing full agent to child");
		this.m_child=true;
		this.m_world.RemoveViewerAgent(this);
		this.m_world.AddViewerAgent(this);
	}

	public void KillClient() {
		KillObjectPacket kill = new KillObjectPacket();
                kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
         	kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
            	kill.ObjectData[0].ID = this.ClientAvatar.localid;
	        foreach (SimClient client in m_clientThreads.Values)
           		{
		                client.OutPacket(kill);
            		}
	        if (this.m_userServer != null)
            		{
                		this.m_inventoryCache.ClientLeaving(this.AgentID, this.m_userServer);
            		}
         	else
		        {
                		this.m_inventoryCache.ClientLeaving(this.AgentID, null);
            		}

	        m_world.RemoveViewerAgent(this);

            	m_clientThreads.Remove(this.CircuitCode);
            	m_application.RemoveClientCircuit(this.CircuitCode);
	        this.ClientThread.Abort();
	}

        public static bool AddPacketHandler(PacketType packetType, PacketMethod handler)
        {
            bool result = false;
            lock (PacketHandlers)
            {
                if (!PacketHandlers.ContainsKey(packetType))
                {
                    PacketHandlers.Add(packetType, handler);
                    result = true;
                }
            }
            return result;
        }

        public bool AddLocalPacketHandler(PacketType packetType, PacketMethod handler)
        {
            bool result = false;
            lock (m_packetHandlers)
            {
                if (!m_packetHandlers.ContainsKey(packetType))
                {
                    m_packetHandlers.Add(packetType, handler);
                    result = true;
                }
            }
            return result;
        }

        protected virtual bool ProcessPacketMethod(Packet packet)
        {
            bool result = false;
            bool found = false;
            PacketMethod method;
            if (m_packetHandlers.TryGetValue(packet.Type, out method))
            {
                //there is a local handler for this packet type
                result = method(this, packet);
            }
            else
            {
                //there is not a local handler so see if there is a Global handler
                lock (PacketHandlers)
                {
                    found = PacketHandlers.TryGetValue(packet.Type, out method);
                }
                if (found)
                {
                    result = method(this, packet);
                }
            }
            return result;
        }

        private void ack_pack(Packet Pack)
        {
            if (Pack.Header.Reliable)
            {
                libsecondlife.Packets.PacketAckPacket ack_it = new PacketAckPacket();
                ack_it.Packets = new PacketAckPacket.PacketsBlock[1];
                ack_it.Packets[0] = new PacketAckPacket.PacketsBlock();
                ack_it.Packets[0].ID = Pack.Header.Sequence;
                ack_it.Header.Reliable = false;

                OutPacket(ack_it);

            }
            /*
            if (Pack.Header.Reliable)
            {
                lock (PendingAcks)
                {
                    uint sequence = (uint)Pack.Header.Sequence;
                    if (!PendingAcks.ContainsKey(sequence)) { PendingAcks[sequence] = sequence; }
                }
            }*/
        }

        protected virtual void ProcessInPacket(Packet Pack)
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
                    case PacketType.CompleteAgentMovement:
			if(this.m_child) this.UpgradeClient();
			ClientAvatar.CompleteMovement(m_world);
       	                ClientAvatar.SendInitialPosition();
                        break;
                    case PacketType.RegionHandshakeReply:
                        m_world.SendLayerData(this);
                        break;
                    case PacketType.AgentWearablesRequest:
                        ClientAvatar.SendInitialAppearance();
                        foreach (SimClient client in m_clientThreads.Values)
                        {
                            if (client.AgentID != this.AgentID)
                            {
                                ObjectUpdatePacket objupdate = client.ClientAvatar.CreateUpdatePacket();
                                this.OutPacket(objupdate);
                                client.ClientAvatar.SendAppearanceToOtherAgent(this);
                            }
                        }
                        m_world.GetInitialPrims(this);
                        break;
                    case PacketType.AgentIsNowWearing:
                        AgentIsNowWearingPacket wear = (AgentIsNowWearingPacket)Pack;
                        //Console.WriteLine(Pack.ToString());
                        break;
                    case PacketType.AgentSetAppearance:
                        AgentSetAppearancePacket appear = (AgentSetAppearancePacket)Pack;
                        // Console.WriteLine(appear.ToString());
                        this.ClientAvatar.SetAppearance(appear);
                        break;
                    case PacketType.ObjectAdd:
                        m_world.AddNewPrim((ObjectAddPacket)Pack, this);
                        break;
                    case PacketType.ObjectLink:
                        OpenSim.Framework.Console.MainConsole.Instance.WriteLine(Pack.ToString());
                        ObjectLinkPacket link = (ObjectLinkPacket)Pack;
                        uint parentprimid = 0;
                        OpenSim.world.Primitive parentprim = null;
                        if (link.ObjectData.Length > 1)
                        {
                            parentprimid = link.ObjectData[0].ObjectLocalID;
                            foreach (Entity ent in m_world.Entities.Values)
                            {
                                if (ent.localid == parentprimid)
                                {
                                    parentprim = (OpenSim.world.Primitive)ent;

                                }
                            }
                            for (int i = 1; i < link.ObjectData.Length; i++)
                            {
                                foreach (Entity ent in m_world.Entities.Values)
                                {
                                    if (ent.localid == link.ObjectData[i].ObjectLocalID)
                                    {
                                        ((OpenSim.world.Primitive)ent).MakeParent(parentprim);
                                    }
                                }
                            }
                        }
                        break;
                    case PacketType.ObjectScale:
                        OpenSim.Framework.Console.MainConsole.Instance.WriteLine(Pack.ToString());
                        break;
                    case PacketType.ObjectShape:
                        ObjectShapePacket shape = (ObjectShapePacket)Pack;
                        for (int i = 0; i < shape.ObjectData.Length; i++)
                        {
                            foreach (Entity ent in m_world.Entities.Values)
                            {
                                if (ent.localid == shape.ObjectData[i].ObjectLocalID)
                                {
                                    ((OpenSim.world.Primitive)ent).UpdateShape(shape.ObjectData[i]);
                                }
                            }
                        }
                        break;
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
                    case PacketType.AgentUpdate:
                        ClientAvatar.HandleUpdate((AgentUpdatePacket)Pack);
                        break;
                    case PacketType.ObjectImage:
                        ObjectImagePacket imagePack = (ObjectImagePacket)Pack;
                        for (int i = 0; i < imagePack.ObjectData.Length; i++)
                        {
                            foreach (Entity ent in m_world.Entities.Values)
                            {
                                if (ent.localid == imagePack.ObjectData[i].ObjectLocalID)
                                {
                                    ((OpenSim.world.Primitive)ent).UpdateTexture(imagePack.ObjectData[i].TextureEntry);
                                }
                            }
                        }
                        break;
                    case PacketType.ObjectFlagUpdate:
                        ObjectFlagUpdatePacket flags = (ObjectFlagUpdatePacket)Pack;
                        foreach (Entity ent in m_world.Entities.Values)
                        {
                            if (ent.localid == flags.AgentData.ObjectLocalID)
                            {
                                ((OpenSim.world.Primitive)ent).UpdateObjectFlags(flags);
                            }
                        }
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
                        m_inventoryCache.CreateNewInventoryFolder(this, invFolder.FolderData.FolderID, (ushort)invFolder.FolderData.Type, Helpers.FieldToString(invFolder.FolderData.Name), invFolder.FolderData.ParentID);
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
                    case PacketType.ViewerEffect:
                        ViewerEffectPacket viewer = (ViewerEffectPacket)Pack;
                        foreach (SimClient client in m_clientThreads.Values)
                        {
                            if (client.AgentID != this.AgentID)
                            {
                                viewer.AgentData.AgentID = client.AgentID;
                                viewer.AgentData.SessionID = client.SessionID;
                                client.OutPacket(viewer);
                            }
                        }
                        break;
                    case PacketType.RequestTaskInventory:
                        // Console.WriteLine(Pack.ToString());
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
                        // Console.WriteLine(Pack.ToString());
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
                                                        this.m_world.AddScript(ent, Helpers.FieldToString(assBase.Data));
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case PacketType.AgentAnimation:
			if(!m_child) {          
				AgentAnimationPacket AgentAni = (AgentAnimationPacket)Pack;
        	                for (int i = 0; i < AgentAni.AnimationList.Length; i++)
	                        {
	                            if (AgentAni.AnimationList[i].StartAnim)
	                            {
	                                ClientAvatar.current_anim = AgentAni.AnimationList[i].AnimID;
	                                ClientAvatar.anim_seq = 1;
	                                ClientAvatar.SendAnimPack();
	                            }
	                        }
			}
                        break;
                    case PacketType.ObjectSelect:
                        ObjectSelectPacket incomingselect = (ObjectSelectPacket)Pack;
                        for (int i = 0; i < incomingselect.ObjectData.Length; i++)
                        {
                            foreach (Entity ent in m_world.Entities.Values)
                            {
                                if (ent.localid == incomingselect.ObjectData[i].ObjectLocalID)
                                {
                                    ((OpenSim.world.Primitive)ent).GetProperites(this);
                                    break;
                                }
                            }
                        }
                        break;
                }
            }
        }

        private void ResendUnacked()
        {
            int now = Environment.TickCount;

            lock (NeedAck)
            {
                foreach (Packet packet in NeedAck.Values)
                {
                    if ((now - packet.TickCount > RESEND_TIMEOUT) && (!packet.Header.Resent))
                    {
                        OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Resending " + packet.Type.ToString() + " packet, " +
                         (now - packet.TickCount) + "ms have passed");

                        packet.Header.Resent = true;
                        OutPacket(packet);
                    }
                }
            }
        }

        private void SendAcks()
        {
            lock (PendingAcks)
            {
                if (PendingAcks.Count > 0)
                {
                    if (PendingAcks.Count > 250)
                    {
                        // FIXME: Handle the odd case where we have too many pending ACKs queued up
                        OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Too many ACKs queued up!");
                        return;
                    }

                    //OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Sending PacketAck");


                    int i = 0;
                    PacketAckPacket acks = new PacketAckPacket();
                    acks.Packets = new PacketAckPacket.PacketsBlock[PendingAcks.Count];

                    foreach (uint ack in PendingAcks.Values)
                    {
                        acks.Packets[i] = new PacketAckPacket.PacketsBlock();
                        acks.Packets[i].ID = ack;
                        i++;
                    }

                    acks.Header.Reliable = false;
                    OutPacket(acks);

                    PendingAcks.Clear();
                }
            }
        }

        private void AckTimer_Elapsed(object sender, ElapsedEventArgs ea)
        {
            SendAcks();
            ResendUnacked();
        }

        protected virtual void ProcessOutPacket(Packet Pack)
        {

            // Keep track of when this packet was sent out
            Pack.TickCount = Environment.TickCount;

            if (!Pack.Header.Resent)
            {
                // Set the sequence number
                lock (SequenceLock)
                {
                    if (Sequence >= MAX_SEQUENCE)
                        Sequence = 1;
                    else
                        Sequence++;
                    Pack.Header.Sequence = Sequence;
                }

                if (Pack.Header.Reliable)  //DIRTY HACK
                {
                    lock (NeedAck)
                    {
                        if (!NeedAck.ContainsKey(Pack.Header.Sequence))
                        {
                            NeedAck.Add(Pack.Header.Sequence, Pack);
                        }
                        else
                        {
                            //  Client.Log("Attempted to add a duplicate sequence number (" +
                            //     packet.Header.Sequence + ") to the NeedAck dictionary for packet type " +
                            //      packet.Type.ToString(), Helpers.LogLevel.Warning);
                        }
                    }

                    // Don't append ACKs to resent packets, in case that's what was causing the
                    // delivery to fail
                    if (!Pack.Header.Resent)
                    {
                        // Append any ACKs that need to be sent out to this packet
                        lock (PendingAcks)
                        {
                            if (PendingAcks.Count > 0 && PendingAcks.Count < MAX_APPENDED_ACKS &&
                                Pack.Type != PacketType.PacketAck &&
                                Pack.Type != PacketType.LogoutRequest)
                            {
                                Pack.Header.AckList = new uint[PendingAcks.Count];
                                int i = 0;

                                foreach (uint ack in PendingAcks.Values)
                                {
                                    Pack.Header.AckList[i] = ack;
                                    i++;
                                }

                                PendingAcks.Clear();
                                Pack.Header.AppendedAcks = true;
                            }
                        }
                    }
                }
            }

            byte[] ZeroOutBuffer = new byte[4096];
            byte[] sendbuffer;
            sendbuffer = Pack.ToBytes();

            try
            {
                if (Pack.Header.Zerocoded)
                {
                    int packetsize = Helpers.ZeroEncode(sendbuffer, sendbuffer.Length, ZeroOutBuffer);
                    m_application.SendPacketTo(ZeroOutBuffer, packetsize, SocketFlags.None, CircuitCode);//userEP);
                }
                else
                {
                    m_application.SendPacketTo(sendbuffer, sendbuffer.Length, SocketFlags.None, CircuitCode); //userEP);
                }
            }
            catch (Exception)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenSimClient.cs:ProcessOutPacket() - WARNING: Socket exception occurred on connection " + userEP.ToString() + " - killing thread");
                ClientThread.Abort();
            }

        }

        public virtual void InPacket(Packet NewPack)
        {
            // Handle appended ACKs
            if (NewPack.Header.AppendedAcks)
            {
                lock (NeedAck)
                {
                    foreach (uint ack in NewPack.Header.AckList)
                    {
                        NeedAck.Remove(ack);
                    }
                }
            }

            // Handle PacketAck packets
            if (NewPack.Type == PacketType.PacketAck)
            {
                PacketAckPacket ackPacket = (PacketAckPacket)NewPack;

                lock (NeedAck)
                {
                    foreach (PacketAckPacket.PacketsBlock block in ackPacket.Packets)
                    {
                        NeedAck.Remove(block.ID);
                    }
                }
            }
            else if ((NewPack.Type == PacketType.StartPingCheck))
            {
                //reply to pingcheck
                libsecondlife.Packets.StartPingCheckPacket startPing = (libsecondlife.Packets.StartPingCheckPacket)NewPack;
                libsecondlife.Packets.CompletePingCheckPacket endPing = new CompletePingCheckPacket();
                endPing.PingID.PingID = startPing.PingID.PingID;
                OutPacket(endPing);
            }
            else
            {
                QueItem item = new QueItem();
                item.Packet = NewPack;
                item.Incoming = true;
                this.PacketQueue.Enqueue(item);
            }

        }

        public virtual void OutPacket(Packet NewPack)
        {
            QueItem item = new QueItem();
            item.Packet = NewPack;
            item.Incoming = false;
            this.PacketQueue.Enqueue(item);
        }

        protected virtual void ClientLoop()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenSimClient.cs:ClientLoop() - Entered loop");
            while (true)
            {
                QueItem nextPacket = PacketQueue.Dequeue();
                if (nextPacket.Incoming)
                {
                    //is a incoming packet
                    ProcessInPacket(nextPacket.Packet);
                }
                else
                {
                    //is a out going packet
                    ProcessOutPacket(nextPacket.Packet);
                }
            }
        }

        protected virtual void InitNewClient()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenSimClient.cs:InitNewClient() - Adding viewer agent to world");

            m_world.AddViewerAgent(this);
            world.Entity tempent = m_world.Entities[this.AgentID];

            this.ClientAvatar = (world.Avatar)tempent;
        }

        protected virtual void AuthUser()
        {
            AuthenticateResponse sessionInfo = m_gridServer.AuthenticateSession(cirpack.CircuitCode.SessionID, cirpack.CircuitCode.ID, cirpack.CircuitCode.Code);
            if (!sessionInfo.Authorised)
            {
                //session/circuit not authorised
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenSimClient.cs:AuthUser() - New user request denied to " + userEP.ToString());
                ClientThread.Abort();
            }
            else
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenSimClient.cs:AuthUser() - Got authenticated connection from " + userEP.ToString());
                //session is authorised
                this.AgentID = cirpack.CircuitCode.ID;
                this.SessionID = cirpack.CircuitCode.SessionID;
                this.CircuitCode = cirpack.CircuitCode.Code;
                InitNewClient(); //shouldn't be called here as we might be a child agent and not want a full avatar 
                this.ClientAvatar.firstname = sessionInfo.LoginInfo.First;
                this.ClientAvatar.lastname = sessionInfo.LoginInfo.Last;
                if (sessionInfo.LoginInfo.SecureSession != LLUUID.Zero)
                {
                    this.SecureSessionID = sessionInfo.LoginInfo.SecureSession;
                }

                // Create Inventory, currently only works for sandbox mode
                if (m_sandboxMode)
                {
                    AgentInventory inventory = null;
                    if (sessionInfo.LoginInfo.InventoryFolder != null)
                    {
                        inventory = this.CreateInventory(sessionInfo.LoginInfo.InventoryFolder);
                        if (sessionInfo.LoginInfo.BaseFolder != null)
                        {
                            if (!inventory.HasFolder(sessionInfo.LoginInfo.BaseFolder))
                            {
                                m_inventoryCache.CreateNewInventoryFolder(this, sessionInfo.LoginInfo.BaseFolder);
                            }
                            this.newAssetFolder = sessionInfo.LoginInfo.BaseFolder;
                            AssetBase[] inventorySet = m_assetCache.CreateNewInventorySet(this.AgentID);
                            if (inventorySet != null)
                            {
                                for (int i = 0; i < inventorySet.Length; i++)
                                {
                                    if (inventorySet[i] != null)
                                    {
                                        m_inventoryCache.AddNewInventoryItem(this, sessionInfo.LoginInfo.BaseFolder, inventorySet[i]);
                                    }
                                }
                            }
                        }
                    }
                }

                ClientLoop();
            }
        }
        #region Packet handlers

        protected virtual bool Logout(SimClient simClient, Packet packet)
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenSimClient.cs:ProcessInPacket() - Got a logout request");
            //send reply to let the client logout
            LogoutReplyPacket logReply = new LogoutReplyPacket();
            logReply.AgentData.AgentID = this.AgentID;
            logReply.AgentData.SessionID = this.SessionID;
            logReply.InventoryData = new LogoutReplyPacket.InventoryDataBlock[1];
            logReply.InventoryData[0] = new LogoutReplyPacket.InventoryDataBlock();
            logReply.InventoryData[0].ItemID = LLUUID.Zero;
            OutPacket(logReply);
            //tell all clients to kill our object
            KillObjectPacket kill = new KillObjectPacket();
            kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
            kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
            kill.ObjectData[0].ID = this.ClientAvatar.localid;
            foreach (SimClient client in m_clientThreads.Values)
            {
                client.OutPacket(kill);
            }
            if (this.m_userServer != null)
            {
                this.m_inventoryCache.ClientLeaving(this.AgentID, this.m_userServer);
            }
            else
            {
                this.m_inventoryCache.ClientLeaving(this.AgentID, null);
            }

            m_gridServer.LogoutSession(this.SessionID, this.AgentID, this.CircuitCode);
            /*lock (m_world.Entities)
            {
                m_world.Entities.Remove(this.AgentID);
            }*/
            m_world.RemoveViewerAgent(this);
            //need to do other cleaning up here too
            m_clientThreads.Remove(this.CircuitCode);
            m_application.RemoveClientCircuit(this.CircuitCode);
            this.ClientThread.Abort();
            return true;
        }

        protected bool AgentTextureCached(SimClient simclient, Packet packet)
        {
            // Console.WriteLine(packet.ToString());
            AgentCachedTexturePacket chechedtex = (AgentCachedTexturePacket)packet;
            AgentCachedTextureResponsePacket cachedresp = new AgentCachedTextureResponsePacket();
            cachedresp.AgentData.AgentID = this.AgentID;
            cachedresp.AgentData.SessionID = this.SessionID;
            cachedresp.AgentData.SerialNum = this.cachedtextureserial;
            this.cachedtextureserial++;
            cachedresp.WearableData = new AgentCachedTextureResponsePacket.WearableDataBlock[chechedtex.WearableData.Length];
            for (int i = 0; i < chechedtex.WearableData.Length; i++)
            {
                cachedresp.WearableData[i] = new AgentCachedTextureResponsePacket.WearableDataBlock();
                cachedresp.WearableData[i].TextureIndex = chechedtex.WearableData[i].TextureIndex;
                cachedresp.WearableData[i].TextureID = LLUUID.Zero;
                cachedresp.WearableData[i].HostName = new byte[0];
            }
            this.OutPacket(cachedresp);
            return true;
        }

        protected bool MultipleObjUpdate(SimClient simClient, Packet packet)
        {
            MultipleObjectUpdatePacket multipleupdate = (MultipleObjectUpdatePacket)packet;
            for (int i = 0; i < multipleupdate.ObjectData.Length; i++)
            {
                if (multipleupdate.ObjectData[i].Type == 9) //change position
                {
                    libsecondlife.LLVector3 pos = new LLVector3(multipleupdate.ObjectData[i].Data, 0);
                    foreach (Entity ent in m_world.Entities.Values)
                    {
                        if (ent.localid == multipleupdate.ObjectData[i].ObjectLocalID)
                        {
                            ((OpenSim.world.Primitive)ent).UpdatePosition(pos);

                        }
                    }
                    //should update stored position of the prim
                }
                else if (multipleupdate.ObjectData[i].Type == 10)//rotation
                {
                    libsecondlife.LLQuaternion rot = new LLQuaternion(multipleupdate.ObjectData[i].Data, 0, true);
                    foreach (Entity ent in m_world.Entities.Values)
                    {
                        if (ent.localid == multipleupdate.ObjectData[i].ObjectLocalID)
                        {
                            ent.rotation = new Axiom.MathLib.Quaternion(rot.W, rot.X, rot.Y, rot.Z);
                            ((OpenSim.world.Primitive)ent).UpdateFlag = true;
                        }
                    }
                }
                else if (multipleupdate.ObjectData[i].Type == 13)//scale
                {

                    libsecondlife.LLVector3 scale = new LLVector3(multipleupdate.ObjectData[i].Data, 12);
                    foreach (Entity ent in m_world.Entities.Values)
                    {
                        if (ent.localid == multipleupdate.ObjectData[i].ObjectLocalID)
                        {
                            ((OpenSim.world.Primitive)ent).Scale = scale;
                        }
                    }
                }
            }
            return true;
        }

        #endregion

        private AgentInventory CreateInventory(LLUUID baseFolder)
        {
            AgentInventory inventory = null;
            if (this.m_userServer != null)
            {
                // a user server is set so request the inventory from it
                Console.WriteLine("getting inventory from user server");
                inventory = m_inventoryCache.FetchAgentsInventory(this.AgentID, m_userServer);
            }
            else
            {
                inventory = new AgentInventory();
                inventory.AgentID = this.AgentID;
                inventory.CreateRootFolder(this.AgentID, false);
                m_inventoryCache.AddNewAgentsInventory(inventory);
                m_inventoryCache.CreateNewInventoryFolder(this, baseFolder);
            }
            return inventory;
        }

        private void CreateInventoryItem(CreateInventoryItemPacket packet)
        {
            if (packet.InventoryBlock.Type == 7)
            {
                //lets try this out with creating a notecard
                AssetBase asset = new AssetBase();
                asset.Name = Helpers.FieldToString(packet.InventoryBlock.Name);
                asset.Description = Helpers.FieldToString(packet.InventoryBlock.Description);
                asset.InvType = packet.InventoryBlock.InvType;
                asset.Type = packet.InventoryBlock.Type;
                asset.FullID = LLUUID.Random();
                asset.Data = new byte[0];

                m_assetCache.AddAsset(asset);
                m_inventoryCache.AddNewInventoryItem(this, packet.InventoryBlock.FolderID, asset);
            }
        }
    }
}
