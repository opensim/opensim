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
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
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
            body = body.Trim();

            //m_log.DebugFormat("[XXX]: query String: {0}", body);

            try
            {
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

                }
                m_log.DebugFormat("[GRID HANDLER]: unknown method {0} request {1}", method.Length, method);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID HANDLER]: Exception {0}", e);
            }

            return FailureResult();

        }

        #region Method-specific handlers

        byte[] Register(Dictionary<string, string> request)
        {
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"], out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to register region");

            int versionNumberMin = 0, versionNumberMax = 0;
            if (request.ContainsKey("VERSIONMIN"))
                Int32.TryParse(request["VERSIONMIN"], out versionNumberMin);
            else
                m_log.WarnFormat("[GRID HANDLER]: no minimum protocol version in request to register region");

            if (request.ContainsKey("VERSIONMAX"))
                Int32.TryParse(request["VERSIONMAX"], out versionNumberMax);
            else
                m_log.WarnFormat("[GRID HANDLER]: no maximum protocol version in request to register region");

            // Check the protocol version
            if ((versionNumberMin > ProtocolVersions.ServerProtocolVersionMax && versionNumberMax < ProtocolVersions.ServerProtocolVersionMax))
            {
                // Can't do, there is no overlap in the acceptable ranges
                return FailureResult();
            }

            Dictionary<string, object> rinfoData = new Dictionary<string, object>();
            GridRegion rinfo = null;
            try
            {
                foreach (KeyValuePair<string, string> kvp in request)
                    rinfoData[kvp.Key] = kvp.Value;
                rinfo = new GridRegion(rinfoData);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID HANDLER]: exception unpacking region data: {0}", e);
            }

            bool result = false;
            if (rinfo != null)
                result = m_GridService.RegisterRegion(scopeID, rinfo);

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
                UUID.TryParse(request["REGIONID"], out regionID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no regionID in request to get neighbours");

            List<GridRegion> rinfos = m_GridService.GetNeighbours(scopeID, regionID);
            //m_log.DebugFormat("[GRID HANDLER]: neighbours for region {0}: {1}", regionID, rinfos.Count);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((rinfos == null) || ((rinfos != null) && (rinfos.Count == 0)))
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (GridRegion rinfo in rinfos)
                {
                    Dictionary<string, object> rinfoDict = rinfo.ToKeyValuePairs();
                    result["region" + i] = rinfoDict;
                    i++;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);

        }

        byte[] GetRegionByUUID(Dictionary<string, string> request)
        {
            UUID scopeID = UUID.Zero;
            if (request["SCOPEID"] != null)
                UUID.TryParse(request["SCOPEID"], out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get neighbours");

            UUID regionID = UUID.Zero;
            if (request["REGIONID"] != null)
                UUID.TryParse(request["REGIONID"], out regionID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no regionID in request to get neighbours");

            GridRegion rinfo = m_GridService.GetRegionByUUID(scopeID, regionID);
            //m_log.DebugFormat("[GRID HANDLER]: neighbours for region {0}: {1}", regionID, rinfos.Count);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if (rinfo == null)
                result["result"] = "null";
            else
                result["result"] = rinfo.ToKeyValuePairs();

            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        byte[] GetRegionByPosition(Dictionary<string, string> request)
        {
            UUID scopeID = UUID.Zero;
            if (request["SCOPEID"] != null)
                UUID.TryParse(request["SCOPEID"], out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get region by position");

            int x = 0, y = 0;
            if (request["X"] != null)
                Int32.TryParse(request["X"], out x);
            else
                m_log.WarnFormat("[GRID HANDLER]: no X in request to get region by position");
            if (request["Y"] != null)
                Int32.TryParse(request["Y"], out y);
            else
                m_log.WarnFormat("[GRID HANDLER]: no Y in request to get region by position");

            GridRegion rinfo = m_GridService.GetRegionByPosition(scopeID, x, y);
            //m_log.DebugFormat("[GRID HANDLER]: neighbours for region {0}: {1}", regionID, rinfos.Count);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if (rinfo == null)
                result["result"] = "null";
            else
                result["result"] = rinfo.ToKeyValuePairs();

            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        byte[] GetRegionByName(Dictionary<string, string> request)
        {
            UUID scopeID = UUID.Zero;
            if (request["SCOPEID"] != null)
                UUID.TryParse(request["SCOPEID"], out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get region by name");

            string regionName = string.Empty;
            if (request["NAME"] != null)
                regionName = request["NAME"];
            else
                m_log.WarnFormat("[GRID HANDLER]: no name in request to get region by name");

            GridRegion rinfo = m_GridService.GetRegionByName(scopeID, regionName);
            //m_log.DebugFormat("[GRID HANDLER]: neighbours for region {0}: {1}", regionID, rinfos.Count);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if (rinfo == null)
                result["result"] = "null";
            else
                result["result"] = rinfo.ToKeyValuePairs();

            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        byte[] GetRegionsByName(Dictionary<string, string> request)
        {
            UUID scopeID = UUID.Zero;
            if (request["SCOPEID"] != null)
                UUID.TryParse(request["SCOPEID"], out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get regions by name");

            string regionName = string.Empty;
            if (request["NAME"] != null)
                regionName = request["NAME"];
            else
                m_log.WarnFormat("[GRID HANDLER]: no NAME in request to get regions by name");

            int max = 0;
            if (request["MAX"] != null)
                Int32.TryParse(request["MAX"], out max);
            else
                m_log.WarnFormat("[GRID HANDLER]: no MAX in request to get regions by name");

            List<GridRegion> rinfos = m_GridService.GetRegionsByName(scopeID, regionName, max);
            //m_log.DebugFormat("[GRID HANDLER]: neighbours for region {0}: {1}", regionID, rinfos.Count);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((rinfos == null) || ((rinfos != null) && (rinfos.Count == 0)))
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (GridRegion rinfo in rinfos)
                {
                    Dictionary<string, object> rinfoDict = rinfo.ToKeyValuePairs();
                    result["region" + i] = rinfoDict;
                    i++;
                }
            }

            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
        }

        byte[] GetRegionRange(Dictionary<string, string> request)
        {
            //m_log.DebugFormat("[GRID HANDLER]: GetRegionRange");
            UUID scopeID = UUID.Zero;
            if (request.ContainsKey("SCOPEID"))
                UUID.TryParse(request["SCOPEID"], out scopeID);
            else
                m_log.WarnFormat("[GRID HANDLER]: no scopeID in request to get region range");

            int xmin = 0, xmax = 0, ymin = 0, ymax = 0;
            if (request.ContainsKey("XMIN"))
                Int32.TryParse(request["XMIN"], out xmin);
            else
                m_log.WarnFormat("[GRID HANDLER]: no XMIN in request to get region range");
            if (request.ContainsKey("XMAX"))
                Int32.TryParse(request["XMAX"], out xmax);
            else
                m_log.WarnFormat("[GRID HANDLER]: no XMAX in request to get region range");
            if (request.ContainsKey("YMIN"))
                Int32.TryParse(request["YMIN"], out ymin);
            else
                m_log.WarnFormat("[GRID HANDLER]: no YMIN in request to get region range");
            if (request.ContainsKey("YMAX"))
                Int32.TryParse(request["YMAX"], out ymax);
            else
                m_log.WarnFormat("[GRID HANDLER]: no YMAX in request to get region range");


            List<GridRegion> rinfos = m_GridService.GetRegionRange(scopeID, xmin, xmax, ymin, ymax);

            Dictionary<string, object> result = new Dictionary<string, object>();
            if ((rinfos == null) || ((rinfos != null) && (rinfos.Count == 0)))
                result["result"] = "null";
            else
            {
                int i = 0;
                foreach (GridRegion rinfo in rinfos)
                {
                    Dictionary<string, object> rinfoDict = rinfo.ToKeyValuePairs();
                    result["region" + i] = rinfoDict;
                    i++;
                }
            }
            string xmlString = ServerUtils.BuildXmlResponse(result);
            //m_log.DebugFormat("[GRID HANDLER]: resp string: {0}", xmlString);
            UTF8Encoding encoding = new UTF8Encoding();
            return encoding.GetBytes(xmlString);
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

            return ms.ToArray();
        }

        #endregion
    }
}
