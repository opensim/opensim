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
using System.Linq;
using System.Reflection;
using System.Text;
using log4net;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework.Monitoring
{
    /// <summary>
    /// Holds individual statistic details
    /// </summary>
    public class Stat : IDisposable
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static readonly char[] DisallowedShortNameCharacters = { '.' };

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

        public StatType StatType { get; private set; }

        public MeasuresOfInterest MeasuresOfInterest { get; private set; }

        /// <summary>
        /// Action used to update this stat when the value is requested if it's a pull type.
        /// </summary>
        public Action<Stat> PullAction { get; private set; }

        public StatVerbosity Verbosity { get; private set; }
        public string ShortName { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public virtual string UnitName { get; private set; }

        public virtual double Value
        {
            get
            {
                // Asking for an update here means that the updater cannot access this value without infinite recursion.
                // XXX: A slightly messy but simple solution may be to flick a flag so we can tell if this is being
                // called by the pull action and just return the value.
                if (StatType == StatType.Pull)
                    PullAction(this);

                return m_value;
            }

            set
            {
                m_value = value;
            }
        }

        private double m_value;

        /// <summary>
        /// Historical samples for calculating measures of interest average.
        /// </summary>
        /// <remarks>
        /// Will be null if no measures of interest require samples.
        /// </remarks>
        private Queue<double> m_samples;

        /// <summary>
        /// Maximum number of statistical samples.
        /// </summary>
        /// <remarks>
        /// At the moment this corresponds to 1 minute since the sampling rate is every 2.5 seconds as triggered from
        /// the main Watchdog.
        /// </remarks>
        private static int m_maxSamples = 24;

        public Stat(
            string shortName,
            string name,
            string description,
            string unitName,
            string category,
            string container,
            StatType type,
            Action<Stat> pullAction,
            StatVerbosity verbosity) 
                : this(
                    shortName, 
                    name, 
                    description, 
                    unitName, 
                    category, 
                    container, 
                    type, 
                    MeasuresOfInterest.None, 
                    pullAction, 
                    verbosity)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name='shortName'>Short name for the stat.  Must not contain spaces.  e.g. "LongFrames"</param>
        /// <param name='name'>Human readable name for the stat.  e.g. "Long frames"</param>
        /// <param name='description'>Description of stat</param>
        /// <param name='unitName'>
        /// Unit name for the stat.  Should be preceeded by a space if the unit name isn't normally appeneded immediately to the value.
        /// e.g. " frames"
        /// </param>
        /// <param name='category'>Category under which this stat should appear, e.g. "scene".  Do not capitalize.</param>
        /// <param name='container'>Entity to which this stat relates.  e.g. scene name if this is a per scene stat.</param>
        /// <param name='type'>Push or pull</param>
        /// <param name='pullAction'>Pull stats need an action to update the stat on request.  Push stats should set null here.</param>
        /// <param name='moi'>Measures of interest</param>
        /// <param name='verbosity'>Verbosity of stat.  Controls whether it will appear in short stat display or only full display.</param>
        public Stat(
            string shortName,
            string name,
            string description,
            string unitName,
            string category,
            string container,
            StatType type,
            MeasuresOfInterest moi,
            Action<Stat> pullAction,
            StatVerbosity verbosity)
        {
            if (StatsManager.SubCommands.Contains(category))
                throw new Exception(
                    string.Format("Stat cannot be in category '{0}' since this is reserved for a subcommand", category));

            foreach (char c in DisallowedShortNameCharacters)
            {
                if (shortName.IndexOf(c) != -1)
                    shortName = shortName.Replace(c, '#');
//                    throw new Exception(string.Format("Stat name {0} cannot contain character {1}", shortName, c));
            }

            ShortName = shortName;
            Name = name;
            Description = description;
            UnitName = unitName;
            Category = category;
            Container = container;
            StatType = type;

            if (StatType == StatType.Push && pullAction != null)
                throw new Exception("A push stat cannot have a pull action");
            else
                PullAction = pullAction;

            MeasuresOfInterest = moi;

            if ((moi & MeasuresOfInterest.AverageChangeOverTime) == MeasuresOfInterest.AverageChangeOverTime)
                m_samples = new Queue<double>(m_maxSamples);

            Verbosity = verbosity;
        }

        // IDisposable.Dispose()
        public virtual void Dispose()
        {
            return;
        }

        /// <summary>
        /// Record a value in the sample set.
        /// </summary>
        /// <remarks>
        /// Do not call this if MeasuresOfInterest.None
        /// </remarks>
        public void RecordValue()
        {
            double newValue = Value;

            lock (m_samples)
            {
                if (m_samples.Count >= m_maxSamples)
                    m_samples.Dequeue();

//                m_log.DebugFormat("[STAT]: Recording value {0} for {1}", newValue, Name);

                m_samples.Enqueue(newValue);
            }
        }

        public virtual string ToConsoleString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(
                "{0}.{1}.{2} : {3}{4}", 
                Category, 
                Container, 
                ShortName, 
                Value, 
                string.IsNullOrEmpty(UnitName) ? "" : string.Format(" {0}", UnitName));

            AppendMeasuresOfInterest(sb);

            return sb.ToString();
        }

        public virtual OSDMap ToOSDMap()
        {
            OSDMap ret = new OSDMap();
            ret.Add("StatType", "Stat");    // used by overloading classes to denote type of stat

            ret.Add("Category", OSD.FromString(Category));
            ret.Add("Container", OSD.FromString(Container));
            ret.Add("ShortName", OSD.FromString(ShortName));
            ret.Add("Name", OSD.FromString(Name));
            ret.Add("Description", OSD.FromString(Description));
            ret.Add("UnitName", OSD.FromString(UnitName));
            ret.Add("Value", OSD.FromReal(Value));

            double lastChangeOverTime, averageChangeOverTime;
            if (ComputeMeasuresOfInterest(out lastChangeOverTime, out averageChangeOverTime))
            {
                ret.Add("LastChangeOverTime", OSD.FromReal(lastChangeOverTime));
                ret.Add("AverageChangeOverTime", OSD.FromReal(averageChangeOverTime));
            }

            return ret;
        }

        // Compute the averages over time and return same.
        // Return 'true' if averages were actually computed. 'false' if no average info.
        public bool ComputeMeasuresOfInterest(out double lastChangeOverTime, out double averageChangeOverTime)
        {
            bool ret = false;
            lastChangeOverTime = 0;
            averageChangeOverTime = 0;

            if ((MeasuresOfInterest & MeasuresOfInterest.AverageChangeOverTime) == MeasuresOfInterest.AverageChangeOverTime)
            {
                double totalChange = 0;
                double? penultimateSample = null;
                double? lastSample = null;

                lock (m_samples)
                {
                    //                    m_log.DebugFormat(
                    //                        "[STAT]: Samples for {0} are {1}", 
                    //                        Name, string.Join(",", m_samples.Select(s => s.ToString()).ToArray()));

                    foreach (double s in m_samples)
                    {
                        if (lastSample != null)
                            totalChange += s - (double)lastSample;

                        penultimateSample = lastSample;
                        lastSample = s;
                    }
                }

                if (lastSample != null && penultimateSample != null)
                {
                    lastChangeOverTime
                        = ((double)lastSample - (double)penultimateSample) / (Watchdog.WATCHDOG_INTERVAL_MS / 1000);
                }

                int divisor = m_samples.Count <= 1 ? 1 : m_samples.Count - 1;

                averageChangeOverTime = totalChange / divisor / (Watchdog.WATCHDOG_INTERVAL_MS / 1000);
                ret = true;
            }

            return ret;
        }

        protected void AppendMeasuresOfInterest(StringBuilder sb)
        {
            double lastChangeOverTime = 0;
            double averageChangeOverTime = 0;

            if (ComputeMeasuresOfInterest(out lastChangeOverTime, out averageChangeOverTime))
            {
                sb.AppendFormat(
                    ", {0:0.##}{1}/s, {2:0.##}{3}/s", 
                    lastChangeOverTime, 
                    string.IsNullOrEmpty(UnitName) ? "" : string.Format(" {0}", UnitName), 
                    averageChangeOverTime,
                    string.IsNullOrEmpty(UnitName) ? "" : string.Format(" {0}", UnitName));
            }
        }
    }
}