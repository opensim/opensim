using System;
using System.Text;
using System.IO;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Timers;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.world;
using OpenSim.Terrain;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.UserServer;
using OpenSim.Assets;
using OpenSim.CAPS;
using OpenSim.Framework.Console;
using OpenSim.Physics.Manager;
using Nwc.XmlRpc;
using OpenSim.Servers;
using OpenSim.GenericConfig;

namespace OpenSim
{
    public class RegionServerBase
    {
        protected IGenericConfig localConfig;
        protected PhysicsManager physManager;
        protected Grid GridServers;
        protected AssetCache AssetCache;
        protected InventoryCache InventoryCache;
        protected Dictionary<EndPoint, uint> clientCircuits = new Dictionary<EndPoint, uint>();
        protected DateTime startuptime;
        protected RegionInfo regionData;

        protected System.Timers.Timer m_heartbeatTimer = new System.Timers.Timer();
        public string m_physicsEngine;
        public bool m_sandbox = false;
        public bool m_loginserver;
        public bool user_accounts = false;
        public bool gridLocalAsset = false;
        protected bool configFileSetup = false;
        public string m_config;

        protected UDPServer m_udpServer;
        protected BaseHttpServer httpServer;
        protected AuthenticateSessionsBase AuthenticateSessionsHandler;

        protected ConsoleBase m_console;

        public RegionServerBase()
        {

        }

        public RegionServerBase(bool sandBoxMode, bool startLoginServer, string physicsEngine, bool useConfigFile, bool silent, string configFile)
        {
            this.configFileSetup = useConfigFile;
            m_sandbox = sandBoxMode;
            m_loginserver = startLoginServer;
            m_physicsEngine = physicsEngine;
            m_config = configFile;
        }

        protected World m_localWorld;
        public World LocalWorld
        {
            get { return m_localWorld; }
        }

        /// <summary>
        /// Performs initialisation of the world, such as loading configuration from disk.
        /// </summary>
        public virtual void StartUp()
        {
        }

        protected virtual void SetupLocalGridServers()
        {
        }

        protected virtual void SetupRemoteGridServers()
        {

        }

        protected virtual void SetupLocalWorld()
        {
        }

        protected virtual void SetupHttpListener()
        {
        }

        protected virtual void ConnectToRemoteGridServer()
        {

        }
    }
}
