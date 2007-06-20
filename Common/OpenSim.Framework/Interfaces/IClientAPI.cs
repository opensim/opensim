/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
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
    public delegate void RequestMapBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY);
    public delegate void TeleportLocationRequest(IClientAPI remoteClient, ulong regionHandle, LLVector3 position, LLVector3 lookAt, uint flags);

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
    public delegate void UpdateAgent(IClientAPI remoteClient, uint flags, LLQuaternion bodyRotation);

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
        event RequestMapBlocks OnRequestMapBlocks;
        event TeleportLocationRequest OnTeleportLocationRequest;

        event GenericCall4 OnDeRezObject;
        event GenericCall OnRegionHandShakeReply;
        event GenericCall OnRequestWearables;
        event GenericCall2 OnCompleteMovementToRegion;
        event UpdateAgent OnAgentUpdate;
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
        void SendLayerData(int px, int py, float[] map);
        void MoveAgentIntoRegion(RegionInfo regInfo, LLVector3 pos, LLVector3 look);
        void InformClientOfNeighbour(ulong neighbourHandle, System.Net.IPAddress neighbourIP, ushort neighbourPort);
        AgentCircuitData RequestClientInfo();
        void CrossRegion(ulong newRegionHandle, LLVector3 pos, LLVector3 lookAt, System.Net.IPAddress newRegionIP, ushort newRegionPort);
        void SendMapBlock(List<MapBlockData> mapBlocks);
        void SendLocalTeleport(LLVector3 position, LLVector3 lookAt, uint flags);
        void SendRegionTeleport(ulong regionHandle, byte simAccess, string ipAddress, ushort ipPort, uint locationID, uint flags);
        void SendTeleportCancel();
        void SendTeleportLocationStart();

        void SendAvatarData(ulong regionHandle, string firstName, string lastName, LLUUID avatarID, uint avatarLocalID, LLVector3 Pos);
        void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position, LLVector3 velocity);

        void AttachObject(uint localID, LLQuaternion rotation, byte attachPoint);
        void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimData primData, LLVector3 pos, LLQuaternion rotation, LLUUID textureID);
        void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimData primData, LLVector3 pos, LLUUID textureID);
        void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position, LLQuaternion rotation);
    }
}
