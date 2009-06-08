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

using Nini.Config;
using log4net;
using System.Reflection;
using System;
using System.Collections.Generic;
using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Server.Handlers.Asset;

namespace OpenSim.Server
{
    public class OpenSimServer
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        protected static HttpServerBase m_Server = null;

        protected static List<IServiceConnector> m_ServiceConnectors =
                new List<IServiceConnector>();

        static int Main(string[] args)
        {
            m_Server = new HttpServerBase("Server", args);

            IConfig serverConfig = m_Server.Config.Configs["Startup"];
            if (serverConfig == null)
            {
                System.Console.WriteLine("Startup config section missing in .ini file");
                throw new Exception("Configuration error");
            }

            string connList = serverConfig.GetString("ServiceConnectors", String.Empty);
            string[] conns = connList.Split(new char[] {',', ' '});

            foreach (string conn in conns)
            {
                if (conn == String.Empty)
                    continue;

                string[] parts = conn.Split(new char[] {':'});
                string friendlyName = parts[0];
                if (parts.Length > 1)
                    friendlyName = parts[1];

                m_log.InfoFormat("[SERVER]: Loading {0}", friendlyName);

                Object[] modargs = new Object[] { m_Server.Config, m_Server.HttpServer };
                IServiceConnector connector =
                        ServerUtils.LoadPlugin<IServiceConnector>(conn,
                        modargs);

                if (connector != null)
                {
                    m_ServiceConnectors.Add(connector);
                    m_log.InfoFormat("[SERVER]: {0} loaded successfully", friendlyName);
                }
                else
                {
                    m_log.InfoFormat("[SERVER]: Failed to load {0}", conn);
                }
            }
            return m_Server.Run();
        }
    }
}
