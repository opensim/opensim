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

using OpenSim.Framework.ServiceAuth;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Server.Base;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    public class GridServicesConnector : BaseServiceConnector, IGridService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;

        private ExpiringCache<ulong, GridRegion> m_regionCache =
                new ExpiringCache<ulong, GridRegion>();

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

            base.Initialise(source, "GridService");
        }


        #region IGridService

        public string RegisterRegion(UUID scopeID, GridRegion regionInfo)
        {
            Dictionary<string, object> rinfo = regionInfo.ToKeyValuePairs();
            Dictionary<string, object> sendData = new Dictionary<string,object>();
            foreach (KeyValuePair<string, object> kvp in rinfo)
                sendData[kvp.Key] = (string)kvp.Value;

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString();
            sendData["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString();
            sendData["METHOD"] = "register";

            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/grid";
            // m_log.DebugFormat("[GRID CONNECTOR]: queryString = {0}", reqString);
            try
            {
                string reply = SynchronousRestFormsRequester.MakeRequest("POST", uri, reqString, m_Auth);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.ContainsKey("Result")&& (replyData["Result"].ToString().ToLower() == "success"))
                    {
                        return String.Empty;
                    }
                    else if (replyData.ContainsKey("Result")&& (replyData["Result"].ToString().ToLower() == "failure"))
                    {
                        m_log.ErrorFormat(
                            "[GRID CONNECTOR]: Registration failed: {0} when contacting {1}", replyData["Message"], uri);

                        return replyData["Message"].ToString();
                    }
                    else if (!replyData.ContainsKey("Result"))
                    {
                        m_log.ErrorFormat(
                            "[GRID CONNECTOR]: reply data does not contain result field when contacting {0}", uri);
                    }
                    else
                    {
                        m_log.ErrorFormat(
                            "[GRID CONNECTOR]: unexpected result {0} when contacting {1}", replyData["Result"], uri);

                        return "Unexpected result " + replyData["Result"].ToString();
                    }
                }
                else
                {
                    m_log.ErrorFormat(
                        "[GRID CONNECTOR]: RegisterRegion received null reply when contacting grid server at {0}", uri);
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[GRID CONNECTOR]: Exception when contacting grid server at {0}: {1}", uri, e.Message);
            }

            return string.Format("Error communicating with the grid service at {0}", uri);
        }

        public bool DeregisterRegion(UUID regionID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["REGIONID"] = regionID.ToString();

            sendData["METHOD"] = "deregister";

            string uri = m_ServerURI + "/grid";

            try
            {
                string reply
                    = SynchronousRestFormsRequester.MakeRequest("POST", uri, ServerUtils.BuildQueryString(sendData), m_Auth);

                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if ((replyData["Result"] != null) && (replyData["Result"].ToString().ToLower() == "success"))
                        return true;
                }
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: DeregisterRegion received null reply");
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: Exception when contacting grid server at {0}: {1}", uri, e.Message);
            }

            return false;
        }

        public List<GridRegion> GetNeighbours(UUID scopeID, UUID regionID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["REGIONID"] = regionID.ToString();

            sendData["METHOD"] = "get_neighbours";

            List<GridRegion> rinfos = new List<GridRegion>();

            string reqString = ServerUtils.BuildQueryString(sendData);
            string reply = string.Empty;
            string uri = m_ServerURI + "/grid";

            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST", uri, reqString, m_Auth);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: Exception when contacting grid server at {0}: {1}", uri, e.Message);
                return rinfos;
            }

            Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

            if (replyData != null)
            {
                Dictionary<string, object>.ValueCollection rinfosList = replyData.Values;
                //m_log.DebugFormat("[GRID CONNECTOR]: get neighbours returned {0} elements", rinfosList.Count);
                foreach (object r in rinfosList)
                {
                    if (r is Dictionary<string, object>)
                    {
                        GridRegion rinfo = new GridRegion((Dictionary<string, object>)r);
                        rinfos.Add(rinfo);
                    }
                }
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetNeighbours {0}, {1} received null response",
                    scopeID, regionID);

            return rinfos;
        }

        public GridRegion GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["REGIONID"] = regionID.ToString();

            sendData["METHOD"] = "get_region_by_uuid";

            string reply = string.Empty;
            string uri = m_ServerURI + "/grid";
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST", uri, ServerUtils.BuildQueryString(sendData), m_Auth);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: Exception when contacting grid server at {0}: {1}", uri, e.Message);
                return null;
            }

            GridRegion rinfo = null;

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if ((replyData != null) && (replyData["result"] != null))
                {
                    if (replyData["result"] is Dictionary<string, object>)
                        rinfo = new GridRegion((Dictionary<string, object>)replyData["result"]);
                    //else
                    //    m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByUUID {0}, {1} received null response",
                    //        scopeID, regionID);
                }
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByUUID {0}, {1} received null response",
                        scopeID, regionID);
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByUUID received null reply");

            return rinfo;
        }

        public GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            GridRegion rinfo = null;
            ulong regionHandle = Util.UIntsToLong((uint)x, (uint)y);

            // this cache includes NULL regions
            if (m_regionCache.TryGetValue(regionHandle, out rinfo))
                return rinfo;

            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["X"] = x.ToString();
            sendData["Y"] = y.ToString();

            sendData["METHOD"] = "get_region_by_position";
            string reply = string.Empty;
            string uri = m_ServerURI + "/grid";
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        ServerUtils.BuildQueryString(sendData), m_Auth);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: Exception when contacting grid server at {0}: {1}", uri, e.Message);
                return null;
            }

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if ((replyData != null) && (replyData["result"] != null))
                {
                    if (replyData["result"] is Dictionary<string, object>)
                        rinfo = new GridRegion((Dictionary<string, object>)replyData["result"]);
                    //else
                    //    m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByPosition {0}, {1}-{2} received no region",
                    //        scopeID, x, y);
                }
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByPosition {0}, {1}-{2} received null response",
                        scopeID, x, y);
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByPosition received null reply");

            m_regionCache.Add(regionHandle, rinfo, TimeSpan.FromSeconds(600));

            return rinfo;
        }

        public GridRegion GetRegionByNameSpecific(UUID scopeID, string regionName)
        {

            return null;
        }

        public GridRegion GetRegionByName(UUID scopeID, string regionName)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["NAME"] = regionName;

            sendData["METHOD"] = "get_region_by_name";
            string reply = string.Empty;
            string uri = m_ServerURI + "/grid";
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        ServerUtils.BuildQueryString(sendData), m_Auth);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: Exception when contacting grid server at {0}: {1}", uri, e.Message);
                return null;
            }

            GridRegion rinfo = null;
            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if ((replyData != null) && (replyData["result"] != null))
                {
                    if (replyData["result"] is Dictionary<string, object>)
                        rinfo = new GridRegion((Dictionary<string, object>)replyData["result"]);
                }
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByPosition {0}, {1} received null response",
                        scopeID, regionName);
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByName received null reply");

            return rinfo;
        }

        public List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["NAME"] = name;
            sendData["MAX"] = maxNumber.ToString();

            sendData["METHOD"] = "get_regions_by_name";
            List<GridRegion> rinfos = new List<GridRegion>();
            string reply = string.Empty;
            string uri = m_ServerURI + "/grid";
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        ServerUtils.BuildQueryString(sendData), m_Auth);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: Exception when contacting grid server at {0}: {1}", uri, e.Message);
                return rinfos;
            }

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if (replyData != null)
                {
                    Dictionary<string, object>.ValueCollection rinfosList = replyData.Values;
                    foreach (object r in rinfosList)
                    {
                        if (r is Dictionary<string, object>)
                        {
                            GridRegion rinfo = new GridRegion((Dictionary<string, object>)r);
                            rinfos.Add(rinfo);
                        }
                    }
                }
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: GetRegionsByName {0}, {1}, {2} received null response",
                        scopeID, name, maxNumber);
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetRegionsByName received null reply");

            return rinfos;
        }

        public List<GridRegion> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["XMIN"] = xmin.ToString();
            sendData["XMAX"] = xmax.ToString();
            sendData["YMIN"] = ymin.ToString();
            sendData["YMAX"] = ymax.ToString();

            sendData["METHOD"] = "get_region_range";

            List<GridRegion> rinfos = new List<GridRegion>();
            string reply = string.Empty;
            string uri = m_ServerURI + "/grid";

            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                //m_log.DebugFormat("[GRID CONNECTOR]: reply was {0}", reply);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: Exception when contacting grid server at {0}: {1}", uri, e.Message);
                return rinfos;
            }

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if (replyData != null)
                {
                    Dictionary<string, object>.ValueCollection rinfosList = replyData.Values;
                    foreach (object r in rinfosList)
                    {
                        if (r is Dictionary<string, object>)
                        {
                            GridRegion rinfo = new GridRegion((Dictionary<string, object>)r);
                            rinfos.Add(rinfo);
                        }
                    }
                }
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: GetRegionRange {0}, {1}-{2} {3}-{4} received null response",
                        scopeID, xmin, xmax, ymin, ymax);
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetRegionRange received null reply");

            return rinfos;
        }

        public List<GridRegion> GetDefaultRegions(UUID scopeID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["SCOPEID"] = scopeID.ToString();

            sendData["METHOD"] = "get_default_regions";

            List<GridRegion> rinfos = new List<GridRegion>();
            string reply = string.Empty;
            string uri = m_ServerURI + "/grid";
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                //m_log.DebugFormat("[GRID CONNECTOR]: reply was {0}", reply);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: Exception when contacting grid server at {0}: {1}", uri, e.Message);
                return rinfos;
            }

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if (replyData != null)
                {
                    Dictionary<string, object>.ValueCollection rinfosList = replyData.Values;
                    foreach (object r in rinfosList)
                    {
                        if (r is Dictionary<string, object>)
                        {
                            GridRegion rinfo = new GridRegion((Dictionary<string, object>)r);
                            rinfos.Add(rinfo);
                        }
                    }
                }
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: GetDefaultRegions {0} received null response",
                        scopeID);
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetDefaultRegions received null reply");

            return rinfos;
        }

        public List<GridRegion> GetDefaultHypergridRegions(UUID scopeID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["SCOPEID"] = scopeID.ToString();

            sendData["METHOD"] = "get_default_hypergrid_regions";

            List<GridRegion> rinfos = new List<GridRegion>();
            string reply = string.Empty;
            string uri = m_ServerURI + "/grid";
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                //m_log.DebugFormat("[GRID CONNECTOR]: reply was {0}", reply);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: Exception when contacting grid server at {0}: {1}", uri, e.Message);
                return rinfos;
            }

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if (replyData != null)
                {
                    Dictionary<string, object>.ValueCollection rinfosList = replyData.Values;
                    foreach (object r in rinfosList)
                    {
                        if (r is Dictionary<string, object>)
                        {
                            GridRegion rinfo = new GridRegion((Dictionary<string, object>)r);
                            rinfos.Add(rinfo);
                        }
                    }
                }
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: GetDefaultHypergridRegions {0} received null response",
                        scopeID);
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetDefaultHypergridRegions received null reply");

            return rinfos;
        }

        public List<GridRegion> GetFallbackRegions(UUID scopeID, int x, int y)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["X"] = x.ToString();
            sendData["Y"] = y.ToString();

            sendData["METHOD"] = "get_fallback_regions";

            List<GridRegion> rinfos = new List<GridRegion>();
            string reply = string.Empty;
            string uri = m_ServerURI + "/grid";
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                //m_log.DebugFormat("[GRID CONNECTOR]: reply was {0}", reply);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: Exception when contacting grid server at {0}: {1}", uri, e.Message);
                return rinfos;
            }

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if (replyData != null)
                {
                    Dictionary<string, object>.ValueCollection rinfosList = replyData.Values;
                    foreach (object r in rinfosList)
                    {
                        if (r is Dictionary<string, object>)
                        {
                            GridRegion rinfo = new GridRegion((Dictionary<string, object>)r);
                            rinfos.Add(rinfo);
                        }
                    }
                }
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: GetFallbackRegions {0}, {1}-{2} received null response",
                        scopeID, x, y);
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetFallbackRegions received null reply");

            return rinfos;
        }

        public List<GridRegion> GetHyperlinks(UUID scopeID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["SCOPEID"] = scopeID.ToString();

            sendData["METHOD"] = "get_hyperlinks";

            List<GridRegion> rinfos = new List<GridRegion>();
            string reply = string.Empty;
            string uri = m_ServerURI + "/grid";
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                //m_log.DebugFormat("[GRID CONNECTOR]: reply was {0}", reply);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: Exception when contacting grid server at {0}: {1}", uri, e.Message);
                return rinfos;
            }

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if (replyData != null)
                {
                    Dictionary<string, object>.ValueCollection rinfosList = replyData.Values;
                    foreach (object r in rinfosList)
                    {
                        if (r is Dictionary<string, object>)
                        {
                            GridRegion rinfo = new GridRegion((Dictionary<string, object>)r);
                            rinfos.Add(rinfo);
                        }
                    }
                }
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: GetHyperlinks {0} received null response",
                        scopeID);
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetHyperlinks received null reply");

            return rinfos;
        }

        public int GetRegionFlags(UUID scopeID, UUID regionID)
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();

            sendData["SCOPEID"] = scopeID.ToString();
            sendData["REGIONID"] = regionID.ToString();

            sendData["METHOD"] = "get_region_flags";

            string reply = string.Empty;
            string uri = m_ServerURI + "/grid";
            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        ServerUtils.BuildQueryString(sendData), m_Auth);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: Exception when contacting grid server at {0}: {1}", uri, e.Message);
                return -1;
            }

            int flags = -1;

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if ((replyData != null) && replyData.ContainsKey("result") && (replyData["result"] != null))
                {
                    Int32.TryParse((string)replyData["result"], out flags);
                    //else
                    //    m_log.DebugFormat("[GRID CONNECTOR]: GetRegionFlags {0}, {1} received wrong type {2}",
                    //        scopeID, regionID, replyData["result"].GetType());
                }
                else
                    m_log.DebugFormat("[GRID CONNECTOR]: GetRegionFlags {0}, {1} received null response",
                        scopeID, regionID);
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetRegionFlags received null reply");

            return flags;
        }

        public Dictionary<string, object> GetExtraFeatures()
        {
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            Dictionary<string, object> extraFeatures = new Dictionary<string, object>();

            sendData["METHOD"] = "get_grid_extra_features";

            string reply = string.Empty;
            string uri = m_ServerURI + "/grid";

            try
            {
                reply = SynchronousRestFormsRequester.MakeRequest("POST",
                                                                  uri,
                                                                  ServerUtils.BuildQueryString(sendData), m_Auth);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: GetExtraFeatures - Exception when contacting grid server at {0}: {1}", uri, e.Message);
                return extraFeatures;
            }

            if (reply != string.Empty)
            {
                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                if ((replyData != null) && replyData.Count > 0)
                {
                    foreach (string key in replyData.Keys)
                    {
                        extraFeatures[key] = replyData[key].ToString();
                    }
                }
            }
            else
                m_log.DebugFormat("[GRID CONNECTOR]: GetExtraServiceURLs received null reply");

            return extraFeatures;
        }
        #endregion

    }
}
