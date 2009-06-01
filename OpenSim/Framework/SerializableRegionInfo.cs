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
using System.Net.Sockets;
using OpenMetaverse;

namespace OpenSim.Framework
{
    [Serializable]
    public class SerializableRegionInfo
    {
        public bool m_allow_alternate_ports;
        protected string m_externalHostName;

        /// <value>
        /// The port by which http communication occurs with the region (most noticeably, CAPS communication)
        ///
        /// FIXME: Defaulting to 9000 temporarily (on the basis that this is the http port most region
        /// servers are running) until the revision in which this change is made propogates around grids.
        /// </value>
        protected uint m_httpPort = 9000;

        protected IPEndPoint m_internalEndPoint;
        protected Guid m_originRegionID = UUID.Zero.Guid;
        protected string m_proxyUrl;
        protected uint? m_regionLocX;
        protected uint? m_regionLocY;
        protected string m_regionName;
        public uint m_remotingPort;
        protected string m_serverURI;
        public Guid RegionID = UUID.Zero.Guid;
        public string RemotingAddress;

        /// <summary>
        /// This is a serializable version of RegionInfo
        /// </summary>
        public SerializableRegionInfo()
        {
        }

        public SerializableRegionInfo(RegionInfo ConvertFrom)
        {
            m_regionLocX = ConvertFrom.RegionLocX;
            m_regionLocY = ConvertFrom.RegionLocY;
            m_internalEndPoint = ConvertFrom.InternalEndPoint;
            m_externalHostName = ConvertFrom.ExternalHostName;
            m_remotingPort = ConvertFrom.RemotingPort;
            m_httpPort = ConvertFrom.HttpPort;
            m_allow_alternate_ports = ConvertFrom.m_allow_alternate_ports;
            RemotingAddress = ConvertFrom.RemotingAddress;
            m_proxyUrl = ConvertFrom.proxyUrl;
            OriginRegionID = ConvertFrom.originRegionID;
            RegionName = ConvertFrom.RegionName;
            ServerURI = ConvertFrom.ServerURI;
        }

        public SerializableRegionInfo(uint regionLocX, uint regionLocY, IPEndPoint internalEndPoint, string externalUri)
        {
            m_regionLocX = regionLocX;
            m_regionLocY = regionLocY;

            m_internalEndPoint = internalEndPoint;
            m_externalHostName = externalUri;
        }

        public SerializableRegionInfo(uint regionLocX, uint regionLocY, string externalUri, uint port)
        {
            m_regionLocX = regionLocX;
            m_regionLocY = regionLocY;

            m_externalHostName = externalUri;

            m_internalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), (int) port);
        }

        public uint RemotingPort
        {
            get { return m_remotingPort; }
            set { m_remotingPort = value; }
        }

        public uint HttpPort
        {
            get { return m_httpPort; }
            set { m_httpPort = value; }
        }

        public IPEndPoint ExternalEndPoint
        {
            get
            {
                // Old one defaults to IPv6
                //return new IPEndPoint(Dns.GetHostAddresses(m_externalHostName)[0], m_internalEndPoint.Port);

                IPAddress ia = null;
                // If it is already an IP, don't resolve it - just return directly
                if (IPAddress.TryParse(m_externalHostName, out ia))
                    return new IPEndPoint(ia, m_internalEndPoint.Port);

                // Reset for next check
                ia = null;


                // New method favors IPv4
                foreach (IPAddress Adr in Dns.GetHostAddresses(m_externalHostName))
                {
                    if (ia == null)
                        ia = Adr;

                    if (Adr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ia = Adr;
                        break;
                    }
                }

                return new IPEndPoint(ia, m_internalEndPoint.Port);
            }

            set { m_externalHostName = value.ToString(); }
        }

        public string ExternalHostName
        {
            get { return m_externalHostName; }
            set { m_externalHostName = value; }
        }

        public IPEndPoint InternalEndPoint
        {
            get { return m_internalEndPoint; }
            set { m_internalEndPoint = value; }
        }

        public uint RegionLocX
        {
            get { return m_regionLocX.Value; }
            set { m_regionLocX = value; }
        }

        public uint RegionLocY
        {
            get { return m_regionLocY.Value; }
            set { m_regionLocY = value; }
        }

        public ulong RegionHandle
        {
            get { return Util.UIntsToLong((RegionLocX * (uint) Constants.RegionSize), (RegionLocY * (uint) Constants.RegionSize)); }
        }

        public string ProxyUrl
        {
            get { return m_proxyUrl; }
            set { m_proxyUrl = value; }
        }

        public UUID OriginRegionID
        {
            get { return new UUID(m_originRegionID); }
            set { m_originRegionID = value.Guid; }
        }

        public string RegionName
        {
            get { return m_regionName; }
            set { m_regionName = value; }
        }

        public string ServerURI
        {
            get { return m_serverURI; }
            set { m_serverURI = value; }
        }
    }
}
