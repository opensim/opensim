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

using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    public class GridServicesConnector : IGridService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;

        public GridServicesConnector()
        {
        }

        public GridServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        public GridServicesConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["GridService"];
            if (gridConfig == null)
            {
                m_log.Error("[GRID CONNECTOR]: GridService missing from OpenSim.ini");
                throw new Exception("Grid connector init error");
            }

            string serviceURI = gridConfig.GetString("GridServerURI",
                    String.Empty);

            if (serviceURI == String.Empty)
            {
                m_log.Error("[GRID CONNECTOR]: No Server URI named in section GridService");
                throw new Exception("Grid connector init error");
            }
            m_ServerURI = serviceURI;
        }


        #region IGridService

        public virtual bool RegisterRegion(UUID scopeID, SimpleRegionInfo regionInfo)
        {
            Dictionary<string, object> rinfo = regionInfo.ToKeyValuePairs();
            Dictionary<string, string> sendData = new Dictionary<string,string>();
            foreach (KeyValuePair<string, object> kvp in rinfo)
                sendData[kvp.Key] = (string)kvp.Value;

            sendData["SCOPEID"] = scopeID.ToString();

            sendData["METHOD"] = "register";

            string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                    m_ServerURI + "/grid",
                    ServerUtils.BuildQueryString(sendData));

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            if ((replyData["Result"] != null) && (replyData["Result"].ToString().ToLower() == "success"))
                return true;

            return false;
        }

        public virtual bool DeregisterRegion(UUID regionID)
        {
            Dictionary<string, string> sendData = new Dictionary<string, string>();

            sendData["REGIONID"] = regionID.ToString();

            sendData["METHOD"] = "deregister";

            string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                    m_ServerURI + "/grid",
                    ServerUtils.BuildQueryString(sendData));

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            if ((replyData["Result"] != null) && (replyData["Result"].ToString().ToLower() == "success"))
                return true;

            return false;
        }

        public virtual List<SimpleRegionInfo> GetNeighbours(UUID scopeID, UUID regionID)
        {
            Dictionary<string, string> sendData = new Dictionary<string, string>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["REGIONID"] = regionID.ToString();

            sendData["METHOD"] = "get_neighbours";

            string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                    m_ServerURI + "/grid",
                    ServerUtils.BuildQueryString(sendData));

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            List<SimpleRegionInfo> rinfos = new List<SimpleRegionInfo>();
            if (replyData != null)
            {
                Dictionary<string, object>.ValueCollection rinfosList = replyData.Values;
                foreach (object r in rinfosList)
                {
                    if (r is Dictionary<string, object>)
                    {
                        SimpleRegionInfo rinfo = new SimpleRegionInfo((Dictionary<string, object>)r);
                        rinfos.Add(rinfo);
                    }
                    else
                        m_log.DebugFormat("[GRID CONNECTOR]: GetNeighbours {0}, {1} received invalid response",
                            scopeID, regionID);
                }
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetNeighbours {0}, {1} received null response",
                    scopeID, regionID);

            return rinfos;
        }

        public virtual SimpleRegionInfo GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            Dictionary<string, string> sendData = new Dictionary<string, string>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["REGIONID"] = regionID.ToString();

            sendData["METHOD"] = "get_region_by_uuid";

            string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                    m_ServerURI + "/grid",
                    ServerUtils.BuildQueryString(sendData));

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            SimpleRegionInfo rinfo = null;
            if ((replyData != null) && (replyData["result"] != null))
            {
                if (replyData["result"] is Dictionary<string, object>)
                    rinfo = new SimpleRegionInfo((Dictionary<string, object>)replyData["result"]);
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByUUID {0}, {1} received invalid response",
                        scopeID, regionID);
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByUUID {0}, {1} received null response",
                    scopeID, regionID);

            return rinfo;
        }

        public virtual SimpleRegionInfo GetRegionByPosition(UUID scopeID, int x, int y)
        {
            Dictionary<string, string> sendData = new Dictionary<string, string>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["X"] = x.ToString();
            sendData["Y"] = y.ToString();

            sendData["METHOD"] = "get_region_by_position";

            string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                    m_ServerURI + "/grid",
                    ServerUtils.BuildQueryString(sendData));

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            SimpleRegionInfo rinfo = null;
            if ((replyData != null) && (replyData["result"] != null))
            {
                if (replyData["result"] is Dictionary<string, object>)
                    rinfo = new SimpleRegionInfo((Dictionary<string, object>)replyData["result"]);
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByPosition {0}, {1}-{2} received invalid response",
                        scopeID, x, y);
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByPosition {0}, {1}-{2} received null response",
                    scopeID, x, y);

            return rinfo;
        }

        public virtual SimpleRegionInfo GetRegionByName(UUID scopeID, string regionName)
        {
            Dictionary<string, string> sendData = new Dictionary<string, string>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["NAME"] = regionName;

            sendData["METHOD"] = "get_region_by_name";

            string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                    m_ServerURI + "/grid",
                    ServerUtils.BuildQueryString(sendData));

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            SimpleRegionInfo rinfo = null;
            if ((replyData != null) && (replyData["result"] != null))
            {
                if (replyData["result"] is Dictionary<string, object>)
                    rinfo = new SimpleRegionInfo((Dictionary<string, object>)replyData["result"]);
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByPosition {0}, {1} received invalid response",
                        scopeID, regionName);
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByPosition {0}, {1} received null response",
                    scopeID, regionName);

            return rinfo;
        }

        public virtual List<SimpleRegionInfo> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            Dictionary<string, string> sendData = new Dictionary<string, string>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["NAME"] = name;
            sendData["MAX"] = maxNumber.ToString();

            sendData["METHOD"] = "get_regions_by_name";

            string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                    m_ServerURI + "/grid",
                    ServerUtils.BuildQueryString(sendData));

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            List<SimpleRegionInfo> rinfos = new List<SimpleRegionInfo>();
            if (replyData != null)
            {
                Dictionary<string, object>.ValueCollection rinfosList = replyData.Values;
                foreach (object r in rinfosList)
                {
                    if (r is Dictionary<string, object>)
                    {
                        SimpleRegionInfo rinfo = new SimpleRegionInfo((Dictionary<string, object>)r);
                        rinfos.Add(rinfo);
                    }
                    else
                        m_log.DebugFormat("[GRID CONNECTOR]: GetRegionsByName {0}, {1}, {2} received invalid response",
                            scopeID, name, maxNumber);
                }
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetRegionsByName {0}, {1}, {2} received null response",
                    scopeID, name, maxNumber);

            return rinfos;
        }

        public virtual List<SimpleRegionInfo> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            Dictionary<string, string> sendData = new Dictionary<string, string>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["XMIN"] = xmin.ToString();
            sendData["XMAX"] = xmax.ToString();
            sendData["YMIN"] = ymin.ToString();
            sendData["YMAX"] = ymax.ToString();

            sendData["METHOD"] = "get_region_range";

            string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                    m_ServerURI + "/grid",
                    ServerUtils.BuildQueryString(sendData));

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            List<SimpleRegionInfo> rinfos = new List<SimpleRegionInfo>();
            if (replyData != null)
            {
                Dictionary<string, object>.ValueCollection rinfosList = replyData.Values;
                foreach (object r in rinfosList)
                {
                    if (r is Dictionary<string, object>)
                    {
                        SimpleRegionInfo rinfo = new SimpleRegionInfo((Dictionary<string, object>)r);
                        rinfos.Add(rinfo);
                    }
                    else
                        m_log.DebugFormat("[GRID CONNECTOR]: GetRegionRange {0}, {1}-{2} {3}-{4} received invalid response",
                            scopeID, xmin, xmax, ymin, ymax);
                }
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetRegionRange {0}, {1}-{2} {3}-{4} received null response",
                    scopeID, xmin, xmax, ymin, ymax);

            return rinfos;
        }

        #endregion

    }
}
