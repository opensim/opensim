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
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;

namespace OpenSim.Region.Communications.Local
{
    public class LocalBackEndServices : IGridServices
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Dictionary<ulong, RegionInfo> m_regions = new Dictionary<ulong, RegionInfo>();

        protected Dictionary<ulong, RegionCommsListener> m_regionListeners =
            new Dictionary<ulong, RegionCommsListener>();

        // private Dictionary<ulong, RegionInfo> m_remoteRegionInfoCache = new Dictionary<ulong, RegionInfo>();

        private Dictionary<string, string> m_queuedGridSettings = new Dictionary<string, string>();

        public string _gdebugRegionName = String.Empty;

        public bool RegionLoginsEnabled
        {
            get { return m_regionLoginsEnabled; }
            set { m_regionLoginsEnabled = value; }
        }
        private bool m_regionLoginsEnabled;

        public bool CheckRegion(string address, uint port)
        {
            return true;
        }

        public string gdebugRegionName
        {
            get { return _gdebugRegionName; }
            set { _gdebugRegionName = value; }
        }

        public string _rdebugRegionName = String.Empty;

        public string rdebugRegionName
        {
            get { return _rdebugRegionName; }
            set { _rdebugRegionName = value; }
        }

        /// <summary>
        /// Register a region method with the BackEnd Services.
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public RegionCommsListener RegisterRegion(RegionInfo regionInfo)
        {
            //m_log.Debug("CommsManager - Region " + regionInfo.RegionHandle + " , " + regionInfo.RegionLocX + " , "+ regionInfo.RegionLocY +" is registering");
            if (!m_regions.ContainsKey(regionInfo.RegionHandle))
            {
                //m_log.Debug("CommsManager - Adding Region " + regionInfo.RegionHandle);
                m_regions.Add(regionInfo.RegionHandle, regionInfo);

                RegionCommsListener regionHost = new RegionCommsListener();
                if (m_regionListeners.ContainsKey(regionInfo.RegionHandle))
                {
                    m_log.Error("[INTERREGION STANDALONE]: " +
                                "Error:Region registered twice as an Events listener for Interregion Communications but not as a listed region.  " +
                                "In Standalone mode this will cause BIG issues.  In grid mode, it means a region went down and came back up.");
                    m_regionListeners.Remove(regionInfo.RegionHandle);
                }
                m_regionListeners.Add(regionInfo.RegionHandle, regionHost);

                return regionHost;
            }
            else
            {
                // Already in our list, so the region went dead and restarted.
                // don't replace the old regioninfo..    this might be a locking issue..  however we need to
                // remove it and let it add normally below or we get extremely strange and intermittant
                // connectivity errors.
                // Don't change this line below to 'm_regions[regionInfo.RegionHandle] = regionInfo' unless you
                // *REALLY* know what you are doing here.
                m_regions[regionInfo.RegionHandle] = regionInfo;

                m_log.Warn("[INTERREGION STANDALONE]: Region registered twice. Region went down and came back up.");

                RegionCommsListener regionHost = new RegionCommsListener();
                if (m_regionListeners.ContainsKey(regionInfo.RegionHandle))
                {
                    m_regionListeners.Remove(regionInfo.RegionHandle);
                }
                m_regionListeners.Add(regionInfo.RegionHandle, regionHost);

                return regionHost;
            }
        }

