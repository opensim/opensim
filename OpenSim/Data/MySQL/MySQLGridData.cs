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
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data.MySQL
{
    /// <summary>
    /// A MySQL Interface for the Grid Server
    /// </summary>
    public class MySQLGridData : GridDataBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// MySQL Database Manager
        /// </summary>
        private MySQLManager database;


        /// <summary>
        /// Better DB manager. Swap-in replacement too.
        /// </summary>
        public Dictionary<int, MySQLSuperManager> m_dbconnections = new Dictionary<int, MySQLSuperManager>();

        public int m_maxConnections = 10;
        public int m_lastConnect;

        public MySQLSuperManager GetLockedConnection()
        {
            int lockedCons = 0;
            while (true)
            {
                m_lastConnect++;

                // Overflow protection
                if (m_lastConnect == int.MaxValue)
                    m_lastConnect = 0;

                MySQLSuperManager x = m_dbconnections[m_lastConnect % m_maxConnections];
                if (!x.Locked)
                {
                    x.GetLock();
                    return x;
                }

                lockedCons++;
                if (lockedCons > m_maxConnections)
                {
                    lockedCons = 0;
                    Thread.Sleep(1000); // Wait some time before searching them again.
                    m_log.Debug(
                        "WARNING: All threads are in use. Probable cause: Something didnt release a mutex properly, or high volume of requests inbound.");
                }
            }
        }


        override public void Initialise()
        {
            m_log.Info("[MySQLGridData]: " + Name + " cannot be default-initialized!");
            throw new PluginNotInitialisedException (Name);
        }

        /// <summary>
        /// <para>Initialises Grid interface</para>
        /// <para>
        /// <list type="bullet">
        /// <item>Loads and initialises the MySQL storage plugin</item>
        /// <item>Warns and uses the obsolete mysql_connection.ini if connect string is empty.</item>
        /// <item>Check for migration</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="connect">connect string.</param>
        override public void Initialise(string connect)
        {
            if (connect != String.Empty)
            {
                database = new MySQLManager(connect);

                m_log.Info("Creating " + m_maxConnections + " DB connections...");
                for (int i = 0; i < m_maxConnections; i++)
                {
                    m_log.Info("Connecting to DB... [" + i + "]");
                    MySQLSuperManager msm = new MySQLSuperManager();
                    msm.Manager = new MySQLManager(connect);
                    m_dbconnections.Add(i, msm);
                }

            }
            else
            {
                m_log.Warn("Using deprecated mysql_connection.ini.  Please update database_connect in GridServer_Config.xml and we'll use that instead");
                IniFile GridDataMySqlFile = new IniFile("mysql_connection.ini");
                string settingHostname = GridDataMySqlFile.ParseFileReadValue("hostname");
                string settingDatabase = GridDataMySqlFile.ParseFileReadValue("database");
                string settingUsername = GridDataMySqlFile.ParseFileReadValue("username");
                string settingPassword = GridDataMySqlFile.ParseFileReadValue("password");
                string settingPooling = GridDataMySqlFile.ParseFileReadValue("pooling");
                string settingPort = GridDataMySqlFile.ParseFileReadValue("port");

                database = new MySQLManager(settingHostname, settingDatabase, settingUsername, settingPassword,
                                            settingPooling, settingPort);

                m_log.Info("Creating " + m_maxConnections + " DB connections...");
                for (int i = 0; i < m_maxConnections; i++)
                {
                    m_log.Info("Connecting to DB... [" + i + "]");
                    MySQLSuperManager msm = new MySQLSuperManager();
                    msm.Manager = new MySQLManager(settingHostname, settingDatabase, settingUsername, settingPassword,
                                                   settingPooling, settingPort);
                    m_dbconnections.Add(i, msm);
                }
            }

            // This actually does the roll forward assembly stuff
            Assembly assem = GetType().Assembly;
            Migration m = new Migration(database.Connection, assem, "GridStore");
            m.Update();
        }

        /// <summary>
        /// Shuts down the grid interface
        /// </summary>
        override public void Dispose()
        {
            database.Close();
        }

        /// <summary>
        /// Returns the plugin name
        /// </summary>
        /// <returns>Plugin name</returns>
        override public string Name
        {
            get { return "MySql OpenGridData"; }
        }

        /// <summary>
        /// Returns the plugin version
        /// </summary>
        /// <returns>Plugin version</returns>
        override public string Version
        {
            get { return "0.1"; }
        }

        /// <summary>
        /// Returns all the specified region profiles within coordates -- coordinates are inclusive
        /// </summary>
        /// <param name="xmin">Minimum X coordinate</param>
        /// <param name="ymin">Minimum Y coordinate</param>
        /// <param name="xmax">Maximum X coordinate</param>
        /// <param name="ymax">Maximum Y coordinate</param>
        /// <returns>Array of sim profiles</returns>
        override public RegionProfileData[] GetProfilesInRange(uint xmin, uint ymin, uint xmax, uint ymax)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                Dictionary<string, object> param = new Dictionary<string, object>();
                    param["?xmin"] = xmin.ToString();
                    param["?ymin"] = ymin.ToString();
                    param["?xmax"] = xmax.ToString();
                    param["?ymax"] = ymax.ToString();

                    using (IDbCommand result = dbm.Manager.Query(
                            "SELECT * FROM regions WHERE locX >= ?xmin AND locX <= ?xmax AND locY >= ?ymin AND locY <= ?ymax",
                            param))
                    {
                        using (IDataReader reader = result.ExecuteReader())
                        {
                            RegionProfileData row;

                            List<RegionProfileData> rows = new List<RegionProfileData>();

                            while ((row = dbm.Manager.readSimRow(reader)) != null)
                                rows.Add(row);

                            return rows.ToArray();
                        }
                    }
            }
            catch (Exception e)
            {
                dbm.Manager.Reconnect();
                m_log.Error(e.Message, e);
                return null;
            }
            finally
            {
                dbm.Release();
            }
        }

        /// <summary>
        /// Returns up to maxNum profiles of regions that have a name starting with namePrefix
        /// </summary>
        /// <param name="name">The name to match against</param>
        /// <param name="maxNum">Maximum number of profiles to return</param>
        /// <returns>A list of sim profiles</returns>
        override public List<RegionProfileData> GetRegionsByName(string namePrefix, uint maxNum)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                Dictionary<string, object> param = new Dictionary<string, object>();
                param["?name"] = namePrefix + "%";

                using (IDbCommand result = dbm.Manager.Query(
                    "SELECT * FROM regions WHERE regionName LIKE ?name",
                    param))
                {
                    using (IDataReader reader = result.ExecuteReader())
                    {
                        RegionProfileData row;

                        List<RegionProfileData> rows = new List<RegionProfileData>();

                        while (rows.Count < maxNum && (row = dbm.Manager.readSimRow(reader)) != null)
                            rows.Add(row);

                        return rows;
                    }
                }
            }
            catch (Exception e)
            {
                dbm.Manager.Reconnect();
                m_log.Error(e.Message, e);
                return null;
            }
            finally
            {
                dbm.Release();
            }
        }

        /// <summary>
        /// Returns a sim profile from it's location
        /// </summary>
        /// <param name="handle">Region location handle</param>
        /// <returns>Sim profile</returns>
        override public RegionProfileData GetProfileByHandle(ulong handle)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                Dictionary<string, object> param = new Dictionary<string, object>();
                param["?handle"] = handle.ToString();

                using (IDbCommand result = dbm.Manager.Query("SELECT * FROM regions WHERE regionHandle = ?handle", param))
                {
                    using (IDataReader reader = result.ExecuteReader())
                    {
                        RegionProfileData row = dbm.Manager.readSimRow(reader);
                        return row;
                    }
                }
            }
            catch (Exception e)
            {
                dbm.Manager.Reconnect();
                m_log.Error(e.Message, e);
                return null;
            }
            finally
            {
                dbm.Release();
            }
        }

        /// <summary>
        /// Returns a sim profile from it's UUID
        /// </summary>
        /// <param name="uuid">The region UUID</param>
        /// <returns>The sim profile</returns>
        override public RegionProfileData GetProfileByUUID(UUID uuid)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                Dictionary<string, object> param = new Dictionary<string, object>();
                param["?uuid"] = uuid.ToString();

                using (IDbCommand result = dbm.Manager.Query("SELECT * FROM regions WHERE uuid = ?uuid", param))
                {
                    using (IDataReader reader = result.ExecuteReader())
                    {
                        RegionProfileData row = dbm.Manager.readSimRow(reader);
                        return row;
                    }
                }
            }
            catch (Exception e)
            {
                dbm.Manager.Reconnect();
                m_log.Error(e.Message, e);
                return null;
            }
            finally
            {
                dbm.Release();
            }
        }

        /// <summary>
        /// Returns a sim profile from it's Region name string
        /// </summary>
        /// <returns>The sim profile</returns>
        override public RegionProfileData GetProfileByString(string regionName)
        {
            if (regionName.Length > 2)
            {
                MySQLSuperManager dbm = GetLockedConnection();

                try
                {
                    Dictionary<string, object> param = new Dictionary<string, object>();
                    // Add % because this is a like query.
                    param["?regionName"] = regionName + "%";
                    // Order by statement will return shorter matches first.  Only returns one record or no record.
                    using (IDbCommand result = dbm.Manager.Query(
                        "SELECT * FROM regions WHERE regionName like ?regionName order by LENGTH(regionName) asc LIMIT 1",
                        param))
                    {
                        using (IDataReader reader = result.ExecuteReader())
                        {
                            RegionProfileData row = dbm.Manager.readSimRow(reader);
                            return row;
                        }
                    }
                }
                catch (Exception e)
                {
                    dbm.Manager.Reconnect();
                    m_log.Error(e.Message, e);
                    return null;
                }
                finally
                {
                    dbm.Release();
                }
            }

            m_log.Error("[GRID DB]: Searched for a Region Name shorter then 3 characters");
            return null;
        }

        /// <summary>
        /// Adds a new profile to the database
        /// </summary>
        /// <param name="profile">The profile to add</param>
        /// <returns>Successful?</returns>
        override public DataResponse StoreProfile(RegionProfileData profile)
        {
            MySQLSuperManager dbm = GetLockedConnection();
            try
            {
                if (dbm.Manager.insertRegion(profile))
                    return DataResponse.RESPONSE_OK;
                else
                   return DataResponse.RESPONSE_ERROR;
            }
            finally
            {
                dbm.Release();
            }
        }

        /// <summary>
        /// Deletes a sim profile from the database
        /// </summary>
        /// <param name="uuid">the sim UUID</param>
        /// <returns>Successful?</returns>
        //public DataResponse DeleteProfile(RegionProfileData profile)
        override public DataResponse DeleteProfile(string uuid)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                if (dbm.Manager.deleteRegion(uuid))
                    return DataResponse.RESPONSE_OK;
                else
                    return DataResponse.RESPONSE_ERROR;
            }
            finally
            {
                dbm.Release();
            }
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
        /// Adds a location reservation
        /// </summary>
        /// <param name="x">x coordinate</param>
        /// <param name="y">y coordinate</param>
        /// <returns></returns>
        override public ReservationData GetReservationAtPoint(uint x, uint y)
        {
            MySQLSuperManager dbm = GetLockedConnection();

            try
            {
                Dictionary<string, object> param = new Dictionary<string, object>();
                param["?x"] = x.ToString();
                param["?y"] = y.ToString();
                using (IDbCommand result = dbm.Manager.Query(
                    "SELECT * FROM reservations WHERE resXMin <= ?x AND resXMax >= ?x AND resYMin <= ?y AND resYMax >= ?y",
                    param))
                {
                    using (IDataReader reader = result.ExecuteReader())
                    {
                        ReservationData row = dbm.Manager.readReservationRow(reader);
                        return row;
                    }
                }
            }
            catch (Exception e)
            {
                dbm.Manager.Reconnect();
                m_log.Error(e.Message, e);
                return null;
            }
            finally
            {
                dbm.Release();
            }
        }
    }
}
