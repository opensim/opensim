/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

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

                if (!con.GetBoolean("Enabled", false))
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

            if (++ticks % 100 == 0)
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
