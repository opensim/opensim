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
 *     * Neither the name of the OpenSim Project nor the
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
using libsecondlife;

namespace OpenSim.Framework
{
    /// <summary>
    /// Information about a particular user known to the userserver
    /// </summary>
    public class UserProfileData
    {
        /// <summary>
        /// The ID value for this user
        /// </summary>
        public LLUUID UUID;

        /// <summary>
        /// The last used Web_login_key
        /// </summary>
        public LLUUID webLoginKey;
        /// <summary>
        /// The first component of a users account name
        /// </summary>
        public string username;

        /// <summary>
        /// The second component of a users account name
        /// </summary>
        public string surname;

        /// <summary>
        /// A salted hash containing the users password, in the format md5(md5(password) + ":" + salt)
        /// </summary>
        /// <remarks>This is double MD5'd because the client sends an unsalted MD5 to the loginserver</remarks>
        public string passwordHash;

        /// <summary>
        /// The salt used for the users hash, should be 32 bytes or longer
        /// </summary>
        public string passwordSalt;

        /// <summary>
        /// The regionhandle of the users preffered home region. If multiple sims occupy the same spot, the grid may decide which region the user logs into
        /// </summary>
        public ulong homeRegion
        {
            get { return Helpers.UIntsToLong((homeRegionX * (uint)Constants.RegionSize), (homeRegionY * (uint)Constants.RegionSize)); }
            set
            {
                homeRegionX = (uint) (value >> 40);
                homeRegionY = (((uint) (value)) >> 8);
            }
        }

        public uint homeRegionX;
        public uint homeRegionY;

        /// <summary>
        /// The coordinates inside the region of the home location
        /// </summary>
        public LLVector3 homeLocation;

        /// <summary>
        /// Where the user will be looking when they rez.
        /// </summary>
        public LLVector3 homeLookAt;

        /// <summary>
        /// A UNIX Timestamp (seconds since epoch) for the users creation
        /// </summary>
        public int created;

        /// <summary>
        /// A UNIX Timestamp for the users last login date / time
        /// </summary>
        public int lastLogin;

        public LLUUID rootInventoryFolderID;

        /// <summary>
        /// A URI to the users inventory server, used for foreigners and large grids
        /// </summary>
        public string userInventoryURI = String.Empty;

        /// <summary>
        /// A URI to the users asset server, used for foreigners and large grids.
        /// </summary>
        public string userAssetURI = String.Empty;

        /// <summary>
        /// A uint mask containing the "I can do" fields of the users profile
        /// </summary>
        public uint profileCanDoMask;

        /// <summary>
        /// A uint mask containing the "I want to do" part of the users profile
        /// </summary>
        public uint profileWantDoMask; // Profile window "I want to" mask

        /// <summary>
        /// The about text listed in a users profile.
        /// </summary>
        public string profileAboutText = String.Empty;

        /// <summary>
        /// The first life about text listed in a users profile
        /// </summary>
        public string profileFirstText = String.Empty;

        /// <summary>
        /// The profile image for an avatar stored on the asset server
        /// </summary>
        public LLUUID profileImage;

        /// <summary>
        /// The profile image for the users first life tab
        /// </summary>
        public LLUUID profileFirstImage;

        /// <summary>
        /// The users last registered agent (filled in on the user server)
        /// </summary>
        public UserAgentData currentAgent;
    }
   
    /// <summary>
    /// Information about a users session
    /// </summary>
    public class UserAgentData
    {
        /// <summary>
        /// The UUID of the users avatar (not the agent!)
        /// </summary>
        public LLUUID UUID;

        /// <summary>
        /// The IP address of the user
        /// </summary>
        public string agentIP = String.Empty;

        /// <summary>
        /// The port of the user
        /// </summary>
        public uint agentPort;

        /// <summary>
        /// Is the user online?
        /// </summary>
        public bool agentOnline;

        /// <summary>
        /// The session ID for the user (also the agent ID)
        /// </summary>
        public LLUUID sessionID;

        /// <summary>
        /// The "secure" session ID for the user
        /// </summary>
        /// <remarks>Not very secure. Dont rely on it for anything more than Linden Lab does.</remarks>
        public LLUUID secureSessionID;

        /// <summary>
        /// The region the user logged into initially
        /// </summary>
        public LLUUID regionID;

        /// <summary>
        /// A unix timestamp from when the user logged in
        /// </summary>
        public int loginTime;

        /// <summary>
        /// When this agent expired and logged out, 0 if still online
        /// </summary>
        public int logoutTime;

        /// <summary>
        /// Current region the user is logged into
        /// </summary>
        public LLUUID currentRegion;

        /// <summary>
        /// Region handle of the current region the user is in
        /// </summary>
        public ulong currentHandle;

        /// <summary>
        /// The position of the user within the region
        /// </summary>
        public LLVector3 currentPos;
    }
}
