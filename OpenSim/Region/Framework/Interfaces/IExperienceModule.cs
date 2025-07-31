using OpenMetaverse;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IExperienceModule
    {
        ExperiencePermission GetExperiencePermission(UUID avatar_id, UUID experience_id);
        bool SetExperiencePermission(UUID avatar_id, UUID experience_id, ExperiencePermission perm);
        bool SetExperiencePermissions(UUID avatar_id, UUID experience_id, bool allow);
        bool ForgetExperiencePermissions(UUID avatar_id, UUID experience_id);

        UUID[] GetAllowedExperiences(UUID avatar_id);
        UUID[] GetBlockedExperiences(UUID avatar_id);

        UUID[] GetAgentExperiences(UUID agent_id);
        UUID[] GetAdminExperiences(UUID agent_id);
        UUID[] GetConributorExperiences(UUID agent_id);

        ExperienceInfo GetExperienceInfo(UUID experience_id, bool fetch = false);
        ExperienceInfo[] GetExperienceInfos(UUID[] experience_ids, bool fetch = false);

        ExperienceInfo[] FindExperiencesByName(string query);

        UUID[] GetGroupExperiences(UUID group_id);

        ExperienceInfo UpdateExperienceInfo(ExperienceInfo info);

        bool IsExperienceAdmin(UUID agent_id, UUID experience_id);
        bool IsExperienceContributor(UUID agent_id, UUID experience_id);

        UUID[] GetEstateAllowedExperiences();
        UUID[] GetEstateKeyExperiences();

        bool IsExperienceEnabled(UUID experience_id);

        string GetKeyValue(UUID experience, string key);
        string CreateKeyValue(UUID experience, string key, string value);
        string UpdateKeyValue(UUID experience, string key, string val, bool check, string original);
        string DeleteKey(UUID experience, string key);
        int GetKeyCount(UUID experience);
        string[] GetKeys(UUID experience, int start, int count);
        int GetSize(UUID experience);
    }
}
