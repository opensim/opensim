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
* 
*/
using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Data.MSSQL
{
    /// <summary>
    /// A grid data interface for Microsoft SQL Server
    /// </summary>
    public class SqlGridData : IGridData
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Database manager
        /// </summary>
        private MSSQLManager database;

        /// <summary>
        /// Initialises the Grid Interface
        /// </summary>
        public void Initialise()
        {
            IniFile GridDataMySqlFile = new IniFile("mssql_connection.ini");
            string settingDataSource = GridDataMySqlFile.ParseFileReadValue("data_source");
            string settingInitialCatalog = GridDataMySqlFile.ParseFileReadValue("initial_catalog");
            string settingPersistSecurityInfo = GridDataMySqlFile.ParseFileReadValue("persist_security_info");
            string settingUserId = GridDataMySqlFile.ParseFileReadValue("user_id");
            string settingPassword = GridDataMySqlFile.ParseFileReadValue("password");

            database =
                new MSSQLManager(settingDataSource, settingInitialCatalog, settingPersistSecurityInfo, settingUserId,
                                 settingPassword);
        }

        /// <summary>
        /// Shuts down the grid interface
        /// </summary>
        public void Close()
        {
            database.Close();
        }

        /// <summary>
        /// Returns the storage system name
        /// </summary>
        /// <returns>A string containing the storage system name</returns>
        public string getName()
        {
            return "Sql OpenGridData";
        }

        /// <summary>
        /// Returns the storage system version
        /// </summary>
        /// <returns>A string containing the storage system version</returns>
        public string getVersion()
        {
            return "0.1";
        }

        /// <summary>
        /// Returns a list of regions within the specified ranges
        /// </summary>
        /// <param name="a">minimum X coordinate</param>
        /// <param name="b">minimum Y coordinate</param>
        /// <param name="c">maximum X coordinate</param>
        /// <param name="d">maximum Y coordinate</param>
        /// <returns>An array of region profiles</returns>
        public RegionProfileData[] GetProfilesInRange(uint a, uint b, uint c, uint d)
        {
            return null;
        }

        /// <summary>
        /// Returns a sim profile from it's location
        /// </summary>
        /// <param name="handle">Region location handle</param>
        /// <returns>Sim profile</returns>
        public RegionProfileData GetProfileByHandle(ulong handle)
        {
            IDataReader reader = null;
            try
            {
                Dictionary<string, string> param = new Dictionary<string, string>();
                param["handle"] = handle.ToString();
                IDbCommand result = database.Query("SELECT * FROM regions WHERE regionHandle = @handle", param);
                reader = result.ExecuteReader();

                RegionProfileData row = database.getRegionRow(reader);
                reader.Close();
                result.Dispose();

                return row;
            }
            catch (Exception)
            {
                if (reader != null)
                {
                    reader.Close();
                }
            }
            return null;
        }

        /// <summary>
        /// // Returns a list of avatar and UUIDs that match the query
        /// </summary>
        public List<AvatarPickerAvatar> GeneratePickerResults(LLUUID queryID, string query)
        {
            List<AvatarPickerAvatar> returnlist = new List<AvatarPickerAvatar>();
            string[] querysplit;
            querysplit = query.Split(' ');
            if (querysplit.Length == 2)
            {
                try
                {
                    lock (database)
                    {
                        Dictionary<string, string> param = new Dictionary<string, string>();
                        param["first"] = querysplit[0];
                        param["second"] = querysplit[1];

                        IDbCommand result =
                            database.Query(
                                "SELECT UUID,username,surname FROM users WHERE username = @first AND lastname = @second",
                                param);
                        IDataReader reader = result.ExecuteReader();


                        while (reader.Read())
                        {
                            AvatarPickerAvatar user = new AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string) reader["UUID"]);
                            user.firstName = (string) reader["username"];
                            user.lastName = (string) reader["surname"];
                            returnlist.Add(user);
                        }
                        reader.Close();
                        result.Dispose();
                    }
                }
                catch (Exception e)
                {
                    database.Reconnect();
                    m_log.Error(e.ToString());
                    return returnlist;
                }
            }
            else if (querysplit.Length == 1)
            {
                try
                {
                    lock (database)
                    {
                        Dictionary<string, string> param = new Dictionary<string, string>();
                        param["first"] = querysplit[0];
                        param["second"] = querysplit[1];

                        IDbCommand result =
                            database.Query(
                                "SELECT UUID,username,surname FROM users WHERE username = @first OR lastname = @second",
                                param);
                        IDataReader reader = result.ExecuteReader();


                        while (reader.Read())
                        {
                            AvatarPickerAvatar user = new AvatarPickerAvatar();
                            user.AvatarID = new LLUUID((string) reader["UUID"]);
                            user.firstName = (string) reader["username"];
                            user.lastName = (string) reader["surname"];
                            returnlist.Add(user);
                        }
                        reader.Close();
                        result.Dispose();
                    }
                }
                catch (Exception e)
                {
                    database.Reconnect();
                    m_log.Error(e.ToString());
                    return returnlist;
                }
            }
            return returnlist;
        }

        /// <summary>
        /// Returns a sim profile from it's UUID
        /// </summary>
        /// <param name="uuid">The region UUID</param>
        /// <returns>The sim profile</returns>
        public RegionProfileData GetProfileByLLUUID(LLUUID uuid)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["uuid"] = uuid.ToString();
            IDbCommand result = database.Query("SELECT * FROM regions WHERE uuid = @uuid", param);
            IDataReader reader = result.ExecuteReader();

            RegionProfileData row = database.getRegionRow(reader);
            reader.Close();
            result.Dispose();

            return row;
        }

        /// <summary>
        /// Adds a new specified region to the database
        /// </summary>
        /// <param name="profile">The profile to add</param>
        /// <returns>A dataresponse enum indicating success</returns>
        public DataResponse AddProfile(RegionProfileData profile)
        {
            try
            {
                if (GetProfileByLLUUID(profile.UUID) != null)
                {
                    return DataResponse.RESPONSE_OK;
                }
            }
            catch (Exception)
            {
                System.Console.WriteLine("No regions found. Create new one.");
            }

            if (database.insertRegionRow(profile))
            {
                return DataResponse.RESPONSE_OK;
            }
            else
            {
                return DataResponse.RESPONSE_ERROR;
            }
        }

        /// <summary>
        /// DEPRECIATED. Attempts to authenticate a region by comparing a shared secret.
        /// </summary>
        /// <param name="uuid">The UUID of the challenger</param>
        /// <param name="handle">The attempted regionHandle of the challenger</param>
        /// <param name="authkey">The secret</param>
        /// <returns>Whether the secret and regionhandle match the database entry for UUID</returns>
        public bool AuthenticateSim(LLUUID uuid, ulong handle, string authkey)
        {
            bool throwHissyFit = false; // Should be true by 1.0

            if (throwHissyFit)
                throw new Exception("CRYPTOWEAK AUTHENTICATE: Refusing to authenticate due to replay potential.");

            RegionProfileData data = GetProfileByLLUUID(uuid);

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
        public bool AuthenticateSim(LLUUID uuid, ulong handle, string authhash, string challenge)
        {
            SHA512Managed HashProvider = new SHA512Managed();
            ASCIIEncoding TextProvider = new ASCIIEncoding();

            byte[] stream = TextProvider.GetBytes(uuid.ToString() + ":" + handle.ToString() + ":" + challenge);
            byte[] hash = HashProvider.ComputeHash(stream);
            return false;
        }

        public ReservationData GetReservationAtPoint(uint x, uint y)
        {
            return null;
        }
    }
}