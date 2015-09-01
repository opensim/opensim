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
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Framework.Client;

namespace OpenSim.Tests.Common
{
    public class TestClient : IClientAPI, IClientCore
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        EventWaitHandle wh = new EventWaitHandle (false, EventResetMode.AutoReset, "Crossing");

        private Scene m_scene;

        // Properties so that we can get at received data for test purposes
        public List<uint> ReceivedKills { get; private set; }
        public List<UUID> ReceivedOfflineNotifications { get; private set; }
        public List<UUID> ReceivedOnlineNotifications { get; private set; }
        public List<UUID> ReceivedFriendshipTerminations { get; private set; }

        public List<ImageDataPacket> SentImageDataPackets { get; private set; }
        public List<ImagePacketPacket> SentImagePacketPackets { get; private set; }
        public List<ImageNotInDatabasePacket> SentImageNotInDatabasePackets { get; private set; }

        // Test client specific events - for use by tests to implement some IClientAPI behaviour.
        public event Action<RegionInfo, Vector3, Vector3> OnReceivedMoveAgentIntoRegion;
        public event Action<ulong, IPEndPoint> OnTestClientInformClientOfNeighbour;
        public event TestClientOnSendRegionTeleportDelegate OnTestClientSendRegionTeleport;

        public event Action<ISceneEntity, PrimUpdateFlags> OnReceivedEntityUpdate;

        public event OnReceivedChatMessageDelegate OnReceivedChatMessage;
        public event Action<GridInstantMessage> OnReceivedInstantMessage;

        public event Action<UUID> OnReceivedSendRebakeAvatarTextures;

        public delegate void TestClientOnSendRegionTeleportDelegate(
            ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint,
            uint locationID, uint flags, string capsURL);

        public delegate void OnReceivedChatMessageDelegate(
            string message, byte type, Vector3 fromPos, string fromName,
            UUID fromAgentID, UUID ownerID, byte source, byte audible);


// disable warning: public events, part of the public API
#pragma warning disable 67

        public event Action<IClientAPI> OnLogout;
        public event ObjectPermissions OnObjectPermissions;

        public event MoneyTransferRequest OnMoneyTransferRequest;
        public event ParcelBuy OnParcelBuy;
        public event Action<IClientAPI> OnConnectionClosed;

        public event ImprovedInstantMessage OnInstantMessage;
        public event ChatMessage OnChatFromClient;
        public event TextureRequest OnRequestTexture;
        public event RezObject OnRezObject;
        public event ModifyTerrain OnModifyTerrain;
        public event BakeTerrain OnBakeTerrain;
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
        public event TeleportLandmarkRequest OnTeleportLandmarkRequest;
        public event TeleportCancel OnTeleportCancel;
        public event DisconnectUser OnDisconnectUser;
        public event RequestAvatarProperties OnRequestAvatarProperties;
        public event SetAlwaysRun OnSetAlwaysRun;

        public event DeRezObject OnDeRezObject;
        public event Action<IClientAPI> OnRegionHandShakeReply;
        public event GenericCall1 OnRequestWearables;
        public event Action<IClientAPI, bool> OnCompleteMovementToRegion;
        public event UpdateAgent OnPreAgentUpdate;
        public event UpdateAgent OnAgentUpdate;
        public event UpdateAgent OnAgentCameraUpdate;
        public event AgentRequestSit OnAgentRequestSit;
        public event AgentSit OnAgentSit;
        public event AvatarPickerRequest OnAvatarPickerRequest;
        public event Action<IClientAPI> OnRequestAvatarsData;
        public event AddNewPrim OnAddPrim;
        public event RequestGodlikePowers OnRequestGodlikePowers;
        public event GodKickUser OnGodKickUser;
        public event ObjectDuplicate OnObjectDuplicate;
        public event GrabObject OnGrabObject;
        public event DeGrabObject OnDeGrabObject;
        public event MoveObject OnGrabUpdate;
        public event SpinStart OnSpinStart;
        public event SpinObject OnSpinUpdate;
        public event SpinStop OnSpinStop;
        public event ViewerEffectEventHandler OnViewerEffect;

        public event FetchInventory OnAgentDataUpdateRequest;
        public event TeleportLocationRequest OnSetStartLocationRequest;

