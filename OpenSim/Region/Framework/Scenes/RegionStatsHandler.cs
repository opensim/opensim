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
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Scenes
{
    public class RegionStatsHandler : BaseStreamHandler
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string osXStatsURI = String.Empty;
        //private string osSecret = String.Empty;
        private OpenSim.Framework.RegionInfo regionInfo;
        public string localZone = TimeZone.CurrentTimeZone.StandardName;
        public TimeSpan utcOffset = TimeZone.CurrentTimeZone.GetUtcOffset(DateTime.Now);

        public RegionStatsHandler(RegionInfo region_info)
            : base("GET", "/" + Util.SHA1Hash(region_info.regionSecret), "RegionStats", "Region Statistics")
        {
            regionInfo = region_info;
            osXStatsURI = Util.SHA1Hash(regionInfo.osSecret);
        }

        protected override byte[] ProcessRequest(
            string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            return Util.UTF8.GetBytes(Report());
        }

        public override string ContentType
        {
            get { return "text/plain"; }
        }

        private string Report()
        {
            OSDMap args = new OSDMap(30);
            //int time = Util.ToUnixTime(DateTime.Now);
            args["OSStatsURI"] = OSD.FromString("http://" + regionInfo.ExternalHostName + ":" + regionInfo.HttpPort + "/" + osXStatsURI + "/");
            args["TimeZoneName"] = OSD.FromString(localZone);
            args["TimeZoneOffs"] = OSD.FromReal(utcOffset.TotalHours);
            args["UxTime"] = OSD.FromInteger(Util.ToUnixTime(DateTime.Now));
            args["Memory"] = OSD.FromReal(Math.Round(GC.GetTotalMemory(false) / 1024.0 / 1024.0));
            args["Version"] = OSD.FromString(VersionInfo.Version);

            string strBuffer = "";
            strBuffer = OSDParser.SerializeJsonString(args);

            return strBuffer;
         }
    }
}