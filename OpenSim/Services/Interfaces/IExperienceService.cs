using System;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Services.Interfaces
{
    public enum ExperiencePermission
    {
        None,
        Allowed,
        Blocked
    }

    public enum ExperienceFlags
    {
        None = 0,
        Invalid = 1 << 0,
        Privileged = 1 << 3,
        Grid = 1 << 4,
        Private = 1 << 5,
        Disabled = 1 << 6,
        Suspended = 1 << 7
    }

    public class ExperienceInfo
    {
        public UUID public_id = UUID.Zero;
        public UUID owner_id = UUID.Zero;

        public string name = string.Empty;
        public string description = string.Empty;

        public UUID group_id = UUID.Zero;

        // extended
        public UUID logo = UUID.Zero;
        public string marketplace = string.Empty;

        public string slurl = string.Empty;

        public int properties = 0;
        public int maturity = 0;

        public DateTime CachedTime = DateTime.Now;

        public int quota = 16;

        public Dictionary<string, object> ToDictionary()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            dict["public_id"] = public_id;
            dict["owner_id"] = owner_id;
            dict["group_id"] = group_id;
            dict["name"] = name;
            dict["description"] = description;
            dict["logo"] = logo;
            dict["marketplace"] = marketplace;
            dict["slurl"] = slurl;
            dict["properties"] = properties;
            dict["maturity"] = maturity;
            return dict;
        }

        public ExperienceInfo()
        {

        }

        public ExperienceInfo(Dictionary<string, object> data)
        {
            if (data.ContainsKey("public_id"))
                public_id = UUID.Parse(data["public_id"].ToString());
            if (data.ContainsKey("owner_id"))
                owner_id = UUID.Parse(data["owner_id"].ToString());
            if (data.ContainsKey("group_id"))
                group_id = UUID.Parse(data["group_id"].ToString());
            if (data.ContainsKey("name"))
                name = data["name"].ToString();
            if (data.ContainsKey("description"))
                description = data["description"].ToString();
            if (data.ContainsKey("logo"))
                logo = UUID.Parse(data["logo"].ToString());
            if (data.ContainsKey("marketplace"))
                marketplace = data["marketplace"].ToString();
            if (data.ContainsKey("slurl"))
                slurl = data["slurl"].ToString();
            if (data.ContainsKey("properties"))
                properties = int.Parse(data["properties"].ToString());
            if (data.ContainsKey("maturity"))
                maturity = int.Parse(data["maturity"].ToString());
        }
    }

    public interface IExperienceService
    {
        Dictionary<UUID, bool> FetchExperiencePermissions(UUID agent_id);
        bool UpdateExperiencePermissions(UUID agent_id, UUID experience, ExperiencePermission perm);
        ExperienceInfo[] GetExperienceInfos(UUID[] experiences);
        UUID[] GetAgentExperiences(UUID agent_id);
        ExperienceInfo UpdateExpereienceInfo(ExperienceInfo info);
        ExperienceInfo[] FindExperiencesByName(string search);
        UUID[] GetGroupExperiences(UUID group_id);
        UUID[] GetExperiencesForGroups(UUID[] groups);

        string GetKeyValue(UUID experience, string key);
        string CreateKeyValue(UUID experience, string key, string value);
        string UpdateKeyValue(UUID experience, string key, string val, bool check, string original);
        string DeleteKey(UUID experience, string key);
        int GetKeyCount(UUID experience);
        string[] GetKeys(UUID experience, int start, int count);
        int GetSize(UUID experience);
    }
}
