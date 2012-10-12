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
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;

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

        public static int Main(string[] args)
        {
            m_Server = new HttpServerBase("R.O.B.U.S.T.", args);

            IConfig serverConfig = m_Server.Config.Configs["Startup"];
            if (serverConfig == null)
            {
                System.Console.WriteLine("Startup config section missing in .ini file");
                throw new Exception("Configuration error");
            }

            string connList = serverConfig.GetString("ServiceConnectors", String.Empty);

            IConfig servicesConfig = m_Server.Config.Configs["ServiceList"];
            if (servicesConfig != null)
            {
                List<string> servicesList = new List<string>();
                if (connList != String.Empty)
                    servicesList.Add(connList);

                foreach (string k in servicesConfig.GetKeys())
                {
                    string v = servicesConfig.GetString(k);
                    if (v != String.Empty)
                        servicesList.Add(v);
                }

                connList = String.Join(",", servicesList.ToArray());
            }

            string[] conns = connList.Split(new char[] {',', ' ', '\n', '\r', '\t'});

//            int i = 0;
            foreach (string c in conns)
            {
                if (c == String.Empty)
                    continue;

                string configName = String.Empty;
                string conn = c;
                uint port = 0;

                string[] split1 = conn.Split(new char[] {'/'});
                if (split1.Length > 1)
                {
                    conn = split1[1];

                    string[] split2 = split1[0].Split(new char[] {'@'});
                    if (split2.Length > 1)
                    {
                        configName = split2[0];
                        port = Convert.ToUInt32(split2[1]);
                    }
                    else
                    {
                        port = Convert.ToUInt32(split1[0]);
                    }
                }
                string[] parts = conn.Split(new char[] {':'});
                string friendlyName = parts[0];
                if (parts.Length > 1)
                    friendlyName = parts[1];

                IHttpServer server;

                if (port != 0)
                    server = MainServer.GetHttpServer(port);
                else
                    server = MainServer.Instance;

                m_log.InfoFormat("[SERVER]: Loading {0} on port {1}", friendlyName, server.Port);

                IServiceConnector connector = null;

                Object[] modargs = new Object[] { m_Server.Config, server, configName };
                connector = ServerUtils.LoadPlugin<IServiceConnector>(conn, modargs);

                if (connector == null)
                {
                    modargs = new Object[] { m_Server.Config, server };
                    connector = ServerUtils.LoadPlugin<IServiceConnector>(conn, modargs);
                }

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
            int res = m_Server.Run();

            Environment.Exit(res);

            return 0;
        }
    }
}
