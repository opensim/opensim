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
using System.Data;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using OpenMetaverse;
using log4net;
using OpenSim.Framework;

namespace OpenSim.Data.MSSQL
{
    /// <summary>
    /// A grid data interface for MSSQL Server
    /// </summary>
    public class MSSQLGridData : GridDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Database manager
        /// </summary>
        private MSSQLManager database;

        private string m_regionsTableName;

        override public void Initialise()
        {
            m_log.Info("[MSSQLGridData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        /// <summary>
        /// Initialises the Grid Interface
        /// </summary>
        /// <param name="connect">connect string</param>
        /// <remarks>use mssql_connection.ini</remarks>
        override public void Initialise(string connect)
        {
            // TODO: make the connect string actually do something
            IniFile iniFile = new IniFile("mssql_connection.ini");

            string settingDataSource = iniFile.ParseFileReadValue("data_source");
            string settingInitialCatalog = iniFile.ParseFileReadValue("initial_catalog");
            string settingPersistSecurityInfo = iniFile.ParseFileReadValue("persist_security_info");
            string settingUserId = iniFile.ParseFileReadValue("user_id");
            string settingPassword = iniFile.ParseFileReadValue("password");

            m_regionsTableName = iniFile.ParseFileReadValue("regionstablename");
            if (m_regionsTableName == null)
            {
                m_regionsTableName = "regions";
            }

            database =
                new MSSQLManager(settingDataSource, settingInitialCatalog, settingPersistSecurityInfo, settingUserId,
                                 settingPassword);

            TestTables();
        }

        /// <summary>
        ///
        /// </summary>
        private void TestTables()
        {
            using (IDbCommand cmd = database.Query("SELECT TOP 1 * FROM " + m_regionsTableName, new Dictionary<string, string>()))
            {
                try
                {
                    cmd.ExecuteNonQuery();
                }
                catch (Exception)
                {
                    m_log.Info("[GRID DB]: MSSQL Database doesn't exist... creating");
                    database.ExecuteResourceSql("Mssql-regions.sql");
                }
            }
        }

        /// <summary>
        /// Shuts down the grid interface
        /// </summary>
        override public void Dispose()
        {
            // nothing to close
        }

        /// <summary>
        /// The name of this DB provider.
        /// </summary>
        /// <returns>A string containing the storage system name</returns>
        override public string Name
        {
            get { return "Sql OpenGridData"; }
        }

        /// <summary>
        /// Database provider version.
        /// </summary>
        /// <returns>A string containing the storage system version</returns>
        override public string Version
        {
            get { return "0.1"; }
        }

        /// <summary>
        /// NOT IMPLEMENTED,
        /// Returns a list of regions within the specified ranges
        /// </summary>
        /// <param name="a">minimum X coordinate</param>
        /// <param name="b">minimum Y coordinate</param>
        /// <param name="c">maximum X coordinate</param>
        /// <param name="d">maximum Y coordinate</param>
        /// <returns>null</returns>
        /// <remarks>always return null</remarks>
        override public RegionProfileData[] GetProfilesInRange(uint a, uint b, uint c, uint d)
        {
            return null;
        }

        /// <summary>
        /// Returns a sim profile from its location
        /// </summary>
        /// <param name="handle">Region location handle</param>
        /// <returns>Sim profile</returns>
        override public RegionProfileData GetProfileByHandle(ulong handle)
        {

            Dictionary<string, string> param = new Dictionary<string, string>();
            param["handle"] = handle.ToString();

            try
            {
                using (IDbCommand result = database.Query("SELECT * FROM " + m_regionsTableName + " WHERE regionHandle = @handle", param))
                using (IDataReader reader = result.ExecuteReader())
                {
                    return database.getRegionRow(reader);
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns a sim profile from its UUID
        /// </summary>
        /// <param name="uuid">The region UUID</param>
        /// <returns>The sim profile</returns>
        override public RegionProfileData GetProfileByUUID(UUID uuid)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["uuid"] = uuid.ToString();

            using (IDbCommand result = database.Query("SELECT * FROM " + m_regionsTableName + " WHERE uuid = @uuid", param))
            using (IDataReader reader = result.ExecuteReader())
            {
                return database.getRegionRow(reader);
            }

        }

        /// <summary>
        /// Returns a sim profile from it's Region name string
        /// </summary>
        /// <param name="uuid">The region name search query</param>
        /// <returns>The sim profile</returns>
        override public RegionProfileData GetProfileByString(string regionName)
        {
            if (regionName.Length > 2)
            {
                try
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    // Add % because this is a like query.
                    param["?regionName"] = regionName + "%";
                    // Order by statement will return shorter matches first.  Only returns one record or no record.
                    using (IDbCommand result = database.Query("SELECT top 1 * FROM " + m_regionsTableName + " WHERE regionName like ?regionName order by regionName", param))
                    using (IDataReader reader = result.ExecuteReader())
                    {
                        return database.getRegionRow(reader);
                    }

                }
                catch (Exception e)
                {
                    m_log.Error(e.ToString());
                    return null;
                }
            }
            else
            {
                m_log.Error("[GRID DB]: Searched for a Region Name shorter then 3 characters");
                return null;
            }
        }

        /// <summary>
        /// Adds a new specified region to the database
        /// </summary>
        /// <param name="profile">The profile to add</param>
        /// <returns>A dataresponse enum indicating success</returns>
        override public DataResponse AddProfile(RegionProfileData profile)
        {
            if (insertRegionRow(profile))
            {
                return DataResponse.RESPONSE_OK;
            }
            else
            {
                return DataResponse.RESPONSE_ERROR;
            }
        }

        /// <summary>
        /// Update the specified region in the database
        /// </summary>
        /// <param name="profile">The profile to update</param>
        /// <returns>A dataresponse enum indicating success</returns>
        public override DataResponse UpdateProfile(RegionProfileData profile)
        {
            if (updateRegionRow(profile))
            {
                return DataResponse.RESPONSE_OK;
            }
            else
            {
                return DataResponse.RESPONSE_ERROR;
            }
        }

        /// <summary>
        /// Update the specified region in the database
        /// </summary>
        /// <param name="profile">The profile to update</param>
        /// <returns>success ?</returns>
        public bool updateRegionRow(RegionProfileData profile)
        {
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
                [serverRemotingPort]=@serverRemotingPort, [owner_uuid]=@owner_uuid
                where [uuid]=@uuid";

            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters["regionHandle"] = profile.regionHandle.ToString();
            parameters["regionName"] = profile.regionName;
            parameters["uuid"] = profile.ToString();
            parameters["regionRecvKey"] = profile.regionRecvKey;
            parameters["regionSecret"] = profile.regionSecret;
            parameters["regionSendKey"] = profile.regionSendKey;
            parameters["regionDataURI"] = profile.regionDataURI;
            parameters["serverIP"] = profile.serverIP;
            parameters["serverPort"] = profile.serverPort.ToString();
            parameters["serverURI"] = profile.serverURI;
            parameters["locX"] = profile.regionLocX.ToString();
            parameters["locY"] = profile.regionLocY.ToString();
            parameters["locZ"] = profile.regionLocZ.ToString();
            parameters["eastOverrideHandle"] = profile.regionEastOverrideHandle.ToString();
            parameters["westOverrideHandle"] = profile.regionWestOverrideHandle.ToString();
            parameters["northOverrideHandle"] = profile.regionNorthOverrideHandle.ToString();
            parameters["southOverrideHandle"] = profile.regionSouthOverrideHandle.ToString();
            parameters["regionAssetURI"] = profile.regionAssetURI;
            parameters["regionAssetRecvKey"] = profile.regionAssetRecvKey;
            parameters["regionAssetSendKey"] = profile.regionAssetSendKey;
            parameters["regionUserURI"] = profile.regionUserURI;
            parameters["regionUserRecvKey"] = profile.regionUserRecvKey;
            parameters["regionUserSendKey"] = profile.regionUserSendKey;
            parameters["regionMapTexture"] = profile.regionMapTextureID.ToString();
            parameters["serverHttpPort"] = profile.httpPort.ToString();
            parameters["serverRemotingPort"] = profile.remotingPort.ToString();
            parameters["owner_uuid"] = profile.owner_uuid.ToString();

            bool returnval = false;

            try
            {
                using (IDbCommand result = database.Query(sql, parameters))
                {

                    if (result.ExecuteNonQuery() == 1)
                        returnval = true;

                }
            }
            catch (Exception e)
            {
                m_log.Error("MSSQLManager : " + e.ToString());
            }

            return returnval;
        }
        /// <summary>
        /// Creates a new region in the database
        /// </summary>
        /// <param name="profile">The region profile to insert</param>
        /// <returns>Successful?</returns>
        public bool insertRegionRow(RegionProfileData profile)
        {
            //Insert new region
            string sql =
                "INSERT INTO " + m_regionsTableName + @" ([regionHandle], [regionName], [uuid], [regionRecvKey], [regionSecret], [regionSendKey], [regionDataURI], 
                                                      [serverIP], [serverPort], [serverURI], [locX], [locY], [locZ], [eastOverrideHandle], [westOverrideHandle], 
                                                      [southOverrideHandle], [northOverrideHandle], [regionAssetURI], [regionAssetRecvKey], [regionAssetSendKey], 
                                                      [regionUserURI], [regionUserRecvKey], [regionUserSendKey], [regionMapTexture], [serverHttpPort], 
                                                      [serverRemotingPort], [owner_uuid]) 
                                                VALUES (@regionHandle, @regionName, @uuid, @regionRecvKey, @regionSecret, @regionSendKey, @regionDataURI, 
                                                        @serverIP, @serverPort, @serverURI, @locX, @locY, @locZ, @eastOverrideHandle, @westOverrideHandle, 
                                                        @southOverrideHandle, @northOverrideHandle, @regionAssetURI, @regionAssetRecvKey, @regionAssetSendKey, 
                                                        @regionUserURI, @regionUserRecvKey, @regionUserSendKey, @regionMapTexture, @serverHttpPort, @serverRemotingPort, @owner_uuid);";

            Dictionary<string, string> parameters = new Dictionary<string, string>();

            parameters["regionHandle"] = profile.regionHandle.ToString();
            parameters["regionName"] = profile.regionName;
            parameters["uuid"] = profile.ToString();
            parameters["regionRecvKey"] = profile.regionRecvKey;
            parameters["regionSecret"] = profile.regionSecret;
            parameters["regionSendKey"] = profile.regionSendKey;
            parameters["regionDataURI"] = profile.regionDataURI;
            parameters["serverIP"] = profile.serverIP;
            parameters["serverPort"] = profile.serverPort.ToString();
            parameters["serverURI"] = profile.serverURI;
            parameters["locX"] = profile.regionLocX.ToString();
            parameters["locY"] = profile.regionLocY.ToString();
            parameters["locZ"] = profile.regionLocZ.ToString();
            parameters["eastOverrideHandle"] = profile.regionEastOverrideHandle.ToString();
            parameters["westOverrideHandle"] = profile.regionWestOverrideHandle.ToString();
            parameters["northOverrideHandle"] = profile.regionNorthOverrideHandle.ToString();
            parameters["southOverrideHandle"] = profile.regionSouthOverrideHandle.ToString();
            parameters["regionAssetURI"] = profile.regionAssetURI;
            parameters["regionAssetRecvKey"] = profile.regionAssetRecvKey;
            parameters["regionAssetSendKey"] = profile.regionAssetSendKey;
            parameters["regionUserURI"] = profile.regionUserURI;
            parameters["regionUserRecvKey"] = profile.regionUserRecvKey;
            parameters["regionUserSendKey"] = profile.regionUserSendKey;
            parameters["regionMapTexture"] = profile.regionMapTextureID.ToString();
            parameters["serverHttpPort"] = profile.httpPort.ToString();
            parameters["serverRemotingPort"] = profile.remotingPort.ToString();
            parameters["owner_uuid"] = profile.owner_uuid.ToString();

            bool returnval = false;

            try
            {
                using (IDbCommand result = database.Query(sql, parameters))
                {
                    if (result.ExecuteNonQuery() == 1)
                        returnval = true;
                }
            }
            catch (Exception e)
            {
                m_log.Error("[GRID DB]: " + e.ToString());
            }

            return returnval;
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
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>null</returns>
        override public ReservationData GetReservationAtPoint(uint x, uint y)
        {
            return null;
        }
    }
}
