/*
* Copyright (c) Contributors, http://opensimulator.org/
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
using System.Net;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.Framework
{
    // Base Args Interface
    public interface IEventArgs
    {
        IScene Scene { get; set; }

        IClientAPI Sender { get; set; }
    }

    public delegate void ViewerEffectEventHandler(IClientAPI sender, ViewerEffectPacket.EffectBlock[] effectBlock);

    public delegate void ChatFromViewer(Object sender, ChatFromViewerArgs e);

    public enum ChatTypeEnum
    {
        Whisper = 0,
        Say = 1,
        Shout = 2,
        StartTyping = 4,
        StopTyping = 5,
        Broadcast = 0xFF
    } ;

    /// <summary>
    /// ChatFromViewer Arguments
    /// </summary>
    public class ChatFromViewerArgs : EventArgs, IEventArgs
    {
        protected string m_message;
        protected ChatTypeEnum m_type;
        protected int m_channel;
        protected LLVector3 m_position;
        protected string m_from;

        protected IClientAPI m_sender;
        protected IScene m_scene;

        /// <summary>
        /// The message sent by the user
        /// </summary>
        public string Message
        {
            get { return m_message; }
            set { m_message = value; }
        }

        /// <summary>
        /// The type of message, eg say, shout, broadcast.
        /// </summary>
        public ChatTypeEnum Type
        {
            get { return m_type; }
            set { m_type = value; }
        }

        /// <summary>
        /// Which channel was this message sent on? Different channels may have different listeners. Public chat is on channel zero.
        /// </summary>
        public int Channel
        {
            get { return m_channel; }
            set { m_channel = value; }
        }

        /// <summary>
        /// The position of the sender at the time of the message broadcast.
        /// </summary>
        public LLVector3 Position
        {
            get { return m_position; }
            set { m_position = value; }
        }

        /// <summary>
        /// The name of the sender (needed for scripts)
        /// </summary>
        public string From
        {
            get { return m_from; }
            set { m_from = value; }
        }

        /// <summary>
        /// The client responsible for sending the message, or null.
        /// </summary>
        public IClientAPI Sender
        {
            get { return m_sender; }
            set { m_sender = value; }
        }

        public IScene Scene
        {
            get { return m_scene; }
            set { m_scene = value; }
        }

        public ChatFromViewerArgs()
        {
            m_position = new LLVector3();
        }
    }

    public class TextureRequestArgs : EventArgs
    {
        protected LLUUID m_requestedAssetID;
        private sbyte m_discardLevel;
        private uint m_packetNumber;

        public uint PacketNumber
        {
            get { return m_packetNumber; }
            set { m_packetNumber = value; }
        }

        public sbyte DiscardLevel
        {
            get { return m_discardLevel; }
            set { m_discardLevel = value; }
        }

        public LLUUID RequestedAssetID
        {
            get { return m_requestedAssetID; }
            set { m_requestedAssetID = value; }
        }
    }

    public delegate void TextureRequest(Object sender, TextureRequestArgs e);

    public delegate void ImprovedInstantMessage(
        LLUUID fromAgentID, LLUUID fromAgentSession, LLUUID toAgentID, LLUUID imSessionID, uint timestamp,
        string fromAgentName, string message, byte dialog); // Cut down from full list
    public delegate void RezObject(IClientAPI remoteClient, LLUUID itemID, LLVector3 pos);

    public delegate void ModifyTerrain(
        float height, float seconds, byte size, byte action, float north, float west, IClientAPI remoteClient);

    public delegate void SetAppearance(byte[] texture, AgentSetAppearancePacket.VisualParamBlock[] visualParam);

    public delegate void StartAnim(IClientAPI remoteClient, LLUUID animID, int seq);

    public delegate void LinkObjects(uint parent, List<uint> children);
    
    public delegate void DelinkObjects(List<uint> primIds);

    public delegate void RequestMapBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY);

    public delegate void TeleportLocationRequest(
        IClientAPI remoteClient, ulong regionHandle, LLVector3 position, LLVector3 lookAt, uint flags);

    public delegate void DisconnectUser();

    public delegate void RequestAvatarProperties(IClientAPI remoteClient, LLUUID avatarID);
    public delegate void SetAlwaysRun (IClientAPI remoteClient, bool SetAlwaysRun);

    public delegate void GenericCall2();

    // really don't want to be passing packets in these events, so this is very temporary.
    public delegate void GenericCall4(Packet packet, IClientAPI remoteClient);

    public delegate void GenericCall5(IClientAPI remoteClient, bool status);

    public delegate void GenericCall7(uint localID, string message);

    public delegate void UpdateShape(uint localID, ObjectShapePacket.ObjectDataBlock shapeBlock);

    public delegate void ObjectExtraParams(uint localID, ushort type, bool inUse, byte[] data);

    public delegate void ObjectSelect(uint localID, IClientAPI remoteClient);

    public delegate void RequestObjectPropertiesFamily(IClientAPI remoteClient,LLUUID AgentID, uint RequestFlags, LLUUID TaskID);

    public delegate void ObjectDeselect(uint localID, IClientAPI remoteClient);

    public delegate void UpdatePrimFlags(uint localID, Packet packet, IClientAPI remoteClient);

    public delegate void UpdatePrimTexture(uint localID, byte[] texture, IClientAPI remoteClient);

    public delegate void UpdateVector(uint localID, LLVector3 pos, IClientAPI remoteClient);

    public delegate void UpdatePrimRotation(uint localID, LLQuaternion rot, IClientAPI remoteClient);

    public delegate void UpdatePrimSingleRotation(uint localID, LLQuaternion rot, IClientAPI remoteClient);

    public delegate void UpdatePrimGroupRotation(uint localID, LLVector3 pos, LLQuaternion rot, IClientAPI remoteClient);

    public delegate void ObjectDuplicate(uint localID, LLVector3 offset, uint dupeFlags, LLUUID AgentID, LLUUID GroupID);

    public delegate void StatusChange(bool status);

    public delegate void NewAvatar(IClientAPI remoteClient, LLUUID agentID, bool status);

    public delegate void UpdateAgent(IClientAPI remoteClient, uint flags, LLQuaternion bodyRotation);

    public delegate void AgentRequestSit(IClientAPI remoteClient, LLUUID agentID, LLUUID targetID, LLVector3 offset);

    public delegate void AgentSit(IClientAPI remoteClient, LLUUID agentID);
    public delegate void AvatarPickerRequest(IClientAPI remoteClient, LLUUID agentdata, LLUUID queryID, string UserQuery);

    public delegate void MoveObject(LLUUID objectID, LLVector3 offset, LLVector3 grapPos, IClientAPI remoteClient);

    public delegate void ParcelPropertiesRequest(
        int start_x, int start_y, int end_x, int end_y, int sequence_id, bool snap_selection, IClientAPI remote_client);

    public delegate void ParcelDivideRequest(int west, int south, int east, int north, IClientAPI remote_client);

    public delegate void ParcelJoinRequest(int west, int south, int east, int north, IClientAPI remote_client);

    public delegate void ParcelPropertiesUpdateRequest(ParcelPropertiesUpdatePacket packet, IClientAPI remote_client);

    public delegate void ParcelSelectObjects(int land_local_id, int request_type, IClientAPI remote_client);

    public delegate void ParcelObjectOwnerRequest(int local_id, IClientAPI remote_client);

    public delegate void EstateOwnerMessageRequest(EstateOwnerMessagePacket packet, IClientAPI remote_client);

    public delegate void UUIDNameRequest(LLUUID id, IClientAPI remote_client);

    public delegate void AddNewPrim(LLUUID ownerID, LLVector3 pos, LLQuaternion rot, PrimitiveBaseShape shape);

    public delegate void CreateInventoryFolder(
        IClientAPI remoteClient, LLUUID folderID, ushort folderType, string folderName, LLUUID parentID);

    public delegate void CreateNewInventoryItem(
        IClientAPI remoteClient, LLUUID transActionID, LLUUID folderID, uint callbackID, string description, string name,
        sbyte invType, sbyte type, byte wearableType, uint nextOwnerMask);

    public delegate void FetchInventoryDescendents(
        IClientAPI remoteClient, LLUUID folderID, LLUUID ownerID, bool fetchFolders, bool fetchItems, int sortOrder);

    public delegate void FetchInventory(IClientAPI remoteClient, LLUUID itemID, LLUUID ownerID);

    public delegate void RequestTaskInventory(IClientAPI remoteClient, uint localID);

    public delegate void UpdateInventoryItemTransaction(
        IClientAPI remoteClient, LLUUID transactionID, LLUUID assetID, LLUUID itemID);

    public delegate void RezScript(IClientAPI remoteClient, LLUUID itemID, uint localID);

    public delegate void UpdateTaskInventory(IClientAPI remoteClient, LLUUID itemID, LLUUID folderID, uint localID);

    public delegate void RemoveTaskInventory(IClientAPI remoteClient, LLUUID itemID, uint localID);

    public delegate void UDPAssetUploadRequest(
        IClientAPI remoteClient, LLUUID assetID, LLUUID transaction, sbyte type, byte[] data, bool storeLocal);

    public delegate void XferReceive(IClientAPI remoteClient, ulong xferID, uint packetID, byte[] data);

    public delegate void RequestXfer(IClientAPI remoteClient, ulong xferID, string fileName);

    public delegate void ConfirmXfer(IClientAPI remoteClient, ulong xferID, uint packetID);

    public interface IClientAPI
    {
        event ImprovedInstantMessage OnInstantMessage;
        event ChatFromViewer OnChatFromViewer;
        event TextureRequest OnRequestTexture;
        event RezObject OnRezObject;
        event ModifyTerrain OnModifyTerrain;
        event SetAppearance OnSetAppearance;
        event StartAnim OnStartAnim;
        event LinkObjects OnLinkObjects;
        event DelinkObjects OnDelinkObjects;
        event RequestMapBlocks OnRequestMapBlocks;
        event TeleportLocationRequest OnTeleportLocationRequest;
        event DisconnectUser OnDisconnectUser;
        event RequestAvatarProperties OnRequestAvatarProperties;
        event SetAlwaysRun OnSetAlwaysRun;
        event GenericCall4 OnDeRezObject;
        event Action<IClientAPI> OnRegionHandShakeReply;
        event GenericCall2 OnRequestWearables;
        event GenericCall2 OnCompleteMovementToRegion;
        event UpdateAgent OnAgentUpdate;
        event AgentRequestSit OnAgentRequestSit;
        event AgentSit OnAgentSit;
        event AvatarPickerRequest OnAvatarPickerRequest;
        event Action<IClientAPI> OnRequestAvatarsData;
        event AddNewPrim OnAddPrim;
        event ObjectDuplicate OnObjectDuplicate;
        event UpdateVector OnGrabObject;
        event ObjectSelect OnDeGrabObject;
        event MoveObject OnGrabUpdate;

        event UpdateShape OnUpdatePrimShape;
        event ObjectExtraParams OnUpdateExtraParams;
        event ObjectSelect OnObjectSelect;
        event ObjectDeselect OnObjectDeselect;
        event GenericCall7 OnObjectDescription;
        event GenericCall7 OnObjectName;
        event RequestObjectPropertiesFamily OnRequestObjectPropertiesFamily;
        event UpdatePrimFlags OnUpdatePrimFlags;
        event UpdatePrimTexture OnUpdatePrimTexture;
        event UpdateVector OnUpdatePrimGroupPosition;
        event UpdateVector OnUpdatePrimSinglePosition;
        event UpdatePrimRotation OnUpdatePrimGroupRotation;
        event UpdatePrimSingleRotation OnUpdatePrimSingleRotation;
        event UpdatePrimGroupRotation OnUpdatePrimGroupMouseRotation;
        event UpdateVector OnUpdatePrimScale;
        event StatusChange OnChildAgentStatus;
        event GenericCall2 OnStopMovement;
        event Action<LLUUID> OnRemoveAvatar;

        event CreateNewInventoryItem OnCreateNewInventoryItem;
        event CreateInventoryFolder OnCreateNewInventoryFolder;
        event FetchInventoryDescendents OnFetchInventoryDescendents;
        event FetchInventory OnFetchInventory;
        event RequestTaskInventory OnRequestTaskInventory;
        event UpdateInventoryItemTransaction OnUpdateInventoryItem;
        event UDPAssetUploadRequest OnAssetUploadRequest;
        event XferReceive OnXferReceive;
        event RequestXfer OnRequestXfer;
        event ConfirmXfer OnConfirmXfer;
        event RezScript OnRezScript;
        event UpdateTaskInventory OnUpdateTaskInventory;
        event RemoveTaskInventory OnRemoveTaskItem;

        event UUIDNameRequest OnNameFromUUIDRequest;

        event ParcelPropertiesRequest OnParcelPropertiesRequest;
        event ParcelDivideRequest OnParcelDivideRequest;
        event ParcelJoinRequest OnParcelJoinRequest;
        event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest;
        event ParcelSelectObjects OnParcelSelectObjects;
        event ParcelObjectOwnerRequest OnParcelObjectOwnerRequest;
        event EstateOwnerMessageRequest OnEstateOwnerMessage;

        LLVector3 StartPos { get; set; }

        LLUUID AgentId { get; }

        LLUUID SessionId { get; }

        string FirstName { get; }

        string LastName { get; }

        uint CircuitCode { get; set; }

        void OutPacket(Packet newPack);
        void SendWearables(AvatarWearable[] wearables, int serial);
        void SendAppearance(LLUUID agentID, byte[] visualParams, byte[] textureEntry);
        void SendStartPingCheck(byte seq);
        void SendKillObject(ulong regionHandle, uint localID);
        void SendAnimation(LLUUID animID, int seq, LLUUID sourceAgentId);
        void SendRegionHandshake(RegionInfo regionInfo);
        void SendChatMessage(string message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID);
        void SendChatMessage(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID);

        void SendInstantMessage(LLUUID fromAgent, LLUUID fromAgentSession, string message, LLUUID toAgent,
                                LLUUID imSessionID, string fromName, byte dialog, uint timeStamp);

        void SendLayerData(float[] map);
        void SendLayerData(int px, int py, float[] map);
        void MoveAgentIntoRegion(RegionInfo regInfo, LLVector3 pos, LLVector3 look);
        void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourExternalEndPoint);
        AgentCircuitData RequestClientInfo();

        void CrossRegion(ulong newRegionHandle, LLVector3 pos, LLVector3 lookAt, IPEndPoint newRegionExternalEndPoint,
                         string capsURL);

        void SendMapBlock(List<MapBlockData> mapBlocks);
        void SendLocalTeleport(LLVector3 position, LLVector3 lookAt, uint flags);

        void SendRegionTeleport(ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint, uint locationID,
                                uint flags, string capsURL);

        void SendTeleportCancel();
        void SendTeleportLocationStart();
        void SendMoneyBalance(LLUUID transaction, bool success, byte[] description, int balance);

        void SendAvatarData(ulong regionHandle, string firstName, string lastName, LLUUID avatarID, uint avatarLocalID,
                            LLVector3 Pos, byte[] textureEntry, uint parentID);

        void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position,
                                   LLVector3 velocity, LLQuaternion rotation);

        void SendCoarseLocationUpdate(List<LLVector3> CoarseLocations);

        void AttachObject(uint localID, LLQuaternion rotation, byte attachPoint);

        void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape,
                                   LLVector3 pos, uint flags, LLUUID objectID, LLUUID ownerID, string text, byte[] color,
                                   uint parentID, byte[] particleSystem, LLQuaternion rotation);

        void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position,
                                 LLQuaternion rotation);
        void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position,
                                 LLQuaternion rotation,LLVector3 velocity, LLVector3 rotationalvelocity);

        void SendInventoryFolderDetails(LLUUID ownerID, LLUUID folderID, List<InventoryItemBase> items);
        void SendInventoryItemDetails(LLUUID ownerID, InventoryItemBase item);
        void SendInventoryItemUpdate(InventoryItemBase Item);
        void SendRemoveInventoryItem(LLUUID itemID);
        void SendTaskInventory(LLUUID taskID, short serial, byte[] fileName);
        void SendXferPacket(ulong xferID, uint packet, byte[] data);
        void SendAvatarPickerReply(AvatarPickerReplyPacket Pack);

        void SendPreLoadSound(LLUUID objectID, LLUUID ownerID, LLUUID soundID);
        void SendPlayAttachedSound(LLUUID soundID, LLUUID objectID, LLUUID ownerID, float gain, byte flags);

        void SendNameReply(LLUUID profileId, string firstname, string lastname);
        void SendAlertMessage(string message);
        void SendAgentAlertMessage(string message, bool modal);
        void SendLoadURL(string objectname, LLUUID objectID, LLUUID ownerID, bool groupOwned, string message, string url);
        bool AddMoney(int debit);

        void SendSunPos(LLVector3 sunPos, LLVector3 sunVel);
        void SendViewerTime(int phase);

        void SendAvatarProperties(LLUUID avatarID, string aboutText, string bornOn, string charterMember, string flAbout,
                                  uint flags, LLUUID flImageID, LLUUID imageID, string profileURL, LLUUID partnerID);

        void SetDebug(int newDebug);
        void InPacket(Packet NewPack);
        void Close();
        void Stop();
        event ViewerEffectEventHandler OnViewerEffect;
        event Action<IClientAPI> OnLogout;
        event Action<IClientAPI> OnConnectionClosed;
        void SendLogoutPacket();
    }
}
