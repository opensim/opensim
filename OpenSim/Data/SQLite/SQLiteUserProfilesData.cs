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
#if CSharpSqlite
using Community.CsharpSqlite.Sqlite;
#else
using Mono.Data.Sqlite;
#endif
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Data.SQLite
{
    public class SQLiteUserProfilesData: IProfilesData
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private SqliteConnection m_connection;
        private string m_connectionString;

        private Dictionary<string, FieldInfo> m_FieldMap =
            new Dictionary<string, FieldInfo>();

        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public SQLiteUserProfilesData()
        {
        }

        public SQLiteUserProfilesData(string connectionString)
        {
            Initialise(connectionString);
        }

        public void Initialise(string connectionString)
        {
            if (Util.IsWindows())
                Util.LoadArchSpecificWindowsDll("sqlite3.dll");

            m_connectionString = connectionString;

            m_log.Info("[PROFILES_DATA]: Sqlite - connecting: "+m_connectionString);

            m_connection = new SqliteConnection(m_connectionString);
            m_connection.Open();

            Migration m = new Migration(m_connection, Assembly, "UserProfiles");
            m.Update();
        }

        private string[] FieldList
        {
            get { return new List<string>(m_FieldMap.Keys).ToArray(); }
        }

        #region IProfilesData implementation
        public OSDArray GetClassifiedRecords(UUID creatorId)
        {
            OSDArray data = new OSDArray();
            string query = "SELECT classifieduuid, name FROM classifieds WHERE creatoruuid = :Id";
            IDataReader reader = null;

            using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
            {
                cmd.CommandText = query;
                cmd.Parameters.AddWithValue(":Id", creatorId);
                reader = cmd.ExecuteReader();
            }

            while (reader.Read())
            {
                OSDMap n = new OSDMap();
                UUID Id = UUID.Zero;
                string Name = null;
                try
                {
                    UUID.TryParse(Convert.ToString( reader["classifieduuid"]), out Id);
                    Name = Convert.ToString(reader["name"]);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat("[PROFILES_DATA]" +
                                      ": UserAccount exception {0}", e.Message);
                }
                n.Add("classifieduuid", OSD.FromUUID(Id));
                n.Add("name", OSD.FromString(Name));
                data.Add(n);
            }

            reader.Close();

            return data;
        }
        public bool UpdateClassifiedRecord(UserClassifiedAdd ad, ref string result)
        {
            string query = string.Empty;

            query += "INSERT OR REPLACE INTO classifieds (";
            query += "`classifieduuid`,";
            query += "`creatoruuid`,";
            query += "`creationdate`,";
            query += "`expirationdate`,";
            query += "`category`,";
            query += "`name`,";
            query += "`description`,";
            query += "`parceluuid`,";
            query += "`parentestate`,";
            query += "`snapshotuuid`,";
            query += "`simname`,";
            query += "`posglobal`,";
            query += "`parcelname`,";
            query += "`classifiedflags`,";
            query += "`priceforlisting`) ";
            query += "VALUES (";
            query += ":ClassifiedId,";
            query += ":CreatorId,";
            query += ":CreatedDate,";
            query += ":ExpirationDate,";
            query += ":Category,";
            query += ":Name,";
            query += ":Description,";
            query += ":ParcelId,";
            query += ":ParentEstate,";
            query += ":SnapshotId,";
            query += ":SimName,";
            query += ":GlobalPos,";
            query += ":ParcelName,";
            query += ":Flags,";
            query += ":ListingPrice ) ";

            if(string.IsNullOrEmpty(ad.ParcelName))
                ad.ParcelName = "Unknown";
            if(ad.ParcelId == null)
                ad.ParcelId = UUID.Zero;
            if(string.IsNullOrEmpty(ad.Description))
                ad.Description = "No Description";

            DateTime epoch = new DateTime(1970, 1, 1);
            DateTime now = DateTime.Now;
            TimeSpan epochnow = now - epoch;
            TimeSpan duration;
            DateTime expiration;
            TimeSpan epochexp;

            if(ad.Flags == 2)
            {
                duration = new TimeSpan(7,0,0,0);
                expiration = now.Add(duration);
                epochexp = expiration - epoch;
            }
            else
            {
                duration = new TimeSpan(365,0,0,0);
                expiration = now.Add(duration);
                epochexp = expiration - epoch;
            }
            ad.CreationDate = (int)epochnow.TotalSeconds;
            ad.ExpirationDate = (int)epochexp.TotalSeconds;

            try {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":ClassifiedId", ad.ClassifiedId.ToString());
                    cmd.Parameters.AddWithValue(":CreatorId", ad.CreatorId.ToString());
                    cmd.Parameters.AddWithValue(":CreatedDate", ad.CreationDate.ToString());
                    cmd.Parameters.AddWithValue(":ExpirationDate", ad.ExpirationDate.ToString());
                    cmd.Parameters.AddWithValue(":Category", ad.Category.ToString());
                    cmd.Parameters.AddWithValue(":Name", ad.Name.ToString());
                    cmd.Parameters.AddWithValue(":Description", ad.Description.ToString());
                    cmd.Parameters.AddWithValue(":ParcelId", ad.ParcelId.ToString());
                    cmd.Parameters.AddWithValue(":ParentEstate", ad.ParentEstate.ToString());
                    cmd.Parameters.AddWithValue(":SnapshotId", ad.SnapshotId.ToString ());
                    cmd.Parameters.AddWithValue(":SimName", ad.SimName.ToString());
                    cmd.Parameters.AddWithValue(":GlobalPos", ad.GlobalPos.ToString());
                    cmd.Parameters.AddWithValue(":ParcelName", ad.ParcelName.ToString());
                    cmd.Parameters.AddWithValue(":Flags", ad.Flags.ToString());
                    cmd.Parameters.AddWithValue(":ListingPrice", ad.Price.ToString ());

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": ClassifiedesUpdate exception {0}", e.Message);
                result = e.Message;
                return false;
            }
            return true;
        }
        public bool DeleteClassifiedRecord(UUID recordId)
        {
            string query = string.Empty;

            query += "DELETE FROM classifieds WHERE ";
            query += "classifieduuid = :ClasifiedId";

            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":ClassifiedId", recordId.ToString());

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": DeleteClassifiedRecord exception {0}", e.Message);
                return false;
            }
            return true;
        }

        public bool GetClassifiedInfo(ref UserClassifiedAdd ad, ref string result)
        {
            IDataReader reader = null;
            string query = string.Empty;

            query += "SELECT * FROM classifieds WHERE ";
            query += "classifieduuid = :AdId";

            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":AdId", ad.ClassifiedId.ToString());

                    using (reader = cmd.ExecuteReader())
                    {
                        if(reader.Read ())
                        {
                            ad.CreatorId = new UUID(reader["creatoruuid"].ToString());
                            ad.ParcelId = new UUID(reader["parceluuid"].ToString ());
                            ad.SnapshotId = new UUID(reader["snapshotuuid"].ToString ());
                            ad.CreationDate = Convert.ToInt32(reader["creationdate"]);
                            ad.ExpirationDate = Convert.ToInt32(reader["expirationdate"]);
                            ad.ParentEstate = Convert.ToInt32(reader["parentestate"]);
                            ad.Flags = (byte) Convert.ToUInt32(reader["classifiedflags"]);
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
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": GetPickInfo exception {0}", e.Message);
            }
            return true;
        }

        public OSDArray GetAvatarPicks(UUID avatarId)
        {
            IDataReader reader = null;
            string query = string.Empty;

            query += "SELECT `pickuuid`,`name` FROM userpicks WHERE ";
            query += "creatoruuid = :Id";
            OSDArray data = new OSDArray();

            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":Id", avatarId.ToString());

                    using (reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            OSDMap record = new OSDMap();

                            record.Add("pickuuid",OSD.FromString((string)reader["pickuuid"]));
                            record.Add("name",OSD.FromString((string)reader["name"]));
                            data.Add(record);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": GetAvatarPicks exception {0}", e.Message);
            }
            return data;
        }
        public UserProfilePick GetPickInfo(UUID avatarId, UUID pickId)
        {
            IDataReader reader = null;
            string query = string.Empty;
            UserProfilePick pick = new UserProfilePick();

            query += "SELECT * FROM userpicks WHERE ";
            query += "creatoruuid = :CreatorId AND ";
            query += "pickuuid =  :PickId";

            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":CreatorId", avatarId.ToString());
                    cmd.Parameters.AddWithValue(":PickId", pickId.ToString());

                    using (reader = cmd.ExecuteReader())
                    {

                        while (reader.Read())
                        {
                            string description = (string)reader["description"];

                            if (string.IsNullOrEmpty(description))
                                description = "No description given.";

                            UUID.TryParse((string)reader["pickuuid"], out pick.PickId);
                            UUID.TryParse((string)reader["creatoruuid"], out pick.CreatorId);
                            UUID.TryParse((string)reader["parceluuid"], out pick.ParcelId);
                            UUID.TryParse((string)reader["snapshotuuid"], out pick.SnapshotId);
                            pick.GlobalPos = (string)reader["posglobal"];
                            bool.TryParse((string)reader["toppick"].ToString(), out pick.TopPick);
                            bool.TryParse((string)reader["enabled"].ToString(), out pick.Enabled);
                            pick.Name = (string)reader["name"];
                            pick.Desc = description;
                            pick.ParcelName = (string)reader["user"];
                            pick.OriginalName = (string)reader["originalname"];
                            pick.SimName = (string)reader["simname"];
                            pick.SortOrder = (int)reader["sortorder"];
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": GetPickInfo exception {0}", e.Message);
            }
            return pick;
        }

        public bool UpdatePicksRecord(UserProfilePick pick)
        {
            string query = string.Empty;

            query += "INSERT OR REPLACE INTO userpicks (";
            query += "pickuuid, ";
            query += "creatoruuid, ";
            query += "toppick, ";
            query += "parceluuid, ";
            query += "name, ";
            query += "description, ";
            query += "snapshotuuid, ";
            query += "user, ";
            query += "originalname, ";
            query += "simname, ";
            query += "posglobal, ";
            query += "sortorder, ";
            query += "enabled ) ";
            query += "VALUES (";
            query += ":PickId,";
            query += ":CreatorId,";
            query += ":TopPick,";
            query += ":ParcelId,";
            query += ":Name,";
            query += ":Desc,";
            query += ":SnapshotId,";
            query += ":User,";
            query += ":Original,";
            query += ":SimName,";
            query += ":GlobalPos,";
            query += ":SortOrder,";
            query += ":Enabled) ";

            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    int top_pick;
                    int.TryParse(pick.TopPick.ToString(), out top_pick);
                    int enabled;
                    int.TryParse(pick.Enabled.ToString(), out enabled);

                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":PickId", pick.PickId.ToString());
                    cmd.Parameters.AddWithValue(":CreatorId", pick.CreatorId.ToString());
                    cmd.Parameters.AddWithValue(":TopPick", top_pick);
                    cmd.Parameters.AddWithValue(":ParcelId", pick.ParcelId.ToString());
                    cmd.Parameters.AddWithValue(":Name", pick.Name.ToString());
                    cmd.Parameters.AddWithValue(":Desc", pick.Desc.ToString());
                    cmd.Parameters.AddWithValue(":SnapshotId", pick.SnapshotId.ToString());
                    cmd.Parameters.AddWithValue(":User", pick.ParcelName.ToString());
                    cmd.Parameters.AddWithValue(":Original", pick.OriginalName.ToString());
                    cmd.Parameters.AddWithValue(":SimName",pick.SimName.ToString());
                    cmd.Parameters.AddWithValue(":GlobalPos", pick.GlobalPos);
                    cmd.Parameters.AddWithValue(":SortOrder", pick.SortOrder.ToString ());
                    cmd.Parameters.AddWithValue(":Enabled", enabled);

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": UpdateAvatarNotes exception {0}", e.Message);
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
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":PickId", pickId.ToString());
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": DeleteUserPickRecord exception {0}", e.Message);
                return false;
            }
            return true;
        }

        public bool GetAvatarNotes(ref UserProfileNotes notes)
        {
            IDataReader reader = null;
            string query = string.Empty;

            query += "SELECT `notes` FROM usernotes WHERE ";
            query += "useruuid = :Id AND ";
            query += "targetuuid = :TargetId";
            OSDArray data = new OSDArray();

            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":Id", notes.UserId.ToString());
                    cmd.Parameters.AddWithValue(":TargetId", notes.TargetId.ToString());

                    using (reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        while (reader.Read())
                        {
                            notes.Notes = OSD.FromString((string)reader["notes"]);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": GetAvatarNotes exception {0}", e.Message);
            }
            return true;
        }

        public bool UpdateAvatarNotes(ref UserProfileNotes note, ref string result)
        {
            string query = string.Empty;
            bool remove;

            if(string.IsNullOrEmpty(note.Notes))
            {
                remove = true;
                query += "DELETE FROM usernotes WHERE ";
                query += "useruuid=:UserId AND ";
                query += "targetuuid=:TargetId";
            }
            else
            {
                remove = false;
                query += "INSERT OR REPLACE INTO usernotes VALUES ( ";
                query += ":UserId,";
                query += ":TargetId,";
                query += ":Notes )";
            }

            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;

                    if(!remove)
                        cmd.Parameters.AddWithValue(":Notes", note.Notes);
                    cmd.Parameters.AddWithValue(":TargetId", note.TargetId.ToString ());
                    cmd.Parameters.AddWithValue(":UserId", note.UserId.ToString());

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": UpdateAvatarNotes exception {0}", e.Message);
                return false;
            }
            return true;
        }

        public bool GetAvatarProperties(ref UserProfileProperties props, ref string result)
        {
            IDataReader reader = null;
            string query = string.Empty;

            query += "SELECT * FROM userprofile WHERE ";
            query += "useruuid = :Id";

                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":Id", props.UserId.ToString());


                    try
                    {
                        reader = cmd.ExecuteReader();
                    }
                    catch(Exception e)
                    {
                        m_log.ErrorFormat("[PROFILES_DATA]" +
                                          ": GetAvatarProperties exception {0}", e.Message);
                        result = e.Message;
                        return false;
                    }
                        if(reader != null && reader.Read())
                        {
                            props.WebUrl = (string)reader["profileURL"];
                            UUID.TryParse((string)reader["profileImage"], out props.ImageId);
                            props.AboutText = (string)reader["profileAboutText"];
                            UUID.TryParse((string)reader["profileFirstImage"], out props.FirstLifeImageId);
                            props.FirstLifeText = (string)reader["profileFirstText"];
                            UUID.TryParse((string)reader["profilePartner"], out props.PartnerId);
                            props.WantToMask = (int)reader["profileWantToMask"];
                            props.WantToText = (string)reader["profileWantToText"];
                            props.SkillsMask = (int)reader["profileSkillsMask"];
                            props.SkillsText = (string)reader["profileSkillsText"];
                            props.Language = (string)reader["profileLanguages"];
                        }
                        else
                        {
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
                            query += "profilePartner, ";
                            query += "profileAllowPublish, ";
                            query += "profileMaturePublish, ";
                            query += "profileURL, ";
                            query += "profileWantToMask, ";
                            query += "profileWantToText, ";
                            query += "profileSkillsMask, ";
                            query += "profileSkillsText, ";
                            query += "profileLanguages, ";
                            query += "profileImage, ";
                            query += "profileAboutText, ";
                            query += "profileFirstImage, ";
                            query += "profileFirstText) VALUES (";
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

                            using (SqliteCommand put = (SqliteCommand)m_connection.CreateCommand())
                            {
                                put.CommandText = query;
                                put.Parameters.AddWithValue(":userId", props.UserId.ToString());
                                put.Parameters.AddWithValue(":profilePartner", props.PartnerId.ToString());
                                put.Parameters.AddWithValue(":profileAllowPublish", props.PublishProfile);
                                put.Parameters.AddWithValue(":profileMaturePublish", props.PublishMature);
                                put.Parameters.AddWithValue(":profileURL", props.WebUrl);
                                put.Parameters.AddWithValue(":profileWantToMask", props.WantToMask);
                                put.Parameters.AddWithValue(":profileWantToText", props.WantToText);
                                put.Parameters.AddWithValue(":profileSkillsMask", props.SkillsMask);
                                put.Parameters.AddWithValue(":profileSkillsText", props.SkillsText);
                                put.Parameters.AddWithValue(":profileLanguages", props.Language);
                                put.Parameters.AddWithValue(":profileImage", props.ImageId.ToString());
                                put.Parameters.AddWithValue(":profileAboutText", props.AboutText);
                                put.Parameters.AddWithValue(":profileFirstImage", props.FirstLifeImageId.ToString());
                                put.Parameters.AddWithValue(":profileFirstText", props.FirstLifeText);

                                put.ExecuteNonQuery();
                            }
                        }
                }
            return true;
        }

        public bool UpdateAvatarProperties(ref UserProfileProperties props, ref string result)
        {
            string query = string.Empty;

            query += "UPDATE userprofile SET ";
            query += "profileURL=:profileURL, ";
            query += "profileImage=:image, ";
            query += "profileAboutText=:abouttext,";
            query += "profileFirstImage=:firstlifeimage,";
            query += "profileFirstText=:firstlifetext ";
            query += "WHERE useruuid=:uuid";

            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":profileURL", props.WebUrl);
                    cmd.Parameters.AddWithValue(":image", props.ImageId.ToString());
                    cmd.Parameters.AddWithValue(":abouttext", props.AboutText);
                    cmd.Parameters.AddWithValue(":firstlifeimage", props.FirstLifeImageId.ToString());
                    cmd.Parameters.AddWithValue(":firstlifetext", props.FirstLifeText);
                    cmd.Parameters.AddWithValue(":uuid", props.UserId.ToString());

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": AgentPropertiesUpdate exception {0}", e.Message);

                return false;
            }
            return true;
        }

        public bool UpdateAvatarInterests(UserProfileProperties up, ref string result)
        {
            string query = string.Empty;

            query += "UPDATE userprofile SET ";
            query += "profileWantToMask=:WantMask, ";
            query += "profileWantToText=:WantText,";
            query += "profileSkillsMask=:SkillsMask,";
            query += "profileSkillsText=:SkillsText, ";
            query += "profileLanguages=:Languages ";
            query += "WHERE useruuid=:uuid";

            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":WantMask", up.WantToMask);
                    cmd.Parameters.AddWithValue(":WantText", up.WantToText);
                    cmd.Parameters.AddWithValue(":SkillsMask", up.SkillsMask);
                    cmd.Parameters.AddWithValue(":SkillsText", up.SkillsText);
                    cmd.Parameters.AddWithValue(":Languages", up.Language);
                    cmd.Parameters.AddWithValue(":uuid", up.UserId.ToString());

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": AgentInterestsUpdate exception {0}", e.Message);
                result = e.Message;
                return false;
            }
            return true;
        }


        public bool UpdateUserPreferences(ref UserPreferences pref, ref string result)
        {
            string query = string.Empty;

            query += "UPDATE usersettings SET ";
            query += "imviaemail=:ImViaEmail, ";
            query += "visible=:Visible, ";
            query += "email=:EMail ";
            query += "WHERE useruuid=:uuid";

            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":ImViaEmail", pref.IMViaEmail);
                    cmd.Parameters.AddWithValue(":Visible", pref.Visible);
                    cmd.Parameters.AddWithValue(":EMail", pref.EMail);
                    cmd.Parameters.AddWithValue(":uuid", pref.UserId.ToString());

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": AgentInterestsUpdate exception {0}", e.Message);
                result = e.Message;
                return false;
            }
            return true;
        }

        public bool GetUserPreferences(ref UserPreferences pref, ref string result)
        {
            IDataReader reader = null;
            string query = string.Empty;

            query += "SELECT imviaemail,visible,email FROM ";
            query += "usersettings WHERE ";
            query += "useruuid = :Id";

            OSDArray data = new OSDArray();

            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue("?Id", pref.UserId.ToString());

                    using (reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if(reader.Read())
                        {
                            bool.TryParse((string)reader["imviaemail"], out pref.IMViaEmail);
                            bool.TryParse((string)reader["visible"], out pref.Visible);
                            pref.EMail = (string)reader["email"];
                         }
                         else
                         {
                            query = "INSERT INTO usersettings VALUES ";
                            query += "(:Id,'false','false', :Email)";

                            using (SqliteCommand put = (SqliteCommand)m_connection.CreateCommand())
                            {
                                put.Parameters.AddWithValue(":Id", pref.UserId.ToString());
                                put.Parameters.AddWithValue(":Email", pref.EMail);
                                put.ExecuteNonQuery();

                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": Get preferences exception {0}", e.Message);
                result = e.Message;
                return false;
            }
            return true;
        }

        public bool GetUserAppData(ref UserAppData props, ref string result)
        {
            IDataReader reader = null;
            string query = string.Empty;

            query += "SELECT * FROM `userdata` WHERE ";
            query += "UserId = :Id AND ";
            query += "TagId = :TagId";

            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":Id", props.UserId.ToString());
                    cmd.Parameters.AddWithValue (":TagId", props.TagId.ToString());

                    using (reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if(reader.Read())
                        {
                            props.DataKey = (string)reader["DataKey"];
                            props.DataVal = (string)reader["DataVal"];
                        }
                        else
                        {
                            query += "INSERT INTO userdata VALUES ( ";
                            query += ":UserId,";
                            query += ":TagId,";
                            query += ":DataKey,";
                            query +=  ":DataVal) ";

                            using (SqliteCommand put = (SqliteCommand)m_connection.CreateCommand())
                            {
                                put.Parameters.AddWithValue(":Id", props.UserId.ToString());
                                put.Parameters.AddWithValue(":TagId", props.TagId.ToString());
                                put.Parameters.AddWithValue(":DataKey", props.DataKey.ToString());
                                put.Parameters.AddWithValue(":DataVal", props.DataVal.ToString());

                                put.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": Requst application data exception {0}", e.Message);
                result = e.Message;
                return false;
            }
            return true;
        }
        public bool SetUserAppData(UserAppData props, ref string result)
        {
            string query = string.Empty;

            query += "UPDATE userdata SET ";
            query += "TagId = :TagId, ";
            query += "DataKey = :DataKey, ";
            query += "DataVal = :DataVal WHERE ";
            query += "UserId = :UserId AND ";
            query += "TagId = :TagId";

            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":UserId", props.UserId.ToString());
                    cmd.Parameters.AddWithValue(":TagId", props.TagId.ToString ());
                    cmd.Parameters.AddWithValue(":DataKey", props.DataKey.ToString ());
                    cmd.Parameters.AddWithValue(":DataVal", props.DataKey.ToString ());

                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": SetUserData exception {0}", e.Message);
                return false;
            }
            return true;
        }
        public OSDArray GetUserImageAssets(UUID avatarId)
        {
            IDataReader reader = null;
            OSDArray data = new OSDArray();
            string query = "SELECT `snapshotuuid` FROM {0} WHERE `creatoruuid` = :Id";

            // Get classified image assets


            try
            {
                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":Id", avatarId.ToString());

                    using (reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        while(reader.Read())
                        {
                            data.Add(new OSDString((string)reader["snapshotuuid"].ToString()));
                        }
                    }
                }

                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":Id", avatarId.ToString());

                    using (reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if(reader.Read())
                        {
                            data.Add(new OSDString((string)reader["snapshotuuid"].ToString ()));
                        }
                    }
                }

                query = "SELECT `profileImage`, `profileFirstImage` FROM `userprofile` WHERE `useruuid` = :Id";

                using (SqliteCommand cmd = (SqliteCommand)m_connection.CreateCommand())
                {
                    cmd.CommandText = query;
                    cmd.Parameters.AddWithValue(":Id", avatarId.ToString());

                    using (reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                    {
                        if(reader.Read())
                        {
                            data.Add(new OSDString((string)reader["profileImage"].ToString ()));
                            data.Add(new OSDString((string)reader["profileFirstImage"].ToString ()));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                  ": GetAvatarNotes exception {0}", e.Message);
            }
            return data;
        }
        #endregion
    }
}