        public event UpdateShape OnUpdatePrimShape;
        public event ObjectExtraParams OnUpdateExtraParams;
        public event RequestObjectPropertiesFamily OnRequestObjectPropertiesFamily;
        public event ObjectSelect OnObjectSelect;
        public event ObjectRequest OnObjectRequest;
        public event GenericCall7 OnObjectDescription;
        public event GenericCall7 OnObjectName;
        public event GenericCall7 OnObjectClickAction;
        public event GenericCall7 OnObjectMaterial;
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

        public event CreateNewInventoryItem OnCreateNewInventoryItem;
        public event LinkInventoryItem OnLinkInventoryItem;
        public event CreateInventoryFolder OnCreateNewInventoryFolder;
        public event UpdateInventoryFolder OnUpdateInventoryFolder;
        public event MoveInventoryFolder OnMoveInventoryFolder;
        public event RemoveInventoryFolder OnRemoveInventoryFolder;
        public event RemoveInventoryItem OnRemoveInventoryItem;
        public event FetchInventoryDescendents OnFetchInventoryDescendents;
        public event PurgeInventoryDescendents OnPurgeInventoryDescendents;
        public event FetchInventory OnFetchInventory;
        public event RequestTaskInventory OnRequestTaskInventory;
        public event UpdateInventoryItem OnUpdateInventoryItem;
        public event CopyInventoryItem OnCopyInventoryItem;
        public event MoveInventoryItem OnMoveInventoryItem;
        public event UDPAssetUploadRequest OnAssetUploadRequest;
        public event RequestTerrain OnRequestTerrain;
        public event RequestTerrain OnUploadTerrain;
        public event XferReceive OnXferReceive;
        public event RequestXfer OnRequestXfer;
        public event ConfirmXfer OnConfirmXfer;
        public event AbortXfer OnAbortXfer;
        public event RezScript OnRezScript;
        public event UpdateTaskInventory OnUpdateTaskInventory;
        public event MoveTaskInventory OnMoveTaskItem;
        public event RemoveTaskInventory OnRemoveTaskItem;
        public event RequestAsset OnRequestAsset;
        public event GenericMessage OnGenericMessage;
        public event UUIDNameRequest OnNameFromUUIDRequest;
        public event UUIDNameRequest OnUUIDGroupNameRequest;

        public event ParcelPropertiesRequest OnParcelPropertiesRequest;
        public event ParcelDivideRequest OnParcelDivideRequest;
        public event ParcelJoinRequest OnParcelJoinRequest;
        public event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest;
        public event ParcelAbandonRequest OnParcelAbandonRequest;
        public event ParcelGodForceOwner OnParcelGodForceOwner;
        public event ParcelReclaim OnParcelReclaim;
        public event ParcelReturnObjectsRequest OnParcelReturnObjectsRequest;
        public event ParcelAccessListRequest OnParcelAccessListRequest;
        public event ParcelAccessListUpdateRequest OnParcelAccessListUpdateRequest;
        public event ParcelSelectObjects OnParcelSelectObjects;
        public event ParcelObjectOwnerRequest OnParcelObjectOwnerRequest;
        public event ParcelDeedToGroup OnParcelDeedToGroup;
        public event ObjectDeselect OnObjectDeselect;
        public event RegionInfoRequest OnRegionInfoRequest;
        public event EstateCovenantRequest OnEstateCovenantRequest;
        public event EstateChangeInfo OnEstateChangeInfo;
        public event EstateManageTelehub OnEstateManageTelehub;
        public event CachedTextureRequest OnCachedTextureRequest;

        public event ObjectDuplicateOnRay OnObjectDuplicateOnRay;

        public event FriendActionDelegate OnApproveFriendRequest;
        public event FriendActionDelegate OnDenyFriendRequest;
        public event FriendshipTermination OnTerminateFriendship;
        public event GrantUserFriendRights OnGrantUserRights;

        public event EconomyDataRequest OnEconomyDataRequest;
        public event MoneyBalanceRequest OnMoneyBalanceRequest;
        public event UpdateAvatarProperties OnUpdateAvatarProperties;

        public event ObjectIncludeInSearch OnObjectIncludeInSearch;
        public event UUIDNameRequest OnTeleportHomeRequest;

        public event ScriptAnswer OnScriptAnswer;
        public event RequestPayPrice OnRequestPayPrice;
        public event ObjectSaleInfo OnObjectSaleInfo;
        public event ObjectBuy OnObjectBuy;
        public event BuyObjectInventory OnBuyObjectInventory;
        public event AgentSit OnUndo;
        public event AgentSit OnRedo;
        public event LandUndo OnLandUndo;

