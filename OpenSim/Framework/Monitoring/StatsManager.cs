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
        public static Dictionary<string, Dictionary<string, Dictionary<string, Stat>>> RegisteredStats
            = new Dictionary<string, Dictionary<string, Dictionary<string, Stat>>>();

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
                    Dictionary<string, Dictionary<string, Stat>> category;
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
            ICommandConsole con, Dictionary<string, Dictionary<string, Stat>> category)
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
            Dictionary<string, Dictionary<string, Stat>> category = null, newCategory;
            Dictionary<string, Stat> container = null, newContainer;

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
                    newContainer = new Dictionary<string, Stat>(container);
                else
                    newContainer = new Dictionary<string, Stat>();

                if (category != null)
                    newCategory = new Dictionary<string, Dictionary<string, Stat>>(category);
                else
                    newCategory = new Dictionary<string, Dictionary<string, Stat>>();

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
        /// <returns></returns
        public static bool DeregisterStat(Stat stat)
        {
            Dictionary<string, Dictionary<string, Stat>> category = null, newCategory;
            Dictionary<string, Stat> container = null, newContainer;

            lock (RegisteredStats)
            {
                if (!TryGetStat(stat, out category, out container))
                    return false;

                newContainer = new Dictionary<string, Stat>(container);
                newContainer.Remove(stat.UniqueName);

                newCategory = new Dictionary<string, Dictionary<string, Stat>>(category);
                newCategory.Remove(stat.Container);

                newCategory[stat.Container] = newContainer;
                RegisteredStats[stat.Category] = newCategory;

                return true;
            }
        }

        public static bool TryGetStats(string category, out Dictionary<string, Dictionary<string, Stat>> stats)
        {
            return RegisteredStats.TryGetValue(category, out stats);
        }

        public static bool TryGetStat(
            Stat stat,
            out Dictionary<string, Dictionary<string, Stat>> category,
            out Dictionary<string, Stat> container)
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

    /// <summary>
    /// Holds individual static details
    /// </summary>
    public class Stat
    {
        /// <summary>
        /// Unique stat name used for indexing.  Each ShortName in a Category must be unique.
        /// </summary>
        public string UniqueName { get; private set; }

        /// <summary>
        /// Category of this stat (e.g. cache, scene, etc).
        /// </summary>
        public string Category { get; private set; }

        /// <summary>
        /// Containing name for this stat.
        /// FIXME: In the case of a scene, this is currently the scene name (though this leaves
        /// us with a to-be-resolved problem of non-unique region names).
        /// </summary>
        /// <value>
        /// The container.
        /// </value>
        public string Container { get; private set; }

        public StatVerbosity Verbosity { get; private set; }
        public string ShortName { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public virtual string UnitName { get; private set; }

        public virtual double Value { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name='shortName'>Short name for the stat.  Must not contain spaces.  e.g. "LongFrames"</param>
        /// <param name='name'>Human readable name for the stat.  e.g. "Long frames"</param>
        /// <param name='unitName'>
        /// Unit name for the stat.  Should be preceeded by a space if the unit name isn't normally appeneded immediately to the value.
        /// e.g. " frames"
        /// </param>
        /// <param name='category'>Category under which this stat should appear, e.g. "scene".  Do not capitalize.</param>
        /// <param name='container'>Entity to which this stat relates.  e.g. scene name if this is a per scene stat.</param>
        /// <param name='verbosity'>Verbosity of stat.  Controls whether it will appear in short stat display or only full display.</param>
        /// <param name='description'>Description of stat</param>
        public Stat(
            string shortName, string name, string unitName, string category, string container, StatVerbosity verbosity, string description)
        {
            if (StatsManager.SubCommands.Contains(category))
                throw new Exception(
                    string.Format("Stat cannot be in category '{0}' since this is reserved for a subcommand", category));

            ShortName = shortName;
            Name = name;
            UnitName = unitName;
            Category = category;
            Container = container;
            Verbosity = verbosity;
            Description = description;

            UniqueName = GenUniqueName(Container, Category, ShortName);
        }

        public static string GenUniqueName(string container, string category, string shortName)
        {
            return string.Format("{0}+{1}+{2}", container, category, shortName);
        }

        public virtual string ToConsoleString()
        {
            return string.Format(
                "{0}.{1}.{2} : {3}{4}", Category, Container, ShortName, Value, UnitName);
        }
    }

    public class PercentageStat : Stat
    {
        public int Antecedent { get; set; }
        public int Consequent { get; set; }

        public override double Value
        {
            get
            {
                int c = Consequent;

                // Avoid any chance of a multi-threaded divide-by-zero
                if (c == 0)
                    return 0;

                return (double)Antecedent / c * 100;
            }

            set
            {
                throw new Exception("Cannot set value on a PercentageStat");
            }
        }

        public PercentageStat(
            string shortName, string name, string category, string container, StatVerbosity verbosity, string description)
            : base(shortName, name, "%", category, container, verbosity, description) {}

        public override string ToConsoleString()
        {
            return string.Format(
                "{0}.{1}.{2} : {3:0.##}{4} ({5}/{6})",
                Category, Container, ShortName, Value, UnitName, Antecedent, Consequent);
        }
    }
}