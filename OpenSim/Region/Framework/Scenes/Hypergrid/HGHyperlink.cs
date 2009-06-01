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
using System.Net;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Scenes.Hypergrid
{
    public class HGHyperlink
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static Random random = new Random();

        public static RegionInfo TryLinkRegionToCoords(Scene m_scene, IClientAPI client, string mapName, uint xloc, uint yloc)
        {
            string host = "127.0.0.1";
            string portstr;
            string regionName = "";
            uint port = 9000;
            string[] parts = mapName.Split(new char[] { ':' });
            if (parts.Length >= 1)
            {
                host = parts[0];
            }
            if (parts.Length >= 2)
            {
                portstr = parts[1];
                if (!UInt32.TryParse(portstr, out port))
                    regionName = parts[1];
            }
            // always take the last one
            if (parts.Length >= 3)
            {
                regionName = parts[2];
            }

            // Sanity check. Don't ever link to this sim.
            IPAddress ipaddr = null;
            try
            {
                ipaddr = Util.GetHostFromDNS(host);
            }
            catch { }

            if ((ipaddr != null) &&
                !((m_scene.RegionInfo.ExternalEndPoint.Address.Equals(ipaddr)) && (m_scene.RegionInfo.HttpPort == port)))
            {
                RegionInfo regInfo;
                bool success = TryCreateLink(m_scene, client, xloc, yloc, regionName, port, host, out regInfo);
                if (success)
                {
                    regInfo.RegionName = mapName;
                    return regInfo;
                }
            }

            return null;
        }

        public static RegionInfo TryLinkRegion(Scene m_scene, IClientAPI client, string mapName)
        {
            uint xloc = (uint)(random.Next(0, Int16.MaxValue));
            return TryLinkRegionToCoords(m_scene, client, mapName, xloc, 0);
        }

        public static bool TryCreateLink(Scene m_scene, IClientAPI client, uint xloc, uint yloc, 
            string externalRegionName, uint externalPort, string externalHostName, out RegionInfo regInfo)
        {
            m_log.DebugFormat("[HGrid]: Link to {0}:{1}, in {2}-{3}", externalHostName, externalPort, xloc, yloc);

            regInfo = new RegionInfo();
            regInfo.RegionName = externalRegionName;
            regInfo.HttpPort = externalPort;
            regInfo.ExternalHostName = externalHostName;
            regInfo.RegionLocX = xloc;
            regInfo.RegionLocY = yloc;

            try
            {
                regInfo.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), (int)0);
            }
            catch (Exception e)
            {
                m_log.Warn("[HGrid]: Wrong format for link-region: " + e.Message);
                return false;
            }
            regInfo.RemotingAddress = regInfo.ExternalEndPoint.Address.ToString();

            // Finally, link it
            try
            {
                m_scene.CommsManager.GridService.RegisterRegion(regInfo);
            }
            catch (Exception e)
            {
                m_log.Warn("[HGrid]: Unable to link region: " + e.Message);
                return false;
            }

            uint x, y;
            if (!Check4096(m_scene, regInfo, out x, out y))
            {
                m_scene.CommsManager.GridService.DeregisterRegion(regInfo);
                if (client != null)
                    client.SendAlertMessage("Region is too far (" + x + ", " + y + ")");
                m_log.Info("[HGrid]: Unable to link, region is too far (" + x + ", " + y + ")");
                return false;
            }

            if (!CheckCoords(m_scene.RegionInfo.RegionLocX, m_scene.RegionInfo.RegionLocY, x, y))
            {
                m_scene.CommsManager.GridService.DeregisterRegion(regInfo);
                if (client != null)
                    client.SendAlertMessage("Region has incompatible coordinates (" + x + ", " + y + ")");
                m_log.Info("[HGrid]: Unable to link, region has incompatible coordinates (" + x + ", " + y + ")");
                return false;
            }

            m_log.Debug("[HGrid]: link region succeeded");
            return true;
        }

        public static bool TryUnlinkRegion(Scene m_scene, string mapName)
        {
            RegionInfo regInfo = null;
            if (mapName.Contains(":"))
            {
                string host = "127.0.0.1";
                //string portstr;
                //string regionName = "";
                uint port = 9000;
                string[] parts = mapName.Split(new char[] { ':' });
                if (parts.Length >= 1)
                {
                    host = parts[0];
                }
//                if (parts.Length >= 2)
//                {
//                    portstr = parts[1];
//                    if (!UInt32.TryParse(portstr, out port))
//                        regionName = parts[1];
//                }
                // always take the last one
//                if (parts.Length >= 3)
//                {
//                    regionName = parts[2];
//                }
                regInfo = m_scene.CommsManager.GridService.RequestNeighbourInfo(host, port);
            }
            else
            {
                regInfo = m_scene.CommsManager.GridService.RequestNeighbourInfo(mapName);
            }
            if (regInfo != null)
            {
                return m_scene.CommsManager.GridService.DeregisterRegion(regInfo);
            }
            else
            {
                m_log.InfoFormat("[HGrid]: Region {0} not found", mapName);
                return false;
            }
        }

        /// <summary>
        /// Cope with this viewer limitation.
        /// </summary>
        /// <param name="regInfo"></param>
        /// <returns></returns>
        public static bool Check4096(Scene m_scene, RegionInfo regInfo, out uint x, out uint y)
        {
            ulong realHandle;
            if (UInt64.TryParse(regInfo.regionSecret, out realHandle))
            {
                Utils.LongToUInts(realHandle, out x, out y);
                x = x / Constants.RegionSize;
                y = y / Constants.RegionSize;

                if ((Math.Abs((int)m_scene.RegionInfo.RegionLocX - (int)x) >= 4096) ||
                    (Math.Abs((int)m_scene.RegionInfo.RegionLocY - (int)y) >= 4096))
                {
                    return false;
                }
                return true;
            }
            else
            {
                m_scene.CommsManager.GridService.RegisterRegion(regInfo);
                m_log.Debug("[HGrid]: Gnomes. Region deregistered.");
                x = y = 0;
                return false;
            }
        }

        public static bool CheckCoords(uint thisx, uint thisy, uint x, uint y)
        {
            if ((thisx == x) && (thisy == y))
                return false;
            return true;
        }

    }
}