        public event ForceReleaseControls OnForceReleaseControls;

        public event GodLandStatRequest OnLandStatRequest;
        public event RequestObjectPropertiesFamily OnObjectGroupRequest;

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
        public event ScriptReset OnScriptReset;
        public event GetScriptRunning OnGetScriptRunning;
        public event SetScriptRunning OnSetScriptRunning;
        public event Action<Vector3, bool, bool> OnAutoPilotGo;

        public event TerrainUnacked OnUnackedTerrain;

        public event RegionHandleRequest OnRegionHandleRequest;
        public event ParcelInfoRequest OnParcelInfoRequest;

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

        public event MuteListRequest OnMuteListRequest;

        public event AvatarInterestUpdate OnAvatarInterestUpdate;

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

#pragma warning restore 67

        /// <value>
        /// This agent's UUID
        /// </value>
        private UUID m_agentId;

        public ISceneAgent SceneAgent { get; set; }

        /// <value>
        /// The last caps seed url that this client was given.
        /// </value>
        public string CapsSeedUrl;

        private Vector3 startPos = new Vector3(((int)Constants.RegionSize * 0.5f), ((int)Constants.RegionSize * 0.5f), 2);

        public virtual Vector3 StartPos
        {
            get { return startPos; }
            set { }
        }

        public virtual UUID AgentId
        {
            get { return m_agentId; }
        }

        public UUID SessionId { get; set; }

        public UUID SecureSessionId { get; set; }

        public virtual string FirstName
        {
            get { return m_firstName; }
        }
        private string m_firstName;

        public virtual string LastName
        {
            get { return m_lastName; }
        }
        private string m_lastName;

        public virtual String Name
        {
            get { return FirstName + " " + LastName; }
        }

        public bool IsActive
        {
            get { return true; }
            set { }
        }

        public bool IsLoggingOut { get; set; }

        public UUID ActiveGroupId
        {
            get { return UUID.Zero; }
        }

        public string ActiveGroupName
        {
            get { return String.Empty; }
        }

        public ulong ActiveGroupPowers
        {
            get { return 0; }
        }

        public bool IsGroupMember(UUID groupID)
        {
            return false;
        }

        public ulong GetGroupPowers(UUID groupID)
        {
            return 0;
        }

        public virtual int NextAnimationSequenceNumber
        {
            get { return 1; }
        }

        public IScene Scene
        {
            get { return m_scene; }
        }

        public bool SendLogoutPacketWhenClosing
        {
            set { }
        }

        private uint m_circuitCode;

        public uint CircuitCode
        {
            get { return m_circuitCode; }
            set { m_circuitCode = value; }
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return new IPEndPoint(IPAddress.Loopback, (ushort)m_circuitCode); }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="agentData"></param>
        /// <param name="scene"></param>
        /// <param name="sceneManager"></param>
        public TestClient(AgentCircuitData agentData, Scene scene)
        {
            m_agentId = agentData.AgentID;
            m_firstName = agentData.firstname;
            m_lastName = agentData.lastname;
            m_circuitCode = agentData.circuitcode;
            m_scene = scene;
            SessionId = agentData.SessionID;
            SecureSessionId = agentData.SecureSessionID;
            CapsSeedUrl = agentData.CapsPath;

            ReceivedKills = new List<uint>();
            ReceivedOfflineNotifications = new List<UUID>();
            ReceivedOnlineNotifications = new List<UUID>();
            ReceivedFriendshipTerminations = new List<UUID>();

            SentImageDataPackets = new List<ImageDataPacket>();
            SentImagePacketPackets = new List<ImagePacketPacket>();
            SentImageNotInDatabasePackets = new List<ImageNotInDatabasePacket>();
        }

        /// <summary>
        /// Trigger chat coming from this connection.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="type"></param>
        /// <param name="message"></param>
        public bool Chat(int channel, ChatTypeEnum type, string message)
        {
            ChatMessage handlerChatFromClient = OnChatFromClient;

            if (handlerChatFromClient != null)
            {
                OSChatMessage args = new OSChatMessage();
                args.Channel = channel;
                args.From = Name;
                args.Message = message;
                args.Type = type;

                args.Scene = Scene;
                args.Sender = this;
                args.SenderUUID = AgentId;

                handlerChatFromClient(this, args);
            }

            return true;
        }

