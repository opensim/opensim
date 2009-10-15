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

using System.Net;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.OptionalModules.Agent.InternetRelayClientView.Server;

namespace OpenSim.Region.OptionalModules.Agent.InternetRelayClientView
{
    public class IRCStackModule : IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IRCServer m_server;
        private Scene m_scene;

        #region Implementation of IRegionModule

        public void Initialise(Scene scene, IConfigSource source)
        {
            if (null != source.Configs["IRCd"] &&
                source.Configs["IRCd"].GetBoolean("Enabled",false))
            {
                int portNo = source.Configs["IRCd"].GetInt("Port",6666);
                m_scene = scene;
                m_server = new IRCServer(IPAddress.Parse("0.0.0.0"), portNo, scene);
                m_server.OnNewIRCClient += m_server_OnNewIRCClient;
            }
        }

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

        public void PostInitialise()
        {

        }

        public void Close()
        {

        }

        public string Name
        {
            get { return "IRCClientStackModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion
    }
}
