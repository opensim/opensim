using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors;

using OpenMetaverse;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Data;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Experience
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "RemoteExperienceServicesConnector")]
    public class RemoteExperienceServicesConnector : ISharedRegionModule, IExperienceService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region ISharedRegionModule

        private bool m_Enabled = false;

        private IExperienceService m_remoteConnector;

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "RemoteExperienceServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("ExperienceServices", "");
                if (name == Name)
                {

                    m_remoteConnector = new ExperienceServicesConnector(source);
                    m_Enabled = true;

                    m_log.Info("[EXPERIENCE CONNECTOR]: Remote ExperienceService enabled");
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.RegisterModuleInterface<IExperienceService>(this);
            m_log.InfoFormat("[EXPERIENCE CONNECTOR]: Enabled for region {0}", scene.RegionInfo.RegionName);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;
        }

        #endregion

        #region IExperienceService
        public Dictionary<UUID, bool> FetchExperiencePermissions(UUID agent_id)
        {
            return m_remoteConnector.FetchExperiencePermissions(agent_id);
        }

        public bool UpdateExperiencePermissions(UUID agent_id, UUID experience, ExperiencePermission perm)
        {
            return m_remoteConnector.UpdateExperiencePermissions(agent_id, experience, perm);
        }

        public ExperienceInfo[] GetExperienceInfos(UUID[] experiences)
        {
            return m_remoteConnector.GetExperienceInfos(experiences);
        }

        public UUID[] GetAgentExperiences(UUID agent_id)
        {
            return m_remoteConnector.GetAgentExperiences(agent_id);
        }

        public ExperienceInfo UpdateExpereienceInfo(ExperienceInfo info)
        {
            return m_remoteConnector.UpdateExpereienceInfo(info);
        }

        public ExperienceInfo[] FindExperiencesByName(string search)
        {
            return m_remoteConnector.FindExperiencesByName(search);
        }

        public UUID[] GetGroupExperiences(UUID group_id)
        {
            return m_remoteConnector.GetGroupExperiences(group_id);
        }

        public UUID[] GetExperiencesForGroups(UUID[] groups)
        {
            return m_remoteConnector.GetExperiencesForGroups(groups);
        }

        public string GetKeyValue(UUID experience, string key)
        {
            return m_remoteConnector.GetKeyValue(experience, key);
        }

        public string CreateKeyValue(UUID experience, string key, string value)
        {
            return m_remoteConnector.CreateKeyValue(experience, key, value);
        }

        public string UpdateKeyValue(UUID experience, string key, string val, bool check, string original)
        {
            return m_remoteConnector.UpdateKeyValue(experience, key, val, check, original);
        }

        public string DeleteKey(UUID experience, string key)
        {
            return m_remoteConnector.DeleteKey(experience, key);
        }

        public int GetKeyCount(UUID experience)
        {
            return m_remoteConnector.GetKeyCount(experience);
        }

        public string[] GetKeys(UUID experience, int start, int count)
        {
            return m_remoteConnector.GetKeys(experience, start, count);
        }

        public int GetSize(UUID experience)
        {
            return m_remoteConnector.GetSize(experience);
        }
        #endregion IExperienceService

    }
}
