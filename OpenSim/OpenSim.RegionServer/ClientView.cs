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
using OpenSim.Framework;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Utilities;
using OpenSim.Assets;

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
        public bool m_child = false;
        private UseCircuitCodePacket cirpack;
        public Thread ClientThread;
        public LLVector3 startpos;

        private AgentAssetUpload UploadAssets;
        private LLUUID newAssetFolder = LLUUID.Zero;
        private bool debug = false;
        private IWorld m_world;
        private Dictionary<uint, ClientView> m_clientThreads;
        private AssetCache m_assetCache;
        private IGridServer m_gridServer;
        private InventoryCache m_inventoryCache;
        private int cachedtextureserial = 0;
        private RegionInfo m_regionData;
        protected AuthenticateSessionsBase m_authenticateSessionsHandler;


        public ClientView(EndPoint remoteEP, UseCircuitCodePacket initialcirpack, Dictionary<uint, ClientView> clientThreads, AssetCache assetCache, PacketServer packServer, InventoryCache inventoryCache, AuthenticateSessionsBase authenSessions)
        {

            m_clientThreads = clientThreads;
            m_assetCache = assetCache;

            m_networkServer = packServer;
            m_inventoryCache = inventoryCache;
            m_authenticateSessionsHandler = authenSessions;

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "OpenSimClient.cs - Started up new client thread to handle incoming request");
            cirpack = initialcirpack;
            userEP = remoteEP;

            this.m_child = m_authenticateSessionsHandler.GetAgentChildStatus(initialcirpack.CircuitCode.Code);
            this.startpos = m_authenticateSessionsHandler.GetPosition(initialcirpack.CircuitCode.Code);

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
            //this.m_world.RemoveViewerAgent(this);

            this.startpos = m_authenticateSessionsHandler.GetPosition(CircuitCode);
            m_authenticateSessionsHandler.UpdateAgentChildStatus(CircuitCode, false);

            OnChildAgentStatus(this.m_child);
            //this.InitNewClient();
        }

        public void DowngradeClient()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "SimClient.cs:UpgradeClient() - changing full agent to child");
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
            //kill.ObjectData[0].ID = this.ClientAvatar.localid;
            foreach (ClientView client in m_clientThreads.Values)
            {
                client.OutPacket(kill);
            }

            this.m_inventoryCache.ClientLeaving(this.AgentID, null);


            //   m_world.RemoveViewerAgent(this);

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
        # endregion

        # region Setup

        protected virtual void InitNewClient()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "OpenSimClient.cs:InitNewClient() - Adding viewer agent to world");
            // this.ClientAvatar = m_world.AddViewerAgent(this);         
        }

        protected virtual void AuthUser()
        {
            // AuthenticateResponse sessionInfo = m_gridServer.AuthenticateSession(cirpack.CircuitCode.SessionID, cirpack.CircuitCode.ID, cirpack.CircuitCode.Code);
            AuthenticateResponse sessionInfo = this.m_authenticateSessionsHandler.AuthenticateSession(cirpack.CircuitCode.SessionID, cirpack.CircuitCode.ID, cirpack.CircuitCode.Code);
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
                InitNewClient();
                //this.ClientAvatar.firstname = sessionInfo.LoginInfo.First;
                // this.ClientAvatar.lastname = sessionInfo.LoginInfo.Last;
                if (sessionInfo.LoginInfo.SecureSession != LLUUID.Zero)
                {
                    this.SecureSessionID = sessionInfo.LoginInfo.SecureSession;
                }

                ClientLoop();
            }
        }
        # endregion


        protected override void KillThread()
        {
            this.ClientThread.Abort();
        }

        #region Inventory Creation
        private void SetupInventory(AuthenticateResponse sessionInfo)
        {

        }
        private AgentInventory CreateInventory(LLUUID baseFolder)
        {
            AgentInventory inventory = null;

            return inventory;
        }

        private void CreateInventoryItem(CreateInventoryItemPacket packet)
        {

        }
        #endregion

    }
}
