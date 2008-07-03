using System;
using System.Reflection;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using log4net;
using Nini.Config;

namespace OpenSim.Region.Environment.Modules.Scripting.EmailModules
{
    public class EmailModule : IEmailModule
    {
        //
        // Log
        //
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //
        // Module vars
        //
        private IConfigSource m_Config;

        // Scenes by Region Handle
        private Dictionary<ulong, Scene> m_Scenes =
                new Dictionary<ulong, Scene>();

		private bool m_Enabled = false;

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_Config = config;

            IConfig startupConfig = m_Config.Configs["Startup"];

			m_Enabled = (startupConfig.GetString("emailmodule",
					"DefaultEmailModule") == "DefaultEmailModule");

            // It's a go!
            if (m_Enabled)
            {
                lock (m_Scenes)
                {
                    // Claim the interface slot
                    scene.RegisterModuleInterface<IEmailModule>(this);

                    // Add to scene list
                    if (m_Scenes.ContainsKey(scene.RegionInfo.RegionHandle))
                    {
                        m_Scenes[scene.RegionInfo.RegionHandle] = scene;
                    }
                    else
                    {
                        m_Scenes.Add(scene.RegionInfo.RegionHandle, scene);
                    }
                }

                m_log.Info("[EMAIL] Activated DefaultEmailModule");
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "DefaultEmailModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        public void SendEmail(LLUUID objectID, string address, string subject, string body)
		{
		}

		public Email GetNextEmail(LLUUID objectID, string sender, string subject)
		{
			return null;
		}
	}
}
