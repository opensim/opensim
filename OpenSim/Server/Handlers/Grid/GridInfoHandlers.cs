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
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Security;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Base;
using OpenSim.Server.Base;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Data;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Server.Handlers.Grid
{
    public class GridInfoHandlers
    {
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IConfigSource m_Config;
        private Dictionary<string, string> _info = [];
        private Dictionary<string, string> _stats = [];
        private byte[] cachedJsonAnswer = null;
        private byte[] cachedRestAnswer = null;
        private byte[] cachedStatAnswer = null;
        private bool stats_available = false;
        private int _lastrun;
        protected IGridService m_GridService = null;
        protected IGridUserData m_Database_griduser = null;

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
            IConfig gridCfg = configSource.Configs["GridInfoService"];

            stats_available = !gridCfg.GetBoolean("DisableStatsEndpoint", false);
            _lastrun = 0;

            if (stats_available)
            {
                stats_available = false;
                string gridService = m_Config.Configs["GridService"].GetString("LocalServiceModule", string.Empty);
                if(!string.IsNullOrEmpty(gridService))
                {
                    m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, [m_Config]);
                    if(m_GridService != null)
                    { 
                        IConfig dbConfig = configSource.Configs["DatabaseService"];
                        if (dbConfig is not null)
                        {
                            ServiceBase serviceBase = new(configSource);
                            string dllName = dbConfig.GetString("StorageProvider", String.Empty);
                            string connString = dbConfig.GetString("ConnectionString", String.Empty);

                            if (dllName.Length != 0 && connString.Length != 0)
                            {
                                m_Database_griduser = serviceBase.LoadPlugin<IGridUserData>(dllName, [connString, "GridUser"]);
                                if (m_Database_griduser != null)
                                {
                                    stats_available = true;
                                    _log.Debug("[GRID INFO SERVICE]: Grid Stats enabled");
                                }
                            }
                        }
                    }
                }
                if (!stats_available)
                {
                    _log.Warn("[GRID INFO SERVICE]: Could not initialize. Grid stats will be unavailable!");
                }
            }

            _info["platform"] = "OpenSim";
            try
            {
                if (gridCfg != null)
                {
                    foreach (string k in gridCfg.GetKeys())
                        _info[k] = gridCfg.GetString(k);
                }
                else 
                {
                    IConfig netCfg = configSource.Configs["Network"];
                    if (netCfg != null)
                    {
                        _info["login"] = string.Format("http://127.0.0.1:{0}/",
                            netCfg.GetString("http_listener_port", ConfigSettings.DefaultRegionHttpPort.ToString()));
                    }
                    else
                    {
                        _info["login"] = "http://127.0.0.1:9000/";
                    }
                    IssueWarning();
                }

                _info.TryGetValue("home", out string tmp);

                tmp = Util.GetConfigVarFromSections<string>(m_Config, "HomeURI", ["Startup", "Hypergrid"], tmp);

                if (string.IsNullOrEmpty(tmp))
                {
                    IConfig logincfg = m_Config.Configs["LoginService"];
                    if (logincfg != null)
                        tmp = logincfg.GetString("SRV_HomeURI", tmp);
                }
                if (!string.IsNullOrEmpty(tmp))
                    _info["home"] = OSD.FromString(tmp);

                tmp = Util.GetConfigVarFromSections<string>(m_Config, "HomeURIAlias", ["Startup", "Hypergrid"], string.Empty);
                if (!string.IsNullOrEmpty(tmp))
                    _info["homealias"] = OSD.FromString(tmp);

                _info.TryGetValue("gatekeeper", out tmp);
                tmp = Util.GetConfigVarFromSections<string>(m_Config, "GatekeeperURI", ["Startup", "Hypergrid"], tmp);
                if (!string.IsNullOrEmpty(tmp))
                    _info["gatekeeper"] = OSD.FromString(tmp);

                tmp = Util.GetConfigVarFromSections<string>(m_Config, "GatekeeperURIAlias", ["Startup", "Hypergrid"], string.Empty);
                if (!string.IsNullOrEmpty(tmp))
                    _info["gatekeeperalias"] = OSD.FromString(tmp);

            }
            catch (Exception)
            {
                _log.Warn("[GRID INFO SERVICE]: Cannot get grid info from config source, using minimal defaults");
            }

            _log.DebugFormat("[GRID INFO SERVICE]: Grid info service initialized with {0} keys", _info.Count);
        }

        private void IssueWarning()
        {
            _log.Warn("[GRID INFO SERVICE]: found no [GridInfoService] section in your configuration files");
            _log.Warn("[GRID INFO SERVICE]: trying to guess sensible defaults, you might want to provide better ones:");

            foreach (KeyValuePair<string, string> k in _info)
            {
                _log.Warn($"[GRID INFO SERVICE]: {k.Key}: {k.Value}");
            }
        }

        public XmlRpcResponse XmlRpcGridInfoMethod(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable responseData = [];

            _log.Debug("[GRID INFO SERVICE]: Request for grid info");

            foreach (KeyValuePair<string, string>  k in _info)
            {
                responseData[k.Key] = k.Value;
            }
            response.Value = responseData;

            return response;
        }

        public void RestGetGridInfoMethod(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            httpResponse.KeepAlive = false;
            if (httpRequest.HttpMethod != "GET")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            if(cachedRestAnswer == null)
            {
                osUTF8 osb = OSUTF8Cached.Acquire();
                osb.AppendASCII("<gridinfo>");
                foreach (KeyValuePair<string, string> k in _info)
                {
                    osb.AppendASCII('<');
                    osb.AppendASCII(k.Key);
                    osb.AppendASCII('>');
                    osb.AppendASCII(SecurityElement.Escape(k.Value.ToString()));
                    osb.AppendASCII("</");
                    osb.AppendASCII(k.Key);
                    osb.AppendASCII('>');
                }
                osb.AppendASCII("</gridinfo>");
                cachedRestAnswer = OSUTF8Cached.GetArrayAndRelease(osb);
            }
            httpResponse.ContentType = "application/xml";
            httpResponse.RawBuffer = cachedRestAnswer;
        }

        /// <summary>
        /// Get GridInfo in json format: Used by the OSSL osGetGrid*
        /// Adding the SRV_HomeURI to the kvp returned for use in scripts
        /// </summary>
        /// <returns>
        /// json string
        /// </returns>
        /// </param>
        /// <param name='httpRequest'>
        /// Http request.
        /// </param>
        /// <param name='httpResponse'>
        /// Http response.
        /// </param>
        public void JsonGetGridInfoMethod(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            httpResponse.KeepAlive = false;

            if (httpRequest.HttpMethod != "GET")
            {
                httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            if (cachedJsonAnswer == null)
            {
                OSDMap map = new OSDMap();
                foreach (KeyValuePair<string, string> k in _info)
                {
                    map[k.Key] = OSD.FromString(k.Value.ToString());
                }
                cachedJsonAnswer = OSDParser.SerializeJsonToBytes(map);
            }

            httpResponse.ContentType = "application/json";
            httpResponse.RawBuffer = cachedJsonAnswer;
        }

        public void GetGridStats(int now)
        {
            int region_count = 0;
            int active_users = 0;
            int residents = 0;

            try
            {
                // Fetch region data
                if(m_GridService is not null)
                {
                    List<GridRegion> regions = m_GridService.GetOnlineRegions(UUID.Zero, 0, 0, int.MaxValue);
                    foreach (GridRegion region in regions)
                    {
                        // Count individual region equivalent
                        region_count += (region.RegionSizeX * region.RegionSizeY) >> 16;
                    }
                    regions = null;
                }

                // Fetch all grid users, can't do a simple query unfortunately
                GridUserData[] gridusers = m_Database_griduser.GetAll(string.Empty);

                // Count if last login was within the last 30 days
                int oldestTime = now - 2592000;

                // Go through grid user data
                foreach (GridUserData griduser in gridusers)
                {
                    // Don't count if uui
                    if (!griduser.UserID.Contains(';'))
                        residents++;

                    if(griduser.Data.TryGetValue("Login", out string login) &&
                            int.TryParse(login, out int last_login))
                    {
                        if (last_login == 0)
                            continue;

                        if (last_login > oldestTime)
                            active_users++;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"[GRID INFO SERVICE]: Could not fetch grid stats: {ex.Message}");
            }

            _stats["residents"] = residents.ToString();
            _stats["active_users"] = active_users.ToString();
            _stats["region_count"] = region_count.ToString();

            _lastrun = now;
        }

        public void RestGridStatsHandler(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            httpResponse.KeepAlive = false;
            if (httpRequest.HttpMethod != "GET" || !stats_available)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            // Only fetch new stats if the last run is 15 minutes old since this is heavy db stuff
            int now = Util.UnixTimeSinceEpoch();

            if (cachedStatAnswer == null || (now - 900) > _lastrun)
            {
                GetGridStats(now);
                osUTF8 osb = OSUTF8Cached.Acquire();
                osb.AppendASCII("<gridstats>");
                foreach (KeyValuePair<string, string> k in _stats)
                {
                    osb.AppendASCII('<');
                    osb.AppendASCII(k.Key);
                    osb.AppendASCII('>');
                    osb.AppendASCII(SecurityElement.Escape(k.Value.ToString()));
                    osb.AppendASCII("</");
                    osb.AppendASCII(k.Key);
                    osb.AppendASCII('>');
                }
                osb.AppendASCII("</gridstats>");
                cachedStatAnswer = OSUTF8Cached.GetArrayAndRelease(osb);
            }
            httpResponse.ContentType = "application/xml";
            httpResponse.RawBuffer = cachedStatAnswer;
        }
    }
}
