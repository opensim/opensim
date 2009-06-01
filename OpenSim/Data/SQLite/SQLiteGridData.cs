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
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.SQLite
{
    /// <summary>
    /// A Grid Interface to the SQLite database
    /// </summary>
    public class SQLiteGridData : GridDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// SQLite database manager
        /// </summary>
        private SQLiteManager database;

        override public void Initialise() 
        { 
            m_log.Info("[SQLite]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        /// <summary>
        /// <list type="bullet">
        /// <item>Initialises Inventory interface</item>
        /// <item>Loads and initialises a new SQLite connection and maintains it.</item>
        /// <item>use default URI if connect string is empty.</item>
        /// </list>
        /// </summary>
        /// <param name="dbconnect">connect string</param>
        override public void Initialise(string connect)
        {
            database = new SQLiteManager(connect);
        }

        /// <summary>
        /// Shuts down the grid interface
        /// </summary>
        override public void Dispose()
        {
            database.Close();
        }

        /// <summary>
        /// Returns the name of this grid interface
        /// </summary>
        /// <returns>A string containing the grid interface</returns>
        override public string Name
        {
            get { return "SQLite OpenGridData"; }
        }

        /// <summary>
        /// Returns the version of this grid interface
        /// </summary>
        /// <returns>A string containing the version</returns>
        override public string Version
        {
            get { return "0.1"; }
        }

        /// <summary>
        /// Returns a list of regions within the specified ranges
        /// </summary>
        /// <param name="a">minimum X coordinate</param>
        /// <param name="b">minimum Y coordinate</param>
        /// <param name="c">maximum X coordinate</param>
        /// <param name="d">maximum Y coordinate</param>
        /// <returns>An array of region profiles</returns>
        /// <remarks>NOT IMPLEMENTED ? always return null</remarks>
        override public RegionProfileData[] GetProfilesInRange(uint a, uint b, uint c, uint d)
        {
            return null;
        }

        
        /// <summary>
        /// Returns up to maxNum profiles of regions that have a name starting with namePrefix
        /// </summary>
        /// <param name="name">The name to match against</param>
        /// <param name="maxNum">Maximum number of profiles to return</param>
        /// <returns>A list of sim profiles</returns>
        override public List<RegionProfileData> GetRegionsByName (string namePrefix, uint maxNum)
        {
            return null;
        }

        /// <summary>
        /// Returns a sim profile from it's handle
        /// </summary>
        /// <param name="handle">Region location handle</param>
        /// <returns>Sim profile</returns>
        override public RegionProfileData GetProfileByHandle(ulong handle)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["handle"] = handle.ToString();

            IDbCommand result = database.Query("SELECT * FROM regions WHERE handle = @handle", param);
            IDataReader reader = result.ExecuteReader();

            RegionProfileData row = database.getRow(reader);
            reader.Close();
            result.Dispose();

            return row;
        }

        /// <summary>
        /// Returns a sim profile from it's Region name string
        /// </summary>
        /// <param name="regionName">The region name search query</param>
        /// <returns>The sim profile</returns>
        override public RegionProfileData GetProfileByString(string regionName)
        {
            if (regionName.Length > 2)
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                // Add % because this is a like query.
                param["?regionName"] = regionName + "%";
                // Only returns one record or no record.
                IDbCommand result = database.Query("SELECT * FROM regions WHERE regionName like ?regionName LIMIT 1", param);
                IDataReader reader = result.ExecuteReader();

                RegionProfileData row = database.getRow(reader);
                reader.Close();
                result.Dispose();

                return row;
            }
            else
            {
                //m_log.Error("[DATABASE]: Searched for a Region Name shorter then 3 characters");
                return null;
            }
        }

        /// <summary>
        /// Returns a sim profile from it's UUID
        /// </summary>
        /// <param name="uuid">The region UUID</param>
        /// <returns>The sim profile</returns>
        override public RegionProfileData GetProfileByUUID(UUID uuid)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["uuid"] = uuid.ToString();

            IDbCommand result = database.Query("SELECT * FROM regions WHERE uuid = @uuid", param);
            IDataReader reader = result.ExecuteReader();

            RegionProfileData row = database.getRow(reader);
            reader.Close();
            result.Dispose();

            return row;
        }

        /// <summary>
        /// Returns a list of avatar and UUIDs that match the query
        /// </summary>
        /// <remarks>do nothing yet</remarks>
        public List<AvatarPickerAvatar> GeneratePickerResults(UUID queryID, string query)
        {
            //Do nothing yet
            List<AvatarPickerAvatar> returnlist = new List<AvatarPickerAvatar>();
            return returnlist;
        }

        /// <summary>
        /// Adds a new specified region to the database
        /// </summary>
        /// <param name="profile">The profile to add</param>
        /// <returns>A dataresponse enum indicating success</returns>
        override public DataResponse AddProfile(RegionProfileData profile)
        {
            if (database.insertRow(profile))
            {
                return DataResponse.RESPONSE_OK;
            }
            else
            {
                return DataResponse.RESPONSE_ERROR;
            }
        }

        override public DataResponse UpdateProfile(RegionProfileData profile)
        {
            return AddProfile(profile);
        }

                /// <summary>
        /// Deletes a sim profile from the database
        /// </summary>
        /// <param name="uuid">the sim UUID</param>
        /// <returns>Successful?</returns>
        //public DataResponse DeleteProfile(RegionProfileData profile)
        override public DataResponse DeleteProfile(string uuid)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["uuid"] = uuid;

            IDbCommand result = database.Query("DELETE FROM regions WHERE uuid = @uuid", param);
            if (result.ExecuteNonQuery() > 0)
            {
                return DataResponse.RESPONSE_OK;
            }
            return DataResponse.RESPONSE_ERROR;
        }

        /// <summary>
        /// DEPRECATED. Attempts to authenticate a region by comparing a shared secret.
        /// </summary>
        /// <param name="uuid">The UUID of the challenger</param>
        /// <param name="handle">The attempted regionHandle of the challenger</param>
        /// <param name="authkey">The secret</param>
        /// <returns>Whether the secret and regionhandle match the database entry for UUID</returns>
        override public bool AuthenticateSim(UUID uuid, ulong handle, string authkey)
        {
            bool throwHissyFit = false; // Should be true by 1.0

            if (throwHissyFit)
                throw new Exception("CRYPTOWEAK AUTHENTICATE: Refusing to authenticate due to replay potential.");

            RegionProfileData data = GetProfileByUUID(uuid);

            return (handle == data.regionHandle && authkey == data.regionSecret);
        }

        /// <summary>
        /// NOT YET FUNCTIONAL. Provides a cryptographic authentication of a region
        /// </summary>
        /// <remarks>This requires a security audit.</remarks>
        /// <param name="uuid"></param>
        /// <param name="handle"></param>
        /// <param name="authhash"></param>
        /// <param name="challenge"></param>
        /// <returns></returns>
        public bool AuthenticateSim(UUID uuid, ulong handle, string authhash, string challenge)
        {
            // SHA512Managed HashProvider = new SHA512Managed();
            // Encoding TextProvider = new UTF8Encoding();

            // byte[] stream = TextProvider.GetBytes(uuid.ToString() + ":" + handle.ToString() + ":" + challenge);
            // byte[] hash = HashProvider.ComputeHash(stream);

            return false;
        }

        /// <summary>
        /// NOT IMPLEMENTED
        /// </summary>
        /// <param name="x">x coordinate</param>
        /// <param name="y">y coordinate</param>
        /// <returns>always return null</returns>
        override public ReservationData GetReservationAtPoint(uint x, uint y)
        {
            return null;
        }
    }
}
