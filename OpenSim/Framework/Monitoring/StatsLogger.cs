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
using System.IO;
using System.Reflection;
using System.Text;
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
                "General",
                false,
                "stats record",
                "stats record start|stop",
                "Control whether stats are being regularly recorded to a separate file.",
                "For debug purposes.  Experimental.",
                HandleStatsRecordCommand);

            console.Commands.AddCommand(
                "General",
                false,
                "stats save",
                "stats save <path>",
                "Save stats snapshot to a file.  If the file already exists, then the report is appended.",
                "For debug purposes.  Experimental.",
                HandleStatsSaveCommand);
        }

        public static void HandleStatsRecordCommand(string module, string[] cmd)
        {
            ICommandConsole con = MainConsole.Instance;

            if (cmd.Length != 3)
            {
                con.Output("Usage: stats record start|stop");
                return;
            }

            if (cmd[2] == "start")
            {
                Start();
                con.Output("Now recording all stats to file every {0}ms", m_statsLogIntervalMs);
            }
            else if (cmd[2] == "stop")
            {
                Stop();
                con.Output("Stopped recording stats to file.");
            }
        }

        public static void HandleStatsSaveCommand(string module, string[] cmd)
        {
            ICommandConsole con = MainConsole.Instance;

            if (cmd.Length != 3)
            {
                con.Output("Usage: stats save <path>");
                return;
            }

            string path = cmd[2];

            using (StreamWriter sw = new StreamWriter(path, true))
            {
                foreach (string line in GetReport())
                    sw.WriteLine(line);
            }

            MainConsole.Instance.Output("Stats saved to file {0}", path);
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
            foreach (string line in GetReport())
                m_statsLog.Info(line);

            m_loggingTimer.Start();
        }

        private static List<string> GetReport()
        {
            List<string> lines = new List<string>();

            lines.Add(string.Format("*** STATS REPORT AT {0} ***", DateTime.Now));

            foreach (string report in StatsManager.GetAllStatsReports())
                lines.Add(report);

            return lines;
        }
    }
}