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
using OpenSim.Framework.Types;
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
    public partial class SimClient
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
        private OpenSimNetworkHandler m_networkServer;
        private InventoryCache m_inventoryCache;
        public bool m_sandboxMode;
        private int cachedtextureserial = 0;
        private RegionInfo m_regionData;
        protected AuthenticateSessionsBase m_authenticateSessionsHandler;

        protected static Dictionary<PacketType, PacketMethod> PacketHandlers = new Dictionary<PacketType, PacketMethod>(); //Global/static handlers for all clients

        protected Dictionary<PacketType, PacketMethod> m_packetHandlers = new Dictionary<PacketType, PacketMethod>(); //local handlers for this instance 

        public IUserServer UserServer
        {
            set
            {
                this.m_userServer = value;
            }
        }

        public SimClient(EndPoint remoteEP, UseCircuitCodePacket initialcirpack, World world, Dictionary<uint, SimClient> clientThreads, AssetCache assetCache, IGridServer gridServer, OpenSimNetworkHandler application, InventoryCache inventoryCache, bool sandboxMode, bool child, RegionInfo regionDat, AuthenticateSessionsBase authenSessions)
        {
            m_world = world;
            m_clientThreads = clientThreads;
            m_assetCache = assetCache;
            m_gridServer = gridServer;
            m_networkServer = application;
            m_inventoryCache = inventoryCache;
            m_sandboxMode = sandboxMode;
            m_child = child;
            m_regionData = regionDat;
            m_authenticateSessionsHandler = authenSessions;

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "OpenSimClient.cs - Started up new client thread to handle incoming request");
            cirpack = initialcirpack;
            userEP = remoteEP;

            if (m_gridServer.GetName() == "Remote")
            {
                this.startpos = m_authenticateSessionsHandler.GetPosition(initialcirpack.CircuitCode.Code);
            }
            else
            {
                this.startpos = new LLVector3(128, 128, m_world.Terrain[(int)128, (int)128] + 15.0f); // new LLVector3(128.0f, 128.0f, 60f);
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

        # region Client Methods
        public void UpgradeClient()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "SimClient.cs:UpgradeClient() - upgrading child to full agent");
            this.m_child = false;
            this.m_world.RemoveViewerAgent(this);
            if (!this.m_sandboxMode)
            {
                this.startpos = ((RemoteGridBase)m_gridServer).agentcircuits[CircuitCode].startpos;
                ((RemoteGridBase)m_gridServer).agentcircuits[CircuitCode].child = false;
            }
            this.InitNewClient();
        }

        public void DowngradeClient()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "SimClient.cs:UpgradeClient() - changing full agent to child");
            this.m_child = true;
            this.m_world.RemoveViewerAgent(this);
            this.m_world.AddViewerAgent(this);
        }

        public void KillClient()
        {
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
            m_networkServer.RemoveClientCircuit(this.CircuitCode);
            this.ClientThread.Abort();
        }
        #endregion

        # region Packet Handling
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

        # endregion

        # region Low Level Packet Methods

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

        private void ResendUnacked()
        {
            int now = Environment.TickCount;

            lock (NeedAck)
            {
                foreach (Packet packet in NeedAck.Values)
                {
                    if ((now - packet.TickCount > RESEND_TIMEOUT) && (!packet.Header.Resent))
                    {
                        OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.VERBOSE, "Resending " + packet.Type.ToString() + " packet, " +
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
                        OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.VERBOSE, "Too many ACKs queued up!");
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

        # endregion

        #region Packet Queue Processing

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
                            try
                            {
                                NeedAck.Add(Pack.Header.Sequence, Pack);
                            }
                            catch (Exception e) // HACKY
                            {
                                e.ToString();
                                // Ignore
                                // Seems to throw a exception here occasionally
                                // of 'duplicate key' despite being locked.
                                // !?!?!?
                            }
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
                    m_networkServer.SendPacketTo(ZeroOutBuffer, packetsize, SocketFlags.None, CircuitCode);//userEP);
                }
                else
                {
                    m_networkServer.SendPacketTo(sendbuffer, sendbuffer.Length, SocketFlags.None, CircuitCode); //userEP);
                }
            }
            catch (Exception)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "OpenSimClient.cs:ProcessOutPacket() - WARNING: Socket exception occurred on connection " + userEP.ToString() + " - killing thread");
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
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "OpenSimClient.cs:ClientLoop() - Entered loop");
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

        #endregion

        # region Setup

        protected virtual void InitNewClient()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "OpenSimClient.cs:InitNewClient() - Adding viewer agent to world");

            m_world.AddViewerAgent(this);
            world.Entity tempent = m_world.Entities[this.AgentID];

            this.ClientAvatar = (world.Avatar)tempent;
        }

        protected virtual void AuthUser()
        {
            // AuthenticateResponse sessionInfo = m_gridServer.AuthenticateSession(cirpack.CircuitCode.SessionID, cirpack.CircuitCode.ID, cirpack.CircuitCode.Code);
            AuthenticateResponse sessionInfo = this.m_networkServer.AuthenticateSession(cirpack.CircuitCode.SessionID, cirpack.CircuitCode.ID, cirpack.CircuitCode.Code);
            if (!sessionInfo.Authorised)
            {
                //session/circuit not authorised
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.NORMAL, "OpenSimClient.cs:AuthUser() - New user request denied to " + userEP.ToString());
                ClientThread.Abort();
            }
            else
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.NORMAL, "OpenSimClient.cs:AuthUser() - Got authenticated connection from " + userEP.ToString());
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
                    this.SetupInventory(sessionInfo);
                }

                ClientLoop();
            }
        }
        # endregion

        #region Inventory Creation
        private void SetupInventory(AuthenticateResponse sessionInfo)
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
            if (!(packet.InventoryBlock.Type == 3 || packet.InventoryBlock.Type == 7))
            {
                System.Console.WriteLine("Attempted to create " + Util.FieldToString(packet.InventoryBlock.Name) + " in inventory.  Unsupported type");
                return;
            }

            //lets try this out with creating a notecard
            AssetBase asset = new AssetBase();

            asset.Name = Util.FieldToString(packet.InventoryBlock.Name);
            asset.Description = Util.FieldToString(packet.InventoryBlock.Description);
            asset.InvType = packet.InventoryBlock.InvType;
            asset.Type = packet.InventoryBlock.Type;
            asset.FullID = LLUUID.Random();

            switch (packet.InventoryBlock.Type)
            {
                case 7: // Notecard
                    asset.Data = new byte[0];
                    break;

                case 3: // Landmark
                    String content;
                    content = "Landmark version 2\n";
                    content += "region_id " + m_regionData.SimUUID + "\n";
                    String strPos = String.Format("%.2f %.2f %.2f>",
                                                    this.ClientAvatar.Pos.X,
                                                    this.ClientAvatar.Pos.Y,
                                                    this.ClientAvatar.Pos.Z);
                    content += "local_pos " + strPos + "\n";
                    asset.Data = (new System.Text.ASCIIEncoding()).GetBytes(content);
                    break;
                default:
                    break;
            }
            m_assetCache.AddAsset(asset);
            m_inventoryCache.AddNewInventoryItem(this, packet.InventoryBlock.FolderID, asset);
        }
        #endregion

        #region Nested Classes

        public class QueItem
        {
            public QueItem()
            {
            }

            public Packet Packet;
            public bool Incoming;
        }
        #endregion
    }
}