        /// <summary>
        /// Attempt a teleport to the given region.
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        public void Teleport(ulong regionHandle, Vector3 position, Vector3 lookAt)
        {
            OnTeleportLocationRequest(this, regionHandle, position, lookAt, 16);
        }

        public void CompleteMovement()
        {
            if (OnCompleteMovementToRegion != null)
                OnCompleteMovementToRegion(this, true);
        }

        /// <summary>
        /// Emulate sending an IM from the viewer to the simulator.
        /// </summary>
        /// <param name='im'></param>
        public void HandleImprovedInstantMessage(GridInstantMessage im)
        {
            ImprovedInstantMessage handlerInstantMessage = OnInstantMessage;

            if (handlerInstantMessage != null)
                handlerInstantMessage(this, im);
        }

        public virtual void ActivateGesture(UUID assetId, UUID gestureId)
        {
        }

        public virtual void SendWearables(AvatarWearable[] wearables, int serial)
        {
        }

        public virtual void SendAppearance(UUID agentID, byte[] visualParams, byte[] textureEntry)
        {
        }

        public void SendCachedTextureResponse(ISceneEntity avatar, int serial, List<CachedTextureResponseArg> cachedTextures)
        {

        }

        public virtual void Kick(string message)
        {
        }

        public virtual void SendStartPingCheck(byte seq)
        {
        }

        public virtual void SendAvatarPickerReply(AvatarPickerReplyAgentDataArgs AgentData, List<AvatarPickerReplyDataArgs> Data)
        {
        }

        public virtual void SendAgentDataUpdate(UUID agentid, UUID activegroupid, string firstname, string lastname, ulong grouppowers, string groupname, string grouptitle)
        {
        }

        public virtual void SendKillObject(List<uint> localID)
        {
            ReceivedKills.AddRange(localID);
        }

        public virtual void SetChildAgentThrottle(byte[] throttle)
        {
        }

        public byte[] GetThrottlesPacked(float multiplier)
        {
            return new byte[0];
        }

        public virtual void SendAnimations(UUID[] animations, int[] seqs, UUID sourceAgentId, UUID[] objectIDs)
        {
        }

        public virtual void SendChatMessage(
            string message, byte type, Vector3 fromPos, string fromName,
            UUID fromAgentID, UUID ownerID, byte source, byte audible)
        {
//            Console.WriteLine("mmm {0} {1} {2}", message, Name, AgentId);
            if (OnReceivedChatMessage != null)
                OnReceivedChatMessage(message, type, fromPos, fromName, fromAgentID, ownerID, source, audible);
        }

        public void SendInstantMessage(GridInstantMessage im)
        {
            if (OnReceivedInstantMessage != null)
                OnReceivedInstantMessage(im);
        }

        public void SendGenericMessage(string method, UUID invoice, List<string> message)
        {

        }

        public void SendGenericMessage(string method, UUID invoice, List<byte[]> message)
        {

        }

        public virtual void SendLayerData(float[] map)
        {
        }

        public virtual void SendLayerData(int px, int py, float[] map)
        {
        }
        public virtual void SendLayerData(int px, int py, float[] map, bool track)
        {
        }

        public virtual void SendWindData(Vector2[] windSpeeds) { }

        public virtual void SendCloudData(float[] cloudCover) { }

        public virtual void MoveAgentIntoRegion(RegionInfo regInfo, Vector3 pos, Vector3 look)
        {
            if (OnReceivedMoveAgentIntoRegion != null)
                OnReceivedMoveAgentIntoRegion(regInfo, pos, look);
        }

        public virtual AgentCircuitData RequestClientInfo()
        {
            AgentCircuitData agentData = new AgentCircuitData();
            agentData.AgentID = AgentId;
            agentData.SessionID = SessionId; 
            agentData.SecureSessionID = UUID.Zero;
            agentData.circuitcode = m_circuitCode;
            agentData.child = false;
            agentData.firstname = m_firstName;
            agentData.lastname = m_lastName;

            ICapabilitiesModule capsModule = m_scene.RequestModuleInterface<ICapabilitiesModule>();
            if (capsModule != null)
            {
                agentData.CapsPath = capsModule.GetCapsPath(m_agentId);
                agentData.ChildrenCapSeeds = new Dictionary<ulong, string>(capsModule.GetChildrenSeeds(m_agentId));
            }

            return agentData;
        }

