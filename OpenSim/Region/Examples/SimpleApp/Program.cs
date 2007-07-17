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
using OpenSim.Region.Environment;

namespace SimpleApp
{
    class Program : RegionApplicationBase, conscmd_callback
    {
        protected override LogBase CreateLog()
        {
            return new LogBase(null, "SimpleApp", this, false);
        }

        protected override void Initialize()
        {
            m_httpServerPort = 9000;

            StartLog();

            LocalAssetServer assetServer = new LocalAssetServer();
            assetServer.SetServerInfo("http://localhost:8003/", "");

            m_assetCache = new AssetCache(assetServer);
        }
        
        public void Run()
        {
            base.StartUp();

            m_commsManager = new CommunicationsLocal(m_networkServersInfo, m_httpServer);

            ScenePresence.PhysicsEngineFlying = true;

            IPEndPoint internalEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9000);
            RegionInfo regionInfo = new RegionInfo(1000, 1000, internalEndPoint, "localhost");
            regionInfo.DataStore = "simpleapp_datastore.yap";
            
            UDPServer udpServer;

            Scene scene = SetupScene(regionInfo, out udpServer);
            
            udpServer.ServerListener();
            
            PrimitiveBaseShape shape = PrimitiveBaseShape.DefaultBox();
            shape.Scale = new LLVector3(0.5f, 0.5f, 0.5f);
            LLVector3 pos = new LLVector3(138, 129, 27);

            SceneObject m_sceneObject = new MySceneObject(scene, scene.EventManager, LLUUID.Zero, scene.PrimIDAllocate(), pos, shape);
            scene.AddEntity(m_sceneObject);

            MyNpcCharacter m_character = new MyNpcCharacter();
            scene.AddNewClient(m_character, false);
          
            m_log.WriteLine(LogPriority.NORMAL, "Press enter to quit.");
            m_log.ReadLine();            
        }

        protected override Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager, AgentCircuitManager circuitManager)
        {
            return new MyWorld(regionInfo, circuitManager, m_commsManager, m_assetCache, storageManager, m_httpServer);
        }

        protected override StorageManager CreateStorageManager(RegionInfo regionInfo)
        {
            return new StorageManager("OpenSim.DataStore.NullStorage.dll", "simpleapp.yap", "simpleapp");
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

            app.Run();
        }
    }
}
