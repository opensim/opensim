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
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Xml;

using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Data;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Hypergrid;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenMetaverse;

namespace OpenSim.Services.GridService
{
    public class HypergridLinker : IHypergridLinker
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private static uint m_autoMappingX = 0;
        private static uint m_autoMappingY = 0;
        private static bool m_enableAutoMapping = false;

        protected IRegionData m_Database;
        protected GridService m_GridService;
        protected IAssetService m_AssetService;
        protected GatekeeperServiceConnector m_GatekeeperConnector;

        protected UUID m_ScopeID = UUID.Zero;
//        protected bool m_Check4096 = true;
        protected string m_MapTileDirectory = string.Empty;
        protected string m_ThisGatekeeper = string.Empty;
        protected Uri m_ThisGatekeeperURI = null;

        protected GridRegion m_DefaultRegion;
        protected GridRegion DefaultRegion
        {
            get
            {
                if (m_DefaultRegion == null)
                {
                    List<GridRegion> defs = m_GridService.GetDefaultHypergridRegions(m_ScopeID);
                    if (defs != null && defs.Count > 0)
                        m_DefaultRegion = defs[0];
                    else
                    {
                        // Get any region
                        defs = m_GridService.GetRegionsByName(m_ScopeID, "", 1);
                        if (defs != null && defs.Count > 0)
                            m_DefaultRegion = defs[0];
                        else
                        {
                            // This shouldn't happen
                            m_DefaultRegion = new GridRegion(1000, 1000);
                            m_log.Error("[HYPERGRID LINKER]: Something is wrong with this grid. It has no regions?");
                        }
                    }
                }
                return m_DefaultRegion;
            }
        }

