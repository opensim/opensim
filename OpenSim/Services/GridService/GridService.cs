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
using System.Net;
using System.Reflection;
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Data;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenMetaverse;

namespace OpenSim.Services.GridService
{
    public class GridService : GridServiceBase, IGridService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_DeleteOnUnregister = true;
        private static GridService m_RootInstance = null;
        protected IConfigSource m_config;
        protected static HypergridLinker m_HypergridLinker;

        protected IAuthenticationService m_AuthenticationService = null;
        protected bool m_AllowDuplicateNames = false;
        protected bool m_AllowHypergridMapSearch = false;

        public GridService(IConfigSource config)
            : base(config)
        {
            m_log.DebugFormat("[GRID SERVICE]: Starting...");

            m_config = config;
            IConfig gridConfig = config.Configs["GridService"];
            if (gridConfig != null)
            {
                m_DeleteOnUnregister = gridConfig.GetBoolean("DeleteOnUnregister", true);
                
                string authService = gridConfig.GetString("AuthenticationService", String.Empty);

                if (authService != String.Empty)
                {
                    Object[] args = new Object[] { config };
                    m_AuthenticationService = ServerUtils.LoadPlugin<IAuthenticationService>(authService, args);
                }
                m_AllowDuplicateNames = gridConfig.GetBoolean("AllowDuplicateNames", m_AllowDuplicateNames);
                m_AllowHypergridMapSearch = gridConfig.GetBoolean("AllowHypergridMapSearch", m_AllowHypergridMapSearch);
            }
            
            if (m_RootInstance == null)
            {
                m_RootInstance = this;

                if (MainConsole.Instance != null)
                {
                    MainConsole.Instance.Commands.AddCommand("Regions", true,
                            "deregister region id",
                            "deregister region id <Region UUID>",
                            "Deregister a region manually.",
                            String.Empty,
                            HandleDeregisterRegion);

                    // A messy way of stopping this command being added if we are in standalone (since the simulator
                    // has an identically named command
                    //
                    // XXX: We're relying on the OpenSimulator version being registered first, which is not well defined.
                    if (MainConsole.Instance.Commands.Resolve(new string[] { "show", "regions" }).Length == 0)
                        MainConsole.Instance.Commands.AddCommand("Regions", true,
                                "show regions",
                                "show regions",
                                "Show details on all regions",
                                String.Empty,
                                HandleShowRegions);

                    MainConsole.Instance.Commands.AddCommand("Regions", true,
                            "show region name",
                            "show region name <Region name>",
                            "Show details on a region",
                            String.Empty,
                            HandleShowRegion);

                    MainConsole.Instance.Commands.AddCommand("Regions", true,
                            "show region at",
                            "show region at <x-coord> <y-coord>",
                            "Show details on a region at the given co-ordinate.",
                            "For example, show region at 1000 1000",
                            HandleShowRegionAt);

                    MainConsole.Instance.Commands.AddCommand("Regions", true,
                            "set region flags",
                            "set region flags <Region name> <flags>",
                            "Set database flags for region",
                            String.Empty,
                            HandleSetFlags);
                }
                m_HypergridLinker = new HypergridLinker(m_config, this, m_Database);
            }
        }

        #region IGridService

