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
using OpenSim.world;
using OpenSim.Physics.Manager;
using OpenSim.Assets;
using libsecondlife;

namespace SimpleApp2
{
    class Program : IWorld, IAssetReceiver, conscmd_callback
    {
        private ConsoleBase m_console;
        private RegionInfo m_regionInfo;
        private float[] m_map;
        
        private void Run()
        {
            m_console = new ConsoleBase(null, "SimpleApp", this, false);
            MainConsole.Instance = m_console;

            m_map = CreateMap();
            
            CheckSumServer checksumServer = new CheckSumServer(12036);
            checksumServer.ServerListener();

            string simAddr = "127.0.0.1";
            int simPort = 9000;

            LoginServer loginServer = new LoginServer(simAddr, simPort, 0, 0, false);
            loginServer.Startup();

            AuthenticateSessionsLocal localSessions = new AuthenticateSessionsLocal();
            loginServer.SetSessionHandler(localSessions.AddNewSessionHandler);

            InventoryCache inventoryCache = new InventoryCache();

            LocalAssetServer assetServer = new LocalAssetServer();
            assetServer.SetServerInfo("http://127.0.0.1:8003/", "");
            assetServer.SetReceiver(this);

            AssetCache assetCache = new AssetCache(assetServer);

            UDPServer udpServer = new UDPServer(simPort, assetCache, inventoryCache, m_console, localSessions);
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

        private void AddNewSessionHandler(Login loginData)
        {
            m_console.WriteLine(LogPriority.NORMAL, "Recieved Login from [{0}] [{1}]", loginData.First, loginData.Last);
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
