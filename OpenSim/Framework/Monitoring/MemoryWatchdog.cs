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
using System.Threading;
using log4net;

namespace OpenSim.Framework.Monitoring
{
    /// <summary>
    /// Experimental watchdog for memory usage.
    /// </summary>
    public static class MemoryWatchdog
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Is this watchdog active?
        /// </summary>
        public static bool Enabled
        {
            get { return m_enabled; }
            set
            {
//                m_log.DebugFormat("[MEMORY WATCHDOG]: Setting MemoryWatchdog.Enabled to {0}", value);

                if (value && !m_enabled)
                    UpdateLastRecord(GC.GetTotalMemory(false), Util.EnvironmentTickCount());

                m_enabled = value;
            }
        }
        private static bool m_enabled;

        /// <summary>
        /// Last memory churn in bytes per millisecond.
        /// </summary>
        public static double AverageMemoryChurn
        {
            get { if (m_samples.Count > 0) return m_samples.Average(); else return 0; }
        }

        /// <summary>
        /// Average memory churn in bytes per millisecond.
        /// </summary>
        public static double LastMemoryChurn
        {
            get { if (m_samples.Count > 0) return m_samples.Last(); else return 0; }
        }

        /// <summary>
        /// Maximum number of statistical samples.
        /// </summary>
        /// <remarks>
        /// At the moment this corresponds to 1 minute since the sampling rate is every 2.5 seconds as triggered from
        /// the main Watchdog.
        /// </remarks>
        private static int m_maxSamples = 24;

        /// <summary>
        /// Time when the watchdog was last updated.
        /// </summary>
        private static int m_lastUpdateTick;

        /// <summary>
        /// Memory used at time of last watchdog update.
        /// </summary>
        private static long m_lastUpdateMemory;

        /// <summary>
        /// Memory churn rate per millisecond.
        /// </summary>
//        private static double m_churnRatePerMillisecond;

        /// <summary>
        /// Historical samples for calculating moving average.
        /// </summary>
        private static Queue<double> m_samples = new Queue<double>(m_maxSamples);

        public static void Update()
        {
            int now = Util.EnvironmentTickCount();
            long memoryNow = GC.GetTotalMemory(false);
            long memoryDiff = memoryNow - m_lastUpdateMemory;

            if (memoryDiff >= 0)
            {
                if (m_samples.Count >= m_maxSamples)
                    m_samples.Dequeue();

                double elapsed = Util.EnvironmentTickCountSubtract(now, m_lastUpdateTick);

                // This should never happen since it's not useful for updates to occur with no time elapsed, but
                // protect ourselves from a divide-by-zero just in case.
                if (elapsed == 0)
                    return;

                m_samples.Enqueue(memoryDiff / (double)elapsed);
            }

            UpdateLastRecord(memoryNow, now);
        }

        private static void UpdateLastRecord(long memoryNow, int timeNow)
        {
            m_lastUpdateMemory = memoryNow;
            m_lastUpdateTick = timeNow;
        }
    }
}