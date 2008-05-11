using libsecondlife;
using Nini.Config;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.World.NPC
{
    public class NPCModule : IRegionModule
    {
        private const bool m_enabled = false;

        public void Initialise(Scene scene, IConfigSource source)
        {
            if (m_enabled)
            {
                NPCAvatar testAvatar = new NPCAvatar("Jack", "NPC", new LLVector3(128, 128, 40), scene);
                NPCAvatar testAvatar2 = new NPCAvatar("Jill", "NPC", new LLVector3(136, 128, 40), scene);
                scene.AddNewClient(testAvatar, false);
                scene.AddNewClient(testAvatar2, false);
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
            get { return "NPCModule"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
    }
}