        public string RegisterRegion(UUID scopeID, GridRegion regionInfos)
        {
            IConfig gridConfig = m_config.Configs["GridService"];

            if (regionInfos.RegionID == UUID.Zero)
                return "Invalid RegionID - cannot be zero UUID";

            RegionData region = m_Database.Get(regionInfos.RegionLocX, regionInfos.RegionLocY, scopeID);
            if ((region != null) && (region.RegionID != regionInfos.RegionID))
            {
                m_log.WarnFormat("[GRID SERVICE]: Region {0} tried to register in coordinates {1}, {2} which are already in use in scope {3}.", 
                    regionInfos.RegionID, regionInfos.RegionLocX, regionInfos.RegionLocY, scopeID);
                return "Region overlaps another region";
            }

            if (region != null)
            {
                // There is a preexisting record
                //
                // Get it's flags
                //
                OpenSim.Framework.RegionFlags rflags = (OpenSim.Framework.RegionFlags)Convert.ToInt32(region.Data["flags"]);

                // Is this a reservation?
                //
                if ((rflags & OpenSim.Framework.RegionFlags.Reservation) != 0)
                {
                    // Regions reserved for the null key cannot be taken.
                    if ((string)region.Data["PrincipalID"] == UUID.Zero.ToString())
                        return "Region location is reserved";

                    // Treat it as an auth request
                    //
                    // NOTE: Fudging the flags value here, so these flags
                    //       should not be used elsewhere. Don't optimize
                    //       this with the later retrieval of the same flags!
                    rflags |= OpenSim.Framework.RegionFlags.Authenticate;
                }

                if ((rflags & OpenSim.Framework.RegionFlags.Authenticate) != 0)
                {
                    // Can we authenticate at all?
                    //
                    if (m_AuthenticationService == null)
                        return "No authentication possible";

                    if (!m_AuthenticationService.Verify(new UUID(region.Data["PrincipalID"].ToString()), regionInfos.Token, 30))
                        return "Bad authentication";
                }
            }

            // If we get here, the destination is clear. Now for the real check.

            if (!m_AllowDuplicateNames)
            {
                List<RegionData> dupe = m_Database.Get(regionInfos.RegionName, scopeID);
                if (dupe != null && dupe.Count > 0)
                {
                    foreach (RegionData d in dupe)
                    {
                        if (d.RegionID != regionInfos.RegionID)
                        {
                            m_log.WarnFormat("[GRID SERVICE]: Region {0} tried to register duplicate name with ID {1}.", 
                                regionInfos.RegionName, regionInfos.RegionID);
                            return "Duplicate region name";
                        }
                    }
                }
            }

            // If there is an old record for us, delete it if it is elsewhere.
            region = m_Database.Get(regionInfos.RegionID, scopeID);
            if ((region != null) && (region.RegionID == regionInfos.RegionID) && 
                ((region.posX != regionInfos.RegionLocX) || (region.posY != regionInfos.RegionLocY)))
            {
                if ((Convert.ToInt32(region.Data["flags"]) & (int)OpenSim.Framework.RegionFlags.NoMove) != 0)
                    return "Can't move this region";

                if ((Convert.ToInt32(region.Data["flags"]) & (int)OpenSim.Framework.RegionFlags.LockedOut) != 0)
                    return "Region locked out";

                // Region reregistering in other coordinates. Delete the old entry
                m_log.DebugFormat("[GRID SERVICE]: Region {0} ({1}) was previously registered at {2}-{3}. Deleting old entry.",
                    regionInfos.RegionName, regionInfos.RegionID, regionInfos.RegionLocX, regionInfos.RegionLocY);

                try
                {
                    m_Database.Delete(regionInfos.RegionID);
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[GRID SERVICE]: Database exception: {0}", e);
                }
            }

            // Everything is ok, let's register
            RegionData rdata = RegionInfo2RegionData(regionInfos);
            rdata.ScopeID = scopeID;
            
            if (region != null)
            {
                int oldFlags = Convert.ToInt32(region.Data["flags"]);

                oldFlags &= ~(int)OpenSim.Framework.RegionFlags.Reservation;

                rdata.Data["flags"] = oldFlags.ToString(); // Preserve flags
            }
            else
            {
                rdata.Data["flags"] = "0";
                if ((gridConfig != null) && rdata.RegionName != string.Empty)
                {
                    int newFlags = 0;
                    string regionName = rdata.RegionName.Trim().Replace(' ', '_');
                    newFlags = ParseFlags(newFlags, gridConfig.GetString("DefaultRegionFlags", String.Empty));
                    newFlags = ParseFlags(newFlags, gridConfig.GetString("Region_" + regionName, String.Empty));
                    newFlags = ParseFlags(newFlags, gridConfig.GetString("Region_" + rdata.RegionID.ToString(), String.Empty));
                    rdata.Data["flags"] = newFlags.ToString();
                }
            }

            int flags = Convert.ToInt32(rdata.Data["flags"]);
            flags |= (int)OpenSim.Framework.RegionFlags.RegionOnline;
            rdata.Data["flags"] = flags.ToString();

            try
            {
                rdata.Data["last_seen"] = Util.UnixTimeSinceEpoch();
                m_Database.Store(rdata);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[GRID SERVICE]: Database exception: {0}", e);
            }

            m_log.DebugFormat("[GRID SERVICE]: Region {0} ({1}) registered successfully at {2}-{3}", 
                regionInfos.RegionName, regionInfos.RegionID, regionInfos.RegionCoordX, regionInfos.RegionCoordY);

            return String.Empty;
        }

