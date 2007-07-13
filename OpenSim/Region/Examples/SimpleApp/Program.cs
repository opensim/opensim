using System;
using System.Net;
using libsecondlife;
using OpenSim.Assets;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Physics.Manager;
using OpenSim.Region.Caches;
using OpenSim.Region.Capabilities;
using OpenSim.Region.ClientStack;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.GridInterfaces.Local;

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

            PhysicsManager physManager = new PhysicsManager();
            physManager.LoadPlugins();
            
            UDPServer udpServer = new UDPServer( internalEndPoint.Port, assetCache, inventoryCache, m_log, m_circuitManager );
            PacketServer packetServer = new PacketServer(udpServer);
            udpServer.ServerListener();
            
            ClientView.TerrainManager = new TerrainManager(new SecondLife());
            BaseHttpServer httpServer = new BaseHttpServer(internalEndPoint.Port);

            NetworkServersInfo serverInfo = new NetworkServersInfo();
            CommunicationsLocal communicationsManager = new CommunicationsLocal(serverInfo, httpServer);

            RegionInfo regionInfo = new RegionInfo( 1000, 1000, internalEndPoint, "127.0.0.1" );

            MyWorld world = new MyWorld(packetServer.ClientManager, regionInfo, m_circuitManager, communicationsManager, assetCache, httpServer);
            world.PhysScene = physManager.GetPhysicsScene("basicphysics");  //PhysicsScene.Null;
            udpServer.LocalWorld = world;
            
            httpServer.Start();
            
            m_log.WriteLine( LogPriority.NORMAL, "Press enter to quit.");
            m_log.ReadLine();

            PrimitiveBaseShape shape = PrimitiveBaseShape.DefaultBox();
            
            shape.Scale = new LLVector3(10, 10, 10);
            
            LLVector3 pos = new LLVector3(128,128,72);

            world.AddNewPrim( LLUUID.Zero, pos, shape );

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