        public virtual void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourExternalEndPoint)
        {
            if (OnTestClientInformClientOfNeighbour != null)
                OnTestClientInformClientOfNeighbour(neighbourHandle, neighbourExternalEndPoint);
        }

        public virtual void SendRegionTeleport(
            ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint,
            uint locationID, uint flags, string capsURL)
        {
            m_log.DebugFormat(
                "[TEST CLIENT]: Received SendRegionTeleport for {0} {1} on {2}", m_firstName, m_lastName, m_scene.Name);

            CapsSeedUrl = capsURL;

            if (OnTestClientSendRegionTeleport != null)
                OnTestClientSendRegionTeleport(
                    regionHandle, simAccess, regionExternalEndPoint, locationID, flags, capsURL);
        }

        public virtual void SendTeleportFailed(string reason)
        {
            m_log.DebugFormat(
                "[TEST CLIENT]: Teleport failed for {0} {1} on {2} with reason {3}", 
                m_firstName, m_lastName, m_scene.Name, reason);
        }

        public virtual void CrossRegion(ulong newRegionHandle, Vector3 pos, Vector3 lookAt,
                                        IPEndPoint newRegionExternalEndPoint, string capsURL)
        {
            // This is supposed to send a packet to the client telling it's ready to start region crossing.
            // Instead I will just signal I'm ready, mimicking the communication behavior.
            // It's ugly, but avoids needless communication setup. This is used in ScenePresenceTests.cs.
            // Arthur V.

            wh.Set();
        }

        public virtual void SendMapBlock(List<MapBlockData> mapBlocks, uint flag)
        {
        }

        public virtual void SendLocalTeleport(Vector3 position, Vector3 lookAt, uint flags)
        {
        }

        public virtual void SendTeleportStart(uint flags)
        {
        }

        public void SendTeleportProgress(uint flags, string message)
        {
        }

        public virtual void SendMoneyBalance(UUID transaction, bool success, byte[] description, int balance, int transactionType, UUID sourceID, bool sourceIsGroup, UUID destID, bool destIsGroup, int amount, string item)
        {
        }

        public virtual void SendPayPrice(UUID objectID, int[] payPrice)
        {
        }

        public virtual void SendCoarseLocationUpdate(List<UUID> users, List<Vector3> CoarseLocations)
        {
        }

        public virtual void SendDialog(string objectname, UUID objectID, UUID ownerID, string ownerFirstName, string ownerLastName, string msg, UUID textureID, int ch, string[] buttonlabels)
        {
        }

        public void SendAvatarDataImmediate(ISceneEntity avatar)
        {
        }

        public void SendEntityUpdate(ISceneEntity entity, PrimUpdateFlags updateFlags)
        {
            if (OnReceivedEntityUpdate != null)
                OnReceivedEntityUpdate(entity, updateFlags);
        }

        public void ReprioritizeUpdates()
        {
        }

        public void FlushPrimUpdates()
        {
        }

        public virtual void SendInventoryFolderDetails(UUID ownerID, UUID folderID,
                                                       List<InventoryItemBase> items,
                                                       List<InventoryFolderBase> folders,
                                                       int version, 
                                                       bool fetchFolders,
                                                       bool fetchItems)
        {
        }

        public virtual void SendInventoryItemDetails(UUID ownerID, InventoryItemBase item)
        {
        }

        public virtual void SendInventoryItemCreateUpdate(InventoryItemBase Item, uint callbackID)
        {
        }

        public virtual void SendRemoveInventoryItem(UUID itemID)
        {
        }

        public virtual void SendBulkUpdateInventory(InventoryNodeBase node)
        {
        }

        public void SendTakeControls(int controls, bool passToAgent, bool TakeControls)
        {
        }

        public virtual void SendTaskInventory(UUID taskID, short serial, byte[] fileName)
        {
        }

        public virtual void SendXferPacket(ulong xferID, uint packet, byte[] data)
        {
        }

        public virtual void SendAbortXferPacket(ulong xferID)
        {

        }

        public virtual void SendEconomyData(float EnergyEfficiency, int ObjectCapacity, int ObjectCount, int PriceEnergyUnit,
            int PriceGroupCreate, int PriceObjectClaim, float PriceObjectRent, float PriceObjectScaleFactor,
            int PriceParcelClaim, float PriceParcelClaimFactor, int PriceParcelRent, int PricePublicObjectDecay,
            int PricePublicObjectDelete, int PriceRentLight, int PriceUpload, int TeleportMinPrice, float TeleportPriceExponent)
        {
        }

        public virtual void SendNameReply(UUID profileId, string firstname, string lastname)
        {
        }

        public virtual void SendPreLoadSound(UUID objectID, UUID ownerID, UUID soundID)
        {
        }

        public virtual void SendPlayAttachedSound(UUID soundID, UUID objectID, UUID ownerID, float gain,
                                                  byte flags)
        {
        }

        public void SendTriggeredSound(UUID soundID, UUID ownerID, UUID objectID, UUID parentID, ulong handle, Vector3 position, float gain)
        {
        }

        public void SendAttachedSoundGainChange(UUID objectID, float gain)
        {

        }

        public void SendAlertMessage(string message)
        {
        }

        public void SendAgentAlertMessage(string message, bool modal)
        {
        }

        public void SendSystemAlertMessage(string message)
        {
        }

        public void SendLoadURL(string objectname, UUID objectID, UUID ownerID, bool groupOwned, string message,
                                string url)
        {
        }

        public virtual void SendRegionHandshake(RegionInfo regionInfo, RegionHandshakeArgs args)
        {
            if (OnRegionHandShakeReply != null)
            {
                OnRegionHandShakeReply(this);
            }
        }
        
        public void SendAssetUploadCompleteMessage(sbyte AssetType, bool Success, UUID AssetFullID)
        {
        }

        public void SendConfirmXfer(ulong xferID, uint PacketID)
        {
        }

        public void SendXferRequest(ulong XferID, short AssetType, UUID vFileID, byte FilePath, byte[] FileName)
        {
        }

        public void SendInitiateDownload(string simFileName, string clientFileName)
        {
        }

        public void SendImageFirstPart(ushort numParts, UUID ImageUUID, uint ImageSize, byte[] ImageData, byte imageCodec)
        {
            ImageDataPacket im = new ImageDataPacket();
            im.Header.Reliable = false;
            im.ImageID.Packets = numParts;
            im.ImageID.ID = ImageUUID;

            if (ImageSize > 0)
                im.ImageID.Size = ImageSize;

            im.ImageData.Data = ImageData;
            im.ImageID.Codec = imageCodec;
            im.Header.Zerocoded = true;
            SentImageDataPackets.Add(im);
        }

        public void SendImageNextPart(ushort partNumber, UUID imageUuid, byte[] imageData)
        {
            ImagePacketPacket im = new ImagePacketPacket();
            im.Header.Reliable = false;
            im.ImageID.Packet = partNumber;
            im.ImageID.ID = imageUuid;
            im.ImageData.Data = imageData;
            SentImagePacketPackets.Add(im);
        }

        public void SendImageNotFound(UUID imageid)
        {
            ImageNotInDatabasePacket p = new ImageNotInDatabasePacket();
            p.ImageID.ID = imageid;

            SentImageNotInDatabasePackets.Add(p);
        }

        public void SendShutdownConnectionNotice()
        {
        }

        public void SendSimStats(SimStats stats)
        {
        }

        public void SendObjectPropertiesFamilyData(ISceneEntity Entity, uint RequestFlags)
        {
        }

        public void SendObjectPropertiesReply(ISceneEntity entity)
        {
        }

        public void SendAgentOffline(UUID[] agentIDs)
        {
            ReceivedOfflineNotifications.AddRange(agentIDs);
        }

        public void SendAgentOnline(UUID[] agentIDs)
        {
            ReceivedOnlineNotifications.AddRange(agentIDs);
        }

        public void SendSitResponse(UUID TargetID, Vector3 OffsetPos, Quaternion SitOrientation, bool autopilot,
                                        Vector3 CameraAtOffset, Vector3 CameraEyeOffset, bool ForceMouseLook)
        {
        }

        public void SendAdminResponse(UUID Token, uint AdminLevel)
        {

        }

        public void SendGroupMembership(GroupMembershipData[] GroupMembership)
        {

        }

        public void SendSunPos(Vector3 sunPos, Vector3 sunVel, ulong time, uint dlen, uint ylen, float phase)
        {
        }

        public void SendViewerEffect(ViewerEffectPacket.EffectBlock[] effectBlocks)
        {
        }

        public void SendViewerTime(int phase)
        {
        }

        public void SendAvatarProperties(UUID avatarID, string aboutText, string bornOn, Byte[] charterMember,
                                         string flAbout, uint flags, UUID flImageID, UUID imageID, string profileURL,
                                         UUID partnerID)
        {
        }

        public int DebugPacketLevel { get; set; }

        public void InPacket(object NewPack)
        {
        }

        public void ProcessInPacket(Packet NewPack)
        {
        }

        /// <summary>
        /// This is a TestClient only method to do shutdown tasks that are normally carried out by LLUDPServer.RemoveClient()
        /// </summary>
        public void Logout()
        {
            // We must set this here so that the presence is removed from the PresenceService by the PresenceDetector
            IsLoggingOut = true;

            Close();
        }

        public void Close()
        {
            Close(false);
        }

        public void Close(bool force)
        {
            // Fire the callback for this connection closing
            // This is necesary to get the presence detector to notice that a client has logged out.
            if (OnConnectionClosed != null)
                OnConnectionClosed(this);

            m_scene.RemoveClient(AgentId, true);
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
        }

        public void SendBlueBoxMessage(UUID FromAvatarID, String FromAvatarName, String Message)
        {

        }
        public void SendLogoutPacket()
        {
        }

        public void Terminate()
        {
        }

        public ClientInfo GetClientInfo()
        {
            return null;
        }

        public void SetClientInfo(ClientInfo info)
        {
        }

        public void SendScriptQuestion(UUID objectID, string taskName, string ownerName, UUID itemID, int question)
        {
        }
        public void SendHealth(float health)
        {
        }

        public void SendTelehubInfo(UUID ObjectID, string ObjectName, Vector3 ObjectPos, Quaternion ObjectRot, List<Vector3> SpawnPoint)
        {
        }

        public void SendEstateList(UUID invoice, int code, UUID[] Data, uint estateID)
        {
        }

        public void SendBannedUserList(UUID invoice, EstateBan[] banlist, uint estateID)
        {
        }

        public void SendRegionInfoToEstateMenu(RegionInfoForEstateMenuArgs args)
        {
        }

        public void SendEstateCovenantInformation(UUID covenant)
        {
        }

        public void SendDetailedEstateData(UUID invoice, string estateName, uint estateID, uint parentEstate, uint estateFlags, uint sunPosition, UUID covenant, uint covenantChanged, string abuseEmail, UUID estateOwner)
        {
        }

        public void SendLandProperties(int sequence_id, bool snap_selection, int request_result, ILandObject lo, float simObjectBonusFactor, int parcelObjectCapacity, int simObjectCapacity, uint regionFlags)
        {
        }

        public void SendLandAccessListData(List<LandAccessEntry> accessList, uint accessFlag, int localLandID)
        {
        }

        public void SendForceClientSelectObjects(List<uint> objectIDs)
        {
        }

        public void SendCameraConstraint(Vector4 ConstraintPlane)
        {
        }

        public void SendLandObjectOwners(LandData land, List<UUID> groups, Dictionary<UUID, int> ownersAndCount)
        {
        }

        public void SendLandParcelOverlay(byte[] data, int sequence_id)
        {
        }

        public void SendParcelMediaCommand(uint flags, ParcelMediaCommandEnum command, float time)
        {
        }

        public void SendParcelMediaUpdate(string mediaUrl, UUID mediaTextureID, byte autoScale, string mediaType,
                                          string mediaDesc, int mediaWidth, int mediaHeight, byte mediaLoop)
        {
        }

        public void SendGroupNameReply(UUID groupLLUID, string GroupName)
        {
        }

        public void SendLandStatReply(uint reportType, uint requestFlags, uint resultCount, LandStatReportItem[] lsrpia)
        {
        }

        public void SendScriptRunningReply(UUID objectID, UUID itemID, bool running)
        {
        }

        public void SendAsset(AssetRequestToClient req)
        {
        }

        public void SendTexture(AssetBase TextureAsset)
        {

        }

        public void SendSetFollowCamProperties (UUID objectID, SortedDictionary<int, float> parameters)
        {
        }

        public void SendClearFollowCamProperties (UUID objectID)
        {
        }

        public void SendRegionHandle (UUID regoinID, ulong handle)
        {
        }

        public void SendParcelInfo (RegionInfo info, LandData land, UUID parcelID, uint x, uint y)
        {
        }

        public void SetClientOption(string option, string value)
        {
        }

        public string GetClientOption(string option)
        {
            return string.Empty;
        }

        public void SendScriptTeleportRequest(string objName, string simName, Vector3 pos, Vector3 lookAt)
        {
        }

        public void SendDirPlacesReply(UUID queryID, DirPlacesReplyData[] data)
        {
        }

        public void SendDirPeopleReply(UUID queryID, DirPeopleReplyData[] data)
        {
        }

        public void SendDirEventsReply(UUID queryID, DirEventsReplyData[] data)
        {
        }

        public void SendDirGroupsReply(UUID queryID, DirGroupsReplyData[] data)
        {
        }

        public void SendDirClassifiedReply(UUID queryID, DirClassifiedReplyData[] data)
        {
        }

        public void SendDirLandReply(UUID queryID, DirLandReplyData[] data)
        {
        }

        public void SendDirPopularReply(UUID queryID, DirPopularReplyData[] data)
        {
        }

        public void SendMapItemReply(mapItemReply[] replies, uint mapitemtype, uint flags)
        {
        }

        public void SendEventInfoReply (EventData info)
        {
        }

        public void SendOfferCallingCard (UUID destID, UUID transactionID)
        {
        }

        public void SendAcceptCallingCard (UUID transactionID)
        {
        }

        public void SendDeclineCallingCard (UUID transactionID)
        {
        }

        public void SendAvatarGroupsReply(UUID avatarID, GroupMembershipData[] data)
        {
        }

        public void SendJoinGroupReply(UUID groupID, bool success)
        {
        }

        public void SendEjectGroupMemberReply(UUID agentID, UUID groupID, bool succss)
        {
        }

        public void SendLeaveGroupReply(UUID groupID, bool success)
        {
        }

        public void SendTerminateFriend(UUID exFriendID)
        {
            ReceivedFriendshipTerminations.Add(exFriendID);
        }

        public bool AddGenericPacketHandler(string MethodName, GenericMessage handler)
        {
            //throw new NotImplementedException();
            return false;
        }

        public void SendAvatarClassifiedReply(UUID targetID, UUID[] classifiedID, string[] name)
        {
        }

        public void SendClassifiedInfoReply(UUID classifiedID, UUID creatorID, uint creationDate, uint expirationDate, uint category, string name, string description, UUID parcelID, uint parentEstate, UUID snapshotID, string simName, Vector3 globalPos, string parcelName, byte classifiedFlags, int price)
        {
        }

        public void SendAgentDropGroup(UUID groupID)
        {
        }

        public void SendAvatarNotesReply(UUID targetID, string text)
        {
        }

        public void SendAvatarPicksReply(UUID targetID, Dictionary<UUID, string> picks)
        {
        }

        public void SendAvatarClassifiedReply(UUID targetID, Dictionary<UUID, string> classifieds)
        {
        }

        public void SendParcelDwellReply(int localID, UUID parcelID, float dwell)
        {
        }

        public void SendUserInfoReply(bool imViaEmail, bool visible, string email)
        {
        }

        public void SendCreateGroupReply(UUID groupID, bool success, string message)
        {
        }

        public void RefreshGroupMembership()
        {
        }

        public void SendUseCachedMuteList()
        {
        }

        public void SendMuteListUpdate(string filename)
        {
        }
        
        public void SendPickInfoReply(UUID pickID,UUID creatorID, bool topPick, UUID parcelID, string name, string desc, UUID snapshotID, string user, string originalName, string simName, Vector3 posGlobal, int sortOrder, bool enabled)
        {
        }

        public bool TryGet<T>(out T iface)
        {
            iface = default(T);
            return false;
        }

        public T Get<T>()
        {
            return default(T);
        }

        public void Disconnect(string reason)
        {
        }

        public void Disconnect() 
        {
        }

        public void SendRebakeAvatarTextures(UUID textureID)
        {
            if (OnReceivedSendRebakeAvatarTextures != null)
                OnReceivedSendRebakeAvatarTextures(textureID);
        }
        
        public void SendAvatarInterestsReply(UUID avatarID, uint wantMask, string wantText, uint skillsMask, string skillsText, string languages)
        {
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

        public void SendTextBoxRequest(string message, int chatChannel, string objectname, UUID ownerID, string ownerFirstName, string ownerLastName, UUID objectId)
        {
        }

        public void SendAgentTerseUpdate(ISceneEntity presence)
        {
        }

        public void SendPlacesReply(UUID queryID, UUID transactionID, PlacesReplyData[] data)
        {
        }

        public void SendPartPhysicsProprieties(ISceneEntity entity)
        {
        }

    }
}
