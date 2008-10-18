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
using OpenMetaverse;
using OpenMetaverse.Packets;

namespace OpenSim.Framework
{
    #region Client API Delegate definitions

    public delegate void ViewerEffectEventHandler(IClientAPI sender, List<ViewerEffectEventHandlerArg> args);

    public delegate void ChatMessage(Object sender, OSChatMessage e);

    public delegate void GenericMessage(Object sender, string method, List<String> args);

    public delegate void TextureRequest(Object sender, TextureRequestArgs e);

    public delegate void AvatarNowWearing(Object sender, AvatarWearingArgs e);

    public delegate void ImprovedInstantMessage(IClientAPI remoteclient,
                                                UUID fromAgentID, UUID fromAgentSession, UUID toAgentID, UUID imSessionID, uint timestamp,
                                                string fromAgentName, string message, byte dialog, bool fromGroup, byte offline, uint ParentEstateID,
                                                Vector3 Position, UUID RegionID, byte[] binaryBucket); // This shouldn't be cut down...
    // especially if we're ever going to implement groups, presence, estate message dialogs...

    public delegate void RezObject(IClientAPI remoteClient, UUID itemID, Vector3 RayEnd, Vector3 RayStart,
                                   UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                                   bool RezSelected, bool RemoveItem, UUID fromTaskID);

    public delegate void RezSingleAttachmentFromInv(IClientAPI remoteClient, UUID itemID, uint AttachmentPt);

    public delegate void ObjectAttach(IClientAPI remoteClient, uint objectLocalID, uint AttachmentPt, Quaternion rot);

    public delegate void ModifyTerrain(
        float height, float seconds, byte size, byte action, float north, float west, float south, float east,
        UUID agentId);

    public delegate void SetAppearance(byte[] texture, List<byte> visualParamList);

    public delegate void StartAnim(IClientAPI remoteClient, UUID animID);

    public delegate void StopAnim(IClientAPI remoteClient, UUID animID);

    public delegate void LinkObjects(IClientAPI remoteClient, uint parent, List<uint> children);

    public delegate void DelinkObjects(List<uint> primIds);

