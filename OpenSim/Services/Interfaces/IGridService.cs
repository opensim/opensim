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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

using OpenSim.Framework;
using OpenMetaverse;

using log4net;

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
        string RegisterRegion(UUID scopeID, GridRegion regionInfos);

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

        /// <summary>
        /// Get information about a region which exactly matches the name given.
        /// </summary>
        /// <param name="scopeID"></param>
        /// <param name="regionName"></param>
        /// <returns>Returns the region information if the name matched.  Null otherwise.</returns>
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

        List<GridRegion> GetDefaultRegions(UUID scopeID);
        List<GridRegion> GetDefaultHypergridRegions(UUID scopeID);
        List<GridRegion> GetFallbackRegions(UUID scopeID, int x, int y);
        List<GridRegion> GetHyperlinks(UUID scopeID);

        /// <summary>
        /// Get internal OpenSimulator region flags.
        /// </summary>
        /// <remarks>
        /// See OpenSimulator.Framework.RegionFlags.  These are not returned in the GridRegion structure -
        /// they currently need to be requested separately.  Possibly this should change to avoid multiple service calls
        /// in some situations.
        /// </remarks>
        /// <returns>
        /// The region flags.
        /// </returns>
        /// <param name='scopeID'></param>
        /// <param name='regionID'></param>
        int GetRegionFlags(UUID scopeID, UUID regionID);

        Dictionary<string,object> GetExtraFeatures();
    }

    public interface IHypergridLinker
    {
        GridRegion TryLinkRegionToCoords(UUID scopeID, string mapName, int xloc, int yloc, UUID ownerID, out string reason);
        bool TryUnlinkRegion(string mapName);
    }

    public class GridRegion
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

#pragma warning disable 414
        private static readonly string LogHeader = "[GRID REGION]";
