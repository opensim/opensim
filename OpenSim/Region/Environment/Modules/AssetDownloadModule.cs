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

        public void Close()
        {
        }

        public string Name
        {
            get { return "AssetDownloadModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        public void NewClient(IClientAPI client)
        {
        }
    }
}