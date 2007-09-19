using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class GroupsModule : IRegionModule
    {
        private Scene m_scene;

        public void Initialise(Scene scene)
        {
            m_scene = scene;
        }

        public void PostInitialise()
        {
        }

        public void CloseDown()
        {
        }

        public string GetName()
        {
            return "GroupsModule";
        }

        public bool IsSharedModule()
        {
            return false;
        }
    }
}