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
using MySql.Data.MySqlClient;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;

namespace OpenSim.Data.MySQL
{
    public class UserProfilesData: IProfilesData
    {
        static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        #region Properites
        string ConnectionString
        {
            get; set;
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
            using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
            {
                dbcon.Open();
                
                Migration m = new Migration(dbcon, Assembly, "UserProfiles");
                m.Update();
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
            
            using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
            {
                string query = "SELECT classifieduuid, name FROM classifieds WHERE creatoruuid = ?Id";
                dbcon.Open();
                using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                {
                    cmd.Parameters.AddWithValue("?Id", creatorId);
                    using( MySqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default))
                    {
                        if(reader.HasRows)
                        {
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
                        }
                    }
                }
            }
            return data;
        }
        
        public bool UpdateClassifiedRecord(UserClassifiedAdd ad, ref string result)
        {
            string query = string.Empty;
            
            
            query += "INSERT INTO classifieds (";
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
            query += "?ClassifiedId,";
            query += "?CreatorId,";
            query += "?CreatedDate,";
            query += "?ExpirationDate,";
            query += "?Category,";
            query += "?Name,";
            query += "?Description,";
            query += "?ParcelId,";
            query += "?ParentEstate,";
            query += "?SnapshotId,";
            query += "?SimName,";
            query += "?GlobalPos,";
            query += "?ParcelName,";
            query += "?Flags,";
            query += "?ListingPrice ) ";
            query += "ON DUPLICATE KEY UPDATE ";
            query += "category=?Category, ";
            query += "expirationdate=?ExpirationDate, ";
            query += "name=?Name, ";
            query += "description=?Description, ";
            query += "parentestate=?ParentEstate, ";
            query += "posglobal=?GlobalPos, ";
            query += "parcelname=?ParcelName, ";
            query += "classifiedflags=?Flags, ";
            query += "priceforlisting=?ListingPrice, ";
            query += "snapshotuuid=?SnapshotId";
            
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
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?ClassifiedId", ad.ClassifiedId.ToString());
                        cmd.Parameters.AddWithValue("?CreatorId", ad.CreatorId.ToString());
                        cmd.Parameters.AddWithValue("?CreatedDate", ad.CreationDate.ToString());
                        cmd.Parameters.AddWithValue("?ExpirationDate", ad.ExpirationDate.ToString());
                        cmd.Parameters.AddWithValue("?Category", ad.Category.ToString());
                        cmd.Parameters.AddWithValue("?Name", ad.Name.ToString());
                        cmd.Parameters.AddWithValue("?Description", ad.Description.ToString());
                        cmd.Parameters.AddWithValue("?ParcelId", ad.ParcelId.ToString());
                        cmd.Parameters.AddWithValue("?ParentEstate", ad.ParentEstate.ToString());
                        cmd.Parameters.AddWithValue("?SnapshotId", ad.SnapshotId.ToString ());
                        cmd.Parameters.AddWithValue("?SimName", ad.SimName.ToString());
                        cmd.Parameters.AddWithValue("?GlobalPos", ad.GlobalPos.ToString());
                        cmd.Parameters.AddWithValue("?ParcelName", ad.ParcelName.ToString());
                        cmd.Parameters.AddWithValue("?Flags", ad.Flags.ToString());
                        cmd.Parameters.AddWithValue("?ListingPrice", ad.Price.ToString ());
                        
                        cmd.ExecuteNonQuery();
                    }
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
            query += "classifieduuid = ?recordId";
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?recordId", recordId.ToString());
                        cmd.ExecuteNonQuery();
                    }
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
            string query = string.Empty;
            
