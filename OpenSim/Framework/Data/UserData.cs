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
using libsecondlife;

namespace OpenSim.Framework.Data
{
    /// <summary>
    /// An interface for connecting to user storage servers.
    /// </summary>
    public interface IUserData
    {
        /// <summary>
        /// Returns a user profile from a database via their UUID
        /// </summary>
        /// <param name="user">The accounts UUID</param>
        /// <returns>The user data profile</returns>
        UserProfileData getUserByUUID(LLUUID user);

        /// <summary>
        /// Returns a users profile by searching their username
        /// </summary>
        /// <param name="name">The users username</param>
        /// <returns>The user data profile</returns>
        UserProfileData getUserByName(string name);

        /// <summary>
        /// Returns a users profile by searching their username parts
        /// </summary>
        /// <param name="fname">Account firstname</param>
        /// <param name="lname">Account lastname</param>
        /// <returns>The user data profile</returns>
        UserProfileData getUserByName(string fname, string lname);

        /// <summary>
        /// Returns the current agent for a user searching by it's UUID
        /// </summary>
        /// <param name="user">The users UUID</param>
        /// <returns>The current agent session</returns>
        UserAgentData getAgentByUUID(LLUUID user);

        /// <summary>
        /// Returns the current session agent for a user searching by username
        /// </summary>
        /// <param name="name">The users account name</param>
        /// <returns>The current agent session</returns>
        UserAgentData getAgentByName(string name);

        /// <summary>
        /// Returns the current session agent for a user searching by username parts
        /// </summary>
        /// <param name="fname">The users first account name</param>
        /// <param name="lname">The users account surname</param>
        /// <returns>The current agent session</returns>
        UserAgentData getAgentByName(string fname, string lname);

        /// <summary>
        /// Adds a new User profile to the database 
        /// </summary>
        /// <param name="user">UserProfile to add</param>
        void addNewUserProfile(UserProfileData user);

        /// <summary>
        /// Adds a new agent to the database
        /// </summary>
        /// <param name="agent">The agent to add</param>
        void addNewUserAgent(UserAgentData agent);

        /// <summary>
        /// Attempts to move currency units between accounts (NOT RELIABLE / TRUSTWORTHY. DONT TRY RUN YOUR OWN CURRENCY EXCHANGE WITH REAL VALUES)
        /// </summary>
        /// <param name="from">The account to transfer from</param>
        /// <param name="to">The account to transfer to</param>
        /// <param name="amount">The amount to transfer</param>
        /// <returns>Successful?</returns>
        bool moneyTransferRequest(LLUUID from, LLUUID to, uint amount);

        /// <summary>
        /// Attempts to move inventory between accounts, if inventory is copyable it will be copied into the target account.
        /// </summary>
        /// <param name="from">User to transfer from</param>
        /// <param name="to">User to transfer to</param>
        /// <param name="inventory">Specified inventory item</param>
        /// <returns>Successful?</returns>
        bool inventoryTransferRequest(LLUUID from, LLUUID to, LLUUID inventory);

        /// <summary>
        /// Returns the plugin version
        /// </summary>
        /// <returns>Plugin version in MAJOR.MINOR.REVISION.BUILD format</returns>
        string getVersion();

        /// <summary>
        /// Returns the plugin name
        /// </summary>
        /// <returns>Plugin name, eg MySQL User Provider</returns>
        string getName();

        /// <summary>
        /// Initialises the plugin (artificial constructor)
        /// </summary>
        void Initialise();
    }
}
