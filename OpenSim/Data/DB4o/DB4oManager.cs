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
using System.Collections.Generic;
using Db4objects.Db4o;
using libsecondlife;
using OpenSim.Framework;

namespace OpenSim.Data.DB4o
{
    /// <summary>
    /// A Database manager for Db4o
    /// </summary>
    internal class DB4oGridManager
    {
        /// <summary>
        /// A list of the current regions connected (in-memory cache)
        /// </summary>
        public Dictionary<LLUUID, RegionProfileData> simProfiles = new Dictionary<LLUUID, RegionProfileData>();

        /// <summary>
        /// Database File Name
        /// </summary>
        private string dbfl;

        /// <summary>
        /// Creates a new grid storage manager
        /// </summary>
        /// <param name="db4odb">Filename to the database file</param>
        public DB4oGridManager(string db4odb)
        {
            dbfl = db4odb;
            IObjectContainer database;
            database = Db4oFactory.OpenFile(dbfl);
            IObjectSet result = database.Get(typeof (RegionProfileData));
            // Loads the file into the in-memory cache
            foreach (RegionProfileData row in result)
            {
                simProfiles.Add(row.UUID, row);
            }
            database.Close();
        }

        /// <summary>
        /// Adds a new profile to the database (Warning: Probably slow.)
        /// </summary>
        /// <param name="row">The profile to add</param>
        /// <returns>Successful?</returns>
        public bool AddRow(RegionProfileData row)
        {
            if (simProfiles.ContainsKey(row.UUID))
            {
                simProfiles[row.UUID] = row;
            }
            else
            {
                simProfiles.Add(row.UUID, row);
            }

            try
            {
                IObjectContainer database;
                database = Db4oFactory.OpenFile(dbfl);
                database.Set(row);
                database.Close();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// A manager for the DB4o database (user profiles)
    /// </summary>
    internal class DB4oUserManager
    {
        /// <summary>
        /// A list of the user profiles (in memory cache)
        /// </summary>
        public Dictionary<LLUUID, UserProfileData> userProfiles = new Dictionary<LLUUID, UserProfileData>();

        /// <summary>
        /// Database filename
        /// </summary>
        private string dbfl;

        /// <summary>
        /// Initialises a new DB manager
        /// </summary>
        /// <param name="db4odb">The filename to the database</param>
        public DB4oUserManager(string db4odb)
        {
            dbfl = db4odb;
            IObjectContainer database;
            database = Db4oFactory.OpenFile(dbfl);
            // Load to cache
            IObjectSet result = database.Get(typeof (UserProfileData));
            foreach (UserProfileData row in result)
            {
                if (userProfiles.ContainsKey(row.Id))
                    userProfiles[row.Id] = row;
                else
                    userProfiles.Add(row.Id, row);
            }
            database.Close();
        }

        /// <summary>
        /// Adds or updates a record to the user database.  Do this when changes are needed
        /// in the user profile that need to be persistant.
        /// 
        /// TODO: the logic here is not ACID, the local cache will be
        /// updated even if the persistant data is not.  This may lead
        /// to unexpected results.
        /// </summary>
        /// <param name="record">The profile to update</param>
        /// <returns>true on success, false on fail to persist to db</returns>
        public bool UpdateRecord(UserProfileData record)
        {
            if (userProfiles.ContainsKey(record.Id))
            {
                userProfiles[record.Id] = record;
            }
            else
            {
                userProfiles.Add(record.Id, record);
            }

            try
            {
                IObjectContainer database;
                database = Db4oFactory.OpenFile(dbfl);
                database.Set(record);
                database.Close();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
