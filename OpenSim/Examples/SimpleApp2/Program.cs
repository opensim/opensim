using System;
using System.Collections.Generic;
using System.Text;
using OpenSim;
using OpenSim.Servers;
using OpenSim.GridInterfaces.Local;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.UserServer;
using OpenSim.Framework.Console;
using OpenSim.Assets;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Caches;

namespace SimpleApp2
{
    class Program : IWorld, IAssetReceiver, conscmd_callback
    {
        private ConsoleBase m_console;
        private RegionInfo m_regionInfo;
        private float[] m_map;
        private AuthenticateSessionsBase m_circuitManager;
        
        private void Run()
        {
            m_console = new ConsoleBase(null, "SimpleApp", this, false);
            MainConsole.Instance = m_console;

            m_map = CreateMap();
            
            CheckSumServer checksumServer = new CheckSumServer(12036);
            checksumServer.ServerListener();

            string simAddr = "127.0.0.1";
            int simPort = 9000;

            m_circuitManager = new AuthenticateSessionsBase();            
            
            LoginServer loginServer = new LoginServer(simAddr, simPort, 0, 0, false);
            loginServer.Startup();

            loginServer.SetSessionHandler( AddNewSessionHandler );

            InventoryCache inventoryCache = new InventoryCache();

            LocalAssetServer assetServer = new LocalAssetServer();
            assetServer.SetServerInfo("http://127.0.0.1:8003/", "");
            assetServer.SetReceiver(this);

            AssetCache assetCache = new AssetCache(assetServer);

            UDPServer udpServer = new UDPServer(simPort, assetCache, inventoryCache, m_console, m_circuitManager );
            PacketServer packetServer = new MyPacketServer(m_map, udpServer, (uint) simPort );
            udpServer.ServerListener();

            ClientView.TerrainManager = new TerrainManager(new SecondLife());

            m_regionInfo = new RegionInfo();

            udpServer.LocalWorld = this;

            // World world = new World(udpServer.PacketServer.ClientAPIs, regionInfo);            
            // PhysicsScene physicsScene = new NullPhysicsScene();
            // world.PhysicsScene = physicsScene;
            // udpServer.LocalWorld = world;

            BaseHttpServer httpServer = new BaseHttpServer(simPort);
            httpServer.AddXmlRPCHandler("login_to_simulator", loginServer.XmlRpcLoginMethod);
            httpServer.Start();

            m_console.WriteLine(LogPriority.NORMAL, "Press enter to quit.");
            m_console.ReadLine();
        }

        private float[] CreateMap()
        {
            float[] map = new float[65536];

            for (int i = 0; i < 65536; i++)
            {
                int x = i % 256;
                int y = i / 256;

                map[i] = (float)(x + y / 2);
            }
            
            return map;
        }

        private bool AddNewSessionHandler(ulong regionHandle, Login loginData)
        {
            m_console.WriteLine(LogPriority.NORMAL, "Region [{0}] recieved Login from [{1}] [{2}]", regionHandle, loginData.First, loginData.Last);

            AgentCircuitData agent = new AgentCircuitData();
            agent.AgentID = loginData.Agent;
            agent.firstname = loginData.First;
            agent.lastname = loginData.Last;
            agent.SessionID = loginData.Session;
            agent.SecureSessionID = loginData.SecureSession;
            agent.circuitcode = loginData.CircuitCode;
            agent.BaseFolder = loginData.BaseFolder;
            agent.InventoryFolder = loginData.InventoryFolder;
            agent.startpos = new LLVector3(128, 128, 70);

            m_circuitManager.AddNewCircuit(agent.circuitcode, agent);

            return true;
        }

        static void Main(string[] args)
        {
            Program app = new Program();

            app.Run();
        }


        #region IWorld Members

        void IWorld.AddNewAvatar(IClientAPI remoteClient, LLUUID agentID, bool child)
        {
            remoteClient.SendRegionHandshake(m_regionInfo);
        }

        void IWorld.RemoveAvatar(LLUUID agentID)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        RegionInfo IWorld.RegionInfo
        {
            get { return m_regionInfo; }
        }

        object IWorld.SyncRoot
        {
            get { return this; }
        }

        private uint m_nextLocalId = 1;

        uint IWorld.NextLocalId
        {
            get { return m_nextLocalId++; }
        }

        #endregion

        #region IAssetReceiver Members

        public void AssetReceived(AssetBase asset, bool IsTexture)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void AssetNotFound(AssetBase asset)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion

        #region conscmd_callback Members

        public void RunCmd(string cmd, string[] cmdparams)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void Show(string ShowWhat)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        #endregion
    }
}