        public bool DeregisterRegion(RegionInfo regionInfo)
        {
            if (m_regions.ContainsKey(regionInfo.RegionHandle))
            {
                m_regions.Remove(regionInfo.RegionHandle);
                if (m_regionListeners.ContainsKey(regionInfo.RegionHandle))
                {
                    m_regionListeners.Remove(regionInfo.RegionHandle);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// </summary>
        /// <param name="regionInfo"></param>
        /// <returns></returns>
        public List<SimpleRegionInfo> RequestNeighbours(uint x, uint y)
        {
            // m_log.Debug("Finding Neighbours to " + regionInfo.RegionHandle);
            List<SimpleRegionInfo> neighbours = new List<SimpleRegionInfo>();

            foreach (RegionInfo reg in m_regions.Values)
            {
                // m_log.Debug("CommsManager- RequestNeighbours() checking region " + reg.RegionLocX + " , "+ reg.RegionLocY);
                if (reg.RegionLocX != x || reg.RegionLocY != y)
                {
                    //m_log.Debug("CommsManager- RequestNeighbours() - found a different region in list, checking location");
                    if ((reg.RegionLocX > (x - 2)) && (reg.RegionLocX < (x + 2)))
                    {
                        if ((reg.RegionLocY > (y - 2)) && (reg.RegionLocY < (y + 2)))
                        {
                            neighbours.Add(reg);
                        }
                    }
                }
            }
            return neighbours;
        }

        /// <summary>
        /// Get information about a neighbouring region
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionInfo RequestNeighbourInfo(ulong regionHandle)
        {
            if (m_regions.ContainsKey(regionHandle))
            {
                return m_regions[regionHandle];
            }
            
            return null;
        }

        /// <summary>
        /// Get information about a neighbouring region
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>        
        public RegionInfo RequestNeighbourInfo(UUID regionID)
        {
            // TODO add a dictionary for faster lookup
            foreach (RegionInfo info in m_regions.Values)
            {
                if (info.RegionID == regionID)
                    return info;
            }
            
            return null;
        }

        /// <summary>
        /// Get information about a neighbouring region
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionInfo RequestNeighbourInfo(string name)
        {
            foreach (RegionInfo info in m_regions.Values)
            {
                if (info.RegionName == name)
                    return info;
            }

            return null;
        }

        /// <summary>
        /// Get information about a neighbouring region
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionInfo RequestNeighbourInfo(string host, uint port)
        {
            foreach (RegionInfo info in m_regions.Values)
            {
                if ((info.ExternalHostName == host) && (info.HttpPort == port))
                    return info;
            }

            return null;
        }

        /// <summary>
        /// Get information about the closet region given a region name.
        /// </summary>
        /// <param name="regionName"></param>
        /// <returns></returns>
        public RegionInfo RequestClosestRegion(string regionName)
        {
            foreach (RegionInfo regInfo in m_regions.Values)
            {
                if (regInfo.RegionName == regionName)
                    return regInfo;
            }
            return null;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        /// <returns></returns>
        public List<MapBlockData> RequestNeighbourMapBlocks(int minX, int minY, int maxX, int maxY)
        {
            List<MapBlockData> mapBlocks = new List<MapBlockData>();
            foreach (RegionInfo regInfo in m_regions.Values)
            {
                if (((regInfo.RegionLocX >= minX) && (regInfo.RegionLocX <= maxX)) &&
                    ((regInfo.RegionLocY >= minY) && (regInfo.RegionLocY <= maxY)))
                {
                    MapBlockData map = new MapBlockData();
                    map.Name = regInfo.RegionName;
                    map.X = (ushort) regInfo.RegionLocX;
                    map.Y = (ushort) regInfo.RegionLocY;
                    map.WaterHeight = (byte) regInfo.RegionSettings.WaterHeight;
                    map.MapImageId = regInfo.RegionSettings.TerrainImageID;
                    map.Agents = 1;
                    map.RegionFlags = 72458694;
                    map.Access = regInfo.AccessLevel;
                    mapBlocks.Add(map);
                }
            }
            return mapBlocks;
        }

        // This function is only here to keep this class in line with the Grid Interface.
        // It never gets called.
        public virtual Dictionary<string, string> GetGridSettings()
        {
            Dictionary<string, string> returnGridSettings = new Dictionary<string, string>();
            lock (m_queuedGridSettings)
            {
                returnGridSettings = m_queuedGridSettings;
                m_queuedGridSettings.Clear();
            }

            return returnGridSettings;
        }

        public virtual void SetForcefulBanlistsDisallowed()
        {
            m_queuedGridSettings.Add("allow_forceful_banlines", "FALSE");
        }


        /// <summary>
        /// Is a Sandbox mode method, used by the local Login server to inform a region of a connection user/session
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="loginData"></param>
        /// <returns></returns>
        public void AddNewSession(ulong regionHandle, Login loginData)
        {
            AgentCircuitData agent = new AgentCircuitData();
            agent.AgentID = loginData.Agent;
            agent.firstname = loginData.First;
            agent.lastname = loginData.Last;
            agent.SessionID = loginData.Session;
            agent.SecureSessionID = loginData.SecureSession;
            agent.circuitcode = loginData.CircuitCode;
            agent.BaseFolder = loginData.BaseFolder;
            agent.InventoryFolder = loginData.InventoryFolder;
            agent.startpos = loginData.StartPos;
            agent.CapsPath = loginData.CapsPath;
            if (loginData.Appearance != null)
                agent.Appearance = loginData.Appearance;
            else
            {
                m_log.WarnFormat("[INTER]: Appearance not found for {0} {1}. Creating default.", agent.firstname, agent.lastname);
                agent.Appearance = new AvatarAppearance(agent.AgentID);
            }

            TriggerExpectUser(regionHandle, agent);
        }

        public void TriggerExpectUser(ulong regionHandle, AgentCircuitData agent)
        {
            //m_log.Info("[INTER]: " + rdebugRegionName + ":Local BackEnd: Other region is sending child agent our way: " + agent.firstname + " " + agent.lastname);

            if (m_regionListeners.ContainsKey(regionHandle))
            {
                //m_log.Info("[INTER]: " + rdebugRegionName + ":Local BackEnd: FoundLocalRegion To send it to: " + agent.firstname + " " + agent.lastname);

                m_regionListeners[regionHandle].TriggerExpectUser(agent);
            }
        }

        public void TriggerLogOffUser(ulong regionHandle, UUID agentID, UUID RegionSecret, string message)
        {
            if (m_regionListeners.ContainsKey(regionHandle))
            {
                //m_log.Info("[INTER]: " + rdebugRegionName + ":Local BackEnd: FoundLocalRegion To send it to: " + agent.firstname + " " + agent.lastname);

                m_regionListeners[regionHandle].TriggerLogOffUser(agentID, RegionSecret, message);
            }
        }

        public void PingCheckReply(Hashtable respData)
        {
            foreach (ulong region in m_regions.Keys)
            {
                Hashtable regData = new Hashtable();
                RegionInfo reg = m_regions[region];
                regData["status"] = "active";
                regData["handle"] = region.ToString();

                respData[reg.RegionID.ToString()] = regData;
            }
        }


        public LandData RequestLandData (ulong regionHandle, uint x, uint y)
        {
            m_log.DebugFormat("[INTERREGION STANDALONE] requests land data in {0}, at {1}, {2}",
                              regionHandle, x, y);

            if (m_regionListeners.ContainsKey(regionHandle))
            {
                LandData land = m_regionListeners[regionHandle].TriggerGetLandData(x, y);
                return land;
            }

            m_log.Debug("[INTERREGION STANDALONE] didn't find land data locally.");
            return null;
        }

        public List<RegionInfo> RequestNamedRegions (string name, int maxNumber)
        {
            List<RegionInfo> lowercase_regions = new List<RegionInfo>();
            List<RegionInfo> regions = new List<RegionInfo>();
            foreach (RegionInfo info in m_regions.Values)
            {
                // Prioritizes exact match
                if (info.RegionName.StartsWith(name))
                {
                    regions.Add(info);
                    if (regions.Count >= maxNumber) break;
                }
                // But still saves lower case matches
                else if (info.RegionName.ToLower().StartsWith(name))
                {
                    if (lowercase_regions.Count < maxNumber)
                    {
                        lowercase_regions.Add(info);
                    }
                }
            }

            // If no exact matches found, return lowercase matches (libOMV compatiblity)
            if (regions.Count == 0 && lowercase_regions.Count != 0)
            {
                return lowercase_regions;
            }
            return regions;
        }
    }
}
