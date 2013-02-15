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
using System.Text;

namespace OpenSim.Framework.Monitoring
{
    /// <summary>
    /// Singleton used to provide access to statistics reporters
    /// </summary>
    public class StatsManager
    {
        // Subcommand used to list other stats.
        public const string AllSubCommand = "all";

        // Subcommand used to list other stats.
        public const string ListSubCommand = "list";

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

        private static AssetStatsCollector assetStats;
        private static UserStatsCollector userStats;
        private static SimExtraStatsCollector simExtraStats = new SimExtraStatsCollector();

        public static AssetStatsCollector AssetStats { get { return assetStats; } }
        public static UserStatsCollector UserStats { get { return userStats; } }
        public static SimExtraStatsCollector SimExtraStats { get { return simExtraStats; } }

        public static void RegisterConsoleCommands(ICommandConsole console)
        {
            console.Commands.AddCommand(
                "General",
                false,
                "show stats",
                "show stats [list|all|<category>]",
                "Show statistical information for this server",
                "If no final argument is specified then legacy statistics information is currently shown.\n"
                    + "If list is specified then statistic categories are shown.\n"
                    + "If all is specified then all registered statistics are shown.\n"
                    + "If a category name is specified then only statistics from that category are shown.\n"
                    + "THIS STATS FACILITY IS EXPERIMENTAL AND DOES NOT YET CONTAIN ALL STATS",
                HandleShowStatsCommand);
        }

        public static void HandleShowStatsCommand(string module, string[] cmd)
        {
            ICommandConsole con = MainConsole.Instance;

            if (cmd.Length > 2)
            {
                var categoryName = cmd[2];

                if (categoryName == AllSubCommand)
                {
                    foreach (var category in RegisteredStats.Values)
                    {
                        OutputCategoryStatsToConsole(con, category);
                    }
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
                        OutputCategoryStatsToConsole(con, category);
                    }
                }
            }
            else
            {
                // Legacy
                con.Output(SimExtraStats.Report());
            }
        }

        private static void OutputCategoryStatsToConsole(
            ICommandConsole con, SortedDictionary<string, SortedDictionary<string, Stat>> category)
        {
            foreach (var container in category.Values)
            {
                foreach (Stat stat in container.Values)
                {
                    con.Output(stat.ToConsoleString());
                }
            }
        }

        /// <summary>
        /// Start collecting statistics related to assets.
        /// Should only be called once.
        /// </summary>
        public static AssetStatsCollector StartCollectingAssetStats()
        {
            assetStats = new AssetStatsCollector();

            return assetStats;
        }

        /// <summary>
        /// Start collecting statistics related to users.
        /// Should only be called once.
        /// </summary>
        public static UserStatsCollector StartCollectingUserStats()
        {
            userStats = new UserStatsCollector();

            return userStats;
        }

        /// <summary>
        /// Registers a statistic.
        /// </summary>
        /// <param name='stat'></param>
        /// <returns></returns>
        public static bool RegisterStat(Stat stat)
        {
            SortedDictionary<string, SortedDictionary<string, Stat>> category = null, newCategory;
            SortedDictionary<string, Stat> container = null, newContainer;

            lock (RegisteredStats)
            {
                // Stat name is not unique across category/container/shortname key.
                // XXX: For now just return false.  This is to avoid problems in regression tests where all tests
                // in a class are run in the same instance of the VM.
                if (TryGetStat(stat, out category, out container))
                    return false;

                // We take a copy-on-write approach here of replacing dictionaries when keys are added or removed.
                // This means that we don't need to lock or copy them on iteration, which will be a much more
                // common operation after startup.
                if (container != null)
                    newContainer = new SortedDictionary<string, Stat>(container);
                else
                    newContainer = new SortedDictionary<string, Stat>();

                if (category != null)
                    newCategory = new SortedDictionary<string, SortedDictionary<string, Stat>>(category);
                else
                    newCategory = new SortedDictionary<string, SortedDictionary<string, Stat>>();

                newContainer[stat.ShortName] = stat;
                newCategory[stat.Container] = newContainer;
                RegisteredStats[stat.Category] = newCategory;
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
            SortedDictionary<string, SortedDictionary<string, Stat>> category = null, newCategory;
            SortedDictionary<string, Stat> container = null, newContainer;

            lock (RegisteredStats)
            {
                if (!TryGetStat(stat, out category, out container))
                    return false;

                newContainer = new SortedDictionary<string, Stat>(container);
                newContainer.Remove(stat.ShortName);

                newCategory = new SortedDictionary<string, SortedDictionary<string, Stat>>(category);
                newCategory.Remove(stat.Container);

                newCategory[stat.Container] = newContainer;
                RegisteredStats[stat.Category] = newCategory;

                return true;
            }
        }

        public static bool TryGetStats(string category, out SortedDictionary<string, SortedDictionary<string, Stat>> stats)
        {
            return RegisteredStats.TryGetValue(category, out stats);
        }

        public static bool TryGetStat(
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