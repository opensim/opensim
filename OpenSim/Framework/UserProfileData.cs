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
        private int _created;

        /// <summary>
        /// The users last registered agent (filled in on the user server)
        /// </summary>
        private UserAgentData _currentAgent;

        /// <summary>
        /// The first component of a users account name
        /// </summary>
        private string _firstname;

        /// <summary>
        /// The coordinates inside the region of the home location
        /// </summary>
        private Vector3 _homeLocation;

        /// <summary>
        /// Where the user will be looking when they rez.
        /// </summary>
        private Vector3 _homeLookAt;

        private uint _homeRegionX;
        private uint _homeRegionY;

        /// <summary>
        /// The ID value for this user
        /// </summary>
        private UUID _id;

        /// <summary>
        /// A UNIX Timestamp for the users last login date / time
        /// </summary>
        private int _lastLogin;

        /// <summary>
        /// A salted hash containing the users password, in the format md5(md5(password) + ":" + salt)
        /// </summary>
        /// <remarks>This is double MD5'd because the client sends an unsalted MD5 to the loginserver</remarks>
        private string _passwordHash;

        /// <summary>
        /// The salt used for the users hash, should be 32 bytes or longer
        /// </summary>
        private string _passwordSalt;

        /// <summary>
        /// The about text listed in a users profile.
        /// </summary>
        private string _profileAboutText = String.Empty;

        /// <summary>
        /// A uint mask containing the "I can do" fields of the users profile
        /// </summary>
        private uint _profileCanDoMask;

        /// <summary>
        /// The profile image for the users first life tab
        /// </summary>
        private UUID _profileFirstImage;

        /// <summary>
        /// The first life about text listed in a users profile
        /// </summary>
        private string _profileFirstText = String.Empty;

        /// <summary>
        /// The profile image for an avatar stored on the asset server
        /// </summary>
        private UUID _profileImage;

        /// <summary>
        /// A uint mask containing the "I want to do" part of the users profile
        /// </summary>
        private uint _profileWantDoMask; // Profile window "I want to" mask

        private UUID _rootInventoryFolderID;

        /// <summary>
        /// The second component of a users account name
        /// </summary>
        private string _surname;

        /// <summary>
        /// A valid email address for the account.  Useful for password reset requests.
        /// </summary>
        private string _email = String.Empty;

        /// <summary>
        /// A URI to the users asset server, used for foreigners and large grids.
        /// </summary>
        private string _userAssetURI = String.Empty;

        /// <summary>
        /// A URI to the users inventory server, used for foreigners and large grids
        /// </summary>
        private string _userInventoryURI = String.Empty;

        /// <summary>
        /// The last used Web_login_key
        /// </summary>
        private UUID _webLoginKey;

        // Data for estates and other goodies
        // to get away from per-machine configs a little
        //
        private int _userFlags;
        private int _godLevel;
        private string _customType;
        private UUID _partner;

        /// <summary>
        /// The regionhandle of the users preferred home region. If
        /// multiple sims occupy the same spot, the grid may decide
        /// which region the user logs into
        /// </summary>
        public virtual ulong HomeRegion
        {
            get { return Utils.UIntsToLong((_homeRegionX * (uint)Constants.RegionSize), (_homeRegionY * (uint)Constants.RegionSize)); }
            set
            {
                _homeRegionX = (uint) (value >> 40);
                _homeRegionY = (((uint) (value)) >> 8);
            }
        }

        private UUID _homeRegionID;
        /// <summary>
        /// The regionID of the users home region. This is unique;
        /// even if the position of the region changes within the
        /// grid, this will refer to the same region.
        /// </summary>
        public UUID HomeRegionID
        {
            get { return _homeRegionID; }
            set { _homeRegionID = value; }
        }

        // Property wrappers
        public UUID ID
        {
            get { return _id; }
            set { _id = value; }
        }

        public UUID WebLoginKey
        {
            get { return _webLoginKey; }
            set { _webLoginKey = value; }
        }

        public string FirstName
        {
            get { return _firstname; }
            set { _firstname = value; }
        }

        public string SurName
        {
            get { return _surname; }
            set { _surname = value; }
        }

        public string Email
        {
            get { return _email; }
            set { _email = value; }
        }

        public string PasswordHash
        {
            get { return _passwordHash; }
            set { _passwordHash = value; }
        }

        public string PasswordSalt
        {
            get { return _passwordSalt; }
            set { _passwordSalt = value; }
        }

        public uint HomeRegionX
        {
            get { return _homeRegionX; }
            set { _homeRegionX = value; }
        }

        public uint HomeRegionY
        {
            get { return _homeRegionY; }
            set { _homeRegionY = value; }
        }

        public Vector3 HomeLocation
        {
            get { return _homeLocation; }
            set { _homeLocation = value; }
        }

        // for handy serialization
        public float HomeLocationX
        {
            get { return _homeLocation.X; }
            set { _homeLocation.X = value; }
        }

        public float HomeLocationY
        {
            get { return _homeLocation.Y; }
            set { _homeLocation.Y = value; }
        }

        public float HomeLocationZ
        {
            get { return _homeLocation.Z; }
            set { _homeLocation.Z = value; }
        }


        public Vector3 HomeLookAt
        {
            get { return _homeLookAt; }
            set { _homeLookAt = value; }
        }

        // for handy serialization
        public float HomeLookAtX
        {
            get { return _homeLookAt.X; }
            set { _homeLookAt.X = value; }
        }

        public float HomeLookAtY
        {
            get { return _homeLookAt.Y; }
            set { _homeLookAt.Y = value; }
        }

        public float HomeLookAtZ
        {
            get { return _homeLookAt.Z; }
            set { _homeLookAt.Z = value; }
        }

        public int Created
        {
            get { return _created; }
            set { _created = value; }
        }

        public int LastLogin
        {
            get { return _lastLogin; }
            set { _lastLogin = value; }
        }

        public UUID RootInventoryFolderID
        {
            get { return _rootInventoryFolderID; }
            set { _rootInventoryFolderID = value; }
        }

        public string UserInventoryURI
        {
            get { return _userInventoryURI; }
            set { _userInventoryURI = value; }
        }

        public string UserAssetURI
        {
            get { return _userAssetURI; }
            set { _userAssetURI = value; }
        }

        public uint CanDoMask
        {
            get { return _profileCanDoMask; }
            set { _profileCanDoMask = value; }
        }

        public uint WantDoMask
        {
            get { return _profileWantDoMask; }
            set { _profileWantDoMask = value; }
        }

        public string AboutText
        {
            get { return _profileAboutText; }
            set { _profileAboutText = value; }
        }

        public string FirstLifeAboutText
        {
            get { return _profileFirstText; }
            set { _profileFirstText = value; }
        }

        public UUID Image
        {
            get { return _profileImage; }
            set { _profileImage = value; }
        }

        public UUID FirstLifeImage
        {
            get { return _profileFirstImage; }
            set { _profileFirstImage = value; }
        }

        public UserAgentData CurrentAgent
        {
            get { return _currentAgent; }
            set { _currentAgent = value; }
        }

        public int UserFlags
        {
            get { return _userFlags; }
            set { _userFlags = value; }
        }

        public int GodLevel
        {
            get { return _godLevel; }
            set { _godLevel = value; }
        }

        public string CustomType
        {
            get { return _customType; }
            set { _customType = value; }
        }

        public UUID Partner
        {
            get { return _partner; }
            set { _partner = value; }
        }
    }
}
