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
using System.IO;
using System.Net;
using System.Reflection;
using System.Security;
using System.Text;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse.StructuredData;

namespace OpenSim.Server.Handlers.Grid
{
    public class GridInfoHandlers
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IConfigSource m_Config;
        private Hashtable _info = new Hashtable();

        /// <summary>
        /// Instantiate a GridInfoService object.
        /// </summary>
        /// <param name="configPath">path to config path containing
        /// grid information</param>
        /// <remarks>
        /// GridInfoService uses the [GridInfo] section of the
        /// standard OpenSim.ini file --- which is not optimal, but
        /// anything else requires a general redesign of the config
        /// system.
        /// </remarks>
        public GridInfoHandlers(IConfigSource configSource)
        {
            m_Config = configSource;
            loadGridInfo(configSource);
        }

        private void loadGridInfo(IConfigSource configSource)
        {
            _info["platform"] = "OpenSim";
            try
            {
                IConfig gridCfg = configSource.Configs["GridInfoService"];
                IConfig netCfg = configSource.Configs["Network"];

                if (null != gridCfg)
                {
                    foreach (string k in gridCfg.GetKeys())
                    {
                        _info[k] = gridCfg.GetString(k);
                    }
                }
                else if (null != netCfg)
                {
                    _info["login"]
                        = String.Format(
                            "http://127.0.0.1:{0}/",
                            netCfg.GetString(
                                "http_listener_port", ConfigSettings.DefaultRegionHttpPort.ToString()));

                    IssueWarning();
                }
                else
                {
                    _info["login"] = "http://127.0.0.1:9000/";
                    IssueWarning();
                }
            }
            catch (Exception)
            {
                _log.Warn("[GRID INFO SERVICE]: Cannot get grid info from config source, using minimal defaults");
            }

            _log.DebugFormat("[GRID INFO SERVICE]: Grid info service initialized with {0} keys", _info.Count);
        }

        private void IssueWarning()
        {
            _log.Warn("[GRID INFO SERVICE]: found no [GridInfo] section in your configuration files");
            _log.Warn("[GRID INFO SERVICE]: trying to guess sensible defaults, you might want to provide better ones:");

            foreach (string k in _info.Keys)
            {
                _log.WarnFormat("[GRID INFO SERVICE]: {0}: {1}", k, _info[k]);
            }
        }

        public XmlRpcResponse XmlRpcGridInfoMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = new Hashtable();

            _log.Debug("[GRID INFO SERVICE]: Request for grid info");

            foreach (string k in _info.Keys)
            {
                responseData[k] = _info[k];
            }
            response.Value = responseData;

            return response;
        }

        public string RestGetGridInfoMethod(string request, string path, string param,
                                            IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("<gridinfo>\n");
            foreach (string k in _info.Keys)
            {
                sb.AppendFormat("<{0}>{1}</{0}>\n", k, SecurityElement.Escape(_info[k].ToString()));
            }
            sb.Append("</gridinfo>\n");

            return sb.ToString();
        }

        /// <summary>
        /// Get GridInfo in json format: Used bu the OSSL osGetGrid*
        /// Adding the SRV_HomeIRI to the kvp returned for use in scripts
        /// </summary>
        /// <returns>
        /// json string
        /// </returns>
        /// <param name='request'>
        /// Request.
        /// </param>
        /// <param name='path'>
        ///  /json_grid_info
        /// </param>
        /// <param name='param'>
        /// Parameter.
        /// </param>
        /// <param name='httpRequest'>
        /// Http request.
        /// </param>
        /// <param name='httpResponse'>
        /// Http response.
        /// </param>
        public string JsonGetGridInfoMethod(string request, string path, string param,
                                            IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            OSDMap map = new OSDMap();

            foreach (string k in _info.Keys)
            {
                map[k] = OSD.FromString(_info[k].ToString());
            }

            string HomeURI = Util.GetConfigVarFromSections<string>(m_Config, "HomeURI",
                new string[] { "Startup", "Hypergrid" }, String.Empty);

            if (!String.IsNullOrEmpty(HomeURI))
                map["home"] = OSD.FromString(HomeURI);
            else // Legacy. Remove soon!
            {
                IConfig cfg = m_Config.Configs["LoginService"];

                if (null != cfg)
                    HomeURI = cfg.GetString("SRV_HomeURI", HomeURI);

                if (!String.IsNullOrEmpty(HomeURI))
                    map["home"] = OSD.FromString(HomeURI);
            }

            return OSDParser.SerializeJsonString(map).ToString();
        }
    }
}
