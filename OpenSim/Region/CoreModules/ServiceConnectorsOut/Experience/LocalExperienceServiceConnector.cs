using log4net;
using Mono.Addins;
using Nini.Config;
using System;
using System.Collections.Generic;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Server.Base;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenSim.Data;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Experience
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "LocalExperienceServicesConnector")]
    public class LocalExperienceServicesConnector : ISharedRegionModule, IExperienceService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private List<Scene> m_Scenes = new List<Scene>();
        protected IExperienceService m_service = null;

        private bool m_Enabled = false;

         #region ISharedRegionModule

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public string Name
        {
            get { return "LocalExperienceServicesConnector"; }
        }

        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];

            if (moduleConfig == null)
                return;

            string name = moduleConfig.GetString("ExperienceService", "");
            if(name != Name)
                return;

            IConfig userConfig = source.Configs["ExperienceService"];
            if (userConfig == null)
            {
                m_log.Error("[EXPERIENCE LOCALCONNECTOR]: ExperienceService missing from configuration");
                return;
            }

            string serviceDll = userConfig.GetString("LocalServiceModule",
                    String.Empty);

            if (serviceDll == String.Empty)
            {
                m_log.Error("[EXPERIENCE LOCALCONNECTOR]: No ExperienceModule named in section ExperienceService");
                return;
            }

            Object[] args = new Object[] { source };
            try
            {
                m_service = ServerUtils.LoadPlugin<IExperienceService>(serviceDll, args);
            }
            catch
            {
                m_log.Error("[EXPERIENCE LOCALCONNECTOR]: Failed to load experience service");
                return;
            }

            if (m_service == null)
            {
                m_log.Error("[EXPERIENCE LOCALCONNECTOR]: Can't load experience service");
                return;
            }

            m_Enabled = true;
            m_log.Info("[EXPERIENCE LOCALCONNECTOR]: Enabled!");
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock(m_Scenes)
            {
                m_Scenes.Add(scene);
                scene.RegisterModuleInterface<IExperienceService>(this);
            }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void PostInitialise()
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock(m_Scenes)
            {
                if (m_Scenes.Contains(scene))
                {
                    m_Scenes.Remove(scene);
                    scene.UnregisterModuleInterface<IExperienceService>(this);
                }
            }
        }

        #endregion ISharedRegionModule

        #region IExperienceService
        public Dictionary<UUID, bool> FetchExperiencePermissions(UUID agent_id)
        {
            return m_service.FetchExperiencePermissions(agent_id);
        }

        public bool UpdateExperiencePermissions(UUID agent_id, UUID experience, ExperiencePermission perm)
        {
            return m_service.UpdateExperiencePermissions(agent_id, experience, perm);
        }

        public ExperienceInfo[] GetExperienceInfos(UUID[] experiences)
        {
            return m_service.GetExperienceInfos(experiences);
        }

        public UUID[] GetAgentExperiences(UUID agent_id)
        {
            return m_service.GetAgentExperiences(agent_id);
        }

        public ExperienceInfo UpdateExpereienceInfo(ExperienceInfo info)
        {
            return m_service.UpdateExpereienceInfo(info);
        }

        public ExperienceInfo[] FindExperiencesByName(string search)
        {
            return m_service.FindExperiencesByName(search);
        }

        public UUID[] GetGroupExperiences(UUID group_id)
        {
            return m_service.GetGroupExperiences(group_id);
        }

        public UUID[] GetExperiencesForGroups(UUID[] groups)
        {
            return m_service.GetExperiencesForGroups(groups);
        }

        public string GetKeyValue(UUID experience, string key)
        {
            return m_service.GetKeyValue(experience, key);
        }

        public string CreateKeyValue(UUID experience, string key, string value)
        {
            return m_service.CreateKeyValue(experience, key, value);
        }

        public string UpdateKeyValue(UUID experience, string key, string val, bool check, string original)
        {
            return m_service.UpdateKeyValue(experience, key, val, check, original);
        }

        public string DeleteKey(UUID experience, string key)
        {
            return m_service.DeleteKey(experience, key);
        }

        public int GetKeyCount(UUID experience)
        {
            return m_service.GetKeyCount(experience);
        }

        public string[] GetKeys(UUID experience, int start, int count)
        {
            return m_service.GetKeys(experience, start, count);
        }

        public int GetSize(UUID experience)
        {
            return m_service.GetSize(experience);
        }
        #endregion IExperienceService
    }
}
