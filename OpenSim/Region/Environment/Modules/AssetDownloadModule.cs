using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Interfaces;


namespace OpenSim.Region.Environment.Modules
{
    public class AssetDownloadModule : IRegionModule
    {
        private Scene m_scene;

        public AssetDownloadModule()
        {

        }

        public void Initialise(Scene scene)
        {
            m_scene = scene;
            m_scene.EventManager.OnNewClient += NewClient;
        }

        public void PostInitialise()
        {

        }

        public void CloseDown()
        {
        }

        public string GetName()
        {
            return "AssetDownloadModule";
        }

        public void NewClient(IClientAPI client)
        {
        }
    }
}
