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
using System.Data;
using System.Reflection;
using OpenSim.Data;
using OpenSim.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Npgsql;

namespace OpenSim.Data.PGSQL
{
    public class UserProfilesData : IProfilesData
    {
        static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected PGSQLManager m_database;

        #region Properites
        string ConnectionString
        {
            get;
            set;
        }

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        #endregion Properties

        #region class Member Functions
        public UserProfilesData(string connectionString)
        {
            ConnectionString = connectionString;
            Init();
        }

        void Init()
        {
            using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
            {
                dbcon.Open();

                Migration m = new Migration(dbcon, Assembly, "UserProfiles");
                m.Update();
                m_database = new PGSQLManager(ConnectionString);
            }
        }
        #endregion Member Functions

        #region Classifieds Queries
        /// <summary>
        /// Gets the classified records.
        /// </summary>
        /// <returns>
        /// Array of classified records
        /// </returns>
        /// <param name='creatorId'>
        /// Creator identifier.
        /// </param>
        public OSDArray GetClassifiedRecords(UUID creatorId)
        {
            OSDArray data = new OSDArray();

            using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
            {
                string query = @"SELECT classifieduuid, name FROM classifieds WHERE creatoruuid = :Id";
                dbcon.Open();
                using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                {
                    cmd.Parameters.Add(m_database.CreateParameter("Id", creatorId));
                    using (NpgsqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default))
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                OSDMap n = new OSDMap();
                                UUID Id = UUID.Zero;

                                string Name = null;
                                try
                                {
                                    Id = DBGuid.FromDB(reader["classifieduuid"]);
                                    Name = Convert.ToString(reader["name"]);
                                }
                                catch (Exception e)
                                {
                                    m_log.Error("[PROFILES_DATA]: UserAccount exception ", e);
                                }

                                n.Add("classifieduuid", OSD.FromUUID(Id));
                                n.Add("name", OSD.FromString(Name));
                                data.Add(n);
                            }
                        }
                    }
                }
            }
            return data;
        }

        public bool UpdateClassifiedRecord(UserClassifiedAdd ad, ref string result)
        {
            string query = string.Empty;

            query = @"WITH upsert AS (
                        UPDATE classifieds SET
                            classifieduuid = :ClassifiedId, creatoruuid = :CreatorId, creationdate = :CreatedDate,
                            expirationdate = :ExpirationDate,category =:Category, name = :Name, description = :Description,
                            parceluuid = :ParcelId, parentestate = :ParentEstate, snapshotuuid = :SnapshotId,
                            simname = :SimName, posglobal = :GlobalPos, parcelname = :ParcelName, classifiedflags = :Flags,
                            priceforlisting = :ListingPrice
                        RETURNING * )
                      INSERT INTO classifieds (classifieduuid,creatoruuid,creationdate,expirationdate,category,name,
                            description,parceluuid,parentestate,snapshotuuid,simname,posglobal,parcelname,classifiedflags,
                            priceforlisting)
                      SELECT
                            :ClassifiedId,:CreatorId,:CreatedDate,:ExpirationDate,:Category,:Name,:Description,
                            :ParcelId,:ParentEstate,:SnapshotId,:SimName,:GlobalPos,:ParcelName,:Flags,:ListingPrice
                      WHERE NOT EXISTS (
                        SELECT * FROM upsert )";

            if (string.IsNullOrEmpty(ad.ParcelName))
                ad.ParcelName = "Unknown";
            if (ad.ParcelId == null)
                ad.ParcelId = UUID.Zero;
            if (string.IsNullOrEmpty(ad.Description))
                ad.Description = "No Description";

            DateTime epoch = new DateTime(1970, 1, 1);
            DateTime now = DateTime.Now;
            TimeSpan epochnow = now - epoch;
            TimeSpan duration;
            DateTime expiration;
            TimeSpan epochexp;

            if (ad.Flags == 2)
            {
                duration = new TimeSpan(7, 0, 0, 0);
                expiration = now.Add(duration);
                epochexp = expiration - epoch;
            }
            else
            {
                duration = new TimeSpan(365, 0, 0, 0);
                expiration = now.Add(duration);
                epochexp = expiration - epoch;
            }
            ad.CreationDate = (int)epochnow.TotalSeconds;
            ad.ExpirationDate = (int)epochexp.TotalSeconds;

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("ClassifiedId", ad.ClassifiedId));
                        cmd.Parameters.Add(m_database.CreateParameter("CreatorId", ad.CreatorId));
                        cmd.Parameters.Add(m_database.CreateParameter("CreatedDate", (int)ad.CreationDate));
                        cmd.Parameters.Add(m_database.CreateParameter("ExpirationDate", (int)ad.ExpirationDate));
                        cmd.Parameters.Add(m_database.CreateParameter("Category", ad.Category.ToString()));
                        cmd.Parameters.Add(m_database.CreateParameter("Name", ad.Name.ToString()));
                        cmd.Parameters.Add(m_database.CreateParameter("Description", ad.Description.ToString()));
                        cmd.Parameters.Add(m_database.CreateParameter("ParcelId", ad.ParcelId));
                        cmd.Parameters.Add(m_database.CreateParameter("ParentEstate", (int)ad.ParentEstate));
                        cmd.Parameters.Add(m_database.CreateParameter("SnapshotId", ad.SnapshotId));
                        cmd.Parameters.Add(m_database.CreateParameter("SimName", ad.SimName.ToString()));
                        cmd.Parameters.Add(m_database.CreateParameter("GlobalPos", ad.GlobalPos.ToString()));
                        cmd.Parameters.Add(m_database.CreateParameter("ParcelName", ad.ParcelName.ToString()));
                        cmd.Parameters.Add(m_database.CreateParameter("Flags", (int)Convert.ToInt32(ad.Flags)));
                        cmd.Parameters.Add(m_database.CreateParameter("ListingPrice", (int)Convert.ToInt32(ad.Price)));

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: ClassifiedsUpdate exception ", e);
                result = e.Message;
                return false;
            }

            return true;
        }

        public bool DeleteClassifiedRecord(UUID recordId)
        {
            string query = string.Empty;

            query = @"DELETE FROM classifieds WHERE classifieduuid = :ClassifiedId ;";

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("ClassifiedId", recordId));
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: DeleteClassifiedRecord exception ", e);
                return false;
            }

            return true;
        }

        public bool GetClassifiedInfo(ref UserClassifiedAdd ad, ref string result)
        {
            string query = string.Empty;

            query += "SELECT * FROM classifieds WHERE ";
            query += "classifieduuid = :AdId";

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("AdId", ad.ClassifiedId));

                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                ad.CreatorId = DBGuid.FromDB(reader["creatoruuid"]);
                                ad.ParcelId = DBGuid.FromDB(reader["parceluuid"]);
                                ad.SnapshotId = DBGuid.FromDB(reader["snapshotuuid"]);
                                ad.CreationDate = Convert.ToInt32(reader["creationdate"]);
                                ad.ExpirationDate = Convert.ToInt32(reader["expirationdate"]);
                                ad.ParentEstate = Convert.ToInt32(reader["parentestate"]);
                                ad.Flags = (byte)Convert.ToInt16(reader["classifiedflags"]);
                                ad.Category = Convert.ToInt32(reader["category"]);
                                ad.Price = Convert.ToInt16(reader["priceforlisting"]);
                                ad.Name = reader["name"].ToString();
                                ad.Description = reader["description"].ToString();
                                ad.SimName = reader["simname"].ToString();
                                ad.GlobalPos = reader["posglobal"].ToString();
                                ad.ParcelName = reader["parcelname"].ToString();
                            }
                        }
                    }
                    dbcon.Close();
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: GetClassifiedInfo exception ", e);
            }

            return true;
        }

        public static UUID GetUUID(object uuidValue)
        {

            UUID ret = UUID.Zero;

            UUID.TryParse(uuidValue.ToString(), out ret);

            return ret;
        }

        #endregion Classifieds Queries

        #region Picks Queries
        public OSDArray GetAvatarPicks(UUID avatarId)
        {
            string query = string.Empty;

            query += "SELECT pickuuid, name FROM userpicks WHERE ";
            query += "creatoruuid = :Id";
            OSDArray data = new OSDArray();

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("Id", avatarId));

                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    OSDMap record = new OSDMap();

                                    record.Add("pickuuid", OSD.FromUUID(DBGuid.FromDB(reader["pickuuid"])));
                                    record.Add("name", OSD.FromString((string)reader["name"]));
                                    data.Add(record);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: GetAvatarPicks exception ", e);
            }

            return data;
        }

        public UserProfilePick GetPickInfo(UUID avatarId, UUID pickId)
        {
            string query = string.Empty;
            UserProfilePick pick = new UserProfilePick();

            query += "SELECT * FROM userpicks WHERE ";
            query += "creatoruuid = :CreatorId AND ";
            query += "pickuuid =  :PickId";

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("CreatorId", avatarId));
                        cmd.Parameters.Add(m_database.CreateParameter("PickId", pickId));

                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                reader.Read();

                                string description = (string)reader["description"];

                                if (string.IsNullOrEmpty(description))
                                    description = "No description given.";

                                pick.PickId = DBGuid.FromDB(reader["pickuuid"]);
                                pick.CreatorId = DBGuid.FromDB(reader["creatoruuid"]);
                                pick.ParcelId = DBGuid.FromDB(reader["parceluuid"]);
                                pick.SnapshotId = DBGuid.FromDB(reader["snapshotuuid"]);
                                pick.GlobalPos = (string)reader["posglobal"].ToString();
                                pick.TopPick = Convert.ToBoolean(reader["toppick"]);
                                pick.Enabled = Convert.ToBoolean(reader["enabled"]);
                                pick.Name = reader["name"].ToString();
                                pick.Desc = reader["description"].ToString();
                                pick.ParcelName = reader["user"].ToString();
                                pick.OriginalName = reader["originalname"].ToString();
                                pick.SimName = reader["simname"].ToString();
                                pick.SortOrder = (int)reader["sortorder"];
                            }
                        }
                    }
                    dbcon.Close();
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: GetPickInfo exception ", e);
            }

            return pick;
        }

        public bool UpdatePicksRecord(UserProfilePick pick)
        {
            string query = string.Empty;


            query = @"WITH upsert AS (
                        UPDATE userpicks SET
                            pickuuid = :PickId, creatoruuid = :CreatorId, toppick = :TopPick, parceluuid = :ParcelId,
                            name = :Name, description = :Desc, snapshotuuid = :SnapshotId, ""user"" = :User, 
                            originalname = :Original, simname = :SimName, posglobal = :GlobalPos, 
                            sortorder = :SortOrder, enabled = :Enabled 
                        RETURNING * ) 
                      INSERT INTO userpicks (pickuuid,creatoruuid,toppick,parceluuid,name,description,
                            snapshotuuid,""user"",originalname,simname,posglobal,sortorder,enabled) 
                      SELECT
                            :PickId,:CreatorId,:TopPick,:ParcelId,:Name,:Desc,:SnapshotId,:User,
                            :Original,:SimName,:GlobalPos,:SortOrder,:Enabled 
                      WHERE NOT EXISTS (
                        SELECT * FROM upsert )";

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("PickId", pick.PickId));
                        cmd.Parameters.Add(m_database.CreateParameter("CreatorId", pick.CreatorId));
                        cmd.Parameters.Add(m_database.CreateParameter("TopPick", pick.TopPick));
                        cmd.Parameters.Add(m_database.CreateParameter("ParcelId", pick.ParcelId));
                        cmd.Parameters.Add(m_database.CreateParameter("Name", pick.Name));
                        cmd.Parameters.Add(m_database.CreateParameter("Desc", pick.Desc));
                        cmd.Parameters.Add(m_database.CreateParameter("SnapshotId", pick.SnapshotId));
                        cmd.Parameters.Add(m_database.CreateParameter("User", pick.ParcelName));
                        cmd.Parameters.Add(m_database.CreateParameter("Original", pick.OriginalName));
                        cmd.Parameters.Add(m_database.CreateParameter("SimName", pick.SimName));
                        cmd.Parameters.Add(m_database.CreateParameter("GlobalPos", pick.GlobalPos));
                        cmd.Parameters.Add(m_database.CreateParameter("SortOrder", pick.SortOrder));
                        cmd.Parameters.Add(m_database.CreateParameter("Enabled", pick.Enabled));

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: UpdateAvatarNotes exception ", e);
                return false;
            }

            return true;
        }

        public bool DeletePicksRecord(UUID pickId)
        {
            string query = string.Empty;

            query += "DELETE FROM userpicks WHERE ";
            query += "pickuuid = :PickId";

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("PickId", pickId));

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: DeleteUserPickRecord exception ", e);
                return false;
            }

            return true;
        }

        #endregion Picks Queries

        #region Avatar Notes Queries

        public bool GetAvatarNotes(ref UserProfileNotes notes)
        {  // WIP
            string query = string.Empty;

            query += "SELECT notes FROM usernotes WHERE ";
            query += "useruuid = :Id AND ";
            query += "targetuuid = :TargetId";
            OSDArray data = new OSDArray();

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("Id", notes.UserId));
                        cmd.Parameters.Add(m_database.CreateParameter("TargetId", notes.TargetId));

                        using (NpgsqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if (reader.HasRows)
                            {
                                reader.Read();
                                notes.Notes = OSD.FromString((string)reader["notes"]);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: GetAvatarNotes exception ", e);
            }

            return true;
        }

        public bool UpdateAvatarNotes(ref UserProfileNotes note, ref string result)
        {
            string query = string.Empty;
            bool remove;

            if (string.IsNullOrEmpty(note.Notes))
            {
                remove = true;
                query += "DELETE FROM usernotes WHERE ";
                query += "useruuid=:UserId AND ";
                query += "targetuuid=:TargetId";
            }
            else
            {
                remove = false;

                query = @"WITH upsert AS (
                          UPDATE usernotes SET notes = :Notes, useruuid = :UserId, targetuuid = :TargetId RETURNING * )
                          INSERT INTO usernotes (notes,useruuid,targetuuid)
                          SELECT :Notes,:UserId,:TargetId
                            WHERE NOT EXISTS (
                              SELECT * FROM upsert
                            )";
            }

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        if (!remove)
                            cmd.Parameters.Add(m_database.CreateParameter("Notes", note.Notes));

                        cmd.Parameters.Add(m_database.CreateParameter("TargetId", note.TargetId));
                        cmd.Parameters.Add(m_database.CreateParameter("UserId", note.UserId));

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: UpdateAvatarNotes exception ", e);
                return false;
            }

            return true;
        }

        #endregion Avatar Notes Queries

        #region Avatar Properties

        public bool GetAvatarProperties(ref UserProfileProperties props, ref string result)
        {
            string query = string.Empty;

            query += "SELECT * FROM userprofile WHERE ";
            query += "useruuid = :Id";

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("Id", props.UserId));

                        using (NpgsqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if (reader.HasRows)
                            {
                                // m_log.DebugFormat("[PROFILES_DATA]" +
                                //                  ": Getting data for {0}.", props.UserId);
                                reader.Read();
                                props.WebUrl = (string)reader["profileURL"].ToString();
                                props.ImageId = DBGuid.FromDB(reader["profileImage"]);
                                props.AboutText = (string)reader["profileAboutText"];
                                props.FirstLifeImageId = DBGuid.FromDB(reader["profileFirstImage"]);
                                props.FirstLifeText = (string)reader["profileFirstText"];
                                props.PartnerId = DBGuid.FromDB(reader["profilePartner"]);
                                props.WantToMask = (int)reader["profileWantToMask"];
                                props.WantToText = (string)reader["profileWantToText"];
                                props.SkillsMask = (int)reader["profileSkillsMask"];
                                props.SkillsText = (string)reader["profileSkillsText"];
                                props.Language = (string)reader["profileLanguages"];
                            }
                            else
                            {
                                //m_log.DebugFormat("[PROFILES_DATA]" +
                                //                 ": No data for {0}", props.UserId);

                                props.WebUrl = string.Empty;
                                props.ImageId = UUID.Zero;
                                props.AboutText = string.Empty;
                                props.FirstLifeImageId = UUID.Zero;
                                props.FirstLifeText = string.Empty;
                                props.PartnerId = UUID.Zero;
                                props.WantToMask = 0;
                                props.WantToText = string.Empty;
                                props.SkillsMask = 0;
                                props.SkillsText = string.Empty;
                                props.Language = string.Empty;
                                props.PublishProfile = false;
                                props.PublishMature = false;

                                query = "INSERT INTO userprofile (";
                                query += "useruuid, ";
                                query += "\"profilePartner\", ";
                                query += "\"profileAllowPublish\", ";
                                query += "\"profileMaturePublish\", ";
                                query += "\"profileURL\", ";
                                query += "\"profileWantToMask\", ";
                                query += "\"profileWantToText\", ";
                                query += "\"profileSkillsMask\", ";
                                query += "\"profileSkillsText\", ";
                                query += "\"profileLanguages\", ";
                                query += "\"profileImage\", ";
                                query += "\"profileAboutText\", ";
                                query += "\"profileFirstImage\", ";
                                query += "\"profileFirstText\") VALUES (";
                                query += ":userId, ";
                                query += ":profilePartner, ";
                                query += ":profileAllowPublish, ";
                                query += ":profileMaturePublish, ";
                                query += ":profileURL, ";
                                query += ":profileWantToMask, ";
                                query += ":profileWantToText, ";
                                query += ":profileSkillsMask, ";
                                query += ":profileSkillsText, ";
                                query += ":profileLanguages, ";
                                query += ":profileImage, ";
                                query += ":profileAboutText, ";
                                query += ":profileFirstImage, ";
                                query += ":profileFirstText)";

                                dbcon.Close();
                                dbcon.Open();

                                using (NpgsqlCommand put = new NpgsqlCommand(query, dbcon))
                                {
                                    //m_log.DebugFormat("[PROFILES_DATA]" +
                                    //                  ": Adding new data for {0}", props.UserId);

                                    put.Parameters.Add(m_database.CreateParameter("userId", props.UserId));
                                    put.Parameters.Add(m_database.CreateParameter("profilePartner", props.PartnerId));
                                    put.Parameters.Add(m_database.CreateParameter("profileAllowPublish", props.PublishProfile));
                                    put.Parameters.Add(m_database.CreateParameter("profileMaturePublish", props.PublishMature));
                                    put.Parameters.Add(m_database.CreateParameter("profileURL", props.WebUrl));
                                    put.Parameters.Add(m_database.CreateParameter("profileWantToMask", props.WantToMask));
                                    put.Parameters.Add(m_database.CreateParameter("profileWantToText", props.WantToText));
                                    put.Parameters.Add(m_database.CreateParameter("profileSkillsMask", props.SkillsMask));
                                    put.Parameters.Add(m_database.CreateParameter("profileSkillsText", props.SkillsText));
                                    put.Parameters.Add(m_database.CreateParameter("profileLanguages", props.Language));
                                    put.Parameters.Add(m_database.CreateParameter("profileImage", props.ImageId));
                                    put.Parameters.Add(m_database.CreateParameter("profileAboutText", props.AboutText));
                                    put.Parameters.Add(m_database.CreateParameter("profileFirstImage", props.FirstLifeImageId));
                                    put.Parameters.Add(m_database.CreateParameter("profileFirstText", props.FirstLifeText));

                                    put.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: GetAvatarProperties exception ", e);
                result = e.Message;
                return false;
            }

            return true;
        }

        public bool UpdateAvatarProperties(ref UserProfileProperties props, ref string result)
        {
            string query = string.Empty;

            query += "UPDATE userprofile SET ";
            query += "\"profileURL\"=:profileURL, ";
            query += "\"profileImage\"=:image, ";
            query += "\"profileAboutText\"=:abouttext,";
            query += "\"profileFirstImage\"=:firstlifeimage,";
            query += "\"profileFirstText\"=:firstlifetext ";
            query += "WHERE \"useruuid\"=:uuid";

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("profileURL", props.WebUrl));
                        cmd.Parameters.Add(m_database.CreateParameter("image", props.ImageId));
                        cmd.Parameters.Add(m_database.CreateParameter("abouttext", props.AboutText));
                        cmd.Parameters.Add(m_database.CreateParameter("firstlifeimage", props.FirstLifeImageId));
                        cmd.Parameters.Add(m_database.CreateParameter("firstlifetext", props.FirstLifeText));
                        cmd.Parameters.Add(m_database.CreateParameter("uuid", props.UserId));

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: AgentPropertiesUpdate exception ", e);
                return false;
            }

            return true;
        }

        #endregion Avatar Properties

        #region Avatar Interests

        public bool UpdateAvatarInterests(UserProfileProperties up, ref string result)
        {
            string query = string.Empty;

            query += "UPDATE userprofile SET ";
            query += "\"profileWantToMask\"=:WantMask, ";
            query += "\"profileWantToText\"=:WantText,";
            query += "\"profileSkillsMask\"=:SkillsMask,";
            query += "\"profileSkillsText\"=:SkillsText, ";
            query += "\"profileLanguages\"=:Languages ";
            query += "WHERE \"useruuid\"=:uuid";

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("WantMask", up.WantToMask));
                        cmd.Parameters.Add(m_database.CreateParameter("WantText", up.WantToText));
                        cmd.Parameters.Add(m_database.CreateParameter("SkillsMask", up.SkillsMask));
                        cmd.Parameters.Add(m_database.CreateParameter("SkillsText", up.SkillsText));
                        cmd.Parameters.Add(m_database.CreateParameter("Languages", up.Language));
                        cmd.Parameters.Add(m_database.CreateParameter("uuid", up.UserId));

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: UpdateAvatarInterests exception ", e);
                result = e.Message;
                return false;
            }

            return true;
        }

        #endregion Avatar Interests

        public OSDArray GetUserImageAssets(UUID avatarId)
        {
            OSDArray data = new OSDArray();
            string query = "SELECT \"snapshotuuid\" FROM {0} WHERE \"creatoruuid\" = :Id";

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();

                    using (NpgsqlCommand cmd = new NpgsqlCommand(string.Format(query, "\"classifieds\""), dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("Id", avatarId));

                        using (NpgsqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    data.Add(new OSDString(reader["snapshotuuid"].ToString()));
                                }
                            }
                        }
                    }

                    dbcon.Close();
                    dbcon.Open();

                    using (NpgsqlCommand cmd = new NpgsqlCommand(string.Format(query, "\"userpicks\""), dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("Id", avatarId));

                        using (NpgsqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    data.Add(new OSDString(reader["snapshotuuid"].ToString()));
                                }
                            }
                        }
                    }

                    dbcon.Close();
                    dbcon.Open();

                    query = "SELECT \"profileImage\", \"profileFirstImage\" FROM \"userprofile\" WHERE \"useruuid\" = :Id";

                    using (NpgsqlCommand cmd = new NpgsqlCommand(string.Format(query, "\"userpicks\""), dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("Id", avatarId));

                        using (NpgsqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if (reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    data.Add(new OSDString(reader["profileImage"].ToString()));
                                    data.Add(new OSDString(reader["profileFirstImage"].ToString()));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: GetUserImageAssets exception ", e);
            }

            return data;
        }

        #region User Preferences

        public bool GetUserPreferences(ref UserPreferences pref, ref string result)
        {
            string query = string.Empty;

            query += "SELECT imviaemail::VARCHAR,visible::VARCHAR,email FROM ";
            query += "usersettings WHERE ";
            query += "useruuid = :Id";

            OSDArray data = new OSDArray();

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("Id", pref.UserId));

                        using (NpgsqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.HasRows)
                            {
                                reader.Read();
                                bool.TryParse((string)reader["imviaemail"], out pref.IMViaEmail);
                                bool.TryParse((string)reader["visible"], out pref.Visible);
                                pref.EMail = (string)reader["email"];
                            }
                            else
                            {
                                using (NpgsqlCommand put = new NpgsqlCommand(query, dbcon))
                                {
                                    put.Parameters.Add(m_database.CreateParameter("Id", pref.UserId));
                                    query = "INSERT INTO usersettings VALUES ";
                                    query += "(:Id,'false','false', '')";

                                    put.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: GetUserPreferences exception ", e);
                result = e.Message;
            }

            return true;
        }

        public bool UpdateUserPreferences(ref UserPreferences pref, ref string result)
        {
            string query = string.Empty;

            query += "UPDATE usersettings SET ";
            query += "imviaemail=:ImViaEmail, ";
            query += "visible=:Visible, ";
            query += "email=:Email ";
            query += "WHERE useruuid=:uuid";

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("ImViaEmail", pref.IMViaEmail));
                        cmd.Parameters.Add(m_database.CreateParameter("Visible", pref.Visible));
                        cmd.Parameters.Add(m_database.CreateParameter("EMail", pref.EMail.ToString().ToLower()));
                        cmd.Parameters.Add(m_database.CreateParameter("uuid", pref.UserId));

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: UpdateUserPreferences exception ", e);
                result = e.Message;
                return false;
            }

            return true;
        }

        #endregion User Preferences

        #region Integration

        public bool GetUserAppData(ref UserAppData props, ref string result)
        {
            string query = string.Empty;

            query += "SELECT * FROM userdata WHERE ";
            query += "\"UserId\" = :Id AND ";
            query += "\"TagId\" = :TagId";

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("Id", props.UserId));
                        cmd.Parameters.Add(m_database.CreateParameter("TagId", props.TagId));

                        using (NpgsqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if (reader.HasRows)
                            {
                                reader.Read();
                                props.DataKey = (string)reader["DataKey"];
                                props.DataVal = (string)reader["DataVal"];
                            }
                            else
                            {
                                query += "INSERT INTO userdata VALUES ( ";
                                query += ":UserId,";
                                query += ":TagId,";
                                query += ":DataKey,";
                                query += ":DataVal) ";

                                using (NpgsqlCommand put = new NpgsqlCommand(query, dbcon))
                                {
                                    put.Parameters.Add(m_database.CreateParameter("UserId", props.UserId));
                                    put.Parameters.Add(m_database.CreateParameter("TagId", props.TagId));
                                    put.Parameters.Add(m_database.CreateParameter("DataKey", props.DataKey.ToString()));
                                    put.Parameters.Add(m_database.CreateParameter("DataVal", props.DataVal.ToString()));

                                    put.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: GetUserAppData exception ", e);
                result = e.Message;
                return false;
            }

            return true;
        }

        public bool SetUserAppData(UserAppData props, ref string result)
        {
            string query = string.Empty;

            query += "UPDATE userdata SET ";
            query += "\"TagId\" = :TagId, ";
            query += "\"DataKey\" = :DataKey, ";
            query += "\"DataVal\" = :DataVal WHERE ";
            query += "\"UserId\" = :UserId AND ";
            query += "\"TagId\" = :TagId";

            try
            {
                using (NpgsqlConnection dbcon = new NpgsqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, dbcon))
                    {
                        cmd.Parameters.Add(m_database.CreateParameter("UserId", props.UserId.ToString()));
                        cmd.Parameters.Add(m_database.CreateParameter("TagId", props.TagId.ToString()));
                        cmd.Parameters.Add(m_database.CreateParameter("DataKey", props.DataKey.ToString()));
                        cmd.Parameters.Add(m_database.CreateParameter("DataVal", props.DataKey.ToString()));

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.Error("[PROFILES_DATA]: SetUserData exception ", e);
                return false;
            }

            return true;
        }

        #endregion Integration
    }
}