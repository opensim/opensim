using System;
using System.Collections.Generic;
using System.Text;
using OpenSim;
using OpenSim.GridInterfaces.Local;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Console;
using OpenSim.Assets;
using libsecondlife;
using OpenSim.UserServer;
using OpenSim.Servers;
using OpenSim.Framework;
using OpenSim.Caches;
using OpenGrid.Framework.Communications;
using OpenSim.LocalCommunications;

namespace SimpleApp
{
    class Program : IAssetReceiver, conscmd_callback
    {
        private LogBase m_log;
        AuthenticateSessionsBase m_circuitManager;
        
        private void Run()
        {
            m_log = new LogBase(null, "SimpleApp", this, false);
            MainLog.Instance = m_log;

            CheckSumServer checksumServer = new CheckSumServer(12036);
            checksumServer.ServerListener();

            string simAddr = "127.0.0.1";
            int simPort = 9000;
            
            LoginServer loginServer = new LoginServer( simAddr, simPort, 0, 0, false );
            loginServer.Startup();
            loginServer.SetSessionHandler( AddNewSessionHandler );

            m_circuitManager = new AuthenticateSessionsBase();

            InventoryCache inventoryCache = new InventoryCache();

            LocalAssetServer assetServer = new LocalAssetServer();
            assetServer.SetServerInfo("http://127.0.0.1:8003/", "");
            assetServer.SetReceiver(this);

            AssetCache assetCache = new AssetCache(assetServer);
            
            UDPServer udpServer = new UDPServer(simPort, assetCache, inventoryCache, m_log, m_circuitManager );
            PacketServer packetServer = new PacketServer( udpServer, (uint) simPort );
            udpServer.ServerListener();
            
            ClientView.TerrainManager = new TerrainManager(new SecondLife());

            CommunicationsManager communicationsManager = new CommunicationsLocal();
            
            RegionInfo regionInfo = new RegionInfo( );

            udpServer.LocalWorld = new MyWorld( packetServer.ClientAPIs, regionInfo, m_circuitManager, communicationsManager, assetCache );

            // World world = new World(udpServer.PacketServer.ClientAPIs, regionInfo);            
            // PhysicsScene physicsScene = new NullPhysicsScene();
            // world.PhysicsScene = physicsScene;
            // udpServer.LocalWorld = world;

            BaseHttpServer httpServer = new BaseHttpServer( simPort );
            httpServer.AddXmlRPCHandler( "login_to_simulator", loginServer.XmlRpcLoginMethod );
            httpServer.Start();
            
            m_log.WriteLine( LogPriority.NORMAL, "Press enter to quit.");
            m_log.ReadLine();
        }

        private bool AddNewSessionHandler(ulong regionHandle, Login loginData)
        {
            m_log.WriteLine(LogPriority.NORMAL, "Region [{0}] recieved Login from [{1}] [{2}]", regionHandle, loginData.First, loginData.Last);

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
        
        #region IAssetReceiver Members

        public void AssetReceived( AssetBase asset, bool IsTexture)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public void AssetNotFound( AssetBase asset)
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

        static void Main(string[] args)
        {
            Program app = new Program();

            app.Run();
        }
    }
}
