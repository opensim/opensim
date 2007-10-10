using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class InstantMessageModule : IRegionModule
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
            get { return "InstantMessageModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }
    }
}