        public HypergridLinker(IConfigSource config, GridService gridService, IRegionData db)
        {
            IConfig gridConfig = config.Configs["GridService"];
            if (gridConfig == null)
                return;

            if (!gridConfig.GetBoolean("HypergridLinker", false))
                return;

            m_Database = db;
            m_GridService = gridService;
            m_log.DebugFormat("[HYPERGRID LINKER]: Starting with db {0}", db.GetType());

            string assetService = gridConfig.GetString("AssetService", string.Empty);

            Object[] args = new Object[] { config };

            if (assetService != string.Empty)
                m_AssetService = ServerUtils.LoadPlugin<IAssetService>(assetService, args);

            string scope = gridConfig.GetString("ScopeID", string.Empty);
            if (scope != string.Empty)
                UUID.TryParse(scope, out m_ScopeID);

//                m_Check4096 = gridConfig.GetBoolean("Check4096", true);

            m_MapTileDirectory = gridConfig.GetString("MapTileDirectory", "maptiles");

            m_ThisGatekeeper = Util.GetConfigVarFromSections<string>(config, "GatekeeperURI",
                new string[] { "Startup", "Hypergrid", "GridService" }, String.Empty);
            // Legacy. Remove soon!
            m_ThisGatekeeper = gridConfig.GetString("Gatekeeper", m_ThisGatekeeper);
            try
            {
                m_ThisGatekeeperURI = new Uri(m_ThisGatekeeper);
            }
            catch
            {
                m_log.WarnFormat("[HYPERGRID LINKER]: Malformed URL in [GridService], variable Gatekeeper = {0}", m_ThisGatekeeper);
            }

            m_GatekeeperConnector = new GatekeeperServiceConnector(m_AssetService);

            m_log.Debug("[HYPERGRID LINKER]: Loaded all services...");

            if (!string.IsNullOrEmpty(m_MapTileDirectory))
            {
                try
                {
                    Directory.CreateDirectory(m_MapTileDirectory);
                }
                catch (Exception e)
                {
                    m_log.WarnFormat("[HYPERGRID LINKER]: Could not create map tile storage directory {0}: {1}", m_MapTileDirectory, e);
                    m_MapTileDirectory = string.Empty;
                }
            }

            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand("Hypergrid", false, "link-region",
                    "link-region <Xloc> <Yloc> <ServerURI> [<RemoteRegionName>]",
                    "Link a HyperGrid Region. Examples for <ServerURI>: http://grid.net:8002/ or http://example.org/path/foo.php", RunCommand);
                MainConsole.Instance.Commands.AddCommand("Hypergrid", false, "link-region",
                    "link-region <Xloc> <Yloc> <RegionIP> <RegionPort> [<RemoteRegionName>]",
                    "Link a hypergrid region (deprecated)", RunCommand);
                MainConsole.Instance.Commands.AddCommand("Hypergrid", false, "unlink-region",
                    "unlink-region <local name>",
                    "Unlink a hypergrid region", RunCommand);
                MainConsole.Instance.Commands.AddCommand("Hypergrid", false, "link-mapping", "link-mapping [<x> <y>]",
                    "Set local coordinate to map HG regions to", RunCommand);
                MainConsole.Instance.Commands.AddCommand("Hypergrid", false, "show hyperlinks", "show hyperlinks",
                    "List the HG regions", HandleShow);
            }
        }


        #region Link Region

        // from map search
        public GridRegion LinkRegion(UUID scopeID, string regionDescriptor)
        {
            string reason = string.Empty;
            uint xloc = Util.RegionToWorldLoc((uint)random.Next(0, Int16.MaxValue));
            return TryLinkRegionToCoords(scopeID, regionDescriptor, (int)xloc, 0, out reason);
        }

        private static Random random = new Random();

        // From the command line link-region (obsolete) and the map
        private GridRegion TryLinkRegionToCoords(UUID scopeID, string mapName, int xloc, int yloc, out string reason)
        {
            return TryLinkRegionToCoords(scopeID, mapName, xloc, yloc, UUID.Zero, out reason);
        }

        public GridRegion TryLinkRegionToCoords(UUID scopeID, string mapName, int xloc, int yloc, UUID ownerID, out string reason)
        {
            reason = string.Empty;
            GridRegion regInfo = null;

            mapName = mapName.Trim();

            if (!mapName.StartsWith("http"))
            {
                // Formats: grid.example.com:8002:region name
                //          grid.example.com:region name
                //          grid.example.com:8002
                //          grid.example.com

                string host;
                uint port = 80;
                string regionName = "";
                
                string[] parts = mapName.Split(new char[] { ':' });
                
                if (parts.Length == 0)
                {
                    reason = "Wrong format for link-region";
                    return null;
                }
                
                host = parts[0];
                
                if (parts.Length >= 2)
                {
                    // If it's a number then assume it's a port. Otherwise, it's a region name.
                    if (!UInt32.TryParse(parts[1], out port))
                        regionName = parts[1];
                }

                // always take the last one
                if (parts.Length >= 3)
                {
                    regionName = parts[2];
                }
               
                bool success = TryCreateLink(scopeID, xloc, yloc, regionName, port, host, ownerID, out regInfo, out reason);
                if (success)
                {
                    regInfo.RegionName = mapName;
                    return regInfo;
                }
            }
            else
            {
                // Formats: http://grid.example.com region name
                //          http://grid.example.com "region name"
                //          http://grid.example.com

                string serverURI;
                string regionName = "";

                string[] parts = mapName.Split(new char[] { ' ' });

                if (parts.Length == 0)
                {
                    reason = "Wrong format for link-region";
                    return null;
                }

                serverURI = parts[0];

                if (parts.Length >= 2)
                {
                    regionName = mapName.Substring(serverURI.Length);
                    regionName = regionName.Trim(new char[] { '"', ' ' });
                }

                if (TryCreateLink(scopeID, xloc, yloc, regionName, 0, null, serverURI, ownerID, out regInfo, out reason))
                {
                    regInfo.RegionName = mapName; 
                    return regInfo;
                }
            }

            return null;
        }

        private bool TryCreateLink(UUID scopeID, int xloc, int yloc, string remoteRegionName, uint externalPort, string externalHostName, UUID ownerID, out GridRegion regInfo, out string reason)
        {
            return TryCreateLink(scopeID, xloc, yloc, remoteRegionName, externalPort, externalHostName, null, ownerID, out regInfo, out reason);
        }

        private bool TryCreateLink(UUID scopeID, int xloc, int yloc, string remoteRegionName, uint externalPort, string externalHostName, string serverURI, UUID ownerID, out GridRegion regInfo, out string reason)
        {
            lock (this)
            {
                return TryCreateLinkImpl(scopeID, xloc, yloc, remoteRegionName, externalPort, externalHostName, serverURI, ownerID, out regInfo, out reason);
            }
        }

        private bool TryCreateLinkImpl(UUID scopeID, int xloc, int yloc, string remoteRegionName, uint externalPort, string externalHostName, string serverURI, UUID ownerID, out GridRegion regInfo, out string reason)
        {
            m_log.InfoFormat("[HYPERGRID LINKER]: Link to {0} {1}, in <{2},{3}>", 
                ((serverURI == null) ? (externalHostName + ":" + externalPort) : serverURI),
                remoteRegionName, Util.WorldToRegionLoc((uint)xloc), Util.WorldToRegionLoc((uint)yloc));

            reason = string.Empty;
            Uri uri = null;

            regInfo = new GridRegion();
            if (externalPort > 0)
                regInfo.HttpPort = externalPort;
            else
                regInfo.HttpPort = 80;
            if (externalHostName != null)
                regInfo.ExternalHostName = externalHostName;
            else
                regInfo.ExternalHostName = "0.0.0.0";
            if (serverURI != null)
            {
                regInfo.ServerURI = serverURI;
                try
                {
                    uri = new Uri(serverURI);
                    regInfo.ExternalHostName = uri.Host;
                    regInfo.HttpPort = (uint)uri.Port;
                }
                catch {}
            }

            if (remoteRegionName != string.Empty)
                regInfo.RegionName = remoteRegionName;
                
            regInfo.RegionLocX = xloc;
            regInfo.RegionLocY = yloc;
            regInfo.ScopeID = scopeID;
            regInfo.EstateOwner = ownerID;

            // Make sure we're not hyperlinking to regions on this grid!
            if (m_ThisGatekeeperURI != null)
            {
                if (regInfo.ExternalHostName == m_ThisGatekeeperURI.Host && regInfo.HttpPort == m_ThisGatekeeperURI.Port)
                {
                    m_log.InfoFormat("[HYPERGRID LINKER]: Cannot hyperlink to regions on the same grid");
                    reason = "Cannot hyperlink to regions on the same grid";
                    return false;
                }
            }
            else
                m_log.WarnFormat("[HYPERGRID LINKER]: Please set this grid's Gatekeeper's address in [GridService]!");

            // Check for free coordinates
            GridRegion region = m_GridService.GetRegionByPosition(regInfo.ScopeID, regInfo.RegionLocX, regInfo.RegionLocY);
            if (region != null)
            {
                m_log.WarnFormat("[HYPERGRID LINKER]: Coordinates <{0},{1}> are already occupied by region {2} with uuid {3}",
                    Util.WorldToRegionLoc((uint)regInfo.RegionLocX), Util.WorldToRegionLoc((uint)regInfo.RegionLocY),
                    region.RegionName, region.RegionID);
                reason = "Coordinates are already in use";
                return false;
            }

            try
            {
                regInfo.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), (int)0);
            }
            catch (Exception e)
            {
                m_log.Warn("[HYPERGRID LINKER]: Wrong format for link-region: " + e.Message);
                reason = "Internal error";
                return false;
            }

            // Finally, link it
            ulong handle = 0;
            UUID regionID = UUID.Zero;
            string externalName = string.Empty;
            string imageURL = string.Empty;
            if (!m_GatekeeperConnector.LinkRegion(regInfo, out regionID, out handle, out externalName, out imageURL, out reason))
                return false;

            if (regionID == UUID.Zero)
            {
                m_log.Warn("[HYPERGRID LINKER]: Unable to link region");
                reason = "Remote region could not be found";
                return false;
            }

            region = m_GridService.GetRegionByUUID(scopeID, regionID);
            if (region != null)
            {
              m_log.DebugFormat("[HYPERGRID LINKER]: Region already exists in coordinates <{0},{1}>",            Util.WorldToRegionLoc((uint)region.RegionLocX), Util.WorldToRegionLoc((uint)region.RegionLocY));
                regInfo = region;
                return true;
            }

            // We are now performing this check for each individual teleport in the EntityTransferModule instead.  This
            // allows us to give better feedback when teleports fail because of the distance reason (which can't be
            // done here) and it also hypergrid teleports that are within range (possibly because the source grid
            // itself has regions that are very far apart).
