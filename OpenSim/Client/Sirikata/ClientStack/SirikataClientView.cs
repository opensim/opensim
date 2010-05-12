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
using System.Net.Sockets;
using System.Text;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Client;

namespace OpenSim.Client.Sirikata.ClientStack
{
    class SirikataClientView : IClientAPI, IClientCore 
    {
        private readonly NetworkStream stream;

        public SirikataClientView(TcpClient client)
        {
            stream = client.GetStream();

            sessionId = UUID.Random();


            // Handshake with client
            string con = "SSTTCP01" + sessionId;
            byte[] handshake = Util.UTF8.GetBytes(con);

            byte[] clientHandshake = new byte[2+6+36];

            stream.Read(clientHandshake, 0, handshake.Length);
            stream.Write(handshake, 0, handshake.Length - 1); // Remove null terminator (hence the -1)
        }


        #region Implementation of IClientAPI

        private Vector3 startPos;

        private UUID sessionId;

        private UUID secureSessionId;

        private UUID activeGroupId;

        private string activeGroupName;

        private ulong activeGroupPowers;

        private string firstName;

        private string lastName;

        private IScene scene;

        private int nextAnimationSequenceNumber;

        private string name;

        private bool isActive;

        private bool sendLogoutPacketWhenClosing;

        private uint circuitCode;

        private IPEndPoint remoteEndPoint;

        public Vector3 StartPos
        {
            get { return startPos; }
            set { startPos = value; }
        }

        public bool TryGet<T>(out T iface)
        {
            throw new System.NotImplementedException();
        }

        public T Get<T>()
        {
            throw new System.NotImplementedException();
        }

        UUID IClientCore.AgentId
        {
            get { throw new NotImplementedException(); }
        }

        public void Disconnect(string reason)
        {
            throw new System.NotImplementedException();
        }

        public void Disconnect()
        {
            throw new System.NotImplementedException();
        }

        UUID IClientAPI.AgentId
        {
            get { throw new NotImplementedException(); }
        }

        public UUID SessionId
        {
            get { return sessionId; }
        }

        public UUID SecureSessionId
        {
            get { return secureSessionId; }
        }

        public UUID ActiveGroupId
        {
            get { return activeGroupId; }
        }

        public string ActiveGroupName
        {
            get { return activeGroupName; }
        }

        public ulong ActiveGroupPowers
        {
            get { return activeGroupPowers; }
        }

        public ulong GetGroupPowers(UUID groupID)
        {
            throw new System.NotImplementedException();
        }

        public bool IsGroupMember(UUID GroupID)
        {
            throw new System.NotImplementedException();
        }

        public string FirstName
        {
            get { return firstName; }
        }

        public string LastName
        {
            get { return lastName; }
        }

        public IScene Scene
        {
            get { return scene; }
        }

        public int NextAnimationSequenceNumber
        {
            get { return nextAnimationSequenceNumber; }
        }

        public string Name
        {
            get { return name; }
        }

        public bool IsActive
        {
            get { return isActive; }
            set { isActive = value; }
        }
        public bool IsLoggingOut
        {
            get { return false; }
            set { }
        }

        public bool SendLogoutPacketWhenClosing
        {
            set { sendLogoutPacketWhenClosing = value; }
        }

        public uint CircuitCode
        {
            get { return circuitCode; }
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return remoteEndPoint; }
        }

