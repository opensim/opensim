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
using System.Linq;
using System.Text;

using OpenSim.Framework;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework.Monitoring
{
    /// <summary>
    /// Static class used to register/deregister/fetch statistics
    /// </summary>
    public static class StatsManager
    {
        // Subcommand used to list other stats.
        public const string AllSubCommand = "all";

        // Subcommand used to list other stats.
        public const string ListSubCommand = "list";

        public static string StatsPassword { get; set; }

        // All subcommands
        public static HashSet<string> SubCommands = new HashSet<string> { AllSubCommand, ListSubCommand };

        /// <summary>
        /// Registered stats categorized by category/container/shortname
        /// </summary>
        /// <remarks>
        /// Do not add or remove directly from this dictionary.
        /// </remarks>
        public static SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, Stat>>> RegisteredStats
            = new SortedDictionary<string, SortedDictionary<string, SortedDictionary<string, Stat>>>();

//        private static AssetStatsCollector assetStats;
//        private static UserStatsCollector userStats;
//        private static SimExtraStatsCollector simExtraStats = new SimExtraStatsCollector();

//        public static AssetStatsCollector AssetStats { get { return assetStats; } }
//        public static UserStatsCollector UserStats { get { return userStats; } }
        public static SimExtraStatsCollector SimExtraStats { get; set; }

        public static void RegisterConsoleCommands(ICommandConsole console)
        {
            console.Commands.AddCommand(
                "General",
                false,
                "stats show",
                "stats show [list|all|(<category>[.<container>])+",
                "Show statistical information for this server",
                "If no final argument is specified then legacy statistics information is currently shown.\n"
                    + "'list' argument will show statistic categories.\n"
                    + "'all' will show all statistics.\n"
                    + "A <category> name will show statistics from that category.\n"
                    + "A <category>.<container> name will show statistics from that category in that container.\n"
                    + "More than one name can be given separated by spaces.\n",
                HandleShowStatsCommand);

            console.Commands.AddCommand(
                "General",
                false,
                "show stats",
                "show stats [list|all|(<category>[.<container>])+",
                "Alias for 'stats show' command",
                HandleShowStatsCommand);
            StatsLogger.RegisterConsoleCommands(console);
        }

        public static void HandleShowStatsCommand(string module, string[] cmd)
        {
            ICommandConsole con = MainConsole.Instance;

            if (cmd.Length > 2)
            {
                foreach (string name in cmd.Skip(2))
                {
                    string[] components = name.Split('.');

                    string categoryName = components[0];
                    string containerName = components.Length > 1 ? components[1] : null;
                    string statName = components.Length > 2 ? components[2] : null;

                    if (categoryName == AllSubCommand)
                    {
                        OutputAllStatsToConsole(con);
                    }
                    else if (categoryName == ListSubCommand)
                    {
                        con.Output("Statistic categories available are:");
                        foreach (string category in RegisteredStats.Keys)
                            con.OutputFormat("  {0}", category);
                    }
                    else
                    {
                        SortedDictionary<string, SortedDictionary<string, Stat>> category;
                        if (!RegisteredStats.TryGetValue(categoryName, out category))
                        {
                            con.OutputFormat("No such category as {0}", categoryName);
                        }
                        else
                        {
                            if (String.IsNullOrEmpty(containerName))
                            {
                                OutputCategoryStatsToConsole(con, category);
                            }
                            else
                            {
                                SortedDictionary<string, Stat> container;
                                if (category.TryGetValue(containerName, out container))
                                {
                                    if (String.IsNullOrEmpty(statName))
                                    {
                                        OutputContainerStatsToConsole(con, container);
                                    }
                                    else
                                    {
                                        Stat stat;
                                        if (container.TryGetValue(statName, out stat))
                                        {
                                            OutputStatToConsole(con, stat);
                                        }
                                        else
                                        {
                                            con.OutputFormat(
                                                "No such stat {0} in {1}.{2}", statName, categoryName, containerName);
                                        }
                                    }
                                }
                                else
                                {
                                    con.OutputFormat("No such container {0} in category {1}", containerName, categoryName);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // Legacy
                if (SimExtraStats != null)
                    con.Output(SimExtraStats.Report());
                else
                    OutputAllStatsToConsole(con);
            }
        }

        public static List<string> GetAllStatsReports()
        {
            List<string> reports = new List<string>();

            foreach (var category in RegisteredStats.Values)
                reports.AddRange(GetCategoryStatsReports(category));

            return reports;
        }

        private static void OutputAllStatsToConsole(ICommandConsole con)
        {
            foreach (string report in GetAllStatsReports())
                con.Output(report);
        }

        private static List<string> GetCategoryStatsReports(
            SortedDictionary<string, SortedDictionary<string, Stat>> category)
        {
            List<string> reports = new List<string>();

            foreach (var container in category.Values)
                reports.AddRange(GetContainerStatsReports(container));

            return reports;
        }

        private static void OutputCategoryStatsToConsole(
            ICommandConsole con, SortedDictionary<string, SortedDictionary<string, Stat>> category)
        {
            foreach (string report in GetCategoryStatsReports(category))
                con.Output(report);
        }

        private static List<string> GetContainerStatsReports(SortedDictionary<string, Stat> container)
        {
            List<string> reports = new List<string>();

            foreach (Stat stat in container.Values)
                reports.Add(stat.ToConsoleString());

            return reports;
        }

        private static void OutputContainerStatsToConsole(
            ICommandConsole con, SortedDictionary<string, Stat> container)
        {
            foreach (string report in GetContainerStatsReports(container))
                con.Output(report);
        }

        private static void OutputStatToConsole(ICommandConsole con, Stat stat)
        {
            con.Output(stat.ToConsoleString());
        }

        // Creates an OSDMap of the format:
        // { categoryName: {
        //         containerName: {
        //               statName: {
        //                     "Name": name,
        //                     "ShortName": shortName,
        //                     ...
        //               },
        //               statName: {
        //                     "Name": name,
        //                     "ShortName": shortName,
        //                     ...
        //               },
        //               ...
        //         },
        //         containerName: {
        //         ...
        //         },
        //         ...
        //   },
        //   categoryName: {
        //   ...
        //   },
        //   ...
        // }
        // The passed in parameters will filter the categories, containers and stats returned. If any of the
        //    parameters are either EmptyOrNull or the AllSubCommand value, all of that type will be returned.
        // Case matters.
        public static OSDMap GetStatsAsOSDMap(string pCategoryName, string pContainerName, string pStatName)
        {
            OSDMap map = new OSDMap();

            lock (RegisteredStats)
            {
                foreach (string catName in RegisteredStats.Keys)
                {
                    // Do this category if null spec, "all" subcommand or category name matches passed parameter.
                    // Skip category if none of the above.
                    if (!(String.IsNullOrEmpty(pCategoryName) || pCategoryName == AllSubCommand || pCategoryName == catName))
                        continue;

                    OSDMap contMap = new OSDMap();
                    foreach (string contName in RegisteredStats[catName].Keys)
                    {
                        if (!(string.IsNullOrEmpty(pContainerName) || pContainerName == AllSubCommand || pContainerName == contName))
                            continue;

                        OSDMap statMap = new OSDMap();

                        SortedDictionary<string, Stat> theStats = RegisteredStats[catName][contName];
                        foreach (string statName in theStats.Keys)
                        {
                            if (!(String.IsNullOrEmpty(pStatName) || pStatName == AllSubCommand || pStatName == statName))
                                continue;

                            statMap.Add(statName, theStats[statName].ToBriefOSDMap());
                        }

                        contMap.Add(contName, statMap);
                    }
                    map.Add(catName, contMap);
                }
            }

            return map;
        }

        public static Hashtable HandleStatsRequest(Hashtable request)
        {
            Hashtable responsedata = new Hashtable();
//            string regpath = request["uri"].ToString();
            int response_code = 200;
            string contenttype = "text/json";

            if (StatsPassword != String.Empty && (!request.ContainsKey("pass") || request["pass"].ToString() != StatsPassword))
            {
                responsedata["int_response_code"] = response_code;
                responsedata["content_type"] = "text/plain";
                responsedata["keepalive"] = false;
                responsedata["str_response_string"] = "Access denied";
                responsedata["access_control_allow_origin"] = "*";

                return responsedata;
            }

            string pCategoryName = StatsManager.AllSubCommand;
            string pContainerName = StatsManager.AllSubCommand;
            string pStatName = StatsManager.AllSubCommand;

            if (request.ContainsKey("cat")) pCategoryName = request["cat"].ToString();
            if (request.ContainsKey("cont")) pContainerName = request["cat"].ToString();
            if (request.ContainsKey("stat")) pStatName = request["stat"].ToString();

            string strOut = StatsManager.GetStatsAsOSDMap(pCategoryName, pContainerName, pStatName).ToString();

            // If requestor wants it as a callback function, build response as a function rather than just the JSON string.
            if (request.ContainsKey("callback"))
            {
                strOut = request["callback"].ToString() + "(" + strOut + ");";
            }

            // m_log.DebugFormat("{0} StatFetch: uri={1}, cat={2}, cont={3}, stat={4}, resp={5}",
            //                         LogHeader, regpath, pCategoryName, pContainerName, pStatName, strOut);

            responsedata["int_response_code"] = response_code;
            responsedata["content_type"] = contenttype;
            responsedata["keepalive"] = false;
            responsedata["str_response_string"] = strOut;
            responsedata["access_control_allow_origin"] = "*";

            return responsedata;
        }

//        /// <summary>
//        /// Start collecting statistics related to assets.
//        /// Should only be called once.
//        /// </summary>
//        public static AssetStatsCollector StartCollectingAssetStats()
//        {
//            assetStats = new AssetStatsCollector();
//
//            return assetStats;
//        }
//
//        /// <summary>
//        /// Start collecting statistics related to users.
//        /// Should only be called once.
//        /// </summary>
//        public static UserStatsCollector StartCollectingUserStats()
//        {
//            userStats = new UserStatsCollector();
//
//            return userStats;
//        }

        /// <summary>
        /// Register a statistic.
        /// </summary>
        /// <param name='stat'></param>
        /// <returns></returns>
        public static bool RegisterStat(Stat stat)
        {
            SortedDictionary<string, SortedDictionary<string, Stat>> category = null;
            SortedDictionary<string, Stat> container = null;

            lock (RegisteredStats)
            {
                // Stat name is not unique across category/container/shortname key.
                // XXX: For now just return false.  This is to avoid problems in regression tests where all tests
                // in a class are run in the same instance of the VM.
                if (TryGetStatParents(stat, out category, out container))
                    return false;

                if (container == null)
                    container = new SortedDictionary<string, Stat>();

                if (category == null)
                    category = new SortedDictionary<string, SortedDictionary<string, Stat>>();

                container[stat.ShortName] = stat;
                category[stat.Container] = container;
                RegisteredStats[stat.Category] = category;
            }

            return true;
        }

        /// <summary>
        /// Deregister a statistic
        /// </summary>>
        /// <param name='stat'></param>
        /// <returns></returns>
        public static bool DeregisterStat(Stat stat)
        {
            SortedDictionary<string, SortedDictionary<string, Stat>> category = null;
            SortedDictionary<string, Stat> container = null;

            lock (RegisteredStats)
            {
                if (!TryGetStatParents(stat, out category, out container))
                    return false;

                if(container != null)
                {
                    container.Remove(stat.ShortName);
                    if(category != null && container.Count == 0)
                    {
                        category.Remove(stat.Container);
                        if(category.Count == 0)
                            RegisteredStats.Remove(stat.Category);
                    }
                }
                return true;
            }
        }

        public static bool TryGetStat(string category, string container, string statShortName, out Stat stat)
        {
            stat = null;
            SortedDictionary<string, SortedDictionary<string, Stat>> categoryStats;

            lock (RegisteredStats)
            {
                if (!TryGetStatsForCategory(category, out categoryStats))
                    return false;

                SortedDictionary<string, Stat> containerStats;

                if (!categoryStats.TryGetValue(container, out containerStats))
                    return false;

                return containerStats.TryGetValue(statShortName, out stat);
            }
        }

        public static bool TryGetStatsForCategory(
            string category, out SortedDictionary<string, SortedDictionary<string, Stat>> stats)
        {
            lock (RegisteredStats)
                return RegisteredStats.TryGetValue(category, out stats);
        }

        /// <summary>
        /// Get the same stat for each container in a given category.
        /// </summary>
        /// <returns>
        /// The stats if there were any to fetch.  Otherwise null.
        /// </returns>
        /// <param name='category'></param>
        /// <param name='statShortName'></param>
        public static List<Stat> GetStatsFromEachContainer(string category, string statShortName)
        {
            SortedDictionary<string, SortedDictionary<string, Stat>> categoryStats;

            lock (RegisteredStats)
            {
                if (!RegisteredStats.TryGetValue(category, out categoryStats))
                    return null;

                List<Stat> stats = null;

                foreach (SortedDictionary<string, Stat> containerStats in categoryStats.Values)
                {
                    if (containerStats.ContainsKey(statShortName))
                    {
                        if (stats == null)
                            stats = new List<Stat>();

                        stats.Add(containerStats[statShortName]);
                    }
                }

                return stats;
            }
        }

        public static bool TryGetStatParents(
            Stat stat,
            out SortedDictionary<string, SortedDictionary<string, Stat>> category,
            out SortedDictionary<string, Stat> container)
        {
            category = null;
            container = null;

            lock (RegisteredStats)
            {
                if (RegisteredStats.TryGetValue(stat.Category, out category))
                {
                    if (category.TryGetValue(stat.Container, out container))
                    {
                        if (container.ContainsKey(stat.ShortName))
                            return true;
                    }
                }
            }

            return false;
        }

        public static void RecordStats()
        {
            lock (RegisteredStats)
            {
                foreach (SortedDictionary<string, SortedDictionary<string, Stat>> category in RegisteredStats.Values)
                {
                    foreach (SortedDictionary<string, Stat> container in category.Values)
                    {
                        foreach (Stat stat in container.Values)
                        {
                            if (stat.MeasuresOfInterest != MeasuresOfInterest.None)
                                stat.RecordValue();
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Stat type.
    /// </summary>
    /// <remarks>
    /// A push stat is one which is continually updated and so it's value can simply by read.
    /// A pull stat is one where reading the value triggers a collection method - the stat is not continually updated.
    /// </remarks>
    public enum StatType
    {
        Push,
        Pull
    }

    /// <summary>
    /// Measures of interest for this stat.
    /// </summary>
    [Flags]
    public enum MeasuresOfInterest
    {
        None,
        AverageChangeOverTime
    }

    /// <summary>
    /// Verbosity of stat.
    /// </summary>
    /// <remarks>
    /// Info will always be displayed.
    /// </remarks>
    public enum StatVerbosity
    {
        Debug,
        Info
    }
}
