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
using OpenSim.world;
using OpenSim.Assets;
using OpenSim.Framework.Console;

namespace OpenSim
{
    public delegate bool PacketMethod(ClientView simClient, Packet packet);

    /// <summary>
    /// Handles new client connections
    /// Constructor takes a single Packet and authenticates everything
    /// </summary>
    public partial class ClientView : ClientViewBase, IClientAPI
    {
        protected static Dictionary<PacketType, PacketMethod> PacketHandlers = new Dictionary<PacketType, PacketMethod>(); //Global/static handlers for all clients
        protected Dictionary<PacketType, PacketMethod> m_packetHandlers = new Dictionary<PacketType, PacketMethod>(); //local handlers for this instance 

        public LLUUID AgentID;
        public LLUUID SessionID;
        public LLUUID SecureSessionID = LLUUID.Zero;
        public bool m_child;
        public world.Avatar ClientAvatar;
        private UseCircuitCodePacket cirpack;
        public Thread ClientThread;
        public LLVector3 startpos;
         
        private AgentAssetUpload UploadAssets;
        private LLUUID newAssetFolder = LLUUID.Zero;
        private bool debug = false;
        private World m_world;
        private Dictionary<uint, ClientView> m_clientThreads;
        private AssetCache m_assetCache;
        private IGridServer m_gridServer;
        private IUserServer m_userServer = null;
        private InventoryCache m_inventoryCache;
        public bool m_sandboxMode;
        private int cachedtextureserial = 0;
        private RegionInfo m_regionData;
        protected AuthenticateSessionsBase m_authenticateSessionsHandler;

        public IUserServer UserServer
        {
            set
            {
                this.m_userServer = value;
            }
        }

        public LLVector3 StartPos
        {
            get
            {
                return startpos;
            }
            set
            {
                startpos = value;
            }
        }

        public ClientView(EndPoint remoteEP, UseCircuitCodePacket initialcirpack, World world, Dictionary<uint, ClientView> clientThreads, AssetCache assetCache, IGridServer gridServer, OpenSimNetworkHandler application, InventoryCache inventoryCache, bool sandboxMode, bool child, RegionInfo regionDat, AuthenticateSessionsBase authenSessions)
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
            MainConsole.Instance.Notice("OpenSimClient.cs - Started up new client thread to handle incoming request");
            cirpack = initialcirpack;
            userEP = remoteEP;

            if (m_gridServer.GetName() == "Remote")
            {
                this.m_child = m_authenticateSessionsHandler.GetAgentChildStatus(initialcirpack.CircuitCode.Code);
                this.startpos = m_authenticateSessionsHandler.GetPosition(initialcirpack.CircuitCode.Code);

                // Dont rez new users underground
                float aboveGround = 3.0f; // rez at least 3 meters above ground
                if (this.startpos.Z < (m_world.Terrain[(int)this.startpos.X, (int)this.startpos.Y] + aboveGround))
                    this.startpos.Z = m_world.Terrain[(int)this.startpos.X, (int)this.startpos.Y] + aboveGround;

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


            m_world.parcelManager.sendParcelOverlay(this);

            ClientThread = new Thread(new ThreadStart(AuthUser));
            ClientThread.IsBackground = true;
            ClientThread.Start();
        }

        # region Client Methods
        public void UpgradeClient()
        {
            MainConsole.Instance.Notice("SimClient.cs:UpgradeClient() - upgrading child to full agent");
            this.m_child = false;
            //this.m_world.RemoveViewerAgent(this);
            if (!this.m_sandboxMode)
            {
                this.startpos = m_authenticateSessionsHandler.GetPosition(CircuitCode);
                m_authenticateSessionsHandler.UpdateAgentChildStatus(CircuitCode, false);
            }
            OnChildAgentStatus(this.m_child);
            //this.InitNewClient();
        }

