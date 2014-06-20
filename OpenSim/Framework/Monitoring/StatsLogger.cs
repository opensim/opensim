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
using System.Reflection;
using System.Timers;
using log4net;

namespace OpenSim.Framework.Monitoring
{
    /// <summary>
    /// Provides a means to continuously log stats for debugging purposes.
    /// </summary>
    public static class StatsLogger
    {
        private static readonly ILog m_statsLog = LogManager.GetLogger("special.StatsLogger");

        private static Timer m_loggingTimer;
        private static int m_statsLogIntervalMs = 5000;

        public static void RegisterConsoleCommands(ICommandConsole console)
        {
            console.Commands.AddCommand(
                "Debug",
                false,
                "debug stats record",
                "debug stats record start|stop",
                "Control whether stats are being regularly recorded to a separate file.",
                "For debug purposes.  Experimental.",
                HandleStatsRecordCommand);
        }

        public static void HandleStatsRecordCommand(string module, string[] cmd)
        {
            ICommandConsole con = MainConsole.Instance;

            if (cmd.Length != 4)
            {
                con.Output("Usage: debug stats record start|stop");
                return;
            }

            if (cmd[3] == "start")
            {
                Start();
                con.OutputFormat("Now recording all stats to file every {0}ms", m_statsLogIntervalMs);
            }
            else if (cmd[3] == "stop")
            {
                Stop();
                con.Output("Stopped recording stats to file.");
            }
        }

        public static void Start()
        {
            if (m_loggingTimer != null)
                Stop();

            m_loggingTimer = new Timer(m_statsLogIntervalMs);
            m_loggingTimer.AutoReset = false;
            m_loggingTimer.Elapsed += Log;
            m_loggingTimer.Start();
        }

        public static void Stop()
        {
            if (m_loggingTimer != null)
            {
                m_loggingTimer.Stop();
            }
        }

        private static void Log(object sender, ElapsedEventArgs e)
        {
            m_statsLog.InfoFormat("*** STATS REPORT AT {0} ***", DateTime.Now);

            foreach (string report in StatsManager.GetAllStatsReports())
                m_statsLog.Info(report);

            m_loggingTimer.Start();
        }
    }
}