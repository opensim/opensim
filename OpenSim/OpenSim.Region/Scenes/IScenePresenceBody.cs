using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenSim.Region.Scenes
{
    public interface IScenePresenceBody
    {
        void processMovement(IClientAPI remoteClient, uint flags, LLQuaternion bodyRotation);
        void SetAppearance(byte[] texture, AgentSetAppearancePacket.VisualParamBlock[] visualParam);
        void SendOurAppearance(IClientAPI OurClient);
        void SendAppearanceToOtherAgent(ScenePresence avatarInfo);
    }
}
