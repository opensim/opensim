using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Inventory;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim
{
    partial class ClientView
    {
        public event ChatFromViewer OnChatFromViewer;
        public event RezObject OnRezObject;
        public event GenericCall4 OnDeRezObject;
        public event ModifyTerrain OnModifyTerrain;
        public event GenericCall OnRegionHandShakeReply;
        public event GenericCall OnRequestWearables;
        public event SetAppearance OnSetAppearance;
        public event GenericCall2 OnCompleteMovementToRegion;
        public event GenericCall3 OnAgentUpdate;
        public event StartAnim OnStartAnim;
        public event GenericCall OnRequestAvatarsData;
        public event LinkObjects OnLinkObjects;
        public event GenericCall4 OnAddPrim;
        public event UpdateShape OnUpdatePrimShape;
        public event ObjectSelect OnObjectSelect;
        public event UpdatePrimFlags OnUpdatePrimFlags;
        public event UpdatePrimTexture OnUpdatePrimTexture;
        public event UpdatePrimVector OnUpdatePrimPosition;
        public event UpdatePrimRotation OnUpdatePrimRotation;
        public event UpdatePrimVector OnUpdatePrimScale;
        public event StatusChange OnChildAgentStatus;
        public event GenericCall2 OnStopMovement;

        public LLVector3 StartPos
        {
            get
            {
                return startpos;
            }
            set
            {
                startpos = value;
            }
        }

        public LLUUID AgentId
        {
            get
            {
                return this.AgentID;
            }
        }

        #region World/Avatar to Client
        public void SendChatMessage(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID)
        {
            System.Text.Encoding enc = System.Text.Encoding.ASCII;
            libsecondlife.Packets.ChatFromSimulatorPacket reply = new ChatFromSimulatorPacket();
            reply.ChatData.Audible = 1;
            reply.ChatData.Message = message;
            reply.ChatData.ChatType = type;
            reply.ChatData.SourceType = 1;
            reply.ChatData.Position = fromPos;
            reply.ChatData.FromName = enc.GetBytes(fromName + "\0");
            reply.ChatData.OwnerID = fromAgentID;
            reply.ChatData.SourceID = fromAgentID;

            this.OutPacket(reply);
        }

        public void SendAppearance(AvatarWearable[] wearables)
        {
            AgentWearablesUpdatePacket aw = new AgentWearablesUpdatePacket();
            aw.AgentData.AgentID = this.AgentID;
            aw.AgentData.SerialNum = 0;
            aw.AgentData.SessionID = this.SessionID;

            aw.WearableData = new AgentWearablesUpdatePacket.WearableDataBlock[13];
            AgentWearablesUpdatePacket.WearableDataBlock awb;
            for (int i = 0; i < wearables.Length; i++)
            {
                awb = new AgentWearablesUpdatePacket.WearableDataBlock();
                awb.WearableType = (byte)i;
                awb.AssetID = wearables[i].AssetID;
                awb.ItemID = wearables[i].ItemID;
                aw.WearableData[i] = awb;
            }

            this.OutPacket(aw);
        }
        #endregion

    }
}
