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
using System.Net;
using System.Net.Sockets;
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Services.Interfaces
{
    public interface IGridService
    {
        /// <summary>
        /// Register a region with the grid service.
        /// </summary>
        /// <param name="regionInfos"> </param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Thrown if region registration failed</exception>
        bool RegisterRegion(UUID scopeID, GridRegion regionInfos);

        /// <summary>
        /// Deregister a region with the grid service.
        /// </summary>
        /// <param name="regionID"></param>
        /// <returns></returns>
        /// <exception cref="System.Exception">Thrown if region deregistration failed</exception>
        bool DeregisterRegion(UUID regionID);   

        /// <summary>
        /// Get information about the regions neighbouring the given co-ordinates (in meters).
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        List<GridRegion> GetNeighbours(UUID scopeID, UUID regionID);

        GridRegion GetRegionByUUID(UUID scopeID, UUID regionID);

        /// <summary>
        /// Get the region at the given position (in meters)
        /// </summary>
        /// <param name="scopeID"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        GridRegion GetRegionByPosition(UUID scopeID, int x, int y);

        GridRegion GetRegionByName(UUID scopeID, string regionName);

        /// <summary>
        /// Get information about regions starting with the provided name. 
        /// </summary>
        /// <param name="name">
        /// The name to match against.
        /// </param>
        /// <param name="maxNumber">
        /// The maximum number of results to return.
        /// </param>
        /// <returns>
        /// A list of <see cref="RegionInfo"/>s of regions with matching name. If the
        /// grid-server couldn't be contacted or returned an error, return null. 
        /// </returns>
        List<GridRegion> GetRegionsByName(UUID scopeID, string name, int maxNumber);

        List<GridRegion> GetRegionRange(UUID scopeID, int xmin, int xmax, int ymin, int ymax);

    }

    public class GridRegion
    {

        /// <summary>
        /// The port by which http communication occurs with the region 
        /// </summary>
        public uint HttpPort
        {
            get { return m_httpPort; }
            set { m_httpPort = value; }
        }
        protected uint m_httpPort;

        /// <summary>
        /// A well-formed URI for the host region server (namely "http://" + ExternalHostName)
        /// </summary>
        public string ServerURI
        {
            get { return m_serverURI; }
            set { m_serverURI = value; }
        }
        protected string m_serverURI;

        public string RegionName
        {
            get { return m_regionName; }
            set { m_regionName = value; }
        }
        protected string m_regionName = String.Empty;

        protected bool Allow_Alternate_Ports;
        public bool m_allow_alternate_ports;

        protected string m_externalHostName;

        protected IPEndPoint m_internalEndPoint;

        public int RegionLocX
        {
            get { return m_regionLocX; }
            set { m_regionLocX = value; }
        }
        protected int m_regionLocX;

        public int RegionLocY
        {
            get { return m_regionLocY; }
            set { m_regionLocY = value; }
        }
        protected int m_regionLocY;

        public UUID RegionID = UUID.Zero;
        public UUID ScopeID = UUID.Zero;

        public GridRegion()
        {
        }

        public GridRegion(int regionLocX, int regionLocY, IPEndPoint internalEndPoint, string externalUri)
        {
            m_regionLocX = regionLocX;
            m_regionLocY = regionLocY;

            m_internalEndPoint = internalEndPoint;
            m_externalHostName = externalUri;
        }

        public GridRegion(int regionLocX, int regionLocY, string externalUri, uint port)
        {
            m_regionLocX = regionLocX;
            m_regionLocY = regionLocY;

            m_externalHostName = externalUri;

            m_internalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), (int)port);
        }

        public GridRegion(uint xcell, uint ycell)
        {
            m_regionLocX = (int)(xcell * Constants.RegionSize);
            m_regionLocY = (int)(ycell * Constants.RegionSize);
        }

        public GridRegion(RegionInfo ConvertFrom)
        {
            m_regionName = ConvertFrom.RegionName;
            m_regionLocX = (int)(ConvertFrom.RegionLocX * Constants.RegionSize);
            m_regionLocY = (int)(ConvertFrom.RegionLocY * Constants.RegionSize);
            m_internalEndPoint = ConvertFrom.InternalEndPoint;
            m_externalHostName = ConvertFrom.ExternalHostName;
            m_httpPort = ConvertFrom.HttpPort;
            m_allow_alternate_ports = ConvertFrom.m_allow_alternate_ports;
            RegionID = UUID.Zero;
            ServerURI = ConvertFrom.ServerURI;
        }


        /// <value>
        /// This accessor can throw all the exceptions that Dns.GetHostAddresses can throw.
        ///
        /// XXX Isn't this really doing too much to be a simple getter, rather than an explict method?
        /// </value>
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
                try
                {
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
                }
                catch (SocketException e)
                {
                    throw new Exception(
                        "Unable to resolve local hostname " + m_externalHostName + " innerException of type '" +
                        e + "' attached to this exception", e);
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

        public ulong RegionHandle
        {
            get { return Util.UIntsToLong((uint)RegionLocX, (uint)RegionLocY); }
        }

        public int getInternalEndPointPort()
        {
            return m_internalEndPoint.Port;
        }

        public Dictionary<string, object> ToKeyValuePairs()
        {
            Dictionary<string, object> kvp = new Dictionary<string, object>();
            kvp["uuid"] = RegionID.ToString();
            kvp["locX"] = RegionLocX.ToString();
            kvp["locY"] = RegionLocY.ToString();
            kvp["name"] = RegionName;
            kvp["external_ip_address"] = ExternalEndPoint.Address.ToString();
            kvp["external_port"] = ExternalEndPoint.Port.ToString();
            kvp["external_host_name"] = ExternalHostName;
            kvp["http_port"] = HttpPort.ToString();
            kvp["internal_ip_address"] = InternalEndPoint.Address.ToString();
            kvp["internal_port"] = InternalEndPoint.Port.ToString();
            kvp["alternate_ports"] = m_allow_alternate_ports.ToString();
            kvp["server_uri"] = ServerURI;

            return kvp;
        }

        public GridRegion(Dictionary<string, object> kvp)
        {
            if (kvp["uuid"] != null)
                RegionID = new UUID((string)kvp["uuid"]);

            if (kvp["locX"] != null)
                RegionLocX = Convert.ToInt32((string)kvp["locX"]);

            if (kvp["locY"] != null)
                RegionLocY = Convert.ToInt32((string)kvp["locY"]);

            if (kvp["name"] != null)
                RegionName = (string)kvp["name"];

            if ((kvp["external_ip_address"] != null) && (kvp["external_port"] != null))
            {
                int port = 0;
                Int32.TryParse((string)kvp["external_port"], out port);
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse((string)kvp["external_ip_address"]), port);
                ExternalEndPoint = ep;
            }
            else
                ExternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0);

            if (kvp["external_host_name"] != null)
                ExternalHostName = (string)kvp["external_host_name"];

            if (kvp["http_port"] != null)
            {
                UInt32 port = 0;
                UInt32.TryParse((string)kvp["http_port"], out port);
                HttpPort = port;
            }

            if ((kvp["internal_ip_address"] != null) && (kvp["internal_port"] != null))
            {
                int port = 0;
                Int32.TryParse((string)kvp["internal_port"], out port);
                IPEndPoint ep = new IPEndPoint(IPAddress.Parse((string)kvp["internal_ip_address"]), port);
                InternalEndPoint = ep;
            }
            else
                InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), 0);

            if (kvp["alternate_ports"] != null)
            {
                bool alts = false;
                Boolean.TryParse((string)kvp["alternate_ports"], out alts);
                m_allow_alternate_ports = alts;
            }

            if (kvp["server_uri"] != null)
                ServerURI = (string)kvp["server_uri"];
        }
    }

}
