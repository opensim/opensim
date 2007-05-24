using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Inventory;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Types;

namespace OpenSim.Framework.Interfaces
{
    public delegate void ChatFromViewer(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID);
    public delegate void RezObject(AssetBase primAsset, LLVector3 pos);
    public delegate void ModifyTerrain(byte action, float north, float west);
    public delegate void SetAppearance(byte[] texture, AgentSetAppearancePacket.VisualParamBlock[] visualParam);
    public delegate void StartAnim(LLUUID animID, int seq);
    public delegate void LinkObjects(uint parent, List<uint> children);

    public interface IClientAPI
    {
        event ChatFromViewer OnChatFromViewer;
        event RezObject OnRezObject;
        event ModifyTerrain OnModifyTerrain;
        event SetAppearance OnSetAppearance;
        event StartAnim OnStartAnim;
        event LinkObjects OnLinkObjects;

        void SendAppearance(AvatarWearable[] wearables);
        void SendChatMessage(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID);
    }
}
