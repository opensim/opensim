using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class FriendsModule : IRegionModule
    {
        private Scene m_scene;

        public void Initialise(Scene scene)
        {
            m_scene = scene;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "FriendsModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }
    }
}