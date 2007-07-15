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
using System.Timers;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework.Data;

namespace SimpleApp
{
    class Program : conscmd_callback
    {
        private LogBase m_log;
        AuthenticateSessionsBase m_circuitManager;
        uint m_localId;
        public MyWorld world;
        private SceneObject m_sceneObject;
        public MyNpcCharacter m_character;
        
        private void Run()
        {
            m_log = new LogBase(null, "SimpleApp", this, false);
            MainLog.Instance = m_log;

            //  CheckSumServer checksumServer = new CheckSumServer(12036);
            // checksumServer.ServerListener();

            IPEndPoint internalEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9000);

            m_circuitManager = new AuthenticateSessionsBase();

            InventoryCache inventoryCache = new InventoryCache();

            LocalAssetServer assetServer = new LocalAssetServer();
            assetServer.SetServerInfo("http://127.0.0.1:8003/", "");

            AssetCache assetCache = new AssetCache(assetServer);

            ScenePresence.LoadTextureFile("avatar-texture.dat");
            ScenePresence.PhysicsEngineFlying = true;

            PhysicsManager physManager = new PhysicsManager();
            physManager.LoadPlugins();

            UDPServer udpServer = new UDPServer(internalEndPoint.Port, assetCache, inventoryCache, m_log, m_circuitManager);
            PacketServer packetServer = new PacketServer(udpServer);

            ClientView.TerrainManager = new TerrainManager(new SecondLife());
            BaseHttpServer httpServer = new BaseHttpServer(internalEndPoint.Port);

            NetworkServersInfo serverInfo = new NetworkServersInfo();
            CommunicationsLocal communicationsManager = new CommunicationsLocal(serverInfo, httpServer);

            RegionInfo regionInfo = new RegionInfo(1000, 1000, internalEndPoint, "127.0.0.1");

            OpenSim.Region.Environment.StorageManager storeMan = new OpenSim.Region.Environment.StorageManager("OpenSim.DataStore.NullStorage.dll", "simpleapp.yap", "simpleapp");

            world = new MyWorld( regionInfo, m_circuitManager, communicationsManager, assetCache, storeMan, httpServer);
            world.PhysScene = physManager.GetPhysicsScene("basicphysics");  //PhysicsScene.Null;
           
            world.LoadWorldMap();
            world.PhysScene.SetTerrain(world.Terrain.getHeights1D());

            udpServer.LocalWorld = world;

            httpServer.Start();
            udpServer.ServerListener();

            UserProfileData masterAvatar = communicationsManager.UserServer.SetupMasterUser("Test", "User", "test");
            if (masterAvatar != null)
            {
                world.RegionInfo.MasterAvatarAssignedUUID = masterAvatar.UUID;
                world.ParcelManager.NoParcelDataFromStorage();
            }

            world.StartTimer();

            PrimitiveBaseShape shape = PrimitiveBaseShape.DefaultBox();
            shape.Scale = new LLVector3(0.5f, 0.5f, 0.5f);
            LLVector3 pos = new LLVector3(138, 129, 27);

            m_sceneObject = new MySceneObject(world, world.EventManager, LLUUID.Zero, world.PrimIDAllocate(), pos, shape);
            world.AddNewEntity(m_sceneObject);

            m_character = new MyNpcCharacter();
            world.AddNewClient(m_character, false);
          
            m_log.WriteLine(LogPriority.NORMAL, "Press enter to quit.");
            m_log.ReadLine();
            
        }

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
