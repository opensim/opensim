using System.Reflection;
using System.Data;
using MySqlConnector;
using OpenSim.Framework;
using System.Collections.Generic;
using OpenMetaverse;
using OpenMetaverse.Packets;
using System.Linq;
using log4net;

namespace OpenSim.Data.MySQL
{
    public class MySqlExperienceData : MySqlFramework, IExperienceData
    {
        protected virtual Assembly Assembly
        {
            get { return GetType().Assembly; }
        }

        public MySqlExperienceData(string connectionString)
                : base(connectionString)
        {
            m_connectionString = connectionString;

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();
                Migration m = new Migration(dbcon, Assembly, "Experience");
                m.Update();
                dbcon.Close();
            }
        }

        public Dictionary<UUID, bool> GetExperiencePermissions(UUID agent_id)
        {
            Dictionary<UUID, bool> experiencePermissions = new Dictionary<UUID, bool>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd
                    = new MySqlCommand("select * from `experience_permissions` where avatar = ?avatar", dbcon))
                {
                    cmd.Parameters.AddWithValue("?avatar", agent_id.ToString());
                    
                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            string uuid = result.GetString(0);
                            bool allow = result.GetBoolean(2);

                            UUID experience_key;
                            if(UUID.TryParse(uuid, out experience_key))
                            {
                                experiencePermissions.Add(experience_key, allow);
                            }
                        }

