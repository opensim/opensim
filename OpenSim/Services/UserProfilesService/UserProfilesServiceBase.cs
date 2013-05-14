using System;
using System.Reflection;
using Nini.Config;
using log4net;
using OpenSim.Services.Base;
using OpenSim.Data;

namespace OpenSim.Services.ProfilesService
{
    public class UserProfilesServiceBase: ServiceBase
    {
        static readonly ILog m_log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        public IProfilesData ProfilesData;

        public string ConfigName
        {
            get; private set;
        }

        public UserProfilesServiceBase(IConfigSource config, string configName):
            base(config)
        {
            if(string.IsNullOrEmpty(configName))
            {
                m_log.WarnFormat("[PROFILES]: Configuration section not given!");
                return;
            }

            string dllName = String.Empty;
            string connString = null;
            string realm = String.Empty;

            IConfig dbConfig = config.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                if (dllName == String.Empty)
                    dllName = dbConfig.GetString("StorageProvider", String.Empty);
                if (string.IsNullOrEmpty(connString))
                    connString = dbConfig.GetString("ConnectionString", String.Empty);
            }
            
            IConfig ProfilesConfig = config.Configs[configName];
            if (ProfilesConfig != null)
            {
                connString = ProfilesConfig.GetString("ConnectionString", connString);
                realm = ProfilesConfig.GetString("Realm", realm);
            }
            
            ProfilesData = LoadPlugin<IProfilesData>(dllName, new Object[] { connString });
            if (ProfilesData == null)
                throw new Exception("Could not find a storage interface in the given module");

        }
    }
}

