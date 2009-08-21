using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Avatar.NPC
{
    public interface INPCModule
    {
        UUID CreateNPC(string firstname, string lastname, Vector3 position, Scene scene, UUID cloneAppearanceFrom);
        void Autopilot(UUID agentID, Scene scene, Vector3 pos);
        void Say(UUID agentID, Scene scene, string text);
        void DeleteNPC(UUID agentID, Scene scene);
    }
}