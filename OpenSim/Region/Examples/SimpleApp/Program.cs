using System;
using System.Collections.Generic;
using System.Text;
using OpenSim;
using OpenSim.Region.GridInterfaces.Local;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Assets;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Caches;
using OpenSim.Framework.Communications;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.ClientStack;
using System.Net;
using libsecondlife.Packets;
using OpenSim.Physics.Manager;

namespace SimpleApp
{
    class Program : IAssetReceiver, conscmd_callback
    {
        private LogBase m_log;
        AuthenticateSessionsBase m_circuitManager;
        uint m_localId;
        
        private void Run()
        {
            m_log = new LogBase(null, "SimpleApp", this, false);
            MainLog.Instance = m_log;

          //  CheckSumServer checksumServer = new CheckSumServer(12036);
           // checksumServer.ServerListener();

            IPEndPoint internalEndPoint = new IPEndPoint( IPAddress.Parse( "127.0.0.1" ), 9000 );

            m_circuitManager = new AuthenticateSessionsBase();

            InventoryCache inventoryCache = new InventoryCache();

            LocalAssetServer assetServer = new LocalAssetServer();
            assetServer.SetServerInfo("http://127.0.0.1:8003/", "");
            assetServer.SetReceiver(this);

            AssetCache assetCache = new AssetCache(assetServer);
            
            UDPServer udpServer = new UDPServer( internalEndPoint.Port, assetCache, inventoryCache, m_log, m_circuitManager );
            PacketServer packetServer = new PacketServer(udpServer);
            udpServer.ServerListener();
            
            ClientView.TerrainManager = new TerrainManager(new SecondLife());

            NetworkServersInfo serverInfo = new NetworkServersInfo();
            CommunicationsLocal communicationsManager = new CommunicationsLocal(serverInfo);

            RegionInfo regionInfo = new RegionInfo( 1000, 1000, internalEndPoint, "localhost" );

            BaseHttpServer httpServer = new BaseHttpServer( internalEndPoint.Port );
            MyWorld world = new MyWorld(packetServer.ClientAPIs, regionInfo, m_circuitManager, communicationsManager, assetCache, httpServer);
            world.PhysScene = PhysicsScene.Null;
            udpServer.LocalWorld = world;

            httpServer.AddXmlRPCHandler("login_to_simulator", communicationsManager.UserServices.XmlRpcLoginMethod );
            httpServer.Start();
            
            m_log.WriteLine( LogPriority.NORMAL, "Press enter to quit.");
            m_log.ReadLine();

            PrimData primData = new PrimData();
            primData.Scale = new LLVector3(1, 1, 1);

            m_localId = world.AddNewPrim( LLUUID.Zero, primData, LLVector3.Zero, new LLQuaternion(0, 0, 0, 0), LLUUID.Zero, 0);

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