        public void DowngradeClient()
        {
            MainConsole.Instance.Notice("SimClient.cs:UpgradeClient() - changing full agent to child");
            this.m_child = true;
            OnChildAgentStatus(this.m_child);
            //this.m_world.RemoveViewerAgent(this);
            //this.m_world.AddViewerAgent(this);
        }

        public void KillClient()
        {
            KillObjectPacket kill = new KillObjectPacket();
            kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
            kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
            kill.ObjectData[0].ID = this.ClientAvatar.localid;
            foreach (ClientView client in m_clientThreads.Values)
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

        protected virtual void ClientLoop()
        {
            MainConsole.Instance.Notice("OpenSimClient.cs:ClientLoop() - Entered loop");
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
        # endregion

        # region Setup

        protected virtual void InitNewClient()
        {
            MainConsole.Instance.Notice("OpenSimClient.cs:InitNewClient() - Adding viewer agent to world");
            this.ClientAvatar = m_world.AddViewerAgent(this);         
        }

        protected virtual void AuthUser()
        {
            // AuthenticateResponse sessionInfo = m_gridServer.AuthenticateSession(cirpack.CircuitCode.SessionID, cirpack.CircuitCode.ID, cirpack.CircuitCode.Code);
            AuthenticateResponse sessionInfo = this.m_networkServer.AuthenticateSession(cirpack.CircuitCode.SessionID, cirpack.CircuitCode.ID, cirpack.CircuitCode.Code);
            if (!sessionInfo.Authorised)
            {
                //session/circuit not authorised
                OpenSim.Framework.Console.MainConsole.Instance.Notice("OpenSimClient.cs:AuthUser() - New user request denied to " + userEP.ToString());
                ClientThread.Abort();
            }
            else
            {
                OpenSim.Framework.Console.MainConsole.Instance.Notice("OpenSimClient.cs:AuthUser() - Got authenticated connection from " + userEP.ToString());
                //session is authorised
                this.AgentID = cirpack.CircuitCode.ID;
                this.SessionID = cirpack.CircuitCode.SessionID;
                this.CircuitCode = cirpack.CircuitCode.Code;
                InitNewClient(); 
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


        protected override void KillThread()
        {
            this.ClientThread.Abort();
        }

        #region World/Avatar To Viewer Methods

        public void SendChatMessage(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID)
        {
            System.Text.Encoding enc = System.Text.Encoding.ASCII;
            libsecondlife.Packets.ChatFromSimulatorPacket reply = new ChatFromSimulatorPacket();
            reply.ChatData.Audible = 1;
            reply.ChatData.Message = message;
            reply.ChatData.ChatType = type;
            reply.ChatData.SourceType = 1;
            reply.ChatData.Position = fromPos;
            reply.ChatData.FromName = enc.GetBytes(fromName + "\0");
            reply.ChatData.OwnerID = fromAgentID;
            reply.ChatData.SourceID = fromAgentID;

            this.OutPacket(reply);
        }

        public void SendAppearance(AvatarWearable[] wearables)
        {
            AgentWearablesUpdatePacket aw = new AgentWearablesUpdatePacket();
            aw.AgentData.AgentID = this.AgentID;
            aw.AgentData.SerialNum = 0;
            aw.AgentData.SessionID = this.SessionID;

            aw.WearableData = new AgentWearablesUpdatePacket.WearableDataBlock[13];
            AgentWearablesUpdatePacket.WearableDataBlock awb;
            for (int i = 0; i < wearables.Length; i++)
            {
                awb = new AgentWearablesUpdatePacket.WearableDataBlock();
                awb.WearableType = (byte)i;
                awb.AssetID = wearables[i].AssetID;
                awb.ItemID = wearables[i].ItemID;
                aw.WearableData[i] = awb;
            }

            this.OutPacket(aw);
        }
        #endregion

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
                MainConsole.Instance.Verbose("getting inventory from user server");
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
                MainConsole.Instance.Warn("Attempted to create " + Util.FieldToString(packet.InventoryBlock.Name) + " in inventory.  Unsupported type");
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

    }
}
