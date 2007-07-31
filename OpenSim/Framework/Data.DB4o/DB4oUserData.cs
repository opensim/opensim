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
using libsecondlife;

namespace OpenSim.Framework.Data.DB4o
{
    /// <summary>
    /// A User storage interface for the DB4o database system
    /// </summary>
    public class DB4oUserData : IUserData
    {
        /// <summary>
        /// The database manager
        /// </summary>
        DB4oUserManager manager;

        /// <summary>
        /// Artificial constructor called upon plugin load
        /// </summary>
        public void Initialise()
        {
            manager = new DB4oUserManager("userprofiles.yap");
        }

        /// <summary>
        /// Loads a specified user profile from a UUID
        /// </summary>
        /// <param name="uuid">The users UUID</param>
        /// <returns>A user profile</returns>
        public UserProfileData getUserByUUID(LLUUID uuid)
        {
            if(manager.userProfiles.ContainsKey(uuid))
                return manager.userProfiles[uuid];
            return null;
        }

        /// <summary>
        /// Returns a user by searching for its name
        /// </summary>
        /// <param name="name">The users account name</param>
        /// <returns>A matching users profile</returns>
        public UserProfileData getUserByName(string name)
        {
            return getUserByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        /// <summary>
        /// Returns a user by searching for its name
        /// </summary>
        /// <param name="fname">The first part of the users account name</param>
        /// <param name="lname">The second part of the users account name</param>
        /// <returns>A matching users profile</returns>
        public UserProfileData getUserByName(string fname, string lname)
        {
            foreach (UserProfileData profile in manager.userProfiles.Values)
            {
                if (profile.username == fname && profile.surname == lname)
                    return profile;
            }
            return null;
        }

        /// <summary>
        /// Returns a user by UUID direct
        /// </summary>
        /// <param name="uuid">The users account ID</param>
        /// <returns>A matching users profile</returns>
        public UserAgentData getAgentByUUID(LLUUID uuid)
        {   
            try
            {
                return getUserByUUID(uuid).currentAgent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Returns a session by account name
        /// </summary>
        /// <param name="name">The account name</param>
        /// <returns>The users session agent</returns>
        public UserAgentData getAgentByName(string name)
        {
            return getAgentByName(name.Split(' ')[0], name.Split(' ')[1]);
        }

        /// <summary>
        /// Returns a session by account name
        /// </summary>
        /// <param name="fname">The first part of the users account name</param>
        /// <param name="lname">The second part of the users account name</param>
        /// <returns>A user agent</returns>
        public UserAgentData getAgentByName(string fname, string lname)
        {
            try
            {
                return getUserByName(fname,lname).currentAgent;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Creates a new user profile
        /// </summary>
        /// <param name="user">The profile to add to the database</param>
        public void addNewUserProfile(UserProfileData user)
        {
            try
            {
                manager.UpdateRecord(user);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
      
        /// <summary>
        /// Creates a new user profile
        /// </summary>
        /// <param name="user">The profile to add to the database</param>
        /// <returns>True on success, false on error</returns>
        public bool updateUserProfile(UserProfileData user)
        {
            try {
                return manager.UpdateRecord(user);
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

      

        /// <summary>
        /// Creates a new user agent
        /// </summary>
        /// <param name="agent">The agent to add to the database</param>
        public void addNewUserAgent(UserAgentData agent)
        {
            // Do nothing. yet.
        }

        /// <summary>
        /// Transfers money between two user accounts
        /// </summary>
        /// <param name="from">Starting account</param>
        /// <param name="to">End account</param>
        /// <param name="amount">The amount to move</param>
        /// <returns>Success?</returns>
        public bool moneyTransferRequest(LLUUID from, LLUUID to, uint amount)
        {
            return true;
        }

        /// <summary>
        /// Transfers inventory between two accounts
        /// </summary>
        /// <remarks>Move to inventory server</remarks>
        /// <param name="from">Senders account</param>
        /// <param name="to">Recievers account</param>
        /// <param name="item">Inventory item</param>
        /// <returns>Success?</returns>
        public bool inventoryTransferRequest(LLUUID from, LLUUID to, LLUUID item)
        {
            return true;
        }

        /// <summary>
        /// Returns the name of the storage provider
        /// </summary>
        /// <returns>Storage provider name</returns>
        public string getName()
        {
            return "DB4o Userdata";
        }

        /// <summary>
        /// Returns the version of the storage provider
        /// </summary>
        /// <returns>Storage provider version</returns>
        public string getVersion()
        {
            return "0.1";
        }
    }
}
