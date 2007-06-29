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
using OpenSim.Framework.Data;

namespace OpenSim.Framework.Data.MySQL
{
    /// <summary>
    /// A MySQL Interface for the Grid Server
    /// </summary>
    public class MySQLGridData : IGridData
    {
        /// <summary>
        /// MySQL Database Manager
        /// </summary>
        private MySQLManager database;

        /// <summary>
        /// Initialises the Grid Interface
        /// </summary>
        public void Initialise()
        {
            IniFile GridDataMySqlFile = new IniFile("mysql_connection.ini");
            string settingHostname = GridDataMySqlFile.ParseFileReadValue("hostname");
            string settingDatabase = GridDataMySqlFile.ParseFileReadValue("database");
            string settingUsername = GridDataMySqlFile.ParseFileReadValue("username");
            string settingPassword = GridDataMySqlFile.ParseFileReadValue("password");
            string settingPooling = GridDataMySqlFile.ParseFileReadValue("pooling");
            string settingPort = GridDataMySqlFile.ParseFileReadValue("port");

            database = new MySQLManager(settingHostname, settingDatabase, settingUsername, settingPassword, settingPooling, settingPort);
        }

        /// <summary>
        /// Shuts down the grid interface
        /// </summary>
        public void Close()
        {
            database.Close();
        }

        /// <summary>
        /// Returns the plugin name
        /// </summary>
        /// <returns>Plugin name</returns>
        public string getName()
        {
            return "MySql OpenGridData";
        }

        /// <summary>
        /// Returns the plugin version
        /// </summary>
        /// <returns>Plugin version</returns>
        public string getVersion()
        {
            return "0.1";
        }

        /// <summary>
        /// Returns all the specified region profiles within coordates -- coordinates are inclusive
        /// </summary>
        /// <param name="xmin">Minimum X coordinate</param>
        /// <param name="ymin">Minimum Y coordinate</param>
        /// <param name="xmax">Maximum X coordinate</param>
        /// <param name="ymax">Maximum Y coordinate</param>
        /// <returns></returns>
        public SimProfileData[] GetProfilesInRange(uint xmin, uint ymin, uint xmax, uint ymax)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?xmin"] = xmin.ToString();
                    param["?ymin"] = ymin.ToString();
                    param["?xmax"] = xmax.ToString();
                    param["?ymax"] = ymax.ToString();

                    System.Data.IDbCommand result = database.Query("SELECT * FROM regions WHERE locX >= ?xmin AND locX <= ?xmax AND locY >= ?ymin AND locY <= ?ymax", param);
                    System.Data.IDataReader reader = result.ExecuteReader();

                    SimProfileData row;

                    List<SimProfileData> rows = new List<SimProfileData>();

                    while ((row = database.readSimRow(reader)) != null)
                    {
                        rows.Add(row);
                    }
                    reader.Close();
                    result.Dispose();

                    return rows.ToArray();

                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns a sim profile from it's location
        /// </summary>
        /// <param name="handle">Region location handle</param>
        /// <returns>Sim profile</returns>
        public SimProfileData GetProfileByHandle(ulong handle)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?handle"] = handle.ToString();

                    System.Data.IDbCommand result = database.Query("SELECT * FROM regions WHERE regionHandle = ?handle", param);
                    System.Data.IDataReader reader = result.ExecuteReader();

                    SimProfileData row = database.readSimRow(reader);
                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Returns a sim profile from it's UUID
        /// </summary>
        /// <param name="uuid">The region UUID</param>
        /// <returns>The sim profile</returns>
        public SimProfileData GetProfileByLLUUID(libsecondlife.LLUUID uuid)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?uuid"] = uuid.ToStringHyphenated();

                    System.Data.IDbCommand result = database.Query("SELECT * FROM regions WHERE uuid = ?uuid", param);
                    System.Data.IDataReader reader = result.ExecuteReader();

                    SimProfileData row = database.readSimRow(reader);
                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                Console.WriteLine(e.ToString());
                return null;
            }
        }

        /// <summary>
        /// Adds a new profile to the database
        /// </summary>
        /// <param name="profile">The profile to add</param>
        /// <returns>Successful?</returns>
        public DataResponse AddProfile(SimProfileData profile)
        {
            lock (database)
            {
                if (database.insertRegion(profile))
                {
                    return DataResponse.RESPONSE_OK;
                }
                else
                {
                    return DataResponse.RESPONSE_ERROR;
                }
            }
        }

        /// <summary>
        /// DEPRECIATED. Attempts to authenticate a region by comparing a shared secret.
        /// </summary>
        /// <param name="uuid">The UUID of the challenger</param>
        /// <param name="handle">The attempted regionHandle of the challenger</param>
        /// <param name="authkey">The secret</param>
        /// <returns>Whether the secret and regionhandle match the database entry for UUID</returns>
        public bool AuthenticateSim(libsecondlife.LLUUID uuid, ulong handle, string authkey)
        {
            bool throwHissyFit = false; // Should be true by 1.0

            if (throwHissyFit)
                throw new Exception("CRYPTOWEAK AUTHENTICATE: Refusing to authenticate due to replay potential.");

            SimProfileData data = GetProfileByLLUUID(uuid);

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
        public bool AuthenticateSim(libsecondlife.LLUUID uuid, ulong handle, string authhash, string challenge)
        {
            System.Security.Cryptography.SHA512Managed HashProvider = new System.Security.Cryptography.SHA512Managed();
            System.Text.ASCIIEncoding TextProvider = new ASCIIEncoding();

            byte[] stream = TextProvider.GetBytes(uuid.ToStringHyphenated() + ":" + handle.ToString() + ":" + challenge);
            byte[] hash = HashProvider.ComputeHash(stream);

            return false;
        }

        public ReservationData GetReservationAtPoint(uint x, uint y)
        {
            try
            {
                lock (database)
                {
                    Dictionary<string, string> param = new Dictionary<string, string>();
                    param["?x"] = x.ToString();
                    param["?y"] = y.ToString();
                    System.Data.IDbCommand result = database.Query("SELECT * FROM reservations WHERE resXMin <= ?x AND resXMax >= ?x AND resYMin <= ?y AND resYMax >= ?y", param);
                    System.Data.IDataReader reader = result.ExecuteReader();

                    ReservationData row = database.readReservationRow(reader);
                    reader.Close();
                    result.Dispose();

                    return row;
                }
            }
            catch (Exception e)
            {
                database.Reconnect();
                Console.WriteLine(e.ToString());
                return null;
            }
        }
    }


}
