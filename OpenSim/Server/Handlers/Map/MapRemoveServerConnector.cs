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
using System.Xml;

using Nini.Config;
using log4net;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Server.Handlers.MapImage
{
    public class MapRemoveServiceConnector : ServiceConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IMapImageService m_MapService;
        private IGridService m_GridService;
        private string m_ConfigName = "MapImageService";

        public MapRemoveServiceConnector(IConfigSource config, IHttpServer server, string configName) :
                base(config, server, configName)
        {
            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section {0} in config file", m_ConfigName));

            string mapService = serverConfig.GetString("LocalServiceModule",
                    String.Empty);

            if (mapService == String.Empty)
                throw new Exception("No LocalServiceModule in config file");

            Object[] args = new Object[] { config };
            m_MapService = ServerUtils.LoadPlugin<IMapImageService>(mapService, args);

            string gridService = serverConfig.GetString("GridService", String.Empty);
            if (gridService != string.Empty)
                m_GridService = ServerUtils.LoadPlugin<IGridService>(gridService, args);

            if (m_GridService != null)
                m_log.InfoFormat("[MAP IMAGE HANDLER]: GridService check is ON");
            else
                m_log.InfoFormat("[MAP IMAGE HANDLER]: GridService check is OFF");

            bool proxy = serverConfig.GetBoolean("HasProxy", false);
            server.AddStreamHandler(new MapServerRemoveHandler(m_MapService, m_GridService, proxy));

        }
    }

    class MapServerRemoveHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IMapImageService m_MapService;
        private IGridService m_GridService;
        bool m_Proxy;

        public MapServerRemoveHandler(IMapImageService service, IGridService grid, bool proxy) :
            base("POST", "/removemap")
        {
            m_MapService = service;
            m_GridService = grid;
            m_Proxy = proxy;
        }

        public override byte[] Handle(string path, Stream requestData, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
//            m_log.DebugFormat("[MAP SERVICE IMAGE HANDLER]: Received {0}", path);
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            try
            {
                Dictionary<string, object> request = ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("X") || !request.ContainsKey("Y"))
                {
                    httpResponse.StatusCode = (int)OSHttpStatusCode.ClientErrorBadRequest;
                    return FailureResult("Bad request.");
                }
                int x = 0, y = 0;
                Int32.TryParse(request["X"].ToString(), out x);
                Int32.TryParse(request["Y"].ToString(), out y);
//                UUID scopeID = new UUID("07f8d88e-cd5e-4239-a0ed-843f75d09992");
                UUID scopeID = UUID.Zero;
                if (request.ContainsKey("SCOPE"))
                    UUID.TryParse(request["SCOPE"].ToString(), out scopeID);

                m_log.DebugFormat("[MAP REMOVE SERVER CONNECTOR]: Received position data for region at {0}-{1}", x, y);

                if (m_GridService != null)
                {
                    System.Net.IPAddress ipAddr = GetCallerIP(httpRequest);
                    GridRegion r = m_GridService.GetRegionByPosition(UUID.Zero, (int)Util.RegionToWorldLoc((uint)x), (int)Util.RegionToWorldLoc((uint)y));
                    if (r != null)
                    {
                        if (r.ExternalEndPoint.Address.ToString() != ipAddr.ToString())
                        {
                            m_log.WarnFormat("[MAP IMAGE HANDLER]: IP address {0} may be trying to impersonate region in IP {1}", ipAddr, r.ExternalEndPoint.Address);
                            return FailureResult("IP address of caller does not match IP address of registered region");
                        }

                    }
                    else
                    {
                        m_log.WarnFormat("[MAP IMAGE HANDLER]: IP address {0} may be rogue. Region not found at coordinates {1}-{2}",
                            ipAddr, x, y);
                        return FailureResult("Region not found at given coordinates");
                    }
                }

                string reason = string.Empty;
                bool result = m_MapService.RemoveMapTile(x, y, scopeID, out reason);

                if (result)
                    return SuccessResult();
                else
                    return FailureResult(reason);

            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[MAP SERVICE IMAGE HANDLER]: Exception {0} {1}", e.Message, e.StackTrace);
            }

            return FailureResult("Unexpected server error");
        }

        private byte[] SuccessResult()
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "Result", "");
            result.AppendChild(doc.CreateTextNode("Success"));

            rootElement.AppendChild(result);

            return DocToBytes(doc);
        }

        private byte[] FailureResult(string msg)
        {
            XmlDocument doc = new XmlDocument();

            XmlNode xmlnode = doc.CreateNode(XmlNodeType.XmlDeclaration,
                    "", "");

            doc.AppendChild(xmlnode);

            XmlElement rootElement = doc.CreateElement("", "ServerResponse",
                    "");

            doc.AppendChild(rootElement);

            XmlElement result = doc.CreateElement("", "Result", "");
            result.AppendChild(doc.CreateTextNode("Failure"));

            rootElement.AppendChild(result);

            XmlElement message = doc.CreateElement("", "Message", "");
            message.AppendChild(doc.CreateTextNode(msg));

            rootElement.AppendChild(message);

            return DocToBytes(doc);
        }

        private byte[] DocToBytes(XmlDocument doc)
        {
            MemoryStream ms = new MemoryStream();
            XmlTextWriter xw = new XmlTextWriter(ms, null);
            xw.Formatting = Formatting.Indented;
            doc.WriteTo(xw);
            xw.Flush();

            return ms.ToArray();
        }

        private System.Net.IPAddress GetCallerIP(IOSHttpRequest request)
        {
            if (!m_Proxy)
                return request.RemoteIPEndPoint.Address;

            // We're behind a proxy
            string xff = "X-Forwarded-For";
            string xffValue = request.Headers[xff.ToLower()];
            if (xffValue == null || (xffValue != null && xffValue == string.Empty))
                xffValue = request.Headers[xff];

            if (xffValue == null || (xffValue != null && xffValue == string.Empty))
            {
                m_log.WarnFormat("[MAP IMAGE HANDLER]: No XFF header");
                return request.RemoteIPEndPoint.Address;
            }

            System.Net.IPEndPoint ep = Util.GetClientIPFromXFF(xffValue);
            if (ep != null)
                return ep.Address;

            // Oops
            return request.RemoteIPEndPoint.Address;
        }

    }
}
