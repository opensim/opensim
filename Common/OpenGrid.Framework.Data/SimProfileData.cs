/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGrid.Framework.Data
{
    /// <summary>
    /// A class which contains information known to the grid server about a region
    /// </summary>
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

        /// <summary>
        /// Region Map Texture Asset
        /// </summary>
        public libsecondlife.LLUUID regionMapTextureID = new libsecondlife.LLUUID("00000000-0000-0000-9999-000000000006");
    }
}
