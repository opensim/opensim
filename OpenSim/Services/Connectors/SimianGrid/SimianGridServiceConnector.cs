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
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Services.Connectors.SimianGrid
{
    /// <summary>
    /// Connects region registration and neighbor lookups to the SimianGrid
    /// backend
    /// </summary>
    public class SimianGridServiceConnector : IGridService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;
//        private bool m_Enabled = false;

        public SimianGridServiceConnector() { }
        public SimianGridServiceConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        public SimianGridServiceConnector(IConfigSource source)
        {
            CommonInit(source);
        }

        public void Initialise(IConfigSource source)
        {
            CommonInit(source);
        }

        private void CommonInit(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["GridService"];
            if (gridConfig == null)
            {
                m_log.Error("[SIMIAN GRID CONNECTOR]: GridService missing from OpenSim.ini");
                throw new Exception("Grid connector init error");
            }

            string serviceUrl = gridConfig.GetString("GridServerURI");
            if (String.IsNullOrEmpty(serviceUrl))
            {
                m_log.Error("[SIMIAN GRID CONNECTOR]: No Server URI named in section GridService");
                throw new Exception("Grid connector init error");
            }

            if (!serviceUrl.EndsWith("/") && !serviceUrl.EndsWith("="))
                serviceUrl = serviceUrl + '/';
            m_ServerURI = serviceUrl;
//            m_Enabled = true;
        }

        #region IGridService

        public string RegisterRegion(UUID scopeID, GridRegion regionInfo)
        {
            IPEndPoint ext = regionInfo.ExternalEndPoint;
            if (ext == null) return "Region registration for " + regionInfo.RegionName + " failed: Could not resolve EndPoint";
            // Generate and upload our map tile in PNG format to the SimianGrid AddMapTile service
//            Scene scene;
//            if (m_scenes.TryGetValue(regionInfo.RegionID, out scene))
//                UploadMapTile(scene);
//            else
//                m_log.Warn("Registering region " + regionInfo.RegionName + " (" + regionInfo.RegionID + ") that we are not tracking");

            Vector3d minPosition = new Vector3d(regionInfo.RegionLocX, regionInfo.RegionLocY, 0.0);
            Vector3d maxPosition = minPosition + new Vector3d(regionInfo.RegionSizeX, regionInfo.RegionSizeY, Constants.RegionHeight);

            OSDMap extraData = new OSDMap
            {
                { "ServerURI", OSD.FromString(regionInfo.ServerURI) },
                { "InternalAddress", OSD.FromString(regionInfo.InternalEndPoint.Address.ToString()) },
                { "InternalPort", OSD.FromInteger(regionInfo.InternalEndPoint.Port) },
                { "ExternalAddress", OSD.FromString(ext.Address.ToString()) },
                { "ExternalPort", OSD.FromInteger(regionInfo.ExternalEndPoint.Port) },
                { "MapTexture", OSD.FromUUID(regionInfo.TerrainImage) },
                { "Access", OSD.FromInteger(regionInfo.Access) },
                { "RegionSecret", OSD.FromString(regionInfo.RegionSecret) },
                { "EstateOwner", OSD.FromUUID(regionInfo.EstateOwner) },
                { "Token", OSD.FromString(regionInfo.Token) }
            };

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddScene" },
                { "SceneID", regionInfo.RegionID.ToString() },
                { "Name", regionInfo.RegionName },
                { "MinPosition", minPosition.ToString() },
                { "MaxPosition", maxPosition.ToString() },
                { "Address", regionInfo.ServerURI },
                { "Enabled", "1" },
                { "ExtraData", OSDParser.SerializeJsonString(extraData) }
            };

            OSDMap response = SimianGrid.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
                return String.Empty;
            else
                return "Region registration for " + regionInfo.RegionName + " failed: " + response["Message"].AsString();
        }

        public bool DeregisterRegion(UUID regionID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "AddScene" },
                { "SceneID", regionID.ToString() },
                { "Enabled", "0" }
            };

            OSDMap response = SimianGrid.PostToService(m_ServerURI, requestArgs);
            bool success = response["Success"].AsBoolean();

            if (!success)
                m_log.Warn("[SIMIAN GRID CONNECTOR]: Region deregistration for " + regionID + " failed: " + response["Message"].AsString());

            return success;
        }

        public List<GridRegion> GetNeighbours(UUID scopeID, UUID regionID)
        {
            GridRegion region = GetRegionByUUID(scopeID, regionID);

            int NEIGHBOR_RADIUS = Math.Max(region.RegionSizeX, region.RegionSizeY) / 2;

            if (region != null)
            {
                List<GridRegion> regions = GetRegionRange(scopeID,
                    region.RegionLocX - NEIGHBOR_RADIUS, region.RegionLocX + region.RegionSizeX + NEIGHBOR_RADIUS,
                    region.RegionLocY - NEIGHBOR_RADIUS, region.RegionLocY + region.RegionSizeY + NEIGHBOR_RADIUS);

                for (int i = 0; i < regions.Count; i++)
                {
                    if (regions[i].RegionID == regionID)
                    {
                        regions.RemoveAt(i);
                        break;
                    }
                }

//                m_log.Debug("[SIMIAN GRID CONNECTOR]: Found " + regions.Count + " neighbors for region " + regionID);
                return regions;
            }

            return new List<GridRegion>(0);
        }

        public GridRegion GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScene" },
                { "SceneID", regionID.ToString() }
            };

            // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] request region with uuid {0}",regionID.ToString());

            OSDMap response = SimianGrid.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] uuid request successful {0}",response["Name"].AsString());
                return ResponseToGridRegion(response);
            }
            else
            {
                m_log.Warn("[SIMIAN GRID CONNECTOR]: Grid service did not find a match for region " + regionID);
                return null;
            }
        }

        public GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            // Go one meter in from the requested x/y coords to avoid requesting a position
            // that falls on the border of two sims
            Vector3d position = new Vector3d(x + 1, y + 1, 0.0);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScene" },
                { "Position", position.ToString() },
                { "Enabled", "1" }
            };

            // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] request grid at {0}",position.ToString());

            OSDMap response = SimianGrid.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] position request successful {0}",response["Name"].AsString());
                return ResponseToGridRegion(response);
            }
            else
            {
                // m_log.InfoFormat("[SIMIAN GRID CONNECTOR]: Grid service did not find a match for region at {0},{1}",
                //     Util.WorldToRegionLoc(x), Util.WorldToRegionLoc(y));
                return null;
            }
        }

        public GridRegion GetRegionByName(UUID scopeID, string regionName)
        {
            List<GridRegion> regions = GetRegionsByName(scopeID, regionName, 1);

            m_log.Debug("[SIMIAN GRID CONNECTOR]: Got " + regions.Count + " matches for region name " + regionName);

            if (regions.Count > 0)
                return regions[0];

            return null;
        }

        public List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
            List<GridRegion> foundRegions = new List<GridRegion>();

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScenes" },
                { "NameQuery", name },
                { "Enabled", "1" }
            };
            if (maxNumber > 0)
                requestArgs["MaxNumber"] = maxNumber.ToString();

            // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] request regions with name {0}",name);

            OSDMap response = SimianGrid.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] found regions with name {0}",name);

                OSDArray array = response["Scenes"] as OSDArray;
                if (array != null)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        GridRegion region = ResponseToGridRegion(array[i] as OSDMap);
                        if (region != null)
                            foundRegions.Add(region);
                    }
                }
            }

            return foundRegions;
        }

        public List<GridRegion> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            List<GridRegion> foundRegions = new List<GridRegion>();

            Vector3d minPosition = new Vector3d(xmin, ymin, 0.0);
            Vector3d maxPosition = new Vector3d(xmax, ymax, Constants.RegionHeight);

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScenes" },
                { "MinPosition", minPosition.ToString() },
                { "MaxPosition", maxPosition.ToString() },
                { "Enabled", "1" }
            };

            //m_log.DebugFormat("[SIMIAN GRID CONNECTOR] request regions by range {0} to {1}",minPosition.ToString(),maxPosition.ToString());


            OSDMap response = SimianGrid.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                OSDArray array = response["Scenes"] as OSDArray;
                if (array != null)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        GridRegion region = ResponseToGridRegion(array[i] as OSDMap);
                        if (region != null)
                            foundRegions.Add(region);
                    }
                }
            }

            return foundRegions;
        }

        public List<GridRegion> GetDefaultRegions(UUID scopeID)
        {
            // TODO: Allow specifying the default grid location
            const int DEFAULT_X = 1000 * 256;
            const int DEFAULT_Y = 1000 * 256;

            GridRegion defRegion = GetNearestRegion(new Vector3d(DEFAULT_X, DEFAULT_Y, 0.0), true);
            if (defRegion != null)
                return new List<GridRegion>(1) { defRegion };
            else
                return new List<GridRegion>(0);
        }

        public List<GridRegion> GetDefaultHypergridRegions(UUID scopeID)
        {
            // TODO: Allow specifying the default grid location
            return GetDefaultRegions(scopeID);
        }

        public List<GridRegion> GetFallbackRegions(UUID scopeID, int x, int y)
        {
            GridRegion defRegion = GetNearestRegion(new Vector3d(x, y, 0.0), true);
            if (defRegion != null)
                return new List<GridRegion>(1) { defRegion };
            else
                return new List<GridRegion>(0);
        }

        public List<GridRegion> GetHyperlinks(UUID scopeID)
        {
            List<GridRegion> foundRegions = new List<GridRegion>();

            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScenes" },
                { "HyperGrid", "true" },
                { "Enabled", "1" }
            };

            OSDMap response = SimianGrid.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                // m_log.DebugFormat("[SIMIAN GRID CONNECTOR] found regions with name {0}",name);

                OSDArray array = response["Scenes"] as OSDArray;
                if (array != null)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        GridRegion region = ResponseToGridRegion(array[i] as OSDMap);
                        if (region != null)
                            foundRegions.Add(region);
                    }
                }
            }

            return foundRegions;
        }

        public int GetRegionFlags(UUID scopeID, UUID regionID)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScene" },
                { "SceneID", regionID.ToString() }
            };

            m_log.DebugFormat("[SIMIAN GRID CONNECTOR] request region flags for {0}",regionID.ToString());

            OSDMap response = SimianGrid.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                OSDMap extraData = response["ExtraData"] as OSDMap;
                int enabled = response["Enabled"].AsBoolean() ? (int)OpenSim.Framework.RegionFlags.RegionOnline : 0;
                int hypergrid = extraData["HyperGrid"].AsBoolean() ? (int)OpenSim.Framework.RegionFlags.Hyperlink : 0;
                int flags =  enabled | hypergrid;
                m_log.DebugFormat("[SGGC] enabled - {0} hg - {1} flags - {2}", enabled, hypergrid, flags);
                return flags;
            }
            else
            {
                m_log.Warn("[SIMIAN GRID CONNECTOR]: Grid service did not find a match for region " + regionID + " during region flags check");
                return -1;
            }
        }

        public Dictionary<string, object> GetExtraFeatures()
        {
            /// See SimulatorFeaturesModule - Need to get map, search and destination guide
            Dictionary<string, object> extraFeatures = new Dictionary<string, object>();
            return extraFeatures;
        }

        #endregion IGridService

        private GridRegion GetNearestRegion(Vector3d position, bool onlyEnabled)
        {
            NameValueCollection requestArgs = new NameValueCollection
            {
                { "RequestMethod", "GetScene" },
                { "Position", position.ToString() },
                { "FindClosest", "1" }
            };
            if (onlyEnabled)
                requestArgs["Enabled"] = "1";

            OSDMap response = SimianGrid.PostToService(m_ServerURI, requestArgs);
            if (response["Success"].AsBoolean())
            {
                return ResponseToGridRegion(response);
            }
            else
            {
                m_log.Warn("[SIMIAN GRID CONNECTOR]: Grid service did not find a match for region at " + position);
                return null;
            }
        }

        private GridRegion ResponseToGridRegion(OSDMap response)
        {
            if (response == null)
                return null;

            OSDMap extraData = response["ExtraData"] as OSDMap;
            if (extraData == null)
                return null;

            GridRegion region = new GridRegion();

            region.RegionID = response["SceneID"].AsUUID();
            region.RegionName = response["Name"].AsString();

            Vector3d minPosition = response["MinPosition"].AsVector3d();
            Vector3d maxPosition = response["MaxPosition"].AsVector3d();
            region.RegionLocX = (int)minPosition.X;
            region.RegionLocY = (int)minPosition.Y;

            region.RegionSizeX = (int)maxPosition.X - (int)minPosition.X;
            region.RegionSizeY = (int)maxPosition.Y - (int)minPosition.Y;

            if ( ! extraData["HyperGrid"] ) {
                Uri httpAddress = response["Address"].AsUri();
                region.ExternalHostName = httpAddress.Host;
                region.HttpPort = (uint)httpAddress.Port;

                IPAddress internalAddress;
                IPAddress.TryParse(extraData["InternalAddress"].AsString(), out internalAddress);
                if (internalAddress == null)
                    internalAddress = IPAddress.Any;

                region.InternalEndPoint = new IPEndPoint(internalAddress, extraData["InternalPort"].AsInteger());
                region.TerrainImage = extraData["MapTexture"].AsUUID();
                region.Access = (byte)extraData["Access"].AsInteger();
                region.RegionSecret = extraData["RegionSecret"].AsString();
                region.EstateOwner = extraData["EstateOwner"].AsUUID();
                region.Token = extraData["Token"].AsString();
                region.ServerURI = extraData["ServerURI"].AsString();
            } else {
                region.ServerURI = response["Address"];
            }

            return region;
        }
    }
}
