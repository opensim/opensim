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
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;

using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Server.Base;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    public class GridServicesConnector : BaseServiceConnector, IGridService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private string m_ServerURI = string.Empty;
        private string m_ServerGridURI = string.Empty;

        public GridServicesConnector()
        {
        }

        public GridServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
            m_ServerGridURI = serverURI + "/grid";
        }

        public GridServicesConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["GridService"];
            if (gridConfig is null)
            {
                m_log.Error("[GRID CONNECTOR]: GridService missing from OpenSim.ini");
                throw new Exception("Grid connector init error");
            }

            string serviceURI = gridConfig.GetString("GridServerURI", string.Empty);

            if (serviceURI.Length == 0)
            {
                m_log.Error("[GRID CONNECTOR]: No Server URI named in section GridService");
                throw new Exception("Grid connector init error");
            }
            m_ServerURI = serviceURI.TrimEnd('/');
            m_ServerGridURI = serviceURI + "/grid";

            base.Initialise(source, "GridService");
        }


        #region IGridService

        public string RegisterRegion(UUID scopeID, GridRegion regionInfo)
        {
            Dictionary<string, object> rinfo = regionInfo.ToKeyValuePairs();
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),
                ["VERSIONMIN"] = ProtocolVersions.ClientProtocolVersionMin.ToString(),
                ["VERSIONMAX"] = ProtocolVersions.ClientProtocolVersionMax.ToString(),
                ["METHOD"] = "register"
            };
            foreach (KeyValuePair<string, object> kvp in rinfo)
                sendData[kvp.Key] = (string)kvp.Value;

            string reqString = ServerUtils.BuildQueryString(sendData);
            // m_log.DebugFormat("[GRID CONNECTOR]: queryString = {0}", reqString);
            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(m_ServerGridURI, reqString, m_Auth);
                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                    if (replyData.TryGetValue("Result", out object tmpo) && tmpo is string tmps)
                    {
                        if(tmps.Equals("success", StringComparison.CurrentCultureIgnoreCase))
                            return string.Empty;
                        if (tmps.Equals("failure", StringComparison.CurrentCultureIgnoreCase))
                        {
                            m_log.Error(
                                $"[GRID CONNECTOR]: Registration failed: {replyData["Message"]} when contacting {m_ServerGridURI}");
                            return replyData["Message"].ToString();
                        }
                        else
                        {
                            m_log.Error(
                                $"[GRID CONNECTOR]: unexpected result {tmps} when contacting {m_ServerGridURI}");
                            return "Unexpected result " + tmps;
                        }
                    }
                    else
                    {
                        m_log.Error(
                            $"[GRID CONNECTOR]: reply data does not contain result field when contacting {m_ServerGridURI}");
                    }
                }
                else
                {
                    m_log.Error(
                        $"[GRID CONNECTOR]: RegisterRegion received null reply when contacting grid server at {m_ServerGridURI}");
                }
            }
            catch (Exception e)
            {
                m_log.Error($"[GRID CONNECTOR]: Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return "Error communicating with the grid service at " + m_ServerGridURI;
        }

        public bool DeregisterRegion(UUID regionID)
        {
            Dictionary<string, object> sendData = new()
            {
                ["REGIONID"] = regionID.ToString(),

                ["METHOD"] = "deregister"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(m_ServerGridURI, ServerUtils.BuildQueryString(sendData), m_Auth);

                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                    return replyData.TryGetValue("Result", out object tmpo) && 
                            tmpo is string rs &&
                            rs.Equals("success", StringComparison.InvariantCultureIgnoreCase);
                }
                else
                    m_log.Debug("[GRID CONNECTOR]: DeregisterRegion received empty reply");
            }
            catch (Exception e)
            {
                m_log.Error($"[GRID CONNECTOR]: Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return false;
        }

        public List<GridRegion> GetNeighbours(UUID scopeID, UUID regionID)
        {
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),
                ["REGIONID"] = regionID.ToString(),

                ["METHOD"] = "get_neighbours"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(
                    m_ServerGridURI,
                    ServerUtils.BuildQueryString(sendData), m_Auth);

                Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                //m_log.DebugFormat("[GRID CONNECTOR]: get neighbours returned {0} elements", replyData.Values.Count);
                if (replyData.Count > 0)
                {
                    List<GridRegion> rinfos = [];
                    foreach (object r in replyData.Values)
                    {
                        if (r is Dictionary<string, object> dr)
                        {
                            GridRegion rinfo = new GridRegion(dr);
                            rinfos.Add(rinfo);
                        }
                    }
                    return rinfos;
                }

            }
            catch (Exception e)
            {
                m_log.Error($"[GRID CONNECTOR]: GetNeighbours Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
                
            }

            return [];
        }

        public GridRegion GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),
                ["REGIONID"] = regionID.ToString(),

                ["METHOD"] = "get_region_by_uuid"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(m_ServerGridURI, ServerUtils.BuildQueryString(sendData), m_Auth);
                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                    if(replyData.TryGetValue("result", out object tmpo) && tmpo is Dictionary<string, object> td)
                        return new GridRegion(td);
                    //else
                    //    m_log.Debug($"[GRID CONNECTOR]: GetRegionByUUID {scopeID}, {regionID} received empty result response");
                }
                else
                    m_log.Debug($"[GRID CONNECTOR]: GetRegionByUUID received empty reply for {scopeID}, {regionID}");
            }
            catch (Exception e)
            {
                m_log.Debug($"[GRID CONNECTOR]: Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return null;
        }

        public GridRegion GetRegionByHandle(UUID scopeID, ulong regionhandle)
        {
            Util.RegionHandleToWorldLoc(regionhandle, out uint x, out uint y);
            return GetRegionByPosition(scopeID, (int)x, (int)y);
        }

        public GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),
                ["X"] = x.ToString(),
                ["Y"] = y.ToString(),

                ["METHOD"] = "get_region_by_position"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(
                        m_ServerGridURI,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                    if(replyData.TryGetValue("result", out object tmpo) && tmpo is Dictionary<string, object> td)
                        return new GridRegion(td);
                    //else
                    //    m_log.Debug($"[GRID CONNECTOR]: GetRegionByPosition {scopeID}, {x}-{y} received empty result response");
                }
                else
                    m_log.Debug($"[GRID CONNECTOR]: GetRegionByPosition {scopeID}, {x}-{y} received empty response");
            }
            catch (Exception e)
            {
                m_log.Debug($"[GRID CONNECTOR]: Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return null;
        }

        public GridRegion GetRegionByName(UUID scopeID, string regionName)
        {
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),
                ["NAME"] = regionName,

                ["METHOD"] = "get_region_by_name"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(
                        m_ServerGridURI,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                    if(replyData.TryGetValue("result", out object tmpo) && tmpo is Dictionary<string, object> td)
                        return new GridRegion(td);
                    //else
                    //    m_log.DebugFormat("$[GRID CONNECTOR]: GetRegionByName {scopeID}, {regionName} received empty result");
                }
                else
                     m_log.DebugFormat("$[GRID CONNECTOR]: GetRegionByName {scopeID}, {regionName} received empty reply");
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID CONNECTOR]: GetRegionByName, Exception when contacting grid server at {0}: {1}", m_ServerGridURI, e.Message);
            }

            return null;
        }

        public GridRegion GetLocalRegionByName(UUID scopeID, string regionName)
        {
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),
                ["NAME"] = regionName,

                ["METHOD"] = "get_localregion_by_name"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(
                        m_ServerGridURI,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                    if(replyData.TryGetValue("result", out object tmpo) && tmpo is Dictionary<string, object> td)
                        return new GridRegion(td);
                    //else
                    //    m_log.DebugFormat("$[GRID CONNECTOR]: GetLocalRegionByName {scopeID}, {regionName} received empty result");
                }
                else
                     m_log.DebugFormat("$[GRID CONNECTOR]: GetLocalRegionByName {scopeID}, {regionName} received empty reply");
            }
            catch (Exception e)
            {
                m_log.Debug($"[GRID CONNECTOR]: GetLocalRegionByName, Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return null;
        }

        public GridRegion GetRegionByURI(UUID scopeID, RegionURI uri)
        {
            return null;
        }

        public GridRegion GetLocalRegionByURI(UUID scopeID, RegionURI uri)
        {
            return null;
        }

        public List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),
                ["NAME"] = name,
                ["MAX"] = maxNumber.ToString(),

                ["METHOD"] = "get_regions_by_name"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(
                        m_ServerGridURI,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                    if (replyData.Count > 0)
                    {
                        List<GridRegion> rinfos = [];
                        foreach (object r in replyData.Values)
                        {
                            if (r is Dictionary<string, object> dr)
                            {
                                GridRegion rinfo = new GridRegion(dr);
                                rinfos.Add(rinfo);
                            }
                        }
                        return rinfos;
                    }
                    else
                        m_log.Debug($"[GRID CONNECTOR]: GetRegionsByName {scopeID}, {name}, {maxNumber} received empty reply data");
                }
                else
                    m_log.Debug($"[GRID CONNECTOR]: GetRegionsByName {scopeID}, {name}, {maxNumber} received empty reply");
            }
            catch (Exception e)
            {
                m_log.Debug($"[GRID CONNECTOR]: Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return [];
        }

        public List<GridRegion> GetRegionsByURI(UUID scopeID, RegionURI uri, int maxNumber)
        {
            return null;
        }

        public List<GridRegion> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),
                ["XMIN"] = xmin.ToString(),
                ["XMAX"] = xmax.ToString(),
                ["YMIN"] = ymin.ToString(),
                ["YMAX"] = ymax.ToString(),

                ["METHOD"] = "get_region_range"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(
                        m_ServerGridURI,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                //m_log.DebugFormat("[GRID CONNECTOR]: GetRegionRange reply was {0}", reply);
                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                    if (replyData.Count > 0)
                    {
                        List<GridRegion> rinfos = [];
                        foreach (object r in replyData.Values)
                        {
                            if (r is Dictionary<string, object> dr)
                            {
                                GridRegion rinfo = new(dr);
                                rinfos.Add(rinfo);
                            }
                        }
                        return rinfos;
                    }
                    else
                        m_log.Debug($"[GRID CONNECTOR]: GetRegionRange {scopeID}, {xmin}-{xmax} {ymin}-{ymax} received null response");
                }
                else
                    m_log.Debug("[GRID CONNECTOR]: GetRegionRange received empty reply");

            }
            catch (Exception e)
            {
                m_log.Debug($"[GRID CONNECTOR]: Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return [];
        }

        public List<GridRegion> GetDefaultRegions(UUID scopeID)
        {
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),

                ["METHOD"] = "get_default_regions"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(
                        m_ServerGridURI,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                //m_log.DebugFormat("[GRID CONNECTOR]: reply was {0}", reply);
                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.Count > 0)
                    {
                        List<GridRegion> rinfos = [];
                        Dictionary<string, object>.ValueCollection rinfosList = replyData.Values;
                        foreach (object r in rinfosList)
                        {
                            if (r is Dictionary<string, object>)
                            {
                                GridRegion rinfo = new GridRegion((Dictionary<string, object>)r);
                                rinfos.Add(rinfo);
                            }
                        }
                        return rinfos;
                    }
                    else
                        m_log.Debug($"[GRID CONNECTOR]: GetDefaultRegions {scopeID} received empty response");
                }
                else
                    m_log.Debug("[GRID CONNECTOR]: GetDefaultRegions received empty reply");

            }
            catch (Exception e)
            {
                m_log.Debug($"[GRID CONNECTOR]: GetDefaultRegions Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return [];
        }

        public List<GridRegion> GetDefaultHypergridRegions(UUID scopeID)
        {
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),

                ["METHOD"] = "get_default_hypergrid_regions"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(
                        m_ServerGridURI,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                //m_log.DebugFormat("[GRID CONNECTOR]: reply was {0}", reply);
                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.Count > 0)
                    {
                        List<GridRegion> rinfos = [];
                        foreach (object r in replyData.Values)
                        {
                            if (r is Dictionary<string, object> dr)
                            {
                                GridRegion rinfo = new(dr);
                                rinfos.Add(rinfo);
                            }
                        }
                        return rinfos;
                    }
                    else
                        m_log.Debug($"[GRID CONNECTOR]: GetDefaultHypergridRegions {scopeID} received empty response");
                }
                else
                    m_log.Debug("[GRID CONNECTOR]: GetDefaultHypergridRegions received empty reply");

            }
            catch (Exception e)
            {
                m_log.Debug($"[GRID CONNECTOR]: Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return [];
        }

        public List<GridRegion> GetFallbackRegions(UUID scopeID, int x, int y)
        {
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),
                ["X"] = x.ToString(),
                ["Y"] = y.ToString(),

                ["METHOD"] = "get_fallback_regions"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(
                        m_ServerGridURI,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                //m_log.DebugFormat("[GRID CONNECTOR]: reply was {0}", reply);
                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.Count > 0)
                    {
                        List<GridRegion> rinfos = [];
                        foreach (object r in replyData.Values)
                        {
                            if (r is Dictionary<string, object> dr)
                            {
                                GridRegion rinfo = new(dr);
                                rinfos.Add(rinfo);
                            }
                        }
                        return rinfos;
                    }
                    else
                        m_log.Debug($"[GRID CONNECTOR]: GetFallbackRegions {scopeID}, {x}-{y} received empty response");
                }
                else
                    m_log.Debug("[GRID CONNECTOR]: GetFallbackRegions received empty reply");
            }
            catch (Exception e)
            {
                m_log.Debug($"[GRID CONNECTOR]: Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return [];
        }

        public List<GridRegion> GetOnlineRegions(UUID scopeID, int x, int y, int maxCount)
        {
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),
                ["X"] = x.ToString(),
                ["Y"] = y.ToString(),
                ["MC"] = maxCount.ToString(),

                ["METHOD"] = "get_online_regions"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(m_ServerGridURI, ServerUtils.BuildQueryString(sendData), m_Auth);

                //m_log.DebugFormat("[GRID CONNECTOR]: reply was {0}", reply);
                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.Count > 0)
                    {
                        List<GridRegion> rinfos = [];
                        foreach (object r in  replyData.Values)
                        {
                            if (r is Dictionary<string, object> dr)
                            {
                                GridRegion rinfo = new(dr);
                                rinfos.Add(rinfo);
                            }
                        }
                        return rinfos;
                    }
                    else
                        m_log.Debug($"[GRID CONNECTOR]: GetOnlineRegions {scopeID}, {x}-{y} received empty response");
                }
                else
                    m_log.Debug("[GRID CONNECTOR]: GetOnlineRegions received empty reply");
            }
            catch (Exception e)
            {
                m_log.Debug($"[GRID CONNECTOR]: Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return [];
        }

        public List<GridRegion> GetHyperlinks(UUID scopeID)
        {
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),

                ["METHOD"] = "get_hyperlinks"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(
                        m_ServerGridURI,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                //m_log.DebugFormat("[GRID CONNECTOR]: reply was {0}", reply);
                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.Count > 0)
                    {
                        List<GridRegion> rinfos = [];
                        foreach (object r in replyData.Values)
                        {
                            if (r is Dictionary<string, object> dr)
                            {
                                GridRegion rinfo = new(dr);
                                rinfos.Add(rinfo);
                            }
                        }
                        return rinfos;
                    }
                    else
                        m_log.Debug($"[GRID CONNECTOR]: GetHyperlinks {scopeID} received empty response");
                }
                else
                    m_log.Debug("[GRID CONNECTOR]: GetHyperlinks received empty reply");
            }
            catch (Exception e)
            {
                m_log.Debug($"[GRID CONNECTOR]: Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return [];
        }

        public int GetRegionFlags(UUID scopeID, UUID regionID)
        {
            Dictionary<string, object> sendData = new()
            {
                ["SCOPEID"] = scopeID.ToString(),
                ["REGIONID"] = regionID.ToString(),

                ["METHOD"] = "get_region_flags"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(
                        m_ServerGridURI,
                        ServerUtils.BuildQueryString(sendData), m_Auth);

                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                    if (replyData.TryGetValue("result", out object tmpo) &&
                            tmpo is string tmps &&
                            Int32.TryParse(tmps, out int flags))
                        return flags;
                     else
                        m_log.Debug($"[GRID CONNECTOR]: GetRegionFlags {scopeID}, {regionID} received invalid response");
                }
                else
                    m_log.Debug("[GRID CONNECTOR]: GetRegionFlags received empty reply");

            }
            catch (Exception e)
            {
                m_log.Debug($"[GRID CONNECTOR]: Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return -1;
        }

        public Dictionary<string, object> GetExtraFeatures()
        {
            Dictionary<string, object> sendData = new()
            {
                ["METHOD"] = "get_grid_extra_features"
            };

            try
            {
                string reply = SynchronousRestFormsRequester.MakePostRequest(
                                                                  m_ServerGridURI,
                                                                  ServerUtils.BuildQueryString(sendData), m_Auth);
                if (reply.Length > 0)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);
                    if (replyData.Count > 0)
                    {
                        Dictionary<string, object> extraFeatures = [];
                        foreach (string key in replyData.Keys)
                        {
                            extraFeatures[key] = replyData[key].ToString();
                        }
                        return extraFeatures;
                    }
                }
                else
                    m_log.Debug("[GRID CONNECTOR]: GetExtraServiceURLs received empty reply");
            }
            catch (Exception e)
            {
                m_log.Debug($"[GRID CONNECTOR]: GetExtraFeatures - Exception when contacting grid server at {m_ServerGridURI}: {e.Message}");
            }

            return [];
        }

        #endregion
    }
}
