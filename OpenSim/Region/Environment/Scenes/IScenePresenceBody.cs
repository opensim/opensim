using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;

namespace OpenSim.Region.Environment.Scenes
{
    public interface IScenePresenceBody
    {
        void processMovement(IClientAPI remoteClient, uint flags, LLQuaternion bodyRotation);
        void SetAppearance(byte[] texture, AgentSetAppearancePacket.VisualParamBlock[] visualParam);
        void SendOurAppearance(IClientAPI OurClient);
        void SendAppearanceToOtherAgent(ScenePresence avatarInfo);
    }
}
