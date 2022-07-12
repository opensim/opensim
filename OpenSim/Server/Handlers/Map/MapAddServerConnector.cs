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
using System.IO;
using System.Reflection;
using System.Net;

using Nini.Config;
using log4net;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Server.Handlers.MapImage
{
    public class MapAddServiceConnector : ServiceConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IMapImageService m_MapService;
        private IGridService m_GridService;
        private string m_ConfigName = "MapImageService";

        public MapAddServiceConnector(IConfigSource config, IHttpServer server, string configName) :
                base(config, server, configName)
        {
            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(string.Format("No section {0} in config file", m_ConfigName));

            string mapService = serverConfig.GetString("LocalServiceModule", string.Empty);

            if (string.IsNullOrWhiteSpace(mapService))
                throw new Exception("No LocalServiceModule in config file");

            object[] args = new object[] { config };
            m_MapService = ServerUtils.LoadPlugin<IMapImageService>(mapService, args);

            string gridService = serverConfig.GetString("GridService", string.Empty);
            if (!string.IsNullOrWhiteSpace(gridService))
                m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);

            if (m_GridService != null)
                m_log.InfoFormat("[MAP IMAGE HANDLER]: GridService check is ON");
            else
                m_log.InfoFormat("[MAP IMAGE HANDLER]: GridService check is OFF");

            bool proxy = serverConfig.GetBoolean("HasProxy", false);
            IServiceAuth auth = ServiceAuth.Create(config, m_ConfigName);
            server.AddSimpleStreamHandler(new MapServerPostHandler(m_MapService, m_GridService, proxy, auth));
        }
    }

    class MapServerPostHandler : SimpleStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IMapImageService m_MapService;
        private IGridService m_GridService;
        bool m_Proxy;

        public MapServerPostHandler(IMapImageService service, IGridService grid, bool proxy, IServiceAuth auth) :
            base("/map", auth)
        {
            m_MapService = service;
            m_GridService = grid;
            m_Proxy = proxy;
        }

        protected override void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            //m_log.DebugFormat("[MAP SERVICE IMAGE HANDLER]: Received {0}", path);

            int x = 0, y = 0;
            UUID scopeID = UUID.Zero;
            byte[] data = null;

            httpResponse.StatusCode = (int)HttpStatusCode.OK;

            try
            {
                string body;
                using (StreamReader sr = new StreamReader(httpRequest.InputStream))
                    body = sr.ReadToEnd();
                body = body.Trim();

                Dictionary<string, object> request = ServerUtils.ParseQueryString(body);

                x = Int32.Parse(request["X"].ToString());
                y = Int32.Parse(request["Y"].ToString());
                if (request.TryGetValue("SCOPE", out object o))
                    UUID.TryParse(o.ToString(), out scopeID);
                if(request.TryGetValue("DATA", out object od))
                {
                    data = Convert.FromBase64String(od.ToString());
                    if (data.Length < 10)
                    {
                        httpResponse.RawBuffer = Util.ResultFailureMessage("Bad request.");
                        return;
                    }
                }
            }
            catch
            {
                httpResponse.RawBuffer = Util.ResultFailureMessage("Bad request.");
                return;
            }

            try
            {
                m_log.DebugFormat("[MAP ADD SERVER CONNECTOR]: Received map data for region at {0}-{1}", x, y);

                //string type = "image/jpeg";
                //if (request.ContainsKey("TYPE"))
                //    type = request["TYPE"].ToString();

                if (m_GridService != null)
                {
                    IPAddress ipAddr = httpRequest.RemoteIPEndPoint.Address;
                    GridRegion r = m_GridService.GetRegionByPosition(scopeID, (int)Util.RegionToWorldLoc((uint)x), (int)Util.RegionToWorldLoc((uint)y));
                    if (r != null)
                    {
                        if (r.ExternalEndPoint.Address.ToString() != ipAddr.ToString())
                        {
                            m_log.WarnFormat("[MAP IMAGE HANDLER]: IP address {0} may be trying to impersonate region in IP {1}", ipAddr, r.ExternalEndPoint.Address);
                            httpResponse.RawBuffer = Util.ResultFailureMessage("IP address of caller does not match IP address of registered region");
                            //return;
                        }
                    }
                    else
                    {
                        m_log.WarnFormat("[MAP IMAGE HANDLER]: IP address {0} may be rogue. Region not found at coordinates {1}-{2}",
                            ipAddr, x, y);
                        httpResponse.RawBuffer = Util.ResultFailureMessage("Region not found at given coordinates");
                        //return;
                    }
                }

                bool result;
                string reason;
                if (data == null)
                    result = m_MapService.RemoveMapTile(x, y, scopeID, out reason);
                else
                    result = m_MapService.AddMapTile(x, y, data, scopeID, out reason);
                httpResponse.RawBuffer = result ? Util.sucessResultSuccess : Util.ResultFailureMessage(reason);
                return;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MAP SERVICE IMAGE HANDLER]: Exception {0} {1}", e.Message, e.StackTrace);
            }

            httpResponse.RawBuffer = Util.ResultFailureMessage("Unexpected server error");
        }
    }
}
