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
using System.Reflection;
using System.Xml;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Servers;


namespace OpenSim.Grid.GridServer.Modules
{
    public class GridDBService : IRegionProfileService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private List<IGridDataPlugin> _plugins = new List<IGridDataPlugin>();
        private List<ILogDataPlugin> _logplugins = new List<ILogDataPlugin>();

        /// <summary>
        /// Adds a list of grid and log data plugins, as described by
        /// `provider' and `connect', to `_plugins' and `_logplugins',
        /// respectively.
        /// </summary>
        /// <param name="provider">
        /// The filename of the inventory server plugin DLL.
        /// </param>
        /// <param name="connect">
        /// The connection string for the storage backend.
        /// </param>
        public void AddPlugin(string provider, string connect)
        {
            _plugins = DataPluginFactory.LoadDataPlugins<IGridDataPlugin>(provider, connect);
            _logplugins = DataPluginFactory.LoadDataPlugins<ILogDataPlugin>(provider, connect);
        }

        public int GetNumberOfPlugins()
        {
            return _plugins.Count;
        }

        /// <summary>
        /// Logs a piece of information to the database
        /// </summary>
        /// <param name="target">What you were operating on (in grid server, this will likely be the region UUIDs)</param>
        /// <param name="method">Which method is being called?</param>
        /// <param name="args">What arguments are being passed?</param>
        /// <param name="priority">How high priority is this? 1 = Max, 6 = Verbose</param>
        /// <param name="message">The message to log</param>
        private void logToDB(string target, string method, string args, int priority, string message)
        {
            foreach (ILogDataPlugin plugin in _logplugins)
            {
                try
                {
                    plugin.saveLog("Gridserver", target, method, args, priority, message);
                }
                catch (Exception)
                {
                    m_log.Warn("[storage]: Unable to write log via " + plugin.Name);
                }
            }
        }

        /// <summary>
        /// Returns a region by argument
        /// </summary>
        /// <param name="uuid">A UUID key of the region to return</param>
        /// <returns>A SimProfileData for the region</returns>
        public RegionProfileData GetRegion(UUID uuid)
        {
            foreach (IGridDataPlugin plugin in _plugins)
            {
                try
                {
                    return plugin.GetProfileByUUID(uuid);
                }
                catch (Exception e)
                {
                    m_log.Warn("[storage]: GetRegion - " + e.Message);
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a region by argument
        /// </summary>
        /// <param name="uuid">A regionHandle of the region to return</param>
        /// <returns>A SimProfileData for the region</returns>
        public RegionProfileData GetRegion(ulong handle)
        {
            foreach (IGridDataPlugin plugin in _plugins)
            {
                try
                {
                    return plugin.GetProfileByHandle(handle);
                }
                catch (Exception ex)
                {
                    m_log.Debug("[storage]: " + ex.Message);
                    m_log.Warn("[storage]: Unable to find region " + handle.ToString() + " via " + plugin.Name);
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a region by argument
        /// </summary>
        /// <param name="regionName">A partial regionName of the region to return</param>
        /// <returns>A SimProfileData for the region</returns>
        public RegionProfileData GetRegion(string regionName)
        {
            foreach (IGridDataPlugin plugin in _plugins)
            {
                try
                {
                    return plugin.GetProfileByString(regionName);
                }
                catch
                {
                    m_log.Warn("[storage]: Unable to find region " + regionName + " via " + plugin.Name);
                }
            }
            return null;
        }

        public List<RegionProfileData> GetRegions(uint xmin, uint ymin, uint xmax, uint ymax)
        {
            List<RegionProfileData> regions = new List<RegionProfileData>();

            foreach (IGridDataPlugin plugin in _plugins)
            {
                try
                {
                    regions.AddRange(plugin.GetProfilesInRange(xmin, ymin, xmax, ymax));
                }
                catch
                {
                    m_log.Warn("[storage]: Unable to query regionblock via " + plugin.Name);
                }
            }

            return regions;
        }

        public List<RegionProfileData> GetRegions(string name, int maxNum)
        {
            List<RegionProfileData> regions = new List<RegionProfileData>();
            foreach (IGridDataPlugin plugin in _plugins)
            {
                try
                {
                    int num = maxNum - regions.Count;
                    List<RegionProfileData> profiles = plugin.GetRegionsByName(name, (uint)num);
                    if (profiles != null) regions.AddRange(profiles);
                }
                catch
                {
                    m_log.Warn("[storage]: Unable to query regionblock via " + plugin.Name);
                }
            }

            return regions;
        }

        public DataResponse AddUpdateRegion(RegionProfileData sim, RegionProfileData existingSim)
        {
            DataResponse insertResponse = DataResponse.RESPONSE_ERROR;
            foreach (IGridDataPlugin plugin in _plugins)
            {
                try
                {
                    if (existingSim == null)
                    {
                        insertResponse = plugin.AddProfile(sim);
                    }
                    else
                    {
                        insertResponse = plugin.UpdateProfile(sim);
                    }
                }
                catch (Exception e)
                {
                    m_log.Warn("[LOGIN END]: " +
                                          "Unable to login region " + sim.ToString() + " via " + plugin.Name);
                    m_log.Warn("[LOGIN END]: " + e.ToString());
                }
            }
            return insertResponse;
        }

        public DataResponse DeleteRegion(string uuid)
        {
            DataResponse insertResponse = DataResponse.RESPONSE_ERROR;
            foreach (IGridDataPlugin plugin in _plugins)
            {
                //OpenSim.Data.MySQL.MySQLGridData dbengine = new OpenSim.Data.MySQL.MySQLGridData();
                try
                {
                    //Nice are we not using multiple databases?
                    //MySQLGridData mysqldata = (MySQLGridData)(plugin);

                    //DataResponse insertResponse = mysqldata.DeleteProfile(TheSim);
                    insertResponse = plugin.DeleteProfile(uuid);
                }
                catch (Exception)
                {
                    m_log.Error("storage Unable to delete region " + uuid + " via " + plugin.Name);
                    //MainLog.Instance.Warn("storage", e.ToString());
                    insertResponse = DataResponse.RESPONSE_ERROR;
                }
            }
            return insertResponse;
        }

        public string CheckReservations(RegionProfileData theSim, XmlNode authkeynode)
        {
            foreach (IGridDataPlugin plugin in _plugins)
            {
                try
                {
                    //Check reservations
                    ReservationData reserveData =
                        plugin.GetReservationAtPoint(theSim.regionLocX, theSim.regionLocY);
                    if ((reserveData != null && reserveData.gridRecvKey == theSim.regionRecvKey) ||
                        (reserveData == null && authkeynode.InnerText != theSim.regionRecvKey))
                    {
                        plugin.AddProfile(theSim);
                        m_log.Info("[grid]: New sim added to grid (" + theSim.regionName + ")");
                        logToDB(theSim.ToString(), "RestSetSimMethod", String.Empty, 5,
                                "Region successfully updated and connected to grid.");
                    }
                    else
                    {
                        m_log.Warn("[grid]: " +
                                   "Unable to update region (RestSetSimMethod): Incorrect reservation auth key.");
                        // Wanted: " + reserveData.gridRecvKey + ", Got: " + theSim.regionRecvKey + ".");
                        return "Unable to update region (RestSetSimMethod): Incorrect auth key.";
                    }
                }
                catch (Exception e)
                {
                    m_log.Warn("[GRID]: GetRegionPlugin Handle " + plugin.Name + " unable to add new sim: " +
                                                  e.ToString());
                }
            }
            return "OK";
        }
    }
}