        public bool DeregisterRegion(UUID regionID)
        {
            RegionData region = m_Database.Get(regionID, UUID.Zero);
            if (region == null)
                return false;

            m_log.DebugFormat(
                "[GRID SERVICE]: Deregistering region {0} ({1}) at {2}-{3}",
                region.RegionName, region.RegionID, region.coordX, region.coordY);

            int flags = Convert.ToInt32(region.Data["flags"]);

            if (!m_DeleteOnUnregister || (flags & (int)OpenSim.Framework.RegionFlags.Persistent) != 0)
            {
                flags &= ~(int)OpenSim.Framework.RegionFlags.RegionOnline;
                region.Data["flags"] = flags.ToString();
                region.Data["last_seen"] = Util.UnixTimeSinceEpoch();
                try
                {
                    m_Database.Store(region);
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[GRID SERVICE]: Database exception: {0}", e);
                }

                return true;

            }

            return m_Database.Delete(regionID);
        }

        public List<GridRegion> GetNeighbours(UUID scopeID, UUID regionID)
        {
            List<GridRegion> rinfos = new List<GridRegion>();
            RegionData region = m_Database.Get(regionID, scopeID);
            
            if (region != null)
            {
                // Not really? Maybe?
                List<RegionData> rdatas = m_Database.Get(region.posX - (int)Constants.RegionSize - 1, region.posY - (int)Constants.RegionSize - 1, 
                    region.posX + (int)Constants.RegionSize + 1, region.posY + (int)Constants.RegionSize + 1, scopeID);

                foreach (RegionData rdata in rdatas)
                {
                    if (rdata.RegionID != regionID)
                    {
                        int flags = Convert.ToInt32(rdata.Data["flags"]);
                        if ((flags & (int)Framework.RegionFlags.Hyperlink) == 0) // no hyperlinks as neighbours
                            rinfos.Add(RegionData2RegionInfo(rdata));
                    }
                }

//                m_log.DebugFormat("[GRID SERVICE]: region {0} has {1} neighbours", region.RegionName, rinfos.Count);
            }
            else
            {
                m_log.WarnFormat(
                    "[GRID SERVICE]: GetNeighbours() called for scope {0}, region {1} but no such region found", 
                    scopeID, regionID);
            }
            
            return rinfos;
        }

        public GridRegion GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            RegionData rdata = m_Database.Get(regionID, scopeID);
            if (rdata != null)
                return RegionData2RegionInfo(rdata);

