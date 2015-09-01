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
using System.Net;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.OptionalModules.Agent.InternetRelayClientView.Server;

using Mono.Addins;

namespace OpenSim.Region.OptionalModules.Agent.InternetRelayClientView
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "IRCStackModule")]
    public class IRCStackModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IRCServer m_server;
        private int m_Port;
//        private Scene m_scene;
        private bool m_Enabled;

        #region Implementation of INonSharedRegionModule

        public void Initialise(IConfigSource source)
        {
            if (null != source.Configs["IRCd"] &&
                source.Configs["IRCd"].GetBoolean("Enabled", false))
            {
                m_Enabled = true;
                m_Port = source.Configs["IRCd"].GetInt("Port", 6666);
            }
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_server = new IRCServer(IPAddress.Parse("0.0.0.0"), m_Port, scene);
            m_server.OnNewIRCClient += m_server_OnNewIRCClient;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "IRCClientStackModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        void m_server_OnNewIRCClient(IRCClientView user)
        {
            user.OnIRCReady += user_OnIRCReady;
        }

        void user_OnIRCReady(IRCClientView cv)
        {
            m_log.Info("[IRCd] Adding user...");
            cv.Start();
            m_log.Info("[IRCd] Added user to Scene");
        }

    }
}
