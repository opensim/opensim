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
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// Information about a particular user known to the userserver
    /// </summary>
    public class UserProfileData
    {
        /// <summary>
        /// A UNIX Timestamp (seconds since epoch) for the users creation
        /// </summary>
        private int m_created;

        /// <summary>
        /// The users last registered agent (filled in on the user server)
        /// </summary>
        private UserAgentData m_currentAgent;

        /// <summary>
        /// The first component of a users account name
        /// </summary>
        private string m_firstname;

        /// <summary>
        /// The coordinates inside the region of the home location
        /// </summary>
        private Vector3 m_homeLocation;

        /// <summary>
        /// Where the user will be looking when they rez.
        /// </summary>
        private Vector3 m_homeLookAt;

        private uint m_homeRegionX;
        private uint m_homeRegionY;

        /// <summary>
        /// The ID value for this user
        /// </summary>
        private UUID m_id;

        /// <summary>
        /// A UNIX Timestamp for the users last login date / time
        /// </summary>
        private int m_lastLogin;

        /// <summary>
        /// A salted hash containing the users password, in the format md5(md5(password) + ":" + salt)
        /// </summary>
        /// <remarks>This is double MD5'd because the client sends an unsalted MD5 to the loginserver</remarks>
        private string m_passwordHash;

        /// <summary>
        /// The salt used for the users hash, should be 32 bytes or longer
        /// </summary>
        private string m_passwordSalt;

        /// <summary>
        /// The about text listed in a users profile.
        /// </summary>
        private string m_profileAboutText = String.Empty;

        /// <summary>
        /// A uint mask containing the "I can do" fields of the users profile
        /// </summary>
        private uint m_profileCanDoMask;

        /// <summary>
        /// The profile image for the users first life tab
        /// </summary>
        private UUID m_profileFirstImage;

        /// <summary>
        /// The first life about text listed in a users profile
        /// </summary>
        private string m_profileFirstText = String.Empty;

        /// <summary>
        /// The profile image for an avatar stored on the asset server
        /// </summary>
        private UUID m_profileImage;

        /// <summary>
        /// A uint mask containing the "I want to do" part of the users profile
        /// </summary>
        private uint m_profileWantDoMask; // Profile window "I want to" mask

        /// <summary>
        /// The profile url for an avatar
        /// </summary>
        private string m_profileUrl;

        /// <summary>
        /// The second component of a users account name
        /// </summary>
        private string m_surname;

        /// <summary>
        /// A valid email address for the account.  Useful for password reset requests.
        /// </summary>
        private string m_email = String.Empty;

        /// <summary>
        /// A URI to the users asset server, used for foreigners and large grids.
        /// </summary>
        private string m_userAssetUri = String.Empty;

        /// <summary>
        /// A URI to the users inventory server, used for foreigners and large grids
        /// </summary>
        private string m_userInventoryUri = String.Empty;

        /// <summary>
        /// The last used Web_login_key
        /// </summary>
        private UUID m_webLoginKey;

        // Data for estates and other goodies
        // to get away from per-machine configs a little
        //
        private int m_userFlags;
        private int m_godLevel;
        private string m_customType;
        private UUID m_partner;

        /// <summary>
        /// The regionhandle of the users preferred home region. If
        /// multiple sims occupy the same spot, the grid may decide
        /// which region the user logs into
        /// </summary>
        public virtual ulong HomeRegion
        {
            get
            {
                return Util.RegionWorldLocToHandle(Util.RegionToWorldLoc(m_homeRegionX), Util.RegionToWorldLoc(m_homeRegionY));
                // return Utils.UIntsToLong( m_homeRegionX * (uint)Constants.RegionSize, m_homeRegionY * (uint)Constants.RegionSize);
            }

            set
            {
                uint regionWorldLocX, regionWorldLocY;
                Util.RegionHandleToWorldLoc(value, out regionWorldLocX, out regionWorldLocY);
                m_homeRegionX = Util.WorldToRegionLoc(regionWorldLocX);
                m_homeRegionY = Util.WorldToRegionLoc(regionWorldLocY);
                // m_homeRegionX = (uint) (value >> 40);
                // m_homeRegionY = (((uint) (value)) >> 8);
            }
        }

        private UUID m_homeRegionId;
        /// <summary>
        /// The regionID of the users home region. This is unique;
        /// even if the position of the region changes within the
        /// grid, this will refer to the same region.
        /// </summary>
        public UUID HomeRegionID
        {
            get { return m_homeRegionId; }
            set { m_homeRegionId = value; }
        }

        // Property wrappers
        public UUID ID
        {
            get { return m_id; }
            set { m_id = value; }
        }

        public UUID WebLoginKey
        {
            get { return m_webLoginKey; }
            set { m_webLoginKey = value; }
        }

        public string FirstName
        {
            get { return m_firstname; }
            set { m_firstname = value; }
        }

        public string SurName
        {
            get { return m_surname; }
            set { m_surname = value; }
        }

        /// <value>
        /// The concatentation of the various name components.
        /// </value>
        public string Name
        {
            get { return String.Format("{0} {1}", m_firstname, m_surname); }
        }

        public string Email
        {
            get { return m_email; }
            set { m_email = value; }
        }

        public string PasswordHash
        {
            get { return m_passwordHash; }
            set { m_passwordHash = value; }
        }

        public string PasswordSalt
        {
            get { return m_passwordSalt; }
            set { m_passwordSalt = value; }
        }

        public uint HomeRegionX
        {
            get { return m_homeRegionX; }
            set { m_homeRegionX = value; }
        }

        public uint HomeRegionY
        {
            get { return m_homeRegionY; }
            set { m_homeRegionY = value; }
        }

        public Vector3 HomeLocation
        {
            get { return m_homeLocation; }
            set { m_homeLocation = value; }
        }

        // for handy serialization
        public float HomeLocationX
        {
            get { return m_homeLocation.X; }
            set { m_homeLocation.X = value; }
        }

        public float HomeLocationY
        {
            get { return m_homeLocation.Y; }
            set { m_homeLocation.Y = value; }
        }

        public float HomeLocationZ
        {
            get { return m_homeLocation.Z; }
            set { m_homeLocation.Z = value; }
        }


        public Vector3 HomeLookAt
        {
            get { return m_homeLookAt; }
            set { m_homeLookAt = value; }
        }

        // for handy serialization
        public float HomeLookAtX
        {
            get { return m_homeLookAt.X; }
            set { m_homeLookAt.X = value; }
        }

        public float HomeLookAtY
        {
            get { return m_homeLookAt.Y; }
            set { m_homeLookAt.Y = value; }
        }

        public float HomeLookAtZ
        {
            get { return m_homeLookAt.Z; }
            set { m_homeLookAt.Z = value; }
        }

        public int Created
        {
            get { return m_created; }
            set { m_created = value; }
        }

        public int LastLogin
        {
            get { return m_lastLogin; }
            set { m_lastLogin = value; }
        }

        public string UserInventoryURI
        {
            get { return m_userInventoryUri; }
            set { m_userInventoryUri = value; }
        }

        public string UserAssetURI
        {
            get { return m_userAssetUri; }
            set { m_userAssetUri = value; }
        }

        public uint CanDoMask
        {
            get { return m_profileCanDoMask; }
            set { m_profileCanDoMask = value; }
        }

        public uint WantDoMask
        {
            get { return m_profileWantDoMask; }
            set { m_profileWantDoMask = value; }
        }

        public string AboutText
        {
            get { return m_profileAboutText; }
            set { m_profileAboutText = value; }
        }

        public string FirstLifeAboutText
        {
            get { return m_profileFirstText; }
            set { m_profileFirstText = value; }
        }

        public string ProfileUrl
        {
            get { return m_profileUrl; }
            set { m_profileUrl = value; }
        }

        public UUID Image
        {
            get { return m_profileImage; }
            set { m_profileImage = value; }
        }

        public UUID FirstLifeImage
        {
            get { return m_profileFirstImage; }
            set { m_profileFirstImage = value; }
        }

        public UserAgentData CurrentAgent
        {
            get { return m_currentAgent; }
            set { m_currentAgent = value; }
        }

        public int UserFlags
        {
            get { return m_userFlags; }
            set { m_userFlags = value; }
        }

        public int GodLevel
        {
            get { return m_godLevel; }
            set { m_godLevel = value; }
        }

        public string CustomType
        {
            get { return m_customType; }
            set { m_customType = value; }
        }

        public UUID Partner
        {
            get { return m_partner; }
            set { m_partner = value; }
        }
    }
}
