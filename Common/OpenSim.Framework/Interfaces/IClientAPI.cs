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
    public delegate void GenericCall(IClientAPI remoteClient);
    public delegate void GenericCall2();
    public delegate void GenericCall3(Packet packet); // really don't want to be passing packets in these events, so this is very temporary.
    public delegate void GenericCall4(Packet packet, IClientAPI remoteClient);
    public delegate void GenericCall5(IClientAPI remoteClient, bool status);
    public delegate void GenericCall6(LLUUID uid);
    public delegate void UpdateShape(uint localID, ObjectShapePacket.ObjectDataBlock shapeBlock);
    public delegate void ObjectSelect(uint localID, IClientAPI remoteClient);
    public delegate void UpdatePrimFlags(uint localID, Packet packet, IClientAPI remoteClient);
    public delegate void UpdatePrimTexture(uint localID, byte[] texture, IClientAPI remoteClient);
    public delegate void UpdatePrimVector(uint localID, LLVector3 pos, IClientAPI remoteClient);
    public delegate void UpdatePrimRotation(uint localID, LLQuaternion rot, IClientAPI remoteClient);
    public delegate void StatusChange(bool status);
    public delegate void NewAvatar(IClientAPI remoteClient, LLUUID agentID, bool status);

    public delegate void ParcelPropertiesRequest(int start_x, int start_y, int end_x, int end_y, int sequence_id, bool snap_selection, IClientAPI remote_client);
    public delegate void ParcelDivideRequest(int west, int south, int east, int north, IClientAPI remote_client);
    public delegate void ParcelJoinRequest(int west, int south, int east, int north, IClientAPI remote_client);
    public delegate void ParcelPropertiesUpdateRequest(ParcelPropertiesUpdatePacket packet, IClientAPI remote_client); // NOTETOSELFremove the packet part

    public delegate void EstateOwnerMessageRequest(EstateOwnerMessagePacket packet, IClientAPI remote_client);

    public interface IClientAPI
    {
        event ChatFromViewer OnChatFromViewer;
        event RezObject OnRezObject;
        event ModifyTerrain OnModifyTerrain;
        event SetAppearance OnSetAppearance;
        event StartAnim OnStartAnim;
        event LinkObjects OnLinkObjects;
        event GenericCall4 OnDeRezObject;
        event GenericCall OnRegionHandShakeReply;
        event GenericCall OnRequestWearables;
        event GenericCall2 OnCompleteMovementToRegion;
        event GenericCall3 OnAgentUpdate;
        event GenericCall OnRequestAvatarsData;
        event GenericCall4 OnAddPrim;
        event UpdateShape OnUpdatePrimShape;
        event ObjectSelect OnObjectSelect;
        event UpdatePrimFlags OnUpdatePrimFlags;
        event UpdatePrimTexture OnUpdatePrimTexture;
        event UpdatePrimVector OnUpdatePrimPosition;
        event UpdatePrimRotation OnUpdatePrimRotation;
        event UpdatePrimVector OnUpdatePrimScale;
        event StatusChange OnChildAgentStatus;
        event GenericCall2 OnStopMovement;
        event NewAvatar OnNewAvatar;
        event GenericCall6 OnRemoveAvatar;

        event ParcelPropertiesRequest OnParcelPropertiesRequest;
        event ParcelDivideRequest OnParcelDivideRequest;
        event ParcelJoinRequest OnParcelJoinRequest;
        event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest;

        event EstateOwnerMessageRequest OnEstateOwnerMessage;

        LLVector3 StartPos
        {
            get;
            set;
        }

        LLUUID AgentId
        {
            get;
        }

        string FirstName
        {
            get;
        }

        string LastName
        {
            get;
        }

        void OutPacket(Packet newPack);
        void SendWearables(AvatarWearable[] wearables);
        void SendRegionHandshake(RegionInfo regionInfo);
        void SendChatMessage(string message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID);
        void SendChatMessage(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID);
        void SendLayerData(float[] map);
        void MoveAgentIntoRegion(RegionInfo regInfo);
        void SendAvatarData(RegionInfo regionInfo, string firstName, string lastName, LLUUID avatarID, uint avatarLocalID, LLVector3 Pos);
        void InformClientOfNeighbour(ulong neighbourHandle, System.Net.IPAddress neighbourIP, ushort neighbourPort);
        AgentCircuitData RequestClientInfo();

        void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimData primData, LLVector3 pos, LLUUID textureID);
    }
}
