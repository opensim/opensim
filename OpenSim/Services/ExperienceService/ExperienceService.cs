using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using Nini.Config;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.ExperienceService
{
    public class ExperienceService : ExperienceServiceBase, IExperienceService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private IUserAccountService m_UserService = null;

        private const int MAX_QUOTA = 1024 * 1024 * 16;

        public ExperienceService(IConfigSource config)
            : base(config)
        {
            m_log.Debug("[EXPERIENCE SERVICE]: Starting experience service");

            IConfig userConfig = config.Configs["ExperienceService"];
            if (userConfig == null)
                throw new Exception("No ExperienceService configuration");

            string userServiceDll = userConfig.GetString("UserAccountService", string.Empty);
            if (userServiceDll != string.Empty)
                m_UserService = LoadPlugin<IUserAccountService>(userServiceDll, new Object[] { config });

            if (MainConsole.Instance != null)
            {
                MainConsole.Instance.Commands.AddCommand("Experience", false,
                        "create experience",
                        "create experience <first> <last>",
                        "Create a new experience owned by a user.", HandleCreateNewExperience);

                MainConsole.Instance.Commands.AddCommand("Experience", false,
                        "suspend experience",
                        "suspend experience <key> <true/false>",
                        "Suspend/unsuspend an experience by its key.", HandleSuspendExperience);
            }
        }

        private void HandleCreateNewExperience(string module, string[] cmdparams)
        {
            string firstName;
            string lastName;
            string experienceKey;

            if (cmdparams.Length < 3)
                firstName = MainConsole.Instance.Prompt("Experience owner first name", "Test");
            else firstName = cmdparams[2];

            if (cmdparams.Length < 4)
                lastName = MainConsole.Instance.Prompt("Experience owner last name", "Resident");
            else lastName = cmdparams[3];

            if (cmdparams.Length < 5)
                experienceKey = MainConsole.Instance.Prompt("Experience Key (leave blank for random)", "");
            else experienceKey = cmdparams[4];

            UUID newExperienceKey;

            if (experienceKey == "")
                newExperienceKey = UUID.Random();
            else
            {
                if(!UUID.TryParse(experienceKey, out newExperienceKey))
                {
                    MainConsole.Instance.Output("Invalid UUID");
                    return;
                }
            }

            UserAccount account = m_UserService.GetUserAccount(UUID.Zero, firstName, lastName);
            if (account == null)
            {
                MainConsole.Instance.Output("No such user as {0} {1}", firstName, lastName);
                return;
            }

            var existing = GetExperienceInfos(new UUID[] { newExperienceKey });
            if(existing.Length > 0)
            {
                MainConsole.Instance.Output("Experience already exists!");
                return;
            }

            ExperienceInfo new_info = new ExperienceInfo
            {
                public_id = newExperienceKey,
                owner_id = account.PrincipalID
            };

            var stored_info = UpdateExpereienceInfo(new_info);

            if (stored_info == null)
                MainConsole.Instance.Output("Unable to create experience!");
            else
            {
                MainConsole.Instance.Output("Experience created!");
            }
        }

        private void HandleSuspendExperience(string module, string[] cmdparams)
        {
            string experience_key;
            string enabled_str;

            if (cmdparams.Length < 3)
                experience_key = MainConsole.Instance.Prompt("Experience Key");
            else experience_key = cmdparams[2];

            UUID experienceID;
            if(!UUID.TryParse(experience_key, out experienceID))
            {
                MainConsole.Instance.Output("Invalid key!");
                return;
            }

            if (cmdparams.Length < 4)
                enabled_str = MainConsole.Instance.Prompt("Suspended:", "false");
            else enabled_str = cmdparams[3];

            bool suspend = enabled_str == "true";

            var infos = GetExperienceInfos(new UUID[] { experienceID });
            if(infos.Length != 1)
            {
                MainConsole.Instance.Output("Experience not found!");
                return;
            }

            ExperienceInfo info = infos[0];

            bool is_suspended = (info.properties & (int)ExperienceFlags.Suspended) != 0;

            string message = "";
            bool update = false;

            if (suspend && !is_suspended)
            {
                info.properties |= (int)ExperienceFlags.Suspended;
                message = "Experience has been suspended";
                update = true;
            }
            else if(!suspend && is_suspended)
            {
                info.properties &= ~(int)ExperienceFlags.Suspended;
                message = "Experience has been unsuspended";
                update = true;
            }
            else if(suspend && is_suspended)
            {
                message = "Experience is already suspended";
            }
            else if (!suspend && !is_suspended)
            {
                message = "Experience is not suspended";
            }

            if(update)
            {
                var updated = UpdateExpereienceInfo(info);
                if (updated != null)
                {
                    MainConsole.Instance.Output(message);
                }
                else
                    MainConsole.Instance.Output("Error updating experience!");
            }
            else
            {
                MainConsole.Instance.Output(message);
            }
        }

        public Dictionary<UUID, bool> FetchExperiencePermissions(UUID agent_id)
        {
            return m_Database.GetExperiencePermissions(agent_id);
        }

        public ExperienceInfo[] FindExperiencesByName(string search)
        {
            List<ExperienceInfo> infos = new List<ExperienceInfo>();
            ExperienceInfoData[] datas = m_Database.FindExperiences(search);

            foreach (var data in datas)
            {
                ExperienceInfo info = new ExperienceInfo(data.ToDictionary());
                infos.Add(info);
            }

            return infos.ToArray();
        }

        public UUID[] GetAgentExperiences(UUID agent_id)
        {
            return m_Database.GetAgentExperiences(agent_id);
        }

        public ExperienceInfo[] GetExperienceInfos(UUID[] experiences)
        {
            ExperienceInfoData[] datas = m_Database.GetExperienceInfos(experiences);

            List<ExperienceInfo> infos = new List<ExperienceInfo>();

            foreach (var data in datas)
            {
                infos.Add(new ExperienceInfo(data.ToDictionary()));
            }

            return infos.ToArray();
        }

        public UUID[] GetExperiencesForGroups(UUID[] groups)
        {
            return m_Database.GetExperiencesForGroups(groups);
        }

        public UUID[] GetGroupExperiences(UUID group_id)
        {
            return m_Database.GetGroupExperiences(group_id);
        }

        public ExperienceInfo UpdateExpereienceInfo(ExperienceInfo info)
        {
            ExperienceInfoData data = new ExperienceInfoData();

            data.public_id = info.public_id;
            data.owner_id = info.owner_id;
            data.name = info.name;
            data.description = info.description;
            data.group_id = info.group_id;
            data.slurl = info.slurl;
            data.logo = info.logo;
            data.marketplace = info.marketplace;
            data.maturity = info.maturity;
            data.properties = info.properties;

            if (m_Database.UpdateExperienceInfo(data))
            {
                var find = GetExperienceInfos(new UUID[] { data.public_id });
                if(find.Length == 1)
                {
                    return new ExperienceInfo(find[0].ToDictionary());
                }
            }
            return null;
        }

        public bool UpdateExperiencePermissions(UUID agent_id, UUID experience, ExperiencePermission perm)
        {
            if (perm == ExperiencePermission.None)
                return m_Database.ForgetExperiencePermissions(agent_id, experience);
            else return m_Database.SetExperiencePermissions(agent_id, experience, perm == ExperiencePermission.Allowed);
        }

        public string GetKeyValue(UUID experience, string key)
        {
            return m_Database.GetKeyValue(experience, key);
        }

        public string CreateKeyValue(UUID experience, string key, string value)
        {
            int current_size = m_Database.GetKeyValueSize(experience);
            if (current_size + key.Length + value.Length > MAX_QUOTA)
                return "full";

            string get = m_Database.GetKeyValue(experience, key);
            if (get == null)
            {
                if (m_Database.SetKeyValue(experience, key, value))
                    return "success";
                else return "error";
            }
            else return "exists";
        }

        public string UpdateKeyValue(UUID experience, string key, string val, bool check, string original)
        {
            string get = m_Database.GetKeyValue(experience, key);
            if (get != null)
            {
                if (check && get != original)
                    return "mismatch";

                int current_size = m_Database.GetKeyValueSize(experience);
                if ((current_size - get.Length) + val.Length > MAX_QUOTA)
                    return "full";

                if (m_Database.SetKeyValue(experience, key, val))
                    return "success";
                else return "error";
            }
            else return "missing";
        }

        public string DeleteKey(UUID experience, string key)
        {
            string get = m_Database.GetKeyValue(experience, key);
            if (get != null)
            {
                return m_Database.DeleteKey(experience, key) ? "success" : "failed";
            }
            return "missing";
        }

        public int GetKeyCount(UUID experience)
        {
            return m_Database.GetKeyCount(experience);
        }

        public string[] GetKeys(UUID experience, int start, int count)
        {
            return m_Database.GetKeys(experience, start, count);
        }

        public int GetSize(UUID experience)
        {
            return m_Database.GetKeyValueSize(experience);
        }
    }
}
