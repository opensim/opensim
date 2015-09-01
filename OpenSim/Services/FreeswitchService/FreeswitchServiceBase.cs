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
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Base;
using log4net;

namespace OpenSim.Services.FreeswitchService
{
    public class FreeswitchServiceBase : ServiceBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected string m_freeSwitchRealm;
        protected string m_freeSwitchSIPProxy;
        protected bool m_freeSwitchAttemptUseSTUN = false;
        protected string m_freeSwitchEchoServer;
        protected int m_freeSwitchEchoPort = 50505;
        protected string m_freeSwitchDefaultWellKnownIP;
        protected int m_freeSwitchDefaultTimeout = 5000;
        protected string m_freeSwitchContext = "default";
        protected string m_freeSwitchServerUser = "freeswitch";
        protected string m_freeSwitchServerPass = "password";
        protected readonly string m_freeSwitchAPIPrefix = "/fsapi";

        protected bool m_Enabled = false;

        public FreeswitchServiceBase(IConfigSource config) : base(config)
        {
            //
            // Try reading the [FreeswitchService] section first, if it exists
            //
            IConfig freeswitchConfig = config.Configs["FreeswitchService"];
            if (freeswitchConfig != null)
            {
                m_freeSwitchDefaultWellKnownIP = freeswitchConfig.GetString("ServerAddress", String.Empty);
                if (m_freeSwitchDefaultWellKnownIP == String.Empty)
                {
                    m_log.Error("[FREESWITCH]: No ServerAddress given, cannot start service.");
                    return;
                }

                m_freeSwitchRealm = freeswitchConfig.GetString("Realm", m_freeSwitchDefaultWellKnownIP);
                m_freeSwitchSIPProxy = freeswitchConfig.GetString("SIPProxy", m_freeSwitchDefaultWellKnownIP + ":5060");
                m_freeSwitchEchoServer = freeswitchConfig.GetString("EchoServer", m_freeSwitchDefaultWellKnownIP);
                m_freeSwitchEchoPort = freeswitchConfig.GetInt("EchoPort", m_freeSwitchEchoPort);
                m_freeSwitchAttemptUseSTUN = freeswitchConfig.GetBoolean("AttemptSTUN", false); // This may not work
                m_freeSwitchDefaultTimeout = freeswitchConfig.GetInt("DefaultTimeout", m_freeSwitchDefaultTimeout);
                m_freeSwitchContext = freeswitchConfig.GetString("Context", m_freeSwitchContext);
                m_freeSwitchServerUser = freeswitchConfig.GetString("UserName", m_freeSwitchServerUser);
                m_freeSwitchServerPass = freeswitchConfig.GetString("Password", m_freeSwitchServerPass);

                m_Enabled = true;
            }
        }
    }
}
