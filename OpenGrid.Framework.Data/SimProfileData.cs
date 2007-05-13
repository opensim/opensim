using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGrid.Framework.Data
{
    public class SimProfileData
    {
        /// <summary>
        /// The name of the region
        /// </summary>
        public string regionName = "";

        /// <summary>
        /// A 64-bit number combining map position into a (mostly) unique ID
        /// </summary>
        public ulong regionHandle;

        /// <summary>
        /// OGS/OpenSim Specific ID for a region
        /// </summary>
        public libsecondlife.LLUUID UUID;

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
        public string regionSendKey = "";
        public string regionRecvKey = "";
        public string regionSecret = "";

        /// <summary>
        /// Whether the region is online
        /// </summary>
        public bool regionOnline;

        /// <summary>
        /// Information about the server that the region is currently hosted on
        /// </summary>
        public string serverIP = "";
        public uint serverPort;
        public string serverURI = "";

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
        public string regionDataURI = "";

        /// <summary>
        /// Region Asset Details
        /// </summary>
        public string regionAssetURI = "";
        public string regionAssetSendKey = "";
        public string regionAssetRecvKey = "";

        /// <summary>
        /// Region Userserver Details
        /// </summary>
        public string regionUserURI = "";
        public string regionUserSendKey = "";
        public string regionUserRecvKey = "";
    }
}
