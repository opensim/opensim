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
using System.Data.SqlClient;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.MSSQL
{
    /// <summary>
    /// A grid data interface for MSSQL Server
    /// </summary>
    public class MSSQLGridData : GridDataBase
    {
        private const string _migrationStore = "GridStore";

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Database manager
        /// </summary>
        private MSSQLManager database;
        private string m_connectionString;

        private string m_regionsTableName = "regions";

        #region IPlugin Members

        // [Obsolete("Cannot be default-initialized!")]
        override public void Initialise()
        {
            m_log.Info("[GRID DB]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException(Name);
        }

        /// <summary>
        /// Initialises the Grid Interface
        /// </summary>
        /// <param name="connectionString">connect string</param>
        /// <remarks>use mssql_connection.ini</remarks>
        override public void Initialise(string connectionString)
        {
            m_connectionString = connectionString;
            database = new MSSQLManager(connectionString);
            
            //New migrations check of store
            database.CheckMigration(_migrationStore);
        }

        /// <summary>
        /// Shuts down the grid interface
        /// </summary>
        override public void Dispose()
        {
            database = null;
        }

        /// <summary>
        /// The name of this DB provider.
        /// </summary>
        /// <returns>A string containing the storage system name</returns>
        override public string Name
        {
            get { return "MSSQL OpenGridData"; }
        }

        /// <summary>
        /// Database provider version.
        /// </summary>
        /// <returns>A string containing the storage system version</returns>
        override public string Version
        {
            get { return "0.1"; }
        }

        #endregion

        #region Public override GridDataBase methods

        /// <summary>
        /// Returns a list of regions within the specified ranges
        /// </summary>
        /// <param name="xmin">minimum X coordinate</param>
        /// <param name="ymin">minimum Y coordinate</param>
        /// <param name="xmax">maximum X coordinate</param>
        /// <param name="ymax">maximum Y coordinate</param>
        /// <returns>null</returns>
        /// <remarks>always return null</remarks>
        override public RegionProfileData[] GetProfilesInRange(uint xmin, uint ymin, uint xmax, uint ymax)
        {
            string sql = "SELECT * FROM regions WHERE locX >= @xmin AND locX <= @xmax AND locY >= @ymin AND locY <= @ymax";
            using (SqlConnection conn = new SqlConnection(m_connectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(database.CreateParameter("xmin", xmin));
                cmd.Parameters.Add(database.CreateParameter("ymin", ymin));
                cmd.Parameters.Add(database.CreateParameter("xmax", xmax));
                cmd.Parameters.Add(database.CreateParameter("ymax", ymax));

                List<RegionProfileData> rows = new List<RegionProfileData>();
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(ReadSimRow(reader));
                    }
                }

                if (rows.Count > 0)
                {
                    return rows.ToArray();
                }
            }
            m_log.Info("[GRID DB] : Found no regions within range.");
            return null;
        }

        
        /// <summary>
        /// Returns up to maxNum profiles of regions that have a name starting with namePrefix
        /// </summary>
        /// <param name="namePrefix">The name to match against</param>
        /// <param name="maxNum">Maximum number of profiles to return</param>
        /// <returns>A list of sim profiles</returns>
        override public List<RegionProfileData> GetRegionsByName (string namePrefix, uint maxNum)
        {
            string sql = "SELECT * FROM regions WHERE regionName LIKE @name";
            using (SqlConnection conn = new SqlConnection(m_connectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(database.CreateParameter("name", namePrefix + "%"));

                List<RegionProfileData> rows = new List<RegionProfileData>();
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (rows.Count < maxNum && reader.Read())
                    {
                        rows.Add(ReadSimRow(reader));
                    }
                }

                return rows;
            }
        }

        /// <summary>
        /// Returns a sim profile from its location
        /// </summary>
        /// <param name="handle">Region location handle</param>
        /// <returns>Sim profile</returns>
        override public RegionProfileData GetProfileByHandle(ulong handle)
        {
            string sql = "SELECT * FROM " + m_regionsTableName + " WHERE regionHandle = @handle";
            using (SqlConnection conn = new SqlConnection(m_connectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))            
            {
                cmd.Parameters.Add(database.CreateParameter("handle", handle));
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return ReadSimRow(reader);
                    }
                }
            }
            m_log.InfoFormat("[GRID DB] : No region found with handle : {0}", handle);
            return null;
        }

        /// <summary>
        /// Returns a sim profile from its UUID
        /// </summary>
        /// <param name="uuid">The region UUID</param>
        /// <returns>The sim profile</returns>
        override public RegionProfileData GetProfileByUUID(UUID uuid)
        {
            string sql = "SELECT * FROM " + m_regionsTableName + " WHERE uuid = @uuid";
            using (SqlConnection conn = new SqlConnection(m_connectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn)) 
            {
                cmd.Parameters.Add(database.CreateParameter("uuid", uuid));
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return ReadSimRow(reader);
                    }
                }
            }
            m_log.InfoFormat("[GRID DB] : No region found with UUID : {0}", uuid);
            return null;
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
                string sql = "SELECT top 1 * FROM " + m_regionsTableName + " WHERE regionName like @regionName order by regionName";

                using (SqlConnection conn = new SqlConnection(m_connectionString))
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add(database.CreateParameter("regionName", regionName + "%"));
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return ReadSimRow(reader);
                        }
                    }
                }
                m_log.InfoFormat("[GRID DB] : No region found with regionName : {0}", regionName);
                return null;
            }

            m_log.Error("[GRID DB]: Searched for a Region Name shorter then 3 characters");
            return null;
        }

        /// <summary>
        /// Adds a new specified region to the database
        /// </summary>
        /// <param name="profile">The profile to add</param>
        /// <returns>A dataresponse enum indicating success</returns>
        override public DataResponse StoreProfile(RegionProfileData profile)
        {
            if (GetProfileByUUID(profile.UUID) == null)
            {
                if (InsertRegionRow(profile))
                {
                    return DataResponse.RESPONSE_OK;
                }
            }
            else
            {
                if (UpdateRegionRow(profile))
                {
                    return DataResponse.RESPONSE_OK;
                }
            }

            return DataResponse.RESPONSE_ERROR;
        }

        /// <summary>
        /// Deletes a sim profile from the database
        /// </summary>
        /// <param name="uuid">the sim UUID</param>
        /// <returns>Successful?</returns>
        //public DataResponse DeleteProfile(RegionProfileData profile)
        override public DataResponse DeleteProfile(string uuid)
        {
            string sql = "DELETE FROM regions WHERE uuid = @uuid;";

            using (SqlConnection conn = new SqlConnection(m_connectionString))
            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add(database.CreateParameter("uuid", uuid));
                try
                {
                    conn.Open();
                    cmd.ExecuteNonQuery();
                    return DataResponse.RESPONSE_OK;
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[GRID DB] : Error deleting region info, error is : {0}", e.Message);
                    return DataResponse.RESPONSE_ERROR;
                }
            }
        }

        #endregion

        #region Methods that are not used or deprecated (still needed because of base class)

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
        /// WHEN IS THIS GONNA BE IMPLEMENTED.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>null</returns>
        override public ReservationData GetReservationAtPoint(uint x, uint y)
        {
            return null;
        }

        #endregion

        #region private methods

        /// <summary>
        /// Reads a region row from a database reader
        /// </summary>
        /// <param name="reader">An active database reader</param>
        /// <returns>A region profile</returns>
        private static RegionProfileData ReadSimRow(IDataRecord reader)
        {
            RegionProfileData retval = new RegionProfileData();

            // Region Main gotta-have-or-we-return-null parts
            UInt64 tmp64;
            if (!UInt64.TryParse(reader["regionHandle"].ToString(), out tmp64))
            {
                return null;
            }

            retval.regionHandle = tmp64;

//            UUID tmp_uuid;
//            if (!UUID.TryParse((string)reader["uuid"], out tmp_uuid))
//            {
//                return null;
//            }

            retval.UUID = new UUID((Guid)reader["uuid"]); // tmp_uuid;

            // non-critical parts
            retval.regionName = reader["regionName"].ToString();
            retval.originUUID = new UUID((Guid)reader["originUUID"]);

            // Secrets
            retval.regionRecvKey = reader["regionRecvKey"].ToString();
            retval.regionSecret = reader["regionSecret"].ToString();
            retval.regionSendKey = reader["regionSendKey"].ToString();

            // Region Server
            retval.regionDataURI = reader["regionDataURI"].ToString();
            retval.regionOnline = false; // Needs to be pinged before this can be set.
            retval.serverIP = reader["serverIP"].ToString();
            retval.serverPort = Convert.ToUInt32(reader["serverPort"]);
            retval.serverURI = reader["serverURI"].ToString();
            retval.httpPort = Convert.ToUInt32(reader["serverHttpPort"].ToString());
            retval.remotingPort = Convert.ToUInt32(reader["serverRemotingPort"].ToString());

            // Location
            retval.regionLocX = Convert.ToUInt32(reader["locX"].ToString());
            retval.regionLocY = Convert.ToUInt32(reader["locY"].ToString());
            retval.regionLocZ = Convert.ToUInt32(reader["locZ"].ToString());

            // Neighbours - 0 = No Override
            retval.regionEastOverrideHandle = Convert.ToUInt64(reader["eastOverrideHandle"].ToString());
            retval.regionWestOverrideHandle = Convert.ToUInt64(reader["westOverrideHandle"].ToString());
            retval.regionSouthOverrideHandle = Convert.ToUInt64(reader["southOverrideHandle"].ToString());
            retval.regionNorthOverrideHandle = Convert.ToUInt64(reader["northOverrideHandle"].ToString());

            // Assets
            retval.regionAssetURI = reader["regionAssetURI"].ToString();
            retval.regionAssetRecvKey = reader["regionAssetRecvKey"].ToString();
            retval.regionAssetSendKey = reader["regionAssetSendKey"].ToString();

            // Userserver
            retval.regionUserURI = reader["regionUserURI"].ToString();
            retval.regionUserRecvKey = reader["regionUserRecvKey"].ToString();
            retval.regionUserSendKey = reader["regionUserSendKey"].ToString();

            // World Map Addition
            retval.regionMapTextureID = new UUID((Guid)reader["regionMapTexture"]);
            retval.owner_uuid = new UUID((Guid)reader["owner_uuid"]);
            retval.maturity = Convert.ToUInt32(reader["access"]);
            return retval;
        }

        /// <summary>
        /// Update the specified region in the database
        /// </summary>
        /// <param name="profile">The profile to update</param>
        /// <returns>success ?</returns>
        private bool UpdateRegionRow(RegionProfileData profile)
        {
            bool returnval = false;

            //Insert new region
            string sql =
                "UPDATE " + m_regionsTableName + @" SET
                [regionHandle]=@regionHandle, [regionName]=@regionName,
                [regionRecvKey]=@regionRecvKey, [regionSecret]=@regionSecret, [regionSendKey]=@regionSendKey,
                [regionDataURI]=@regionDataURI, [serverIP]=@serverIP, [serverPort]=@serverPort, [serverURI]=@serverURI,
                [locX]=@locX, [locY]=@locY, [locZ]=@locZ, [eastOverrideHandle]=@eastOverrideHandle,
                [westOverrideHandle]=@westOverrideHandle, [southOverrideHandle]=@southOverrideHandle,
                [northOverrideHandle]=@northOverrideHandle, [regionAssetURI]=@regionAssetURI,
                [regionAssetRecvKey]=@regionAssetRecvKey, [regionAssetSendKey]=@regionAssetSendKey,
                [regionUserURI]=@regionUserURI, [regionUserRecvKey]=@regionUserRecvKey, [regionUserSendKey]=@regionUserSendKey,
                [regionMapTexture]=@regionMapTexture, [serverHttpPort]=@serverHttpPort,
                [serverRemotingPort]=@serverRemotingPort, [owner_uuid]=@owner_uuid , [originUUID]=@originUUID
                where [uuid]=@uuid";

            using (SqlConnection conn = new SqlConnection(m_connectionString))
            using (SqlCommand command = new SqlCommand(sql, conn))
            {
                command.Parameters.Add(database.CreateParameter("regionHandle", profile.regionHandle));
                command.Parameters.Add(database.CreateParameter("regionName", profile.regionName));
                command.Parameters.Add(database.CreateParameter("uuid", profile.UUID));
                command.Parameters.Add(database.CreateParameter("regionRecvKey", profile.regionRecvKey));
                command.Parameters.Add(database.CreateParameter("regionSecret", profile.regionSecret));
                command.Parameters.Add(database.CreateParameter("regionSendKey", profile.regionSendKey));
                command.Parameters.Add(database.CreateParameter("regionDataURI", profile.regionDataURI));
                command.Parameters.Add(database.CreateParameter("serverIP", profile.serverIP));
                command.Parameters.Add(database.CreateParameter("serverPort", profile.serverPort));
                command.Parameters.Add(database.CreateParameter("serverURI", profile.serverURI));
                command.Parameters.Add(database.CreateParameter("locX", profile.regionLocX));
                command.Parameters.Add(database.CreateParameter("locY", profile.regionLocY));
                command.Parameters.Add(database.CreateParameter("locZ", profile.regionLocZ));
                command.Parameters.Add(database.CreateParameter("eastOverrideHandle", profile.regionEastOverrideHandle));
                command.Parameters.Add(database.CreateParameter("westOverrideHandle", profile.regionWestOverrideHandle));
                command.Parameters.Add(database.CreateParameter("northOverrideHandle", profile.regionNorthOverrideHandle));
                command.Parameters.Add(database.CreateParameter("southOverrideHandle", profile.regionSouthOverrideHandle));
                command.Parameters.Add(database.CreateParameter("regionAssetURI", profile.regionAssetURI));
                command.Parameters.Add(database.CreateParameter("regionAssetRecvKey", profile.regionAssetRecvKey));
                command.Parameters.Add(database.CreateParameter("regionAssetSendKey", profile.regionAssetSendKey));
                command.Parameters.Add(database.CreateParameter("regionUserURI", profile.regionUserURI));
                command.Parameters.Add(database.CreateParameter("regionUserRecvKey", profile.regionUserRecvKey));
                command.Parameters.Add(database.CreateParameter("regionUserSendKey", profile.regionUserSendKey));
                command.Parameters.Add(database.CreateParameter("regionMapTexture", profile.regionMapTextureID));
                command.Parameters.Add(database.CreateParameter("serverHttpPort", profile.httpPort));
                command.Parameters.Add(database.CreateParameter("serverRemotingPort", profile.remotingPort));
                command.Parameters.Add(database.CreateParameter("owner_uuid", profile.owner_uuid));
                command.Parameters.Add(database.CreateParameter("originUUID", profile.originUUID));
                conn.Open();
                try
                {
                    command.ExecuteNonQuery();
                    returnval = true;
                }
                catch (Exception e)
                {
                    m_log.Error("[GRID DB] : Error updating region, error: " + e.Message);
                }
            }

            return returnval;
        }

        /// <summary>
        /// Creates a new region in the database
        /// </summary>
        /// <param name="profile">The region profile to insert</param>
        /// <returns>Successful?</returns>
        private bool InsertRegionRow(RegionProfileData profile)
        {
            bool returnval = false;

            //Insert new region
            string sql =
                "INSERT INTO " + m_regionsTableName + @" ([regionHandle], [regionName], [uuid], [regionRecvKey], [regionSecret], [regionSendKey], [regionDataURI], 
                                                      [serverIP], [serverPort], [serverURI], [locX], [locY], [locZ], [eastOverrideHandle], [westOverrideHandle], 
                                                      [southOverrideHandle], [northOverrideHandle], [regionAssetURI], [regionAssetRecvKey], [regionAssetSendKey], 
                                                      [regionUserURI], [regionUserRecvKey], [regionUserSendKey], [regionMapTexture], [serverHttpPort], 
                                                      [serverRemotingPort], [owner_uuid], [originUUID], [access]) 
                                                VALUES (@regionHandle, @regionName, @uuid, @regionRecvKey, @regionSecret, @regionSendKey, @regionDataURI, 
                                                        @serverIP, @serverPort, @serverURI, @locX, @locY, @locZ, @eastOverrideHandle, @westOverrideHandle, 
                                                        @southOverrideHandle, @northOverrideHandle, @regionAssetURI, @regionAssetRecvKey, @regionAssetSendKey, 
                                                        @regionUserURI, @regionUserRecvKey, @regionUserSendKey, @regionMapTexture, @serverHttpPort, @serverRemotingPort, @owner_uuid, @originUUID, @access);";

            using (SqlConnection conn = new SqlConnection(m_connectionString))
            using (SqlCommand command = new SqlCommand(sql, conn))
            {
                command.Parameters.Add(database.CreateParameter("regionHandle", profile.regionHandle));
                command.Parameters.Add(database.CreateParameter("regionName", profile.regionName));
                command.Parameters.Add(database.CreateParameter("uuid", profile.UUID));
                command.Parameters.Add(database.CreateParameter("regionRecvKey", profile.regionRecvKey));
                command.Parameters.Add(database.CreateParameter("regionSecret", profile.regionSecret));
                command.Parameters.Add(database.CreateParameter("regionSendKey", profile.regionSendKey));
                command.Parameters.Add(database.CreateParameter("regionDataURI", profile.regionDataURI));
                command.Parameters.Add(database.CreateParameter("serverIP", profile.serverIP));
                command.Parameters.Add(database.CreateParameter("serverPort", profile.serverPort));
                command.Parameters.Add(database.CreateParameter("serverURI", profile.serverURI));
                command.Parameters.Add(database.CreateParameter("locX", profile.regionLocX));
                command.Parameters.Add(database.CreateParameter("locY", profile.regionLocY));
                command.Parameters.Add(database.CreateParameter("locZ", profile.regionLocZ));
                command.Parameters.Add(database.CreateParameter("eastOverrideHandle", profile.regionEastOverrideHandle));
                command.Parameters.Add(database.CreateParameter("westOverrideHandle", profile.regionWestOverrideHandle));
                command.Parameters.Add(database.CreateParameter("northOverrideHandle", profile.regionNorthOverrideHandle));
                command.Parameters.Add(database.CreateParameter("southOverrideHandle", profile.regionSouthOverrideHandle));
                command.Parameters.Add(database.CreateParameter("regionAssetURI", profile.regionAssetURI));
                command.Parameters.Add(database.CreateParameter("regionAssetRecvKey", profile.regionAssetRecvKey));
                command.Parameters.Add(database.CreateParameter("regionAssetSendKey", profile.regionAssetSendKey));
                command.Parameters.Add(database.CreateParameter("regionUserURI", profile.regionUserURI));
                command.Parameters.Add(database.CreateParameter("regionUserRecvKey", profile.regionUserRecvKey));
                command.Parameters.Add(database.CreateParameter("regionUserSendKey", profile.regionUserSendKey));
                command.Parameters.Add(database.CreateParameter("regionMapTexture", profile.regionMapTextureID));
                command.Parameters.Add(database.CreateParameter("serverHttpPort", profile.httpPort));
                command.Parameters.Add(database.CreateParameter("serverRemotingPort", profile.remotingPort));
                command.Parameters.Add(database.CreateParameter("owner_uuid", profile.owner_uuid));
                command.Parameters.Add(database.CreateParameter("originUUID", profile.originUUID));
                command.Parameters.Add(database.CreateParameter("access", profile.maturity));
                conn.Open();
                try
                {
                    command.ExecuteNonQuery();
                    returnval = true;
                }
                catch (Exception e)
                {
                    m_log.Error("[GRID DB] : Error inserting region, error: " + e.Message);
                }
            }

            return returnval;
        }

        #endregion
    }
}
