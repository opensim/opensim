using System;
using System.Collections.Generic;
using System.Net;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Region.Physics.Manager;
 
using OpenSim.Region.Capabilities;
using OpenSim.Region.ClientStack;
using OpenSim.Region.Communications.Local;
using OpenSim.Framework.Communications.Caches;
using System.Timers;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework.Data;
using OpenSim.Region.Environment;
using System.IO;

namespace SimpleApp
{
    class Program : RegionApplicationBase, conscmd_callback
    {
        private ModuleLoader m_moduleLoader;
        protected override LogBase CreateLog()
        {
            return new LogBase(null, "SimpleApp", this, false);
        }

        protected override void Initialize()
        {
            m_httpServerPort = 9000;

            StartLog();

            m_networkServersInfo = new NetworkServersInfo( 1000, 1000 );

            LocalAssetServer assetServer = new LocalAssetServer();
            assetServer.SetServerInfo("http://localhost:8003/", "");

            m_assetCache = new AssetCache(assetServer);
        }
        
        public void Run()
        {
            base.StartUp();

            CommunicationsLocal.LocalSettings settings = new CommunicationsLocal.LocalSettings("", false, "", "");
            m_commsManager = new CommunicationsLocal(m_networkServersInfo, m_httpServer, m_assetCache, settings);

            m_log.Notice(m_log.LineInfo);
            
            IPEndPoint internalEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9000);
           
            RegionInfo regionInfo = new RegionInfo(1000, 1000, internalEndPoint, "localhost");
            regionInfo.DataStore = "simpleapp_datastore.yap";
            
            UDPServer udpServer;

            m_moduleLoader = new ModuleLoader();
            m_moduleLoader.LoadDefaultSharedModules("");

            Scene scene = SetupScene(regionInfo, out udpServer);

            m_moduleLoader.InitialiseSharedModules(scene);
            m_moduleLoader.CreateDefaultModules(scene, "");
            scene.SetModuleInterfaces();

            scene.StartTimer();

            m_sceneManager.Add(scene);

            m_moduleLoader.PostInitialise();
            m_moduleLoader.ClearCache();
            
            udpServer.ServerListener();
            
            LLVector3 pos = new LLVector3(110, 129, 27);

            SceneObjectGroup sceneObject = new CpuCounterObject(scene, regionInfo.RegionHandle, LLUUID.Zero, scene.PrimIDAllocate(), pos + new LLVector3( 1f, 1f, 1f ));
            scene.AddEntity(sceneObject);

            for (int i = 0; i < 27; i++)
            {
                LLVector3 posOffset = new LLVector3( (i%3)*4, (i%9)/3 * 4, (i/9) * 4 );
                ComplexObject complexObject = new ComplexObject(scene, regionInfo.RegionHandle, LLUUID.Zero, scene.PrimIDAllocate(), pos + posOffset );
                scene.AddEntity(complexObject);
            }

            for (int i = 0; i < 2; i++)
            {
                MyNpcCharacter m_character = new MyNpcCharacter(scene.EventManager);
                scene.AddNewClient(m_character, false);
            }

            List<ScenePresence> avatars = scene.GetAvatars();
            foreach (ScenePresence avatar in avatars)
            {
                avatar.AbsolutePosition = new LLVector3((float)OpenSim.Framework.Utilities.Util.RandomClass.Next(100,200), (float)OpenSim.Framework.Utilities.Util.RandomClass.Next(30, 200), 2);                
            }

          
       
            DirectoryInfo dirInfo = new DirectoryInfo( "." );

            float x = 0;
            float z = 0;
            
            foreach( FileInfo fileInfo in dirInfo.GetFiles())
            {
                LLVector3 filePos = new LLVector3(100 + x, 129, 27 + z);
                x = x + 2;
                if( x > 50 )
                {
                    x = 0;
                    z = z + 2;
                }
                
                FileSystemObject fileObject = new FileSystemObject( scene, fileInfo, filePos );
                scene.AddEntity(fileObject);
            }
            
            m_log.Notice("Press enter to quit.");
            m_log.ReadLine();            
        }

        protected override Scene CreateScene(RegionInfo regionInfo, StorageManager storageManager, AgentCircuitManager circuitManager)
        {
            return new MyWorld(regionInfo, circuitManager, m_commsManager, m_assetCache, storageManager, m_httpServer, new ModuleLoader());
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