                        dbcon.Close();
                    }
                }
            }

            return experiencePermissions;
        }

        public bool ForgetExperiencePermissions(UUID agent_id, UUID experience_id)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd
                    = new MySqlCommand("delete from `experience_permissions` where avatar = ?avatar AND experience = ?experience LIMIT 1", dbcon))
                {
                    cmd.Parameters.AddWithValue("?avatar", agent_id.ToString());
                    cmd.Parameters.AddWithValue("?experience", experience_id.ToString());

                    return (cmd.ExecuteNonQuery() > 0);
                }
            }
        }

        public bool SetExperiencePermissions(UUID agent_id, UUID experience_id, bool allow)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("replace into `experience_permissions` (avatar, experience, allow) VALUES (?avatar, ?experience, ?allow)", dbcon))
                {
                    cmd.Parameters.AddWithValue("?avatar", agent_id.ToString());
                    cmd.Parameters.AddWithValue("?experience", experience_id.ToString());
                    cmd.Parameters.AddWithValue("?allow", allow);

                    return (cmd.ExecuteNonQuery() > 0);
                }
            }
        }

        public ExperienceInfoData[] GetExperienceInfos(UUID[] experiences)
        {
            List<string> uuids = new List<string>();
            foreach (var u in experiences)
                uuids.Add("'" + u.ToString() + "'");
            string joined = string.Join(",", uuids);

            List<ExperienceInfoData> infos = new List<ExperienceInfoData>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM `experiences` WHERE public_id IN (" + joined + ")", dbcon))
                {
                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            ExperienceInfoData info = new ExperienceInfoData();
                            info.public_id = UUID.Parse(result["public_id"].ToString());
                            info.owner_id = UUID.Parse(result["owner_id"].ToString());
                            info.group_id = UUID.Parse(result["group_id"].ToString());
                            info.name = result["name"].ToString();
                            info.description = result["description"].ToString();
                            info.logo = UUID.Parse(result["logo"].ToString());
                            info.marketplace = result["marketplace"].ToString();
                            info.slurl = result["slurl"].ToString();
                            info.maturity = int.Parse(result["maturity"].ToString());
                            info.properties = int.Parse(result["properties"].ToString());

                            infos.Add(info);
                        }
                    }
                }

                dbcon.Close();
            }

            return infos.ToArray();
        }

        public UUID[] GetAgentExperiences(UUID agent_id)
        {
            List<UUID> experiences = new List<UUID>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM `experiences` WHERE owner_id = ?avatar", dbcon))
                {
                    cmd.Parameters.AddWithValue("?avatar", agent_id.ToString());
                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            experiences.Add(UUID.Parse(result["public_id"].ToString()));
                        }
                    }
                }

                dbcon.Close();
            }

            return experiences.ToArray();
        }

        public bool UpdateExperienceInfo(ExperienceInfoData data)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("replace into `experiences` (public_id, owner_id, name, description, group_id, logo, marketplace, slurl, maturity, properties) VALUES (?public_id, ?owner_id, ?name, ?description, ?group_id, ?logo, ?marketplace, ?slurl, ?maturity, ?properties)", dbcon))
                {
                    cmd.Parameters.AddWithValue("?public_id", data.public_id.ToString());
                    cmd.Parameters.AddWithValue("?owner_id", data.owner_id.ToString());
                    cmd.Parameters.AddWithValue("?name", data.name);
                    cmd.Parameters.AddWithValue("?description", data.description);
                    cmd.Parameters.AddWithValue("?group_id", data.group_id.ToString());
                    cmd.Parameters.AddWithValue("?logo", data.logo.ToString());
                    cmd.Parameters.AddWithValue("?marketplace", data.marketplace);
                    cmd.Parameters.AddWithValue("?slurl", data.slurl);
                    cmd.Parameters.AddWithValue("?maturity", data.maturity);
                    cmd.Parameters.AddWithValue("?properties", data.properties);

                    return (cmd.ExecuteNonQuery() > 0);
                }
            }
        }

        public ExperienceInfoData[] FindExperiences(string search)
        {
            List<ExperienceInfoData> experiences = new List<ExperienceInfoData>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM `experiences` WHERE name LIKE ?search", dbcon))
                {
                    cmd.Parameters.AddWithValue("?search", string.Format("%{0}%", search));

                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            ExperienceInfoData info = new ExperienceInfoData();
                            info.public_id = UUID.Parse(result["public_id"].ToString());
                            info.owner_id = UUID.Parse(result["owner_id"].ToString());
                            info.group_id = UUID.Parse(result["group_id"].ToString());
                            info.name = result["name"].ToString();
                            info.description = result["description"].ToString();
                            info.logo = UUID.Parse(result["logo"].ToString());
                            info.marketplace = result["marketplace"].ToString();
                            info.slurl = result["slurl"].ToString();
                            info.maturity = int.Parse(result["maturity"].ToString());
                            info.properties = int.Parse(result["properties"].ToString());

                            experiences.Add(info);
                        }
                    }
                }

                dbcon.Close();
            }

            return experiences.ToArray();
        }

        public UUID[] GetGroupExperiences(UUID group_id)
        {
            List<UUID> experiences = new List<UUID>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM `experiences` WHERE group_id = ?group", dbcon))
                {
                    cmd.Parameters.AddWithValue("?group", group_id.ToString());
                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            experiences.Add(UUID.Parse(result["public_id"].ToString()));
                        }
                    }
                }

                dbcon.Close();
            }

            return experiences.ToArray();
        }

        public UUID[] GetExperiencesForGroups(UUID[] groups)
        {
            List<string> uuids = new List<string>();
            foreach (var u in groups)
                uuids.Add("'" + u.ToString() + "'");
            string joined = string.Join(",", uuids);

            List<UUID> experiences = new List<UUID>();

            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM `experiences` WHERE group_id IN (" + joined + ")", dbcon))
                {
                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            experiences.Add(UUID.Parse(result["public_id"].ToString()));
                        }
                    }
                }

                dbcon.Close();
            }

            return experiences.ToArray();
        }

        // KeyValue


        public bool SetKeyValue(UUID experience, string key, string val)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("REPLACE INTO `experience_kv` (`experience`, `key`, `value`) VALUES (?experience, ?key, ?value)", dbcon))
                {
                    cmd.Parameters.AddWithValue("?experience", experience.ToString());
                    cmd.Parameters.AddWithValue("?key", key);
                    cmd.Parameters.AddWithValue("?value", val);

                    return (cmd.ExecuteNonQuery() > 0);
                }
            }
        }

        public string GetKeyValue(UUID experience, string key)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT * FROM `experience_kv` WHERE `experience` = ?experience AND `key` = ?key LIMIT 1", dbcon))
                {
                    cmd.Parameters.AddWithValue("?experience", experience.ToString());
                    cmd.Parameters.AddWithValue("?key", key);

                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        if (result.Read())
                        {
                            return result["value"].ToString();
                        }
                    }
                }

                dbcon.Close();
            }

            return null;
        }

        public bool DeleteKey(UUID experience, string key)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("DELETE FROM `experience_kv` WHERE `experience` = ?experience AND `key` = ?key LIMIT 1", dbcon))
                {
                    cmd.Parameters.AddWithValue("?experience", experience.ToString());
                    cmd.Parameters.AddWithValue("?key", key);

                    return (cmd.ExecuteNonQuery() > 0);
                }
            }
        }

        public int GetKeyCount(UUID experience)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT COUNT(*) AS `count` FROM `experience_kv` WHERE `experience` = ?experience", dbcon))
                {
                    cmd.Parameters.AddWithValue("?experience", experience.ToString());

                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        if (result.Read())
                        {
                            return int.Parse(result["count"].ToString());
                        }
                    }
                }

                dbcon.Close();
            }

            return 0;
        }

        public string[] GetKeys(UUID experience, int start, int count)
        {
            List<string> keys = new List<string>();
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT `key` FROM `experience_kv` WHERE `experience` = ?experience LIMIT ?start, ?count;", dbcon))
                {
                    cmd.Parameters.AddWithValue("?experience", experience.ToString());
                    cmd.Parameters.AddWithValue("?start", start);
                    cmd.Parameters.AddWithValue("?count", count);

                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        while (result.Read())
                        {
                            keys.Add(result["key"].ToString());
                        }
                    }
                }

                dbcon.Close();
            }
            return keys.ToArray();
        }

        public int GetKeyValueSize(UUID experience)
        {
            using (MySqlConnection dbcon = new MySqlConnection(m_connectionString))
            {
                dbcon.Open();

                using (MySqlCommand cmd = new MySqlCommand("SELECT IFNULL(SUM(LENGTH(`key`) + LENGTH(`value`)), 0) AS `size` FROM `experience_kv` WHERE `experience` = ?experience", dbcon))
                {
                    cmd.Parameters.AddWithValue("?experience", experience.ToString());

                    using (IDataReader result = cmd.ExecuteReader())
                    {
                        if (result.Read())
                        {
                            return int.Parse(result["size"].ToString());
                        }
                    }
                }

                dbcon.Close();
            }

            return 0;
        }
    }
}
