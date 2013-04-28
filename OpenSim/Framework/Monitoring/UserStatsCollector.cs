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

using System.Timers;

using OpenMetaverse.StructuredData;

namespace OpenSim.Framework.Monitoring
{
    /// <summary>
    /// Collects user service statistics
    /// </summary>
    public class UserStatsCollector : BaseStatsCollector
    {
        private Timer ageStatsTimer = new Timer(24 * 60 * 60 * 1000);

        private int successfulLoginsToday;
        public int SuccessfulLoginsToday { get { return successfulLoginsToday; } }

        private int successfulLoginsYesterday;
        public int SuccessfulLoginsYesterday { get { return successfulLoginsYesterday; } }

        private int successfulLogins;
        public int SuccessfulLogins { get { return successfulLogins; } }

        private int logouts;
        public int Logouts { get { return logouts; } }

        public UserStatsCollector()
        {
            ageStatsTimer.Elapsed += new ElapsedEventHandler(OnAgeing);
            ageStatsTimer.Enabled = true;
        }

        private void OnAgeing(object source, ElapsedEventArgs e)
        {
            successfulLoginsYesterday = successfulLoginsToday;

            // There is a possibility that an asset request could occur between the execution of these
            // two statements.  But we're better off without the synchronization overhead.
            successfulLoginsToday = 0;
        }

        /// <summary>
        /// Record a successful login
        /// </summary>
        public void AddSuccessfulLogin()
        {
            successfulLogins++;
            successfulLoginsToday++;
        }

        public void AddLogout()
        {
            logouts++;
        }

        /// <summary>
        /// Report back collected statistical information.
        /// </summary>
        /// <returns></returns>
        override public string Report()
        {
            return string.Format(
@"Successful logins total : {0}, today : {1}, yesterday : {2}
          Logouts total : {3}",
                SuccessfulLogins, SuccessfulLoginsToday, SuccessfulLoginsYesterday, Logouts);
        }

        public override string XReport(string uptime, string version)
        {
            return OSDParser.SerializeJsonString(OReport(uptime, version));
        }

        public override OSDMap OReport(string uptime, string version)
        {
            OSDMap ret = new OSDMap();
            ret.Add("SuccessfulLogins", OSD.FromInteger(SuccessfulLogins));
            ret.Add("SuccessfulLoginsToday", OSD.FromInteger(SuccessfulLoginsToday));
            ret.Add("SuccessfulLoginsYesterday", OSD.FromInteger(SuccessfulLoginsYesterday));
            ret.Add("Logouts", OSD.FromInteger(Logouts));

            return ret;
        }
    }
}
