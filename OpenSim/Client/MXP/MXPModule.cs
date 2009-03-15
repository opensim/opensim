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
    /**
     * MXP Client Module which adds MXP support to client / region communication.
     */
    public class MXPModule : IRegionModule
    {

        private int m_port = 1253;
        //private int m_ticks = 0;
        private bool m_shutdown = false;

        private IConfigSource m_config;
        private readonly Timer m_ticker = new Timer(100);
        private readonly Dictionary<UUID, Scene> m_scenes = new Dictionary<UUID, Scene>();

        private MXPPacketServer m_server;

        public void Initialise(Scene scene, IConfigSource source)
        {
            if (!m_scenes.ContainsKey(scene.RegionInfo.RegionID))
                m_scenes.Add(scene.RegionInfo.RegionID, scene);

            m_config = source;
        }

        public void PostInitialise()
        {
            if (m_config.Configs["MXP"] != null)
            {
                IConfig con = m_config.Configs["MXP"];

                if (!con.GetBoolean("Enabled", false))
                    return;

                m_port = con.GetInt("Port", m_port);

                m_server = new MXPPacketServer(m_port, m_scenes,m_config.Configs["StandAlone"].GetBoolean("accounts_authenticate",true));

                m_ticker.AutoReset = false;
                m_ticker.Elapsed += ticker_Elapsed;

                m_ticker.Start();
            }
        }

        void ticker_Elapsed(object sender, ElapsedEventArgs e)
        {
            m_server.Process();

            if (!m_shutdown)
                m_ticker.Start();

            // Commenting this at because of the excess flood to log.
            // TODO: Add ini file option.
            /*if (++ticks % 100 == 0)
            {
                server.PrintDebugInformation();
            }*/
        }

        public void Close()
        {
            m_shutdown = true;
            m_ticker.Stop();
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
