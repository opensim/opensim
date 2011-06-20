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

using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Handlers.Base;

namespace OpenSim.Server.Handlers.MapImage
{
    public class MapAddServiceConnector : ServiceConnector
    {
        private IMapImageService m_MapService;
        private string m_ConfigName = "MapImageService";

        public MapAddServiceConnector(IConfigSource config, IHttpServer server, string configName) :
                base(config, server, configName)
        {
            IConfig serverConfig = config.Configs[m_ConfigName];
            if (serverConfig == null)
                throw new Exception(String.Format("No section {0} in config file", m_ConfigName));

            string gridService = serverConfig.GetString("LocalServiceModule",
                    String.Empty);

            if (gridService == String.Empty)
                throw new Exception("No LocalServiceModule in config file");

            Object[] args = new Object[] { config };
            m_MapService = ServerUtils.LoadPlugin<IMapImageService>(gridService, args);

            server.AddStreamHandler(new MapServerPostHandler(m_MapService));
        }
    }

    class MapServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private IMapImageService m_MapService;

        public MapServerPostHandler(IMapImageService service) :
            base("POST", "/map")
        {
            m_MapService = service;
        }

        public override byte[] Handle(string path, Stream requestData, OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            m_log.DebugFormat("[MAP SERVICE IMAGE HANDLER]: Received {0}", path);
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            body = body.Trim();

            try
            {
                Dictionary<string, object> request = ServerUtils.ParseQueryString(body);

                if (!request.ContainsKey("X") || !request.ContainsKey("Y") || !request.ContainsKey("DATA"))
                {
                    httpResponse.StatusCode = (int)OSHttpStatusCode.ClientErrorBadRequest;
                    return FailureResult("Bad request.");
                }
                int x = 0, y = 0;
                Int32.TryParse(request["X"].ToString(), out x);
                Int32.TryParse(request["Y"].ToString(), out y);
                string type = "image/jpeg";
                if (request.ContainsKey("TYPE"))
                    type = request["TYPE"].ToString();
                byte[] data = Convert.FromBase64String(request["DATA"].ToString());

                string reason = string.Empty;
                bool result = m_MapService.AddMapTile(x, y, data, out reason);

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
    }
}