            query += "SELECT * FROM classifieds WHERE ";
            query += "classifieduuid = ?AdId";
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?AdId", ad.ClassifiedId.ToString());
                        
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if(reader.Read ())
                            {
                                ad.CreatorId = new UUID(reader.GetGuid("creatoruuid"));
                                ad.ParcelId = new UUID(reader.GetGuid("parceluuid"));
                                ad.SnapshotId = new UUID(reader.GetGuid("snapshotuuid"));
                                ad.CreationDate = Convert.ToInt32(reader["creationdate"]);
                                ad.ExpirationDate = Convert.ToInt32(reader["expirationdate"]);
                                ad.ParentEstate = Convert.ToInt32(reader["parentestate"]);
                                ad.Flags = (byte)reader.GetUInt32("classifiedflags");
                                ad.Category = reader.GetInt32("category");
                                ad.Price = reader.GetInt16("priceforlisting");
                                ad.Name = reader.GetString("name");
                                ad.Description = reader.GetString("description");
                                ad.SimName = reader.GetString("simname");
                                ad.GlobalPos = reader.GetString("posglobal");
                                ad.ParcelName = reader.GetString("parcelname");
                                
                            }
                        }
                    }
                    dbcon.Close();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                 ": GetPickInfo exception {0}", e.Message);
            }
            return true;
        }
        #endregion Classifieds Queries
        
        #region Picks Queries
        public OSDArray GetAvatarPicks(UUID avatarId)
        {
            string query = string.Empty;
            
            query += "SELECT `pickuuid`,`name` FROM userpicks WHERE ";
            query += "creatoruuid = ?Id";
            OSDArray data = new OSDArray();
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?Id", avatarId.ToString());
                        
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if(reader.HasRows)
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
            string query = string.Empty;
            UserProfilePick pick = new UserProfilePick();
            
            query += "SELECT * FROM userpicks WHERE ";
            query += "creatoruuid = ?CreatorId AND ";
            query += "pickuuid =  ?PickId";
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?CreatorId", avatarId.ToString());
                        cmd.Parameters.AddWithValue("?PickId", pickId.ToString());
                        
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if(reader.HasRows)
                            {
                                reader.Read();
                                
                                string description = (string)reader["description"];
                                
                                if (string.IsNullOrEmpty(description))
                                    description = "No description given.";
                                
                                UUID.TryParse((string)reader["pickuuid"], out pick.PickId);
                                UUID.TryParse((string)reader["creatoruuid"], out pick.CreatorId);
                                UUID.TryParse((string)reader["parceluuid"], out pick.ParcelId);
                                UUID.TryParse((string)reader["snapshotuuid"], out pick.SnapshotId);
                                pick.GlobalPos = (string)reader["posglobal"];
                                pick.Gatekeeper = (string)reader["gatekeeper"];
                                bool.TryParse((string)reader["toppick"], out pick.TopPick);
                                bool.TryParse((string)reader["enabled"], out pick.Enabled);
                                pick.Name = (string)reader["name"];
                                pick.Desc = description;
                                pick.ParcelName = (string)reader["user"];
                                pick.OriginalName = (string)reader["originalname"];
                                pick.SimName = (string)reader["simname"];
                                pick.SortOrder = (int)reader["sortorder"];
                            }
                        }
                    }
                    dbcon.Close();
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
            
            query += "INSERT INTO userpicks VALUES (";
            query += "?PickId,";
            query += "?CreatorId,";
            query += "?TopPick,";
            query += "?ParcelId,";
            query += "?Name,";
            query += "?Desc,";
            query += "?SnapshotId,";
            query += "?User,";
            query += "?Original,";
            query += "?SimName,";
            query += "?GlobalPos,";
            query += "?SortOrder,";
            query += "?Enabled,";
            query += "?Gatekeeper)";
            query += "ON DUPLICATE KEY UPDATE ";
            query += "parceluuid=?ParcelId,";
            query += "name=?Name,";
            query += "description=?Desc,";
            query += "user=?User,";
            query += "simname=?SimName,";
            query += "snapshotuuid=?SnapshotId,";
            query += "pickuuid=?PickId,";
            query += "posglobal=?GlobalPos,";
            query += "gatekeeper=?Gatekeeper";
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?PickId", pick.PickId.ToString());
                        cmd.Parameters.AddWithValue("?CreatorId", pick.CreatorId.ToString());
                        cmd.Parameters.AddWithValue("?TopPick", pick.TopPick.ToString());
                        cmd.Parameters.AddWithValue("?ParcelId", pick.ParcelId.ToString());
                        cmd.Parameters.AddWithValue("?Name", pick.Name.ToString());
                        cmd.Parameters.AddWithValue("?Desc", pick.Desc.ToString());
                        cmd.Parameters.AddWithValue("?SnapshotId", pick.SnapshotId.ToString());
                        cmd.Parameters.AddWithValue("?User", pick.ParcelName.ToString());
                        cmd.Parameters.AddWithValue("?Original", pick.OriginalName.ToString());
                        cmd.Parameters.AddWithValue("?SimName",pick.SimName.ToString());
                        cmd.Parameters.AddWithValue("?GlobalPos", pick.GlobalPos);
                        cmd.Parameters.AddWithValue("?Gatekeeper",pick.Gatekeeper);
                        cmd.Parameters.AddWithValue("?SortOrder", pick.SortOrder.ToString ());
                        cmd.Parameters.AddWithValue("?Enabled", pick.Enabled.ToString());
                        
                        cmd.ExecuteNonQuery();
                    }
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
            query += "pickuuid = ?PickId";
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?PickId", pickId.ToString());
                        
                        cmd.ExecuteNonQuery();
                    }
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
        #endregion Picks Queries
        
        #region Avatar Notes Queries
        public bool GetAvatarNotes(ref UserProfileNotes notes)
        {  // WIP
            string query = string.Empty;
            
            query += "SELECT `notes` FROM usernotes WHERE ";
            query += "useruuid = ?Id AND ";
            query += "targetuuid = ?TargetId";
            OSDArray data = new OSDArray();
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?Id", notes.UserId.ToString());
                        cmd.Parameters.AddWithValue("?TargetId", notes.TargetId.ToString());
                        
                        using (MySqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if(reader.HasRows)
                            {
                                reader.Read();
                                notes.Notes = OSD.FromString((string)reader["notes"]);
                            }
                            else
                            {
                                notes.Notes = OSD.FromString("");
                            }
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
                query += "useruuid=?UserId AND ";
                query += "targetuuid=?TargetId";
            }
            else
            {
                remove = false;
                query += "INSERT INTO usernotes VALUES ( ";
                query += "?UserId,";
                query += "?TargetId,";
                query += "?Notes )";
                query += "ON DUPLICATE KEY ";
                query += "UPDATE ";
                query += "notes=?Notes";
            }
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        if(!remove)
                            cmd.Parameters.AddWithValue("?Notes", note.Notes);
                        cmd.Parameters.AddWithValue("?TargetId", note.TargetId.ToString ());
                        cmd.Parameters.AddWithValue("?UserId", note.UserId.ToString());
                        
                        cmd.ExecuteNonQuery();
                    }
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
        #endregion Avatar Notes Queries
        
        #region Avatar Properties
        public bool GetAvatarProperties(ref UserProfileProperties props, ref string result)
        {
            string query = string.Empty;
            
            query += "SELECT * FROM userprofile WHERE ";
            query += "useruuid = ?Id";
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?Id", props.UserId.ToString());
                        
                        using (MySqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if(reader.HasRows)
                            {
                                reader.Read();
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
                                query += "?userId, ";
                                query += "?profilePartner, ";
                                query += "?profileAllowPublish, ";
                                query += "?profileMaturePublish, ";
                                query += "?profileURL, ";
                                query += "?profileWantToMask, ";
                                query += "?profileWantToText, ";
                                query += "?profileSkillsMask, ";
                                query += "?profileSkillsText, ";
                                query += "?profileLanguages, ";
                                query += "?profileImage, ";
                                query += "?profileAboutText, ";
                                query += "?profileFirstImage, ";
                                query += "?profileFirstText)";

                                dbcon.Close();
                                dbcon.Open();

                                using (MySqlCommand put = new MySqlCommand(query, dbcon))
                                {
                                    put.Parameters.AddWithValue("?userId", props.UserId.ToString());
                                    put.Parameters.AddWithValue("?profilePartner", props.PartnerId.ToString());
                                    put.Parameters.AddWithValue("?profileAllowPublish", props.PublishProfile);
                                    put.Parameters.AddWithValue("?profileMaturePublish", props.PublishMature);
                                    put.Parameters.AddWithValue("?profileURL", props.WebUrl);
                                    put.Parameters.AddWithValue("?profileWantToMask", props.WantToMask);
                                    put.Parameters.AddWithValue("?profileWantToText", props.WantToText);
                                    put.Parameters.AddWithValue("?profileSkillsMask", props.SkillsMask);
                                    put.Parameters.AddWithValue("?profileSkillsText", props.SkillsText);
                                    put.Parameters.AddWithValue("?profileLanguages", props.Language);
                                    put.Parameters.AddWithValue("?profileImage", props.ImageId.ToString());
                                    put.Parameters.AddWithValue("?profileAboutText", props.AboutText);
                                    put.Parameters.AddWithValue("?profileFirstImage", props.FirstLifeImageId.ToString());
                                    put.Parameters.AddWithValue("?profileFirstText", props.FirstLifeText);

                                    put.ExecuteNonQuery();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                                 ": Requst properties exception {0}", e.Message);
                result = e.Message;
                return false;
            }
            return true;
        }
        
        public bool UpdateAvatarProperties(ref UserProfileProperties props, ref string result)
        {            
            string query = string.Empty;
            
            query += "UPDATE userprofile SET ";
            query += "profileURL=?profileURL, ";
            query += "profileImage=?image, ";
            query += "profileAboutText=?abouttext,";
            query += "profileFirstImage=?firstlifeimage,";
            query += "profileFirstText=?firstlifetext ";
            query += "WHERE useruuid=?uuid";
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?profileURL", props.WebUrl);
                        cmd.Parameters.AddWithValue("?image", props.ImageId.ToString());
                        cmd.Parameters.AddWithValue("?abouttext", props.AboutText);
                        cmd.Parameters.AddWithValue("?firstlifeimage", props.FirstLifeImageId.ToString());
                        cmd.Parameters.AddWithValue("?firstlifetext", props.FirstLifeText);
                        cmd.Parameters.AddWithValue("?uuid", props.UserId.ToString());
                        
                        cmd.ExecuteNonQuery();
                    }
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
        #endregion Avatar Properties
        
        #region Avatar Interests
        public bool UpdateAvatarInterests(UserProfileProperties up, ref string result)
        {           
            string query = string.Empty;
            
            query += "UPDATE userprofile SET ";
            query += "profileWantToMask=?WantMask, ";
            query += "profileWantToText=?WantText,";
            query += "profileSkillsMask=?SkillsMask,";
            query += "profileSkillsText=?SkillsText, ";
            query += "profileLanguages=?Languages ";
            query += "WHERE useruuid=?uuid";
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?WantMask", up.WantToMask);
                        cmd.Parameters.AddWithValue("?WantText", up.WantToText);
                        cmd.Parameters.AddWithValue("?SkillsMask", up.SkillsMask);
                        cmd.Parameters.AddWithValue("?SkillsText", up.SkillsText);
                        cmd.Parameters.AddWithValue("?Languages", up.Language);
                        cmd.Parameters.AddWithValue("?uuid", up.UserId.ToString());
                        
                        cmd.ExecuteNonQuery();
                    }
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
        #endregion Avatar Interests

        public OSDArray GetUserImageAssets(UUID avatarId)
        {
            OSDArray data = new OSDArray();
            string query = "SELECT `snapshotuuid` FROM {0} WHERE `creatoruuid` = ?Id";

            // Get classified image assets
            
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();

                    using (MySqlCommand cmd = new MySqlCommand(string.Format (query,"`classifieds`"), dbcon))
                    {
                        cmd.Parameters.AddWithValue("?Id", avatarId.ToString());
                        
                        using (MySqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if(reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    data.Add(new OSDString((string)reader["snapshotuuid"].ToString ()));
                                }
                            }
                        }
                    }

                    dbcon.Close();
                    dbcon.Open();

                    using (MySqlCommand cmd = new MySqlCommand(string.Format (query,"`userpicks`"), dbcon))
                    {
                        cmd.Parameters.AddWithValue("?Id", avatarId.ToString());
                        
                        using (MySqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if(reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    data.Add(new OSDString((string)reader["snapshotuuid"].ToString ()));
                                }
                            }
                        }
                    }
                    
                    dbcon.Close();
                    dbcon.Open();

                    query = "SELECT `profileImage`, `profileFirstImage` FROM `userprofile` WHERE `useruuid` = ?Id";

                    using (MySqlCommand cmd = new MySqlCommand(string.Format (query,"`userpicks`"), dbcon))
                    {
                        cmd.Parameters.AddWithValue("?Id", avatarId.ToString());
                        
                        using (MySqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if(reader.HasRows)
                            {
                                while (reader.Read())
                                {
                                    data.Add(new OSDString((string)reader["profileImage"].ToString ()));
                                    data.Add(new OSDString((string)reader["profileFirstImage"].ToString ()));
                                }
                            }
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
        
        #region User Preferences
        public bool GetUserPreferences(ref UserPreferences pref, ref string result)
        {
            string query = string.Empty;
            
            query += "SELECT imviaemail,visible,email FROM ";
            query += "usersettings WHERE ";
            query += "useruuid = ?Id";
            
            OSDArray data = new OSDArray();
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?Id", pref.UserId.ToString());
                        
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            if(reader.HasRows)
                            {
                                reader.Read();
                                bool.TryParse((string)reader["imviaemail"], out pref.IMViaEmail);
                                bool.TryParse((string)reader["visible"], out pref.Visible);
                                pref.EMail = (string)reader["email"];
                            }
                            else
                            {
                                dbcon.Close();
                                dbcon.Open();
                                
                                query = "INSERT INTO usersettings VALUES ";
                                query += "(?uuid,'false','false', ?Email)";

                                using (MySqlCommand put = new MySqlCommand(query, dbcon))
                                {
                                    
                                    put.Parameters.AddWithValue("?Email", pref.EMail);
                                    put.Parameters.AddWithValue("?uuid", pref.UserId.ToString());

                                    put.ExecuteNonQuery();
                                }
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
        
        public bool UpdateUserPreferences(ref UserPreferences pref, ref string result)
        {           
            string query = string.Empty;

            query += "UPDATE usersettings SET ";
            query += "imviaemail=?ImViaEmail, ";
            query += "visible=?Visible, ";
            query += "email=?EMail ";
            query += "WHERE useruuid=?uuid";
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?ImViaEmail", pref.IMViaEmail.ToString().ToLower());
                        cmd.Parameters.AddWithValue("?Visible", pref.Visible.ToString().ToLower());
                        cmd.Parameters.AddWithValue("?uuid", pref.UserId.ToString());
                        cmd.Parameters.AddWithValue("?EMail", pref.EMail.ToString().ToLower());

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[PROFILES_DATA]" +
                    ": UserPreferencesUpdate exception {0} {1}", e.Message, e.InnerException);
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
            
            query += "SELECT * FROM `userdata` WHERE ";
            query += "UserId = ?Id AND ";
            query += "TagId = ?TagId";
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?Id", props.UserId.ToString());
                        cmd.Parameters.AddWithValue ("?TagId", props.TagId.ToString());
                        
                        using (MySqlDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            if(reader.HasRows)
                            {
                                reader.Read();
                                props.DataKey = (string)reader["DataKey"];
                                props.DataVal = (string)reader["DataVal"];
                            }
                            else
                            {
                                query += "INSERT INTO userdata VALUES ( ";
                                query += "?UserId,";
                                query += "?TagId,";
                                query += "?DataKey,";
                                query +=  "?DataVal) ";
                                
                                using (MySqlCommand put = new MySqlCommand(query, dbcon))
                                {
                                    put.Parameters.AddWithValue("?UserId", props.UserId.ToString());
                                    put.Parameters.AddWithValue("?TagId", props.TagId.ToString());
                                    put.Parameters.AddWithValue("?DataKey", props.DataKey.ToString());
                                    put.Parameters.AddWithValue("?DataVal", props.DataVal.ToString());

                                    put.ExecuteNonQuery();
                                }
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
            query += "TagId = ?TagId, ";
            query += "DataKey = ?DataKey, ";
            query += "DataVal = ?DataVal WHERE ";
            query += "UserId = ?UserId AND ";
            query += "TagId = ?TagId";
            
            try
            {
                using (MySqlConnection dbcon = new MySqlConnection(ConnectionString))
                {
                    dbcon.Open();
                    using (MySqlCommand cmd = new MySqlCommand(query, dbcon))
                    {
                        cmd.Parameters.AddWithValue("?UserId", props.UserId.ToString());
                        cmd.Parameters.AddWithValue("?TagId", props.TagId.ToString());
                        cmd.Parameters.AddWithValue("?DataKey", props.DataKey.ToString());
                        cmd.Parameters.AddWithValue("?DataVal", props.DataKey.ToString());

                        cmd.ExecuteNonQuery();
                    }
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
        #endregion Integration
    }
}