//            uint x, y;
//            if (m_Check4096 && !Check4096(handle, out x, out y))
//            {
//                //RemoveHyperlinkRegion(regInfo.RegionID);
//                reason = "Region is too far (" + x + ", " + y + ")";
//                m_log.Info("[HYPERGRID LINKER]: Unable to link, region is too far (" + x + ", " + y + ")");
//                //return false;
//            }

            regInfo.RegionID = regionID;

            if (externalName == string.Empty)
                regInfo.RegionName = regInfo.ServerURI;
             else
                regInfo.RegionName = externalName;

            m_log.DebugFormat("[HYPERGRID LINKER]: naming linked region {0}, handle {1}", regInfo.RegionName, handle.ToString());
                
            // Get the map image
            regInfo.TerrainImage = GetMapImage(regionID, imageURL);

            // Store the origin's coordinates somewhere
            regInfo.RegionSecret = handle.ToString();

            AddHyperlinkRegion(regInfo, handle);
            m_log.InfoFormat("[HYPERGRID LINKER]: Successfully linked to region {0} at <{1},{2}> with image {3}",
                regInfo.RegionName, Util.WorldToRegionLoc((uint)regInfo.RegionLocX), Util.WorldToRegionLoc((uint)regInfo.RegionLocY), regInfo.TerrainImage);
            return true;
        }

        public bool TryUnlinkRegion(string mapName)
        {
            m_log.DebugFormat("[HYPERGRID LINKER]: Request to unlink {0}", mapName);
            GridRegion regInfo = null;

            List<RegionData> regions = m_Database.Get(Util.EscapeForLike(mapName), m_ScopeID);
            if (regions != null && regions.Count > 0)
            {
                OpenSim.Framework.RegionFlags rflags = (OpenSim.Framework.RegionFlags)Convert.ToInt32(regions[0].Data["flags"]);
                if ((rflags & OpenSim.Framework.RegionFlags.Hyperlink) != 0)
                {
                    regInfo = new GridRegion(); 
                    regInfo.RegionID = regions[0].RegionID;
                    regInfo.ScopeID = m_ScopeID;
                }
            }

            if (regInfo != null)
            {
                RemoveHyperlinkRegion(regInfo.RegionID);
                return true;
            }
            else
            {
                m_log.InfoFormat("[HYPERGRID LINKER]: Region {0} not found", mapName);
                return false;
            }
        }

