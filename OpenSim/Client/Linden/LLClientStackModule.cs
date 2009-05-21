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
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Region.ClientStack;
using OpenSim.Region.ClientStack.LindenUDP;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Client.Linden
{
    public class LLClientStackModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region IRegionModule Members

        protected Scene m_scene;
        protected bool m_createClientStack = false;
        protected IClientNetworkServer m_clientServer;
        protected ClientStackManager m_clientStackManager;
        protected IConfigSource m_source;

        protected string m_clientStackDll = "OpenSim.Region.ClientStack.LindenUDP.dll";

        public void Initialise(IConfigSource source)
        {
            if (m_scene == null)
            {
                m_source = source;

                IConfig startupConfig = m_source.Configs["Startup"];
                if (startupConfig != null)
                {
                    m_clientStackDll = startupConfig.GetString("clientstack_plugin", "OpenSim.Region.ClientStack.LindenUDP.dll");
                }
            }
        }

        public void AddRegion(Scene scene)
        {

        }

        public void RemoveRegion(Scene scene)
        {

        }
        
        public void RegionLoaded(Scene scene)
        {
            if (m_scene == null)
            {
                m_scene = scene;
            }

            if ((m_scene != null) && (m_createClientStack))
            {
                m_log.Info("[LLClientStackModule] Starting up LLClientStack.");
                IPEndPoint endPoint = m_scene.RegionInfo.InternalEndPoint;

                uint port = (uint)endPoint.Port;
                m_clientStackManager = new ClientStackManager(m_clientStackDll);

                m_clientServer
                   = m_clientStackManager.CreateServer(endPoint.Address,
                     ref port, m_scene.RegionInfo.ProxyOffset, m_scene.RegionInfo.m_allow_alternate_ports, m_source,
                       m_scene.AuthenticateHandler);

                m_clientServer.AddScene(m_scene);

                m_clientServer.Start();
            }
        }

        public void Close()
        {

        }

        public string Name
        {
            get { return "LLClientStackModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion
    }
}
