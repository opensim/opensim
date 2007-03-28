using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
//using System.Net.Sockets;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.world;
using OpenSim.Framework.Interfaces;
using OpenSim.UserServer;
using OpenSim.Assets;
using OpenSim.CAPS;
using OpenSim.Framework.Console;
using OpenSim.Physics.Manager;

namespace OpenSim
{
    public sealed class OpenSimRoot
    {
        private static OpenSimRoot instance = new OpenSimRoot();

        public static OpenSimRoot Instance
        {
            get
            {
                return instance;
            }
        }

        private OpenSimRoot()
        {
             
        }

        public World LocalWorld;
        public Grid GridServers;
        public SimConfig Cfg;
        public SimCAPSHTTPServer HttpServer;
        public AssetCache AssetCache;
        public InventoryCache InventoryCache;
        //public Dictionary<EndPoint, SimClient> ClientThreads = new Dictionary<EndPoint, SimClient>();
        public Dictionary<uint, SimClient> ClientThreads = new Dictionary<uint, SimClient>();
        public DateTime startuptime;
        public OpenSimApplication Application;
        public bool Sandbox = false;

    }
}