        public event GenericMessage OnGenericMessage;
        public event ImprovedInstantMessage OnInstantMessage;
        public event ChatMessage OnChatFromClient;
        public event TextureRequest OnRequestTexture;
        public event RezObject OnRezObject;
        public event ModifyTerrain OnModifyTerrain;
        public event BakeTerrain OnBakeTerrain;
        public event EstateChangeInfo OnEstateChangeInfo;
        public event SetAppearance OnSetAppearance;
        public event AvatarNowWearing OnAvatarNowWearing;
        public event RezSingleAttachmentFromInv OnRezSingleAttachmentFromInv;
        public event RezMultipleAttachmentsFromInv OnRezMultipleAttachmentsFromInv;
        public event UUIDNameRequest OnDetachAttachmentIntoInv;
        public event ObjectAttach OnObjectAttach;
        public event ObjectDeselect OnObjectDetach;
        public event ObjectDrop OnObjectDrop;
        public event StartAnim OnStartAnim;
        public event StopAnim OnStopAnim;
        public event LinkObjects OnLinkObjects;
        public event DelinkObjects OnDelinkObjects;
        public event RequestMapBlocks OnRequestMapBlocks;
        public event RequestMapName OnMapNameRequest;
        public event TeleportLocationRequest OnTeleportLocationRequest;
        public event DisconnectUser OnDisconnectUser;
        public event RequestAvatarProperties OnRequestAvatarProperties;
        public event SetAlwaysRun OnSetAlwaysRun;
        public event TeleportLandmarkRequest OnTeleportLandmarkRequest;
        public event DeRezObject OnDeRezObject;
        public event Action<IClientAPI> OnRegionHandShakeReply;
        public event GenericCall2 OnRequestWearables;
        public event GenericCall1 OnCompleteMovementToRegion;
        public event UpdateAgent OnPreAgentUpdate;
        public event UpdateAgent OnAgentUpdate;
        public event AgentRequestSit OnAgentRequestSit;
        public event AgentSit OnAgentSit;
        public event AvatarPickerRequest OnAvatarPickerRequest;
        public event Action<IClientAPI> OnRequestAvatarsData;
        public event AddNewPrim OnAddPrim;
        public event FetchInventory OnAgentDataUpdateRequest;
        public event TeleportLocationRequest OnSetStartLocationRequest;
        public event RequestGodlikePowers OnRequestGodlikePowers;
        public event GodKickUser OnGodKickUser;
        public event ObjectDuplicate OnObjectDuplicate;
        public event ObjectDuplicateOnRay OnObjectDuplicateOnRay;
        public event GrabObject OnGrabObject;
        public event DeGrabObject OnDeGrabObject;
        public event MoveObject OnGrabUpdate;
        public event SpinStart OnSpinStart;
        public event SpinObject OnSpinUpdate;
        public event SpinStop OnSpinStop;
        public event UpdateShape OnUpdatePrimShape;
        public event ObjectExtraParams OnUpdateExtraParams;
        public event ObjectRequest OnObjectRequest;
        public event ObjectSelect OnObjectSelect;
        public event ObjectDeselect OnObjectDeselect;
        public event GenericCall7 OnObjectDescription;
        public event GenericCall7 OnObjectName;
        public event GenericCall7 OnObjectClickAction;
        public event GenericCall7 OnObjectMaterial;
        public event RequestObjectPropertiesFamily OnRequestObjectPropertiesFamily;
        public event UpdatePrimFlags OnUpdatePrimFlags;
        public event UpdatePrimTexture OnUpdatePrimTexture;
        public event UpdateVector OnUpdatePrimGroupPosition;
        public event UpdateVector OnUpdatePrimSinglePosition;
        public event UpdatePrimRotation OnUpdatePrimGroupRotation;
        public event UpdatePrimSingleRotation OnUpdatePrimSingleRotation;
        public event UpdatePrimSingleRotationPosition OnUpdatePrimSingleRotationPosition;
        public event UpdatePrimGroupRotation OnUpdatePrimGroupMouseRotation;
        public event UpdateVector OnUpdatePrimScale;
        public event UpdateVector OnUpdatePrimGroupScale;
        public event StatusChange OnChildAgentStatus;
        public event GenericCall2 OnStopMovement;
        public event Action<UUID> OnRemoveAvatar;
        public event ObjectPermissions OnObjectPermissions;
        public event CreateNewInventoryItem OnCreateNewInventoryItem;
        public event LinkInventoryItem OnLinkInventoryItem;
        public event CreateInventoryFolder OnCreateNewInventoryFolder;
        public event UpdateInventoryFolder OnUpdateInventoryFolder;
        public event MoveInventoryFolder OnMoveInventoryFolder;
        public event FetchInventoryDescendents OnFetchInventoryDescendents;
        public event PurgeInventoryDescendents OnPurgeInventoryDescendents;
        public event FetchInventory OnFetchInventory;
        public event RequestTaskInventory OnRequestTaskInventory;
        public event UpdateInventoryItem OnUpdateInventoryItem;
        public event CopyInventoryItem OnCopyInventoryItem;
        public event MoveInventoryItem OnMoveInventoryItem;
        public event RemoveInventoryFolder OnRemoveInventoryFolder;
        public event RemoveInventoryItem OnRemoveInventoryItem;
        public event UDPAssetUploadRequest OnAssetUploadRequest;
        public event XferReceive OnXferReceive;
        public event RequestXfer OnRequestXfer;
        public event ConfirmXfer OnConfirmXfer;
        public event AbortXfer OnAbortXfer;
        public event RezScript OnRezScript;
        public event UpdateTaskInventory OnUpdateTaskInventory;
        public event MoveTaskInventory OnMoveTaskItem;
        public event RemoveTaskInventory OnRemoveTaskItem;
        public event RequestAsset OnRequestAsset;
        public event UUIDNameRequest OnNameFromUUIDRequest;
        public event ParcelAccessListRequest OnParcelAccessListRequest;
        public event ParcelAccessListUpdateRequest OnParcelAccessListUpdateRequest;
        public event ParcelPropertiesRequest OnParcelPropertiesRequest;
        public event ParcelDivideRequest OnParcelDivideRequest;
        public event ParcelJoinRequest OnParcelJoinRequest;
        public event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest;
        public event ParcelSelectObjects OnParcelSelectObjects;
        public event ParcelObjectOwnerRequest OnParcelObjectOwnerRequest;
        public event ParcelAbandonRequest OnParcelAbandonRequest;
        public event ParcelGodForceOwner OnParcelGodForceOwner;
        public event ParcelReclaim OnParcelReclaim;
        public event ParcelReturnObjectsRequest OnParcelReturnObjectsRequest;
        public event ParcelDeedToGroup OnParcelDeedToGroup;
        public event RegionInfoRequest OnRegionInfoRequest;
        public event EstateCovenantRequest OnEstateCovenantRequest;
        public event FriendActionDelegate OnApproveFriendRequest;
        public event FriendActionDelegate OnDenyFriendRequest;
        public event FriendshipTermination OnTerminateFriendship;
        public event MoneyTransferRequest OnMoneyTransferRequest;
        public event EconomyDataRequest OnEconomyDataRequest;
        public event MoneyBalanceRequest OnMoneyBalanceRequest;
        public event UpdateAvatarProperties OnUpdateAvatarProperties;
        public event ParcelBuy OnParcelBuy;
        public event RequestPayPrice OnRequestPayPrice;
        public event ObjectSaleInfo OnObjectSaleInfo;
        public event ObjectBuy OnObjectBuy;
        public event BuyObjectInventory OnBuyObjectInventory;
        public event RequestTerrain OnRequestTerrain;
        public event RequestTerrain OnUploadTerrain;
        public event ObjectIncludeInSearch OnObjectIncludeInSearch;
        public event UUIDNameRequest OnTeleportHomeRequest;
        public event ScriptAnswer OnScriptAnswer;
        public event AgentSit OnUndo;
        public event AgentSit OnRedo;
        public event LandUndo OnLandUndo;
        public event ForceReleaseControls OnForceReleaseControls;
        public event GodLandStatRequest OnLandStatRequest;
        public event DetailedEstateDataRequest OnDetailedEstateDataRequest;
        public event SetEstateFlagsRequest OnSetEstateFlagsRequest;
        public event SetEstateTerrainBaseTexture OnSetEstateTerrainBaseTexture;
        public event SetEstateTerrainDetailTexture OnSetEstateTerrainDetailTexture;
        public event SetEstateTerrainTextureHeights OnSetEstateTerrainTextureHeights;
        public event CommitEstateTerrainTextureRequest OnCommitEstateTerrainTextureRequest;
        public event SetRegionTerrainSettings OnSetRegionTerrainSettings;
        public event EstateRestartSimRequest OnEstateRestartSimRequest;
        public event EstateChangeCovenantRequest OnEstateChangeCovenantRequest;
        public event UpdateEstateAccessDeltaRequest OnUpdateEstateAccessDeltaRequest;
        public event SimulatorBlueBoxMessageRequest OnSimulatorBlueBoxMessageRequest;
        public event EstateBlueBoxMessageRequest OnEstateBlueBoxMessageRequest;
        public event EstateDebugRegionRequest OnEstateDebugRegionRequest;
        public event EstateTeleportOneUserHomeRequest OnEstateTeleportOneUserHomeRequest;
        public event EstateTeleportAllUsersHomeRequest OnEstateTeleportAllUsersHomeRequest;
        public event UUIDNameRequest OnUUIDGroupNameRequest;
        public event RegionHandleRequest OnRegionHandleRequest;
        public event ParcelInfoRequest OnParcelInfoRequest;
        public event RequestObjectPropertiesFamily OnObjectGroupRequest;
        public event ScriptReset OnScriptReset;
        public event GetScriptRunning OnGetScriptRunning;
        public event SetScriptRunning OnSetScriptRunning;
        public event UpdateVector OnAutoPilotGo;
        public event TerrainUnacked OnUnackedTerrain;
        public event ActivateGesture OnActivateGesture;
        public event DeactivateGesture OnDeactivateGesture;
        public event ObjectOwner OnObjectOwner;
        public event DirPlacesQuery OnDirPlacesQuery;
        public event DirFindQuery OnDirFindQuery;
        public event DirLandQuery OnDirLandQuery;
        public event DirPopularQuery OnDirPopularQuery;
        public event DirClassifiedQuery OnDirClassifiedQuery;
        public event EventInfoRequest OnEventInfoRequest;
        public event ParcelSetOtherCleanTime OnParcelSetOtherCleanTime;
        public event MapItemRequest OnMapItemRequest;
        public event OfferCallingCard OnOfferCallingCard;
        public event AcceptCallingCard OnAcceptCallingCard;
        public event DeclineCallingCard OnDeclineCallingCard;
        public event SoundTrigger OnSoundTrigger;
        public event StartLure OnStartLure;
        public event TeleportLureRequest OnTeleportLureRequest;
        public event NetworkStats OnNetworkStatsUpdate;
        public event ClassifiedInfoRequest OnClassifiedInfoRequest;
        public event ClassifiedInfoUpdate OnClassifiedInfoUpdate;
        public event ClassifiedDelete OnClassifiedDelete;
        public event ClassifiedDelete OnClassifiedGodDelete;
        public event EventNotificationAddRequest OnEventNotificationAddRequest;
        public event EventNotificationRemoveRequest OnEventNotificationRemoveRequest;
        public event EventGodDelete OnEventGodDelete;
        public event ParcelDwellRequest OnParcelDwellRequest;
        public event UserInfoRequest OnUserInfoRequest;
        public event UpdateUserInfo OnUpdateUserInfo;
        public event RetrieveInstantMessages OnRetrieveInstantMessages;
        public event PickDelete OnPickDelete;
        public event PickGodDelete OnPickGodDelete;
        public event PickInfoUpdate OnPickInfoUpdate;
        public event AvatarNotesUpdate OnAvatarNotesUpdate;
        public event AvatarInterestUpdate OnAvatarInterestUpdate;
        public event GrantUserFriendRights OnGrantUserRights;
        public event MuteListRequest OnMuteListRequest;
        public event PlacesQuery OnPlacesQuery;
        public event FindAgentUpdate OnFindAgent;
        public event TrackAgentUpdate OnTrackAgent;
        public event NewUserReport OnUserReport;
        public event SaveStateHandler OnSaveState;
        public event GroupAccountSummaryRequest OnGroupAccountSummaryRequest;
        public event GroupAccountDetailsRequest OnGroupAccountDetailsRequest;
        public event GroupAccountTransactionsRequest OnGroupAccountTransactionsRequest;
        public event FreezeUserUpdate OnParcelFreezeUser;
        public event EjectUserUpdate OnParcelEjectUser;
        public event ParcelBuyPass OnParcelBuyPass;
        public event ParcelGodMark OnParcelGodMark;
        public event GroupActiveProposalsRequest OnGroupActiveProposalsRequest;
        public event GroupVoteHistoryRequest OnGroupVoteHistoryRequest;
        public event SimWideDeletesDelegate OnSimWideDeletes;
        public event SendPostcard OnSendPostcard;
        public event MuteListEntryUpdate OnUpdateMuteListEntry;
        public event MuteListEntryRemove OnRemoveMuteListEntry;
        public event GodlikeMessage onGodlikeMessage;
        public event GodUpdateRegionInfoUpdate OnGodUpdateRegionInfoUpdate;
        public void SetDebugPacketLevel(int newDebug)
        {
            throw new System.NotImplementedException();
        }

