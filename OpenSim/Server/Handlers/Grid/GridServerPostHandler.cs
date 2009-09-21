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
using System;
using System.Reflection;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using System.Collections.Generic;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Grid
{
    public class GridServerPostHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private IGridService m_GridService;

        public GridServerPostHandler(IGridService service) :
                base("POST", "/grid")
        {
            m_GridService = service;
        }

        public override byte[] Handle(string path, Stream requestData,
                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            StreamReader sr = new StreamReader(requestData);
            string body = sr.ReadToEnd();
            sr.Close();
            
            Dictionary<string, string> request =
                    ServerUtils.ParseQueryString(body);

            if (!request.ContainsKey("METHOD"))
                return FailureResult();

            string method = request["METHOD"];

            switch (method)
            {
                case "register":
                    return Register(request);

                case "deregister":
                    return Deregister(request);

                case "get_neighbours":
                    return GetNeighbours(request);

                case "get_region_by_uuid":
                    return GetRegionByUUID(request);

                case "get_region_by_position":
                    return GetRegionByPosition(request);

                case "get_region_by_name":
                    return GetRegionByName(request);

                case "get_regions_by_name":
                    return GetRegionsByName(request);

                case "get_region_range":
                    return GetRegionRange(request);

                default:
                    m_log.DebugFormat("[GRID HANDLER]: unknown method request {0}", method);
                    return FailureResult();
            }

        }

        #region Method-specific handlers

        byte[] Register(Dictionary<string, string> request)
        {
            UUID scopeID = UUID.Zero;
            if (request["SCOPEID"] != null)
                UUID.TryParse(request["SCOPEID"], out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to register region");

            Dictionary<string, object> rinfoData = new Dictionary<string, object>();
            foreach (KeyValuePair<string, string> kvp in request)
                rinfoData[kvp.Key] = kvp.Value;
            SimpleRegionInfo rinfo = new SimpleRegionInfo(rinfoData);

            bool result = m_GridService.RegisterRegion(scopeID, rinfo);

            if (result)
                return SuccessResult();
            else
                return FailureResult();
        }

        byte[] Deregister(Dictionary<string, string> request)
        {
            UUID regionID = UUID.Zero;
            if (request["REGIONID"] != null)
                UUID.TryParse(request["REGIONID"], out regionID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no regionID in request to deregister region");

            bool result = m_GridService.DeregisterRegion(regionID);

            if (result)
                return SuccessResult();
            else
                return FailureResult();

        }

        byte[] GetNeighbours(Dictionary<string, string> request)
        {
            UUID scopeID = UUID.Zero;
            if (request["SCOPEID"] != null)
                UUID.TryParse(request["SCOPEID"], out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get neighbours");

            UUID regionID = UUID.Zero;
            if (request["REGIONID"] != null)
                UUID.TryParse(request["REGIONID"], out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no regionID in request to get neighbours");

            List<SimpleRegionInfo> rinfos = m_GridService.GetNeighbours(scopeID, regionID);

            Dictionary<string, object> result = new Dictionary<string, object>();
            int i = 0;
            foreach (SimpleRegionInfo rinfo in rinfos)
            {
                Dictionary<string, object> rinfoDict = rinfo.ToKeyValuePairs();
                result["region" + i] = rinfoDict;
                i++;
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);

        }

        byte[] GetRegionByUUID(Dictionary<string, string> request)
        {
            // TODO
            return new byte[0];
        }

        byte[] GetRegionByPosition(Dictionary<string, string> request)
        {
            // TODO
            return new byte[0];
        }

        byte[] GetRegionByName(Dictionary<string, string> request)
        {
            // TODO
            return new byte[0];
        }

        byte[] GetRegionsByName(Dictionary<string, string> request)
        {
            // TODO
            return new byte[0];
        }

        byte[] GetRegionRange(Dictionary<string, string> request)
        {
            // TODO
            return new byte[0];
        }

        #endregion

        #region Misc

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

        private byte[] FailureResult()
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

            return DocToBytes(doc);
        }

        private byte[] DocToBytes(XmlDocument doc)
        {
            MemoryStream ms = new MemoryStream();
            XmlTextWriter xw = new XmlTextWriter(ms, null);
            xw.Formatting = Formatting.Indented;
            doc.WriteTo(xw);
            xw.Flush();

            return ms.GetBuffer();
        }

        #endregion
    }
}
