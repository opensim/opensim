using System;
using System.Collections.Generic;
using System.Text;
using System.Timers;
using MXP;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Client.MXP.PacketHandler;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Client.MXP
{
    public class MXPModule : IRegionModule
    {
        private int mxp_Port = 1253;
        private double mxp_BubbleRadius = 181.01933598375616624661615669884; // Radius of a sphere big enough to encapsulate a 256x256 square

        private readonly Timer ticker = new Timer(100);

        private int ticks;
        private bool shutdown = false;

        private IConfigSource config;

        private readonly Dictionary<UUID,Scene> m_scenes = new Dictionary<UUID, Scene>();

        private MXPPacketServer server;


        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scenes.Add(scene.RegionInfo.RegionID, scene);
            config = source;
        }

        public void PostInitialise()
        {
            if (config.Configs["MXP"] != null)
            {
                IConfig con = config.Configs["MXP"];

                if(!con.GetBoolean("Enabled",false))
                    return;

                mxp_Port = con.GetInt("Port", mxp_Port);


                server = new MXPPacketServer("http://null", mxp_Port, m_scenes);

                ticker.AutoReset = false;
                ticker.Elapsed += ticker_Elapsed;

                ticker.Start();
            }
        }

        void ticker_Elapsed(object sender, ElapsedEventArgs e)
        {
            server.Process();

            if (!shutdown)
                ticker.Start();

            if(++ticks % 100 == 0)
            {
                server.PrintDebugInformation();
            }
        }

        public void Close()
        {
            shutdown = true;
            ticker.Stop();
        }

        public string Name
        {
            get { return "MXP ClientStack Module"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
    }
}
