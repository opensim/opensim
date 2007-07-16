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
    class Program : RegionApplicationBase, conscmd_callback
    {
        AuthenticateSessionsBase m_circuitManager;

        public MyWorld m_world;
        private SceneObject m_sceneObject;
        public MyNpcCharacter m_character;

        protected override LogBase CreateLog()
        {
            return new LogBase(null, "SimpleApp", this, false);
        }

        protected override void Initialize()
        {
        }
        
        public void Run()
        {
            base.StartUp();

            string ipaddr = "192.168.0.109";

            MainLog.Instance = m_log;

            m_circuitManager = new AuthenticateSessionsBase();

            InventoryCache inventoryCache = new InventoryCache();

            LocalAssetServer assetServer = new LocalAssetServer();
            assetServer.SetServerInfo("http://" + ipaddr + ":8003/", "");

            AssetCache assetCache = new AssetCache(assetServer);

            ScenePresence.LoadTextureFile("avatar-texture.dat");
            ScenePresence.PhysicsEngineFlying = true;

            IPEndPoint internalEndPoint = new IPEndPoint(IPAddress.Parse(ipaddr), 9000);

            UDPServer udpServer = new UDPServer(internalEndPoint.Port, assetCache, inventoryCache, m_log, m_circuitManager);
            PacketServer packetServer = new PacketServer(udpServer);

            BaseHttpServer httpServer = new BaseHttpServer(internalEndPoint.Port);

            CommunicationsLocal communicationsManager = new CommunicationsLocal(m_networkServersInfo, httpServer);

            RegionInfo regionInfo = new RegionInfo(1000, 1000, internalEndPoint, ipaddr);

            OpenSim.Region.Environment.StorageManager storeMan = new OpenSim.Region.Environment.StorageManager("OpenSim.DataStore.NullStorage.dll", "simpleapp.yap", "simpleapp");

            m_world = new MyWorld( regionInfo, m_circuitManager, communicationsManager, assetCache, storeMan, httpServer);
            m_world.PhysScene = GetPhysicsScene( );
           
            m_world.LoadWorldMap();
            m_world.PhysScene.SetTerrain(m_world.Terrain.getHeights1D());
            m_world.performParcelPrimCountUpdate();

            udpServer.LocalWorld = m_world;

            httpServer.Start();
            udpServer.ServerListener();

            UserProfileData masterAvatar = communicationsManager.UserServer.SetupMasterUser("Test", "User", "test");
            if (masterAvatar != null)
            {
                m_world.RegionInfo.MasterAvatarAssignedUUID = masterAvatar.UUID;
                m_world.LandManager.NoLandDataFromStorage();
            }

            m_world.StartTimer();

            PrimitiveBaseShape shape = PrimitiveBaseShape.DefaultBox();
            shape.Scale = new LLVector3(0.5f, 0.5f, 0.5f);
            LLVector3 pos = new LLVector3(138, 129, 27);

            m_sceneObject = new MySceneObject(m_world, m_world.EventManager, LLUUID.Zero, m_world.PrimIDAllocate(), pos, shape);
            m_world.AddEntity(m_sceneObject);

            m_character = new MyNpcCharacter();
            m_world.AddNewClient(m_character, false);
          
            m_log.WriteLine(LogPriority.NORMAL, "Press enter to quit.");
            m_log.ReadLine();
            
        }

        protected override PhysicsScene GetPhysicsScene( )
        {
            return GetPhysicsScene("basicphysics");
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

            //app.StartUp();
            app.Run();
        }
    }
}
