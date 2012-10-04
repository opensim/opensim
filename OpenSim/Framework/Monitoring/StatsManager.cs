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
        /// <summary>
        /// Registered stats.
        /// </summary>
        /// <remarks>
        /// Do not add or remove from this dictionary.
        /// </remarks>
        public static Dictionary<string, Stat> RegisteredStats = new Dictionary<string, Stat>();

        private static AssetStatsCollector assetStats;
        private static UserStatsCollector userStats;
        private static SimExtraStatsCollector simExtraStats = new SimExtraStatsCollector();

        public static AssetStatsCollector AssetStats { get { return assetStats; } }
        public static UserStatsCollector UserStats { get { return userStats; } }
        public static SimExtraStatsCollector SimExtraStats { get { return simExtraStats; } }

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

        public static bool RegisterStat(Stat stat)
        {
            lock (RegisteredStats)
            {
                if (RegisteredStats.ContainsKey(stat.UniqueName))
                {
                    // XXX: For now just return false.  This is to avoid problems in regression tests where all tests
                    // in a class are run in the same instance of the VM.
                    return false;

//                    throw new Exception(
//                        "StatsManager already contains stat with ShortName {0} in Category {1}", stat.ShortName, stat.Category);
                }

                // We take a replace-on-write approach here so that we don't need to generate a new Dictionary
                Dictionary<string, Stat> newRegisteredStats = new Dictionary<string, Stat>(RegisteredStats);
                newRegisteredStats[stat.UniqueName] = stat;
                RegisteredStats = newRegisteredStats;
            }

            return true;
        }

        public static bool DeregisterStat(Stat stat)
        {
            lock (RegisteredStats)
            {
                if (!RegisteredStats.ContainsKey(stat.UniqueName))
                    return false;

                Dictionary<string, Stat> newRegisteredStats = new Dictionary<string, Stat>(RegisteredStats);
                newRegisteredStats.Remove(stat.UniqueName);
                RegisteredStats = newRegisteredStats;

                return true;
            }
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

        public Stat(
            string shortName, string name, string unitName, string category, string container, StatVerbosity verbosity, string description)
        {
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

                return (double)Antecedent / c;
            }

            set
            {
                throw new Exception("Cannot set value on a PercentageStat");
            }
        }

        public PercentageStat(
            string shortName, string name, string category, string container, StatVerbosity verbosity, string description)
            : base(shortName, name, " %", category, container, verbosity, description)
        {
        }
    }
}