// Not currently used
//        /// <summary>
//        /// Cope with this viewer limitation.
//        /// </summary>
//        /// <param name="regInfo"></param>
//        /// <returns></returns>
//        public bool Check4096(ulong realHandle, out uint x, out uint y)
//        {
//            uint ux = 0, uy = 0;
//            Utils.LongToUInts(realHandle, out ux, out uy);
//            x = Util.WorldToRegionLoc(ux);
//            y = Util.WorldToRegionLoc(uy);
//
//            const uint limit = Util.RegionToWorldLoc(4096 - 1);
//            uint xmin = ux - limit;
//            uint xmax = ux + limit;
//            uint ymin = uy - limit;
//            uint ymax = uy + limit;
//            // World map boundary checks
//            if (xmin < 0 || xmin > ux)
//                xmin = 0;
//            if (xmax > int.MaxValue || xmax < ux)
//                xmax = int.MaxValue;
//            if (ymin < 0 || ymin > uy)
//                ymin = 0;
//            if (ymax > int.MaxValue || ymax < uy)
//                ymax = int.MaxValue;
//
//            // Check for any regions that are within the possible teleport range to the linked region
//            List<GridRegion> regions = m_GridService.GetRegionRange(m_ScopeID, (int)xmin, (int)xmax, (int)ymin, (int)ymax);
//            if (regions.Count == 0)
//            {
//                return false;
//            }
//            else
//            {
//                // Check for regions which are not linked regions
//                List<GridRegion> hyperlinks = m_GridService.GetHyperlinks(m_ScopeID);
//                IEnumerable<GridRegion> availableRegions = regions.Except(hyperlinks);
//                if (availableRegions.Count() == 0)
//                    return false;
//            }
//
//            return true;
//        }

        private void AddHyperlinkRegion(GridRegion regionInfo, ulong regionHandle)
        {
            RegionData rdata = m_GridService.RegionInfo2RegionData(regionInfo);
            int flags = (int)OpenSim.Framework.RegionFlags.Hyperlink + (int)OpenSim.Framework.RegionFlags.NoDirectLogin + (int)OpenSim.Framework.RegionFlags.RegionOnline;
            rdata.Data["flags"] = flags.ToString();

            m_Database.Store(rdata);
        }

        private void RemoveHyperlinkRegion(UUID regionID)
        {
            m_Database.Delete(regionID);
        }

        public UUID GetMapImage(UUID regionID, string imageURL)
        {
            return m_GatekeeperConnector.GetMapImage(regionID, imageURL, m_MapTileDirectory);
        }
        #endregion


        #region Console Commands

        public void HandleShow(string module, string[] cmd)
        {
            if (cmd.Length != 2)
            {
                MainConsole.Instance.Output("Syntax: show hyperlinks");
                return;
            }
            List<RegionData> regions = m_Database.GetHyperlinks(UUID.Zero);
            if (regions == null || regions.Count < 1)
            {
                MainConsole.Instance.Output("No hyperlinks");
                return;
            }

            MainConsole.Instance.Output("Region Name");
            MainConsole.Instance.Output("Location                         Region UUID");
            MainConsole.Instance.Output(new string('-', 72));
            foreach (RegionData r in regions)
            {
                MainConsole.Instance.Output(
                    String.Format("{0}\n{2,-32} {1}\n",
                        r.RegionName, r.RegionID, 
                        String.Format("{0},{1} ({2},{3})", r.posX, r.posY,
                                    Util.WorldToRegionLoc((uint)r.posX), Util.WorldToRegionLoc((uint)r.posY)
                        )
                    )
                );
            }
            return;
        }

        public void RunCommand(string module, string[] cmdparams)
        {
            List<string> args = new List<string>(cmdparams);
            if (args.Count < 1)
                return;

            string command = args[0];
            args.RemoveAt(0);

            cmdparams = args.ToArray();

            RunHGCommand(command, cmdparams);

        }
        
        private void RunLinkRegionCommand(string[] cmdparams)
        {
            int xloc, yloc;
            string serverURI;
            string remoteName = null;
            xloc = (int)Util.RegionToWorldLoc((uint)Convert.ToInt32(cmdparams[0]));
            yloc = (int)Util.RegionToWorldLoc((uint)Convert.ToInt32(cmdparams[1]));
            serverURI = cmdparams[2];
            if (cmdparams.Length > 3)
                remoteName = string.Join(" ", cmdparams, 3, cmdparams.Length - 3);
            string reason = string.Empty;
            GridRegion regInfo;
            if (TryCreateLink(UUID.Zero, xloc, yloc, remoteName, 0, null, serverURI, UUID.Zero, out regInfo, out reason))
                MainConsole.Instance.Output("Hyperlink established");
            else
                MainConsole.Instance.Output("Failed to link region: " + reason);
        }

        private void RunHGCommand(string command, string[] cmdparams)
        {
            if (command.Equals("link-mapping"))
            {
                if (cmdparams.Length == 2)
                {
                    try
                    {
                        m_autoMappingX = Convert.ToUInt32(cmdparams[0]);
                        m_autoMappingY = Convert.ToUInt32(cmdparams[1]);
                        m_enableAutoMapping = true;
                    }
                    catch (Exception)
                    {
                        m_autoMappingX = 0;
                        m_autoMappingY = 0;
                        m_enableAutoMapping = false;
                    }
                }
            }
            else if (command.Equals("link-region"))
            {
                if (cmdparams.Length < 3)
                {
                    if ((cmdparams.Length == 1) || (cmdparams.Length == 2))
                    {
                        LoadXmlLinkFile(cmdparams);
                    }
                    else
                    {
                        LinkRegionCmdUsage();
                    }
                    return;
                }

                //this should be the prefererred way of setting up hg links now
                if (cmdparams[2].StartsWith("http"))
                {
                    RunLinkRegionCommand(cmdparams);
                } 
                else if (cmdparams[2].Contains(":"))
                {
                    // New format
                    string[] parts = cmdparams[2].Split(':');
                    if (parts.Length > 2)
                    {
                        // Insert remote region name
                        ArrayList parameters = new ArrayList(cmdparams);
                        parameters.Insert(3, parts[2]);
                        cmdparams = (string[])parameters.ToArray(typeof(string));
                    }
                    cmdparams[2] = "http://" + parts[0] + ':' + parts[1];

                    RunLinkRegionCommand(cmdparams);
                }
                else
                {
                    // old format
                    GridRegion regInfo;
                    uint xloc, yloc;
                    uint externalPort;
                    string externalHostName;
                    try
                    {
                        xloc = Convert.ToUInt32(cmdparams[0]);
                        yloc = Convert.ToUInt32(cmdparams[1]);
                        externalPort = Convert.ToUInt32(cmdparams[3]);
                        externalHostName = cmdparams[2];
                        //internalPort = Convert.ToUInt32(cmdparams[4]);
                        //remotingPort = Convert.ToUInt32(cmdparams[5]);
                    }
                    catch (Exception e)
                    {
                        MainConsole.Instance.Output("[HGrid] Wrong format for link-region command: " + e.Message);
                        LinkRegionCmdUsage();
                        return;
                    }

                    // Convert cell coordinates given by the user to meters
                    xloc = Util.RegionToWorldLoc(xloc);
                    yloc = Util.RegionToWorldLoc(yloc);
                    string reason = string.Empty;
                    if (TryCreateLink(UUID.Zero, (int)xloc, (int)yloc,
                                    string.Empty, externalPort, externalHostName, UUID.Zero, out regInfo, out reason))
                    {
                        // What is this? The GridRegion instance will be discarded anyway,
                        // which effectively ignores any local name given with the command.
                        //if (cmdparams.Length >= 5)
                        //{
                        //    regInfo.RegionName = "";
                        //    for (int i = 4; i < cmdparams.Length; i++)
                        //        regInfo.RegionName += cmdparams[i] + " ";
                        //}
                    }
                }
                return;
            }
            else if (command.Equals("unlink-region"))
            {
                if (cmdparams.Length < 1)
                {
                    UnlinkRegionCmdUsage();
                    return;
                }
                string region = string.Join(" ", cmdparams);
                if (TryUnlinkRegion(region))
                    MainConsole.Instance.Output("Successfully unlinked " + region);
                else
                    MainConsole.Instance.Output("Unable to unlink " + region + ", region not found.");
            }
        }

        private void LoadXmlLinkFile(string[] cmdparams)
        {
            //use http://www.hgurl.com/hypergrid.xml for test
            try
            {
                XmlReader r = XmlReader.Create(cmdparams[0]);
                XmlConfigSource cs = new XmlConfigSource(r);
                string[] excludeSections = null;

                if (cmdparams.Length == 2)
                {
                    if (cmdparams[1].ToLower().StartsWith("excludelist:"))
                    {
                        string excludeString = cmdparams[1].ToLower();
                        excludeString = excludeString.Remove(0, 12);
                        char[] splitter = { ';' };

                        excludeSections = excludeString.Split(splitter);
                    }
                }

                for (int i = 0; i < cs.Configs.Count; i++)
                {
                    bool skip = false;
                    if ((excludeSections != null) && (excludeSections.Length > 0))
                    {
                        for (int n = 0; n < excludeSections.Length; n++)
                        {
                            if (excludeSections[n] == cs.Configs[i].Name.ToLower())
                            {
                                skip = true;
                                break;
                            }
                        }
                    }
                    if (!skip)
                    {
                        ReadLinkFromConfig(cs.Configs[i]);
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
        }


        private void ReadLinkFromConfig(IConfig config)
        {
            GridRegion regInfo;
            uint xloc, yloc;
            uint externalPort;
            string externalHostName;
            uint realXLoc, realYLoc;

            xloc = Convert.ToUInt32(config.GetString("xloc", "0"));
            yloc = Convert.ToUInt32(config.GetString("yloc", "0"));
            externalPort = Convert.ToUInt32(config.GetString("externalPort", "0"));
            externalHostName = config.GetString("externalHostName", "");
            realXLoc = Convert.ToUInt32(config.GetString("real-xloc", "0"));
            realYLoc = Convert.ToUInt32(config.GetString("real-yloc", "0"));

            if (m_enableAutoMapping)
            {
                xloc = (xloc % 100) + m_autoMappingX;
                yloc = (yloc % 100) + m_autoMappingY;
            }

            if (((realXLoc == 0) && (realYLoc == 0)) ||
                (((realXLoc - xloc < 3896) || (xloc - realXLoc < 3896)) &&
                 ((realYLoc - yloc < 3896) || (yloc - realYLoc < 3896))))
            {
                xloc = Util.RegionToWorldLoc(xloc);
                yloc = Util.RegionToWorldLoc(yloc);
                string reason = string.Empty;
                if (TryCreateLink(UUID.Zero, (int)xloc, (int)yloc,
                                string.Empty, externalPort, externalHostName, UUID.Zero, out regInfo, out reason))
                {
                    regInfo.RegionName = config.GetString("localName", "");
                }
                else
                    MainConsole.Instance.Output("Unable to link " + externalHostName + ": " + reason);
            }
        }


        private void LinkRegionCmdUsage()
        {
            MainConsole.Instance.Output("Usage: link-region <Xloc> <Yloc> <ServerURI> [<RemoteRegionName>]");
            MainConsole.Instance.Output("Usage (deprecated): link-region <Xloc> <Yloc> <HostName>:<HttpPort>[:<RemoteRegionName>]");
            MainConsole.Instance.Output("Usage (deprecated): link-region <Xloc> <Yloc> <HostName> <HttpPort> [<LocalName>]");
            MainConsole.Instance.Output("Usage: link-region <URI_of_xml> [<exclude>]");
        }

        private void UnlinkRegionCmdUsage()
        {
            MainConsole.Instance.Output("Usage: unlink-region <LocalName>");
        }

        #endregion

    }
}