    public delegate void RequestMapBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag);

    public delegate void RequestMapName(IClientAPI remoteClient, string mapName);

    public delegate void TeleportLocationRequest(
        IClientAPI remoteClient, ulong regionHandle, Vector3 position, Vector3 lookAt, uint flags);

    public delegate void TeleportLandmarkRequest(
        IClientAPI remoteClient, UUID regionID, Vector3 position);

    public delegate void DisconnectUser();

    public delegate void RequestAvatarProperties(IClientAPI remoteClient, UUID avatarID);

    public delegate void UpdateAvatarProperties(IClientAPI remoteClient, UserProfileData ProfileData);

    public delegate void SetAlwaysRun(IClientAPI remoteClient, bool SetAlwaysRun);

    public delegate void GenericCall2();

    // really don't want to be passing packets in these events, so this is very temporary.
    public delegate void GenericCall4(Packet packet, IClientAPI remoteClient);
    public delegate void DeRezObject(IClientAPI remoteClient, uint localID, UUID groupID, byte destination, UUID destinationID);

    public delegate void GenericCall5(IClientAPI remoteClient, bool status);

    public delegate void GenericCall7(IClientAPI remoteClient, uint localID, string message);

    public delegate void UpdateShape(UUID agentID, uint localID, UpdateShapeArgs shapeBlock);

    public delegate void ObjectExtraParams(UUID agentID, uint localID, ushort type, bool inUse, byte[] data);

    public delegate void ObjectSelect(uint localID, IClientAPI remoteClient);

    public delegate void RequestObjectPropertiesFamily(
        IClientAPI remoteClient, UUID AgentID, uint RequestFlags, UUID TaskID);

    public delegate void ObjectDeselect(uint localID, IClientAPI remoteClient);
    public delegate void ObjectDrop(uint localID, IClientAPI remoteClient);

    public delegate void UpdatePrimFlags(uint localID, Packet packet, IClientAPI remoteClient);

    public delegate void UpdatePrimTexture(uint localID, byte[] texture, IClientAPI remoteClient);

    public delegate void UpdateVector(uint localID, Vector3 pos, IClientAPI remoteClient);

    public delegate void UpdatePrimRotation(uint localID, Quaternion rot, IClientAPI remoteClient);

    public delegate void UpdatePrimSingleRotation(uint localID, Quaternion rot, IClientAPI remoteClient);

    public delegate void UpdatePrimGroupRotation(uint localID, Vector3 pos, Quaternion rot, IClientAPI remoteClient);

    public delegate void ObjectDuplicate(uint localID, Vector3 offset, uint dupeFlags, UUID AgentID, UUID GroupID);

    public delegate void ObjectDuplicateOnRay(uint localID, uint dupeFlags, UUID AgentID, UUID GroupID,
                                              UUID RayTargetObj, Vector3 RayEnd, Vector3 RayStart,
                                              bool BypassRaycast, bool RayEndIsIntersection, bool CopyCenters, bool CopyRotates);


    public delegate void StatusChange(bool status);

    public delegate void NewAvatar(IClientAPI remoteClient, UUID agentID, bool status);

    public delegate void UpdateAgent(IClientAPI remoteClient, AgentUpdateArgs agentData);

    public delegate void AgentRequestSit(IClientAPI remoteClient, UUID agentID, UUID targetID, Vector3 offset);

    public delegate void AgentSit(IClientAPI remoteClient, UUID agentID);

    public delegate void AvatarPickerRequest(IClientAPI remoteClient, UUID agentdata, UUID queryID, string UserQuery
        );

    public delegate void GrabObject(uint localID, Vector3 pos, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs);

    public delegate void MoveObject(UUID objectID, Vector3 offset, Vector3 grapPos, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs);

    public delegate void ParcelAccessListRequest(
        UUID agentID, UUID sessionID, uint flags, int sequenceID, int landLocalID, IClientAPI remote_client);

    public delegate void ParcelAccessListUpdateRequest(
        UUID agentID, UUID sessionID, uint flags, int landLocalID, List<ParcelManager.ParcelAccessEntry> entries,
        IClientAPI remote_client);

    public delegate void ParcelPropertiesRequest(
        int start_x, int start_y, int end_x, int end_y, int sequence_id, bool snap_selection, IClientAPI remote_client);

    public delegate void ParcelDivideRequest(int west, int south, int east, int north, IClientAPI remote_client);

    public delegate void ParcelJoinRequest(int west, int south, int east, int north, IClientAPI remote_client);

    public delegate void ParcelPropertiesUpdateRequest(LandUpdateArgs args, int local_id, IClientAPI remote_client);

    public delegate void ParcelSelectObjects(int land_local_id, int request_type, IClientAPI remote_client);

    public delegate void ParcelObjectOwnerRequest(int local_id, IClientAPI remote_client);

    public delegate void ParcelAbandonRequest(int local_id, IClientAPI remote_client);
    public delegate void ParcelGodForceOwner(int local_id, UUID ownerID, IClientAPI remote_client);
    public delegate void ParcelReclaim(int local_id, IClientAPI remote_client);

    public delegate void ParcelReturnObjectsRequest(int local_id, uint return_type, UUID[] agent_ids, UUID[] selected_ids, IClientAPI remote_client);

    public delegate void EstateOwnerMessageRequest(UUID AgentID, UUID SessionID, UUID TransactionID, UUID Invoice, byte[] Method, byte[][] Parameters, IClientAPI remote_client);

    public delegate void RegionInfoRequest(IClientAPI remote_client);

    public delegate void EstateCovenantRequest(IClientAPI remote_client);

    public delegate void UUIDNameRequest(UUID id, IClientAPI remote_client);

    public delegate void AddNewPrim(
        UUID ownerID, Vector3 RayEnd, Quaternion rot, PrimitiveBaseShape shape, byte bypassRaycast, Vector3 RayStart, UUID RayTargetID,
        byte RayEndIsIntersection);

    public delegate void RequestGodlikePowers(UUID AgentID, UUID SessionID, UUID token, bool GodLike, IClientAPI remote_client);

    public delegate void GodKickUser(
        UUID GodAgentID, UUID GodSessionID, UUID AgentID, uint kickflags, byte[] reason);

    public delegate void CreateInventoryFolder(
        IClientAPI remoteClient, UUID folderID, ushort folderType, string folderName, UUID parentID);

    public delegate void UpdateInventoryFolder(
        IClientAPI remoteClient, UUID folderID, ushort type, string name, UUID parentID);

    public delegate void MoveInventoryFolder(
        IClientAPI remoteClient, UUID folderID, UUID parentID);

    public delegate void CreateNewInventoryItem(
        IClientAPI remoteClient, UUID transActionID, UUID folderID, uint callbackID, string description, string name,
        sbyte invType, sbyte type, byte wearableType, uint nextOwnerMask, int creationDate);

    public delegate void FetchInventoryDescendents(
        IClientAPI remoteClient, UUID folderID, UUID ownerID, bool fetchFolders, bool fetchItems, int sortOrder);

    public delegate void PurgeInventoryDescendents(
        IClientAPI remoteClient, UUID folderID);

    public delegate void FetchInventory(IClientAPI remoteClient, UUID itemID, UUID ownerID);

    public delegate void RequestTaskInventory(IClientAPI remoteClient, uint localID);

