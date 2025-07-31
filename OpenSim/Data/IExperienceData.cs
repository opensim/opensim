using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Data
{
    public interface IExperienceData
    {
        Dictionary<UUID, bool> GetExperiencePermissions(UUID agent_id);
        bool ForgetExperiencePermissions(UUID agent_id, UUID experience_id);
        bool SetExperiencePermissions(UUID agent_id, UUID experience_id, bool allow);

        ExperienceInfoData[] GetExperienceInfos(UUID[] experiences);
        ExperienceInfoData[] FindExperiences(string search);

        UUID[] GetAgentExperiences(UUID agent_id);
        UUID[] GetGroupExperiences(UUID agent_id);
        UUID[] GetExperiencesForGroups(UUID[] groups);

        bool UpdateExperienceInfo(ExperienceInfoData data);

        // KeyValue
        string GetKeyValue(UUID experience, string key);
        bool SetKeyValue(UUID experience, string key, string val);
        bool DeleteKey(UUID experience, string key);
        int GetKeyCount(UUID experience);
        string[] GetKeys(UUID experience, int start, int count);
        int GetKeyValueSize(UUID experience);
    }
}
