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
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Net;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.Framework
{
    #region Client API Delegate definitions

    public delegate void ViewerEffectEventHandler(IClientAPI sender, List<ViewerEffectEventHandlerArg> args);

    public delegate void ChatFromViewer(Object sender, ChatFromViewerArgs e);

    public delegate void TextureRequest(Object sender, TextureRequestArgs e);

    public delegate void AvatarNowWearing(Object sender, AvatarWearingArgs e);

    public delegate void ImprovedInstantMessage(IClientAPI remoteclient,
                                                LLUUID fromAgentID, LLUUID fromAgentSession, LLUUID toAgentID, LLUUID imSessionID, uint timestamp,
                                                string fromAgentName, string message, byte dialog, bool fromGroup, byte offline, uint ParentEstateID,
                                                LLVector3 Position, LLUUID RegionID, byte[] binaryBucket); // This shouldn't be cut down...
    // especially if we're ever going to implement groups, presence, estate message dialogs...

    public delegate void RezObject(IClientAPI remoteClient, LLUUID itemID, LLVector3 RayEnd, LLVector3 RayStart,
                                   LLUUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                                   uint EveryoneMask, uint GroupMask, uint NextOwnerMask, uint ItemFlags,
                                   bool RezSelected, bool RemoveItem, LLUUID fromTaskID);

    public delegate void RezSingleAttachmentFromInv(IClientAPI remoteClient, LLUUID itemID, uint AttachmentPt,
                                                    uint ItemFlags, uint NextOwnerMask);

    public delegate void ObjectAttach(IClientAPI remoteClient, uint objectLocalID, uint AttachmentPt, LLQuaternion rot);

    public delegate void ModifyTerrain(
        float height, float seconds, byte size, byte action, float north, float west, float south, float east,
        IClientAPI remoteClient);
    
    public delegate void SetAppearance(byte[] texture, List<byte> visualParamList);

    public delegate void StartAnim(IClientAPI remoteClient, LLUUID animID);

    public delegate void StopAnim(IClientAPI remoteClient, LLUUID animID);

    public delegate void LinkObjects(IClientAPI remoteClient, uint parent, List<uint> children);

    public delegate void DelinkObjects(List<uint> primIds);

    public delegate void RequestMapBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag);

    public delegate void RequestMapName(IClientAPI remoteClient, string mapName);

    public delegate void TeleportLocationRequest(
        IClientAPI remoteClient, ulong regionHandle, LLVector3 position, LLVector3 lookAt, uint flags);

    public delegate void TeleportLandmarkRequest(
        IClientAPI remoteClient, ulong regionHandle, LLVector3 position);

    public delegate void DisconnectUser();

    public delegate void RequestAvatarProperties(IClientAPI remoteClient, LLUUID avatarID);

    public delegate void UpdateAvatarProperties(IClientAPI remoteClient, UserProfileData ProfileData);

    public delegate void SetAlwaysRun(IClientAPI remoteClient, bool SetAlwaysRun);

    public delegate void GenericCall2();

    // really don't want to be passing packets in these events, so this is very temporary.
    public delegate void GenericCall4(Packet packet, IClientAPI remoteClient);

    public delegate void GenericCall5(IClientAPI remoteClient, bool status);

    public delegate void GenericCall7(IClientAPI remoteClient, uint localID, string message);

    public delegate void UpdateShape(LLUUID agentID, uint localID, UpdateShapeArgs shapeBlock);

    public delegate void ObjectExtraParams(LLUUID agentID, uint localID, ushort type, bool inUse, byte[] data);

    public delegate void ObjectSelect(uint localID, IClientAPI remoteClient);

    public delegate void RequestObjectPropertiesFamily(
        IClientAPI remoteClient, LLUUID AgentID, uint RequestFlags, LLUUID TaskID);

    public delegate void ObjectDeselect(uint localID, IClientAPI remoteClient);

    public delegate void UpdatePrimFlags(uint localID, Packet packet, IClientAPI remoteClient);

    public delegate void UpdatePrimTexture(uint localID, byte[] texture, IClientAPI remoteClient);

    public delegate void UpdateVector(uint localID, LLVector3 pos, IClientAPI remoteClient);

    public delegate void UpdatePrimRotation(uint localID, LLQuaternion rot, IClientAPI remoteClient);

    public delegate void UpdatePrimSingleRotation(uint localID, LLQuaternion rot, IClientAPI remoteClient);

    public delegate void UpdatePrimGroupRotation(uint localID, LLVector3 pos, LLQuaternion rot, IClientAPI remoteClient);

    public delegate void ObjectDuplicate(uint localID, LLVector3 offset, uint dupeFlags, LLUUID AgentID, LLUUID GroupID);

    public delegate void ObjectDuplicateOnRay(uint localID, uint dupeFlags, LLUUID AgentID, LLUUID GroupID,
                                              LLUUID RayTargetObj, LLVector3 RayEnd, LLVector3 RayStart,
                                              bool BypassRaycast, bool RayEndIsIntersection, bool CopyCenters, bool CopyRotates);


    public delegate void StatusChange(bool status);

    public delegate void NewAvatar(IClientAPI remoteClient, LLUUID agentID, bool status);

    public delegate void UpdateAgent(IClientAPI remoteClient, AgentUpdateArgs agentData);

    public delegate void AgentRequestSit(IClientAPI remoteClient, LLUUID agentID, LLUUID targetID, LLVector3 offset);

    public delegate void AgentSit(IClientAPI remoteClient, LLUUID agentID);

    public delegate void AvatarPickerRequest(IClientAPI remoteClient, LLUUID agentdata, LLUUID queryID, string UserQuery
        );

    public delegate void MoveObject(LLUUID objectID, LLVector3 offset, LLVector3 grapPos, IClientAPI remoteClient);

    public delegate void ParcelAccessListRequest(
        LLUUID agentID, LLUUID sessionID, uint flags, int sequenceID, int landLocalID, IClientAPI remote_client);

    public delegate void ParcelAccessListUpdateRequest(
        LLUUID agentID, LLUUID sessionID, uint flags, int landLocalID, List<ParcelManager.ParcelAccessEntry> entries,
        IClientAPI remote_client);

    public delegate void ParcelPropertiesRequest(
        int start_x, int start_y, int end_x, int end_y, int sequence_id, bool snap_selection, IClientAPI remote_client);

    public delegate void ParcelDivideRequest(int west, int south, int east, int north, IClientAPI remote_client);

    public delegate void ParcelJoinRequest(int west, int south, int east, int north, IClientAPI remote_client);

    public delegate void ParcelPropertiesUpdateRequest(LandUpdateArgs args, int local_id, IClientAPI remote_client);

    public delegate void ParcelSelectObjects(int land_local_id, int request_type, IClientAPI remote_client);

    public delegate void ParcelObjectOwnerRequest(int local_id, IClientAPI remote_client);

    public delegate void ParcelAbandonRequest(int local_id, IClientAPI remote_client);
    public delegate void ParcelReclaim(int local_id, IClientAPI remote_client);

    public delegate void ParcelReturnObjectsRequest(int local_id, uint return_type, LLUUID[] agent_ids, LLUUID[] selected_ids, IClientAPI remote_client);

    public delegate void EstateOwnerMessageRequest(LLUUID AgentID, LLUUID SessionID, LLUUID TransactionID, LLUUID Invoice, byte[] Method, byte[][] Parameters, IClientAPI remote_client);

    public delegate void RegionInfoRequest(IClientAPI remote_client);

    public delegate void EstateCovenantRequest(IClientAPI remote_client);

    public delegate void UUIDNameRequest(LLUUID id, IClientAPI remote_client);

    public delegate void AddNewPrim(
        LLUUID ownerID, LLVector3 RayEnd, LLQuaternion rot, PrimitiveBaseShape shape, byte bypassRaycast, LLVector3 RayStart, LLUUID RayTargetID,
        byte RayEndIsIntersection);

    public delegate void RequestGodlikePowers(LLUUID AgentID, LLUUID SessionID, LLUUID token, bool GodLike, IClientAPI remote_client);

    public delegate void GodKickUser(
        LLUUID GodAgentID, LLUUID GodSessionID, LLUUID AgentID, uint kickflags, byte[] reason);

    public delegate void CreateInventoryFolder(
        IClientAPI remoteClient, LLUUID folderID, ushort folderType, string folderName, LLUUID parentID);

    public delegate void UpdateInventoryFolder(
        IClientAPI remoteClient, LLUUID folderID, ushort type, string name, LLUUID parentID);

    public delegate void MoveInventoryFolder(
        IClientAPI remoteClient, LLUUID folderID, LLUUID parentID);

    public delegate void CreateNewInventoryItem(
        IClientAPI remoteClient, LLUUID transActionID, LLUUID folderID, uint callbackID, string description, string name,
        sbyte invType, sbyte type, byte wearableType, uint nextOwnerMask, int creationDate);

    public delegate void FetchInventoryDescendents(
        IClientAPI remoteClient, LLUUID folderID, LLUUID ownerID, bool fetchFolders, bool fetchItems, int sortOrder);

    public delegate void PurgeInventoryDescendents(
        IClientAPI remoteClient, LLUUID folderID);

    public delegate void FetchInventory(IClientAPI remoteClient, LLUUID itemID, LLUUID ownerID);

    public delegate void RequestTaskInventory(IClientAPI remoteClient, uint localID);

