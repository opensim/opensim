using System;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Data;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Base;

namespace OpenSim.Services.ExperienceService
{
    public class ExperienceServiceBase : ServiceBase
    {
        protected IExperienceData m_Database = null;

        public ExperienceServiceBase(IConfigSource config)
            : base(config)
        {
            string dllName = string.Empty;
            string connString = string.Empty;

            //
            // Try reading the [DatabaseService] section, if it exists
            //
            IConfig dbConfig = config.Configs["DatabaseService"];
            if (dbConfig != null)
            {
                if (dllName == string.Empty)
                    dllName = dbConfig.GetString("StorageProvider", string.Empty);
                if (connString == string.Empty)
                    connString = dbConfig.GetString("ConnectionString", string.Empty);
            }

            //
            // [ExperienceService] section overrides [DatabaseService], if it exists
            //
            IConfig presenceConfig = config.Configs["ExperienceService"];
            if (presenceConfig != null)
            {
                dllName = presenceConfig.GetString("StorageProvider", dllName);
                connString = presenceConfig.GetString("ConnectionString", connString);
            }

            //
            // We tried, but this doesn't exist. We can't proceed.
            //
            if (dllName.Equals(string.Empty))
                throw new Exception("No StorageProvider configured");

            m_Database = LoadPlugin<IExperienceData>(dllName, new object[] { connString });
            if (m_Database == null)
                throw new Exception("Could not find a storage interface in the given module " + dllName);

        }
    }
}
