using OpenSim.Framework.Interfaces;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

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

        public bool IsSharedModule()
        {
            return false;
        }

        public void NewClient(IClientAPI client)
        {
        }
    }
}