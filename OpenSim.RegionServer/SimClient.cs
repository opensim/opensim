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
    /// <summary>
    /// Handles new client connections
    /// Constructor takes a single Packet and authenticates everything
    /// </summary>
    public class SimClient
    {

        public LLUUID AgentID;
        public LLUUID SessionID;
        public LLUUID SecureSessionID = LLUUID.Zero;
        public uint CircuitCode;
        public world.Avatar ClientAvatar;
        private UseCircuitCodePacket cirpack;
        private Thread ClientThread;
        public EndPoint userEP;
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

        private void ack_pack(Packet Pack)
        {
            //libsecondlife.Packets.PacketAckPacket ack_it = new PacketAckPacket();
            //ack_it.Packets = new PacketAckPacket.PacketsBlock[1];
            //ack_it.Packets[0] = new PacketAckPacket.PacketsBlock();
            //ack_it.Packets[0].ID = Pack.Header.ID;
            //ack_it.Header.Reliable = false;

            //OutPacket(ack_it);

            if (Pack.Header.Reliable)
            {
                lock (PendingAcks)
                {
                    uint sequence = (uint)Pack.Header.Sequence;
                    if (!PendingAcks.ContainsKey(sequence)) { PendingAcks[sequence] = sequence; }
                }
            }
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
            switch (Pack.Type)
            {
                case PacketType.CompleteAgentMovement:
                    ClientAvatar.CompleteMovement(OpenSimRoot.Instance.LocalWorld);
                    ClientAvatar.SendInitialPosition();
                    break;
                case PacketType.RegionHandshakeReply:
                    OpenSimRoot.Instance.LocalWorld.SendLayerData(this);
                    break;
                case PacketType.AgentWearablesRequest:
                    ClientAvatar.SendInitialAppearance();
                    foreach (SimClient client in OpenSimRoot.Instance.ClientThreads.Values)
                    {
                        if (client.AgentID != this.AgentID)
                        {
                            ObjectUpdatePacket objupdate = client.ClientAvatar.CreateUpdatePacket();
                            this.OutPacket(objupdate);
                            client.ClientAvatar.SendAppearanceToOtherAgent(this);
                        }
                    }
                    OpenSimRoot.Instance.LocalWorld.GetInitialPrims(this);
                    break;
                case PacketType.ObjectAdd:
                    OpenSimRoot.Instance.LocalWorld.AddNewPrim((ObjectAddPacket)Pack, this);
                    break;
                case PacketType.ObjectLink:
                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine(Pack.ToString());
                    break;
                case PacketType.ObjectScale:
                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine(Pack.ToString());
                    break;
                case PacketType.ObjectShape:
                    ObjectShapePacket shape = (ObjectShapePacket)Pack;
                    for (int i = 0; i < shape.ObjectData.Length; i++)
                    {
                        foreach (Entity ent in OpenSimRoot.Instance.LocalWorld.Entities.Values)
                        {
                            if (ent.localid == shape.ObjectData[i].ObjectLocalID)
                            {
                                ((OpenSim.world.Primitive)ent).UpdateShape(shape.ObjectData[i]);
                            }
                        }
                    }
                    break;
                case PacketType.MultipleObjectUpdate:
                    MultipleObjectUpdatePacket multipleupdate = (MultipleObjectUpdatePacket)Pack;

                    for (int i = 0; i < multipleupdate.ObjectData.Length; i++)
                    {
                        if (multipleupdate.ObjectData[i].Type == 9) //change position
                        {
                            libsecondlife.LLVector3 pos = new LLVector3(multipleupdate.ObjectData[i].Data, 0);
                            foreach (Entity ent in OpenSimRoot.Instance.LocalWorld.Entities.Values)
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
                            foreach (Entity ent in OpenSimRoot.Instance.LocalWorld.Entities.Values)
                            {
                                if (ent.localid == multipleupdate.ObjectData[i].ObjectLocalID)
                                {
                                    ent.rotation = new Axiom.MathLib.Quaternion(rot.W, rot.X, rot.Y, rot.W);
                                    ((OpenSim.world.Primitive)ent).UpdateFlag = true;
                                }
                            }
                        }
                        else if (multipleupdate.ObjectData[i].Type == 13)//scale
                        {

                            libsecondlife.LLVector3 scale = new LLVector3(multipleupdate.ObjectData[i].Data, 12);
                            foreach (Entity ent in OpenSimRoot.Instance.LocalWorld.Entities.Values)
                            {
                                if (ent.localid == multipleupdate.ObjectData[i].ObjectLocalID)
                                {
                                    ((OpenSim.world.Primitive)ent).Scale = scale;
                                }
                            }
                        }
                    }
                    break;
                case PacketType.RequestImage:
                    RequestImagePacket imageRequest = (RequestImagePacket)Pack;
                    for (int i = 0; i < imageRequest.RequestImage.Length; i++)
                    {
                        OpenSimRoot.Instance.AssetCache.AddTextureRequest(this, imageRequest.RequestImage[i].Image);
                    }
                    break;
                case PacketType.TransferRequest:
                    //Console.WriteLine("OpenSimClient.cs:ProcessInPacket() - Got transfer request");
                    TransferRequestPacket transfer = (TransferRequestPacket)Pack;
                    OpenSimRoot.Instance.AssetCache.AddAssetRequest(this, transfer);
                    break;
                case PacketType.AgentUpdate:
                    ClientAvatar.HandleUpdate((AgentUpdatePacket)Pack);
                    break;
                case PacketType.LogoutRequest:
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
                    foreach (SimClient client in OpenSimRoot.Instance.ClientThreads.Values)
                    {
                        client.OutPacket(kill);
                    }
                    OpenSimRoot.Instance.GridServers.GridServer.LogoutSession(this.SessionID, this.AgentID, this.CircuitCode);
                    lock (OpenSimRoot.Instance.LocalWorld.Entities)
                    {
                        OpenSimRoot.Instance.LocalWorld.Entities.Remove(this.AgentID);
                    }
                    //need to do other cleaning up here too
                    OpenSimRoot.Instance.ClientThreads.Remove(this.CircuitCode); //this.userEP);
                    OpenSimRoot.Instance.Application.RemoveClientCircuit(this.CircuitCode);
                    this.ClientThread.Abort();
                    break;
                case PacketType.ChatFromViewer:
                    ChatFromViewerPacket inchatpack = (ChatFromViewerPacket)Pack;
                    if (Helpers.FieldToString(inchatpack.ChatData.Message) == "") break;

                    System.Text.Encoding _enc = System.Text.Encoding.ASCII;
                    libsecondlife.Packets.ChatFromSimulatorPacket reply = new ChatFromSimulatorPacket();
                    reply.ChatData.Audible = 1;
                    reply.ChatData.Message = inchatpack.ChatData.Message;
                    reply.ChatData.ChatType = 1;
                    reply.ChatData.SourceType = 1;
                    reply.ChatData.Position = this.ClientAvatar.position;
                    reply.ChatData.FromName = _enc.GetBytes(this.ClientAvatar.firstname + " " + this.ClientAvatar.lastname + "\0");
                    reply.ChatData.OwnerID = this.AgentID;
                    reply.ChatData.SourceID = this.AgentID;
                    foreach (SimClient client in OpenSimRoot.Instance.ClientThreads.Values)
                    {
                        client.OutPacket(reply);
                    }
                    break;
                case PacketType.ObjectImage:
                    ObjectImagePacket imagePack = (ObjectImagePacket)Pack;
                    for (int i = 0; i < imagePack.ObjectData.Length; i++)
                    {
                        foreach (Entity ent in OpenSimRoot.Instance.LocalWorld.Entities.Values)
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
                    foreach (Entity ent in OpenSimRoot.Instance.LocalWorld.Entities.Values)
                    {
                        if (ent.localid == flags.AgentData.ObjectLocalID)
                        {
                            ((OpenSim.world.Primitive)ent).UpdateObjectFlags(flags);
                        }
                    }

                    break;
                case PacketType.AssetUploadRequest:
                    //this.debug = true;
                    AssetUploadRequestPacket request = (AssetUploadRequestPacket)Pack;
                    Console.WriteLine(Pack.ToString());
                    if (request.AssetBlock.Type == 0)
                    {
                        this.UploadAssets.HandleUploadPacket(request, LLUUID.Random());
                    }
                    else
                    {
                        this.UploadAssets.HandleUploadPacket(request, request.AssetBlock.TransactionID.Combine(this.SecureSessionID));
                    }
                    break;
                case PacketType.SendXferPacket:
                    Console.WriteLine(Pack.ToString());
                    this.UploadAssets.HandleXferPacket((SendXferPacketPacket)Pack);
                    break;
                case PacketType.CreateInventoryFolder:
                    CreateInventoryFolderPacket invFolder = (CreateInventoryFolderPacket)Pack;
                    OpenSimRoot.Instance.InventoryCache.CreateNewInventoryFolder(this, invFolder.FolderData.FolderID, (ushort)invFolder.FolderData.Type);
                    Console.WriteLine(Pack.ToString());
                    break;
                case PacketType.CreateInventoryItem:
                    Console.WriteLine(Pack.ToString());
                    CreateInventoryItemPacket createItem = (CreateInventoryItemPacket)Pack;
                    if (createItem.InventoryBlock.TransactionID != LLUUID.Zero)
                    {
                        this.UploadAssets.CreateInventoryItem(createItem);
                    }
                    break;
                case PacketType.FetchInventory:
                    //Console.WriteLine("fetch item packet");
                    FetchInventoryPacket FetchInventory = (FetchInventoryPacket)Pack;
                    OpenSimRoot.Instance.InventoryCache.FetchInventory(this, FetchInventory);
                    break;
                case PacketType.FetchInventoryDescendents:
                    FetchInventoryDescendentsPacket Fetch = (FetchInventoryDescendentsPacket)Pack;
                    OpenSimRoot.Instance.InventoryCache.FetchInventoryDescendents(this, Fetch);
                    break;
                case PacketType.UpdateInventoryItem:
                  /*  UpdateInventoryItemPacket update = (UpdateInventoryItemPacket)Pack;
                    for (int i = 0; i < update.InventoryData.Length; i++)
                    {
                        if (update.InventoryData[i].TransactionID != LLUUID.Zero)
                        {
                            AssetBase asset = OpenSimRoot.Instance.AssetCache.GetAsset(update.InventoryData[i].TransactionID.Combine(this.SecureSessionID));
                            OpenSimRoot.Instance.InventoryCache.UpdateInventoryItem(this, update.InventoryData[i].ItemID, asset);
                        }
                    }*/
                    break;
                case PacketType.ViewerEffect:
                    ViewerEffectPacket viewer = (ViewerEffectPacket)Pack;
                    foreach (SimClient client in OpenSimRoot.Instance.ClientThreads.Values)
                    {
                        if (client.AgentID != this.AgentID)
                        {
                            viewer.AgentData.AgentID = client.AgentID;
                            viewer.AgentData.SessionID = client.SessionID;
                            client.OutPacket(viewer);
                        }
                    }
                    break;
                case PacketType.DeRezObject:
                    //OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Received DeRezObject packet");
                    OpenSimRoot.Instance.LocalWorld.DeRezObject((DeRezObjectPacket)Pack, this);
                    break;
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

                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Sending PacketAck");


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

            //ServerConsole.MainConsole.Instance.WriteLine("OUT: \n" + Pack.ToString());

            byte[] ZeroOutBuffer = new byte[4096];
            byte[] sendbuffer;
            sendbuffer = Pack.ToBytes();

            try
            {
                if (Pack.Header.Zerocoded)
                {
                    int packetsize = Helpers.ZeroEncode(sendbuffer, sendbuffer.Length, ZeroOutBuffer);
                    OpenSimRoot.Instance.Application.SendPacketTo(ZeroOutBuffer, packetsize, SocketFlags.None, CircuitCode);//userEP);
                }
                else
                {
                    OpenSimRoot.Instance.Application.SendPacketTo(sendbuffer, sendbuffer.Length, SocketFlags.None, CircuitCode); //userEP);
                }
            }
            catch (Exception)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenSimClient.cs:ProcessOutPacket() - WARNING: Socket exception occurred on connection " + userEP.ToString() + " - killing thread");
                ClientThread.Abort();
            }

        }

        public  virtual void InPacket(Packet NewPack)
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

        public SimClient(EndPoint remoteEP, UseCircuitCodePacket initialcirpack)
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("OpenSimClient.cs - Started up new client thread to handle incoming request");
            cirpack = initialcirpack;
            userEP = remoteEP;
            PacketQueue = new BlockingQueue<QueItem>();

            this.UploadAssets = new AgentAssetUpload(this);
            AckTimer = new System.Timers.Timer(500);
            AckTimer.Elapsed += new ElapsedEventHandler(AckTimer_Elapsed);
            AckTimer.Start();

            ClientThread = new Thread(new ThreadStart(AuthUser));
            ClientThread.IsBackground = true;
            ClientThread.Start();
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
            OpenSimRoot.Instance.LocalWorld.AddViewerAgent(this);
            world.Entity tempent = OpenSimRoot.Instance.LocalWorld.Entities[this.AgentID];
            this.ClientAvatar = (world.Avatar)tempent;
        }

        protected virtual void AuthUser()
        {
            AuthenticateResponse sessionInfo = OpenSimRoot.Instance.GridServers.GridServer.AuthenticateSession(cirpack.CircuitCode.SessionID, cirpack.CircuitCode.ID, cirpack.CircuitCode.Code);
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
                if (OpenSimRoot.Instance.Sandbox)
                {
                    if (sessionInfo.LoginInfo.InventoryFolder != null)
                    {
                        this.CreateInventory(sessionInfo.LoginInfo.InventoryFolder);
                        if (sessionInfo.LoginInfo.BaseFolder != null)
                        {
                            OpenSimRoot.Instance.InventoryCache.CreateNewInventoryFolder(this, sessionInfo.LoginInfo.BaseFolder);
                            this.newAssetFolder = sessionInfo.LoginInfo.BaseFolder;
                            AssetBase[] inventorySet = OpenSimRoot.Instance.AssetCache.CreateNewInventorySet(this.AgentID);
                            if (inventorySet != null)
                            {
                                for (int i = 0; i < inventorySet.Length; i++)
                                {
                                    if (inventorySet[i] != null)
                                    {
                                        OpenSimRoot.Instance.InventoryCache.AddNewInventoryItem(this, sessionInfo.LoginInfo.BaseFolder, inventorySet[i]);
                                    }
                                }
                            }
                        }
                    }
                }

                ClientLoop();
            }
        }

        private void CreateInventory(LLUUID baseFolder)
        {
            AgentInventory inventory = new AgentInventory();
            inventory.AgentID = this.AgentID;
            OpenSimRoot.Instance.InventoryCache.AddNewAgentsInventory(inventory);
            OpenSimRoot.Instance.InventoryCache.CreateNewInventoryFolder(this, baseFolder);
        }
    }
}
