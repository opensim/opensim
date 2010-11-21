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
using System.Collections;
using System.Web;
using System.Reflection;
using Nini.Config;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;
using log4net;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Server.Handlers.Freeswitch
{
    public class FreeswitchServerConnector : ServiceConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IFreeswitchService m_FreeswitchService;
        private string m_ConfigName = "FreeswitchService";
        protected readonly string m_freeSwitchAPIPrefix = "/fsapi";

        public FreeswitchServerConnector(IConfigSource config, IHttpServer server, string configName) :
                base(config, server, configName)
        {
            if (configName != String.Empty)
                m_ConfigName = configName;

            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section '{0}' in config file", m_ConfigName));

            string freeswitchService = serverConfig.GetString("LocalServiceModule",
                    String.Empty);

            if (freeswitchService == String.Empty)
                throw new Exception("No LocalServiceModule in config file");

            Object[] args = new Object[] { config };
            m_FreeswitchService =
                    ServerUtils.LoadPlugin<IFreeswitchService>(freeswitchService, args);

            server.AddHTTPHandler(String.Format("{0}/freeswitch-config", m_freeSwitchAPIPrefix), FreeSwitchConfigHTTPHandler);
            server.AddHTTPHandler(String.Format("{0}/region-config", m_freeSwitchAPIPrefix), RegionConfigHTTPHandler);
        }

        public Hashtable FreeSwitchConfigHTTPHandler(Hashtable request)
        {
            Hashtable response = new Hashtable();
            response["str_response_string"] = string.Empty;
            response["content_type"] = "text/plain";
            response["keepalive"] = false;
            response["int_response_code"] = 500;

            Hashtable requestBody = ParseRequestBody((string) request["body"]);

            string section = (string) requestBody["section"];

            if (section == "directory")
                response = m_FreeswitchService.HandleDirectoryRequest(requestBody);
            else if (section == "dialplan")
                response = m_FreeswitchService.HandleDialplanRequest(requestBody);
            else
                m_log.WarnFormat("[FreeSwitchVoice]: section was {0}", section);

            return response;
        }

        private Hashtable ParseRequestBody(string body)
        {
            Hashtable bodyParams = new Hashtable();
            // split string
            string [] nvps = body.Split(new Char [] {'&'});

            foreach (string s in nvps)
            {
                if (s.Trim() != "")
                {
                    string [] nvp = s.Split(new Char [] {'='});
                    bodyParams.Add(HttpUtility.UrlDecode(nvp[0]), HttpUtility.UrlDecode(nvp[1]));
                }
            }

            return bodyParams;
        }

        public Hashtable RegionConfigHTTPHandler(Hashtable request)
        {
            Hashtable response = new Hashtable();
            response["content_type"] = "text/json";
            response["keepalive"] = false;
            response["int_response_code"] = 200;

            response["str_response_string"] = m_FreeswitchService.GetJsonConfig();

            return response;
        }

    }
}
