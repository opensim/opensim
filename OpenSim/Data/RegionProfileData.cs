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
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data
{
    /// <summary>
    /// A class which contains information known to the grid server about a region
    /// </summary>
    [Serializable]
    public class RegionProfileData
    {
        /// <summary>
        /// The name of the region
        /// </summary>
        public string regionName = String.Empty;

        /// <summary>
        /// A 64-bit number combining map position into a (mostly) unique ID
        /// </summary>
        public ulong regionHandle;

        /// <summary>
        /// OGS/OpenSim Specific ID for a region
        /// </summary>
        public UUID UUID;

        /// <summary>
        /// Coordinates of the region
        /// </summary>
        public uint regionLocX;
        public uint regionLocY;
        public uint regionLocZ; // Reserved (round-robin, layers, etc)

        /// <summary>
        /// Authentication secrets
        /// </summary>
        /// <remarks>Not very secure, needs improvement.</remarks>
        public string regionSendKey = String.Empty;
        public string regionRecvKey = String.Empty;
        public string regionSecret = String.Empty;

        /// <summary>
        /// Whether the region is online
        /// </summary>
        public bool regionOnline;

        /// <summary>
        /// Information about the server that the region is currently hosted on
        /// </summary>
        public string serverIP = String.Empty;
        public uint serverPort;
        public string serverURI = String.Empty;

        public uint httpPort;
        public uint remotingPort;
        public string httpServerURI = String.Empty;

        /// <summary>
        /// Set of optional overrides. Can be used to create non-eulicidean spaces.
        /// </summary>
        public ulong regionNorthOverrideHandle;
        public ulong regionSouthOverrideHandle;
        public ulong regionEastOverrideHandle;
        public ulong regionWestOverrideHandle;

        /// <summary>
        /// Optional: URI Location of the region database
        /// </summary>
        /// <remarks>Used for floating sim pools where the region data is not nessecarily coupled to a specific server</remarks>
        public string regionDataURI = String.Empty;

        /// <summary>
        /// Region Asset Details
        /// </summary>
        public string regionAssetURI = String.Empty;

        public string regionAssetSendKey = String.Empty;
        public string regionAssetRecvKey = String.Empty;

        /// <summary>
        /// Region Userserver Details
        /// </summary>
        public string regionUserURI = String.Empty;

        public string regionUserSendKey = String.Empty;
        public string regionUserRecvKey = String.Empty;

        /// <summary>
        /// Region Map Texture Asset
        /// </summary>
        public UUID regionMapTextureID = new UUID("00000000-0000-1111-9999-000000000006");

        /// <summary>
        /// this particular mod to the file provides support within the spec for RegionProfileData for the
        /// owner_uuid for the region
        /// </summary>
        public UUID owner_uuid = UUID.Zero;

        /// <summary>
        /// OGS/OpenSim Specific original ID for a region after move/split
        /// </summary>
        public UUID originUUID;

        /// <summary>
        /// The Maturity rating of the region
        /// </summary>
        public uint maturity;


        //Data Wrappers
        public string RegionName
        {
            get { return regionName; }
            set { regionName = value; }
        }
        public ulong RegionHandle
        {
            get { return regionHandle; }
            set { regionHandle = value; }
        }
        public UUID Uuid
        {
            get { return UUID; }
            set { UUID = value; }
        }
        public uint RegionLocX
        {
            get { return regionLocX; }
            set { regionLocX = value; }
        }
        public uint RegionLocY
        {
            get { return regionLocY; }
            set { regionLocY = value; }
        }
        public uint RegionLocZ
        {
            get { return regionLocZ; }
            set { regionLocZ = value; }
        }
        public string RegionSendKey
        {
            get { return regionSendKey; }
            set { regionSendKey = value; }
        }
        public string RegionRecvKey
        {
            get { return regionRecvKey; }
            set { regionRecvKey = value; }
        }
        public string RegionSecret
        {
            get { return regionSecret; }
            set { regionSecret = value; }
        }
        public bool RegionOnline
        {
            get { return regionOnline; }
            set { regionOnline = value; }
        }
        public string ServerIP
        {
            get { return serverIP; }
            set { serverIP = value; }
        }
        public uint ServerPort
        {
            get { return serverPort; }
            set { serverPort = value; }
        }
        public string ServerURI
        {
            get { return serverURI; }
            set { serverURI = value; }
        }
        public uint ServerHttpPort
        {
            get { return httpPort; }
            set { httpPort = value; }
        }
        public uint ServerRemotingPort
        {
            get { return remotingPort; }
            set { remotingPort = value; }
        }

        public ulong NorthOverrideHandle
        {
            get { return regionNorthOverrideHandle; }
            set { regionNorthOverrideHandle = value; }
        }
        public ulong SouthOverrideHandle
        {
            get { return regionSouthOverrideHandle; }
            set { regionSouthOverrideHandle = value; }
        }
        public ulong EastOverrideHandle
        {
            get { return regionEastOverrideHandle; }
            set { regionEastOverrideHandle = value; }
        }
        public ulong WestOverrideHandle
        {
            get { return regionWestOverrideHandle; }
            set { regionWestOverrideHandle = value; }
        }
        public string RegionDataURI
        {
            get { return regionDataURI; }
            set { regionDataURI = value; }
        }
        public string RegionAssetURI
        {
            get { return regionAssetURI; }
            set { regionAssetURI = value; }
        }
        public string RegionAssetSendKey
        {
            get { return regionAssetSendKey; }
            set { regionAssetSendKey = value; }
        }
        public string RegionAssetRecvKey
        {
            get { return regionAssetRecvKey; }
            set { regionAssetRecvKey = value; }
        }
        public string RegionUserURI
        {
            get { return regionUserURI; }
            set { regionUserURI = value; }
        }
        public string RegionUserSendKey
        {
            get { return regionUserSendKey; }
            set { regionUserSendKey = value; }
        }
        public string RegionUserRecvKey
        {
            get { return regionUserRecvKey; }
            set { regionUserRecvKey = value; }
        }
        public UUID RegionMapTextureID
        {
            get { return regionMapTextureID; }
            set { regionMapTextureID = value; }
        }
        public UUID Owner_uuid
        {
            get { return owner_uuid; }
            set { owner_uuid = value; }
        }
        public UUID OriginUUID
        {
            get { return originUUID; }
            set { originUUID = value; }
        }
        public uint Maturity
        {
            get { return maturity; }
            set { maturity = value; }
        }

        public byte AccessLevel
        {
            get { return Util.ConvertMaturityToAccessLevel(maturity); }
        }


        public RegionInfo ToRegionInfo()
        {
            return RegionInfo.Create(UUID, regionName, regionLocX, regionLocY, serverIP, httpPort, serverPort, remotingPort, serverURI);
        }

        public static RegionProfileData FromRegionInfo(RegionInfo regionInfo)
        {
            if (regionInfo == null)
            {
                return null;
            }

            return Create(regionInfo.RegionID, regionInfo.RegionName, regionInfo.RegionLocX,
                          regionInfo.RegionLocY, regionInfo.ExternalHostName,
                          (uint) regionInfo.ExternalEndPoint.Port, regionInfo.HttpPort, regionInfo.RemotingPort,
                          regionInfo.ServerURI, regionInfo.AccessLevel);
        }

        public static RegionProfileData Create(UUID regionID, string regionName, uint locX, uint locY, string externalHostName, uint regionPort, uint httpPort, uint remotingPort, string serverUri, byte access)
        {
            RegionProfileData regionProfile;
            regionProfile = new RegionProfileData();
            regionProfile.regionLocX = locX;
            regionProfile.regionLocY = locY;
            regionProfile.regionHandle =
                Utils.UIntsToLong((regionProfile.regionLocX * Constants.RegionSize),
                                  (regionProfile.regionLocY*Constants.RegionSize));
            regionProfile.serverIP = externalHostName;
            regionProfile.serverPort = regionPort;
            regionProfile.httpPort = httpPort;
            regionProfile.remotingPort = remotingPort;
            regionProfile.serverURI = serverUri;
            regionProfile.httpServerURI = "http://" + externalHostName + ":" + httpPort + "/";
            regionProfile.UUID = regionID;
            regionProfile.regionName = regionName;
            regionProfile.maturity = access;
            return regionProfile;
        }
    }
}
