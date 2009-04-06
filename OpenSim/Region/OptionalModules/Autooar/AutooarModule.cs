using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Timers;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Autooar
{
    public class AutooarModule : IRegionModule
    {
        private readonly Timer m_timer = new Timer(60000*20);
        private readonly List<Scene> m_scenes = new List<Scene>();
        private IConfigSource config;
        private bool m_enabled = false;
        

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scenes.Add(scene);
            config = source;
        }

        public void PostInitialise()
        {
            if(config.Configs["autooar"] != null)
            {
                m_enabled = config.Configs["autooar"].GetBoolean("Enabled", m_enabled);
            }

            if(m_enabled)
            {
                m_timer.Elapsed += m_timer_Elapsed;
                m_timer.AutoReset = true;
                m_timer.Start();
            }
        }

        void m_timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!Directory.Exists("autooars"))
                Directory.CreateDirectory("autooars");

            foreach (Scene scene in m_scenes)
            {
                IRegionArchiverModule archiver = scene.RequestModuleInterface<IRegionArchiverModule>();

                archiver.ArchiveRegion(Path.Combine("autooars",
                                                    scene.RegionInfo.RegionName + "_" + scene.RegionInfo.RegionLocX +
                                                    "x" + scene.RegionInfo.RegionLocY + ".oar.tar.gz"));
            }
        }

        public void Close()
        {
            if (m_timer.Enabled)
                m_timer.Stop();
        }

        public string Name
        {
            get { return "Automatic OAR Module"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
    }
}
