
using System;
using System.Collections.Generic;
using System.Drawing;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Modules.ModuleFramework;
using System.IO;

namespace OpenSim.Region.Environment.Modules.ExportSerialiser
{
    public class ExportSerialisationModule : IRegionModule
    {
        private List<Scene> m_regions = new List<Scene>();
        private List<IFileSerialiser> m_serialisers = new List<IFileSerialiser>();
        private Commander m_commander = new Commander("Export");
        private string m_savedir = "exports" + "/";

        private List<string> SerialiseRegion(Scene scene)
        {
            List<string> results = new List<string>();

            string saveDir = m_savedir + scene.RegionInfo.RegionID.ToString() + "/";

            if (!Directory.Exists(saveDir))
            {
                Directory.CreateDirectory(saveDir);
            }

            lock (m_serialisers)
            {
                foreach (IFileSerialiser serialiser in m_serialisers)
                {
                    results.Add(serialiser.WriteToFile(scene, saveDir));
                }
            }

            TextWriter regionInfoWriter = new StreamWriter(saveDir + "README.TXT");
            regionInfoWriter.WriteLine("Region Name: " + scene.RegionInfo.RegionName);
            regionInfoWriter.WriteLine("Region ID: " + scene.RegionInfo.RegionID.ToString());
            regionInfoWriter.WriteLine("Backup Time: UTC " + DateTime.UtcNow.ToString());
            regionInfoWriter.WriteLine("Serialise Version: 0.1");
            regionInfoWriter.Close();

            TextWriter manifestWriter = new StreamWriter(saveDir + "region.manifest");
            foreach (string line in results)
            {
                manifestWriter.WriteLine(line);
            }
            manifestWriter.Close();

            return results;
        }


        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource source)
        {
            scene.RegisterModuleCommander("Export", m_commander);
            scene.EventManager.OnPluginConsole += EventManager_OnPluginConsole;

            lock (m_regions)
            {
                m_regions.Add(scene);
            }
        }

        void EventManager_OnPluginConsole(string[] args)
        {
            if (args[0] == "export")
            {
                string[] tmpArgs = new string[args.Length - 2];
                int i = 0;
                for (i = 2; i < args.Length; i++)
                    tmpArgs[i - 2] = args[i];

                m_commander.ProcessConsoleCommand(args[1], tmpArgs);
            }
        }

        private void InterfaceSaveRegion(Object[] args)
        {
            foreach (Scene region in m_regions)
            {
                if (region.RegionInfo.RegionName == (string)args[0])
                {
                    List<string> results = SerialiseRegion(region);
                }
            }
        }

        private void InterfaceSaveAllRegions(Object[] args)
        {
            foreach (Scene region in m_regions)
            {
                List<string> results = SerialiseRegion(region);
            }
        }

        private void LoadCommanderCommands()
        {
            Command serialiseSceneCommand = new Command("save", InterfaceSaveRegion, "Saves the named region into the exports directory.");
            serialiseSceneCommand.AddArgument("region-name", "The name of the region you wish to export", "String");

            Command serialiseAllScenesCommand = new Command("save-all", InterfaceSaveAllRegions, "Saves all regions into the exports directory.");

            m_commander.RegisterCommand("save", serialiseSceneCommand);
            m_commander.RegisterCommand("save-all", serialiseAllScenesCommand);
        }

        public void PostInitialise()
        {
            lock (m_serialisers)
            {
                m_serialisers.Add(new SerialiseTerrain());
                m_serialisers.Add(new SerialiseObjects());
            }

            LoadCommanderCommands();
        }

        public void Close()
        {
            m_regions.Clear();
        }

        public string Name
        {
            get { return "ExportSerialisationModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion
    }
}