            return null;
        }

        public GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            int snapX = (int)(x / Constants.RegionSize) * (int)Constants.RegionSize;
            int snapY = (int)(y / Constants.RegionSize) * (int)Constants.RegionSize;
            RegionData rdata = m_Database.Get(snapX, snapY, scopeID);
            if (rdata != null)
                return RegionData2RegionInfo(rdata);

            return null;
        }

        public GridRegion GetRegionByName(UUID scopeID, string name)
        {
            List<RegionData> rdatas = m_Database.Get(name, scopeID);
            if ((rdatas != null) && (rdatas.Count > 0))
                return RegionData2RegionInfo(rdatas[0]); // get the first

            if (m_AllowHypergridMapSearch)
            {
                GridRegion r = GetHypergridRegionByName(scopeID, name);
                if (r != null)
                    return r;
            }

            return null;
        }

        public List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber)
        {
//            m_log.DebugFormat("[GRID SERVICE]: GetRegionsByName {0}", name);

            List<RegionData> rdatas = m_Database.Get(name + "%", scopeID);

            int count = 0;
            List<GridRegion> rinfos = new List<GridRegion>();

            if (rdatas != null)
            {
//                m_log.DebugFormat("[GRID SERVICE]: Found {0} regions", rdatas.Count);
                foreach (RegionData rdata in rdatas)
                {
                    if (count++ < maxNumber)
                        rinfos.Add(RegionData2RegionInfo(rdata));
                }
            }

            if (m_AllowHypergridMapSearch && (rdatas == null || (rdatas != null && rdatas.Count == 0)))
            {
                GridRegion r = GetHypergridRegionByName(scopeID, name);
                if (r != null)
                    rinfos.Add(r);
            }

            return rinfos;
        }

        /// <summary>
        /// Get a hypergrid region.
        /// </summary>
        /// <param name="scopeID"></param>
        /// <param name="name"></param>
        /// <returns>null if no hypergrid region could be found.</returns>
        protected GridRegion GetHypergridRegionByName(UUID scopeID, string name)
        {
            if (name.Contains("."))
                return m_HypergridLinker.LinkRegion(scopeID, name);
            else
                return null;
        }

        public List<GridRegion> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax)
        {
            int xminSnap = (int)(xmin / Constants.RegionSize) * (int)Constants.RegionSize;
            int xmaxSnap = (int)(xmax / Constants.RegionSize) * (int)Constants.RegionSize;
            int yminSnap = (int)(ymin / Constants.RegionSize) * (int)Constants.RegionSize;
            int ymaxSnap = (int)(ymax / Constants.RegionSize) * (int)Constants.RegionSize;

            List<RegionData> rdatas = m_Database.Get(xminSnap, yminSnap, xmaxSnap, ymaxSnap, scopeID);
            List<GridRegion> rinfos = new List<GridRegion>();
            foreach (RegionData rdata in rdatas)
                rinfos.Add(RegionData2RegionInfo(rdata));

            return rinfos;
        }

        #endregion

        #region Data structure conversions

        public RegionData RegionInfo2RegionData(GridRegion rinfo)
        {
            RegionData rdata = new RegionData();
            rdata.posX = (int)rinfo.RegionLocX;
            rdata.posY = (int)rinfo.RegionLocY;
            rdata.RegionID = rinfo.RegionID;
            rdata.RegionName = rinfo.RegionName;
            rdata.Data = rinfo.ToKeyValuePairs();
            rdata.Data["regionHandle"] = Utils.UIntsToLong((uint)rdata.posX, (uint)rdata.posY);
            rdata.Data["owner_uuid"] = rinfo.EstateOwner.ToString();
            return rdata;
        }

        public GridRegion RegionData2RegionInfo(RegionData rdata)
        {
            GridRegion rinfo = new GridRegion(rdata.Data);
            rinfo.RegionLocX = rdata.posX;
            rinfo.RegionLocY = rdata.posY;
            rinfo.RegionID = rdata.RegionID;
            rinfo.RegionName = rdata.RegionName;
            rinfo.ScopeID = rdata.ScopeID;

            return rinfo;
        }

        #endregion 

        public List<GridRegion> GetDefaultRegions(UUID scopeID)
        {
            List<GridRegion> ret = new List<GridRegion>();

            List<RegionData> regions = m_Database.GetDefaultRegions(scopeID);

            foreach (RegionData r in regions)
            {
                if ((Convert.ToInt32(r.Data["flags"]) & (int)OpenSim.Framework.RegionFlags.RegionOnline) != 0)
                    ret.Add(RegionData2RegionInfo(r));
            }

            m_log.DebugFormat("[GRID SERVICE]: GetDefaultRegions returning {0} regions", ret.Count);
            return ret;
        }

        public List<GridRegion> GetFallbackRegions(UUID scopeID, int x, int y)
        {
            List<GridRegion> ret = new List<GridRegion>();

            List<RegionData> regions = m_Database.GetFallbackRegions(scopeID, x, y);

            foreach (RegionData r in regions)
            {
                if ((Convert.ToInt32(r.Data["flags"]) & (int)OpenSim.Framework.RegionFlags.RegionOnline) != 0)
                    ret.Add(RegionData2RegionInfo(r));
            }

            m_log.DebugFormat("[GRID SERVICE]: Fallback returned {0} regions", ret.Count);
            return ret;
        }

        public List<GridRegion> GetHyperlinks(UUID scopeID)
        {
            List<GridRegion> ret = new List<GridRegion>();

            List<RegionData> regions = m_Database.GetHyperlinks(scopeID);

            foreach (RegionData r in regions)
            {
                if ((Convert.ToInt32(r.Data["flags"]) & (int)OpenSim.Framework.RegionFlags.RegionOnline) != 0)
                    ret.Add(RegionData2RegionInfo(r));
            }

            m_log.DebugFormat("[GRID SERVICE]: Hyperlinks returned {0} regions", ret.Count);
            return ret;
        }
        
        public int GetRegionFlags(UUID scopeID, UUID regionID)
        {
            RegionData region = m_Database.Get(regionID, scopeID);

            if (region != null)
            {
                int flags = Convert.ToInt32(region.Data["flags"]);
                //m_log.DebugFormat("[GRID SERVICE]: Request for flags of {0}: {1}", regionID, flags);
                return flags;
            }
            else
                return -1;
        }

        private void HandleDeregisterRegion(string module, string[] cmd)
        {
            if (cmd.Length != 4)
            {
                MainConsole.Instance.Output("Syntax: degregister region id <Region UUID>");
                return;
            }

            string rawRegionUuid = cmd[3];
            UUID regionUuid;

            if (!UUID.TryParse(rawRegionUuid, out regionUuid))
            {
                MainConsole.Instance.OutputFormat("{0} is not a valid region uuid", rawRegionUuid);
                return;
            }

            GridRegion region = GetRegionByUUID(UUID.Zero, regionUuid);

            if (region == null)
            {
                MainConsole.Instance.OutputFormat("No region with UUID {0}", regionUuid);
                return;
            }

            if (DeregisterRegion(regionUuid))
            {
                MainConsole.Instance.OutputFormat("Deregistered {0} {1}", region.RegionName, regionUuid);
            }
            else
            {
                // I don't think this can ever occur if we know that the region exists.
                MainConsole.Instance.OutputFormat("Error deregistering {0} {1}", region.RegionName, regionUuid);
            }

            return;
        }

        private void HandleShowRegions(string module, string[] cmd)
        {
            if (cmd.Length != 2)
            {
                MainConsole.Instance.Output("Syntax: show regions");
                return;
            }

            List<RegionData> regions = m_Database.Get(int.MinValue, int.MinValue, int.MaxValue, int.MaxValue, UUID.Zero);

            OutputRegionsToConsoleSummary(regions);
        }


        private void HandleShowRegion(string module, string[] cmd)
        {
            if (cmd.Length != 4)
            {
                MainConsole.Instance.Output("Syntax: show region name <region name>");
                return;
            }

            string regionName = cmd[3];

            List<RegionData> regions = m_Database.Get(regionName, UUID.Zero);
            if (regions == null || regions.Count < 1)
            {
                MainConsole.Instance.Output("No region with name {0} found", regionName);
                return;
            }

            OutputRegionsToConsole(regions);
        }

        private void HandleShowRegionAt(string module, string[] cmd)
        {
            if (cmd.Length != 5)
            {
                MainConsole.Instance.Output("Syntax: show region at <x-coord> <y-coord>");
                return;
            }

            int x, y;
            if (!int.TryParse(cmd[3], out x))
            {
                MainConsole.Instance.Output("x-coord must be an integer");
                return;
            }

            if (!int.TryParse(cmd[4], out y))
            {
                MainConsole.Instance.Output("y-coord must be an integer");
                return;
            }

            RegionData region = m_Database.Get(x * (int)Constants.RegionSize, y * (int)Constants.RegionSize, UUID.Zero);
            if (region == null)
            {
                MainConsole.Instance.OutputFormat("No region found at {0},{1}", x, y);
                return;
            }

            OutputRegionToConsole(region);
        }

        private void OutputRegionToConsole(RegionData r)
        {
            OpenSim.Framework.RegionFlags flags = (OpenSim.Framework.RegionFlags)Convert.ToInt32(r.Data["flags"]);

            ConsoleDisplayList dispList = new ConsoleDisplayList();
            dispList.AddRow("Region Name", r.RegionName);
            dispList.AddRow("Region ID", r.RegionID);
            dispList.AddRow("Location", string.Format("{0},{1}", r.coordX, r.coordY));
            dispList.AddRow("URI", r.Data["serverURI"]);
            dispList.AddRow("Owner ID", r.Data["owner_uuid"]);
            dispList.AddRow("Flags", flags);

            MainConsole.Instance.Output(dispList.ToString());
        }

        private void OutputRegionsToConsole(List<RegionData> regions)
        {
            foreach (RegionData r in regions)
                OutputRegionToConsole(r);
        }

        private void OutputRegionsToConsoleSummary(List<RegionData> regions)
        {
            ConsoleDisplayTable dispTable = new ConsoleDisplayTable();
            dispTable.AddColumn("Name", 16);
            dispTable.AddColumn("ID", 36);
            dispTable.AddColumn("Position", 11);
            dispTable.AddColumn("Owner ID", 36);
            dispTable.AddColumn("Flags", 60);

            foreach (RegionData r in regions)
            {
                OpenSim.Framework.RegionFlags flags = (OpenSim.Framework.RegionFlags)Convert.ToInt32(r.Data["flags"]);
                dispTable.AddRow(
                    r.RegionName,
                    r.RegionID.ToString(),
                    string.Format("{0},{1}", r.coordX, r.coordY),
                    r.Data["owner_uuid"].ToString(),
                    flags.ToString());
            }

            MainConsole.Instance.Output(dispTable.ToString());
        }

        private int ParseFlags(int prev, string flags)
        {
            OpenSim.Framework.RegionFlags f = (OpenSim.Framework.RegionFlags)prev;

            string[] parts = flags.Split(new char[] {',', ' '}, StringSplitOptions.RemoveEmptyEntries);

            foreach (string p in parts)
            {
                int val;

                try
                {
                    if (p.StartsWith("+"))
                    {
                        val = (int)Enum.Parse(typeof(OpenSim.Framework.RegionFlags), p.Substring(1));
                        f |= (OpenSim.Framework.RegionFlags)val;
                    }
                    else if (p.StartsWith("-"))
                    {
                        val = (int)Enum.Parse(typeof(OpenSim.Framework.RegionFlags), p.Substring(1));
                        f &= ~(OpenSim.Framework.RegionFlags)val;
                    }
                    else
                    {
                        val = (int)Enum.Parse(typeof(OpenSim.Framework.RegionFlags), p);
                        f |= (OpenSim.Framework.RegionFlags)val;
                    }
                }
                catch (Exception)
                {
                    MainConsole.Instance.Output("Error in flag specification: " + p);
                }
            }

            return (int)f;
        }

        private void HandleSetFlags(string module, string[] cmd)
        {
            if (cmd.Length < 5)
            {
                MainConsole.Instance.Output("Syntax: set region flags <region name> <flags>");
                return;
            }

            List<RegionData> regions = m_Database.Get(cmd[3], UUID.Zero);
            if (regions == null || regions.Count < 1)
            {
                MainConsole.Instance.Output("Region not found");
                return;
            }

            foreach (RegionData r in regions)
            {
                int flags = Convert.ToInt32(r.Data["flags"]);
                flags = ParseFlags(flags, cmd[4]);
                r.Data["flags"] = flags.ToString();
                OpenSim.Framework.RegionFlags f = (OpenSim.Framework.RegionFlags)flags;

                MainConsole.Instance.Output(String.Format("Set region {0} to {1}", r.RegionName, f));
                m_Database.Store(r);
            }
        }
    }
}