/*    public delegate void UpdateInventoryItem(
        IClientAPI remoteClient, LLUUID transactionID, LLUUID itemID, string name, string description,
        uint nextOwnerMask);*/

    public delegate void UpdateInventoryItem(
        IClientAPI remoteClient, LLUUID transactionID, LLUUID itemID, InventoryItemBase itemUpd);

    public delegate void CopyInventoryItem(
        IClientAPI remoteClient, uint callbackID, LLUUID oldAgentID, LLUUID oldItemID, LLUUID newFolderID,
        string newName);

    public delegate void MoveInventoryItem(
        IClientAPI remoteClient, LLUUID folderID, LLUUID itemID, int length, string newName);

    public delegate void RemoveInventoryItem(
        IClientAPI remoteClient, LLUUID itemID);

    public delegate void RemoveInventoryFolder(
        IClientAPI remoteClient, LLUUID folderID);

    public delegate void RequestAsset(IClientAPI remoteClient, RequestAssetArgs transferRequest);

    public delegate void RezScript(IClientAPI remoteClient, InventoryItemBase item, LLUUID transactionID, uint localID);

    public delegate void UpdateTaskInventory(IClientAPI remoteClient, LLUUID transactionID, TaskInventoryItem item, uint localID);

    public delegate void MoveTaskInventory(IClientAPI remoteClient, LLUUID folderID, uint localID, LLUUID itemID);

    public delegate void RemoveTaskInventory(IClientAPI remoteClient, LLUUID itemID, uint localID);

    public delegate void UDPAssetUploadRequest(
        IClientAPI remoteClient, LLUUID assetID, LLUUID transaction, sbyte type, byte[] data, bool storeLocal,
        bool tempFile);

    public delegate void XferReceive(IClientAPI remoteClient, ulong xferID, uint packetID, byte[] data);

    public delegate void RequestXfer(IClientAPI remoteClient, ulong xferID, string fileName);

    public delegate void ConfirmXfer(IClientAPI remoteClient, ulong xferID, uint packetID);

    public delegate void FriendActionDelegate(IClientAPI remoteClient, LLUUID agentID, LLUUID transactionID, List<LLUUID> callingCardFolders);

    public delegate void FriendshipTermination(IClientAPI remoteClient, LLUUID agentID, LLUUID ExID);

    public delegate void PacketStats(int inPackets, int outPackets, int unAckedBytes);

    public delegate void MoneyTransferRequest(LLUUID sourceID, LLUUID destID, int amount, int transactionType, string description);

    public delegate void ParcelBuy(LLUUID agentId, LLUUID groupId, bool final, bool groupOwned,
                                   bool removeContribution, int parcelLocalID, int parcelArea, int parcelPrice, bool authenticated);

    // We keep all this information for fraud purposes in the future.
    public delegate void MoneyBalanceRequest(IClientAPI remoteClient, LLUUID agentID, LLUUID sessionID, LLUUID TransactionID);

    public delegate void ObjectPermissions(IClientAPI controller, LLUUID agentID, LLUUID sessionID, byte field, uint localId, uint mask, byte set);

    public delegate void EconomyDataRequest(LLUUID agentID);

    public delegate void ObjectIncludeInSearch(IClientAPI remoteClient, bool IncludeInSearch, uint localID);

    public delegate void ScriptAnswer(IClientAPI remoteClient, LLUUID objectID, LLUUID itemID, int answer);

    public delegate void RequestPayPrice(IClientAPI remoteClient, LLUUID objectID);

    public delegate void ForceReleaseControls(IClientAPI remoteClient, LLUUID agentID);

    public delegate void GodLandStatRequest(int parcelID, uint reportType, uint requestflags, string filter, IClientAPI remoteClient);

    //Estate Requests
    public delegate void DetailedEstateDataRequest(IClientAPI remoteClient, LLUUID invoice);
    public delegate void SetEstateFlagsRequest(bool blockTerraform, bool noFly, bool allowDamage, bool blockLandResell, int maxAgents, float objectBonusFactor, int matureLevel, bool restrictPushObject, bool allowParcelChanges);
    public delegate void SetEstateTerrainBaseTexture(IClientAPI remoteClient, int corner, LLUUID side);
    public delegate void SetEstateTerrainDetailTexture(IClientAPI remoteClient, int corner, LLUUID side);
    public delegate void SetEstateTerrainTextureHeights(IClientAPI remoteClient, int corner, float lowVal, float highVal);
    public delegate void CommitEstateTerrainTextureRequest(IClientAPI remoteClient);
    public delegate void SetRegionTerrainSettings(float waterHeight, float terrainRaiseLimit, float terrainLowerLimit, bool fixedSun, float sunHour);
    public delegate void BakeTerrain(IClientAPI remoteClient );
    public delegate void EstateRestartSimRequest(IClientAPI remoteClient, int secondsTilReboot);
    public delegate void EstateChangeCovenantRequest(IClientAPI remoteClient, LLUUID newCovenantID);
    public delegate void UpdateEstateAccessDeltaRequest(IClientAPI remote_client, LLUUID invoice, int estateAccessType, LLUUID user);
    public delegate void SimulatorBlueBoxMessageRequest(IClientAPI remoteClient, LLUUID invoice, LLUUID senderID, LLUUID sessionID, string senderName, string message);
    public delegate void EstateBlueBoxMessageRequest(IClientAPI remoteClient, LLUUID invoice, LLUUID senderID, LLUUID sessionID, string senderName, string message);
    public delegate void EstateDebugRegionRequest(IClientAPI remoteClient, LLUUID invoice, LLUUID senderID, bool scripted, bool collisionEvents, bool physics);
    public delegate void EstateTeleportOneUserHomeRequest(IClientAPI remoteClient, LLUUID invoice, LLUUID senderID, LLUUID prey);
    public delegate void ScriptReset(IClientAPI remoteClient, LLUUID objectID, LLUUID itemID);
    public delegate void GetScriptRunning(IClientAPI remoteClient, LLUUID objectID, LLUUID itemID);
    public delegate void SetScriptRunning(IClientAPI remoteClient, LLUUID objectID, LLUUID itemID, bool running);

    #endregion

    public interface IClientAPI
    {
        LLVector3 StartPos { get; set; }

        LLUUID AgentId { get; }

        LLUUID SessionId { get; }

        LLUUID SecureSessionId { get; }

        LLUUID ActiveGroupId { get; }

        string ActiveGroupName { get; }

        ulong ActiveGroupPowers { get; }
        
        string FirstName { get; }
        
        string LastName { get; }

        // [Obsolete("LLClientView Specific - Replace with ???")]
        int NextAnimationSequenceNumber { get; }

        /// <summary>
        /// Returns the full name of the agent/avatar represented by this client
        /// </summary>
        /// <param name="newPack"></param>
        /// <param name="packType"></param>
        string Name { get; }

        bool IsActive
        {
            get;
            set;
        }

        // [Obsolete("LLClientView Specific - Circuits are unique to LLClientView")]
        uint CircuitCode { get; }
        // [Obsolete("LLClientView Specific - Replace with more bare-bones arguments.")]
        event ImprovedInstantMessage OnInstantMessage;
        // [Obsolete("LLClientView Specific - Replace with more bare-bones arguments. Rename OnChat.")]
        event ChatFromViewer OnChatFromViewer;
        // [Obsolete("LLClientView Specific - Replace with more bare-bones arguments.")]
        event TextureRequest OnRequestTexture;
        // [Obsolete("LLClientView Specific - Remove bitbuckets. Adam, can you be more specific here..  as I don't see any bit buckets.")]
        event RezObject OnRezObject;
        // [Obsolete("LLClientView Specific - Replace with more suitable arguments.")]
        event ModifyTerrain OnModifyTerrain;
        event BakeTerrain OnBakeTerrain;
        // [Obsolete("LLClientView Specific.")]
        event SetAppearance OnSetAppearance;
        // [Obsolete("LLClientView Specific - Replace and rename OnAvatarUpdate. Difference from SetAppearance?")]
        event AvatarNowWearing OnAvatarNowWearing;
        event RezSingleAttachmentFromInv OnRezSingleAttachmentFromInv;
        event UUIDNameRequest OnDetachAttachmentIntoInv;
        event ObjectAttach OnObjectAttach;
        event ObjectDeselect OnObjectDetach;
        event StartAnim OnStartAnim;
        event StopAnim OnStopAnim;
        event LinkObjects OnLinkObjects;
        event DelinkObjects OnDelinkObjects;
        event RequestMapBlocks OnRequestMapBlocks;
        event RequestMapName OnMapNameRequest;
        event TeleportLocationRequest OnTeleportLocationRequest;
        event DisconnectUser OnDisconnectUser;
        event RequestAvatarProperties OnRequestAvatarProperties;
        event SetAlwaysRun OnSetAlwaysRun;
        event TeleportLandmarkRequest OnTeleportLandmarkRequest;
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

        event FetchInventory OnAgentDataUpdateRequest;
        event FetchInventory OnUserInfoRequest;
        event TeleportLocationRequest OnSetStartLocationRequest;

        event RequestGodlikePowers OnRequestGodlikePowers;
        event GodKickUser OnGodKickUser;

        event ObjectDuplicate OnObjectDuplicate;
        event ObjectDuplicateOnRay OnObjectDuplicateOnRay;
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
        event UpdateVector OnUpdatePrimGroupScale;
        event StatusChange OnChildAgentStatus;
        event GenericCall2 OnStopMovement;
        event Action<LLUUID> OnRemoveAvatar;
        event ObjectPermissions OnObjectPermissions;

        event CreateNewInventoryItem OnCreateNewInventoryItem;
        event CreateInventoryFolder OnCreateNewInventoryFolder;
        event UpdateInventoryFolder OnUpdateInventoryFolder;
        event MoveInventoryFolder OnMoveInventoryFolder;
        event FetchInventoryDescendents OnFetchInventoryDescendents;
        event PurgeInventoryDescendents OnPurgeInventoryDescendents;
        event FetchInventory OnFetchInventory;
        event RequestTaskInventory OnRequestTaskInventory;
        event UpdateInventoryItem OnUpdateInventoryItem;
        event CopyInventoryItem OnCopyInventoryItem;
        event MoveInventoryItem OnMoveInventoryItem;
        event RemoveInventoryFolder OnRemoveInventoryFolder;
        event RemoveInventoryItem OnRemoveInventoryItem;
        event UDPAssetUploadRequest OnAssetUploadRequest;
        event XferReceive OnXferReceive;
        event RequestXfer OnRequestXfer;
        event ConfirmXfer OnConfirmXfer;
        event RezScript OnRezScript;
        event UpdateTaskInventory OnUpdateTaskInventory;
        event MoveTaskInventory OnMoveTaskItem;
        event RemoveTaskInventory OnRemoveTaskItem;
        event RequestAsset OnRequestAsset;

        event UUIDNameRequest OnNameFromUUIDRequest;

        event ParcelAccessListRequest OnParcelAccessListRequest;
        event ParcelAccessListUpdateRequest OnParcelAccessListUpdateRequest;
        event ParcelPropertiesRequest OnParcelPropertiesRequest;
        event ParcelDivideRequest OnParcelDivideRequest;
        event ParcelJoinRequest OnParcelJoinRequest;
        event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest;
        event ParcelSelectObjects OnParcelSelectObjects;
        event ParcelObjectOwnerRequest OnParcelObjectOwnerRequest;
        event ParcelAbandonRequest OnParcelAbandonRequest;
        event ParcelReclaim OnParcelReclaim;
        event ParcelReturnObjectsRequest OnParcelReturnObjectsRequest;
        event RegionInfoRequest OnRegionInfoRequest;
        event EstateCovenantRequest OnEstateCovenantRequest;

        event FriendActionDelegate OnApproveFriendRequest;
        event FriendActionDelegate OnDenyFriendRequest;
        event FriendshipTermination OnTerminateFriendship;
        event PacketStats OnPacketStats;

        // Financial packets
        event MoneyTransferRequest OnMoneyTransferRequest;
        event EconomyDataRequest OnEconomyDataRequest;

        event MoneyBalanceRequest OnMoneyBalanceRequest;
        event UpdateAvatarProperties OnUpdateAvatarProperties;
        event ParcelBuy OnParcelBuy;
        event RequestPayPrice OnRequestPayPrice;

        event ObjectIncludeInSearch OnObjectIncludeInSearch;

        event UUIDNameRequest OnTeleportHomeRequest;

        event ScriptAnswer OnScriptAnswer;

        event AgentSit OnUndo;

        event ForceReleaseControls OnForceReleaseControls;
        event GodLandStatRequest OnLandStatRequest;

        event DetailedEstateDataRequest OnDetailedEstateDataRequest;
        event SetEstateFlagsRequest OnSetEstateFlagsRequest;
        event SetEstateTerrainBaseTexture OnSetEstateTerrainBaseTexture;
        event SetEstateTerrainDetailTexture OnSetEstateTerrainDetailTexture;
        event SetEstateTerrainTextureHeights OnSetEstateTerrainTextureHeights;
        event CommitEstateTerrainTextureRequest OnCommitEstateTerrainTextureRequest;
        event SetRegionTerrainSettings OnSetRegionTerrainSettings;
        event EstateRestartSimRequest OnEstateRestartSimRequest;
        event EstateChangeCovenantRequest OnEstateChangeCovenantRequest;
        event UpdateEstateAccessDeltaRequest OnUpdateEstateAccessDeltaRequest;
        event SimulatorBlueBoxMessageRequest  OnSimulatorBlueBoxMessageRequest;
        event EstateBlueBoxMessageRequest OnEstateBlueBoxMessageRequest;
        event EstateDebugRegionRequest OnEstateDebugRegionRequest;
        event EstateTeleportOneUserHomeRequest OnEstateTeleportOneUserHomeRequest;
        event UUIDNameRequest OnUUIDGroupNameRequest;

        event RequestObjectPropertiesFamily OnObjectGroupRequest;
        event ScriptReset OnScriptReset;
        event GetScriptRunning OnGetScriptRunning;
        event SetScriptRunning OnSetScriptRunning;
        event UpdateVector OnAutoPilotGo;

        // [Obsolete("IClientAPI.OutPacket SHOULD NOT EXIST outside of LLClientView please refactor appropriately.")]
        void OutPacket(Packet newPack, ThrottleOutPacketType packType);
        void SendWearables(AvatarWearable[] wearables, int serial);
        void SendAppearance(LLUUID agentID, byte[] visualParams, byte[] textureEntry);
        void SendStartPingCheck(byte seq);
        void SendKillObject(ulong regionHandle, uint localID);
        void SendAnimations(LLUUID[] animID, int[] seqs, LLUUID sourceAgentId);
        void SendRegionHandshake(RegionInfo regionInfo, RegionHandshakeArgs args);
        void SendChatMessage(string message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID, byte source, byte audible);
        void SendChatMessage(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID, byte source, byte audible);

        void SendInstantMessage(LLUUID fromAgent, LLUUID fromAgentSession, string message, LLUUID toAgent,
                                LLUUID imSessionID, string fromName, byte dialog, uint timeStamp);

        void SendInstantMessage(LLUUID fromAgent, LLUUID fromAgentSession, string message, LLUUID toAgent,
                                LLUUID imSessionID, string fromName, byte dialog, uint timeStamp,
                                byte[] binaryBucket);

        void SendLayerData(float[] map);
        void SendLayerData(int px, int py, float[] map);
        void MoveAgentIntoRegion(RegionInfo regInfo, LLVector3 pos, LLVector3 look);
        void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourExternalEndPoint);
        AgentCircuitData RequestClientInfo();

        void CrossRegion(ulong newRegionHandle, LLVector3 pos, LLVector3 lookAt, IPEndPoint newRegionExternalEndPoint,
                         string capsURL);

        void SendMapBlock(List<MapBlockData> mapBlocks, uint flag);
        void SendLocalTeleport(LLVector3 position, LLVector3 lookAt, uint flags);

        void SendRegionTeleport(ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint, uint locationID,
                                uint flags, string capsURL);

        void SendTeleportFailed(string reason);
        void SendTeleportLocationStart();
        void SendMoneyBalance(LLUUID transaction, bool success, byte[] description, int balance);
        void SendPayPrice(LLUUID objectID, int[] payPrice);

        void SendAvatarData(ulong regionHandle, string firstName, string lastName, LLUUID avatarID, uint avatarLocalID,
                            LLVector3 Pos, byte[] textureEntry, uint parentID);

        void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position,
                                   LLVector3 velocity, LLQuaternion rotation);

        void SendCoarseLocationUpdate(List<LLVector3> CoarseLocations);

        void AttachObject(uint localID, LLQuaternion rotation, byte attachPoint);
        void SetChildAgentThrottle(byte[] throttle);

        void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape,
                                   LLVector3 pos, LLVector3 vel, LLVector3 acc, LLQuaternion rotation, LLVector3 rvel,
                                   uint flags,
                                   LLUUID objectID, LLUUID ownerID, string text, byte[] color, uint parentID, byte[] particleSystem,
                                   byte clickAction, byte[] textureanim, bool attachment, uint AttachPoint, LLUUID AssetId, LLUUID SoundId, double SoundVolume, byte SoundFlags, double SoundRadius);


        void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape,
                                          LLVector3 pos, LLVector3 vel, LLVector3 acc, LLQuaternion rotation, LLVector3 rvel,
                                          uint flags, LLUUID objectID, LLUUID ownerID, string text, byte[] color,
                                   uint parentID, byte[] particleSystem, byte clickAction);

        void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position,
                                 LLQuaternion rotation, LLVector3 velocity, LLVector3 rotationalvelocity, byte state, LLUUID AssetId);

        void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, LLVector3 position,
                                 LLQuaternion rotation, LLVector3 velocity, LLVector3 rotationalvelocity);

        void SendInventoryFolderDetails(LLUUID ownerID, LLUUID folderID, List<InventoryItemBase> items,
                                        List<InventoryFolderBase> folders, bool fetchFolders,
                                        bool fetchItems);

        void SendInventoryItemDetails(LLUUID ownerID, InventoryItemBase item);

        /// <summary>
        /// Tell the client that we have created the item it requested.
        /// </summary>
        /// <param name="Item"></param>
        void SendInventoryItemCreateUpdate(InventoryItemBase Item);

        void SendRemoveInventoryItem(LLUUID itemID);

        void SendTakeControls(int controls, bool passToAgent, bool TakeControls);

        void SendTaskInventory(LLUUID taskID, short serial, byte[] fileName);

        /// <summary>
        /// Used by the server to inform the client of a new inventory item.  Used when transferring items
        /// between avatars, possibly among other things.
        /// </summary>
        /// <param name="item"></param>
        void SendBulkUpdateInventory(InventoryItemBase item);

        void SendXferPacket(ulong xferID, uint packet, byte[] data);

        void SendEconomyData(float EnergyEfficiency, int ObjectCapacity, int ObjectCount, int PriceEnergyUnit,
                             int PriceGroupCreate, int PriceObjectClaim, float PriceObjectRent, float PriceObjectScaleFactor,
                             int PriceParcelClaim, float PriceParcelClaimFactor, int PriceParcelRent, int PricePublicObjectDecay,
                             int PricePublicObjectDelete, int PriceRentLight, int PriceUpload, int TeleportMinPrice, float TeleportPriceExponent);

        void SendAvatarPickerReply(AvatarPickerReplyAgentDataArgs AgentData, List<AvatarPickerReplyDataArgs> Data);

        void SendAgentDataUpdate(LLUUID agentid, LLUUID activegroupid, string firstname, string lastname, ulong grouppowers, string groupname, string grouptitle);

        void SendPreLoadSound(LLUUID objectID, LLUUID ownerID, LLUUID soundID);
        void SendPlayAttachedSound(LLUUID soundID, LLUUID objectID, LLUUID ownerID, float gain, byte flags);
        void SendTriggeredSound(LLUUID soundID, LLUUID ownerID, LLUUID objectID, LLUUID parentID, ulong handle, LLVector3 position, float gain);
        void SendAttachedSoundGainChange(LLUUID objectID, float gain);

        void SendNameReply(LLUUID profileId, string firstname, string lastname);
        void SendAlertMessage(string message);

        void SendAgentAlertMessage(string message, bool modal);
        void SendLoadURL(string objectname, LLUUID objectID, LLUUID ownerID, bool groupOwned, string message, string url);
        void SendDialog(string objectname, LLUUID objectID, LLUUID ownerID, string msg, LLUUID textureID, int ch, string[] buttonlabels);
        bool AddMoney(int debit);

        void SendSunPos(LLVector3 sunPos, LLVector3 sunVel, ulong CurrentTime, uint SecondsPerSunCycle, uint SecondsPerYear, float OrbitalPosition);
        void SendViewerTime(int phase);
        LLUUID GetDefaultAnimation(string name);

        void SendAvatarProperties(LLUUID avatarID, string aboutText, string bornOn, string charterMember, string flAbout,
                                  uint flags, LLUUID flImageID, LLUUID imageID, string profileURL, LLUUID partnerID);

        void SendScriptQuestion(LLUUID taskID, string taskName, string ownerName, LLUUID itemID, int question);
        void SendHealth(float health);


        void SendEstateManagersList(LLUUID invoice, LLUUID[] EstateManagers, uint estateID);

        void SendBannedUserList(LLUUID invoice, List<RegionBanListItem> banlist, uint estateID);

        void SendRegionInfoToEstateMenu(RegionInfoForEstateMenuArgs args);
        void SendEstateCovenantInformation();
        void SendDetailedEstateData(LLUUID invoice,string estateName, uint estateID);

        void SendLandProperties(IClientAPI remote_client, int sequence_id, bool snap_selection, int request_result, LandData landData, float simObjectBonusFactor, int parcelObjectCapacity, int simObjectCapacity, uint regionFlags);
        void SendLandAccessListData(List<LLUUID> avatars, uint accessFlag, int localLandID);
        void SendForceClientSelectObjects(List<uint> objectIDs);
        void SendLandObjectOwners(Dictionary<LLUUID, int> ownersAndCount);
        void SendLandParcelOverlay(byte[] data, int sequence_id);

        #region Parcel Methods

        void SendParcelMediaCommand(uint flags, ParcelMediaCommandEnum command, float time);

        void SendParcelMediaUpdate(string mediaUrl, LLUUID mediaTextureID,
                                   byte autoScale, string mediaType, string mediaDesc, int mediaWidth, int mediaHeight,
                                   byte mediaLoop);

        #endregion

        void SendAssetUploadCompleteMessage(sbyte AssetType, bool Success, LLUUID AssetFullID);
        void SendConfirmXfer(ulong xferID, uint PacketID);
        void SendXferRequest(ulong XferID, short AssetType, LLUUID vFileID, byte FilePath, byte[] FileName);

        void SendImagePart(ushort numParts, LLUUID ImageUUID, uint ImageSize, byte[] ImageData, byte imageCodec);

        void SendShutdownConnectionNotice();
        void SendSimStats(Packet pack);
        void SendObjectPropertiesFamilyData(uint RequestFlags, LLUUID ObjectUUID, LLUUID OwnerID, LLUUID GroupID,
                                                    uint BaseMask, uint OwnerMask, uint GroupMask, uint EveryoneMask,
                                                    uint NextOwnerMask, int OwnershipCost, byte SaleType, int SalePrice, uint Category,
                                                    LLUUID LastOwnerID, string ObjectName, string Description);

        void SendObjectPropertiesReply(LLUUID ItemID, ulong CreationDate, LLUUID CreatorUUID, LLUUID FolderUUID, LLUUID FromTaskUUID,
                                              LLUUID GroupUUID, short InventorySerial, LLUUID LastOwnerUUID, LLUUID ObjectUUID,
                                              LLUUID OwnerUUID, string TouchTitle, byte[] TextureID, string SitTitle, string ItemName,
                                              string ItemDescription, uint OwnerMask, uint NextOwnerMask, uint GroupMask, uint EveryoneMask,
                                              uint BaseMask);
        void SendAgentOffline(LLUUID[] agentIDs);

        void SendAgentOnline(LLUUID[] agentIDs);

        void SendSitResponse(LLUUID TargetID, LLVector3 OffsetPos, LLQuaternion SitOrientation, bool autopilot,
                                        LLVector3 CameraAtOffset, LLVector3 CameraEyeOffset, bool ForceMouseLook);

        void SendAdminResponse(LLUUID Token, uint AdminLevel);

        void SendGroupMembership(GroupData[] GroupMembership);

        void SendGroupNameReply(LLUUID groupLLUID, string GroupName);

        void SendLandStatReply(uint reportType, uint requestFlags, uint resultCount, LandStatReportItem[] lsrpia);

        void SendScriptRunningReply(LLUUID objectID, LLUUID itemID, bool running);

        void SendAsset(AssetRequestToClient req);

        void SendTexture(AssetBase TextureAsset);

        byte[] GetThrottlesPacked(float multiplier);


        void SetDebug(int newDebug);
        void InPacket(Packet NewPack);
        void Close(bool ShutdownCircuit);
        void Kick(string message);
        void Stop();
        event ViewerEffectEventHandler OnViewerEffect;
        event Action<IClientAPI> OnLogout;
        event Action<IClientAPI> OnConnectionClosed;

        void SendBlueBoxMessage(LLUUID FromAvatarID, LLUUID fromSessionID, String FromAvatarName, String Message);

        void SendLogoutPacket();
        ClientInfo GetClientInfo();
        void SetClientInfo(ClientInfo info);
        void Terminate();
    }
}