        public void InPacket(object NewPack)
        {
            throw new System.NotImplementedException();
        }

        public void ProcessInPacket(Packet NewPack)
        {
            throw new System.NotImplementedException();
        }

        public void Close()
        {
            throw new System.NotImplementedException();
        }

        public void Kick(string message)
        {
            throw new System.NotImplementedException();
        }

        public void Start()
        {
            throw new System.NotImplementedException();
        }

        public void Stop()
        {
            throw new System.NotImplementedException();
        }

        public void SendWearables(AvatarWearable[] wearables, int serial)
        {
            throw new System.NotImplementedException();
        }

        public void SendAppearance(UUID agentID, byte[] visualParams, byte[] textureEntry)
        {
            throw new System.NotImplementedException();
        }

        public void SendStartPingCheck(byte seq)
        {
            throw new System.NotImplementedException();
        }

        public void SendKillObject(ulong regionHandle, uint localID)
        {
            throw new System.NotImplementedException();
        }

        public void SendAnimations(UUID[] animID, int[] seqs, UUID sourceAgentId, UUID[] objectIDs)
        {
            throw new System.NotImplementedException();
        }

        public void SendRegionHandshake(RegionInfo regionInfo, RegionHandshakeArgs args)
        {
            throw new System.NotImplementedException();
        }

