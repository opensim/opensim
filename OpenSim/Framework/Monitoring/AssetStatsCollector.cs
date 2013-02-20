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
using System.Timers;

using OpenMetaverse.StructuredData;

namespace OpenSim.Framework.Monitoring
{
    /// <summary>
    /// Asset service statistics collection
    /// </summary>
    public class AssetStatsCollector : BaseStatsCollector
    {
        private Timer ageStatsTimer = new Timer(24 * 60 * 60 * 1000);
        private DateTime startTime = DateTime.Now;

        private long assetRequestsToday;
        private long assetRequestsNotFoundToday;
        private long assetRequestsYesterday;
        private long assetRequestsNotFoundYesterday;

        public long AssetRequestsToday { get { return assetRequestsToday; } }
        public long AssetRequestsNotFoundToday { get { return assetRequestsNotFoundToday; } }
        public long AssetRequestsYesterday { get { return assetRequestsYesterday; } }
        public long AssetRequestsNotFoundYesterday { get { return assetRequestsNotFoundYesterday; } }

        public AssetStatsCollector()
        {
            ageStatsTimer.Elapsed += new ElapsedEventHandler(OnAgeing);
            ageStatsTimer.Enabled = true;
        }

        private void OnAgeing(object source, ElapsedEventArgs e)
        {
            assetRequestsYesterday = assetRequestsToday;

            // There is a possibility that an asset request could occur between the execution of these
            // two statements.  But we're better off without the synchronization overhead.
            assetRequestsToday = 0;

            assetRequestsNotFoundYesterday = assetRequestsNotFoundToday;
            assetRequestsNotFoundToday = 0;
        }

        /// <summary>
        /// Record that an asset request failed to find an asset
        /// </summary>
        public void AddNotFoundRequest()
        {
            assetRequestsNotFoundToday++;
        }

        /// <summary>
        /// Record that a request was made to the asset server
        /// </summary>
        public void AddRequest()
        {
            assetRequestsToday++;
        }

        /// <summary>
        /// Report back collected statistical information.
        /// </summary>
        /// <returns></returns>
        override public string Report()
        {
            double elapsedHours = (DateTime.Now - startTime).TotalHours;
            if (elapsedHours <= 0) { elapsedHours = 1; }  // prevent divide by zero

            long assetRequestsTodayPerHour = (long)Math.Round(AssetRequestsToday / elapsedHours);
            long assetRequestsYesterdayPerHour = (long)Math.Round(AssetRequestsYesterday / 24.0);

            return string.Format(
@"Asset requests today     : {0}  ({1} per hour)  of which {2} were not found
Asset requests yesterday : {3}  ({4} per hour)  of which {5} were not found",
                AssetRequestsToday, assetRequestsTodayPerHour, AssetRequestsNotFoundToday,
                AssetRequestsYesterday, assetRequestsYesterdayPerHour, AssetRequestsNotFoundYesterday);
        }

        public override string XReport(string uptime, string version)
        {
            return OSDParser.SerializeJsonString(OReport(uptime, version));
        }

        public override OSDMap OReport(string uptime, string version)
        {
            double elapsedHours = (DateTime.Now - startTime).TotalHours;
            if (elapsedHours <= 0) { elapsedHours = 1; }  // prevent divide by zero

            long assetRequestsTodayPerHour = (long)Math.Round(AssetRequestsToday / elapsedHours);
            long assetRequestsYesterdayPerHour = (long)Math.Round(AssetRequestsYesterday / 24.0);

            OSDMap ret = new OSDMap();
            ret.Add("AssetRequestsToday", OSD.FromLong(AssetRequestsToday));
            ret.Add("AssetRequestsTodayPerHour", OSD.FromLong(assetRequestsTodayPerHour));
            ret.Add("AssetRequestsNotFoundToday", OSD.FromLong(AssetRequestsNotFoundToday));
            ret.Add("AssetRequestsYesterday", OSD.FromLong(AssetRequestsYesterday));
            ret.Add("AssetRequestsYesterdayPerHour", OSD.FromLong(assetRequestsYesterdayPerHour));
            ret.Add("AssetRequestsNotFoundYesterday", OSD.FromLong(assetRequestsNotFoundYesterday));

            return ret;
        }
    }
}