#pragma warning restore 414

        /// <summary>
        /// The port by which http communication occurs with the region
        /// </summary>
        public uint HttpPort { get; set; }

        /// <summary>
        /// A well-formed URI for the host region server (namely "http://" + ExternalHostName)
        /// </summary>
        public string ServerURI
        {
            get {
                if (!String.IsNullOrEmpty(m_serverURI)) {
                    return m_serverURI;
                } else {
                    if (HttpPort == 0)
                        return "http://" + m_externalHostName + "/";
                    else
                        return "http://" + m_externalHostName + ":" + HttpPort + "/";
                }
            }
            set {
                if ( value == null)
                {
                    m_serverURI = String.Empty;
                    return;
                }

                if ( value.EndsWith("/") )
                {

                    m_serverURI = value;
                }
                else
                {
                    m_serverURI = value + '/';
                }
            }
        }

        protected string m_serverURI;

        /// <summary>
        /// Provides direct access to the 'm_serverURI' field, without returning a generated URL if m_serverURI is missing.
        /// </summary>
        public string RawServerURI
        {
            get { return m_serverURI; }
            set { m_serverURI = value; }
        }


        public string RegionName
        {
            get { return m_regionName; }
            set { m_regionName = value; }
        }
        protected string m_regionName = String.Empty;

        /// <summary>
        /// Region flags.
        /// </summary>
        /// <remarks>
        /// If not set (chiefly if a robust service is running code pre OpenSim 0.8.1) then this will be null and
        /// should be ignored.  If you require flags information please use the separate IGridService.GetRegionFlags() call
        /// XXX: This field is currently ignored when used in RegisterRegion, but could potentially be
        /// used to set flags at this point.
        /// </remarks>
        public OpenSim.Framework.RegionFlags? RegionFlags { get; set; }

        protected string m_externalHostName;

        protected IPEndPoint m_internalEndPoint;

        /// <summary>
        /// The co-ordinate of this region in region units.
        /// </summary>
        public int RegionCoordX { get { return (int)Util.WorldToRegionLoc((uint)RegionLocX); } }

        /// <summary>
        /// The co-ordinate of this region in region units
        /// </summary>
        public int RegionCoordY { get { return (int)Util.WorldToRegionLoc((uint)RegionLocY); } }

        /// <summary>
        /// The location of this region in meters.
        /// DANGER DANGER! Note that this name means something different in RegionInfo.
        /// </summary>
        public int RegionLocX
        {
            get { return m_regionLocX; }
            set { m_regionLocX = value; }
        }
        protected int m_regionLocX;

        public int RegionSizeX { get; set; }
        public int RegionSizeY { get; set; }

        /// <summary>
        /// The location of this region in meters.
        /// DANGER DANGER! Note that this name means something different in RegionInfo.
        /// </summary>
        public int RegionLocY
        {
            get { return m_regionLocY; }
            set { m_regionLocY = value; }
        }
        protected int m_regionLocY;

        protected UUID m_estateOwner;

        public UUID EstateOwner
        {
            get { return m_estateOwner; }
            set { m_estateOwner = value; }
        }

        public UUID RegionID = UUID.Zero;
        public UUID ScopeID = UUID.Zero;

        public UUID TerrainImage = UUID.Zero;
        public UUID ParcelImage = UUID.Zero;
        public byte Access;
        public int  Maturity;
        public string RegionSecret = string.Empty;
        public string Token = string.Empty;

        public GridRegion()
        {
            RegionSizeX = (int)Constants.RegionSize;
            RegionSizeY = (int)Constants.RegionSize;
            m_serverURI = string.Empty;
        }

        /*
        public GridRegion(int regionLocX, int regionLocY, IPEndPoint internalEndPoint, string externalUri)
        {
            m_regionLocX = regionLocX;
            m_regionLocY = regionLocY;
            RegionSizeX = (int)Constants.RegionSize;
            RegionSizeY = (int)Constants.RegionSize;

            m_internalEndPoint = internalEndPoint;
            m_externalHostName = externalUri;
        }

        public GridRegion(int regionLocX, int regionLocY, string externalUri, uint port)
        {
            m_regionLocX = regionLocX;
            m_regionLocY = regionLocY;
            RegionSizeX = (int)Constants.RegionSize;
            RegionSizeY = (int)Constants.RegionSize;

            m_externalHostName = externalUri;

            m_internalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), (int)port);
        }
         */

        public GridRegion(uint xcell, uint ycell)
        {
            m_regionLocX = (int)Util.RegionToWorldLoc(xcell);
            m_regionLocY = (int)Util.RegionToWorldLoc(ycell);
            RegionSizeX = (int)Constants.RegionSize;
            RegionSizeY = (int)Constants.RegionSize;
        }

        public GridRegion(RegionInfo ConvertFrom)
        {
            m_regionName = ConvertFrom.RegionName;
            m_regionLocX = (int)(ConvertFrom.WorldLocX);
            m_regionLocY = (int)(ConvertFrom.WorldLocY);
            RegionSizeX = (int)ConvertFrom.RegionSizeX;
            RegionSizeY = (int)ConvertFrom.RegionSizeY;
            m_internalEndPoint = ConvertFrom.InternalEndPoint;
            m_externalHostName = ConvertFrom.ExternalHostName;
            HttpPort = ConvertFrom.HttpPort;
            RegionID = ConvertFrom.RegionID;
            ServerURI = ConvertFrom.ServerURI;
            TerrainImage = ConvertFrom.RegionSettings.TerrainImageID;
            ParcelImage = ConvertFrom.RegionSettings.ParcelImageID;
            Access = ConvertFrom.AccessLevel;
            Maturity = ConvertFrom.RegionSettings.Maturity;
            RegionSecret = ConvertFrom.regionSecret;
            EstateOwner = ConvertFrom.EstateSettings.EstateOwner;
        }

        public GridRegion(GridRegion ConvertFrom)
        {
            m_regionName = ConvertFrom.RegionName;
            RegionFlags = ConvertFrom.RegionFlags;
            m_regionLocX = ConvertFrom.RegionLocX;
            m_regionLocY = ConvertFrom.RegionLocY;
            RegionSizeX = ConvertFrom.RegionSizeX;
            RegionSizeY = ConvertFrom.RegionSizeY;
            m_internalEndPoint = ConvertFrom.InternalEndPoint;
            m_externalHostName = ConvertFrom.ExternalHostName;
            HttpPort = ConvertFrom.HttpPort;
            RegionID = ConvertFrom.RegionID;
            ServerURI = ConvertFrom.ServerURI;
            TerrainImage = ConvertFrom.TerrainImage;
            ParcelImage = ConvertFrom.ParcelImage;
            Access = ConvertFrom.Access;
            Maturity = ConvertFrom.Maturity;
            RegionSecret = ConvertFrom.RegionSecret;
            EstateOwner = ConvertFrom.EstateOwner;
        }

        public GridRegion(Dictionary<string, object> kvp)
        {
            if (kvp.ContainsKey("uuid"))
                RegionID = new UUID((string)kvp["uuid"]);

            if (kvp.ContainsKey("locX"))
                RegionLocX = Convert.ToInt32((string)kvp["locX"]);

            if (kvp.ContainsKey("locY"))
                RegionLocY = Convert.ToInt32((string)kvp["locY"]);

            if (kvp.ContainsKey("sizeX"))
                RegionSizeX = Convert.ToInt32((string)kvp["sizeX"]);
            else
                RegionSizeX = (int)Constants.RegionSize;

            if (kvp.ContainsKey("sizeY"))
                RegionSizeY = Convert.ToInt32((string)kvp["sizeY"]);
            else
                RegionSizeX = (int)Constants.RegionSize;

            if (kvp.ContainsKey("regionName"))
                RegionName = (string)kvp["regionName"];

            if (kvp.ContainsKey("access"))
            {
                byte access = Convert.ToByte((string)kvp["access"]);
                Access = access;
                Maturity = (int)Util.ConvertAccessLevelToMaturity(access);
            }

            if (kvp.ContainsKey("flags") && kvp["flags"] != null)
                RegionFlags = (OpenSim.Framework.RegionFlags?)Convert.ToInt32((string)kvp["flags"]);

            if (kvp.ContainsKey("serverIP"))
            {
                //int port = 0;
                //Int32.TryParse((string)kvp["serverPort"], out port);
                //IPEndPoint ep = new IPEndPoint(IPAddress.Parse((string)kvp["serverIP"]), port);
                ExternalHostName = (string)kvp["serverIP"];
            }
            else
                ExternalHostName = "127.0.0.1";

            if (kvp.ContainsKey("serverPort"))
            {
                Int32 port = 0;
                Int32.TryParse((string)kvp["serverPort"], out port);
                InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);
            }

            if (kvp.ContainsKey("serverHttpPort"))
            {
                UInt32 port = 0;
                UInt32.TryParse((string)kvp["serverHttpPort"], out port);
                HttpPort = port;
            }

            if (kvp.ContainsKey("serverURI"))
                ServerURI = (string)kvp["serverURI"];

            if (kvp.ContainsKey("regionMapTexture"))
                UUID.TryParse((string)kvp["regionMapTexture"], out TerrainImage);

            if (kvp.ContainsKey("parcelMapTexture"))
                UUID.TryParse((string)kvp["parcelMapTexture"], out ParcelImage);

            if (kvp.ContainsKey("regionSecret"))
                RegionSecret =(string)kvp["regionSecret"];

            if (kvp.ContainsKey("owner_uuid"))
                EstateOwner = new UUID(kvp["owner_uuid"].ToString());

            if (kvp.ContainsKey("Token"))
                Token = kvp["Token"].ToString();

            // m_log.DebugFormat("{0} New GridRegion. id={1}, loc=<{2},{3}>, size=<{4},{5}>",
            //                         LogHeader, RegionID, RegionLocX, RegionLocY, RegionSizeX, RegionSizeY);
        }

        public Dictionary<string, object> ToKeyValuePairs()
        {
            Dictionary<string, object> kvp = new Dictionary<string, object>();
            kvp["uuid"] = RegionID.ToString();
            kvp["locX"] = RegionLocX.ToString();
            kvp["locY"] = RegionLocY.ToString();
            kvp["sizeX"] = RegionSizeX.ToString();
            kvp["sizeY"] = RegionSizeY.ToString();
            kvp["regionName"] = RegionName;

            if (RegionFlags != null)
                kvp["flags"] = ((int)RegionFlags).ToString();

            kvp["serverIP"] = ExternalHostName; //ExternalEndPoint.Address.ToString();
            kvp["serverHttpPort"] = HttpPort.ToString();
            kvp["serverURI"] = ServerURI;
            kvp["serverPort"] = InternalEndPoint.Port.ToString();
            kvp["regionMapTexture"] = TerrainImage.ToString();
            kvp["parcelMapTexture"] = ParcelImage.ToString();
            kvp["access"] = Access.ToString();
            kvp["regionSecret"] = RegionSecret;
            kvp["owner_uuid"] = EstateOwner.ToString();
            kvp["Token"] = Token.ToString();
            // Maturity doesn't seem to exist in the DB

            return kvp;
        }

        #region Definition of equality

        /// <summary>
        /// Define equality as two regions having the same, non-zero UUID.
        /// </summary>
        public bool Equals(GridRegion region)
        {
            if ((object)region == null)
                return false;
            // Return true if the non-zero UUIDs are equal:
            return (RegionID != UUID.Zero) && RegionID.Equals(region.RegionID);
        }

        public override bool Equals(Object obj)
        {
            if (obj == null)
                return false;
            return Equals(obj as GridRegion);
        }

        public override int GetHashCode()
        {
            return RegionID.GetHashCode() ^ TerrainImage.GetHashCode() ^ ParcelImage.GetHashCode();
        }

        #endregion

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
                    /*throw new Exception(
                        "Unable to resolve local hostname " + m_externalHostName + " innerException of type '" +
                        e + "' attached to this exception", e);*/
                    // Don't throw a fatal exception here, instead, return Null and handle it in the caller.
                    // Reason is, on systems such as OSgrid it has occured that known hostnames stop
                    // resolving and thus make surrounding regions crash out with this exception.
                    return null;
                }

                return new IPEndPoint(ia, m_internalEndPoint.Port);
            }
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
    }
}