/*    public delegate void UpdateInventoryItem(
        IClientAPI remoteClient, UUID transactionID, UUID itemID, string name, string description,
        uint nextOwnerMask);*/

    public delegate void UpdateInventoryItem(
        IClientAPI remoteClient, UUID transactionID, UUID itemID, InventoryItemBase itemUpd);

    public delegate void CopyInventoryItem(
        IClientAPI remoteClient, uint callbackID, UUID oldAgentID, UUID oldItemID, UUID newFolderID,
        string newName);

    public delegate void MoveInventoryItem(
        IClientAPI remoteClient, UUID folderID, UUID itemID, int length, string newName);

    public delegate void RemoveInventoryItem(
        IClientAPI remoteClient, UUID itemID);

    public delegate void RemoveInventoryFolder(
        IClientAPI remoteClient, UUID folderID);

    public delegate void RequestAsset(IClientAPI remoteClient, RequestAssetArgs transferRequest);

    public delegate void RezScript(IClientAPI remoteClient, InventoryItemBase item, UUID transactionID, uint localID);

    public delegate void UpdateTaskInventory(IClientAPI remoteClient, UUID transactionID, TaskInventoryItem item, uint localID);

    public delegate void MoveTaskInventory(IClientAPI remoteClient, UUID folderID, uint localID, UUID itemID);

    public delegate void RemoveTaskInventory(IClientAPI remoteClient, UUID itemID, uint localID);

    public delegate void UDPAssetUploadRequest(
        IClientAPI remoteClient, UUID assetID, UUID transaction, sbyte type, byte[] data, bool storeLocal,
        bool tempFile);

    public delegate void XferReceive(IClientAPI remoteClient, ulong xferID, uint packetID, byte[] data);

    public delegate void RequestXfer(IClientAPI remoteClient, ulong xferID, string fileName);

    public delegate void ConfirmXfer(IClientAPI remoteClient, ulong xferID, uint packetID);

    public delegate void FriendActionDelegate(IClientAPI remoteClient, UUID agentID, UUID transactionID, List<UUID> callingCardFolders);

    public delegate void FriendshipTermination(IClientAPI remoteClient, UUID agentID, UUID ExID);

    public delegate void MoneyTransferRequest(UUID sourceID, UUID destID, int amount, int transactionType, string description);

    public delegate void ParcelBuy(UUID agentId, UUID groupId, bool final, bool groupOwned,
                                   bool removeContribution, int parcelLocalID, int parcelArea, int parcelPrice, bool authenticated);

    // We keep all this information for fraud purposes in the future.
    public delegate void MoneyBalanceRequest(IClientAPI remoteClient, UUID agentID, UUID sessionID, UUID TransactionID);

    public delegate void ObjectPermissions(IClientAPI controller, UUID agentID, UUID sessionID, byte field, uint localId, uint mask, byte set);

    public delegate void EconomyDataRequest(UUID agentID);

    public delegate void ObjectIncludeInSearch(IClientAPI remoteClient, bool IncludeInSearch, uint localID);

    public delegate void ScriptAnswer(IClientAPI remoteClient, UUID objectID, UUID itemID, int answer);

    public delegate void RequestPayPrice(IClientAPI remoteClient, UUID objectID);
    public delegate void ObjectSaleInfo(IClientAPI remoteClient, UUID agentID, UUID sessionID, uint localID, byte saleType, int salePrice);
    public delegate void ObjectBuy(IClientAPI remoteClient, UUID agentID, UUID sessionID, UUID groupID, UUID categoryID, uint localID, byte saleType, int salePrice);
    public delegate void BuyObjectInventory(IClientAPI remoteClient, UUID agentID, UUID sessionID, UUID objectID, UUID itemID, UUID folderID);

    public delegate void ForceReleaseControls(IClientAPI remoteClient, UUID agentID);

    public delegate void GodLandStatRequest(int parcelID, uint reportType, uint requestflags, string filter, IClientAPI remoteClient);

    //Estate Requests
    public delegate void DetailedEstateDataRequest(IClientAPI remoteClient, UUID invoice);
    public delegate void SetEstateFlagsRequest(bool blockTerraform, bool noFly, bool allowDamage, bool blockLandResell, int maxAgents, float objectBonusFactor, int matureLevel, bool restrictPushObject, bool allowParcelChanges);
    public delegate void SetEstateTerrainBaseTexture(IClientAPI remoteClient, int corner, UUID side);
    public delegate void SetEstateTerrainDetailTexture(IClientAPI remoteClient, int corner, UUID side);
    public delegate void SetEstateTerrainTextureHeights(IClientAPI remoteClient, int corner, float lowVal, float highVal);
    public delegate void CommitEstateTerrainTextureRequest(IClientAPI remoteClient);
    public delegate void SetRegionTerrainSettings(float waterHeight, float terrainRaiseLimit, float terrainLowerLimit, bool estateSun, bool fixedSun, float sunHour, bool globalSun, bool estateFixed, float estateSunHour);
    public delegate void EstateChangeInfo(IClientAPI client, UUID invoice, UUID senderID, UInt32 param1, UInt32 param2);
    public delegate void BakeTerrain(IClientAPI remoteClient );
    public delegate void EstateRestartSimRequest(IClientAPI remoteClient, int secondsTilReboot);
    public delegate void EstateChangeCovenantRequest(IClientAPI remoteClient, UUID newCovenantID);
    public delegate void UpdateEstateAccessDeltaRequest(IClientAPI remote_client, UUID invoice, int estateAccessType, UUID user);
    public delegate void SimulatorBlueBoxMessageRequest(IClientAPI remoteClient, UUID invoice, UUID senderID, UUID sessionID, string senderName, string message);
    public delegate void EstateBlueBoxMessageRequest(IClientAPI remoteClient, UUID invoice, UUID senderID, UUID sessionID, string senderName, string message);
    public delegate void EstateDebugRegionRequest(IClientAPI remoteClient, UUID invoice, UUID senderID, bool scripted, bool collisionEvents, bool physics);
    public delegate void EstateTeleportOneUserHomeRequest(IClientAPI remoteClient, UUID invoice, UUID senderID, UUID prey);
    public delegate void EstateTeleportAllUsersHomeRequest(IClientAPI remoteClient, UUID invoice, UUID senderID);
    public delegate void RegionHandleRequest(IClientAPI remoteClient, UUID regionID);
    public delegate void ParcelInfoRequest(IClientAPI remoteClient, UUID parcelID);

    public delegate void ScriptReset(IClientAPI remoteClient, UUID objectID, UUID itemID);
    public delegate void GetScriptRunning(IClientAPI remoteClient, UUID objectID, UUID itemID);
    public delegate void SetScriptRunning(IClientAPI remoteClient, UUID objectID, UUID itemID, bool running);
    public delegate void ActivateGesture(IClientAPI client, UUID gestureid, UUID assetId);
    public delegate void DeactivateGesture(IClientAPI client, UUID gestureid);

    public delegate void TerrainUnacked(IClientAPI remoteClient, int patchX, int patchY);
    public delegate void ObjectOwner(IClientAPI remoteClient, UUID ownerID, UUID groupID, List<uint> localIDs);

    public delegate void DirPlacesQuery(IClientAPI remoteClient, UUID queryID, string queryText, int queryFlags, int category, string simName, int queryStart);
    public delegate void DirFindQuery(IClientAPI remoteClient, UUID queryID, string queryText, uint queryFlags, int queryStart);
    public delegate void DirLandQuery(IClientAPI remoteClient, UUID queryID, uint queryFlags, uint searchType, int price, int area, int queryStart);
    public delegate void DirPopularQuery(IClientAPI remoteClient, UUID queryID, uint queryFlags);
    public delegate void DirClassifiedQuery(IClientAPI remoteClient, UUID queryID, string queryText, uint queryFlags, uint category, int queryStart);
    public delegate void EventInfoRequest(IClientAPI remoteClient, uint eventID);
    public delegate void ParcelSetOtherCleanTime(IClientAPI remoteClient, int localID, int otherCleanTime);

    public delegate void MapItemRequest(IClientAPI remoteClient, uint flags, uint EstateID, bool godlike, uint itemtype, ulong regionhandle);

    #endregion

    public struct DirPlacesReplyData
    {
        public UUID parcelID;
        public string name;
        public bool forSale;
        public bool auction;
        public float dwell;
    }

    public struct DirPeopleReplyData
    {
        public UUID agentID;
        public string firstName;
        public string lastName;
        public string group;
        public bool online;
        public int reputation;
    }

    public struct DirEventsReplyData
    {
        public UUID ownerID;
        public string name;
        public uint eventID;
        public string date;
        public uint unixTime;
        public uint eventFlags;
    }

    public struct DirGroupsReplyData
    {
        public UUID groupID;
        public string groupName;
        public int members;
        public float searchOrder;
    }

    public struct DirClassifiedReplyData
    {
        public UUID classifiedID;
        public string name;
        public byte classifiedFlags;
        public uint creationDate;
        public uint expirationDate;
        public int price;
    }

    public struct DirLandReplyData
    {
        public UUID parcelID;
        public string name;
        public bool auction;
        public bool forSale;
        public int salePrice;
        public int actualArea;
    }

    public struct DirPopularReplyData
    {
        public UUID parcelID;
        public string name;
        public float dwell;
    }

    public interface IClientAPI
    {
        Vector3 StartPos { get; set; }

        UUID AgentId { get; }

        UUID SessionId { get; }

        UUID SecureSessionId { get; }

        UUID ActiveGroupId { get; }

        string ActiveGroupName { get; }

        ulong ActiveGroupPowers { get; }

        ulong GetGroupPowers(UUID groupID);

        string FirstName { get; }

        string LastName { get; }

        IScene Scene { get; }

        // [Obsolete("LLClientView Specific - Replace with ???")]
        int NextAnimationSequenceNumber { get; }

        /// <summary>
        /// Returns the full name of the agent/avatar represented by this client
        /// </summary>
        string Name { get; }

        bool IsActive
        {
            get;
            set;
        }

        bool SendLogoutPacketWhenClosing
        {
            set;
        }

        // [Obsolete("LLClientView Specific - Circuits are unique to LLClientView")]
        uint CircuitCode { get; }

        event GenericMessage OnGenericMessage;

        // [Obsolete("LLClientView Specific - Replace with more bare-bones arguments.")]
        event ImprovedInstantMessage OnInstantMessage;
        // [Obsolete("LLClientView Specific - Replace with more bare-bones arguments. Rename OnChat.")]
        event ChatMessage OnChatFromClient;
        // [Obsolete("LLClientView Specific - Replace with more bare-bones arguments.")]
        event TextureRequest OnRequestTexture;
        // [Obsolete("LLClientView Specific - Remove bitbuckets. Adam, can you be more specific here..  as I don't see any bit buckets.")]
        event RezObject OnRezObject;
        // [Obsolete("LLClientView Specific - Replace with more suitable arguments.")]
        event ModifyTerrain OnModifyTerrain;
        event BakeTerrain OnBakeTerrain;
        event EstateChangeInfo OnEstateChangeInfo;
        // [Obsolete("LLClientView Specific.")]
        event SetAppearance OnSetAppearance;
        // [Obsolete("LLClientView Specific - Replace and rename OnAvatarUpdate. Difference from SetAppearance?")]
        event AvatarNowWearing OnAvatarNowWearing;
        event RezSingleAttachmentFromInv OnRezSingleAttachmentFromInv;
        event UUIDNameRequest OnDetachAttachmentIntoInv;
        event ObjectAttach OnObjectAttach;
        event ObjectDeselect OnObjectDetach;
        event ObjectDrop OnObjectDrop;
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
        event DeRezObject OnDeRezObject;
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
        event GrabObject OnGrabObject;
        event ObjectSelect OnDeGrabObject;
        event MoveObject OnGrabUpdate;

        event UpdateShape OnUpdatePrimShape;
        event ObjectExtraParams OnUpdateExtraParams;
        event ObjectSelect OnObjectSelect;
        event ObjectDeselect OnObjectDeselect;
        event GenericCall7 OnObjectDescription;
        event GenericCall7 OnObjectName;
        event GenericCall7 OnObjectClickAction;
        event GenericCall7 OnObjectMaterial;
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
        event Action<UUID> OnRemoveAvatar;
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
        event ParcelGodForceOwner OnParcelGodForceOwner;
        event ParcelReclaim OnParcelReclaim;
        event ParcelReturnObjectsRequest OnParcelReturnObjectsRequest;
        event RegionInfoRequest OnRegionInfoRequest;
        event EstateCovenantRequest OnEstateCovenantRequest;

        event FriendActionDelegate OnApproveFriendRequest;
        event FriendActionDelegate OnDenyFriendRequest;
        event FriendshipTermination OnTerminateFriendship;

        // Financial packets
        event MoneyTransferRequest OnMoneyTransferRequest;
        event EconomyDataRequest OnEconomyDataRequest;

        event MoneyBalanceRequest OnMoneyBalanceRequest;
        event UpdateAvatarProperties OnUpdateAvatarProperties;
        event ParcelBuy OnParcelBuy;
        event RequestPayPrice OnRequestPayPrice;
        event ObjectSaleInfo OnObjectSaleInfo;
        event ObjectBuy OnObjectBuy;
        event BuyObjectInventory OnBuyObjectInventory;

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
        event EstateTeleportAllUsersHomeRequest OnEstateTeleportAllUsersHomeRequest;
        event UUIDNameRequest OnUUIDGroupNameRequest;

        event RegionHandleRequest OnRegionHandleRequest;
        event ParcelInfoRequest OnParcelInfoRequest;

        event RequestObjectPropertiesFamily OnObjectGroupRequest;
        event ScriptReset OnScriptReset;
        event GetScriptRunning OnGetScriptRunning;
        event SetScriptRunning OnSetScriptRunning;
        event UpdateVector OnAutoPilotGo;

        event TerrainUnacked OnUnackedTerrain;
        event ActivateGesture OnActivateGesture;
        event DeactivateGesture OnDeactivateGesture;
        event ObjectOwner OnObjectOwner;

        event DirPlacesQuery OnDirPlacesQuery;
        event DirFindQuery OnDirFindQuery;
        event DirLandQuery OnDirLandQuery;
        event DirPopularQuery OnDirPopularQuery;
        event DirClassifiedQuery OnDirClassifiedQuery;
        event EventInfoRequest OnEventInfoRequest;
        event ParcelSetOtherCleanTime OnParcelSetOtherCleanTime;
        
        event MapItemRequest OnMapItemRequest;


   //     void ActivateGesture(UUID assetId, UUID gestureId);

        void SendWearables(AvatarWearable[] wearables, int serial);
        void SendAppearance(UUID agentID, byte[] visualParams, byte[] textureEntry);
        void SendStartPingCheck(byte seq);
        
        /// <summary>
        /// Tell the client that an object has been deleted
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="localID"></param>        
        void SendKillObject(ulong regionHandle, uint localID);
        
        void SendAnimations(UUID[] animID, int[] seqs, UUID sourceAgentId);
        void SendRegionHandshake(RegionInfo regionInfo, RegionHandshakeArgs args);
        void SendChatMessage(string message, byte type, Vector3 fromPos, string fromName, UUID fromAgentID, byte source, byte audible);
        void SendChatMessage(byte[] message, byte type, Vector3 fromPos, string fromName, UUID fromAgentID, byte source, byte audible);

        void SendInstantMessage(UUID fromAgent, UUID fromAgentSession, string message, UUID toAgent,
                                UUID imSessionID, string fromName, byte dialog, uint timeStamp);

        void SendInstantMessage(UUID fromAgent, UUID fromAgentSession, string message, UUID toAgent,
                                UUID imSessionID, string fromName, byte dialog, uint timeStamp,
                                byte[] binaryBucket);

        void SendGenericMessage(string method, List<string> message);

        void SendLayerData(float[] map);
        void SendLayerData(int px, int py, float[] map);

        void SendWindData(Vector2[] windSpeeds);

        void MoveAgentIntoRegion(RegionInfo regInfo, Vector3 pos, Vector3 look);
        void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourExternalEndPoint);
        AgentCircuitData RequestClientInfo();

        void CrossRegion(ulong newRegionHandle, Vector3 pos, Vector3 lookAt, IPEndPoint newRegionExternalEndPoint,
                         string capsURL);

        void SendMapBlock(List<MapBlockData> mapBlocks, uint flag);
        void SendLocalTeleport(Vector3 position, Vector3 lookAt, uint flags);

        void SendRegionTeleport(ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint, uint locationID,
                                uint flags, string capsURL);

        void SendTeleportFailed(string reason);
        void SendTeleportLocationStart();
        void SendMoneyBalance(UUID transaction, bool success, byte[] description, int balance);
        void SendPayPrice(UUID objectID, int[] payPrice);

        void SendAvatarData(ulong regionHandle, string firstName, string lastName, UUID avatarID, uint avatarLocalID,
                            Vector3 Pos, byte[] textureEntry, uint parentID, Quaternion rotation);

        void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, Vector3 position,
                                   Vector3 velocity, Quaternion rotation);

        void SendCoarseLocationUpdate(List<Vector3> CoarseLocations);

        void AttachObject(uint localID, Quaternion rotation, byte attachPoint);
        void SetChildAgentThrottle(byte[] throttle);

        void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape,
                                   Vector3 pos, Vector3 vel, Vector3 acc, Quaternion rotation, Vector3 rvel,
                                   uint flags,
                                   UUID objectID, UUID ownerID, string text, byte[] color, uint parentID, byte[] particleSystem,
                                   byte clickAction, byte material, byte[] textureanim, bool attachment, uint AttachPoint, UUID AssetId, UUID SoundId, double SoundVolume, byte SoundFlags, double SoundRadius);


        void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape,
                                          Vector3 pos, Vector3 vel, Vector3 acc, Quaternion rotation, Vector3 rvel,
                                          uint flags, UUID objectID, UUID ownerID, string text, byte[] color,
                                   uint parentID, byte[] particleSystem, byte clickAction, byte material);


        void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, Vector3 position,
                                 Quaternion rotation, Vector3 velocity, Vector3 rotationalvelocity, byte state, UUID AssetId);

        void SendInventoryFolderDetails(UUID ownerID, UUID folderID, List<InventoryItemBase> items,
                                        List<InventoryFolderBase> folders, bool fetchFolders,
                                        bool fetchItems);

        void SendInventoryItemDetails(UUID ownerID, InventoryItemBase item);

        /// <summary>
        /// Tell the client that we have created the item it requested.
        /// </summary>
        /// <param name="Item"></param>
        void SendInventoryItemCreateUpdate(InventoryItemBase Item);

        void SendRemoveInventoryItem(UUID itemID);

        void SendTakeControls(int controls, bool passToAgent, bool TakeControls);

        void SendTaskInventory(UUID taskID, short serial, byte[] fileName);

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

        void SendAgentDataUpdate(UUID agentid, UUID activegroupid, string firstname, string lastname, ulong grouppowers, string groupname, string grouptitle);

        void SendPreLoadSound(UUID objectID, UUID ownerID, UUID soundID);
        void SendPlayAttachedSound(UUID soundID, UUID objectID, UUID ownerID, float gain, byte flags);
        void SendTriggeredSound(UUID soundID, UUID ownerID, UUID objectID, UUID parentID, ulong handle, Vector3 position, float gain);
        void SendAttachedSoundGainChange(UUID objectID, float gain);

        void SendNameReply(UUID profileId, string firstname, string lastname);
        void SendAlertMessage(string message);

        void SendAgentAlertMessage(string message, bool modal);
        void SendLoadURL(string objectname, UUID objectID, UUID ownerID, bool groupOwned, string message, string url);
        void SendDialog(string objectname, UUID objectID, UUID ownerID, string msg, UUID textureID, int ch, string[] buttonlabels);
        bool AddMoney(int debit);

        void SendSunPos(Vector3 sunPos, Vector3 sunVel, ulong CurrentTime, uint SecondsPerSunCycle, uint SecondsPerYear, float OrbitalPosition);
        void SendViewerEffect(ViewerEffectPacket.EffectBlock[] effectBlocks);
        void SendViewerTime(int phase);
        UUID GetDefaultAnimation(string name);

        void SendAvatarProperties(UUID avatarID, string aboutText, string bornOn, Byte[] charterMember, string flAbout,
                                  uint flags, UUID flImageID, UUID imageID, string profileURL, UUID partnerID);

        void SendScriptQuestion(UUID taskID, string taskName, string ownerName, UUID itemID, int question);
        void SendHealth(float health);


        void SendEstateManagersList(UUID invoice, UUID[] EstateManagers, uint estateID);

        void SendBannedUserList(UUID invoice, EstateBan[] banlist, uint estateID);

        void SendRegionInfoToEstateMenu(RegionInfoForEstateMenuArgs args);
        void SendEstateCovenantInformation(UUID covenant);
        void SendDetailedEstateData(UUID invoice, string estateName, uint estateID, uint parentEstate, uint estateFlags, uint sunPosition, UUID covenant, string abuseEmail, UUID estateOwner);

        void SendLandProperties(int sequence_id, bool snap_selection, int request_result, LandData landData, float simObjectBonusFactor, int parcelObjectCapacity, int simObjectCapacity, uint regionFlags);
        void SendLandAccessListData(List<UUID> avatars, uint accessFlag, int localLandID);
        void SendForceClientSelectObjects(List<uint> objectIDs);
        void SendLandObjectOwners(Dictionary<UUID, int> ownersAndCount);
        void SendLandParcelOverlay(byte[] data, int sequence_id);

        #region Parcel Methods

        void SendParcelMediaCommand(uint flags, ParcelMediaCommandEnum command, float time);

        void SendParcelMediaUpdate(string mediaUrl, UUID mediaTextureID,
                                   byte autoScale, string mediaType, string mediaDesc, int mediaWidth, int mediaHeight,
                                   byte mediaLoop);

        #endregion

        void SendAssetUploadCompleteMessage(sbyte AssetType, bool Success, UUID AssetFullID);
        void SendConfirmXfer(ulong xferID, uint PacketID);
        void SendXferRequest(ulong XferID, short AssetType, UUID vFileID, byte FilePath, byte[] FileName);

        /// <summary>
        /// Send the first part of a texture.  For sufficiently small textures, this may be the only packet.
        /// </summary>
        /// <param name="numParts"></param>
        /// <param name="ImageUUID"></param>
        /// <param name="ImageSize"></param>
        /// <param name="ImageData"></param>
        /// <param name="imageCodec"></param>
        void SendImageFirstPart(ushort numParts, UUID ImageUUID, uint ImageSize, byte[] ImageData, byte imageCodec);
        
        /// <summary>
        /// Send the next packet for a series of packets making up a single texture, 
        /// as established by SendImageFirstPart()
        /// </summary>
        /// <param name="partNumber"></param>
        /// <param name="imageUuid"></param>
        /// <param name="imageData"></param>
        void SendImageNextPart(ushort partNumber, UUID imageUuid, byte[] imageData);

        void SendShutdownConnectionNotice();
        
        /// <summary>
        /// Send statistical information about the sim to the client.
        /// </summary>
        /// <param name="stats"></param>
        void SendSimStats(SimStats stats);
        
        void SendObjectPropertiesFamilyData(uint RequestFlags, UUID ObjectUUID, UUID OwnerID, UUID GroupID,
                                                    uint BaseMask, uint OwnerMask, uint GroupMask, uint EveryoneMask,
                                                    uint NextOwnerMask, int OwnershipCost, byte SaleType, int SalePrice, uint Category,
                                                    UUID LastOwnerID, string ObjectName, string Description);

        void SendObjectPropertiesReply(UUID ItemID, ulong CreationDate, UUID CreatorUUID, UUID FolderUUID, UUID FromTaskUUID,
                                              UUID GroupUUID, short InventorySerial, UUID LastOwnerUUID, UUID ObjectUUID,
                                              UUID OwnerUUID, string TouchTitle, byte[] TextureID, string SitTitle, string ItemName,
                                              string ItemDescription, uint OwnerMask, uint NextOwnerMask, uint GroupMask, uint EveryoneMask,
                                              uint BaseMask, byte saleType, int salePrice);
        void SendAgentOffline(UUID[] agentIDs);

        void SendAgentOnline(UUID[] agentIDs);

        void SendSitResponse(UUID TargetID, Vector3 OffsetPos, Quaternion SitOrientation, bool autopilot,
                                        Vector3 CameraAtOffset, Vector3 CameraEyeOffset, bool ForceMouseLook);

        void SendAdminResponse(UUID Token, uint AdminLevel);

        void SendGroupMembership(GroupData[] GroupMembership);

        void SendGroupNameReply(UUID groupLLUID, string GroupName);

        void SendLandStatReply(uint reportType, uint requestFlags, uint resultCount, LandStatReportItem[] lsrpia);

        void SendScriptRunningReply(UUID objectID, UUID itemID, bool running);

        void SendAsset(AssetRequestToClient req);

        void SendTexture(AssetBase TextureAsset);

        byte[] GetThrottlesPacked(float multiplier);

        /// <summary>
        /// Set the debug level at which packet output should be printed to console.
        /// </summary>
        /// <param name="newDebugPacketLevel"></param>
        void SetDebugPacketLevel(int newDebug);
        
        void InPacket(object NewPack);
        void ProcessInPacket(Packet NewPack);
        void Close(bool ShutdownCircuit);
        void Kick(string message);
        void Stop();
        event ViewerEffectEventHandler OnViewerEffect;
        event Action<IClientAPI> OnLogout;
        event Action<IClientAPI> OnConnectionClosed;

        void SendBlueBoxMessage(UUID FromAvatarID, UUID fromSessionID, String FromAvatarName, String Message);

        void SendLogoutPacket();
        ClientInfo GetClientInfo();

        void SetClientInfo(ClientInfo info);
        void SetClientOption(string option, string value);
        string GetClientOption(string option);
        void Terminate();

        void SendSetFollowCamProperties(UUID objectID, SortedDictionary<int, float> parameters);
        void SendClearFollowCamProperties(UUID objectID);

        void SendRegionHandle(UUID regoinID, ulong handle);
        void SendParcelInfo(RegionInfo info, LandData land, UUID parcelID, uint x, uint y);
        void SendScriptTeleportRequest(string objName, string simName, Vector3 pos, Vector3 lookAt);

        void SendDirPlacesReply(UUID queryID, DirPlacesReplyData[] data);
        void SendDirPeopleReply(UUID queryID, DirPeopleReplyData[] data);
        void SendDirEventsReply(UUID queryID, DirEventsReplyData[] data);
        void SendDirGroupsReply(UUID queryID, DirGroupsReplyData[] data);
        void SendDirClassifiedReply(UUID queryID, DirClassifiedReplyData[] data);
        void SendDirLandReply(UUID queryID, DirLandReplyData[] data);
        void SendDirPopularReply(UUID queryID, DirPopularReplyData[] data);
        void SendEventInfoReply(EventData info);

        void SendMapItemReply(mapItemReply[] replies, uint mapitemtype, uint flags);
        
        void KillEndDone();
    }
}