        public void SendChatMessage(string message, byte type, Vector3 fromPos, string fromName, UUID fromAgentID, byte source, byte audible)
        {
            throw new System.NotImplementedException();
        }

        public void SendInstantMessage(GridInstantMessage im)
        {
            throw new System.NotImplementedException();
        }

        public void SendGenericMessage(string method, List<byte[]> message)
        {
            throw new System.NotImplementedException();
        }

        public void SendLayerData(float[] map)
        {
            throw new System.NotImplementedException();
        }

        public void SendLayerData(int px, int py, float[] map)
        {
            throw new System.NotImplementedException();
        }

        public void SendWindData(Vector2[] windSpeeds)
        {
            throw new System.NotImplementedException();
        }

        public void SendCloudData(float[] cloudCover)
        {
            throw new System.NotImplementedException();
        }

        public void MoveAgentIntoRegion(RegionInfo regInfo, Vector3 pos, Vector3 look)
        {
            throw new System.NotImplementedException();
        }

        public void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourExternalEndPoint)
        {
            throw new System.NotImplementedException();
        }

        public AgentCircuitData RequestClientInfo()
        {
            throw new System.NotImplementedException();
        }

        public void CrossRegion(ulong newRegionHandle, Vector3 pos, Vector3 lookAt, IPEndPoint newRegionExternalEndPoint, string capsURL)
        {
            throw new System.NotImplementedException();
        }

        public void SendMapBlock(List<MapBlockData> mapBlocks, uint flag)
        {
            throw new System.NotImplementedException();
        }

        public void SendLocalTeleport(Vector3 position, Vector3 lookAt, uint flags)
        {
            throw new System.NotImplementedException();
        }

        public void SendRegionTeleport(ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint, uint locationID, uint flags, string capsURL)
        {
            throw new System.NotImplementedException();
        }

        public void SendTeleportFailed(string reason)
        {
            throw new System.NotImplementedException();
        }

        public void SendTeleportLocationStart()
        {
            throw new System.NotImplementedException();
        }

        public void SendMoneyBalance(UUID transaction, bool success, byte[] description, int balance)
        {
            throw new System.NotImplementedException();
        }

        public void SendPayPrice(UUID objectID, int[] payPrice)
        {
            throw new System.NotImplementedException();
        }

        public void SendCoarseLocationUpdate(List<UUID> users, List<Vector3> CoarseLocations)
        {
            throw new System.NotImplementedException();
        }

        public void AttachObject(uint localID, Quaternion rotation, byte attachPoint, UUID ownerID)
        {
            throw new System.NotImplementedException();
        }

        public void SetChildAgentThrottle(byte[] throttle)
        {
            throw new System.NotImplementedException();
        }

        public void SendAvatarDataImmediate(ISceneEntity avatar)
        {
            throw new System.NotImplementedException();
        }

        public void SendPrimUpdate(ISceneEntity entity, PrimUpdateFlags updateFlags)
        {
            throw new System.NotImplementedException();
        }

        public void ReprioritizeUpdates(UpdatePriorityHandler handler)
        {
            throw new System.NotImplementedException();
        }

        public void FlushPrimUpdates()
        {
            throw new System.NotImplementedException();
        }

        public void SendInventoryFolderDetails(UUID ownerID, UUID folderID, List<InventoryItemBase> items, List<InventoryFolderBase> folders, int version, bool fetchFolders, bool fetchItems)
        {
            throw new System.NotImplementedException();
        }

        public void SendInventoryItemDetails(UUID ownerID, InventoryItemBase item)
        {
            throw new System.NotImplementedException();
        }

        public void SendInventoryItemCreateUpdate(InventoryItemBase Item, uint callbackId)
        {
            throw new System.NotImplementedException();
        }

        public void SendRemoveInventoryItem(UUID itemID)
        {
            throw new System.NotImplementedException();
        }

        public void SendTakeControls(int controls, bool passToAgent, bool TakeControls)
        {
            throw new System.NotImplementedException();
        }

        public void SendTaskInventory(UUID taskID, short serial, byte[] fileName)
        {
            throw new System.NotImplementedException();
        }

        public void SendBulkUpdateInventory(InventoryNodeBase node)
        {
            throw new System.NotImplementedException();
        }

        public void SendXferPacket(ulong xferID, uint packet, byte[] data)
        {
            throw new System.NotImplementedException();
        }

        public void SendEconomyData(float EnergyEfficiency, int ObjectCapacity, int ObjectCount, int PriceEnergyUnit, int PriceGroupCreate, int PriceObjectClaim, float PriceObjectRent, float PriceObjectScaleFactor, int PriceParcelClaim, float PriceParcelClaimFactor, int PriceParcelRent, int PricePublicObjectDecay, int PricePublicObjectDelete, int PriceRentLight, int PriceUpload, int TeleportMinPrice, float TeleportPriceExponent)
        {
            throw new System.NotImplementedException();
        }

        public void SendAvatarPickerReply(AvatarPickerReplyAgentDataArgs AgentData, List<AvatarPickerReplyDataArgs> Data)
        {
            throw new System.NotImplementedException();
        }

        public void SendAgentDataUpdate(UUID agentid, UUID activegroupid, string firstname, string lastname, ulong grouppowers, string groupname, string grouptitle)
        {
            throw new System.NotImplementedException();
        }

        public void SendPreLoadSound(UUID objectID, UUID ownerID, UUID soundID)
        {
            throw new System.NotImplementedException();
        }

        public void SendPlayAttachedSound(UUID soundID, UUID objectID, UUID ownerID, float gain, byte flags)
        {
            throw new System.NotImplementedException();
        }

        public void SendTriggeredSound(UUID soundID, UUID ownerID, UUID objectID, UUID parentID, ulong handle, Vector3 position, float gain)
        {
            throw new System.NotImplementedException();
        }

        public void SendAttachedSoundGainChange(UUID objectID, float gain)
        {
            throw new System.NotImplementedException();
        }

        public void SendNameReply(UUID profileId, string firstname, string lastname)
        {
            throw new System.NotImplementedException();
        }

        public void SendAlertMessage(string message)
        {
            throw new System.NotImplementedException();
        }

        public void SendAgentAlertMessage(string message, bool modal)
        {
            throw new System.NotImplementedException();
        }

        public void SendLoadURL(string objectname, UUID objectID, UUID ownerID, bool groupOwned, string message, string url)
        {
            throw new System.NotImplementedException();
        }

        public void SendDialog(string objectname, UUID objectID, string ownerFirstName, string ownerLastName, string msg, UUID textureID, int ch, string[] buttonlabels)
        {
            throw new System.NotImplementedException();
        }

        public bool AddMoney(int debit)
        {
            throw new System.NotImplementedException();
        }

        public void SendSunPos(Vector3 sunPos, Vector3 sunVel, ulong CurrentTime, uint SecondsPerSunCycle, uint SecondsPerYear, float OrbitalPosition)
        {
            throw new System.NotImplementedException();
        }

        public void SendViewerEffect(ViewerEffectPacket.EffectBlock[] effectBlocks)
        {
            throw new System.NotImplementedException();
        }

        public void SendViewerTime(int phase)
        {
            throw new System.NotImplementedException();
        }

        public UUID GetDefaultAnimation(string name)
        {
            throw new System.NotImplementedException();
        }

        public void SendAvatarProperties(UUID avatarID, string aboutText, string bornOn, byte[] charterMember, string flAbout, uint flags, UUID flImageID, UUID imageID, string profileURL, UUID partnerID)
        {
            throw new System.NotImplementedException();
        }

        public void SendScriptQuestion(UUID taskID, string taskName, string ownerName, UUID itemID, int question)
        {
            throw new System.NotImplementedException();
        }

        public void SendHealth(float health)
        {
            throw new System.NotImplementedException();
        }

        public void SendEstateList(UUID invoice, int code, UUID[] Data, uint estateID)
        {
            throw new System.NotImplementedException();
        }

        public void SendBannedUserList(UUID invoice, EstateBan[] banlist, uint estateID)
        {
            throw new System.NotImplementedException();
        }

        public void SendRegionInfoToEstateMenu(RegionInfoForEstateMenuArgs args)
        {
            throw new System.NotImplementedException();
        }

        public void SendEstateCovenantInformation(UUID covenant)
        {
            throw new System.NotImplementedException();
        }

        public void SendDetailedEstateData(UUID invoice, string estateName, uint estateID, uint parentEstate, uint estateFlags, uint sunPosition, UUID covenant, string abuseEmail, UUID estateOwner)
        {
            throw new System.NotImplementedException();
        }

        public void SendLandProperties(int sequence_id, bool snap_selection, int request_result, LandData landData, float simObjectBonusFactor, int parcelObjectCapacity, int simObjectCapacity, uint regionFlags)
        {
            throw new System.NotImplementedException();
        }

        public void SendLandAccessListData(List<UUID> avatars, uint accessFlag, int localLandID)
        {
            throw new System.NotImplementedException();
        }

        public void SendForceClientSelectObjects(List<uint> objectIDs)
        {
            throw new System.NotImplementedException();
        }

        public void SendCameraConstraint(Vector4 ConstraintPlane)
        {
            throw new System.NotImplementedException();
        }

        public void SendLandObjectOwners(LandData land, List<UUID> groups, Dictionary<UUID, int> ownersAndCount)
        {
            throw new System.NotImplementedException();
        }

        public void SendLandParcelOverlay(byte[] data, int sequence_id)
        {
            throw new System.NotImplementedException();
        }

        public void SendParcelMediaCommand(uint flags, ParcelMediaCommandEnum command, float time)
        {
            throw new System.NotImplementedException();
        }

        public void SendParcelMediaUpdate(string mediaUrl, UUID mediaTextureID, byte autoScale, string mediaType, string mediaDesc, int mediaWidth, int mediaHeight, byte mediaLoop)
        {
            throw new System.NotImplementedException();
        }

        public void SendAssetUploadCompleteMessage(sbyte AssetType, bool Success, UUID AssetFullID)
        {
            throw new System.NotImplementedException();
        }

        public void SendConfirmXfer(ulong xferID, uint PacketID)
        {
            throw new System.NotImplementedException();
        }

        public void SendXferRequest(ulong XferID, short AssetType, UUID vFileID, byte FilePath, byte[] FileName)
        {
            throw new System.NotImplementedException();
        }

        public void SendInitiateDownload(string simFileName, string clientFileName)
        {
            throw new System.NotImplementedException();
        }

        public void SendImageFirstPart(ushort numParts, UUID ImageUUID, uint ImageSize, byte[] ImageData, byte imageCodec)
        {
            throw new System.NotImplementedException();
        }

        public void SendImageNextPart(ushort partNumber, UUID imageUuid, byte[] imageData)
        {
            throw new System.NotImplementedException();
        }

        public void SendImageNotFound(UUID imageid)
        {
            throw new System.NotImplementedException();
        }

        public void SendShutdownConnectionNotice()
        {
            throw new System.NotImplementedException();
        }

        public void SendSimStats(SimStats stats)
        {
            throw new System.NotImplementedException();
        }

        public void SendObjectPropertiesFamilyData(uint RequestFlags, UUID ObjectUUID, UUID OwnerID, UUID GroupID, uint BaseMask, uint OwnerMask, uint GroupMask, uint EveryoneMask, uint NextOwnerMask, int OwnershipCost, byte SaleType, int SalePrice, uint Category, UUID LastOwnerID, string ObjectName, string Description)
        {
            throw new System.NotImplementedException();
        }

        public void SendObjectPropertiesReply(UUID ItemID, ulong CreationDate, UUID CreatorUUID, UUID FolderUUID, UUID FromTaskUUID, UUID GroupUUID, short InventorySerial, UUID LastOwnerUUID, UUID ObjectUUID, UUID OwnerUUID, string TouchTitle, byte[] TextureID, string SitTitle, string ItemName, string ItemDescription, uint OwnerMask, uint NextOwnerMask, uint GroupMask, uint EveryoneMask, uint BaseMask, byte saleType, int salePrice)
        {
            throw new System.NotImplementedException();
        }

        public void SendAgentOffline(UUID[] agentIDs)
        {
            throw new System.NotImplementedException();
        }

        public void SendAgentOnline(UUID[] agentIDs)
        {
            throw new System.NotImplementedException();
        }

        public void SendSitResponse(UUID TargetID, Vector3 OffsetPos, Quaternion SitOrientation, bool autopilot, Vector3 CameraAtOffset, Vector3 CameraEyeOffset, bool ForceMouseLook)
        {
            throw new System.NotImplementedException();
        }

        public void SendAdminResponse(UUID Token, uint AdminLevel)
        {
            throw new System.NotImplementedException();
        }

        public void SendGroupMembership(GroupMembershipData[] GroupMembership)
        {
            throw new System.NotImplementedException();
        }

        public void SendGroupNameReply(UUID groupLLUID, string GroupName)
        {
            throw new System.NotImplementedException();
        }

        public void SendJoinGroupReply(UUID groupID, bool success)
        {
            throw new System.NotImplementedException();
        }

        public void SendEjectGroupMemberReply(UUID agentID, UUID groupID, bool success)
        {
            throw new System.NotImplementedException();
        }

        public void SendLeaveGroupReply(UUID groupID, bool success)
        {
            throw new System.NotImplementedException();
        }

        public void SendCreateGroupReply(UUID groupID, bool success, string message)
        {
            throw new System.NotImplementedException();
        }

        public void SendLandStatReply(uint reportType, uint requestFlags, uint resultCount, LandStatReportItem[] lsrpia)
        {
            throw new System.NotImplementedException();
        }

        public void SendScriptRunningReply(UUID objectID, UUID itemID, bool running)
        {
            throw new System.NotImplementedException();
        }

        public void SendAsset(AssetRequestToClient req)
        {
            throw new System.NotImplementedException();
        }

        public void SendTexture(AssetBase TextureAsset)
        {
            throw new System.NotImplementedException();
        }

        public byte[] GetThrottlesPacked(float multiplier)
        {
            throw new System.NotImplementedException();
        }

        public event ViewerEffectEventHandler OnViewerEffect;
        public event Action<IClientAPI> OnLogout;
        public event Action<IClientAPI> OnConnectionClosed;
        public void SendBlueBoxMessage(UUID FromAvatarID, string FromAvatarName, string Message)
        {
            throw new System.NotImplementedException();
        }

        public void SendLogoutPacket()
        {
            throw new System.NotImplementedException();
        }

        public EndPoint GetClientEP()
        {
            throw new System.NotImplementedException();
        }

        public ClientInfo GetClientInfo()
        {
            throw new System.NotImplementedException();
        }

        public void SetClientInfo(ClientInfo info)
        {
            throw new System.NotImplementedException();
        }

        public void SetClientOption(string option, string value)
        {
            throw new System.NotImplementedException();
        }

        public string GetClientOption(string option)
        {
            throw new System.NotImplementedException();
        }

        public void SendSetFollowCamProperties(UUID objectID, SortedDictionary<int, float> parameters)
        {
            throw new System.NotImplementedException();
        }

        public void SendClearFollowCamProperties(UUID objectID)
        {
            throw new System.NotImplementedException();
        }

        public void SendRegionHandle(UUID regoinID, ulong handle)
        {
            throw new System.NotImplementedException();
        }

        public void SendParcelInfo(RegionInfo info, LandData land, UUID parcelID, uint x, uint y)
        {
            throw new System.NotImplementedException();
        }

        public void SendScriptTeleportRequest(string objName, string simName, Vector3 pos, Vector3 lookAt)
        {
            throw new System.NotImplementedException();
        }

        public void SendDirPlacesReply(UUID queryID, DirPlacesReplyData[] data)
        {
            throw new System.NotImplementedException();
        }

        public void SendDirPeopleReply(UUID queryID, DirPeopleReplyData[] data)
        {
            throw new System.NotImplementedException();
        }

        public void SendDirEventsReply(UUID queryID, DirEventsReplyData[] data)
        {
            throw new System.NotImplementedException();
        }

        public void SendDirGroupsReply(UUID queryID, DirGroupsReplyData[] data)
        {
            throw new System.NotImplementedException();
        }

        public void SendDirClassifiedReply(UUID queryID, DirClassifiedReplyData[] data)
        {
            throw new System.NotImplementedException();
        }

        public void SendDirLandReply(UUID queryID, DirLandReplyData[] data)
        {
            throw new System.NotImplementedException();
        }

        public void SendDirPopularReply(UUID queryID, DirPopularReplyData[] data)
        {
            throw new System.NotImplementedException();
        }

        public void SendEventInfoReply(EventData info)
        {
            throw new System.NotImplementedException();
        }

        public void SendMapItemReply(mapItemReply[] replies, uint mapitemtype, uint flags)
        {
            throw new System.NotImplementedException();
        }

        public void SendAvatarGroupsReply(UUID avatarID, GroupMembershipData[] data)
        {
            throw new System.NotImplementedException();
        }

        public void SendOfferCallingCard(UUID srcID, UUID transactionID)
        {
            throw new System.NotImplementedException();
        }

        public void SendAcceptCallingCard(UUID transactionID)
        {
            throw new System.NotImplementedException();
        }

        public void SendDeclineCallingCard(UUID transactionID)
        {
            throw new System.NotImplementedException();
        }

        public void SendTerminateFriend(UUID exFriendID)
        {
            throw new System.NotImplementedException();
        }

        public void SendAvatarClassifiedReply(UUID targetID, UUID[] classifiedID, string[] name)
        {
            throw new System.NotImplementedException();
        }

        public void SendClassifiedInfoReply(UUID classifiedID, UUID creatorID, uint creationDate, uint expirationDate, uint category, string name, string description, UUID parcelID, uint parentEstate, UUID snapshotID, string simName, Vector3 globalPos, string parcelName, byte classifiedFlags, int price)
        {
            throw new System.NotImplementedException();
        }

        public void SendAgentDropGroup(UUID groupID)
        {
            throw new System.NotImplementedException();
        }

        public void RefreshGroupMembership()
        {
            throw new System.NotImplementedException();
        }

        public void SendAvatarNotesReply(UUID targetID, string text)
        {
            throw new System.NotImplementedException();
        }

        public void SendAvatarPicksReply(UUID targetID, Dictionary<UUID, string> picks)
        {
            throw new System.NotImplementedException();
        }

        public void SendPickInfoReply(UUID pickID, UUID creatorID, bool topPick, UUID parcelID, string name, string desc, UUID snapshotID, string user, string originalName, string simName, Vector3 posGlobal, int sortOrder, bool enabled)
        {
            throw new System.NotImplementedException();
        }

        public void SendAvatarClassifiedReply(UUID targetID, Dictionary<UUID, string> classifieds)
        {
            throw new System.NotImplementedException();
        }

        public void SendParcelDwellReply(int localID, UUID parcelID, float dwell)
        {
            throw new System.NotImplementedException();
        }

        public void SendUserInfoReply(bool imViaEmail, bool visible, string email)
        {
            throw new System.NotImplementedException();
        }

        public void SendUseCachedMuteList()
        {
            throw new System.NotImplementedException();
        }

        public void SendMuteListUpdate(string filename)
        {
            throw new System.NotImplementedException();
        }

        public void KillEndDone()
        {
            throw new System.NotImplementedException();
        }

        public bool AddGenericPacketHandler(string MethodName, GenericMessage handler)
        {
            throw new System.NotImplementedException();
        }

        public void SendRebakeAvatarTextures(UUID textureID)
        {
            throw new System.NotImplementedException();
        }

        public void SendAvatarInterestsReply(UUID avatarID, uint wantMask, string wantText, uint skillsMask, string skillsText, string languages)
        {
            throw new System.NotImplementedException();
        }
        
        public void SendGroupAccountingDetails(IClientAPI sender,UUID groupID, UUID transactionID, UUID sessionID, int amt)
        {
        }
        
        public void SendGroupAccountingSummary(IClientAPI sender,UUID groupID, uint moneyAmt, int totalTier, int usedTier)
        {
        }
        
        public void SendGroupTransactionsSummaryDetails(IClientAPI sender,UUID groupID, UUID transactionID, UUID sessionID,int amt)
        {
        }

        public void SendGroupVoteHistory(UUID groupID, UUID transactionID, GroupVoteHistory[] Votes)
        {
        }

        public void SendGroupActiveProposals(UUID groupID, UUID transactionID, GroupActiveProposals[] Proposals)
        {
        }

        public void SendChangeUserRights(UUID agentID, UUID friendID, int rights)
        {
        }

        public void SendTextBoxRequest(string message, int chatChannel, string objectname, string ownerFirstName, string ownerLastName, UUID objectId)
        {
        }

        #endregion
    }
}
