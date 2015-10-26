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
 *     * Neither the name of the OpenSimulator Project nor the
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

    public delegate void AvatarNowWearing(IClientAPI sender, AvatarWearingArgs e);

    public delegate void ImprovedInstantMessage(IClientAPI remoteclient, GridInstantMessage im);

    public delegate void RezObject(IClientAPI remoteClient, UUID itemID, Vector3 RayEnd, Vector3 RayStart,
                                   UUID RayTargetID, byte BypassRayCast, bool RayEndIsIntersection,
                                   bool RezSelected, bool RemoveItem, UUID fromTaskID);

    public delegate ISceneEntity RezSingleAttachmentFromInv(IClientAPI remoteClient, UUID itemID, uint AttachmentPt);

    public delegate void RezMultipleAttachmentsFromInv(IClientAPI remoteClient, List<KeyValuePair<UUID, uint>> rezlist );

    public delegate void ObjectAttach(
        IClientAPI remoteClient, uint objectLocalID, uint AttachmentPt, bool silent);

    public delegate void ModifyTerrain(UUID user, 
        float height, float seconds, byte size, byte action, float north, float west, float south, float east,
        UUID agentId);

    public delegate void NetworkStats(int inPackets, int outPackets, int unAckedBytes);

    public delegate void SetAppearance(IClientAPI remoteClient, Primitive.TextureEntry textureEntry, byte[] visualParams, Vector3 AvSize, WearableCacheItem[] CacheItems);
    public delegate void CachedTextureRequest(IClientAPI remoteClient, int serial, List<CachedTextureRequestArg> cachedTextureRequest);

    public delegate void StartAnim(IClientAPI remoteClient, UUID animID);

    public delegate void StopAnim(IClientAPI remoteClient, UUID animID);

    public delegate void ChangeAnim(UUID animID, bool addOrRemove, bool sendPack);

    public delegate void LinkObjects(IClientAPI remoteClient, uint parent, List<uint> children);

    public delegate void DelinkObjects(List<uint> primIds, IClientAPI client);

    public delegate void RequestMapBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag);

    public delegate void RequestMapName(IClientAPI remoteClient, string mapName, uint flags);

    public delegate void TeleportLocationRequest(
        IClientAPI remoteClient, ulong regionHandle, Vector3 position, Vector3 lookAt, uint flags);

    public delegate void TeleportLandmarkRequest(
        IClientAPI remoteClient, AssetLandmark lm);

    public delegate void TeleportCancel(IClientAPI remoteClient);

    public delegate void DisconnectUser();

    public delegate void RequestAvatarProperties(IClientAPI remoteClient, UUID avatarID);

    public delegate void UpdateAvatarProperties(IClientAPI remoteClient, UserProfileData ProfileData);

    public delegate void SetAlwaysRun(IClientAPI remoteClient, bool SetAlwaysRun);

    public delegate void GenericCall1(IClientAPI remoteClient);

    public delegate void GenericCall2();

    // really don't want to be passing packets in these events, so this is very temporary.
    public delegate void GenericCall4(Packet packet, IClientAPI remoteClient);

    public delegate void DeRezObject(
        IClientAPI remoteClient, List<uint> localIDs, UUID groupID, DeRezAction action, UUID destinationID);

    public delegate void GenericCall5(IClientAPI remoteClient, bool status);

    public delegate void GenericCall7(IClientAPI remoteClient, uint localID, string message);

    public delegate void UpdateShape(UUID agentID, uint localID, UpdateShapeArgs shapeBlock);

    public delegate void ObjectExtraParams(UUID agentID, uint localID, ushort type, bool inUse, byte[] data);

    public delegate void ObjectSelect(uint localID, IClientAPI remoteClient);

    public delegate void ObjectRequest(uint localID, IClientAPI remoteClient);

    public delegate void RequestObjectPropertiesFamily(
        IClientAPI remoteClient, UUID AgentID, uint RequestFlags, UUID TaskID);

    public delegate void ObjectDeselect(uint localID, IClientAPI remoteClient);

    public delegate void ObjectDrop(uint localID, IClientAPI remoteClient);

    public delegate void UpdatePrimFlags(
        uint localID, bool UsePhysics, bool IsTemporary, bool IsPhantom, ExtraPhysicsData PhysData, IClientAPI remoteClient);

    public delegate void UpdatePrimTexture(uint localID, byte[] texture, IClientAPI remoteClient);

    public delegate void UpdateVector(uint localID, Vector3 pos, IClientAPI remoteClient);

    public delegate void ClientChangeObject(uint localID, object data ,IClientAPI remoteClient);

    public delegate void UpdatePrimRotation(uint localID, Quaternion rot, IClientAPI remoteClient);

    public delegate void UpdatePrimSingleRotation(uint localID, Quaternion rot, IClientAPI remoteClient);

    public delegate void UpdatePrimSingleRotationPosition(uint localID, Quaternion rot, Vector3 pos, IClientAPI remoteClient);

    public delegate void UpdatePrimGroupRotation(uint localID, Vector3 pos, Quaternion rot, IClientAPI remoteClient);

    public delegate void ObjectDuplicate(uint localID, Vector3 offset, uint dupeFlags, UUID AgentID, UUID GroupID);

    public delegate void ObjectDuplicateOnRay(uint localID, uint dupeFlags, UUID AgentID, UUID GroupID,
                                              UUID RayTargetObj, Vector3 RayEnd, Vector3 RayStart,
                                              bool BypassRaycast, bool RayEndIsIntersection, bool CopyCenters,
                                              bool CopyRotates);

    public delegate void StatusChange(bool status);

    public delegate void NewAvatar(IClientAPI remoteClient, UUID agentID, bool status);

    public delegate void UpdateAgent(IClientAPI remoteClient, AgentUpdateArgs agentData);

    public delegate void AgentRequestSit(IClientAPI remoteClient, UUID agentID, UUID targetID, Vector3 offset);

    public delegate void AgentSit(IClientAPI remoteClient, UUID agentID);

    public delegate void LandUndo(IClientAPI remoteClient);

    public delegate void AvatarPickerRequest(IClientAPI remoteClient, UUID agentdata, UUID queryID, string UserQuery);

    public delegate void GrabObject(
        uint localID, Vector3 pos, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs);

    public delegate void DeGrabObject(
        uint localID, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs);

    public delegate void MoveObject(
        UUID objectID, Vector3 offset, Vector3 grapPos, IClientAPI remoteClient, List<SurfaceTouchEventArgs> surfaceArgs);

    public delegate void SpinStart(UUID objectID, IClientAPI remoteClient);
    public delegate void SpinObject(UUID objectID, Quaternion rotation, IClientAPI remoteClient);
    public delegate void SpinStop(UUID objectID, IClientAPI remoteClient);

    public delegate void ParcelAccessListRequest(
        UUID agentID, UUID sessionID, uint flags, int sequenceID, int landLocalID, IClientAPI remote_client);

    public delegate void ParcelAccessListUpdateRequest(UUID agentID, uint flags,
                    int landLocalID, UUID transactionID, int sequenceID,
                    int sections, List<LandAccessEntry> entries,
                    IClientAPI remote_client);

    public delegate void ParcelPropertiesRequest(
        int start_x, int start_y, int end_x, int end_y, int sequence_id, bool snap_selection, IClientAPI remote_client);

    public delegate void ParcelDivideRequest(int west, int south, int east, int north, IClientAPI remote_client);

    public delegate void ParcelJoinRequest(int west, int south, int east, int north, IClientAPI remote_client);

    public delegate void ParcelPropertiesUpdateRequest(LandUpdateArgs args, int local_id, IClientAPI remote_client);

    public delegate void ParcelSelectObjects(int land_local_id, int request_type, List<UUID> returnIDs, IClientAPI remote_client);

    public delegate void ParcelObjectOwnerRequest(int local_id, IClientAPI remote_client);

    public delegate void ParcelAbandonRequest(int local_id, IClientAPI remote_client);

    public delegate void ParcelGodForceOwner(int local_id, UUID ownerID, IClientAPI remote_client);

    public delegate void ParcelReclaim(int local_id, IClientAPI remote_client);

    public delegate void ParcelReturnObjectsRequest(
        int local_id, uint return_type, UUID[] agent_ids, UUID[] selected_ids, IClientAPI remote_client);

    public delegate void ParcelDeedToGroup(int local_id, UUID group_id, IClientAPI remote_client);

    public delegate void EstateOwnerMessageRequest(
        UUID AgentID, UUID SessionID, UUID TransactionID, UUID Invoice, byte[] Method, byte[][] Parameters,
        IClientAPI remote_client);

    public delegate void RegionInfoRequest(IClientAPI remote_client);

    public delegate void EstateCovenantRequest(IClientAPI remote_client);

    public delegate void UUIDNameRequest(UUID id, IClientAPI remote_client);

    public delegate void AddNewPrim(
        UUID ownerID, UUID groupID, Vector3 RayEnd, Quaternion rot, PrimitiveBaseShape shape, byte bypassRaycast, Vector3 RayStart,
        UUID RayTargetID,
        byte RayEndIsIntersection);

    public delegate void RequestGodlikePowers(
        UUID AgentID, UUID SessionID, UUID token, bool GodLike, IClientAPI remote_client);

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

    public delegate void LinkInventoryItem(
        IClientAPI remoteClient, UUID transActionID, UUID folderID, uint callbackID, string description, string name,
        sbyte invType, sbyte type, UUID olditemID);

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
        IClientAPI remoteClient, List<InventoryItemBase> items);

    public delegate void MoveItemsAndLeaveCopy(
        IClientAPI remoteClient, List<InventoryItemBase> items, UUID destFolder);

    public delegate void RemoveInventoryItem(
        IClientAPI remoteClient, List<UUID> itemIDs);

    public delegate void RemoveInventoryFolder(
        IClientAPI remoteClient, List<UUID> folderIDs);

    public delegate void RequestAsset(IClientAPI remoteClient, RequestAssetArgs transferRequest);

    public delegate void AbortXfer(IClientAPI remoteClient, ulong xferID);

    public delegate void RezScript(IClientAPI remoteClient, InventoryItemBase item, UUID transactionID, uint localID);

    public delegate void UpdateTaskInventory(
        IClientAPI remoteClient, UUID transactionID, TaskInventoryItem item, uint localID);

    public delegate void MoveTaskInventory(IClientAPI remoteClient, UUID folderID, uint localID, UUID itemID);

    public delegate void RemoveTaskInventory(IClientAPI remoteClient, UUID itemID, uint localID);

    public delegate void UDPAssetUploadRequest(
        IClientAPI remoteClient, UUID assetID, UUID transaction, sbyte type, byte[] data, bool storeLocal,
        bool tempFile);

    public delegate void XferReceive(IClientAPI remoteClient, ulong xferID, uint packetID, byte[] data);

    public delegate void RequestXfer(IClientAPI remoteClient, ulong xferID, string fileName);

    public delegate void ConfirmXfer(IClientAPI remoteClient, ulong xferID, uint packetID);

    public delegate void FriendActionDelegate(
        IClientAPI remoteClient, UUID transactionID, List<UUID> callingCardFolders);

    public delegate void FriendshipTermination(IClientAPI remoteClient, UUID ExID);

    public delegate void MoneyTransferRequest(
        UUID sourceID, UUID destID, int amount, int transactionType, string description);

    public delegate void ParcelBuy(UUID agentId, UUID groupId, bool final, bool groupOwned,
                                   bool removeContribution, int parcelLocalID, int parcelArea, int parcelPrice,
                                   bool authenticated);

    // We keep all this information for fraud purposes in the future.
    public delegate void MoneyBalanceRequest(IClientAPI remoteClient, UUID agentID, UUID sessionID, UUID TransactionID);

    public delegate void ObjectPermissions(
        IClientAPI controller, UUID agentID, UUID sessionID, byte field, uint localId, uint mask, byte set);

    public delegate void EconomyDataRequest(IClientAPI client);

    public delegate void ObjectIncludeInSearch(IClientAPI remoteClient, bool IncludeInSearch, uint localID);

    public delegate void ScriptAnswer(IClientAPI remoteClient, UUID objectID, UUID itemID, int answer);

    public delegate void RequestPayPrice(IClientAPI remoteClient, UUID objectID);

    public delegate void ObjectSaleInfo(
        IClientAPI remoteClient, UUID agentID, UUID sessionID, uint localID, byte saleType, int salePrice);

    public delegate void ObjectBuy(
        IClientAPI remoteClient, UUID agentID, UUID sessionID, UUID groupID, UUID categoryID, uint localID,
        byte saleType, int salePrice);

    public delegate void BuyObjectInventory(
        IClientAPI remoteClient, UUID agentID, UUID sessionID, UUID objectID, UUID itemID, UUID folderID);

    public delegate void ForceReleaseControls(IClientAPI remoteClient, UUID agentID);

    public delegate void GodLandStatRequest(
        int parcelID, uint reportType, uint requestflags, string filter, IClientAPI remoteClient);

    //Estate Requests
    public delegate void DetailedEstateDataRequest(IClientAPI remoteClient, UUID invoice);

    public delegate void SetEstateFlagsRequest(
        bool blockTerraform, bool noFly, bool allowDamage, bool blockLandResell, int maxAgents, float objectBonusFactor,
        int matureLevel, bool restrictPushObject, bool allowParcelChanges);

    public delegate void SetEstateTerrainBaseTexture(IClientAPI remoteClient, int corner, UUID side);

    public delegate void SetEstateTerrainDetailTexture(IClientAPI remoteClient, int corner, UUID side);

    public delegate void SetEstateTerrainTextureHeights(IClientAPI remoteClient, int corner, float lowVal, float highVal
        );

    public delegate void CommitEstateTerrainTextureRequest(IClientAPI remoteClient);

    public delegate void SetRegionTerrainSettings(
        float waterHeight, float terrainRaiseLimit, float terrainLowerLimit, bool estateSun, bool fixedSun,
        float sunHour, bool globalSun, bool estateFixed, float estateSunHour);

    public delegate void EstateChangeInfo(IClientAPI client, UUID invoice, UUID senderID, UInt32 param1, UInt32 param2);

    public delegate void EstateManageTelehub(IClientAPI client, UUID invoice, UUID senderID, string cmd, UInt32 param1);

    public delegate void RequestTerrain(IClientAPI remoteClient, string clientFileName);

    public delegate void BakeTerrain(IClientAPI remoteClient);


    public delegate void EstateRestartSimRequest(IClientAPI remoteClient, int secondsTilReboot);

    public delegate void EstateChangeCovenantRequest(IClientAPI remoteClient, UUID newCovenantID);

    public delegate void UpdateEstateAccessDeltaRequest(
        IClientAPI remote_client, UUID invoice, int estateAccessType, UUID user);

    public delegate void SimulatorBlueBoxMessageRequest(
        IClientAPI remoteClient, UUID invoice, UUID senderID, UUID sessionID, string senderName, string message);

    public delegate void EstateBlueBoxMessageRequest(
        IClientAPI remoteClient, UUID invoice, UUID senderID, UUID sessionID, string senderName, string message);

    public delegate void EstateDebugRegionRequest(
        IClientAPI remoteClient, UUID invoice, UUID senderID, bool scripted, bool collisionEvents, bool physics);

    public delegate void EstateTeleportOneUserHomeRequest(
        IClientAPI remoteClient, UUID invoice, UUID senderID, UUID prey);

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

    public delegate void DirPlacesQuery(
        IClientAPI remoteClient, UUID queryID, string queryText, int queryFlags, int category, string simName,
        int queryStart);

    public delegate void DirFindQuery(
        IClientAPI remoteClient, UUID queryID, string queryText, uint queryFlags, int queryStart);

    public delegate void DirLandQuery(
        IClientAPI remoteClient, UUID queryID, uint queryFlags, uint searchType, int price, int area, int queryStart);

    public delegate void DirPopularQuery(IClientAPI remoteClient, UUID queryID, uint queryFlags);

    public delegate void DirClassifiedQuery(
        IClientAPI remoteClient, UUID queryID, string queryText, uint queryFlags, uint category, int queryStart);

    public delegate void EventInfoRequest(IClientAPI remoteClient, uint eventID);

    public delegate void ParcelSetOtherCleanTime(IClientAPI remoteClient, int localID, int otherCleanTime);

    public delegate void MapItemRequest(
        IClientAPI remoteClient, uint flags, uint EstateID, bool godlike, uint itemtype, ulong regionhandle);

    public delegate void OfferCallingCard(IClientAPI remoteClient, UUID destID, UUID transactionID);

    public delegate void AcceptCallingCard(IClientAPI remoteClient, UUID transactionID, UUID folderID);

    public delegate void DeclineCallingCard(IClientAPI remoteClient, UUID transactionID);

    public delegate void SoundTrigger(
        UUID soundId, UUID ownerid, UUID objid, UUID parentid, double Gain, Vector3 Position, UInt64 Handle, float radius);

    public delegate void StartLure(byte lureType, string message, UUID targetID, IClientAPI client);
    public delegate void TeleportLureRequest(UUID lureID, uint teleportFlags, IClientAPI client);

    public delegate void ClassifiedInfoRequest(UUID classifiedID, IClientAPI client);
    public delegate void ClassifiedInfoUpdate(UUID classifiedID, uint category, string name, string description, UUID parcelID, uint parentEstate, UUID snapshotID, Vector3 globalPos, byte classifiedFlags, int price, IClientAPI client);
    public delegate void ClassifiedDelete(UUID classifiedID, IClientAPI client);
    public delegate void ClassifiedGodDelete(UUID classifiedID, UUID queryID, IClientAPI client);

    public delegate void EventNotificationAddRequest(uint EventID, IClientAPI client);
    public delegate void EventNotificationRemoveRequest(uint EventID, IClientAPI client);

    public delegate void EventGodDelete(uint eventID, UUID queryID, string queryText, uint queryFlags, int queryStart, IClientAPI client);

    public delegate void ParcelDwellRequest(int localID, IClientAPI client);

    public delegate void UserInfoRequest(IClientAPI client);
    public delegate void UpdateUserInfo(bool imViaEmail, bool visible, IClientAPI client);
    public delegate void RetrieveInstantMessages(IClientAPI client);
    public delegate void PickDelete(IClientAPI client, UUID pickID);
    public delegate void PickGodDelete(IClientAPI client, UUID agentID, UUID pickID, UUID queryID);
    public delegate void PickInfoUpdate(IClientAPI client, UUID pickID, UUID creatorID, bool topPick, string name, string desc, UUID snapshotID, int sortOrder, bool enabled);
    public delegate void AvatarNotesUpdate(IClientAPI client, UUID targetID, string notes);
    public delegate void MuteListRequest(IClientAPI client, uint muteCRC);
    public delegate void AvatarInterestUpdate(IClientAPI client, uint wantmask, string wanttext, uint skillsmask, string skillstext, string languages);
    public delegate void GrantUserFriendRights(IClientAPI client, UUID target, int rights);
    public delegate void PlacesQuery(UUID QueryID, UUID TransactionID, string QueryText, uint QueryFlags, byte Category, string SimName, IClientAPI client);

    public delegate void AgentFOV(IClientAPI client, float verticalAngle);
    
    public delegate void MuteListEntryUpdate(IClientAPI client, UUID MuteID, string Name, int type, uint flags);
    
    public delegate void MuteListEntryRemove(IClientAPI client, UUID MuteID, string Name);
    
    public delegate void AvatarInterestReply(IClientAPI client,UUID target, uint wantmask, string wanttext, uint skillsmask, string skillstext, string languages);
    
    public delegate void FindAgentUpdate(IClientAPI client, UUID hunter, UUID target);
    
    public delegate void TrackAgentUpdate(IClientAPI client, UUID hunter, UUID target);
    
    public delegate void FreezeUserUpdate(IClientAPI client, UUID parcelowner,uint flags, UUID target);
    
    public delegate void EjectUserUpdate(IClientAPI client, UUID parcelowner,uint flags, UUID target);
    
    public delegate void NewUserReport(IClientAPI client, string regionName,UUID abuserID, byte catagory, byte checkflags, string details, UUID objectID, Vector3 postion, byte reportType ,UUID screenshotID, string Summary, UUID reporter);
    
    public delegate void GodUpdateRegionInfoUpdate(IClientAPI client, float BillableFactor, ulong EstateID, ulong RegionFlags, byte[] SimName,int RedirectX, int RedirectY);
    
    public delegate void GodlikeMessage(IClientAPI client, UUID requester, byte[] Method, byte[] Parameter);
    
    public delegate void SaveStateHandler(IClientAPI client,UUID agentID);
    
    public delegate void GroupAccountSummaryRequest(IClientAPI client,UUID agentID, UUID groupID);
    
    public delegate void GroupAccountDetailsRequest(IClientAPI client,UUID agentID, UUID groupID, UUID transactionID, UUID sessionID);
    
    public delegate void GroupAccountTransactionsRequest(IClientAPI client,UUID agentID, UUID groupID, UUID transactionID, UUID sessionID);
    
    public delegate void ParcelBuyPass(IClientAPI client, UUID agentID, int ParcelLocalID);
    
    public delegate void ParcelGodMark(IClientAPI client, UUID agentID, int ParcelLocalID);
    
    public delegate void GroupActiveProposalsRequest(IClientAPI client,UUID agentID, UUID groupID, UUID transactionID, UUID sessionID);
    
    public delegate void GroupVoteHistoryRequest(IClientAPI client,UUID agentID, UUID groupID, UUID transactionID, UUID sessionID);
    
    
    public delegate void SimWideDeletesDelegate(IClientAPI client,UUID agentID, int flags, UUID targetID);
    
    public delegate void SendPostcard(IClientAPI client);
    public delegate void ChangeInventoryItemFlags(IClientAPI client, UUID itemID, uint flags);

    #endregion

    public struct DirPlacesReplyData
    {
        public UUID parcelID;
        public string name;
        public bool forSale;
        public bool auction;
        public float dwell;
        public uint Status;
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
        public uint Status;
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
        public uint Status;
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

    public class IEntityUpdate
    {
        private ISceneEntity m_entity;
        private uint m_flags;
        private int m_updateTime;

        public ISceneEntity Entity
        {
            get { return m_entity; }
        }

        public uint Flags
        {
            get { return m_flags; }
        }

        public int UpdateTime
        {
            get { return m_updateTime; }
        }

        public virtual void Update(IEntityUpdate update)
        {
            m_flags |= update.Flags;

            // Use the older of the updates as the updateTime
            if (Util.EnvironmentTickCountCompare(UpdateTime, update.UpdateTime) > 0)
                m_updateTime = update.UpdateTime;
        }

        public IEntityUpdate(ISceneEntity entity, uint flags)
        {
            m_entity = entity;
            m_flags = flags;
            m_updateTime = Util.EnvironmentTickCount();
        }

        public IEntityUpdate(ISceneEntity entity, uint flags, Int32 updateTime)
        {
            m_entity = entity;
            m_flags = flags;
            m_updateTime = updateTime;
        }
    }

    public class EntityUpdate : IEntityUpdate
    {
        private float m_timeDilation;

        public float TimeDilation
        {
            get { return m_timeDilation; }
        }

        public EntityUpdate(ISceneEntity entity, PrimUpdateFlags flags, float timedilation)
            : base(entity, (uint)flags)
        {
            // Flags = flags;
            m_timeDilation = timedilation;
        }

        public EntityUpdate(ISceneEntity entity, PrimUpdateFlags flags, float timedilation, Int32 updateTime)
            : base(entity,(uint)flags,updateTime)
        {
            m_timeDilation = timedilation;
        }
    }

    public class PlacesReplyData
    {
        public UUID OwnerID;
        public string Name;
        public string Desc;
        public int ActualArea;
        public int BillableArea;
        public byte Flags;
        public uint GlobalX;
        public uint GlobalY;
        public uint GlobalZ;
        public string SimName;
        public UUID SnapshotID;
        public uint Dwell;
        public int Price;
    }

    /// <summary>
    /// Specifies the fields that have been changed when sending a prim or
    /// avatar update
    /// </summary>
    [Flags]
    public enum PrimUpdateFlags : uint
    {
        None = 0,
        AttachmentPoint = 1 << 0,
        Material = 1 << 1,
        ClickAction = 1 << 2,
        Scale = 1 << 3,
        ParentID = 1 << 4,
        PrimFlags = 1 << 5,
        PrimData = 1 << 6,
        MediaURL = 1 << 7,
        ScratchPad = 1 << 8,
        Textures = 1 << 9,
        TextureAnim = 1 << 10,
        NameValue = 1 << 11,
        Position = 1 << 12,
        Rotation = 1 << 13,
        Velocity = 1 << 14,
        Acceleration = 1 << 15,
        AngularVelocity = 1 << 16,
        CollisionPlane = 1 << 17,
        Text = 1 << 18,
        Particles = 1 << 19,
        ExtraData = 1 << 20,
        Sound = 1 << 21,
        Joint = 1 << 22,
        FullUpdate = UInt32.MaxValue
    }

    public static class PrimUpdateFlagsExtensions
    {
        public static bool HasFlag(this PrimUpdateFlags updateFlags, PrimUpdateFlags flag)
        {
            return (updateFlags & flag) == flag;
        }
    }

    public interface IClientAPI
    {
        Vector3 StartPos { get; set; }

        UUID AgentId { get; }

        /// <summary>
        /// The scene agent for this client.  This will only be set if the client has an agent in a scene (i.e. if it
        /// is connected).
        /// </summary>
        ISceneAgent SceneAgent { get; set; }

        UUID SessionId { get; }

        UUID SecureSessionId { get; }

        UUID ActiveGroupId { get; }

        string ActiveGroupName { get; }

        ulong ActiveGroupPowers { get; }

        ulong GetGroupPowers(UUID groupID);

        bool IsGroupMember(UUID GroupID);

        string FirstName { get; }

        string LastName { get; }

        IScene Scene { get; }

        List<uint> SelectedObjects { get; }

        // [Obsolete("LLClientView Specific - Replace with ???")]
        int NextAnimationSequenceNumber { get; }

        /// <summary>
        /// Returns the full name of the agent/avatar represented by this client
        /// </summary>
        string Name { get; }

        /// <summary>
        /// True if the client is active (sending and receiving new UDP messages).  False if the client is being closed.
        /// </summary>
        bool IsActive { get; set; }

        int PingTimeMS { get; }

        /// <summary>
        /// Set if the client is closing due to a logout request
        /// </summary>
        /// <remarks>
        /// Do not use this flag if you want to know if the client is closing, since it will not be set in other
        /// circumstances (e.g. if a child agent is closed or the agent is kicked off the simulator).  Use IsActive
        /// instead with a IClientAPI.SceneAgent.IsChildAgent check if necessary.
        ///
        /// Only set for root agents.
        /// </remarks>
        bool IsLoggingOut { get; set; }
        
        bool SendLogoutPacketWhenClosing { set; }

        // [Obsolete("LLClientView Specific - Circuits are unique to LLClientView")]
        uint CircuitCode { get; }

        IPEndPoint RemoteEndPoint { get; }

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
        event EstateManageTelehub OnEstateManageTelehub;
        // [Obsolete("LLClientView Specific.")]
        event CachedTextureRequest OnCachedTextureRequest;
        event SetAppearance OnSetAppearance;
        // [Obsolete("LLClientView Specific - Replace and rename OnAvatarUpdate. Difference from SetAppearance?")]
        event AvatarNowWearing OnAvatarNowWearing;
        event RezSingleAttachmentFromInv OnRezSingleAttachmentFromInv;
        event RezMultipleAttachmentsFromInv OnRezMultipleAttachmentsFromInv;
        event UUIDNameRequest OnDetachAttachmentIntoInv;
        event ObjectAttach OnObjectAttach;
        event ObjectDeselect OnObjectDetach;
        event ObjectDrop OnObjectDrop;
        event StartAnim OnStartAnim;
        event StopAnim OnStopAnim;
        event ChangeAnim OnChangeAnim;
        event LinkObjects OnLinkObjects;
        event DelinkObjects OnDelinkObjects;
        event RequestMapBlocks OnRequestMapBlocks;
        event RequestMapName OnMapNameRequest;
        event TeleportLocationRequest OnTeleportLocationRequest;
        event DisconnectUser OnDisconnectUser;
        event RequestAvatarProperties OnRequestAvatarProperties;
        event SetAlwaysRun OnSetAlwaysRun;
        event TeleportLandmarkRequest OnTeleportLandmarkRequest;
        event TeleportCancel OnTeleportCancel;
        event DeRezObject OnDeRezObject;
        event Action<IClientAPI> OnRegionHandShakeReply;
        event GenericCall1 OnRequestWearables;
        event Action<IClientAPI, bool> OnCompleteMovementToRegion;

        /// <summary>
        /// Called when an AgentUpdate message is received and before OnAgentUpdate.
        /// </summary>
        /// <remarks>
        /// Listeners must not retain a reference to AgentUpdateArgs since this object may be reused for subsequent AgentUpdates.
        /// </remarks>
        event UpdateAgent OnPreAgentUpdate;

        /// <summary>
        /// Called when an AgentUpdate message is received and after OnPreAgentUpdate.
        /// </summary>
        /// <remarks>
        /// Listeners must not retain a reference to AgentUpdateArgs since this object may be reused for subsequent AgentUpdates.
        /// </remarks>
        event UpdateAgent OnAgentUpdate;

        event UpdateAgent OnAgentCameraUpdate;

        event AgentRequestSit OnAgentRequestSit;
        event AgentSit OnAgentSit;
        event AvatarPickerRequest OnAvatarPickerRequest;
        event Action<IClientAPI> OnRequestAvatarsData;
        event AddNewPrim OnAddPrim;

        event FetchInventory OnAgentDataUpdateRequest;
        event TeleportLocationRequest OnSetStartLocationRequest;

        event RequestGodlikePowers OnRequestGodlikePowers;
        event GodKickUser OnGodKickUser;

        event ObjectDuplicate OnObjectDuplicate;
        event ObjectDuplicateOnRay OnObjectDuplicateOnRay;
        event GrabObject OnGrabObject;
        event DeGrabObject OnDeGrabObject;
        event MoveObject OnGrabUpdate;
        event SpinStart OnSpinStart;
        event SpinObject OnSpinUpdate;
        event SpinStop OnSpinStop;

        event UpdateShape OnUpdatePrimShape;
        event ObjectExtraParams OnUpdateExtraParams;
        event ObjectRequest OnObjectRequest;
        event ObjectSelect OnObjectSelect;
        event ObjectDeselect OnObjectDeselect;
        event GenericCall7 OnObjectDescription;
        event GenericCall7 OnObjectName;
        event GenericCall7 OnObjectClickAction;
        event GenericCall7 OnObjectMaterial;
        event RequestObjectPropertiesFamily OnRequestObjectPropertiesFamily;
        event UpdatePrimFlags OnUpdatePrimFlags;
        event UpdatePrimTexture OnUpdatePrimTexture;
        event ClientChangeObject onClientChangeObject;
        event UpdateVector OnUpdatePrimGroupPosition;
        event UpdateVector OnUpdatePrimSinglePosition;
        event UpdatePrimRotation OnUpdatePrimGroupRotation;
        event UpdatePrimSingleRotation OnUpdatePrimSingleRotation;
        event UpdatePrimSingleRotationPosition OnUpdatePrimSingleRotationPosition;
        event UpdatePrimGroupRotation OnUpdatePrimGroupMouseRotation;
        event UpdateVector OnUpdatePrimScale;
        event UpdateVector OnUpdatePrimGroupScale;
        event StatusChange OnChildAgentStatus;
        event GenericCall2 OnStopMovement;
        event Action<UUID> OnRemoveAvatar;
        event ObjectPermissions OnObjectPermissions;

        event CreateNewInventoryItem OnCreateNewInventoryItem;
        event LinkInventoryItem OnLinkInventoryItem;
        event CreateInventoryFolder OnCreateNewInventoryFolder;
        event UpdateInventoryFolder OnUpdateInventoryFolder;
        event MoveInventoryFolder OnMoveInventoryFolder;
        event FetchInventoryDescendents OnFetchInventoryDescendents;
        event PurgeInventoryDescendents OnPurgeInventoryDescendents;
        event FetchInventory OnFetchInventory;
        event RequestTaskInventory OnRequestTaskInventory;
        event UpdateInventoryItem OnUpdateInventoryItem;
        event CopyInventoryItem OnCopyInventoryItem;
        event MoveItemsAndLeaveCopy OnMoveItemsAndLeaveCopy;
        event MoveInventoryItem OnMoveInventoryItem;
        event RemoveInventoryFolder OnRemoveInventoryFolder;
        event RemoveInventoryItem OnRemoveInventoryItem;
        event UDPAssetUploadRequest OnAssetUploadRequest;
        event XferReceive OnXferReceive;
        event RequestXfer OnRequestXfer;
        event ConfirmXfer OnConfirmXfer;
        event AbortXfer OnAbortXfer;
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
        event ParcelDeedToGroup OnParcelDeedToGroup;
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

        event RequestTerrain OnRequestTerrain;

        event RequestTerrain OnUploadTerrain;

        event ObjectIncludeInSearch OnObjectIncludeInSearch;

        event UUIDNameRequest OnTeleportHomeRequest;

        event ScriptAnswer OnScriptAnswer;

        event AgentSit OnUndo;
        event AgentSit OnRedo;
        event LandUndo OnLandUndo;

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
        event SimulatorBlueBoxMessageRequest OnSimulatorBlueBoxMessageRequest;
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
        event Action<Vector3, bool, bool> OnAutoPilotGo;

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

        event OfferCallingCard OnOfferCallingCard;
        event AcceptCallingCard OnAcceptCallingCard;
        event DeclineCallingCard OnDeclineCallingCard;
        event SoundTrigger OnSoundTrigger;

        event StartLure OnStartLure;
        event TeleportLureRequest OnTeleportLureRequest;
        event NetworkStats OnNetworkStatsUpdate;

        event ClassifiedInfoRequest OnClassifiedInfoRequest;
        event ClassifiedInfoUpdate OnClassifiedInfoUpdate;
        event ClassifiedDelete OnClassifiedDelete;
        event ClassifiedGodDelete OnClassifiedGodDelete;

        event EventNotificationAddRequest OnEventNotificationAddRequest;
        event EventNotificationRemoveRequest OnEventNotificationRemoveRequest;
        event EventGodDelete OnEventGodDelete;

        event ParcelDwellRequest OnParcelDwellRequest;

        event UserInfoRequest OnUserInfoRequest;
        event UpdateUserInfo OnUpdateUserInfo;

        event RetrieveInstantMessages OnRetrieveInstantMessages;

        event PickDelete OnPickDelete;
        event PickGodDelete OnPickGodDelete;
        event PickInfoUpdate OnPickInfoUpdate;
        event AvatarNotesUpdate OnAvatarNotesUpdate;
        event AvatarInterestUpdate OnAvatarInterestUpdate;
        event GrantUserFriendRights OnGrantUserRights;

        event MuteListRequest OnMuteListRequest;

        event PlacesQuery OnPlacesQuery;
        
        event FindAgentUpdate OnFindAgent;
        event TrackAgentUpdate OnTrackAgent;
        event NewUserReport OnUserReport;
        event SaveStateHandler OnSaveState;
        event GroupAccountSummaryRequest OnGroupAccountSummaryRequest;
        event GroupAccountDetailsRequest OnGroupAccountDetailsRequest;
        event GroupAccountTransactionsRequest OnGroupAccountTransactionsRequest;
        event FreezeUserUpdate OnParcelFreezeUser;
        event EjectUserUpdate OnParcelEjectUser;
        event ParcelBuyPass OnParcelBuyPass;
        event ParcelGodMark OnParcelGodMark;
        event GroupActiveProposalsRequest OnGroupActiveProposalsRequest;
        event GroupVoteHistoryRequest OnGroupVoteHistoryRequest;
        event SimWideDeletesDelegate OnSimWideDeletes;
        event SendPostcard OnSendPostcard;
        event ChangeInventoryItemFlags OnChangeInventoryItemFlags;
        event MuteListEntryUpdate OnUpdateMuteListEntry;
        event MuteListEntryRemove OnRemoveMuteListEntry;
        event GodlikeMessage onGodlikeMessage;
        event GodUpdateRegionInfoUpdate OnGodUpdateRegionInfoUpdate;
        event GenericCall2 OnUpdateThrottles;
        /// <summary>
        /// Set the debug level at which packet output should be printed to console.
        /// </summary>
        int DebugPacketLevel { get; set; }

        void InPacket(object NewPack);
        void ProcessInPacket(Packet NewPack);

        /// <summary>
        /// Close this client
        /// </summary>
        void Close();

        /// <summary>
        /// Close this client
        /// </summary>
        /// <param name='force'>
        /// If true, attempts the close without checking active status.  You do not want to try this except as a last
        /// ditch attempt where Active == false but the ScenePresence still exists.
        /// </param>
        void Close(bool sendStop, bool force);

        void Kick(string message);
        
        /// <summary>
        /// Start processing for this client.
        /// </summary>
        void Start();
        
        void Stop();

        //     void ActivateGesture(UUID assetId, UUID gestureId);

        /// <summary>
        /// Tell this client what items it should be wearing now
        /// </summary>
        void SendWearables(AvatarWearable[] wearables, int serial);

        /// <summary>
        /// Send information about the given agent's appearance to another client.
        /// </summary>
        /// <param name="agentID">The id of the agent associated with the appearance</param>
        /// <param name="visualParams"></param>
        /// <param name="textureEntry"></param>
        void SendAppearance(UUID agentID, byte[] visualParams, byte[] textureEntry);

        void SendCachedTextureResponse(ISceneEntity avatar, int serial, List<CachedTextureResponseArg> cachedTextures);

        void SendStartPingCheck(byte seq);

        /// <summary>
        /// Tell the client that an object has been deleted
        /// </summary>
        /// <param name="localID"></param>
        void SendKillObject(List<uint> localID);

        void SendPartFullUpdate(ISceneEntity ent, uint? parentID);

        void SendAnimations(UUID[] animID, int[] seqs, UUID sourceAgentId, UUID[] objectIDs);
        void SendRegionHandshake(RegionInfo regionInfo, RegionHandshakeArgs args);

        /// <summary>
        /// Send chat to the viewer.
        /// </summary>
        /// <param name='message'></param>
        /// <param name='type'></param>
        /// <param name='fromPos'></param>
        /// <param name='fromName'></param>
        /// <param name='fromAgentID'></param>
        /// <param name='ownerID'></param>
        /// <param name='source'></param>
        /// <param name='audible'></param>
        void SendChatMessage(
            string message, byte type, Vector3 fromPos, string fromName, UUID fromAgentID, UUID ownerID, byte source,
            byte audible);

        void SendInstantMessage(GridInstantMessage im);

        void SendGenericMessage(string method, UUID invoice, List<string> message);
        void SendGenericMessage(string method, UUID invoice, List<byte[]> message);

        bool CanSendLayerData();

        void SendLayerData(float[] map);
        void SendLayerData(int px, int py, float[] map);

        void SendWindData(Vector2[] windSpeeds);
        void SendCloudData(float[] cloudCover);

        /// <summary>
        /// Sent when an agent completes its movement into a region.
        /// </summary>
        /// <remarks>
        /// This packet marks completion of the arrival of a root avatar in a region, whether through login, region
        /// crossing or direct teleport.
        /// </remarks>
        void MoveAgentIntoRegion(RegionInfo regInfo, Vector3 pos, Vector3 look);

        void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourExternalEndPoint);
        
        /// <summary>
        /// Return circuit information for this client.
        /// </summary>
        /// <returns></returns>
        AgentCircuitData RequestClientInfo();

        void CrossRegion(ulong newRegionHandle, Vector3 pos, Vector3 lookAt, IPEndPoint newRegionExternalEndPoint,
                         string capsURL);

        void SendMapBlock(List<MapBlockData> mapBlocks, uint flag);
        void SendLocalTeleport(Vector3 position, Vector3 lookAt, uint flags);

        void SendRegionTeleport(ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint, uint locationID,
                                uint flags, string capsURL);

        void SendTeleportFailed(string reason);
        void SendTeleportStart(uint flags);
        void SendTeleportProgress(uint flags, string message);

        void SendMoneyBalance(UUID transaction, bool success, byte[] description, int balance, int transactionType, UUID sourceID, bool sourceIsGroup, UUID destID, bool destIsGroup, int amount, string item);

        void SendPayPrice(UUID objectID, int[] payPrice);

        void SendCoarseLocationUpdate(List<UUID> users, List<Vector3> CoarseLocations);

        void SetChildAgentThrottle(byte[] throttle);
        void SetChildAgentThrottle(byte[] throttle,float factor);

        void SetAgentThrottleSilent(int throttle, int setting);
        int GetAgentThrottleSilent(int throttle);

        void SendAvatarDataImmediate(ISceneEntity avatar);

        /// <summary>
        /// Send a positional, velocity, etc. update to the viewer for a given entity.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="updateFlags"></param>
        void SendEntityUpdate(ISceneEntity entity, PrimUpdateFlags updateFlags);

        void ReprioritizeUpdates();
        void FlushPrimUpdates();

        void SendInventoryFolderDetails(UUID ownerID, UUID folderID, List<InventoryItemBase> items,
                                        List<InventoryFolderBase> folders, int version, bool fetchFolders,
                                        bool fetchItems);

        void SendInventoryItemDetails(UUID ownerID, InventoryItemBase item);

        /// <summary>
        /// Tell the client that we have created the item it requested.
        /// </summary>
        /// <param name="Item"></param>
        void SendInventoryItemCreateUpdate(InventoryItemBase Item, uint callbackId);
        void SendInventoryItemCreateUpdate(InventoryItemBase Item, UUID transactionID, uint callbackId);

        void SendRemoveInventoryItem(UUID itemID);

        void SendTakeControls(int controls, bool passToAgent, bool TakeControls);

        void SendTaskInventory(UUID taskID, short serial, byte[] fileName);

        void SendTelehubInfo(UUID ObjectID, string ObjectName, Vector3 ObjectPos, Quaternion ObjectRot, List<Vector3> SpawnPoint);

        /// <summary>
        /// Used by the server to inform the client of new inventory items and folders.
        /// </summary>
        /// 
        /// If the node is a folder then the contents will be transferred
        /// (including all descendent folders) as well as the folder itself.
        /// 
        /// <param name="node"></param>
        void SendBulkUpdateInventory(InventoryNodeBase node);

        void SendXferPacket(ulong xferID, uint packet, byte[] data, bool isTaskInventory);

        void SendAbortXferPacket(ulong xferID);

        void SendEconomyData(float EnergyEfficiency, int ObjectCapacity, int ObjectCount, int PriceEnergyUnit,
                             int PriceGroupCreate, int PriceObjectClaim, float PriceObjectRent,
                             float PriceObjectScaleFactor,
                             int PriceParcelClaim, float PriceParcelClaimFactor, int PriceParcelRent,
                             int PricePublicObjectDecay,
                             int PricePublicObjectDelete, int PriceRentLight, int PriceUpload, int TeleportMinPrice,
                             float TeleportPriceExponent);

        void SendAvatarPickerReply(AvatarPickerReplyAgentDataArgs AgentData, List<AvatarPickerReplyDataArgs> Data);

        void SendAgentDataUpdate(UUID agentid, UUID activegroupid, string firstname, string lastname, ulong grouppowers,
                                 string groupname, string grouptitle);

        void SendPreLoadSound(UUID objectID, UUID ownerID, UUID soundID);
        void SendPlayAttachedSound(UUID soundID, UUID objectID, UUID ownerID, float gain, byte flags);

        void SendTriggeredSound(UUID soundID, UUID ownerID, UUID objectID, UUID parentID, ulong handle, Vector3 position,
                                float gain);

        void SendAttachedSoundGainChange(UUID objectID, float gain);

        void SendNameReply(UUID profileId, string firstname, string lastname);
        void SendAlertMessage(string message);

        void SendAgentAlertMessage(string message, bool modal);
        void SendLoadURL(string objectname, UUID objectID, UUID ownerID, bool groupOwned, string message, string url);

        /// <summary>
        /// Open a dialog box on the client.
        /// </summary>
        /// <param name="objectname"></param>
        /// <param name="objectID"></param>
        /// <param name="ownerID">/param>
        /// <param name="ownerFirstName"></param>
        /// <param name="ownerLastName"></param>
        /// <param name="msg"></param>
        /// <param name="textureID"></param>
        /// <param name="ch"></param>
        /// <param name="buttonlabels"></param>
        void SendDialog(string objectname, UUID objectID, UUID ownerID, string ownerFirstName, string ownerLastName, string msg, UUID textureID, int ch,
                        string[] buttonlabels);

        /// <summary>
        /// Update the client as to where the sun is currently located.
        /// </summary>
        /// <param name="sunPos"></param>
        /// <param name="sunVel"></param>
        /// <param name="CurrentTime">Seconds since Unix Epoch 01/01/1970 00:00:00</param>
        /// <param name="SecondsPerSunCycle"></param>
        /// <param name="SecondsPerYear"></param>
        /// <param name="OrbitalPosition">The orbital position is given in radians, and must be "adjusted" for the linden client, see LLClientView</param>
        void SendSunPos(Vector3 sunPos, Vector3 sunVel, ulong CurrentTime, uint SecondsPerSunCycle, uint SecondsPerYear,
                        float OrbitalPosition);
        
        void SendViewerEffect(ViewerEffectPacket.EffectBlock[] effectBlocks);
        void SendViewerTime(int phase);

        void SendAvatarProperties(UUID avatarID, string aboutText, string bornOn, Byte[] charterMember, string flAbout,
                                  uint flags, UUID flImageID, UUID imageID, string profileURL, UUID partnerID);

        void SendScriptQuestion(UUID taskID, string taskName, string ownerName, UUID itemID, int question);
        void SendHealth(float health);


        void SendEstateList(UUID invoice, int code, UUID[] Data, uint estateID);

        void SendBannedUserList(UUID invoice, EstateBan[] banlist, uint estateID);

        void SendRegionInfoToEstateMenu(RegionInfoForEstateMenuArgs args);
        void SendEstateCovenantInformation(UUID covenant);

        void SendDetailedEstateData(UUID invoice, string estateName, uint estateID, uint parentEstate, uint estateFlags,
                                    uint sunPosition, UUID covenant, uint covenantChanged, string abuseEmail, UUID estateOwner);

        /// <summary>
        /// Send land properties to the client.
        /// </summary>
        /// <param name="sequence_id"></param>
        /// <param name="snap_selection"></param>
        /// <param name="request_result"></param>
        /// <param name="lo"></param></param>
        /// <param name="parcelObjectCapacity">/param>
        /// <param name="simObjectCapacity"></param>
        /// <param name="regionFlags"></param>
        void SendLandProperties(int sequence_id, bool snap_selection, int request_result, ILandObject lo,
                                float simObjectBonusFactor, int parcelObjectCapacity, int simObjectCapacity,
                                uint regionFlags);

        void SendLandAccessListData(List<LandAccessEntry> accessList, uint accessFlag, int localLandID);
        void SendForceClientSelectObjects(List<uint> objectIDs);
        void SendCameraConstraint(Vector4 ConstraintPlane);
        void SendLandObjectOwners(LandData land, List<UUID> groups, Dictionary<UUID, int> ownersAndCount);
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

        void SendInitiateDownload(string simFileName, string clientFileName);

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

        /// <summary>
        /// Tell the client that the requested texture cannot be found
        /// </summary>
        void SendImageNotFound(UUID imageid);

        void SendShutdownConnectionNotice();

        /// <summary>
        /// Send statistical information about the sim to the client.
        /// </summary>
        /// <param name="stats"></param>
        void SendSimStats(SimStats stats);

        void SendObjectPropertiesFamilyData(ISceneEntity Entity, uint RequestFlags);

        void SendObjectPropertiesReply(ISceneEntity Entity);

        void SendPartPhysicsProprieties(ISceneEntity Entity);

        void SendAgentOffline(UUID[] agentIDs);

        void SendAgentOnline(UUID[] agentIDs);

        void SendFindAgent(UUID HunterID, UUID PreyID, double GlobalX, double GlobalY);

        void SendSitResponse(UUID TargetID, Vector3 OffsetPos, Quaternion SitOrientation, bool autopilot,
                             Vector3 CameraAtOffset, Vector3 CameraEyeOffset, bool ForceMouseLook);

        void SendAdminResponse(UUID Token, uint AdminLevel);

        void SendGroupMembership(GroupMembershipData[] GroupMembership);

        void SendGroupNameReply(UUID groupLLUID, string GroupName);

        void SendJoinGroupReply(UUID groupID, bool success);

        void SendEjectGroupMemberReply(UUID agentID, UUID groupID, bool success);

        void SendLeaveGroupReply(UUID groupID, bool success);

        void SendCreateGroupReply(UUID groupID, bool success, string message);

        void SendLandStatReply(uint reportType, uint requestFlags, uint resultCount, LandStatReportItem[] lsrpia);

        void SendScriptRunningReply(UUID objectID, UUID itemID, bool running);

        void SendAsset(AssetRequestToClient req);

        void SendTexture(AssetBase TextureAsset);

        byte[] GetThrottlesPacked(float multiplier);

        event ViewerEffectEventHandler OnViewerEffect;
        event Action<IClientAPI> OnLogout;
        event Action<IClientAPI> OnConnectionClosed;

        void SendBlueBoxMessage(UUID FromAvatarID, String FromAvatarName, String Message);

        void SendLogoutPacket();

        // WARNING WARNING WARNING
        //
        // The two following methods are EXCLUSIVELY for the load balancer.
        // they cause a MASSIVE performance hit!
        //
        ClientInfo GetClientInfo();
        void SetClientInfo(ClientInfo info);

        void SetClientOption(string option, string value);
        string GetClientOption(string option);

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

        void SendAvatarGroupsReply(UUID avatarID, GroupMembershipData[] data);
        void SendAgentGroupDataUpdate(UUID avatarID, GroupMembershipData[] data);
        void SendOfferCallingCard(UUID srcID, UUID transactionID);
        void SendAcceptCallingCard(UUID transactionID);
        void SendDeclineCallingCard(UUID transactionID);

        void SendTerminateFriend(UUID exFriendID);

        void SendAvatarClassifiedReply(UUID targetID, UUID[] classifiedID, string[] name);
        void SendClassifiedInfoReply(UUID classifiedID, UUID creatorID, uint creationDate, uint expirationDate, uint category, string name, string description, UUID parcelID, uint parentEstate, UUID snapshotID, string simName, Vector3 globalPos, string parcelName, byte classifiedFlags, int price);

        void SendAgentDropGroup(UUID groupID);
        void RefreshGroupMembership();
        void SendAvatarNotesReply(UUID targetID, string text);
        void SendAvatarPicksReply(UUID targetID, Dictionary<UUID, string> picks);
        void SendPickInfoReply(UUID pickID,UUID creatorID, bool topPick, UUID parcelID, string name, string desc, UUID snapshotID, string user, string originalName, string simName, Vector3 posGlobal, int sortOrder, bool enabled);

        void SendAvatarClassifiedReply(UUID targetID, Dictionary<UUID, string> classifieds);

        void SendParcelDwellReply(int localID, UUID parcelID, float dwell);

        void SendUserInfoReply(bool imViaEmail, bool visible, string email);
        
        void SendUseCachedMuteList();

        void SendMuteListUpdate(string filename);

        void SendGroupActiveProposals(UUID groupID, UUID transactionID, GroupActiveProposals[] Proposals);

        void SendGroupVoteHistory(UUID groupID, UUID transactionID, GroupVoteHistory[] Votes);

        bool AddGenericPacketHandler(string MethodName, GenericMessage handler);

        void SendRebakeAvatarTextures(UUID textureID);

        void SendAvatarInterestsReply(UUID avatarID, uint wantMask, string wantText, uint skillsMask, string skillsText, string languages);
        
        void SendGroupAccountingDetails(IClientAPI sender,UUID groupID, UUID transactionID, UUID sessionID, int amt);
        
        void SendGroupAccountingSummary(IClientAPI sender,UUID groupID, uint moneyAmt, int totalTier, int usedTier);
        
        void SendGroupTransactionsSummaryDetails(IClientAPI sender,UUID groupID, UUID transactionID, UUID sessionID,int amt);
        
        void SendChangeUserRights(UUID agentID, UUID friendID, int rights);
        void SendTextBoxRequest(string message, int chatChannel, string objectname, UUID ownerID, string ownerFirstName, string ownerLastName, UUID objectId);

        void SendAgentTerseUpdate(ISceneEntity presence);

        void SendPlacesReply(UUID queryID, UUID transactionID, PlacesReplyData[] data);
    }
}
