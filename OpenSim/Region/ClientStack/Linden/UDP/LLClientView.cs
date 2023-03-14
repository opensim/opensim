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
using System.Drawing;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using log4net;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;

using OpenSim.Framework;
using OpenSim.Framework.Client;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;

using AssetLandmark = OpenSim.Framework.AssetLandmark;
using Caps = OpenSim.Framework.Capabilities.Caps;
using PermissionMask = OpenSim.Framework.PermissionMask;
using RegionFlags = OpenMetaverse.RegionFlags;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public delegate void PacketMethod(Packet packet);

    /// <summary>
    /// Handles new client connections
    /// Constructor takes a single Packet and authenticates everything
    /// </summary>
    public class LLClientView : IClientAPI, IClientCore, IClientIM, IClientChat, IClientInventory, IStatsCollector, IClientIPEndpoint
    {
        /// <value>
        /// Debug packet level.  See OpenSim.RegisterConsoleCommands() for more details.
        /// </value>
        public int DebugPacketLevel { get; set; }

        #region Events

        public event BinaryGenericMessage OnBinaryGenericMessage;
        public event Action<IClientAPI> OnLogout;
        public event ObjectPermissions OnObjectPermissions;
        public event Action<IClientAPI> OnConnectionClosed;
        public event ViewerEffectEventHandler OnViewerEffect;
        public event ImprovedInstantMessage OnInstantMessage;
        public event ChatMessage OnChatFromClient;
        public event RezObject OnRezObject;
        public event DeRezObject OnDeRezObject;
        public event RezRestoreToWorld OnRezRestoreToWorld;
        public event ModifyTerrain OnModifyTerrain;
        public event Action<IClientAPI> OnRegionHandShakeReply;
        public event GenericCall1 OnRequestWearables;
        public event SetAppearance OnSetAppearance;
        public event AvatarNowWearing OnAvatarNowWearing;
        public event RezSingleAttachmentFromInv OnRezSingleAttachmentFromInv;
        public event RezMultipleAttachmentsFromInv OnRezMultipleAttachmentsFromInv;
        public event UUIDNameRequest OnDetachAttachmentIntoInv;
        public event ObjectAttach OnObjectAttach;
        public event ObjectDeselect OnObjectDetach;
        public event ObjectDrop OnObjectDrop;
        public event Action<IClientAPI, bool> OnCompleteMovementToRegion;
        public event UpdateAgent OnPreAgentUpdate;
        public event UpdateAgent OnAgentUpdate;
        public event UpdateAgent OnAgentCameraUpdate;
        public event AgentRequestSit OnAgentRequestSit;
        public event AgentSit OnAgentSit;
        public event AvatarPickerRequest OnAvatarPickerRequest;
        public event ChangeAnim OnChangeAnim;
        public event Action<IClientAPI> OnRequestAvatarsData;
        public event LinkObjects OnLinkObjects;
        public event DelinkObjects OnDelinkObjects;
        public event GrabObject OnGrabObject;
        public event DeGrabObject OnDeGrabObject;
        public event SpinStart OnSpinStart;
        public event SpinStop OnSpinStop;
        public event ObjectDuplicate OnObjectDuplicate;
        public event ObjectDuplicateOnRay OnObjectDuplicateOnRay;
        public event MoveObject OnGrabUpdate;
        public event SpinObject OnSpinUpdate;
        public event AddNewPrim OnAddPrim;
        public event RequestGodlikePowers OnRequestGodlikePowers;
        public event GodKickUser OnGodKickUser;
        public event ObjectExtraParams OnUpdateExtraParams;
        public event UpdateShape OnUpdatePrimShape;
        public event ObjectRequest OnObjectRequest;
        public event ObjectSelect OnObjectSelect;
        public event ObjectDeselect OnObjectDeselect;
        public event GenericCall7 OnObjectDescription;
        public event GenericCall7 OnObjectName;
        public event GenericCall7 OnObjectClickAction;
        public event GenericCall7 OnObjectMaterial;
        public event ObjectIncludeInSearch OnObjectIncludeInSearch;
        public event RequestObjectPropertiesFamily OnRequestObjectPropertiesFamily;
        public event UpdatePrimFlags OnUpdatePrimFlags;
        public event UpdatePrimTexture OnUpdatePrimTexture;
        public event ClientChangeObject onClientChangeObject;
        public event UpdateVector OnUpdatePrimGroupPosition;
        public event UpdatePrimRotation OnUpdatePrimGroupRotation;
        public event UpdateVector OnUpdatePrimGroupScale;
        public event RequestMapBlocks OnRequestMapBlocks;
        public event RequestMapName OnMapNameRequest;
        public event TeleportLocationRequest OnTeleportLocationRequest;
        public event TeleportLandmarkRequest OnTeleportLandmarkRequest;
        public event TeleportCancel OnTeleportCancel;
        public event RequestAvatarProperties OnRequestAvatarProperties;
        public event SetAlwaysRun OnSetAlwaysRun;
        public event AgentDataUpdate OnAgentDataUpdateRequest;
        public event TeleportLocationRequest OnSetStartLocationRequest;
        public event UpdateAvatarProperties OnUpdateAvatarProperties;
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
        public event MoveItemsAndLeaveCopy OnMoveItemsAndLeaveCopy;
        public event MoveInventoryItem OnMoveInventoryItem;
        public event RemoveInventoryItem OnRemoveInventoryItem;
        public event RemoveInventoryFolder OnRemoveInventoryFolder;
        public event UDPAssetUploadRequest OnAssetUploadRequest;
        public event XferReceive OnXferReceive;
        public event RequestXfer OnRequestXfer;
        public event ConfirmXfer OnConfirmXfer;
        public event AbortXfer OnAbortXfer;
        public event RequestTerrain OnRequestTerrain;
        public event RezScript OnRezScript;
        public event UpdateTaskInventory OnUpdateTaskInventory;
        public event MoveTaskInventory OnMoveTaskItem;
        public event RemoveTaskInventory OnRemoveTaskItem;
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
        public event GrantUserFriendRights OnGrantUserRights;
        public event MoneyTransferRequest OnMoneyTransferRequest;
        public event EconomyDataRequest OnEconomyDataRequest;
        public event MoneyBalanceRequest OnMoneyBalanceRequest;
        public event ParcelBuy OnParcelBuy;
        public event UUIDNameRequest OnTeleportHomeRequest;
        public event UUIDNameRequest OnUUIDGroupNameRequest;
        public event ScriptAnswer OnScriptAnswer;
        public event RequestPayPrice OnRequestPayPrice;
        public event ObjectSaleInfo OnObjectSaleInfo;
        public event ObjectBuy OnObjectBuy;
        public event AgentSit OnUndo;
        public event AgentSit OnRedo;
        public event LandUndo OnLandUndo;
        public event ForceReleaseControls OnForceReleaseControls;
        public event GodLandStatRequest OnLandStatRequest;
        public event RequestObjectPropertiesFamily OnObjectGroupRequest;
        public event DetailedEstateDataRequest OnDetailedEstateDataRequest;
        public event SetEstateFlagsRequest OnSetEstateFlagsRequest;
        public event SetEstateTerrainDetailTexture OnSetEstateTerrainDetailTexture;
        public event SetEstateTerrainTextureHeights OnSetEstateTerrainTextureHeights;
        public event CommitEstateTerrainTextureRequest OnCommitEstateTerrainTextureRequest;
        public event SetRegionTerrainSettings OnSetRegionTerrainSettings;
        public event BakeTerrain OnBakeTerrain;
        public event RequestTerrain OnUploadTerrain;
        public event EstateChangeInfo OnEstateChangeInfo;
        public event EstateManageTelehub OnEstateManageTelehub;
        public event EstateRestartSimRequest OnEstateRestartSimRequest;
        public event EstateChangeCovenantRequest OnEstateChangeCovenantRequest;
        public event UpdateEstateAccessDeltaRequest OnUpdateEstateAccessDeltaRequest;
        public event SimulatorBlueBoxMessageRequest OnSimulatorBlueBoxMessageRequest;
        public event EstateBlueBoxMessageRequest OnEstateBlueBoxMessageRequest;
        public event EstateDebugRegionRequest OnEstateDebugRegionRequest;
        public event EstateTeleportOneUserHomeRequest OnEstateTeleportOneUserHomeRequest;
        public event EstateTeleportAllUsersHomeRequest OnEstateTeleportAllUsersHomeRequest;
        public event RegionHandleRequest OnRegionHandleRequest;
        public event ParcelInfoRequest OnParcelInfoRequest;
        public event ScriptReset OnScriptReset;
        public event GetScriptRunning OnGetScriptRunning;
        public event SetScriptRunning OnSetScriptRunning;
        public event Action<Vector3, bool, bool> OnAutoPilotGo;
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
        public event ClassifiedGodDelete OnClassifiedGodDelete;
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
        public event AgentFOV OnAgentFOV;
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
        public event ChangeInventoryItemFlags OnChangeInventoryItemFlags;
        public event MuteListEntryUpdate OnUpdateMuteListEntry;
        public event MuteListEntryRemove OnRemoveMuteListEntry;
        public event GodlikeMessage onGodlikeMessage;
        public event GodUpdateRegionInfoUpdate OnGodUpdateRegionInfoUpdate;
        public event GenericCall2 OnUpdateThrottles;

#pragma warning disable 0067
        // still unused
        public event GenericMessage OnGenericMessage;
        public event TextureRequest OnRequestTexture;
        public event StatusChange OnChildAgentStatus;
        public event GenericCall2 OnStopMovement;
        public event Action<UUID> OnRemoveAvatar;
        public event DisconnectUser OnDisconnectUser;
        public event RequestAsset OnRequestAsset;
        public event BuyObjectInventory OnBuyObjectInventory;
        public event SetEstateTerrainBaseTexture OnSetEstateTerrainBaseTexture;
        public event TerrainUnacked OnUnackedTerrain;
        public event CachedTextureRequest OnCachedTextureRequest;

        public event UpdateVector OnUpdatePrimSinglePosition;
        public event StartAnim OnStartAnim;
        public event StopAnim OnStopAnim;
        public event UpdatePrimSingleRotation OnUpdatePrimSingleRotation;
        public event UpdatePrimSingleRotationPosition OnUpdatePrimSingleRotationPosition;
        public event UpdatePrimGroupRotation OnUpdatePrimGroupMouseRotation;
        public event UpdateVector OnUpdatePrimScale;

#pragma warning restore 0067

        #endregion Events

        #region Class Members

        // LLClientView Only
        public delegate void BinaryGenericMessage(Object sender, string method, byte[][] args);

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[LLCLIENTVIEW]";

        /// <summary>
        /// Handles UDP texture download.
        /// </summary>
        public LLImageManager ImageManager { get; private set; }

        public JobEngine m_asyncPacketProcess;
        private readonly LLUDPServer m_udpServer;
        private readonly LLUDPClient m_udpClient;
        private readonly UUID m_sessionId;
        private readonly UUID m_secureSessionId;
        protected readonly UUID m_agentId;
        protected readonly UUID m_scopeId;
        private readonly uint m_circuitCode;
        private readonly byte[] m_regionChannelVersion = Utils.EmptyBytes;
        private readonly IGroupsModule m_GroupsModule;

        //private int m_cachedTextureSerial;
        private readonly PriorityQueue m_entityUpdates;
        private readonly PriorityQueue m_entityProps;
        private readonly Prioritizer m_prioritizer;
        private bool m_disableFacelights;

        // needs optimization
        private HashSet<SceneObjectGroup> GroupsInView = new();
#pragma warning disable 0414
        private bool m_VelocityInterpolate;
#pragma warning restore 0414
        private const uint MaxTransferBytesPerPacket = 600;

        private bool m_SupportObjectAnimations;

        /// <value>
        /// Maintain a record of all the objects killed.  This allows us to stop an update being sent from the
        /// thread servicing the m_primFullUpdates queue after a kill.  If this happens the object persists as an
        /// ownerless phantom.
        ///
        /// All manipulation of this set has to occur under an m_entityUpdates.SyncRoot lock
        ///
        /// </value>
        protected List<uint> m_killRecord;

        //protected HashSet<uint> m_attachmentsSent;

        private bool m_SendLogoutPacketWhenClosing = true;

        /// <summary>
        /// We retain a single AgentUpdateArgs so that we can constantly reuse it rather than construct a new one for
        /// every single incoming AgentUpdate.  Every client sends 10 AgentUpdate UDP messages per second, even if it
        /// is doing absolutely nothing.
        /// </summary>
        /// <remarks>
        /// This does mean that agent updates must be processed synchronously, at least for each client, and called methods
        /// cannot retain a reference to it outside of that method.
        /// </remarks>
        private readonly AgentUpdateArgs m_thisAgentUpdateArgs = new();

        protected Dictionary<PacketType, PacketProcessor> m_packetHandlers = new();
        protected Dictionary<string, GenericMessage> m_genericPacketHandlers = new(); //PauPaw:Local Generic Message handlers
        protected Scene m_scene;
        protected string m_firstName;
        protected string m_lastName;
        protected Vector3 m_startpos;
        protected UUID m_activeGroupID;
        protected string m_activeGroupName = String.Empty;
        protected ulong m_activeGroupPowers;
        protected Dictionary<UUID, ulong> m_groupPowers = new();
        protected int m_terrainCheckerCount;
        protected uint m_agentFOVCounter;

        protected IAssetService m_assetService;

        protected bool m_supportViewerCache = false;
        #endregion Class Members

        #region Properties

        public LLUDPClient UDPClient { get { return m_udpClient; } }
        public LLUDPServer UDPServer { get { return m_udpServer; } }
        public IPEndPoint RemoteEndPoint { get { return m_udpClient.RemoteEndPoint; } }
        public UUID SecureSessionId { get { return m_secureSessionId; } }
        public IScene Scene { get { return m_scene; } }
        public UUID SessionId { get { return m_sessionId; } }
        public Vector3 StartPos
        {
            get { return m_startpos; }
            set { m_startpos = value; }
        }
        public float StartFar { get; set; }

        public UUID AgentId { get { return m_agentId; } }
        public UUID ScopeId { get { return m_scopeId; } }
        public ISceneAgent SceneAgent { get; set; }
        public UUID ActiveGroupId { get { return m_activeGroupID; } set { m_activeGroupID = value; } }
        public string ActiveGroupName { get { return m_activeGroupName; } set { m_activeGroupName = value; } }
        public ulong ActiveGroupPowers { get { return m_activeGroupPowers; } set { m_activeGroupPowers = value; } }
        public bool IsGroupMember(UUID groupID) { return m_groupPowers.ContainsKey(groupID); }

        public int PingTimeMS
        {
            get
            {
                if (UDPClient != null)
                    return UDPClient.PingTimeMS;
                return 0;
            }
        }

        /// <summary>
        /// Entity update queues
        /// </summary>
        public PriorityQueue EntityUpdateQueue { get { return m_entityUpdates; } }

        /// <summary>
        /// First name of the agent/avatar represented by the client
        /// </summary>
        public string FirstName { get { return m_firstName; } }

        /// <summary>
        /// Last name of the agent/avatar represented by the client
        /// </summary>
        public string LastName { get { return m_lastName; } }

        /// <summary>
        /// Full name of the client (first name and last name)
        /// </summary>
        public string Name { get { return FirstName + " " + LastName; } }

        public uint CircuitCode { get { return m_circuitCode; } }

        protected int m_animationSequenceNumber = (int)(Util.GetTimeStampTicks() & 0x5fffafL);
        public int NextAnimationSequenceNumber
        {
            get
            {
                int ret = Interlocked.Increment(ref m_animationSequenceNumber);
                if (ret <= 0)
                {
                    m_animationSequenceNumber = (int)(Util.GetTimeStampTicks() & 0xafff5fL);
                    ret = Interlocked.Increment(ref m_animationSequenceNumber);
                }
                return ret;
            }
            set
            {
                m_animationSequenceNumber = value;
            }
        }

        /// <summary>
        /// As well as it's function in IClientAPI, in LLClientView we are locking on this property in order to
        /// prevent race conditions by different threads calling Close().
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Used to synchronise threads when client is being closed.
        /// </summary>
        public object CloseSyncLock { get;} = new object();

        public bool IsLoggingOut { get; set; }

        public bool DisableFacelights
        {
            get { return m_disableFacelights; }
            set { m_disableFacelights = value; }
        }

        public List<uint> SelectedObjects {get; private set;}

        public bool SendLogoutPacketWhenClosing { set { m_SendLogoutPacketWhenClosing = value; } }


        #endregion Properties

        //~LLClientView()
        //{
        //    m_log.DebugFormat("{0} Destructor called for {1}, circuit code {2}", LogHeader, Name, CircuitCode);
        //}

        /// <summary>
        /// Constructor
        /// </summary>
        public LLClientView(Scene scene, LLUDPServer udpServer, LLUDPClient udpClient, AuthenticateResponse sessionInfo,
            UUID agentId, UUID sessionId, uint circuitCode)
        {
            //DebugPacketLevel = 1;

            SelectedObjects = new List<uint>();

            RegisterInterface<IClientIM>(this);
            RegisterInterface<IClientInventory>(this);
            RegisterInterface<IClientChat>(this);

            m_scene = scene;
            int pcap = 512;
            if(pcap > m_scene.Entities.Count)
                pcap = m_scene.Entities.Count;
            m_entityUpdates = new PriorityQueue(pcap);
            m_entityProps = new PriorityQueue(pcap);
            m_killRecord = new List<uint>();
            //m_attachmentsSent = new HashSet<uint>();

            m_assetService = m_scene.RequestModuleInterface<IAssetService>();
            m_GroupsModule = scene.RequestModuleInterface<IGroupsModule>();
            ImageManager = new LLImageManager(this, m_assetService, Scene.RequestModuleInterface<IJ2KDecoder>());
            m_regionChannelVersion = Util.StringToBytes1024(scene.GetSimulatorVersion());
            m_agentId = agentId;
            m_sessionId = sessionId;
            m_secureSessionId = sessionInfo.LoginInfo.SecureSession;
            m_circuitCode = circuitCode;
            m_firstName = sessionInfo.LoginInfo.First;
            m_lastName = sessionInfo.LoginInfo.Last;
            m_startpos = sessionInfo.LoginInfo.StartPos;
            StartFar = sessionInfo.LoginInfo.StartFar;

            m_udpServer = udpServer;
            m_udpClient = udpClient;
            m_udpClient.OnQueueEmpty += HandleQueueEmpty;
            m_udpClient.HasUpdates += HandleHasUpdates;
            m_udpClient.OnPacketStats += PopulateStats;

            m_prioritizer = new Prioritizer(m_scene);

            // Pick up agent scope which, for gods, can be different from the region scope
            IUserAccountService userAccountService = m_scene.RequestModuleInterface<IUserAccountService>();
            var myself = userAccountService.GetUserAccount(m_scene.RegionInfo.ScopeID, m_agentId);

            RegisterLocalPacketHandlers();
            string name = string.Format("AsyncInUDP-{0}",m_agentId.ToString());
            m_asyncPacketProcess = new JobEngine(name, name, 5000);
            IsActive = true;

            m_supportViewerCache = m_udpServer.SupportViewerObjectsCache;
        }

        #region Client Methods


        /// <summary>
        /// Close down the client view
        /// </summary>
        public void Close()
        {
            Close(true, false);
        }

        public void Close(bool sendStop, bool force)
        {
            // We lock here to prevent race conditions between two threads calling close simultaneously (e.g.
            // a simultaneous relog just as a client is being closed out due to no packet ack from the old connection.
            lock (CloseSyncLock)
            {
                // We still perform a force close inside the sync lock since this is intended to attempt close where
                // there is some unidentified connection problem, not where we have issues due to deadlock
                if (!IsActive && !force)
                {
                    m_log.DebugFormat( "{0} Not attempting to close inactive client {1} in {2} since force flag is not set",
                        LogHeader, Name, m_scene.Name);

                    return;
                }

                IsActive = false;
                CloseWithoutChecks(sendStop);
            }
        }

        /// <summary>
        /// Closes down the client view without first checking whether it is active.
        /// </summary>
        /// <remarks>
        /// This exists because LLUDPServer has to set IsActive = false in earlier synchronous code before calling
        /// CloseWithoutIsActiveCheck asynchronously.
        ///
        /// Callers must lock ClosingSyncLock before calling.
        /// </remarks>
        public void CloseWithoutChecks(bool sendStop)
        {
            m_log.DebugFormat(
                "[CLIENT]: Close has been called for {0} attached to scene {1}",
                Name, m_scene.RegionInfo.RegionName);

            if (sendStop)
            {
                // Send the STOP packet
                DisableSimulatorPacket disable = (DisableSimulatorPacket)PacketPool.Instance.GetPacket(PacketType.DisableSimulator);
                OutPacket(disable, ThrottleOutPacketType.Unknown);
            }

            // Fire the callback for this connection closing
            OnConnectionClosed?.Invoke(this);

            m_asyncPacketProcess.Stop();

            // Flush all of the packets out of the UDP server for this client
            m_udpServer?.Flush(m_udpClient);

            // Remove ourselves from the scene
            m_scene.RemoveClient(m_agentId, true);
            SceneAgent = null;

            // We can't reach into other scenes and close the connection
            // We need to do this over grid communications
            //m_scene.CloseAllAgents(CircuitCode);

            // Disable UDP handling for this client
            m_udpClient.OnQueueEmpty -= HandleQueueEmpty;
            m_udpClient.HasUpdates -= HandleHasUpdates;
            m_udpClient.OnPacketStats -= PopulateStats;
            m_udpClient.Shutdown();

            // Shutdown the image manager
            ImageManager.Close();
            ImageManager = null;

            m_entityUpdates.Close();
            m_entityProps.Close();
            m_killRecord.Clear();
            GroupsInView.Clear();

            if(m_scene.GetNumberOfClients() == 0)
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.Default;
            }
        }

        public void Kick(string message)
        {
            if (!SceneAgent.IsChildAgent)
            {
                KickUserPacket kupack = (KickUserPacket)PacketPool.Instance.GetPacket(PacketType.KickUser);
                kupack.UserInfo.AgentID = m_agentId;
                kupack.UserInfo.SessionID = SessionId;
                kupack.TargetBlock.TargetIP = 0;
                kupack.TargetBlock.TargetPort = 0;
                kupack.UserInfo.Reason = Util.StringToBytes256(message);
                OutPacket(kupack, ThrottleOutPacketType.Task);
                // You must sleep here or users get no message!
                Thread.Sleep(500);
            }
        }

        public void Stop()
        {

        }

        #endregion Client Methods

        #region Packet Handling

        public void PopulateStats(int inPackets, int outPackets, int unAckedBytes)
        {
            OnNetworkStatsUpdate?.Invoke(inPackets, outPackets, unAckedBytes);
        }

        /// <summary>
        /// Add a handler for the given packet type.
        /// </summary>
        /// <remarks>
        /// The packet is handled on its own thread.  If packets must be handled in the order in which they
        /// are received then please use the synchronous version of this method.
        /// </remarks>
        /// <param name="packetType"></param>
        /// <param name="handler"></param>
        /// <returns>true if the handler was added.  This is currently always the case.</returns>
        public bool AddLocalPacketHandler(PacketType packetType, PacketMethod handler)
        {
            return AddLocalPacketHandler(packetType, handler, true);
        }

        /// <summary>
        /// Add a handler for the given packet type.
        /// </summary>
        /// <param name="packetType"></param>
        /// <param name="handler"></param>
        /// <param name="doAsync">
        /// If true, when the packet is received it is handled on its own thread rather than on the main inward bound
        /// packet handler thread.  This vastly increases respnosiveness but some packets need to be handled
        /// synchronously.
        /// </param>
        /// <returns>true if the handler was added.  This is currently always the case.</returns>
        public bool AddLocalPacketHandler(PacketType packetType, PacketMethod handler, bool doAsync)
        {
            lock (m_packetHandlers)
                return m_packetHandlers.TryAdd(packetType, new PacketProcessor() { method = handler, Async = doAsync});
        }

        public bool AddGenericPacketHandler(string MethodName, GenericMessage handler)
        {
            MethodName = MethodName.ToLower().Trim();
            lock (m_genericPacketHandlers)
                return m_genericPacketHandlers.TryAdd(MethodName, handler);
        }

        /// <summary>
        /// Try to process a packet using registered packet handlers
        /// </summary>
        /// <param name="packet"></param>
        /// <returns>True if a handler was found which successfully processed the packet.</returns>
        protected bool ProcessPacketMethod(Packet packet)
        {
            if(m_packetHandlers.TryGetValue(packet.Type, out PacketProcessor pprocessor))
            {
                if (pprocessor.Async)
                {
                    Packet lp = packet;
                    _ = m_asyncPacketProcess.QueueJob(packet.Type.ToString(), () =>
                    {
                        try
                        {
                            pprocessor.method(lp);
                        }
                        catch (Exception e)
                        {
                            // Make sure that we see any exception caused by the asynchronous operation.
                            m_log.Error(
                                    $"[LLCLIENTVIEW]: Caught exception while processing {packet.Type} for {Name}: {e.Message}");
                        }
                    });
                }
                else
                {
                    pprocessor.method(packet);
                }
                return true;
            }
            return false;
        }

        #endregion Packet Handling

        # region Setup

        public virtual void Start()
        {
            m_asyncPacketProcess.Start();
            m_scene.AddNewAgent(this, PresenceType.User);

            //RefreshGroupMembership();
        }

        # endregion

        public void ActivateGesture(UUID assetId, UUID gestureId)
        {
        }

        public void DeactivateGesture(UUID assetId, UUID gestureId)
        {
        }

        // Sound
        public void SoundTrigger(UUID soundId, UUID owerid, UUID Objectid, UUID ParentId, float Gain, Vector3 Position, UInt64 Handle)
        {
        }

        #region Scene/Avatar to Client

        // temporary here ( from estatemanagermodule)
        private uint GetRegionFlags()
        {
            RegionFlags flags = RegionFlags.None;

            if (Scene.RegionInfo.RegionSettings.AllowDamage)
                flags |= RegionFlags.AllowDamage;
            if (Scene.RegionInfo.EstateSettings.AllowLandmark)
                flags |= RegionFlags.AllowLandmark;
            if (Scene.RegionInfo.EstateSettings.AllowSetHome)
                flags |= RegionFlags.AllowSetHome;
            if (Scene.RegionInfo.EstateSettings.ResetHomeOnTeleport)
                flags |= RegionFlags.ResetHomeOnTeleport;
            if (Scene.RegionInfo.RegionSettings.FixedSun)
                flags |= RegionFlags.SunFixed;

            // allow access override (was taxfree)
            if (!Scene.RegionInfo.EstateSettings.TaxFree) // this is now wrong means !ALLOW_ACCESS_OVERRIDE
                flags |= RegionFlags.AllowParcelAccessOverride;

            if (Scene.RegionInfo.RegionSettings.BlockTerraform)
                flags |= RegionFlags.BlockTerraform;
            if (!Scene.RegionInfo.RegionSettings.AllowLandResell)
                flags |= RegionFlags.BlockLandResell;
            if (Scene.RegionInfo.RegionSettings.Sandbox)
                flags |= RegionFlags.Sandbox;
            // nulllayer not used
            if (Scene.RegionInfo.RegionSettings.Casino)
                flags |= RegionFlags.SkipAgentAction; // redefined
            if (Scene.RegionInfo.RegionSettings.GodBlockSearch)
                flags |= RegionFlags.SkipUpdateInterestList; // redefined
            if (Scene.RegionInfo.RegionSettings.DisableCollisions)
                flags |= RegionFlags.SkipCollisions;
            if (Scene.RegionInfo.RegionSettings.DisableScripts)
                flags |= RegionFlags.SkipScripts;
            if (Scene.RegionInfo.RegionSettings.DisablePhysics)
                flags |= RegionFlags.SkipPhysics;
            if (Scene.RegionInfo.EstateSettings.PublicAccess)
                flags |= RegionFlags.ExternallyVisible; // ???? need revision
            //MainlandVisible -> allow return enc object
            //PublicAllowed -> allow return enc estate object
            if (Scene.RegionInfo.EstateSettings.BlockDwell)
                flags |= RegionFlags.BlockDwell;
            if (Scene.RegionInfo.RegionSettings.BlockFly)
                flags |= RegionFlags.NoFly;
            if (Scene.RegionInfo.EstateSettings.AllowDirectTeleport)
                flags |= RegionFlags.AllowDirectTeleport;
            if (Scene.RegionInfo.EstateSettings.EstateSkipScripts)
                flags |= RegionFlags.EstateSkipScripts;
            if (Scene.RegionInfo.RegionSettings.RestrictPushing)
                flags |= RegionFlags.RestrictPushObject;
            if (Scene.RegionInfo.EstateSettings.DenyAnonymous)
                flags |= RegionFlags.DenyAnonymous;
            //DenyIdentified  unused
            //DenyTransacted  unused
            if (Scene.RegionInfo.RegionSettings.AllowLandJoinDivide)
                flags |= RegionFlags.AllowParcelChanges;
            //AbuseEmailToEstateOwner -> block flyover
            if (Scene.RegionInfo.EstateSettings.AllowVoice)
                flags |= RegionFlags.AllowVoice;
            if (Scene.RegionInfo.RegionSettings.BlockShowInSearch)
                flags |= RegionFlags.BlockParcelSearch;
            if (Scene.RegionInfo.EstateSettings.DenyMinors)
                flags |= RegionFlags.DenyAgeUnverified;
            
            if(Scene.RegionInfo.EstateSettings.AllowEnvironmentOverride)
                flags |= RegionFlags.AllowEnvironmentOverride;
            return (uint)flags;
        }

        // Region handshake may need a more detailed look
        static private readonly byte[] RegionHandshakeHeader = new byte[] {
                Helpers.MSG_RELIABLE | Helpers.MSG_ZEROCODED,
                0, 0, 0, 0, // sequence number
                0, // extra
                //0xff, 0xff, 0, 148 // ID 148 (low frequency bigendian)
                0xff, 0xff, 0, 1, 148 // ID 148 (low frequency bigendian) zero encoded
                };


        public void SendRegionHandshake()
        {
            RegionInfo regionInfo = m_scene.RegionInfo;
            RegionSettings regionSettings = regionInfo.RegionSettings;
            EstateSettings es = regionInfo.EstateSettings;

            bool isEstateManager = m_scene.Permissions.IsEstateManager(m_agentId); // go by oficial path
            uint regionFlags = GetRegionFlags();

            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            Buffer.BlockCopy(RegionHandshakeHeader, 0, buf.Data, 0, 11);

            // inline zeroencode
            LLUDPZeroEncoder zc = new(buf.Data);
            zc.Position = 11;

            //RegionInfo Block
            //RegionFlags U32
            zc.AddUInt(regionFlags);
            //SimAccess U8
            zc.AddByte(regionInfo.AccessLevel);
            //SimName
            zc.AddShortString(regionInfo.RegionName, 255);
            //SimOwner
            zc.AddUUID(es.EstateOwner);
            //IsEstateManager
            zc.AddByte((byte)(isEstateManager ? 1 : 0));
            //WaterHeight
            zc.AddFloat((float)regionSettings.WaterHeight); // why is this a double ??
            //BillableFactor
            zc.AddFloat(es.BillableFactor);
            //CacheID
            zc.AddUUID(regionSettings.CacheID);
            //TerrainBase0
            //TerrainBase1
            //TerrainBase2
            //TerrainBase3
            // this seem not obsolete, sending zero uuids
            // we should send the basic low resolution default ?
            zc.AddZeros(16 * 4);
            //TerrainDetail0
            zc.AddUUID(regionSettings.TerrainTexture1);
            //TerrainDetail1
            zc.AddUUID(regionSettings.TerrainTexture2);
            //TerrainDetail2
            zc.AddUUID(regionSettings.TerrainTexture3);
            //TerrainDetail3
            zc.AddUUID(regionSettings.TerrainTexture4);
            //TerrainStartHeight00
            zc.AddFloat((float)regionSettings.Elevation1SW);
            //TerrainStartHeight01
            zc.AddFloat((float)regionSettings.Elevation1NW);
            //TerrainStartHeight10
            zc.AddFloat((float)regionSettings.Elevation1SE);
            //TerrainStartHeight11
            zc.AddFloat((float)regionSettings.Elevation1NE);
            //TerrainHeightRange00
            zc.AddFloat((float)regionSettings.Elevation2SW);
            //TerrainHeightRange01
            zc.AddFloat((float)regionSettings.Elevation2NW);
            //TerrainHeightRange10
            zc.AddFloat((float)regionSettings.Elevation2SE);
            //TerrainHeightRange11
            zc.AddFloat((float)regionSettings.Elevation2NE);

            //RegionInfo2 block

            //region ID
            zc.AddUUID(regionInfo.RegionID);

            //RegionInfo3 block

            //CPUClassID
            zc.AddInt(9);
            //CPURatio
            zc.AddInt(1);
            // ColoName (string)
            // ProductSKU (string)
            // both empty strings
            zc.AddZeros(2);
            //ProductName
            zc.AddShortString(regionInfo.RegionType, 255);

            //RegionInfo4 block

            //RegionFlagsExtended
            //zc.AddZeros(1); // if we dont have this else
            zc.AddByte(1); 
            zc.AddUInt64(regionFlags); // we have nothing other base flags
            //RegionProtocols
                // bit 0 signals server side texture baking
                // bit 63 signals more than 6 baked textures support"
            zc.AddUInt64(1UL << 63);

            buf.DataLength = zc.Finish();
            m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Unknown);
        }

        static private readonly byte[] AgentMovementCompleteHeader = new byte[] {
                Helpers.MSG_RELIABLE,
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 0xff, 0, 250 // ID 250 (low frequency bigendian)
                };

        public unsafe void MoveAgentIntoRegion(RegionInfo regInfo, Vector3 pos, Vector3 look)
        {
            // reset agent update args
            m_thisAgentUpdateArgs.CameraAtAxis.X = float.MinValue;
            m_thisAgentUpdateArgs.lastUpdateTS = 0;
            m_thisAgentUpdateArgs.ControlFlags = 0;

            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            byte[] bdata = buf.Data;

            //setup header
            Buffer.BlockCopy(AgentMovementCompleteHeader, 0, bdata, 0, 10);

            fixed(byte* data = bdata)
            {            
                //AgentData block
                m_agentId.ToBytes(data + 10); // 26
                m_sessionId.ToBytes(data + 26); // 42

                //Data block
                if ((pos.X == 0) && (pos.Y == 0) && (pos.Z == 0))
                    m_startpos.ToBytes(data + 42); //54
                else
                    pos.ToBytes(data + 42); //54
                look.ToBytes(data + 54); // 66
                //Utils.UInt64ToBytesSafepos(regInfo.RegionHandle, data + 66); // 74
                //Utils.UIntToBytesSafepos((uint)Util.UnixTimeSinceEpoch(), data + 74); //78
                Utils.UInt64ToBytes(regInfo.RegionHandle, data + 66); // 74
                Utils.IntToBytes((int)Util.UnixTimeSinceEpoch(), data + 74); //78

                //SimData
                int len = m_regionChannelVersion.Length;
                if(len == 0)
                {
                    data[78] = 0;
                    data[79] = 0;
                }
                else
                {
                    data[78] = (byte)len;
                    data[79] = (byte)(len >> 8);
                    Buffer.BlockCopy(m_regionChannelVersion, 0, bdata, 80, len);
                }
                    buf.DataLength = 80 + len;
            }
            m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Unknown);
        }

        static private readonly byte[] ChatFromSimulatorHeader = new byte[] {
                Helpers.MSG_RELIABLE,
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 0xff, 0, 139 // ID 139 (low frequency bigendian)
                };

        public void SendChatMessage(string message, byte chattype, Vector3 fromPos, string fromName,
            UUID sourceID, UUID ownerID, byte sourcetype, byte audible)
        {
            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            byte[] data = buf.Data;

            //setup header
            Buffer.BlockCopy(ChatFromSimulatorHeader, 0, data, 0, 10);

            int pos = 11;
            int len = Util.osUTF8Getbytes(fromName, data, 11, 255, true);
            data[10] = (byte)len;
            if (len > 0)
                pos += len;

            sourceID.ToBytes(data, pos); pos += 16;
            ownerID.ToBytes(data, pos); pos += 16;
            data[pos++] = sourcetype;
            data[pos++] = chattype;
            data[pos++] = audible;
            fromPos.ToBytes(data, pos); pos += 12;

            byte[] msg = Util.StringToBytes1024(message);
            len = msg.Length;
            if (len == 0)
            {
                data[pos++] = 0;
                data[pos++] = 0;
            }
            else
            {
                data[pos++] = (byte)len;
                data[pos++] = (byte)(len >> 8);
                Buffer.BlockCopy(msg, 0, data, pos, len); pos += len;
            }

            buf.DataLength = pos;
            m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Unknown);
        }

        /// <summary>
        /// Send an instant message to this client
        /// </summary>
        //

        static private readonly byte[] ImprovedInstantMessageHeader = new byte[] {
                Helpers.MSG_RELIABLE, //| Helpers.MSG_ZEROCODED, not doing spec zeroencode on this
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 0xff, 0, 254 // ID 139 (low frequency bigendian)
                };

        public void SendInstantMessage(GridInstantMessage im)
        {
            UUID fromAgentID = new(im.fromAgentID);
            UUID toAgentID = new(im.toAgentID);

            if (!m_scene.Permissions.CanInstantMessage(fromAgentID, toAgentID))
                return;

            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            byte[] data = buf.Data;

            //setup header
            Buffer.BlockCopy(ImprovedInstantMessageHeader, 0, data, 0, 10);

            //agentdata block
            fromAgentID.ToBytes(data, 10); // 26
            UUID.Zero.ToBytes(data, 26); // 42  sessionID  zero?? TO check

            int pos = 42;

            //MessageBlock
            data[pos++] = (byte)((im.fromGroup) ? 1 : 0);
            toAgentID.ToBytes(data, pos); pos += 16;
            Utils.UIntToBytesSafepos(im.ParentEstateID, data, pos); pos += 4;
            (new UUID(im.RegionID)).ToBytes(data, pos); pos += 16;
            (im.Position).ToBytes(data, pos); pos += 12;
            data[pos++] = im.offline;
            data[pos++] = im.dialog;

            // this is odd
            if (im.imSessionID == UUID.Zero.Guid)
                (fromAgentID ^ toAgentID).ToBytes(data, pos);
            else
                (new UUID(im.imSessionID)).ToBytes(data, pos);

            pos += 16;

            Utils.UIntToBytesSafepos(im.timestamp, data, pos); pos += 4;

            int len = Util.osUTF8Getbytes(im.fromAgentName, data, pos + 1, 255, true);
            data[pos++] = (byte)len;
            if (len > 0)
                pos += len;

            len = Util.osUTF8Getbytes(im.message, data, pos + 2, 1024, true);
            if (len == 0)
            {
                data[pos++] = 0;
                data[pos++] = 0;
            }
            else
            {
                data[pos++] = (byte)len;
                data[pos++] = (byte)(len >> 8);
                pos += len;
            }

            byte[] tmp = im.binaryBucket;
            if(tmp is null)
            {
                data[pos++] = 0;
                data[pos++] = 0;
            }
            else
            {
                len = tmp.Length;
                if (len == 0)
                {
                    data[pos++] = 0;
                    data[pos++] = 0;
                }
                else
                {
                    data[pos++] = (byte)len;
                    data[pos++] = (byte)(len >> 8);
                    Buffer.BlockCopy(tmp, 0, data, pos, len); pos += len;
                }
            }

            //EstateBlock does not seem in use TODO
            //Utils.UIntToBytesSafepos(m_scene.RegionInfo.EstateSettings.EstateID, data, pos); pos += 4;
            data[pos++] = 0;
            data[pos++] = 0;
            data[pos++] = 0;
            data[pos++] = 0;

            buf.DataLength = pos;
            //m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Unknown, null, false, true);
            m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Unknown);
        }

        static private readonly byte[] GenericMessageHeader = new byte[] {
                Helpers.MSG_RELIABLE, //| Helpers.MSG_ZEROCODED, not doing spec zeroencode on this
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 0xff, 1, 5 // ID 261 (low frequency bigendian)
                };

        public void SendGenericMessage(string method, UUID invoice, List<string> message)
        {
            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            byte[] data = buf.Data;

            //setup header
            Buffer.BlockCopy(GenericMessageHeader, 0, data, 0, 10);

            //agentdata block
            m_agentId.ToBytes(data, 10); // 26
            m_sessionId.ToBytes(data, 26); // 42  sessionID  zero?? TO check
            UUID.Zero.ToBytes(data, 42); // 58

            int pos = 58;

            //method block
            int len = Util.osUTF8Getbytes(method, data, pos + 1, 255, true);
            data[pos++] = (byte)len;
            if (len > 0)
                pos += len;

            invoice.ToBytes(data, pos); pos += 16;

            //ParamList block
            if (message.Count == 0)
            {
                data[pos++] = 0;
                buf.DataLength = pos;
                //m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task, null, false, true);
                m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
                return;
            }

            int countpos = pos;
            ++pos;

            int count = 0;
            for(int indx = 0; indx < message.Count; ++indx)
            {
                len = Util.osUTF8Getbytes(message[indx], data, pos + 1, 255, true);
                data[pos++] = (byte)len;
                if (len > 0)
                    pos += len;

                if (pos > LLUDPServer.MAXPAYLOAD - 100)
                {
                    data[countpos] = (byte)count;
                    buf.DataLength = pos;
                    //m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task, null, false, true);
                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);

                    UDPPacketBuffer newbuf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                    Buffer.BlockCopy(data, 0, newbuf.Data, 0, countpos);
                    buf = newbuf;
                    data = buf.Data;
                    pos = countpos + 1;
                    count = 1;
                }
                else
                    ++count;
            }
            if (count > 0)
            {
                data[countpos] = (byte)count;
                buf.DataLength = pos;
                //m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task, null, false, true);
                m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
            }
        }

        public void SendGenericMessage(string method, UUID invoice, List<byte[]> message)
        {
            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            byte[] data = buf.Data;

            //setup header
            Buffer.BlockCopy(GenericMessageHeader, 0, data, 0, 10);

            //agentdata block
            m_agentId.ToBytes(data, 10); // 26
            m_sessionId.ToBytes(data, 26); // 42  sessionID  zero?? TO check
            UUID.Zero.ToBytes(data, 42); // 58

            int pos = 58;

            //method block
            int len = Util.osUTF8Getbytes(method, data, pos + 1, 255, true);
            data[pos++] = (byte)len;
            if (len > 0)
                pos += len;

            invoice.ToBytes(data, pos); pos += 16;

            //ParamList block
            if (message.Count == 0)
            {
                data[pos++] = 0;
                buf.DataLength = pos;
                //m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task, null, false, true);
                m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
                return;
            }

            int countpos = pos;
            ++pos;

            byte[] val;
            int count = 0;
            for (int indx = 0; indx < message.Count; ++indx)
            {
                val = message[indx];
                len = val.Length;
                if(len > 255)
                    len = 255;

                if (pos + len >= LLUDPServer.MAXPAYLOAD)
                {
                    data[countpos] = (byte)count;
                    buf.DataLength = pos;
                    //m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task, null, false, true);
                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);

                    UDPPacketBuffer newbuf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                    Buffer.BlockCopy(data, 0, newbuf.Data, 0, countpos);
                    buf = newbuf;
                    data = buf.Data;
                    pos = countpos + 1;
                    count = 1;
                }
                else
                    ++count;

                data[pos++] = (byte)len;
                if (len > 0)
                    Buffer.BlockCopy(val, 0, data, pos, len); pos += len;
            }
            if (count > 0)
            {
                data[countpos] = (byte)count;
                buf.DataLength = pos;
                //m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task, null, false, true);
                m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
            }
        }

        public void SendGroupActiveProposals(UUID groupID, UUID transactionID, GroupActiveProposals[] Proposals)
        {
            /* not in use and broken
            GroupActiveProposalItemReplyPacket GAPIRP = new();

            GAPIRP.AgentData.AgentID = m_agentId;
            GAPIRP.AgentData.GroupID = groupID;

            GAPIRP.TransactionData.TransactionID = transactionID;
            if (Proposals.Length == 0)
            {
                GAPIRP.TransactionData.TotalNumItems = 1;
                GAPIRP.ProposalData = new GroupActiveProposalItemReplyPacket.ProposalDataBlock[]
                {
                    new GroupActiveProposalItemReplyPacket.ProposalDataBlock()
                    {
                        VoteCast = Utils.StringToBytes("false"),
                        VoteID = UUID.Zero,
                        VoteInitiator = UUID.Zero,
                        Majority = 0,
                        Quorum = 0,
                        TerseDateID = Array.Empty<byte>(),
                        StartDateTime = Array.Empty<byte>(),
                        EndDateTime = Array.Empty<byte>(),
                        ProposalText = Array.Empty<byte>(),
                        AlreadyVoted = false
                    }
                };
            }
            else
            {
                GAPIRP.TransactionData.TotalNumItems = (uint)Proposals.Length;
                GAPIRP.ProposalData = new GroupActiveProposalItemReplyPacket.ProposalDataBlock[Proposals.Length];

                int i = 0;
                foreach (GroupActiveProposals Proposal in Proposals)
                {
                    GAPIRP.ProposalData[i++] = new GroupActiveProposalItemReplyPacket.ProposalDataBlock()
                    {
                        VoteCast = Utils.StringToBytes("false"),
                        VoteID = new UUID(Proposal.VoteID),
                        VoteInitiator = new UUID(Proposal.VoteInitiator),
                        Majority = Convert.ToSingle(Proposal.Majority),
                        Quorum = Convert.ToInt32(Proposal.Quorum),
                        TerseDateID = Utils.StringToBytes(Proposal.TerseDateID),
                        StartDateTime = Utils.StringToBytes(Proposal.StartDateTime),
                        EndDateTime = Utils.StringToBytes(Proposal.EndDateTime),
                        ProposalText = Utils.StringToBytes(Proposal.ProposalText),
                        AlreadyVoted = false
                    };
                }
            }
            OutPacket(GAPIRP, ThrottleOutPacketType.Task);
            */
        }

        public void SendGroupVoteHistory(UUID groupID, UUID transactionID, GroupVoteHistory[] Votes)
        {
            /* not in use and broken
            GroupVoteHistoryItemReplyPacket GVHIRP = new GroupVoteHistoryItemReplyPacket();

            GVHIRP.AgentData.AgentID = m_agentId;
            GVHIRP.AgentData.GroupID = groupID;
            GVHIRP.TransactionData.TransactionID = transactionID;

            if (Votes.Length == 0)
            {
                GVHIRP.TransactionData.TotalNumItems = 0;
                GVHIRP.HistoryItemData.VoteID = UUID.Zero;
                GVHIRP.HistoryItemData.VoteInitiator = UUID.Zero;
                GVHIRP.HistoryItemData.Majority = 0;
                GVHIRP.HistoryItemData.Quorum = 0;
                GVHIRP.HistoryItemData.TerseDateID = Array.Empty<byte>();
                GVHIRP.HistoryItemData.StartDateTime = Array.Empty<byte>();
                GVHIRP.HistoryItemData.EndDateTime = Array.Empty<byte>();
                GVHIRP.HistoryItemData.VoteType = Array.Empty<byte>();
                GVHIRP.HistoryItemData.VoteResult = Array.Empty<byte>();
                GVHIRP.HistoryItemData.ProposalText = Array.Empty<byte>();
                GVHIRP.VoteItem = new GroupVoteHistoryItemReplyPacket.VoteItemBlock[]
                {
                    new GroupVoteHistoryItemReplyPacket.VoteItemBlock()
                    {
                        CandidateID = UUID.Zero,
                        NumVotes = 0, //TODO: FIX THIS!!!
                        VoteCast = Utils.StringToBytes("No")
                    }
                };
            }
            else
            {
                GVHIRP.TransactionData.TotalNumItems = (uint)Votes.Length;
                int i = 0;
                foreach (GroupVoteHistory Vote in Votes)
                {
                    GVHIRP.HistoryItemData.VoteID = new UUID(Vote.VoteID);
                    GVHIRP.HistoryItemData.VoteInitiator = new UUID(Vote.VoteInitiator);
                    GVHIRP.HistoryItemData.Majority = (float)Convert.ToInt32(Vote.Majority);
                    GVHIRP.HistoryItemData.Quorum = Convert.ToInt32(Vote.Quorum);
                    GVHIRP.HistoryItemData.TerseDateID = Utils.StringToBytes(Vote.TerseDateID);
                    GVHIRP.HistoryItemData.StartDateTime = Utils.StringToBytes(Vote.StartDateTime);
                    GVHIRP.HistoryItemData.EndDateTime = Utils.StringToBytes(Vote.EndDateTime);
                    GVHIRP.HistoryItemData.VoteType = Utils.StringToBytes(Vote.VoteType);
                    GVHIRP.HistoryItemData.VoteResult = Utils.StringToBytes(Vote.VoteResult);
                    GVHIRP.HistoryItemData.ProposalText = Utils.StringToBytes(Vote.ProposalText);
                    GroupVoteHistoryItemReplyPacket.VoteItemBlock VoteItem = new GroupVoteHistoryItemReplyPacket.VoteItemBlock();
                    GVHIRP.VoteItem = new GroupVoteHistoryItemReplyPacket.VoteItemBlock[1];
                    VoteItem.CandidateID = UUID.Zero;
                    VoteItem.NumVotes = 0; //TODO: FIX THIS!!!
                    VoteItem.VoteCast = Utils.StringToBytes("Yes");
                    GVHIRP.VoteItem[i] = VoteItem;
                    i++;
                }
            }
            OutPacket(GVHIRP, ThrottleOutPacketType.Task);
            */
        }

        public void SendGroupAccountingDetails(IClientAPI sender,UUID groupID, UUID transactionID, UUID sessionID, int amt)
        {
            GroupAccountDetailsReplyPacket GADRP = new();

            GADRP.AgentData.AgentID = sender.AgentId;
            GADRP.AgentData.GroupID = groupID;

            GADRP.MoneyData.CurrentInterval = 0;
            GADRP.MoneyData.IntervalDays = 7;
            GADRP.MoneyData.RequestID = transactionID;
            GADRP.MoneyData.StartDate = Utils.StringToBytes(DateTime.Today.ToString());

            GADRP.HistoryData = new GroupAccountDetailsReplyPacket.HistoryDataBlock[]
            {
                new GroupAccountDetailsReplyPacket.HistoryDataBlock()
                {
                    Amount = amt,
                    Description = Array.Empty<byte>()
                }
            };
            OutPacket(GADRP, ThrottleOutPacketType.Task);
        }

        public void SendGroupAccountingSummary(IClientAPI sender,UUID groupID, uint moneyAmt, int totalTier, int usedTier)
        {
            GroupAccountSummaryReplyPacket GASRP =
                    (GroupAccountSummaryReplyPacket)PacketPool.Instance.GetPacket(
                    PacketType.GroupAccountSummaryReply);

            GASRP.AgentData.AgentID = sender.AgentId;
            GASRP.AgentData.GroupID = groupID;

            GASRP.MoneyData.Balance = (int)moneyAmt;
            GASRP.MoneyData.TotalCredits = totalTier;
            GASRP.MoneyData.TotalDebits = usedTier;
            GASRP.MoneyData.StartDate = new byte[1];
            GASRP.MoneyData.CurrentInterval = 1;
            GASRP.MoneyData.GroupTaxCurrent = 0;
            GASRP.MoneyData.GroupTaxEstimate = 0;
            GASRP.MoneyData.IntervalDays = 0;
            GASRP.MoneyData.LandTaxCurrent = 0;
            GASRP.MoneyData.LandTaxEstimate = 0;
            GASRP.MoneyData.LastTaxDate = new byte[1];
            GASRP.MoneyData.LightTaxCurrent = 0;
            GASRP.MoneyData.TaxDate = new byte[1];
            GASRP.MoneyData.RequestID = sender.AgentId;
            GASRP.MoneyData.ParcelDirFeeEstimate = 0;
            GASRP.MoneyData.ParcelDirFeeCurrent = 0;
            GASRP.MoneyData.ObjectTaxEstimate = 0;
            GASRP.MoneyData.NonExemptMembers = 0;
            GASRP.MoneyData.ObjectTaxCurrent = 0;
            GASRP.MoneyData.LightTaxEstimate = 0;

            OutPacket(GASRP, ThrottleOutPacketType.Task);
        }

        public void SendGroupTransactionsSummaryDetails(IClientAPI sender,UUID groupID, UUID transactionID, UUID sessionID, int amt)
        {
            // bad !
            GroupAccountTransactionsReplyPacket GATRP =
                    (GroupAccountTransactionsReplyPacket)PacketPool.Instance.GetPacket(
                    PacketType.GroupAccountTransactionsReply);

            GATRP.AgentData.AgentID = sender.AgentId;
            GATRP.AgentData.GroupID = groupID;

            GATRP.MoneyData.CurrentInterval = 0;
            GATRP.MoneyData.IntervalDays = 7;
            GATRP.MoneyData.RequestID = transactionID;
            GATRP.MoneyData.StartDate = Utils.StringToBytes(DateTime.Today.ToString());

            GATRP.HistoryData = new GroupAccountTransactionsReplyPacket.HistoryDataBlock[]
            {
                new GroupAccountTransactionsReplyPacket.HistoryDataBlock()
                {
                    Amount = amt,
                    Item = Array.Empty<byte>(),
                    Time = Array.Empty<byte>(),
                    Type = 0,
                    User = Array.Empty<byte>(),
                }
            };
            OutPacket(GATRP, ThrottleOutPacketType.Task);
        }

        public virtual bool CanSendLayerData()
        {
            int n = m_udpClient.GetPacketsQueuedCount(ThrottleOutPacketType.Land);
            return n <= 128;
        }

        /// <summary>
        ///  Send the region heightmap to the client
        ///  This method is only called when not doing intellegent terrain patch sending and
        ///  is only called when the scene presence is initially created and sends all of the
        ///  region's patches to the client.
        /// </summary>
        /// <param name="map">heightmap</param>
        public virtual void SendLayerData()
        {
            Util.FireAndForget(DoSendLayerData, null, "LLClientView.DoSendLayerData");
        }

        /// <summary>
        /// Send terrain layer information to the client.
        /// </summary>
        /// <param name="o"></param>
        private void DoSendLayerData(object o)
        {
            TerrainData map = m_scene.Heightmap.GetTerrainData();
            try
            {
                // Send LayerData in a spiral pattern. Fun!
                SendLayerTopRight(0, 0, map.SizeX / Constants.TerrainPatchSize - 1, map.SizeY / Constants.TerrainPatchSize - 1);
            }
            catch (Exception e)
            {
                m_log.Error("[CLIENT]: SendLayerData() Failed with exception: " + e.Message, e);
            }
        }

        private void SendLayerTopRight(int x1, int y1, int x2, int y2)
        {
            int[] p = new int[2];

            // Row
            p[1] = y1;
            for (int i = x1; i <= x2; ++i)
            {
                p[0] = i;
                SendLayerData(p);
            }

            // Column
            p[0] = x2;
            for (int j = y1 + 1; j <= y2; ++j)
            {
                p[1] = j;
                SendLayerData(p);
            }

            if (x2 - x1 > 0 && y2 - y1 > 0)
                SendLayerBottomLeft(x1, y1 + 1, x2 - 1, y2);
        }

        void SendLayerBottomLeft(int x1, int y1, int x2, int y2)
        {
            int[] p = new int[2];

            // Row in reverse
            p[1] = y2;
            for (int i = x2; i >= x1; --i)
            {
                p[0] = i;
                SendLayerData(p);
            }

            // Column in reverse
            p[0] = x1;
            for (int j = y2 - 1; j >= y1; --j)
            {
                p[1] = j;
                SendLayerData(p);
            }

            if (x2 - x1 > 0 && y2 - y1 > 0)
                SendLayerTopRight(x1 + 1, y1, x2, y2 - 1);
        }

        static private readonly byte[] TerrainPacketHeader = new byte[] {
                Helpers.MSG_RELIABLE, // zero code is not as spec
                0, 0, 0, 0, // sequence number
                0, // extra
                11, // ID (high frequency)
                };

        private const int END_OF_PATCHES = 97;
        private const int STRIDE = 264;

        public void SendLayerData(int[] map)
        {
            if(map is null)
                return;

            try
            {
                TerrainData terrData = m_scene.Heightmap.GetTerrainData();
                byte landPacketType = (terrData.SizeX > Constants.RegionSize || terrData.SizeY > Constants.RegionSize) ?
                        (byte)TerrainPatch.LayerType.LandExtended : (byte)TerrainPatch.LayerType.Land;

                int numberPatchs = map.Length / 2;

                UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                byte[] data = buf.Data;

                Buffer.BlockCopy(TerrainPacketHeader, 0, data, 0, 7);

                data[7] = landPacketType;
                //data[8]  and data[9] == datablock size to fill later

                data[10] = 0; // BitPack needs this on reused packets

                // start data
                BitPack bitpack = new(data, 10);
                bitpack.PackBits(STRIDE, 16);
                bitpack.PackBitsFromByte(16);
                bitpack.PackBitsFromByte(landPacketType);

                int s;
                int datasize = 0;
                for (int i = 0; i < numberPatchs; i++)
                {
                    s = 2 * i;
                    OpenSimTerrainCompressor.CreatePatchFromTerrainData(bitpack, terrData, map[s], map[s + 1]);
                    if (bitpack.BytePos > 900 && i != numberPatchs - 1)
                    {
                        //finish this packet
                        bitpack.PackBitsFromByte(END_OF_PATCHES);

                        // fix the datablock length
                        datasize = bitpack.BytePos - 9;
                        data[8] = (byte)datasize;
                        data[9] = (byte)(datasize >> 8);

                        buf.DataLength = bitpack.BytePos + 1;
                        m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Land);

                        // start another
                        buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                        data = buf.Data;

                        Buffer.BlockCopy(TerrainPacketHeader, 0, data, 0, 7);

                        data[7] = landPacketType;
                        //data[8]  and data[9] == datablock size to fill later

                        data[10] = 0; // BitPack needs this
                                      // start data
                        bitpack = new BitPack(data, 10);

                        bitpack.PackBits(STRIDE, 16);
                        bitpack.PackBitsFromByte(16);
                        bitpack.PackBitsFromByte(landPacketType);
                    }
                }

                bitpack.PackBitsFromByte(END_OF_PATCHES);

                datasize = bitpack.BytePos - 9;
                data[8] = (byte)datasize;
                data[9] = (byte)(datasize >> 8);

                buf.DataLength = bitpack.BytePos + 1;
                m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Land);

            }
            catch (Exception e)
            {
                m_log.Error("[CLIENT]: SendLayerData() Failed with exception: " + e.Message, e);
            }
        }

        private static void DebugSendingPatches(string pWho, int[] pX, int[] pY)
        {
            if (m_log.IsDebugEnabled)
            {
                int numPatches = pX.Length;
                string Xs = "";
                string Ys = "";
                for (int pp = 0; pp < numPatches; pp++)
                {
                    Xs += String.Format("{0}", (int)pX[pp]) + ",";
                    Ys += String.Format("{0}", (int)pY[pp]) + ",";
                }
                m_log.DebugFormat("{0} {1}: numPatches={2}, X={3}, Y={4}", LogHeader, pWho, numPatches, Xs, Ys);
            }
        }

        // wind caching
        private readonly static Dictionary<ulong,int> lastWindVersion = new();
        private readonly static Dictionary<ulong,List<LayerDataPacket>> lastWindPackets = new();


        /// <summary>
        ///  Send the wind matrix to the client
        /// </summary>
        /// <param name="windSpeeds">16x16 array of wind speeds</param>
        public virtual void SendWindData(int version, Vector2[] windSpeeds)
        {
            //Vector2[] windSpeeds = (Vector2[])o;

            ulong handle = this.Scene.RegionInfo.RegionHandle;
            bool isNewData;
            lock(lastWindPackets)
            {
                if(!lastWindVersion.ContainsKey(handle) ||
                    !lastWindPackets.ContainsKey(handle))
                {
                    lastWindVersion[handle] = 0;
                    lastWindPackets[handle] = new List<LayerDataPacket>();
                    isNewData = true;
                }
                else
                    isNewData = lastWindVersion[handle] != version;
            }

            if(isNewData)
            {
                TerrainPatch[] patches = new TerrainPatch[2];
                patches[0] = new TerrainPatch { Data = new float[16 * 16] };
                patches[1] = new TerrainPatch { Data = new float[16 * 16] };

                for (int x = 0; x < 16 * 16; x++)
                {
                    patches[0].Data[x] = windSpeeds[x].X;
                    patches[1].Data[x] = windSpeeds[x].Y;
                }

                // neither we or viewers have extended wind
                byte layerType = (byte)TerrainPatch.LayerType.Wind;

                LayerDataPacket layerpack =
                     OpenSimTerrainCompressor.CreateLayerDataPacketStandardSize(
                        patches, layerType);
                layerpack.Header.Zerocoded = true;
                lock(lastWindPackets)
                {
                    lastWindPackets[handle].Clear();
                    lastWindPackets[handle].Add(layerpack);
                    lastWindVersion[handle] = version;
                }
            }

            lock(lastWindPackets)
                foreach(LayerDataPacket pkt in lastWindPackets[handle])
                    OutPacket(pkt, ThrottleOutPacketType.Wind);
        }

        /// <summary>
        /// Tell the client that the given neighbour region is ready to receive a child agent.
        /// </summary>
        public virtual void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourEndPoint)
        {
            byte[] byteIP = neighbourEndPoint.Address.GetAddressBytes();
            uint neighbourIP =  (uint)byteIP[3] << 24 |
                                (uint)byteIP[2] << 16 |
                                (uint)byteIP[1] << 8 |
                                (uint)byteIP[0];
            ushort neighbourPort = (ushort)neighbourEndPoint.Port;

            EnableSimulatorPacket enablesimpacket = new();
            enablesimpacket.SimulatorInfo.Handle = neighbourHandle;
            enablesimpacket.SimulatorInfo.IP = neighbourIP;
            enablesimpacket.SimulatorInfo.Port = neighbourPort;

            enablesimpacket.Header.Reliable = true; // ESP's should be reliable.


            OutPacket(enablesimpacket, ThrottleOutPacketType.Task);
        }

        public AgentCircuitData RequestClientInfo()
        {
            AgentCircuitData agentData = new()
            {
                AgentID = m_agentId,
                SessionID = m_sessionId,
                SecureSessionID = SecureSessionId,
                circuitcode = m_circuitCode,
                child = false,
                firstname = m_firstName,
                lastname = m_lastName
            };

            ICapabilitiesModule capsModule = m_scene.RequestModuleInterface<ICapabilitiesModule>();
            if (capsModule is not null) // can happen when shutting down.
            {
                agentData.CapsPath = capsModule.GetCapsPath(m_agentId);
                agentData.ChildrenCapSeeds = new Dictionary<ulong, string>(capsModule.GetChildrenSeeds(m_agentId));
            }
            return agentData;
        }

        public virtual void CrossRegion(ulong newRegionHandle, Vector3 pos, Vector3 lookAt, IPEndPoint externalIPEndPoint,
                                string capsURL)
        {
            Vector3 look = new(lookAt.X * 10, lookAt.Y * 10, lookAt.Z * 10);
            byte[] byteIP = externalIPEndPoint.Address.GetAddressBytes();
            uint externalIP = (uint)byteIP[3] << 24 |
                              (uint)byteIP[2] << 16 |
                              (uint)byteIP[1] << 8 |
                              (uint)byteIP[0];

            CrossedRegionPacket newSimPack = new();

            newSimPack.AgentData.AgentID = m_agentId;
            newSimPack.AgentData.SessionID = m_sessionId;

            newSimPack.Info.Position = pos;
            newSimPack.Info.LookAt = look;

            newSimPack.RegionData.RegionHandle = newRegionHandle;
            newSimPack.RegionData.SimIP = externalIP;
            newSimPack.RegionData.SimPort = (ushort)externalIPEndPoint.Port;
            newSimPack.RegionData.SeedCapability = Util.StringToBytes256(capsURL);

            // Hack to get this out immediately and skip throttles
            OutPacket(newSimPack, ThrottleOutPacketType.Unknown);
        }

        static private readonly byte[] MapBlockItemHeader = new byte[] {
                Helpers.MSG_RELIABLE,
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 0xff, 1, 155 // ID 411 (low frequency bigendian)
                };

        public void SendMapItemReply(mapItemReply[] replies, uint mapitemtype, uint flags)
        {
            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            byte[] data = buf.Data;

            //setup header and agentinfo block
            Buffer.BlockCopy(MapBlockItemHeader, 0, data, 0, 10);
            m_agentId.ToBytes(data, 10); // 26
            Utils.UIntToBytesSafepos(flags, data, 26); // 30

            //RequestData block
            Utils.UIntToBytesSafepos(mapitemtype, data, 30); // 34

            int countpos = 34;
            int pos = 35;
            int lastpos;

            int capacity = LLUDPServer.MAXPAYLOAD - pos;

            int count = 0;

            mapItemReply mr;
            for (int k = 0; k < replies.Length; ++k)
            {
                lastpos = pos;
                mr = replies[k];

                Utils.UIntToBytesSafepos(mr.x, data, pos); pos += 4;
                Utils.UIntToBytesSafepos(mr.y, data, pos); pos += 4;
                mr.id.ToBytes(data, pos); pos += 16;
                Utils.IntToBytesSafepos(mr.Extra, data, pos); pos += 4;
                Utils.IntToBytesSafepos(mr.Extra2, data, pos); pos += 4;

                int len = Util.osUTF8Getbytes(mr.name, data, pos + 1, 255, true);
                data[pos++] = (byte)len;
                if (len > 0)
                    pos += len;

                if (pos < capacity)
                    ++count;
                else
                {
                    // prepare next packet
                    UDPPacketBuffer newbuf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                    Buffer.BlockCopy(data, 0, newbuf.Data, 0, 34);

                    // copy the block we already did
                    int alreadyDone = pos - lastpos;
                    Buffer.BlockCopy(data, lastpos, newbuf.Data, 35, alreadyDone); // 34 is datablock size

                    // finish current
                    data[countpos] = (byte)count;

                    buf.DataLength = lastpos;
                    // send it
                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Land);

                    buf = newbuf;
                    data = buf.Data;
                    pos = alreadyDone + 35;
                    capacity = LLUDPServer.MAXPAYLOAD - pos;

                    count = 1;
                }
            }

            if (count > 0)
            {
                data[countpos] = (byte)count;

                buf.DataLength = pos;
                m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Land);
            }
        }

        static private readonly byte[] MapBlockReplyHeader = new byte[] {
                Helpers.MSG_RELIABLE,
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 0xff, 1, 153 // ID 409 (low frequency bigendian)
                };

        public void SendMapBlock(List<MapBlockData> mapBlocks, uint flags)
        {
            ushort[] sizes =  new ushort[2 * mapBlocks.Count];
            bool needSizes = false;
            int sizesptr = 0;

            // check if we will need sizes block and get them aside
            int count = 0;
            ushort ut;
            MapBlockData md;
            for (int indx = 0; indx < mapBlocks.Count; ++indx)
            {
                md = mapBlocks[indx];
                ut = md.SizeX;
                sizes[count++] = ut;
                if (ut > 256)
                    needSizes = true;

                ut = md.SizeY;
                sizes[count++] = ut;
                if (ut > 256)
                    needSizes = true;
            }

            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            byte[] data = buf.Data;

            //setup header and agentinfo block
            Buffer.BlockCopy(MapBlockReplyHeader, 0, data, 0, 10);
            m_agentId.ToBytes(data, 10); // 26
            Utils.UIntToBytesSafepos(flags, data, 26); // 30

            int countpos = 30;
            int pos = 31;
            int lastpos;

            int capacity = LLUDPServer.MAXPAYLOAD - pos;

            count = 0;

            for (int indx = 0; indx < mapBlocks.Count; ++indx)
            {
                md = mapBlocks[indx];
                lastpos = pos;

                Utils.UInt16ToBytes(md.X, data, pos); pos += 2;
                Utils.UInt16ToBytes(md.Y, data, pos); pos += 2;

                int len = Util.osUTF8Getbytes(md.Name, data, pos + 1, 255, true);
                data[pos++] = (byte)len;
                if (len > 0)
                    pos += len;

                data[pos++] = md.Access;
                Utils.UIntToBytesSafepos(md.RegionFlags, data, pos); pos += 4;
                data[pos++] = md.WaterHeight;
                data[pos++] = md.Agents;
                md.MapImageId.ToBytes(data, pos); pos += 16;

                if(needSizes)
                    capacity -= 4; // 2 shorts per entry

                if(pos < capacity)
                    ++count;
                else
                {
                    // prepare next packet
                    UDPPacketBuffer newbuf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                    Buffer.BlockCopy(data, 0, newbuf.Data, 0, 30);

                    // copy the block we already did
                    int alreadyDone = pos - lastpos;
                    Buffer.BlockCopy(data, lastpos, newbuf.Data, 31, alreadyDone); // 30 is datablock size

                    // finish current
                    data[countpos] = (byte)count;
                    if (needSizes)
                    {
                        data[lastpos++] = (byte)count;
                        while (--count >= 0)
                        {
                            Utils.UInt16ToBytes(sizes[sizesptr++], data, lastpos); lastpos += 2;
                            Utils.UInt16ToBytes(sizes[sizesptr++], data, lastpos); lastpos += 2;
                        }
                    }
                    else
                        data[lastpos++] = 0;

                    buf.DataLength = lastpos;
                    // send it
                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Land);

                    buf = newbuf;
                    data = buf.Data;
                    pos = alreadyDone + 31;
                    capacity = LLUDPServer.MAXPAYLOAD - pos;
                    if (needSizes)
                        capacity -= 4; // 2 shorts per entry

                    count = 1;
                }
            }

            if (count > 0)
            {
                data[countpos] = (byte)count;
                if (needSizes)
                {
                    data[pos++] = (byte)count;
                    while (--count >= 0)
                    {
                        Utils.UInt16ToBytes(sizes[sizesptr++], data, pos); pos += 2;
                        Utils.UInt16ToBytes(sizes[sizesptr++], data, pos); pos += 2;
                    }
                }
                else
                    data[pos++] = 0;

                buf.DataLength = pos;
                m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Land);
            }
        }

        public void SendLocalTeleport(Vector3 position, Vector3 lookAt, uint flags)
        {
            TeleportLocalPacket tpLocal = (TeleportLocalPacket)PacketPool.Instance.GetPacket(PacketType.TeleportLocal);
            tpLocal.Info.AgentID = m_agentId;
            tpLocal.Info.TeleportFlags = flags;
            tpLocal.Info.LocationID = 2;
            tpLocal.Info.LookAt = lookAt;
            tpLocal.Info.Position = position;

            // Hack to get this out immediately and skip throttles
            OutPacket(tpLocal, ThrottleOutPacketType.Unknown);
        }

        public virtual void SendRegionTeleport(ulong regionHandle, byte simAccess, IPEndPoint newRegionEndPoint, uint locationID,
                                       uint flags, string capsURL)
        {
            TeleportFinishPacket teleport = new();
            teleport.Info.AgentID = m_agentId;
            teleport.Info.RegionHandle = regionHandle;
            teleport.Info.SimAccess = simAccess;

            teleport.Info.SeedCapability = Util.StringToBytes256(capsURL);

            byte[] byteIP = newRegionEndPoint.Address.GetAddressBytes();
            teleport.Info.SimIP = (uint)byteIP[3] << 24 |
                                  (uint)byteIP[2] << 16 |
                                  (uint)byteIP[1] << 8 |
                                  (uint)byteIP[0];

            teleport.Info.SimPort = (ushort)newRegionEndPoint.Port;
            teleport.Info.LocationID = 4;
            teleport.Info.TeleportFlags = 1 << 4;

            // Hack to get this out immediately and skip throttles.
            OutPacket(teleport, ThrottleOutPacketType.Unknown);
        }

        /// <summary>
        /// Inform the client that a teleport attempt has failed
        /// </summary>
        public void SendTeleportFailed(string reason)
        {
            TeleportFailedPacket tpFailed = (TeleportFailedPacket)PacketPool.Instance.GetPacket(PacketType.TeleportFailed);
            tpFailed.Info.AgentID = m_agentId;
            tpFailed.Info.Reason = Util.StringToBytes256(reason);
            tpFailed.AlertInfo = Array.Empty<TeleportFailedPacket.AlertInfoBlock>();

            // Hack to get this out immediately and skip throttles
            OutPacket(tpFailed, ThrottleOutPacketType.Unknown);
        }

        /// <summary>
        ///
        /// </summary>
        public void SendTeleportStart(uint flags)
        {
            TeleportStartPacket tpStart = (TeleportStartPacket)PacketPool.Instance.GetPacket(PacketType.TeleportStart);
            //TeleportStartPacket tpStart = new TeleportStartPacket();
            tpStart.Info.TeleportFlags = flags; //16; // Teleport via location

            // Hack to get this out immediately and skip throttles
            OutPacket(tpStart, ThrottleOutPacketType.Unknown);
        }

        public void SendTeleportProgress(uint flags, string message)
        {
            TeleportProgressPacket tpProgress = (TeleportProgressPacket)PacketPool.Instance.GetPacket(PacketType.TeleportProgress);
            tpProgress.AgentData.AgentID = m_agentId;
            tpProgress.Info.TeleportFlags = flags;
            tpProgress.Info.Message = Util.StringToBytes256(message);

            // Hack to get this out immediately and skip throttles
            OutPacket(tpProgress, ThrottleOutPacketType.Unknown);
        }

        public void SendMoneyBalance(UUID transaction, bool success, byte[] description, int balance, int transactionType, UUID sourceID, bool sourceIsGroup, UUID destID, bool destIsGroup, int amount, string item)
        {
            MoneyBalanceReplyPacket money = (MoneyBalanceReplyPacket)PacketPool.Instance.GetPacket(PacketType.MoneyBalanceReply);
            money.MoneyData.AgentID = m_agentId;
            money.MoneyData.TransactionID = transaction;
            money.MoneyData.TransactionSuccess = success;
            money.MoneyData.Description = description;
            money.MoneyData.MoneyBalance = balance;
            money.TransactionInfo.TransactionType = transactionType;
            money.TransactionInfo.SourceID = sourceID;
            money.TransactionInfo.IsSourceGroup = sourceIsGroup;
            money.TransactionInfo.DestID = destID;
            money.TransactionInfo.IsDestGroup = destIsGroup;
            money.TransactionInfo.Amount = amount;
            money.TransactionInfo.ItemDescription = Util.StringToBytes256(item);

            OutPacket(money, ThrottleOutPacketType.Task);
        }

        public void SendPayPrice(UUID objectID, int[] payPrice)
        {
            if (payPrice[0] == 0 &&
                payPrice[1] == 0 &&
                payPrice[2] == 0 &&
                payPrice[3] == 0 &&
                payPrice[4] == 0)
                return;

            PayPriceReplyPacket payPriceReply = new();
            payPriceReply.ObjectData.ObjectID = objectID;
            payPriceReply.ObjectData.DefaultPayPrice = payPrice[0];
            payPriceReply.ButtonData = new PayPriceReplyPacket.ButtonDataBlock[]
            {
                new PayPriceReplyPacket.ButtonDataBlock
                {
                    PayButton = payPrice[1]
                },
                new PayPriceReplyPacket.ButtonDataBlock
                {
                    PayButton = payPrice[2]
                },
                new PayPriceReplyPacket.ButtonDataBlock
                {
                    PayButton = payPrice[3]
                },
                new PayPriceReplyPacket.ButtonDataBlock
                {
                    PayButton = payPrice[4]
                }
            };

            OutPacket(payPriceReply, ThrottleOutPacketType.Task);
        }

        public void SendKillObject(List<uint> localIDs)
        {
            // foreach (uint id in localIDs)
            //  m_log.DebugFormat("[CLIENT]: Sending KillObjectPacket to {0} for {1} in {2}", Name, id, regionHandle);

            // remove pending entities to reduce looping chances.
            m_entityProps.Remove(localIDs);
            m_entityUpdates.Remove(localIDs);

            KillObjectPacket kill = (KillObjectPacket)PacketPool.Instance.GetPacket(PacketType.KillObject);

            int perpacket = localIDs.Count;
            if(perpacket > 200)
                perpacket = 200;

            int nsent = 0;

            kill.ObjectData = new KillObjectPacket.ObjectDataBlock[perpacket];
            for (int i = 0 ; i < localIDs.Count ; i++ )
            {
                kill.ObjectData[nsent] = new KillObjectPacket.ObjectDataBlock
                {
                    ID = localIDs[i]
                };

                if (++nsent >= 200)
                {
                    OutPacket(kill, ThrottleOutPacketType.Task);
                    perpacket = localIDs.Count - i - 1;
                    if(perpacket == 0)
                        break;
                    if(perpacket > 200)
                        perpacket = 200;

                    kill = (KillObjectPacket)PacketPool.Instance.GetPacket(PacketType.KillObject);
                    kill.ObjectData = new KillObjectPacket.ObjectDataBlock[perpacket];
                    nsent = 0;
                }
            }

            if(nsent != 0)
            {
                OutPacket(kill, ThrottleOutPacketType.Task);
            }
         }

        /// <summary>
        /// Send information about the items contained in a folder to the client.
        /// </summary>
        /// <remarks>
        /// XXX This method needs some refactoring loving
        /// </remarks>
        /// <param name="ownerID">The owner of the folder</param>
        /// <param name="folderID">The id of the folder</param>
        /// <param name="items">The items contained in the folder identified by folderID</param>
        /// <param name="folders"></param>
        /// <param name="fetchFolders">Do we need to send folder information?</param>
        /// <param name="fetchItems">Do we need to send item information?</param>
        public void SendInventoryFolderDetails(UUID ownerID, UUID folderID, List<InventoryItemBase> items,
                                               List<InventoryFolderBase> folders, int version, int descendents,
                                               bool fetchFolders, bool fetchItems)
        {
            // An inventory descendents packet consists of a single agent section and an inventory details
            // section for each inventory item.  The size of each inventory item is approximately 550 bytes.
            // limit to what may fit on MTU
            int MAX_ITEMS_PER_PACKET = 5;
            int MAX_FOLDERS_PER_PACKET = 6;

            int totalItems = fetchItems ? items.Count : 0;
            int totalFolders = fetchFolders ? folders.Count : 0;
            int itemsSent = 0;
            int foldersSent = 0;
            int foldersToSend = 0;
            int itemsToSend = 0;

            InventoryDescendentsPacket currentPacket = null;

            // Handle empty folders
            //
            if (totalItems == 0 && totalFolders == 0)
                currentPacket = CreateInventoryDescendentsPacket(ownerID, folderID, version, descendents, 0, 0);

            // To preserve SL compatibility, we will NOT combine folders and items in one packet
            //
            while (itemsSent < totalItems || foldersSent < totalFolders)
            {
                if (currentPacket is null) // Start a new packet
                {
                    foldersToSend = totalFolders - foldersSent;
                    if (foldersToSend > MAX_FOLDERS_PER_PACKET)
                        foldersToSend = MAX_FOLDERS_PER_PACKET;

                    if (foldersToSend == 0)
                    {
                        itemsToSend = totalItems - itemsSent;
                        if (itemsToSend > MAX_ITEMS_PER_PACKET)
                            itemsToSend = MAX_ITEMS_PER_PACKET;
                    }

                    currentPacket = CreateInventoryDescendentsPacket(ownerID, folderID, version, descendents, foldersToSend, itemsToSend);
                }

                if (foldersToSend-- > 0)
                    currentPacket.FolderData[foldersSent % MAX_FOLDERS_PER_PACKET] = CreateFolderDataBlock(folders[foldersSent++]);
                else if (itemsToSend-- > 0)
                    currentPacket.ItemData[itemsSent % MAX_ITEMS_PER_PACKET] = CreateItemDataBlock(items[itemsSent++]);
                else
                {
                    //m_log.DebugFormat(
                    //    "[LLCLIENTVIEW]: Sending inventory folder details packet to {0} for folder {1}", Name, folderID);
                    OutPacket(currentPacket, ThrottleOutPacketType.Asset, false);
                    currentPacket = null;
                }
            }

            if (currentPacket != null)
            {
                //m_log.DebugFormat(
                //    "[LLCLIENTVIEW]: Sending inventory folder details packet to {0} for folder {1}", Name, folderID);
                OutPacket(currentPacket, ThrottleOutPacketType.Asset, false);
            }
        }

        private static InventoryDescendentsPacket.FolderDataBlock CreateFolderDataBlock(InventoryFolderBase folder)
        {
            InventoryDescendentsPacket.FolderDataBlock newBlock = new()
            {
                FolderID = folder.ID,
                Name = Util.StringToBytes256(folder.Name),
                ParentID = folder.ParentID,
                Type = (sbyte)folder.Type
            };
            //if (newBlock.Type == InventoryItemBase.SUITCASE_FOLDER_TYPE)
            //    newBlock.Type = InventoryItemBase.SUITCASE_FOLDER_FAKE_TYPE;

            return newBlock;
        }

        private static InventoryDescendentsPacket.ItemDataBlock CreateItemDataBlock(InventoryItemBase item)
        {
            InventoryDescendentsPacket.ItemDataBlock newBlock = new()
            {
                ItemID = item.ID,
                AssetID = item.AssetID,
                CreatorID = item.CreatorIdAsUuid,
                BaseMask = item.BasePermissions,
                Description = Util.StringToBytes256(item.Description),
                EveryoneMask = item.EveryOnePermissions,
                OwnerMask = item.CurrentPermissions,
                FolderID = item.Folder,
                InvType = (sbyte)item.InvType,
                Name = Util.StringToBytes256(item.Name),
                NextOwnerMask = item.NextPermissions,
                OwnerID = item.Owner,
                Type = (sbyte)item.AssetType,

                GroupID = item.GroupID,
                GroupOwned = item.GroupOwned,
                GroupMask = item.GroupPermissions,
                CreationDate = item.CreationDate,
                SalePrice = item.SalePrice,
                SaleType = item.SaleType,
                Flags = item.Flags & 0x2000ff
            };

            newBlock.CRC =
                Helpers.InventoryCRC(newBlock.CreationDate, newBlock.SaleType,
                                     newBlock.InvType, newBlock.Type,
                                     newBlock.AssetID, newBlock.GroupID,
                                     newBlock.SalePrice,
                                     newBlock.OwnerID, newBlock.CreatorID,
                                     newBlock.ItemID, newBlock.FolderID,
                                     newBlock.EveryoneMask,
                                     newBlock.Flags, newBlock.OwnerMask,
                                     newBlock.GroupMask, newBlock.NextOwnerMask);

            return newBlock;
        }

        private static void AddNullFolderBlockToDecendentsPacket(ref InventoryDescendentsPacket packet)
        {
            packet.FolderData = new InventoryDescendentsPacket.FolderDataBlock[]
            {
                new InventoryDescendentsPacket.FolderDataBlock
                {
                    FolderID = UUID.Zero,
                    ParentID = UUID.Zero,
                    Type = -1,
                    Name = Array.Empty<byte>()
                }
            };
        }

        private  static void AddNullItemBlockToDescendentsPacket(ref InventoryDescendentsPacket packet)
        {
            packet.ItemData = new InventoryDescendentsPacket.ItemDataBlock[]
            {
                new InventoryDescendentsPacket.ItemDataBlock
                {
                    ItemID = UUID.Zero,
                    AssetID = UUID.Zero,
                    CreatorID = UUID.Zero,
                    BaseMask = 0,
                    Description = Array.Empty<byte>(),
                    EveryoneMask = 0,
                    OwnerMask = 0,
                    FolderID = UUID.Zero,
                    InvType = (sbyte)0,
                    Name = Array.Empty<byte>(),
                    NextOwnerMask = 0,
                    OwnerID = UUID.Zero,
                    Type = -1,

                    GroupID = UUID.Zero,
                    GroupOwned = false,
                    GroupMask = 0,
                    CreationDate = 0,
                    SalePrice = 0,
                    SaleType = 0,
                    Flags = 0
                }
            };

            // No need to add CRC
        }

        private InventoryDescendentsPacket CreateInventoryDescendentsPacket(UUID ownerID, UUID folderID, int version, int descendents, int folders, int items)
        {
            InventoryDescendentsPacket descend = (InventoryDescendentsPacket)PacketPool.Instance.GetPacket(PacketType.InventoryDescendents);
            descend.Header.Zerocoded = true;
            descend.AgentData.AgentID = m_agentId;
            descend.AgentData.OwnerID = ownerID;
            descend.AgentData.FolderID = folderID;
            descend.AgentData.Version = version;
            descend.AgentData.Descendents = descendents;

            if (folders > 0)
                descend.FolderData = new InventoryDescendentsPacket.FolderDataBlock[folders];
            else
                AddNullFolderBlockToDecendentsPacket(ref descend);

            if (items > 0)
                descend.ItemData = new InventoryDescendentsPacket.ItemDataBlock[items];
            else
                AddNullItemBlockToDescendentsPacket(ref descend);

            return descend;
        }

        public void SendInventoryItemDetails(InventoryItemBase[] items)
        {
            // Fudge this value. It's only needed to make the CRC anyway
            const uint FULL_MASK_PERMISSIONS = (uint)0x7fffffff;

            FetchInventoryReplyPacket inventoryReply = (FetchInventoryReplyPacket)PacketPool.Instance.GetPacket(PacketType.FetchInventoryReply);
            inventoryReply.AgentData.AgentID = m_agentId;

            int total = items.Length;
            int count = 0;
            for(int i = 0; i < items.Length; ++i)
            {
                if(count == 0)
                {
                    if(total < 10)
                    {
                        inventoryReply.InventoryData = new FetchInventoryReplyPacket.InventoryDataBlock[total];
                        total = 0;
                    }
                    else
                    {
                        inventoryReply.InventoryData = new FetchInventoryReplyPacket.InventoryDataBlock[10];
                        total -= 10;
                    }
                }

                inventoryReply.InventoryData[count] = new FetchInventoryReplyPacket.InventoryDataBlock();
                FetchInventoryReplyPacket.InventoryDataBlock data = inventoryReply.InventoryData[count];

                data.ItemID = items[i].ID;
                data.AssetID = items[i].AssetID;
                data.CreatorID = items[i].CreatorIdAsUuid;
                data.BaseMask = items[i].BasePermissions;
                data.CreationDate = items[i].CreationDate;

                data.Description = Util.StringToBytes256(items[i].Description);
                data.EveryoneMask = items[i].EveryOnePermissions;
                data.FolderID = items[i].Folder;
                data.InvType = (sbyte)items[i].InvType;
                data.Name = Util.StringToBytes256(items[i].Name);
                data.NextOwnerMask = items[i].NextPermissions;
                data.OwnerID = items[i].Owner;
                data.OwnerMask = items[i].CurrentPermissions;
                data.Type = (sbyte)items[i].AssetType;

                data.GroupID = items[i].GroupID;
                data.GroupOwned = items[i].GroupOwned;
                data.GroupMask = items[i].GroupPermissions;
                data.Flags = items[i].Flags;
                data.SalePrice = items[i].SalePrice;
                data.SaleType = items[i].SaleType;

                data.CRC = Helpers.InventoryCRC(
                        1000, 0, data.InvType, data.Type, data.AssetID,
                        data.GroupID, 100, data.OwnerID, data.CreatorID,
                        data.ItemID, data.FolderID, FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS,
                        FULL_MASK_PERMISSIONS);

                ++count;
                if(count == 10 || total == 0)
                {
                    inventoryReply.Header.Zerocoded = true;
                    OutPacket(inventoryReply, ThrottleOutPacketType.Asset);
                    if(total == 0)
                        break;
                    count = 0;
                }
            }
        }

        protected void SendBulkUpdateInventoryFolder(InventoryFolderBase folderBase, UUID? transationID)
        {
            // We will use the same transaction id for all the separate packets to be sent out in this update.
            UUID transactionId = transationID ?? UUID.Random();

            List<BulkUpdateInventoryPacket.FolderDataBlock> folderDataBlocks = new();

            SendBulkUpdateInventoryFolderRecursive(folderBase, ref folderDataBlocks, transactionId);

            if (folderDataBlocks.Count > 0)
            {
                // We'll end up with some unsent folder blocks if there were some empty folders at the end of the list
                // Send these now
                BulkUpdateInventoryPacket bulkUpdate
                    = (BulkUpdateInventoryPacket)PacketPool.Instance.GetPacket(PacketType.BulkUpdateInventory);
                bulkUpdate.Header.Zerocoded = true;

                bulkUpdate.AgentData.AgentID = m_agentId;
                bulkUpdate.AgentData.TransactionID = transactionId;
                bulkUpdate.FolderData = folderDataBlocks.ToArray();
                bulkUpdate.ItemData = Array.Empty<BulkUpdateInventoryPacket.ItemDataBlock>();

                //m_log.Debug("SendBulkUpdateInventory :" + bulkUpdate);
                OutPacket(bulkUpdate, ThrottleOutPacketType.Asset);
            }
        }

        /// <summary>
        /// Recursively construct bulk update packets to send folders and items
        /// </summary>
        /// <param name="folder"></param>
        /// <param name="folderDataBlocks"></param>
        /// <param name="transactionId"></param>
        private void SendBulkUpdateInventoryFolderRecursive(
            InventoryFolderBase folder, ref List<BulkUpdateInventoryPacket.FolderDataBlock> folderDataBlocks,
            UUID transactionId)
        {
            folderDataBlocks.Add(GenerateBulkUpdateFolderDataBlock(folder));

            const int MAX_ITEMS_PER_PACKET = 5;

            IInventoryService invService = m_scene.RequestModuleInterface<IInventoryService>();
            // If there are any items then we have to start sending them off in this packet - the next folder will have
            // to be in its own bulk update packet.  Also, we can only fit 5 items in a packet (at least this was the limit
            // being used on the Linden grid at 20081203).
            InventoryCollection contents = invService.GetFolderContent(m_agentId, folder.ID); // folder.RequestListOfItems();
            List<InventoryItemBase> items = contents.Items;
            while (items.Count > 0)
            {
                BulkUpdateInventoryPacket bulkUpdate
                    = (BulkUpdateInventoryPacket)PacketPool.Instance.GetPacket(PacketType.BulkUpdateInventory);
                bulkUpdate.Header.Zerocoded = true;

                bulkUpdate.AgentData.AgentID = m_agentId;
                bulkUpdate.AgentData.TransactionID = transactionId;
                bulkUpdate.FolderData = folderDataBlocks.ToArray();

                int itemsToSend = (items.Count > MAX_ITEMS_PER_PACKET ? MAX_ITEMS_PER_PACKET : items.Count);
                bulkUpdate.ItemData = new BulkUpdateInventoryPacket.ItemDataBlock[itemsToSend];

                for (int i = 0; i < itemsToSend; i++)
                {
                    // Remove from the end of the list so that we don't incur a performance penalty
                    bulkUpdate.ItemData[i] = GenerateBulkUpdateItemDataBlock(items[^1]);
                    items.RemoveAt(items.Count - 1);
                }

                //m_log.Debug("SendBulkUpdateInventoryRecursive :" + bulkUpdate);
                OutPacket(bulkUpdate, ThrottleOutPacketType.Asset);

                folderDataBlocks = new List<BulkUpdateInventoryPacket.FolderDataBlock>();

                // If we're going to be sending another items packet then it needs to contain just the folder to which those
                // items belong.
                if (items.Count > 0)
                    folderDataBlocks.Add(GenerateBulkUpdateFolderDataBlock(folder));
            }

            List<InventoryFolderBase> subFolders = contents.Folders;
            for (int indx = 0; indx < subFolders.Count; ++indx)
            {
                SendBulkUpdateInventoryFolderRecursive(subFolders[indx], ref folderDataBlocks, transactionId);
            }
        }

        /// <summary>
        /// Generate a bulk update inventory data block for the given folder
        /// </summary>
        /// <param name="folder"></param>
        /// <returns></returns>
        private static BulkUpdateInventoryPacket.FolderDataBlock GenerateBulkUpdateFolderDataBlock(InventoryFolderBase folder)
        {
            BulkUpdateInventoryPacket.FolderDataBlock folderBlock = new()
            {
                FolderID = folder.ID,
                ParentID = folder.ParentID,
                Type = (sbyte)folder.Type,
                // Leaving this here for now, just in case we need to do this for a while
                //if (folderBlock.Type == InventoryItemBase.SUITCASE_FOLDER_TYPE)
                //    folderBlock.Type = InventoryItemBase.SUITCASE_FOLDER_FAKE_TYPE;
                Name = Util.StringToBytes256(folder.Name)
            };

            return folderBlock;
        }

        /// <summary>
        /// Generate a bulk update inventory data block for the given item
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        private static BulkUpdateInventoryPacket.ItemDataBlock GenerateBulkUpdateItemDataBlock(InventoryItemBase item)
        {
            BulkUpdateInventoryPacket.ItemDataBlock itemBlock = new()
            {
                ItemID = item.ID,
                AssetID = item.AssetID,
                CreatorID = item.CreatorIdAsUuid,
                BaseMask = item.BasePermissions,
                Description = Util.StringToBytes256(item.Description),
                EveryoneMask = item.EveryOnePermissions,
                FolderID = item.Folder,
                InvType = (sbyte)item.InvType,
                Name = Util.StringToBytes256(item.Name),
                NextOwnerMask = item.NextPermissions,
                OwnerID = item.Owner,
                OwnerMask = item.CurrentPermissions,
                Type = (sbyte)item.AssetType,
                GroupID = item.GroupID,
                GroupOwned = item.GroupOwned,
                GroupMask = item.GroupPermissions,
                Flags = item.Flags & 0x2000ff,
                SalePrice = item.SalePrice,
                SaleType = item.SaleType,
                CreationDate = item.CreationDate
            };

            itemBlock.CRC =
                Helpers.InventoryCRC(
                    1000, 0, itemBlock.InvType,
                    itemBlock.Type, itemBlock.AssetID,
                    itemBlock.GroupID, 100,
                    itemBlock.OwnerID, itemBlock.CreatorID,
                    itemBlock.ItemID, itemBlock.FolderID,
                    (uint)PermissionMask.All, 1, (uint)PermissionMask.All, (uint)PermissionMask.All,
                    (uint)PermissionMask.All);

            return itemBlock;
        }

        public void SendBulkUpdateInventory(InventoryNodeBase node, UUID? transationID = null)
        {
            if (node is InventoryItemBase itbase)
                SendBulkUpdateInventoryItem(itbase, transationID);
            else if (node is InventoryFolderBase ftbase)
                SendBulkUpdateInventoryFolder(ftbase, transationID);
            else if (node is not null)
                m_log.Error($"[CLIENT]: {Name} sent unknown inventory node named {node.Name}");
            else
                m_log.Error($"[CLIENT]: {Name} sent null inventory node");
        }

        protected void SendBulkUpdateInventoryItem(InventoryItemBase item, UUID? transationID = null)
        {
            IEventQueue eq = Scene.RequestModuleInterface<IEventQueue>();
            eq?.SendBulkUpdateInventoryItem(item, m_agentId, transationID);
        }

        public void SendInventoryItemCreateUpdate(InventoryItemBase Item, uint callbackId)
        {
            SendInventoryItemCreateUpdate(Item, UUID.Zero, callbackId);
        }

        /// <see>IClientAPI.SendInventoryItemCreateUpdate(InventoryItemBase)</see>
        public void SendInventoryItemCreateUpdate(InventoryItemBase Item, UUID transactionID, uint callbackId)
        {
            const uint FULL_MASK_PERMISSIONS = (uint)0x7fffffff;

            UpdateCreateInventoryItemPacket InventoryReply
                = (UpdateCreateInventoryItemPacket)PacketPool.Instance.GetPacket(
                                                       PacketType.UpdateCreateInventoryItem);

            // TODO: don't create new blocks if recycling an old packet
            InventoryReply.AgentData.AgentID = m_agentId;
            InventoryReply.AgentData.SimApproved = true;
            InventoryReply.AgentData.TransactionID = transactionID;
            InventoryReply.InventoryData = new UpdateCreateInventoryItemPacket.InventoryDataBlock[]
            {
                new UpdateCreateInventoryItemPacket.InventoryDataBlock
                {
                    ItemID = Item.ID,
                    AssetID = Item.AssetID,
                    CreatorID = Item.CreatorIdAsUuid,
                    BaseMask = Item.BasePermissions,
                    Description = Util.StringToBytes256(Item.Description),
                    EveryoneMask = Item.EveryOnePermissions,
                    FolderID = Item.Folder,
                    InvType = (sbyte)Item.InvType,
                    Name = Util.StringToBytes256(Item.Name),
                    NextOwnerMask = Item.NextPermissions,
                    OwnerID = Item.Owner,
                    OwnerMask = Item.CurrentPermissions,
                    Type = (sbyte)Item.AssetType,
                    CallbackID = callbackId,

                    GroupID = Item.GroupID,
                    GroupOwned = Item.GroupOwned,
                    GroupMask = Item.GroupPermissions,
                    Flags = Item.Flags & 0x2000ff,
                    SalePrice = Item.SalePrice,
                    SaleType = Item.SaleType,
                    CreationDate = Item.CreationDate
                }
            };

            InventoryReply.InventoryData[0].CRC =
                Helpers.InventoryCRC(1000, 0, InventoryReply.InventoryData[0].InvType,
                                     InventoryReply.InventoryData[0].Type, InventoryReply.InventoryData[0].AssetID,
                                     InventoryReply.InventoryData[0].GroupID, 100,
                                     InventoryReply.InventoryData[0].OwnerID, InventoryReply.InventoryData[0].CreatorID,
                                     InventoryReply.InventoryData[0].ItemID, InventoryReply.InventoryData[0].FolderID,
                                     FULL_MASK_PERMISSIONS, 1, FULL_MASK_PERMISSIONS, FULL_MASK_PERMISSIONS,
                                     FULL_MASK_PERMISSIONS);
            InventoryReply.Header.Zerocoded = true;
            OutPacket(InventoryReply, ThrottleOutPacketType.Asset);
        }

        public void SendRemoveInventoryItem(UUID itemID)
        {
            RemoveInventoryItemPacket remove = (RemoveInventoryItemPacket)PacketPool.Instance.GetPacket(PacketType.RemoveInventoryItem);
            // TODO: don't create new blocks if recycling an old packet
            remove.AgentData.AgentID = m_agentId;
            remove.AgentData.SessionID = m_sessionId;
            remove.InventoryData = new RemoveInventoryItemPacket.InventoryDataBlock[]
            {
                new RemoveInventoryItemPacket.InventoryDataBlock
                {
                    ItemID = itemID
                }
            };
            remove.Header.Zerocoded = true;
            OutPacket(remove, ThrottleOutPacketType.Asset);
        }

/*
        private uint adjustControls(int input)
        {
            uint ret = (uint)input;
            uint masked = ret & 0x0f;
            masked <<= 19;
            ret |= masked;
            return ret;
        }
*/

        public void SendTakeControls(int controls, bool passToAgent, bool TakeControls)
        {
            ScriptControlChangePacket scriptcontrol = (ScriptControlChangePacket)PacketPool.Instance.GetPacket(PacketType.ScriptControlChange);
            scriptcontrol.Data = new ScriptControlChangePacket.DataBlock[]
            {
                new ScriptControlChangePacket.DataBlock
                {
                    //ddata.Controls = adjustControls(controls);
                    Controls = (uint)controls,
                    PassToAgent = passToAgent,
                    TakeControls = TakeControls
                }
            };
            OutPacket(scriptcontrol, ThrottleOutPacketType.Task);
        }

        static private readonly byte[] ReplyTaskInventoryHeader = new byte[] {
                Helpers.MSG_RELIABLE, //| Helpers.MSG_ZEROCODED, not doing spec zeroencode on this
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 0xff, 1, 34 // ID 90 (low frequency bigendian)
                };

        public void SendTaskInventory(UUID taskID, short serial, byte[] fileName)
        {
            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            byte[] data = buf.Data;

            //setup header
            Buffer.BlockCopy(ReplyTaskInventoryHeader, 0, data, 0, 10);

            taskID.ToBytes(data, 10); // 26
            Utils.Int16ToBytes(serial, data, 26); // 28
            data[28] = (byte)fileName.Length;
            if(data[28] > 0)
                Buffer.BlockCopy(fileName, 0, data, 29, data[28]);

            buf.DataLength = 29 + data[28];
            //m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task, null, false, true);
            m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
        }


        static private readonly byte[] SendXferPacketHeader = new byte[] {
                0, //Helpers.MSG_RELIABLE, Xfer control must provide reliabialty
                0, 0, 0, 0, // sequence number
                0, // extra
                18 // ID (high frequency bigendian)
                };

        public void SendXferPacket(ulong xferID, uint packet,
                byte[] XferData, int XferDataOffset, int XferDatapktLen, bool isTaskInventory)
        {
            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            byte[] data = buf.Data;

            //setup header
            Buffer.BlockCopy(SendXferPacketHeader, 0, data, 0, 7);

            Utils.UInt64ToBytesSafepos(xferID, data, 7); // 15
            Utils.UIntToBytesSafepos(packet, data, 15); // 19

            int len = XferDatapktLen;
            if (XferDataOffset == 0) // first packet needs to send the total xfer data len
                len += 4;

            if (len > LLUDPServer.MAXPAYLOAD) // should never happen
                len = LLUDPServer.MAXPAYLOAD;
            if (len == 0)
            {
                data[19] = 0;
                data[20] = 0;
            }
            else
            {
                data[19] = (byte)len;
                data[20] = (byte)(len >> 8);
                if(XferDataOffset == 0)
                {
                    // need to send total xfer data len
                    Utils.IntToBytesSafepos(XferData.Length, data, 21);
                    if (XferDatapktLen > 0)
                        Buffer.BlockCopy(XferData, XferDataOffset, data, 25, XferDatapktLen);
                }
                else
                    Buffer.BlockCopy(XferData, XferDataOffset, data, 21, XferDatapktLen);
            }

            buf.DataLength = 21 + len;
            m_udpServer.SendUDPPacket(m_udpClient, buf, isTaskInventory ? ThrottleOutPacketType.Task : ThrottleOutPacketType.Asset);
        }

        static private readonly byte[] AbortXferHeader = new byte[] {
                Helpers.MSG_RELIABLE,
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 0xff, 0, 157 // ID 157 (low frequency bigendian)
                };

        public void SendAbortXferPacket(ulong xferID)
        {
            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            byte[] data = buf.Data;

            //setup header
            Buffer.BlockCopy(AbortXferHeader, 0, data, 0, 10);

            Utils.UInt64ToBytesSafepos(xferID, data, 10); // 18
            Utils.IntToBytesSafepos(0, data, 18); // 22  reason TODO

            buf.DataLength = 22;
            m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Asset);
        }

        public void SendEconomyData(float EnergyEfficiency, int ObjectCapacity, int ObjectCount, int PriceEnergyUnit,
                                    int PriceGroupCreate, int PriceObjectClaim, float PriceObjectRent, float PriceObjectScaleFactor,
                                    int PriceParcelClaim, float PriceParcelClaimFactor, int PriceParcelRent, int PricePublicObjectDecay,
                                    int PricePublicObjectDelete, int PriceRentLight, int PriceUpload, int TeleportMinPrice, float TeleportPriceExponent)
        {
            EconomyDataPacket economyData = (EconomyDataPacket)PacketPool.Instance.GetPacket(PacketType.EconomyData);
            economyData.Info.EnergyEfficiency = EnergyEfficiency;
            economyData.Info.ObjectCapacity = ObjectCapacity;
            economyData.Info.ObjectCount = ObjectCount;
            economyData.Info.PriceEnergyUnit = PriceEnergyUnit;
            economyData.Info.PriceGroupCreate = PriceGroupCreate;
            economyData.Info.PriceObjectClaim = PriceObjectClaim;
            economyData.Info.PriceObjectRent = PriceObjectRent;
            economyData.Info.PriceObjectScaleFactor = PriceObjectScaleFactor;
            economyData.Info.PriceParcelClaim = PriceParcelClaim;
            economyData.Info.PriceParcelClaimFactor = PriceParcelClaimFactor;
            economyData.Info.PriceParcelRent = PriceParcelRent;
            economyData.Info.PricePublicObjectDecay = PricePublicObjectDecay;
            economyData.Info.PricePublicObjectDelete = PricePublicObjectDelete;
            economyData.Info.PriceRentLight = PriceRentLight;
            economyData.Info.PriceUpload = PriceUpload;
            economyData.Info.TeleportMinPrice = TeleportMinPrice;
            economyData.Info.TeleportPriceExponent = TeleportPriceExponent;
            economyData.Header.Reliable = true;
            OutPacket(economyData, ThrottleOutPacketType.Task);
        }

        static private readonly byte[] AvatarPickerReplyHeader = new byte[] {
                Helpers.MSG_RELIABLE,
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 0xff, 0, 28 // ID 28 (low frequency bigendian)
                };

        public void SendAvatarPickerReply(UUID QueryID, List<UserData> users)
        {
            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            byte[] data = buf.Data;

            //setup header
            Buffer.BlockCopy(AvatarPickerReplyHeader, 0, data, 0, 10);
            m_agentId.ToBytes(data, 10); //26
            QueryID.ToBytes(data, 26); //42

            if (users.Count == 0)
            {
                data[42] = 0;
                buf.DataLength = 43;
                m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
                return;
            }

            int pos = 43;
            int count = 0;
            for(int u = 0; u < users.Count; ++u)
            {
                UserData user = users[u];
                user.Id.ToBytes(data,pos);
                pos+= 16;
                byte[] tmp = Utils.StringToBytes(user.FirstName);
                data[pos++] = (byte)tmp.Length;
                if(tmp.Length > 0)
                {
                    Buffer.BlockCopy(tmp, 0, data, pos, tmp.Length);
                    pos += tmp.Length;
                }
                tmp = Utils.StringToBytes(user.LastName);
                data[pos++] = (byte)tmp.Length;
                if (tmp.Length > 0)
                {
                    Buffer.BlockCopy(tmp, 0, data, pos, tmp.Length);
                    pos += tmp.Length;
                }
                ++count;

                if (pos >= LLUDPServer.MAXPAYLOAD - 120)
                {
                    data[42] = (byte)count;
                    buf.DataLength = pos;
                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
                    if (u < users.Count - 1)
                    {
                        UDPPacketBuffer newbuf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                        byte[] newdata = newbuf.Data;
                        Buffer.BlockCopy(data, 0, newdata, 0, 42);
                        buf = newbuf;
                        data = newdata;
                        pos = 43;
                    }
                    count = 0;
                }
            }
            if(count > 0)
            {
                data[42] = (byte)count;
                buf.DataLength = pos;
                m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
            }
        }

        public void SendAgentDataUpdate(UUID agentid, UUID activegroupid, string firstname, string lastname, ulong grouppowers, string groupname, string grouptitle)
        {
            if (agentid.Equals(m_agentId))
            {
                ActiveGroupId = activegroupid;
                ActiveGroupName = groupname;
                ActiveGroupPowers = grouppowers;
            }

            AgentDataUpdatePacket sendAgentDataUpdate = (AgentDataUpdatePacket)PacketPool.Instance.GetPacket(PacketType.AgentDataUpdate);
            sendAgentDataUpdate.AgentData.ActiveGroupID = activegroupid;
            sendAgentDataUpdate.AgentData.AgentID = agentid;
            sendAgentDataUpdate.AgentData.FirstName = Util.StringToBytes256(firstname);
            sendAgentDataUpdate.AgentData.GroupName = Util.StringToBytes256(groupname);
            sendAgentDataUpdate.AgentData.GroupPowers = grouppowers;
            sendAgentDataUpdate.AgentData.GroupTitle = Util.StringToBytes256(grouptitle);
            sendAgentDataUpdate.AgentData.LastName = Util.StringToBytes256(lastname);
            OutPacket(sendAgentDataUpdate, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// Send an alert message to the client. This pops up a brief duration information box at a corner
        /// </summary>
        /// <param name="message"></param>
        public void SendAlertMessage(string message)
        {
            AlertMessagePacket alertPack = (AlertMessagePacket)PacketPool.Instance.GetPacket(PacketType.AlertMessage);
            alertPack.AlertData.Message = Util.StringToBytes256(message);
            alertPack.AlertInfo = Array.Empty<AlertMessagePacket.AlertInfoBlock>();
            OutPacket(alertPack, ThrottleOutPacketType.Task);
        }

        public void SendAlertMessage(string message, string info)
        {
            AlertMessagePacket alertPack = (AlertMessagePacket)PacketPool.Instance.GetPacket(PacketType.AlertMessage);
            alertPack.AlertData.Message = Util.StringToBytes256(message);

            alertPack.AlertInfo = new AlertMessagePacket.AlertInfoBlock[]
            {
                new AlertMessagePacket.AlertInfoBlock
                {
                    Message = Util.StringToBytes256(info),
                    ExtraParams = Array.Empty<byte>()
                }
            };
            OutPacket(alertPack, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// Send an agent alert message to the client.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="modal">On the linden client, if this true then it displays a one button text box placed in the
        /// middle of the window.  If false, the message is displayed in a brief duration blue information box (as for
        /// the AlertMessage packet).</param>
        public void SendAgentAlertMessage(string message, bool modal)
        {
            // Prepend a slash to make the message come up in the top right
            // again.
            // Allow special formats to be sent from aware modules.
            if (!modal && !message.StartsWith("ALERT: ") && !message.StartsWith("NOTIFY: ") && message != "Home position set." && message != "You died and have been teleported to your home location")
                message = "/" + message;
            AgentAlertMessagePacket alertPack = (AgentAlertMessagePacket)PacketPool.Instance.GetPacket(PacketType.AgentAlertMessage);
            alertPack.AgentData.AgentID = m_agentId;
            alertPack.AlertData.Message = Util.StringToBytes256(message);
            alertPack.AlertData.Modal = modal;
            OutPacket(alertPack, ThrottleOutPacketType.Task);
        }

        public void SendLoadURL(string objectname, UUID objectID, UUID ownerID, bool groupOwned, string message,
                                string url)
        {
            LoadURLPacket loadURL = (LoadURLPacket)PacketPool.Instance.GetPacket(PacketType.LoadURL);
            loadURL.Data.ObjectName = Util.StringToBytes256(objectname);
            loadURL.Data.ObjectID = objectID;
            loadURL.Data.OwnerID = ownerID;
            loadURL.Data.OwnerIsGroup = groupOwned;
            loadURL.Data.Message = Util.StringToBytes256(message);
            loadURL.Data.URL = Util.StringToBytes256(url);
            OutPacket(loadURL, ThrottleOutPacketType.Task);
        }

        public void SendDialog(
            string objectname, UUID objectID, UUID ownerID, string ownerFirstName, string ownerLastName, string msg,
            UUID textureID, int ch, string[] buttonlabels)
        {
            ScriptDialogPacket dialog = (ScriptDialogPacket)PacketPool.Instance.GetPacket(PacketType.ScriptDialog);
            dialog.Data.ObjectID = objectID;
            dialog.Data.ObjectName = Util.StringToBytes256(objectname);
            // this is the username of the *owner*
            dialog.Data.FirstName = Util.StringToBytes256(ownerFirstName);
            dialog.Data.LastName = Util.StringToBytes256(ownerLastName);
            dialog.Data.Message = Util.StringToBytes(msg,512);
            dialog.Data.ImageID = textureID;
            dialog.Data.ChatChannel = ch;
            ScriptDialogPacket.ButtonsBlock[] buttons = new ScriptDialogPacket.ButtonsBlock[buttonlabels.Length];
            for (int i = 0; i < buttonlabels.Length; i++)
            {
                buttons[i] = new ScriptDialogPacket.ButtonsBlock
                {
                    ButtonLabel = Util.StringToBytes(buttonlabels[i], 24)
                };
            }
            dialog.Buttons = buttons;

            dialog.OwnerData = new ScriptDialogPacket.OwnerDataBlock[1];
            dialog.OwnerData[0] = new ScriptDialogPacket.OwnerDataBlock
            {
                OwnerID = ownerID
            };

            OutPacket(dialog, ThrottleOutPacketType.Task);
        }

        public void SendPreLoadSound(UUID objectID, UUID ownerID, UUID soundID)
        {
            PreloadSoundPacket preSound = (PreloadSoundPacket)PacketPool.Instance.GetPacket(PacketType.PreloadSound);
            // TODO: don't create new blocks if recycling an old packet
            preSound.DataBlock = new PreloadSoundPacket.DataBlockBlock[1];
            preSound.DataBlock[0] = new PreloadSoundPacket.DataBlockBlock
            {
                ObjectID = objectID,
                OwnerID = ownerID,
                SoundID = soundID
            };
            preSound.Header.Zerocoded = true;
            OutPacket(preSound, ThrottleOutPacketType.Task);
        }

        public void SendPlayAttachedSound(UUID soundID, UUID objectID, UUID ownerID, float gain, byte flags)
        {
            AttachedSoundPacket sound = (AttachedSoundPacket)PacketPool.Instance.GetPacket(PacketType.AttachedSound);
            sound.DataBlock.SoundID = soundID;
            sound.DataBlock.ObjectID = objectID;
            sound.DataBlock.OwnerID = ownerID;
            sound.DataBlock.Gain = gain;
            sound.DataBlock.Flags = flags;

            OutPacket(sound, ThrottleOutPacketType.Task);
        }

        public void SendTransferAbort(TransferRequestPacket transferRequest)
        {
            TransferAbortPacket abort = (TransferAbortPacket)PacketPool.Instance.GetPacket(PacketType.TransferAbort);
            abort.TransferInfo.TransferID = transferRequest.TransferInfo.TransferID;
            abort.TransferInfo.ChannelType = transferRequest.TransferInfo.ChannelType;
            m_log.Debug("[Assets] Aborting transfer; asset request failed");
            OutPacket(abort, ThrottleOutPacketType.Task);
        }

        public void SendTriggeredSound(UUID soundID, UUID ownerID, UUID objectID, UUID parentID, ulong handle, Vector3 position, float gain)
        {
            SoundTriggerPacket sound = (SoundTriggerPacket)PacketPool.Instance.GetPacket(PacketType.SoundTrigger);
            sound.SoundData.SoundID = soundID;
            sound.SoundData.OwnerID = ownerID;
            sound.SoundData.ObjectID = objectID;
            sound.SoundData.ParentID = parentID;
            sound.SoundData.Handle = handle;
            sound.SoundData.Position = position;
            sound.SoundData.Gain = gain;

            OutPacket(sound, ThrottleOutPacketType.Task);
        }

        public void SendAttachedSoundGainChange(UUID objectID, float gain)
        {
            AttachedSoundGainChangePacket sound = (AttachedSoundGainChangePacket)PacketPool.Instance.GetPacket(PacketType.AttachedSoundGainChange);
            sound.DataBlock.ObjectID = objectID;
            sound.DataBlock.Gain = gain;

            OutPacket(sound, ThrottleOutPacketType.Task);
        }

        public void SendViewerTime(Vector3 sunDir, float sunphase)
        {
            SimulatorViewerTimeMessagePacket viewertime = (SimulatorViewerTimeMessagePacket)PacketPool.Instance.GetPacket(PacketType.SimulatorViewerTimeMessage);

            viewertime.TimeInfo.UsecSinceStart = Util.UnixTimeSinceEpoch_uS();
            viewertime.TimeInfo.SunDirection = sunDir;
            viewertime.TimeInfo.SunPhase = sunphase;

            viewertime.TimeInfo.SunAngVelocity = Vector3.Zero; //legacy
            viewertime.TimeInfo.SecPerDay = 14400; // legacy
            viewertime.TimeInfo.SecPerYear = 158400; // legacy

            viewertime.Header.Reliable = false;
            viewertime.Header.Zerocoded = true;
            OutPacket(viewertime, ThrottleOutPacketType.Task);
        }



        public void SendViewerEffect(ViewerEffectPacket.EffectBlock[] effectBlocks)
        {
            ViewerEffectPacket packet = (ViewerEffectPacket)PacketPool.Instance.GetPacket(PacketType.ViewerEffect);

//            packet.AgentData.AgentID = m_agentId;
//            packet.AgentData.SessionID = SessionId;

            packet.Effect = effectBlocks;

            // OutPacket(packet, ThrottleOutPacketType.State);
            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendAvatarProperties(UUID avatarID, string aboutText, string bornOn, Byte[] membershipType,
                                         string flAbout, uint flags, UUID flImageID, UUID imageID, string profileURL,
                                         UUID partnerID)
        {
            AvatarPropertiesReplyPacket avatarReply = (AvatarPropertiesReplyPacket)PacketPool.Instance.GetPacket(PacketType.AvatarPropertiesReply);
            avatarReply.AgentData.AgentID = m_agentId;
            avatarReply.AgentData.AvatarID = avatarID;
            if (aboutText != null)
                avatarReply.PropertiesData.AboutText = Util.StringToBytes1024(aboutText);
            else
                avatarReply.PropertiesData.AboutText = Utils.EmptyBytes;
            avatarReply.PropertiesData.BornOn = Util.StringToBytes256(bornOn);
            avatarReply.PropertiesData.CharterMember = membershipType;
            if (flAbout != null)
                avatarReply.PropertiesData.FLAboutText = Util.StringToBytes256(flAbout);
            else
                avatarReply.PropertiesData.FLAboutText = Utils.EmptyBytes;
            avatarReply.PropertiesData.Flags = flags;
            avatarReply.PropertiesData.FLImageID = flImageID;
            avatarReply.PropertiesData.ImageID = imageID;
            avatarReply.PropertiesData.ProfileURL = Util.StringToBytes256(profileURL);
            avatarReply.PropertiesData.PartnerID = partnerID;
            OutPacket(avatarReply, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// Send the client an Estate message blue box pop-down with a single OK button
        /// </summary>
        /// <param name="FromAvatarID"></param>
        /// <param name="fromSessionID"></param>
        /// <param name="FromAvatarName"></param>
        /// <param name="Message"></param>
        public void SendBlueBoxMessage(UUID FromAvatarID, String FromAvatarName, String Message)
        {
            if (!SceneAgent.IsChildAgent)
                SendInstantMessage(new GridInstantMessage(null, FromAvatarID, FromAvatarName, m_agentId, 1, Message, false, new Vector3()));

            //SendInstantMessage(FromAvatarID, fromSessionID, Message, m_agentId, SessionId, FromAvatarName, (byte)21,(uint) Util.UnixTimeSinceEpoch());
        }

        public void SendLogoutPacket()
        {
            // I know this is a bit of a hack, however there are times when you don't
            // want to send this, but still need to do the rest of the shutdown process
            // this method gets called from the packet server..   which makes it practically
            // impossible to do any other way.

            if (m_SendLogoutPacketWhenClosing)
            {
                LogoutReplyPacket logReply = (LogoutReplyPacket)PacketPool.Instance.GetPacket(PacketType.LogoutReply);
                // TODO: don't create new blocks if recycling an old packet
                logReply.AgentData.AgentID = m_agentId;
                logReply.AgentData.SessionID = m_sessionId;
                logReply.InventoryData = new LogoutReplyPacket.InventoryDataBlock[1];
                logReply.InventoryData[0] = new LogoutReplyPacket.InventoryDataBlock
                {
                    ItemID = UUID.Zero
                };

                OutPacket(logReply, ThrottleOutPacketType.Task);
            }
        }

        public void SendHealth(float health)
        {
            HealthMessagePacket healthpacket = (HealthMessagePacket)PacketPool.Instance.GetPacket(PacketType.HealthMessage);
            healthpacket.HealthData.Health = health;
            OutPacket(healthpacket, ThrottleOutPacketType.Task);
        }

        public void SendAgentOnline(UUID[] agentIDs)
        {
            OnlineNotificationPacket.AgentBlockBlock[] onpb = new OnlineNotificationPacket.AgentBlockBlock[agentIDs.Length];
            for (int i = 0; i < agentIDs.Length; i++)
            {
                onpb[i] = new OnlineNotificationPacket.AgentBlockBlock
                {
                    AgentID = agentIDs[i]
                };
            }
            OnlineNotificationPacket onp = new()
            {
                AgentBlock = onpb
            };
            onp.Header.Reliable = true;
            OutPacket(onp, ThrottleOutPacketType.Task);
        }

        public void SendAgentOffline(UUID[] agentIDs)
        {
            OfflineNotificationPacket offp = new();
            OfflineNotificationPacket.AgentBlockBlock[] offpb = new OfflineNotificationPacket.AgentBlockBlock[agentIDs.Length];
            for (int i = 0; i < agentIDs.Length; i++)
            {
                offpb[i] = new OfflineNotificationPacket.AgentBlockBlock
                {
                    AgentID = agentIDs[i]
                };
            }
            offp.AgentBlock = offpb;
            offp.Header.Reliable = true;
            OutPacket(offp, ThrottleOutPacketType.Task);
        }

        public void SendFindAgent(UUID HunterID, UUID PreyID, double GlobalX, double GlobalY)
        {
            FindAgentPacket fap = new();
            fap.AgentBlock.Hunter = HunterID;
            fap.AgentBlock.Prey = PreyID;
            fap.AgentBlock.SpaceIP = 0;
            fap.LocationBlock = new FindAgentPacket.LocationBlockBlock[]
            {
                new FindAgentPacket.LocationBlockBlock
                {
                    GlobalX = GlobalX,
                    GlobalY = GlobalY
                }
            };

            OutPacket(fap, ThrottleOutPacketType.Task);
         }

        public void SendSitResponse(UUID TargetID, Vector3 OffsetPos,
                Quaternion SitOrientation, bool autopilot,
                Vector3 CameraAtOffset, Vector3 CameraEyeOffset, bool ForceMouseLook)
        {
            AvatarSitResponsePacket avatarSitResponse = new();
            avatarSitResponse.SitObject.ID = TargetID;
            avatarSitResponse.SitTransform.CameraAtOffset = CameraAtOffset;
            avatarSitResponse.SitTransform.CameraEyeOffset = CameraEyeOffset;
            avatarSitResponse.SitTransform.ForceMouselook = ForceMouseLook;
            avatarSitResponse.SitTransform.AutoPilot = autopilot;
            avatarSitResponse.SitTransform.SitPosition = OffsetPos;
            avatarSitResponse.SitTransform.SitRotation = SitOrientation;

            OutPacket(avatarSitResponse, ThrottleOutPacketType.Task);
        }

        public void SendAdminResponse(UUID Token, uint AdminLevel)
        {
            GrantGodlikePowersPacket respondPacket = new();
            respondPacket.AgentData.AgentID = m_agentId;
            respondPacket.AgentData.SessionID = m_sessionId;

            respondPacket.GrantData.GodLevel = (byte)AdminLevel;
            respondPacket.GrantData.Token = Token;

            OutPacket(respondPacket, ThrottleOutPacketType.Task);
        }

        public void SendGroupMembership(GroupMembershipData[] GroupMembership)
        {

            UpdateGroupMembership(GroupMembership);
            SendAgentGroupDataUpdate(m_agentId, GroupMembership);
        }

        public void SendSelectedPartsProprieties(List<ISceneEntity> parts)
        {
            /* not in use
            // udp part
            ObjectPropertiesPacket packet =
                (ObjectPropertiesPacket)PacketPool.Instance.GetPacket(PacketType.ObjectProperties);
            ObjectPropertiesPacket.ObjectDataBlock[] ObjectData = new ObjectPropertiesPacket.ObjectDataBlock[parts.Count];

            int i = 0;
            foreach(SceneObjectPart sop in parts)
                ObjectData[i++] = CreateObjectPropertiesBlock(sop);

            packet.ObjectData = ObjectData;
            packet.Header.Zerocoded = true;
            // udp send splits this mega packets correctly
            // mb later will avoid that to reduce gc stress
            OutPacket(packet, ThrottleOutPacketType.Task, true);

            // caps physics part
            IEventQueue eq = Scene.RequestModuleInterface<IEventQueue>();
            if(eq is null)
                return;

            OSDArray array = new OSDArray();
            foreach(SceneObjectPart sop in parts)
            {
                OSDMap physinfo = new OSDMap(6);
                physinfo["LocalID"] = sop.LocalId;
                physinfo["Density"] = sop.Density;
                physinfo["Friction"] = sop.Friction;
                physinfo["GravityMultiplier"] = sop.GravityModifier;
                physinfo["Restitution"] = sop.Restitution;
                physinfo["PhysicsShapeType"] = (int)sop.PhysicsShapeType;
                array.Add(physinfo);
            }

            OSDMap llsdBody = new OSDMap(1);
            llsdBody.Add("ObjectData", array);

            eq.Enqueue(BuildEvent("ObjectPhysicsProperties", llsdBody),m_agentId);
            */
        }


        public void SendPartPhysicsProprieties(ISceneEntity entity)
        {
            IEventQueue eq = Scene.RequestModuleInterface<IEventQueue>();
            if (eq is null)
                return;

            if (entity is not SceneObjectPart part)
                return;

            uint localid = part.LocalId;
            byte physshapetype = part.PhysicsShapeType;
            float density = part.Density;
            float friction = part.Friction;
            float bounce = part.Restitution;
            float gravmod = part.GravityModifier;

            eq.partPhysicsProperties(localid, physshapetype, density, friction, bounce, gravmod, m_agentId);
        }

        public void SendGroupNameReply(UUID groupLLUID, string GroupName)
        {
            UUIDGroupNameReplyPacket pack = new()
            {
                UUIDNameBlock = new UUIDGroupNameReplyPacket.UUIDNameBlockBlock[]
                {
                    new UUIDGroupNameReplyPacket.UUIDNameBlockBlock()
                    {
                        ID = groupLLUID,
                        GroupName = Util.StringToBytes256(GroupName)
                    }
                }
            };
            OutPacket(pack, ThrottleOutPacketType.Task);
        }

        public void SendLandStatReply(uint reportType, uint requestFlags, uint resultCount, LandStatReportItem[] lsrpia)
        {
            IEventQueue eq = Scene.RequestModuleInterface<IEventQueue>();
            if (eq is null)
            {
                LandStatReplyPacket.ReportDataBlock[] lsrepdba = new LandStatReplyPacket.ReportDataBlock[lsrpia.Length];
                for (int i = 0; i < lsrpia.Length; i++)
                {
                    lsrepdba[i] = new LandStatReplyPacket.ReportDataBlock
                    {
                        LocationX = lsrpia[i].LocationX,
                        LocationY = lsrpia[i].LocationY,
                        LocationZ = lsrpia[i].LocationZ,
                        Score = lsrpia[i].Score,
                        TaskID = lsrpia[i].TaskID,
                        TaskLocalID = lsrpia[i].TaskLocalID,
                        TaskName = Util.StringToBytes256(lsrpia[i].TaskName),
                        OwnerName = Util.StringToBytes256(lsrpia[i].OwnerName)
                    };
                }
                LandStatReplyPacket lsrp = new();
                lsrp.RequestData.ReportType = reportType;
                lsrp.RequestData.RequestFlags = requestFlags;
                lsrp.RequestData.TotalObjectCount = resultCount;
                lsrp.ReportData = lsrepdba;

                OutPacket(lsrp, ThrottleOutPacketType.Task);
            }
            else
            {
                osUTF8 sb = eq.StartEvent("LandStatReply");

                LLSDxmlEncode2.AddArrayAndMap("RequestData", sb);
                LLSDxmlEncode2.AddElem("ReportType", reportType, sb);
                LLSDxmlEncode2.AddElem("RequestFlags", requestFlags, sb);
                LLSDxmlEncode2.AddElem("TotalObjectCount", (uint)lsrpia.Length, sb);
                LLSDxmlEncode2.AddEndMapAndArray(sb);

                if (lsrpia.Length > 0)
                {
                    LLSDxmlEncode2.AddArray("ReportData", sb);

                    foreach (var item in lsrpia)
                    {
                        LLSDxmlEncode2.AddMap(sb);
                        LLSDxmlEncode2.AddElem("LocationX", item.LocationX, sb);
                        LLSDxmlEncode2.AddElem("LocationY", item.LocationY, sb);
                        LLSDxmlEncode2.AddElem("LocationZ", item.LocationZ, sb);
                        LLSDxmlEncode2.AddElem("OwnerName", item.OwnerName, sb);
                        LLSDxmlEncode2.AddElem("Score", item.Score, sb);
                        LLSDxmlEncode2.AddElem("TaskID", item.TaskID, sb);
                        LLSDxmlEncode2.AddElem("TaskLocalID", item.TaskLocalID, sb);
                        LLSDxmlEncode2.AddElem("TaskName", item.TaskName, sb);
                        LLSDxmlEncode2.AddEndMap(sb);
                    }

                    LLSDxmlEncode2.AddEndArray(sb);

                    LLSDxmlEncode2.AddArray("DataExtended", sb);

                    foreach (var item in lsrpia)
                    {
                        LLSDxmlEncode2.AddMap(sb);
                        LLSDxmlEncode2.AddElem("MonoScore", 0.0f, sb);
                        LLSDxmlEncode2.AddElem("OwnerID", item.OwnerID, sb);
                        LLSDxmlEncode2.AddElem("ParcelName", item.Parcel, sb);
                        LLSDxmlEncode2.AddElem("PublicURLs", item.Urls, sb);
                        LLSDxmlEncode2.AddElem("Size", (float)item.Bytes, sb);
                        LLSDxmlEncode2.AddElem("TimeStamp", item.Time, sb);
                        LLSDxmlEncode2.AddEndMap(sb);
                    }

                    LLSDxmlEncode2.AddEndArray(sb);
                }

                eq.Enqueue(eq.EndEventToBytes(sb), m_agentId);
            }
        }

        public void SendScriptRunningReply(UUID objectID, UUID itemID, bool running)
        {
            IEventQueue eq = Scene.RequestModuleInterface<IEventQueue>();
            if (eq is null)
            {
                ScriptRunningReplyPacket scriptRunningReply = new();
                scriptRunningReply.Script.ObjectID = objectID;
                scriptRunningReply.Script.ItemID = itemID;
                scriptRunningReply.Script.Running = running;
                OutPacket(scriptRunningReply, ThrottleOutPacketType.Task);
            }
            else
            {
                eq.ScriptRunningEvent(objectID, itemID, running, m_agentId);
            }
        }

        public void SendAsset(AssetRequestToClient req)
        {
            if (req.AssetInf is null)
            {
                m_log.Error($"{LogHeader} Cannot send asset, because it is null");
                return;
            }

            if (req.AssetInf.Data is null)
            {
                m_log.Error($"{LogHeader} Cannot send asset {req.AssetInf.ID} ({req.AssetInf.Metadata.ContentType}), asset data is null");
                return;
            }

            TransferInfoPacket Transfer = new();
            Transfer.TransferInfo.ChannelType = 2;
            Transfer.TransferInfo.Status = 0;
            Transfer.TransferInfo.TargetType = 0;
            if (req.AssetRequestSource == 2)
            {
                Transfer.TransferInfo.Params = new byte[20];
                Array.Copy(req.RequestAssetID.GetBytes(), 0, Transfer.TransferInfo.Params, 0, 16);
                int assType = req.AssetInf.Type;
                Array.Copy(Utils.IntToBytes(assType), 0, Transfer.TransferInfo.Params, 16, 4);
            }
            else if (req.AssetRequestSource == 3)
            {
                Transfer.TransferInfo.Params = req.Params;
            }
            Transfer.TransferInfo.Size = req.AssetInf.Data.Length;
            Transfer.TransferInfo.TransferID = req.TransferRequestID;
            Transfer.Header.Zerocoded = true;
            OutPacket(Transfer, ThrottleOutPacketType.Asset);

            if (req.NumPackets == 1)
            {
                TransferPacketPacket TransferPacket = new();
                TransferPacket.TransferData.Packet = 0;
                TransferPacket.TransferData.ChannelType = 2;
                TransferPacket.TransferData.TransferID = req.TransferRequestID;
                TransferPacket.TransferData.Data = req.AssetInf.Data;
                TransferPacket.TransferData.Status = 1;
                TransferPacket.Header.Zerocoded = true;
                OutPacket(TransferPacket, ThrottleOutPacketType.Asset);
            }
            else
            {
                int processedLength = 0;
//                int maxChunkSize = Settings.MAX_PACKET_SIZE - 100;

                int maxChunkSize = (int) MaxTransferBytesPerPacket;
                int packetNumber = 0;

                while (processedLength < req.AssetInf.Data.Length)
                {
                    TransferPacketPacket TransferPacket = new();
                    TransferPacket.TransferData.Packet = packetNumber;
                    TransferPacket.TransferData.ChannelType = 2;
                    TransferPacket.TransferData.TransferID = req.TransferRequestID;

                    int chunkSize = Math.Min(req.AssetInf.Data.Length - processedLength, maxChunkSize);
                    byte[] chunk = new byte[chunkSize];
                    Array.Copy(req.AssetInf.Data, processedLength, chunk, 0, chunk.Length);

                    TransferPacket.TransferData.Data = chunk;

                    // 0 indicates more packets to come, 1 indicates last packet
                    if (req.AssetInf.Data.Length - processedLength > maxChunkSize)
                    {
                        TransferPacket.TransferData.Status = 0;
                    }
                    else
                    {
                        TransferPacket.TransferData.Status = 1;
                    }
                    TransferPacket.Header.Zerocoded = true;
                    OutPacket(TransferPacket, ThrottleOutPacketType.Asset);

                    processedLength += chunkSize;
                    packetNumber++;
                }
            }
        }

        public void SendAssetNotFound(AssetRequestToClient req)
        {
            TransferInfoPacket Transfer = new();
            Transfer.TransferInfo.ChannelType = 2;
            Transfer.TransferInfo.Status = -2;
            Transfer.TransferInfo.TargetType = 0;
            Transfer.TransferInfo.Params = req.Params;
            Transfer.TransferInfo.Size = 0;
            Transfer.TransferInfo.TransferID = req.TransferRequestID;
            Transfer.Header.Zerocoded = true;
            OutPacket(Transfer, ThrottleOutPacketType.Asset);
        }

        public void SendTexture(AssetBase TextureAsset)
        {

        }

        public void SendRegionHandle(UUID regionID, ulong handle)
        {
            RegionIDAndHandleReplyPacket reply = (RegionIDAndHandleReplyPacket)PacketPool.Instance.GetPacket(PacketType.RegionIDAndHandleReply);
            reply.ReplyBlock.RegionID = regionID;
            reply.ReplyBlock.RegionHandle = handle;
            OutPacket(reply, ThrottleOutPacketType.Land);
        }

        public void SendParcelInfo(RegionInfo info, LandData land, UUID parcelID, uint x, uint y)
        {
            ParcelInfoReplyPacket reply = (ParcelInfoReplyPacket)PacketPool.Instance.GetPacket(PacketType.ParcelInfoReply);
            reply.AgentData.AgentID = m_agentId;
            reply.Data.ParcelID = parcelID;
            reply.Data.OwnerID = land.OwnerID;
            reply.Data.Name = Utils.StringToBytes(land.Name);
            if (!string.IsNullOrEmpty(land.Description))
            {
                if (land.Description.Length > 254)
                    reply.Data.Desc = Utils.StringToBytes(land.Description[..254]);
                else
                    reply.Data.Desc = Utils.StringToBytes(land.Description);
            }
            else
                reply.Data.Desc = Array.Empty<byte>();
            reply.Data.ActualArea = land.Area;
            reply.Data.BillableArea = land.Area; // TODO: what is this?

            reply.Data.Flags = (byte)Util.ConvertAccessLevelToMaturity((byte)info.AccessLevel);
            if((land.Flags & (uint)ParcelFlags.ForSale) != 0)
                reply.Data.Flags |= (1 << 7);

            if (land.IsGroupOwned)
                reply.Data.Flags |= 0x04;

            Vector3 pos = land.UserLocation;
            if (pos.IsZero())
            {
                pos = (land.AABBMax + land.AABBMin) * 0.5f;
            }
            reply.Data.GlobalX = info.RegionLocX + x;
            reply.Data.GlobalY = info.RegionLocY + y;
            reply.Data.GlobalZ = pos.Z;
            reply.Data.SimName = Utils.StringToBytes(info.RegionName);
            reply.Data.SnapshotID = land.SnapshotID;
            reply.Data.Dwell = land.Dwell;
            reply.Data.SalePrice = land.SalePrice;
            reply.Data.AuctionID = (int)land.AuctionID;

            OutPacket(reply, ThrottleOutPacketType.Land);
        }

        public void SendScriptTeleportRequest(string objName, string simName, Vector3 pos, Vector3 lookAt)
        {
            ScriptTeleportRequestPacket packet = (ScriptTeleportRequestPacket)PacketPool.Instance.GetPacket(PacketType.ScriptTeleportRequest);

            packet.Data.ObjectName = Utils.StringToBytes(objName);
            packet.Data.SimName = Utils.StringToBytes(simName);
            packet.Data.SimPosition = pos;
            packet.Data.LookAt = lookAt;

            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendDirPlacesReply(UUID queryID, DirPlacesReplyData[] data)
        {
            DirPlacesReplyPacket packet = (DirPlacesReplyPacket)PacketPool.Instance.GetPacket(PacketType.DirPlacesReply);

            packet.AgentData = new DirPlacesReplyPacket.AgentDataBlock();

            packet.QueryData = new DirPlacesReplyPacket.QueryDataBlock[1];
            packet.QueryData[0] = new DirPlacesReplyPacket.QueryDataBlock();

            packet.AgentData.AgentID = m_agentId;

            packet.QueryData[0].QueryID = queryID;

            DirPlacesReplyPacket.QueryRepliesBlock[] replies = Array.Empty<DirPlacesReplyPacket.QueryRepliesBlock>();
            DirPlacesReplyPacket.StatusDataBlock[] status = Array.Empty<DirPlacesReplyPacket.StatusDataBlock>();

            packet.QueryReplies = replies;
            packet.StatusData = status;

            foreach (DirPlacesReplyData d in data)
            {
                int idx = replies.Length;
                Array.Resize(ref replies, idx + 1);
                Array.Resize(ref status, idx + 1);

                replies[idx] = new DirPlacesReplyPacket.QueryRepliesBlock();
                status[idx] = new DirPlacesReplyPacket.StatusDataBlock();
                replies[idx].ParcelID = d.parcelID;
                replies[idx].Name = Utils.StringToBytes(d.name);
                replies[idx].ForSale = d.forSale;
                replies[idx].Auction = d.auction;
                replies[idx].Dwell = d.dwell;
                status[idx].Status = d.Status;

                packet.QueryReplies = replies;
                packet.StatusData = status;

                if (packet.Length >= 1000)
                {
                    OutPacket(packet, ThrottleOutPacketType.Task);

                    packet = (DirPlacesReplyPacket)PacketPool.Instance.GetPacket(PacketType.DirPlacesReply);

                    packet.AgentData = new DirPlacesReplyPacket.AgentDataBlock();

                    packet.QueryData = new DirPlacesReplyPacket.QueryDataBlock[1];
                    packet.QueryData[0] = new DirPlacesReplyPacket.QueryDataBlock();

                    packet.AgentData.AgentID = m_agentId;

                    packet.QueryData[0].QueryID = queryID;

                    replies = Array.Empty<DirPlacesReplyPacket.QueryRepliesBlock>();
                    status = Array.Empty<DirPlacesReplyPacket.StatusDataBlock>();
                }
            }

            if (replies.Length > 0 || data.Length == 0)
                OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendDirPeopleReply(UUID queryID, DirPeopleReplyData[] data)
        {
            DirPeopleReplyPacket packet = (DirPeopleReplyPacket)PacketPool.Instance.GetPacket(PacketType.DirPeopleReply);

            packet.AgentData = new DirPeopleReplyPacket.AgentDataBlock
            {
                AgentID = m_agentId
            };

            packet.QueryData = new DirPeopleReplyPacket.QueryDataBlock
            {
                QueryID = queryID
            };

            packet.QueryReplies = new DirPeopleReplyPacket.QueryRepliesBlock[data.Length];

            int i = 0;
            foreach (DirPeopleReplyData d in data)
            {
                packet.QueryReplies[i] = new DirPeopleReplyPacket.QueryRepliesBlock
                {
                    AgentID = d.agentID,
                    FirstName = Utils.StringToBytes(d.firstName),
                    LastName =  Utils.StringToBytes(d.lastName),
                    Group =     Utils.StringToBytes(d.group),
                    Online = d.online,
                    Reputation = d.reputation
                };
                i++;
            }

            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendDirEventsReply(UUID queryID, DirEventsReplyData[] data)
        {
            DirEventsReplyPacket packet = (DirEventsReplyPacket)PacketPool.Instance.GetPacket(PacketType.DirEventsReply);

            packet.AgentData = new DirEventsReplyPacket.AgentDataBlock { AgentID = m_agentId };

            packet.QueryData = new DirEventsReplyPacket.QueryDataBlock { QueryID = queryID };

            packet.QueryReplies = new DirEventsReplyPacket.QueryRepliesBlock[data.Length];
            packet.StatusData = new DirEventsReplyPacket.StatusDataBlock[data.Length];

            int i = 0;
            foreach (DirEventsReplyData d in data)
            {
                packet.QueryReplies[i] = new DirEventsReplyPacket.QueryRepliesBlock();
                packet.StatusData[i] = new DirEventsReplyPacket.StatusDataBlock();
                packet.QueryReplies[i].OwnerID = d.ownerID;
                packet.QueryReplies[i].Name = Utils.StringToBytes(d.name);
                packet.QueryReplies[i].EventID = d.eventID;
                packet.QueryReplies[i].Date = Utils.StringToBytes(d.date);
                packet.QueryReplies[i].UnixTime = d.unixTime;
                packet.QueryReplies[i].EventFlags = d.eventFlags;
                packet.StatusData[i].Status = d.Status;
                i++;
            }

            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendDirGroupsReply(UUID queryID, DirGroupsReplyData[] data)
        {
            DirGroupsReplyPacket packet = (DirGroupsReplyPacket)PacketPool.Instance.GetPacket(PacketType.DirGroupsReply);

            packet.AgentData = new DirGroupsReplyPacket.AgentDataBlock { AgentID = m_agentId };
            packet.QueryData = new DirGroupsReplyPacket.QueryDataBlock { QueryID = queryID };
            packet.QueryReplies = new DirGroupsReplyPacket.QueryRepliesBlock[data.Length];

            int i = 0;
            foreach (DirGroupsReplyData d in data)
            {
                packet.QueryReplies[i++] = new DirGroupsReplyPacket.QueryRepliesBlock
                {
                    GroupID = d.groupID,
                    GroupName = Util.StringToBytes(d.groupName, 35),
                    Members = d.members,
                    SearchOrder = d.searchOrder
                };
            }

            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendDirClassifiedReply(UUID queryID, DirClassifiedReplyData[] data)
        {
            DirClassifiedReplyPacket packet = (DirClassifiedReplyPacket)PacketPool.Instance.GetPacket(PacketType.DirClassifiedReply);

            packet.AgentData = new DirClassifiedReplyPacket.AgentDataBlock { AgentID = m_agentId };
            packet.QueryData = new DirClassifiedReplyPacket.QueryDataBlock { QueryID = queryID };

            packet.QueryReplies = new DirClassifiedReplyPacket.QueryRepliesBlock[data.Length];
            packet.StatusData = new DirClassifiedReplyPacket.StatusDataBlock[data.Length];

            int i = 0;
            foreach (DirClassifiedReplyData d in data)
            {
                packet.QueryReplies[i] = new DirClassifiedReplyPacket.QueryRepliesBlock();
                packet.StatusData[i] = new DirClassifiedReplyPacket.StatusDataBlock();
                packet.QueryReplies[i].ClassifiedID = d.classifiedID;
                packet.QueryReplies[i].Name = Utils.StringToBytes(d.name);
                packet.QueryReplies[i].ClassifiedFlags = d.classifiedFlags;
                packet.QueryReplies[i].CreationDate = d.creationDate;
                packet.QueryReplies[i].ExpirationDate = d.expirationDate;
                packet.QueryReplies[i].PriceForListing = d.price;
                packet.StatusData[i].Status = d.Status;
                i++;
            }

            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendDirLandReply(UUID queryID, DirLandReplyData[] data)
        {
            DirLandReplyPacket packet = (DirLandReplyPacket)PacketPool.Instance.GetPacket(PacketType.DirLandReply);

            packet.AgentData = new DirLandReplyPacket.AgentDataBlock { AgentID = m_agentId };
            packet.QueryData = new DirLandReplyPacket.QueryDataBlock { QueryID = queryID };
            packet.QueryReplies = new DirLandReplyPacket.QueryRepliesBlock[data.Length];

            int i = 0;
            foreach (DirLandReplyData d in data)
            {
                packet.QueryReplies[i++] = new DirLandReplyPacket.QueryRepliesBlock
                {
                    ParcelID = d.parcelID,
                    Name = Utils.StringToBytes(d.name),
                    Auction = d.auction,
                    ForSale = d.forSale,
                    SalePrice = d.salePrice,
                    ActualArea = d.actualArea
                };
            }

            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendDirPopularReply(UUID queryID, DirPopularReplyData[] data)
        {
            DirPopularReplyPacket packet = (DirPopularReplyPacket)PacketPool.Instance.GetPacket(PacketType.DirPopularReply);

            packet.AgentData = new DirPopularReplyPacket.AgentDataBlock { AgentID = m_agentId };
            packet.QueryData = new DirPopularReplyPacket.QueryDataBlock{ QueryID = queryID };

            packet.QueryReplies = new DirPopularReplyPacket.QueryRepliesBlock[data.Length];

            int i = 0;
            foreach (DirPopularReplyData d in data)
            {
                packet.QueryReplies[i++] = new DirPopularReplyPacket.QueryRepliesBlock
                {
                    ParcelID = d.parcelID,
                    Name = Utils.StringToBytes(d.name),
                    Dwell = d.dwell
                };
            }

            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendEventInfoReply(EventData data)
        {
            EventInfoReplyPacket packet = (EventInfoReplyPacket)PacketPool.Instance.GetPacket(PacketType.EventInfoReply);

            packet.AgentData = new EventInfoReplyPacket.AgentDataBlock { AgentID = m_agentId };

            packet.EventData = new EventInfoReplyPacket.EventDataBlock
            {
                EventID = data.eventID,
                Creator = Utils.StringToBytes(data.creator),
                Name = Utils.StringToBytes(data.name),
                Category = Utils.StringToBytes(data.category),
                Desc = Utils.StringToBytes(data.description),
                Date = Utils.StringToBytes(data.date),
                DateUTC = data.dateUTC,
                Duration = data.duration,
                Cover = data.cover,
                Amount = data.amount,
                SimName = Utils.StringToBytes(data.simName),
                GlobalPos = new Vector3d(data.globalPos),
                EventFlags = data.eventFlags
            };

            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendOfferCallingCard(UUID srcID, UUID transactionID)
        {
            // a bit special, as this uses AgentID to store the source instead
            // of the destination. The destination (the receiver) goes into destID
            OfferCallingCardPacket p = (OfferCallingCardPacket)PacketPool.Instance.GetPacket(PacketType.OfferCallingCard);
            p.AgentData.AgentID = srcID;
            p.AgentData.SessionID = UUID.Zero;
            p.AgentBlock.DestID = m_agentId;
            p.AgentBlock.TransactionID = transactionID;
            OutPacket(p, ThrottleOutPacketType.Task);
        }

        public void SendAcceptCallingCard(UUID transactionID)
        {
            AcceptCallingCardPacket p = (AcceptCallingCardPacket)PacketPool.Instance.GetPacket(PacketType.AcceptCallingCard);
            p.AgentData.AgentID = m_agentId;
            p.AgentData.SessionID = UUID.Zero;
            p.FolderData = new AcceptCallingCardPacket.FolderDataBlock[1];
            p.FolderData[0] = new AcceptCallingCardPacket.FolderDataBlock
            {
                FolderID = UUID.Zero
            };
            OutPacket(p, ThrottleOutPacketType.Task);
        }

        public void SendDeclineCallingCard(UUID transactionID)
        {
            DeclineCallingCardPacket p = (DeclineCallingCardPacket)PacketPool.Instance.GetPacket(PacketType.DeclineCallingCard);
            p.AgentData.AgentID = m_agentId;
            p.AgentData.SessionID = UUID.Zero;
            p.TransactionBlock.TransactionID = transactionID;
            OutPacket(p, ThrottleOutPacketType.Task);
        }

        public void SendTerminateFriend(UUID exFriendID)
        {
            TerminateFriendshipPacket p = (TerminateFriendshipPacket)PacketPool.Instance.GetPacket(PacketType.TerminateFriendship);
            p.AgentData.AgentID = m_agentId;
            p.AgentData.SessionID = SessionId;
            p.ExBlock.OtherID = exFriendID;
            OutPacket(p, ThrottleOutPacketType.Task);
        }

        public void SendAvatarGroupsReply(UUID avatarID, GroupMembershipData[] data)
        {
            IEventQueue eq = Scene.RequestModuleInterface<IEventQueue>();
            if (eq is null)
                return;

            // message template has a GroupData field AcceptNotices ignored by viewers
            // and a array NewGroupData also ignored
            osUTF8 sb = eq.StartEvent("AvatarGroupsReply");

            LLSDxmlEncode2.AddArrayAndMap("AgentData", sb);
                LLSDxmlEncode2.AddElem("AgentID", m_agentId, sb);
                LLSDxmlEncode2.AddElem("AvatarID", avatarID, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            bool notSameAvatar = avatarID != m_agentId;
            if(data.Length == 0)
                LLSDxmlEncode2.AddEmptyArray("GroupData", sb);
            else
            {
                LLSDxmlEncode2.AddArray("GroupData", sb);
                GroupMembershipData m;
                for (int indx = 0; indx < data.Length; ++indx)
                {
                    m = data[indx];
                    if(notSameAvatar && !m.ListInProfile)
                        continue;
                    LLSDxmlEncode2.AddMap(sb);
                       LLSDxmlEncode2.AddElem("GroupPowers", m.GroupPowers, sb);
                        LLSDxmlEncode2.AddElem("GroupTitle", m.GroupTitle, sb);
                        LLSDxmlEncode2.AddElem("GroupID",m.GroupID, sb);
                        LLSDxmlEncode2.AddElem("GroupName", m.GroupName, sb);
                        LLSDxmlEncode2.AddElem("GroupInsigniaID", m.GroupPicture, sb);
                    LLSDxmlEncode2.AddEndMap(sb);
                }
                LLSDxmlEncode2.AddEndArray(sb);
            }
            eq.Enqueue(eq.EndEventToBytes(sb), m_agentId);
        }

        public void SendAgentGroupDataUpdate(UUID avatarID, GroupMembershipData[] data)
        {
            if(avatarID != m_agentId)
                m_log.Debug("[CLIENT]: SendAgentGroupDataUpdate avatarID != AgentId");

            IEventQueue eq = this.Scene.RequestModuleInterface<IEventQueue>();
            if(eq != null)
            {
                eq.GroupMembershipData(avatarID,data);
            }
            else
            {
                // use UDP if no caps
                AgentGroupDataUpdatePacket Groupupdate = new();
                AgentGroupDataUpdatePacket.GroupDataBlock[] Groups = new AgentGroupDataUpdatePacket.GroupDataBlock[data.Length];
                for (int i = 0; i < data.Length; ++i)
                {
                    AgentGroupDataUpdatePacket.GroupDataBlock Group = new()
                    {
                        AcceptNotices = data[i].AcceptNotices,
                        Contribution = data[i].Contribution,
                        GroupID = data[i].GroupID,
                        GroupInsigniaID = data[i].GroupPicture,
                        GroupName = Util.StringToBytes256(data[i].GroupName),
                        GroupPowers = data[i].GroupPowers
                    };
                    Groups[i] = Group;
                }
                Groupupdate.GroupData = Groups;
                Groupupdate.AgentData = new AgentGroupDataUpdatePacket.AgentDataBlock { AgentID = avatarID };
                OutPacket(Groupupdate, ThrottleOutPacketType.Task);
            }
        }

        public void SendJoinGroupReply(UUID groupID, bool success)
        {
            JoinGroupReplyPacket p = (JoinGroupReplyPacket)PacketPool.Instance.GetPacket(PacketType.JoinGroupReply);

            p.AgentData = new JoinGroupReplyPacket.AgentDataBlock { AgentID = m_agentId };
            p.GroupData = new JoinGroupReplyPacket.GroupDataBlock
            {
                GroupID = groupID,
                Success = success
            };

            OutPacket(p, ThrottleOutPacketType.Task);
        }

        public void SendEjectGroupMemberReply(UUID agentID, UUID groupID, bool success)
        {
            EjectGroupMemberReplyPacket p = (EjectGroupMemberReplyPacket)PacketPool.Instance.GetPacket(PacketType.EjectGroupMemberReply);

            p.AgentData = new EjectGroupMemberReplyPacket.AgentDataBlock { AgentID = agentID };
            p.GroupData = new EjectGroupMemberReplyPacket.GroupDataBlock { GroupID = groupID };
            p.EjectData = new EjectGroupMemberReplyPacket.EjectDataBlock { Success = success };

            OutPacket(p, ThrottleOutPacketType.Task);
        }

        public void SendLeaveGroupReply(UUID groupID, bool success)
        {
            LeaveGroupReplyPacket p = (LeaveGroupReplyPacket)PacketPool.Instance.GetPacket(PacketType.LeaveGroupReply);

            p.AgentData = new LeaveGroupReplyPacket.AgentDataBlock { AgentID = m_agentId };
            p.GroupData = new LeaveGroupReplyPacket.GroupDataBlock
            {
                GroupID = groupID,
                Success = success
            };

            OutPacket(p, ThrottleOutPacketType.Task);
        }

        public void SendAvatarClassifiedReply(UUID targetID, UUID[] classifiedID, string[] name)
        {
            if (classifiedID.Length != name.Length)
                return;

            AvatarClassifiedReplyPacket ac =
                    (AvatarClassifiedReplyPacket)PacketPool.Instance.GetPacket(PacketType.AvatarClassifiedReply);

            ac.AgentData = new AvatarClassifiedReplyPacket.AgentDataBlock
            {
                AgentID = m_agentId,
                TargetID = targetID
            };

            ac.Data = new AvatarClassifiedReplyPacket.DataBlock[classifiedID.Length];
            int i;
            for (i = 0; i < classifiedID.Length; i++)
            {
                ac.Data[i].ClassifiedID = classifiedID[i];
                ac.Data[i].Name = Utils.StringToBytes(name[i]);
            }

            OutPacket(ac, ThrottleOutPacketType.Task);
        }

        public void SendClassifiedInfoReply(UUID classifiedID, UUID creatorID, uint creationDate, uint expirationDate, uint category, string name, string description, UUID parcelID, uint parentEstate, UUID snapshotID, string simName, Vector3 globalPos, string parcelName, byte classifiedFlags, int price)
        {
            // fix classifiedFlags maturity
            if((classifiedFlags & 0x4e) == 0) // if none
                classifiedFlags |= 0x4; // pg

            ClassifiedInfoReplyPacket cr =
                    (ClassifiedInfoReplyPacket)PacketPool.Instance.GetPacket(PacketType.ClassifiedInfoReply);

            cr.AgentData = new ClassifiedInfoReplyPacket.AgentDataBlock { AgentID = m_agentId };

            cr.Data = new ClassifiedInfoReplyPacket.DataBlock
            {
                ClassifiedID = classifiedID,
                CreatorID = creatorID,
                CreationDate = creationDate,
                ExpirationDate = expirationDate,
                Category = category,
                Name = Utils.StringToBytes(name),
                Desc = Utils.StringToBytes(description),
                ParcelID = parcelID,
                ParentEstate = parentEstate,
                SnapshotID = snapshotID,
                SimName = Utils.StringToBytes(simName),
                PosGlobal = new Vector3d(globalPos),
                ParcelName = Utils.StringToBytes(parcelName),
                ClassifiedFlags = classifiedFlags,
                PriceForListing = price
            };

            OutPacket(cr, ThrottleOutPacketType.Task);
        }

        public void SendAgentDropGroup(UUID groupID)
        {
            AgentDropGroupPacket dg = (AgentDropGroupPacket)PacketPool.Instance.GetPacket(PacketType.AgentDropGroup);

            dg.AgentData = new AgentDropGroupPacket.AgentDataBlock
            {
                AgentID = m_agentId,
                GroupID = groupID
            };

            OutPacket(dg, ThrottleOutPacketType.Task);
        }

        public void SendAvatarNotesReply(UUID targetID, string text)
        {
            AvatarNotesReplyPacket an = (AvatarNotesReplyPacket)PacketPool.Instance.GetPacket(PacketType.AvatarNotesReply);

            an.AgentData = new AvatarNotesReplyPacket.AgentDataBlock { AgentID = m_agentId };
            an.Data = new AvatarNotesReplyPacket.DataBlock
            {
                TargetID = targetID,
                Notes = Utils.StringToBytes(text)
            };

            OutPacket(an, ThrottleOutPacketType.Task);
        }

        public void SendAvatarPicksReply(UUID targetID, Dictionary<UUID, string> picks)
        {
            AvatarPicksReplyPacket ap = (AvatarPicksReplyPacket)PacketPool.Instance.GetPacket(PacketType.AvatarPicksReply);

            ap.AgentData = new AvatarPicksReplyPacket.AgentDataBlock
            {
                AgentID = m_agentId,
                TargetID = targetID
            };

            ap.Data = new AvatarPicksReplyPacket.DataBlock[picks.Count];

            int i = 0;
            foreach (KeyValuePair<UUID, string> pick in picks)
            {
                ap.Data[i++] = new AvatarPicksReplyPacket.DataBlock
                {
                    PickID = pick.Key,
                    PickName = Utils.StringToBytes(pick.Value)
                };
            }

            OutPacket(ap, ThrottleOutPacketType.Task);
        }

        public void SendAvatarClassifiedReply(UUID targetID, Dictionary<UUID, string> classifieds)
        {
            AvatarClassifiedReplyPacket ac =
                    (AvatarClassifiedReplyPacket)PacketPool.Instance.GetPacket(PacketType.AvatarClassifiedReply);

            ac.AgentData = new AvatarClassifiedReplyPacket.AgentDataBlock
            {
                AgentID = m_agentId,
                TargetID = targetID
            };

            ac.Data = new AvatarClassifiedReplyPacket.DataBlock[classifieds.Count];

            int i = 0;
            foreach (KeyValuePair<UUID, string> classified in classifieds)
            {
                ac.Data[i++] = new AvatarClassifiedReplyPacket.DataBlock
                {
                    ClassifiedID = classified.Key,
                    Name = Utils.StringToBytes(classified.Value)
                };
            }

            OutPacket(ac, ThrottleOutPacketType.Task);
        }

        public void SendParcelDwellReply(int localID, UUID parcelID, float dwell)
        {
            ParcelDwellReplyPacket pd =
                    (ParcelDwellReplyPacket)PacketPool.Instance.GetPacket(PacketType.ParcelDwellReply);

            pd.AgentData = new ParcelDwellReplyPacket.AgentDataBlock { AgentID = m_agentId };
            pd.Data = new ParcelDwellReplyPacket.DataBlock
            {
                LocalID = localID,
                ParcelID = parcelID,
                Dwell = dwell
            };

            OutPacket(pd, ThrottleOutPacketType.Land);
        }

        public void SendUserInfoReply(bool imViaEmail, bool visible, string email)
        {
            UserInfoReplyPacket ur =
                    (UserInfoReplyPacket)PacketPool.Instance.GetPacket(PacketType.UserInfoReply);

            ur.AgentData = new UserInfoReplyPacket.AgentDataBlock { AgentID = m_agentId };
            ur.UserData = new UserInfoReplyPacket.UserDataBlock
            {
                IMViaEMail = imViaEmail,
                DirectoryVisibility = Utils.StringToBytes(visible ? "default" : "hidden"),
                EMail = Utils.StringToBytes(email)
            };

            OutPacket(ur, ThrottleOutPacketType.Task);
        }

        public void SendCreateGroupReply(UUID groupID, bool success, string message)
        {
            CreateGroupReplyPacket createGroupReply = (CreateGroupReplyPacket)PacketPool.Instance.GetPacket(PacketType.CreateGroupReply);

            createGroupReply.AgentData = new CreateGroupReplyPacket.AgentDataBlock { AgentID = m_agentId };
            createGroupReply.ReplyData = new CreateGroupReplyPacket.ReplyDataBlock
            {
                GroupID = groupID,
                Success = success,
                Message = Utils.StringToBytes(message)
            };
            OutPacket(createGroupReply, ThrottleOutPacketType.Task);
        }

        public void SendUseCachedMuteList()
        {
            UseCachedMuteListPacket useCachedMuteList = (UseCachedMuteListPacket)PacketPool.Instance.GetPacket(PacketType.UseCachedMuteList);

            useCachedMuteList.AgentData = new UseCachedMuteListPacket.AgentDataBlock { AgentID = m_agentId };

            OutPacket(useCachedMuteList, ThrottleOutPacketType.Task);
        }

       public void SendEmpytMuteList()
        {
            GenericMessagePacket gmp = new();

            gmp.AgentData.AgentID = m_agentId;
            gmp.AgentData.SessionID = m_sessionId;
            gmp.AgentData.TransactionID = UUID.Zero;

            gmp.MethodData.Method = Util.StringToBytes256("emptymutelist");
            gmp.ParamList = new GenericMessagePacket.ParamListBlock[1];
            gmp.ParamList[0] = new GenericMessagePacket.ParamListBlock { Parameter = Array.Empty<byte>() };

            OutPacket(gmp, ThrottleOutPacketType.Task);
        }

        public void SendMuteListUpdate(string filename)
        {
            MuteListUpdatePacket muteListUpdate = (MuteListUpdatePacket)PacketPool.Instance.GetPacket(PacketType.MuteListUpdate);

            muteListUpdate.MuteData = new MuteListUpdatePacket.MuteDataBlock
            {
                AgentID = m_agentId,
                Filename = Utils.StringToBytes(filename)
            };

            OutPacket(muteListUpdate, ThrottleOutPacketType.Task);
        }

        public void SendPickInfoReply(UUID pickID, UUID creatorID, bool topPick, UUID parcelID, string name, string desc, UUID snapshotID, string user, string originalName, string simName, Vector3 posGlobal, int sortOrder, bool enabled)
        {
            PickInfoReplyPacket pickInfoReply = (PickInfoReplyPacket)PacketPool.Instance.GetPacket(PacketType.PickInfoReply);

            pickInfoReply.AgentData = new PickInfoReplyPacket.AgentDataBlock { AgentID = m_agentId };

            pickInfoReply.Data = new PickInfoReplyPacket.DataBlock
            {
                PickID = pickID,
                CreatorID = creatorID,
                TopPick = topPick,
                ParcelID = parcelID,
                Name = Utils.StringToBytes(name),
                Desc = Utils.StringToBytes(desc),
                SnapshotID = snapshotID,
                User = Utils.StringToBytes(user),
                OriginalName = Utils.StringToBytes(originalName),
                SimName = Utils.StringToBytes(simName),
                PosGlobal = new Vector3d(posGlobal),
                SortOrder = sortOrder,
                Enabled = enabled
            };

            OutPacket(pickInfoReply, ThrottleOutPacketType.Task);
        }

        #endregion Scene/Avatar to Client

        // Gesture

        #region Appearance/ Wearables Methods

        public void SendWearables(AvatarWearable[] wearables, int serial)
        {
            AgentWearablesUpdatePacket aw = (AgentWearablesUpdatePacket)PacketPool.Instance.GetPacket(PacketType.AgentWearablesUpdate);
            aw.AgentData.AgentID = m_agentId;
            aw.AgentData.SerialNum = (uint)serial;
            aw.AgentData.SessionID = m_sessionId;

            int count = 0;
            for (int i = 0; i < wearables.Length; i++)
                count += wearables[i].Count;

            // TODO: don't create new blocks if recycling an old packet
            aw.WearableData = new AgentWearablesUpdatePacket.WearableDataBlock[count];
            AgentWearablesUpdatePacket.WearableDataBlock awb;
            int idx = 0;
            for (int i = 0; i < wearables.Length; i++)
            {
                for (int j = 0; j < wearables[i].Count; j++)
                {
                    awb = new AgentWearablesUpdatePacket.WearableDataBlock
                    {
                        WearableType = (byte)i,
                        AssetID = wearables[i][j].AssetID,
                        ItemID = wearables[i][j].ItemID
                    };
                    aw.WearableData[idx++] = awb;

                    //m_log.DebugFormat(
                    //    "[APPEARANCE]: Sending wearable item/asset {0} {1} (index {2}) for {3}",
                    //    awb.ItemID, awb.AssetID, i, Name);
                }
            }

            OutPacket(aw, ThrottleOutPacketType.Task);
        }

        static private readonly byte[] AvatarAppearanceHeader = new byte[] {
                Helpers.MSG_RELIABLE | Helpers.MSG_ZEROCODED,
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 0xff, 0, 158 // ID 158 (low frequency bigendian) not zeroencoded
                //0xff, 0xff, 0, 1, 158 // ID 158 (low frequency bigendian) zeroencoded
                };

        public void SendAppearance(UUID targetID, byte[] visualParams, byte[] textureEntry, float hover)
        {
            // doing post zero encode, because odds of beeing bad are not that low
            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            Buffer.BlockCopy(AvatarAppearanceHeader, 0, buf.Data, 0, 10);
            byte[] data = buf.Data;
            int pos = 10;

            //sender block
            targetID.ToBytes(data, pos); pos += 16;
            data[pos++] = 0;// is trial = false

            // objectdata block ie texture
            int len = textureEntry.Length;
            if (len == 0)
            {
                data[pos++] = 0;
                data[pos++] = 0;
            }
            else
            {
                data[pos++] = (byte)len;
                data[pos++] = (byte)(len >> 8);
                Buffer.BlockCopy(textureEntry, 0, data, pos, len); pos += len;
            }

            // visual parameters
            len = visualParams.Length;
            data[pos++] = (byte)len;
            if(len > 0)
                Buffer.BlockCopy(visualParams, 0, data, pos, len); pos += len;

            // no AppearanceData
            data[pos++] = 0;
            // AppearanceHover vector 3
            data[pos++] = 1;
            Utils.FloatToBytesSafepos(0, data, pos); pos += 4;
            Utils.FloatToBytesSafepos(0, data, pos); pos += 4;
            Utils.FloatToBytesSafepos(hover, data, pos); pos += 4;

            buf.DataLength = pos;
            m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task, null, true);
        }

        static private readonly byte[] AvatarAnimationHeader = new byte[] {
                Helpers.MSG_RELIABLE,
                0, 0, 0, 0, // sequence number
                0, // extra
                20 // ID (high frequency)
                };

        public void SendAnimations(UUID[] animations, int[] seqs, UUID sourceAgentId, UUID[] objectIDs)
        {
            //            m_log.DebugFormat("[LLCLIENTVIEW]: Sending animations for {0} to {1}", sourceAgentId, Name);

            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            byte[] data = buf.Data;
            //setup header
            Buffer.BlockCopy(AvatarAnimationHeader, 0, data, 0, 7);
            //agent block
            sourceAgentId.ToBytes(data, 7);

            // animations count
            data[23] = (byte)animations.Length;

            int pos = 24;

            //self animations
            if (sourceAgentId.Equals(m_agentId))
            {
                List<int> withobjects = new(animations.Length);
                List<int> noobjects = new(animations.Length);
                for (int indx = 0; indx < animations.Length; ++indx)
                {
                    if (objectIDs[indx].Equals(sourceAgentId) || objectIDs[indx].IsZero())
                        noobjects.Add(indx);
                    else
                        withobjects.Add(indx);
                }

                int i;
                // first the ones with corresponding objects
                for (int indx = 0; indx < withobjects.Count; ++indx)
                {
                    i = withobjects[indx];
                    animations[i].ToBytes(data, pos); pos += 16;
                    Utils.IntToBytesSafepos(seqs[i], data, pos); pos += 4;
                }
                // then the rest
                for (int indx = 0; indx < noobjects.Count; ++indx)
                {
                    i = noobjects[indx];
                    animations[i].ToBytes(data, pos); pos += 16;
                    Utils.IntToBytesSafepos(seqs[i], data, pos); pos += 4;
                }
                // object ids block
                data[pos++] = (byte)withobjects.Count;
                for (int indx = 0; indx < withobjects.Count; ++indx)
                {
                    i = withobjects[indx];
                    objectIDs[i].ToBytes(data, pos); pos += 16;
                }
            }
            else
            {
                for(int i = 0; i < animations.Length; ++i)
                {
                    animations[i].ToBytes(data, pos); pos += 16;
                    Utils.IntToBytesSafepos(seqs[i], data, pos); pos += 4;
                }
                data[pos++] = 0; // no object ids
            }

            data[pos++] = 0; // no physical avatar events

            buf.DataLength = pos;
            m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
        }

        public void SendObjectAnimations(UUID[] animations, int[] seqs, UUID senderId)
        {
            // m_log.DebugFormat("[LLCLIENTVIEW]: Sending Object animations for {0} to {1}", sourceAgentId, Name);
            if(!m_SupportObjectAnimations)
                return;

            ObjectAnimationPacket ani = (ObjectAnimationPacket)PacketPool.Instance.GetPacket(PacketType.ObjectAnimation);
            // TODO: don't create new blocks if recycling an old packet
            ani.Sender = new ObjectAnimationPacket.SenderBlock
            {
                ID = senderId
            };
            ani.AnimationList = new ObjectAnimationPacket.AnimationListBlock[animations.Length];

            for (int i = 0; i < animations.Length; ++i)
            {
                ani.AnimationList[i] = new ObjectAnimationPacket.AnimationListBlock
                {
                    AnimID = animations[i],
                    AnimSequenceID = seqs[i]
                };
            }
            OutPacket(ani, ThrottleOutPacketType.Task);
        }

        #endregion

        #region Avatar Packet/Data Sending Methods

        /// <summary>
        /// Send an ObjectUpdate packet with information about an avatar
        /// </summary>
        public void SendEntityFullUpdateImmediate(ISceneEntity ent)
        {
            if (ent is not ScenePresence && ent is not SceneObjectPart)
                return;

            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            Buffer.BlockCopy(objectUpdateHeader, 0, buf.Data, 0, 7);

            LLUDPZeroEncoder zc = new(buf.Data);
            zc.Position = 7;

            zc.AddUInt64(m_scene.RegionInfo.RegionHandle);
            zc.AddUInt16(Utils.FloatZeroOneToushort(m_scene.TimeDilation));

            zc.AddByte(1); // block count

            if (ent is ScenePresence sp)
                CreateAvatarUpdateBlock(sp, zc);
            else
                CreatePrimUpdateBlock(ent as SceneObjectPart, (ScenePresence)SceneAgent, zc);

            buf.DataLength = zc.Finish();
            m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
        }

        public unsafe void SendEntityTerseUpdateImmediate(ISceneEntity ent)
        {
            if (ent is null)
                return;

            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);

            //setup header and regioninfo block
            Buffer.BlockCopy(terseUpdateHeader, 0, buf.Data, 0, 7);
            fixed(byte* bdata = &buf.Data[0])
            {
                Utils.UInt16ToBytes(Utils.FloatZeroOneToushort(m_scene.TimeDilation), bdata + 15);
                bdata[17] = 1;

                byte* data = bdata + 18;
                if (ent is ScenePresence sp)
                {
                    Utils.UInt64ToBytes(sp.RegionHandle, bdata + 7);
                    CreateAvatartImprovedTerseBlock(sp, ref data);
                }
                else
                {
                    Utils.UInt64ToBytes(m_scene.RegionInfo.RegionHandle, bdata + 7);
                    CreatePartImprovedTerseBlock((SceneObjectPart)ent, ref data, false);
                }
                buf.DataLength = (int)(data - bdata);
            }
            m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task, null, true);
        }

        //UUID m_courseLocationPrey = UUID.Zero;
        bool m_couseLocationLastEmpty = false;

        static private readonly byte[] CoarseLocationUpdateHeader = new byte[] {
                0, // no acks plz
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 6 // ID 6 (medium frequency)
                };

        public void SendCoarseLocationUpdate(List<UUID> users, List<Vector3> CoarseLocations)
        {
            // We don't need to update inactive clients.
            if (!IsActive)
                return;

            int totalLocations = Math.Min(CoarseLocations.Count, 60);
            if(totalLocations == 0)
            {
                if(m_couseLocationLastEmpty)
                    return;
                m_couseLocationLastEmpty = true;
            }
            else
                m_couseLocationLastEmpty = false;

            int totalAgents = Math.Min(users.Count, 60);
            if(totalAgents > totalLocations)
                totalAgents = totalLocations;

            int selfindex = -1;
            int preyindex = -1;

            //bool doprey = m_courseLocationPrey != UUID.Zero;

            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            Buffer.BlockCopy(CoarseLocationUpdateHeader, 0, buf.Data, 0, 8);
            byte[] data = buf.Data;

            data[8] = (byte)totalLocations;
            int pos = 9;

            for (int i = 0; i < totalLocations; ++i)
            {
                data[pos++] = (byte)CoarseLocations[i].X;
                data[pos++] = (byte)CoarseLocations[i].Y;
                data[pos++] = CoarseLocations[i].Z > 1024 ? (byte)0 : (byte)(CoarseLocations[i].Z * 0.25f);
                
                if (i < totalAgents)
                {
                    if (users[i] == m_agentId)
                        selfindex = i;
                    //if (doprey && users[i] == m_courseLocationPrey)
                    //    preyindex = i;
                }
            }

            Utils.Int16ToBytes((short)selfindex, data, pos); pos += 2;
            Utils.Int16ToBytes((short)preyindex, data, pos); pos += 2;

            data[pos++] = (byte)totalAgents;
            for (int i = 0; i < totalAgents; ++i)
            {
                users[i].ToBytes(data, pos);
                pos += 16;
            }

            buf.DataLength = pos;
            m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
        }

        #endregion Avatar Packet/Data Sending Methods

        #region Primitive Packet/Data Sending Methods

        /// <summary>
        /// Generate one of the object update packets based on PrimUpdateFlags
        /// and broadcast the packet to clients
        /// </summary>
        public void SendEntityUpdate(ISceneEntity entity, PrimUpdateFlags updateFlags)
        {
            if (entity is SceneObjectPart p)
            {
                SceneObjectGroup g = p.ParentGroup;
                if (g.HasPrivateAttachmentPoint && g.OwnerID != m_agentId)
                    return; // Don't send updates for other people's HUDs

                if((updateFlags ^ PrimUpdateFlags.SendInTransit) == 0)
                {
                    List<uint> partIDs = (new List<uint> {p.LocalId});
                    m_entityProps.Remove(partIDs);
                    m_entityUpdates.Remove(partIDs);
                    return;
                }
            }

            int priority = m_prioritizer.GetUpdatePriority(this, entity);
            m_entityUpdates.Enqueue(priority, EntityUpdatesPool.Get(entity, updateFlags));
        }

        /// <summary>
        /// Requeue an EntityUpdate when it was not acknowledged by the client.
        /// We will update the priority and put it in the correct queue, merging update flags
        /// with any other updates that may be queued for the same entity.
        /// The original update time is used for the merged update.
        /// </summary>
        private void ResendPrimUpdate(EntityUpdate update)
        {
            // If the update exists in priority queue, it will be updated.
            // If it does not exist then it will be added with the current (rather than its original) priority
            int priority = m_prioritizer.GetUpdatePriority(this, update.Entity);
            m_entityUpdates.Enqueue(priority, update);
        }

        /// <summary>
        /// Requeue a list of EntityUpdates when they were not acknowledged by the client.
        /// We will update the priority and put it in the correct queue, merging update flags
        /// with any other updates that may be queued for the same entity.
        /// The original update time is used for the merged update.
        /// </summary>
        private void ResendPrimUpdates(List<EntityUpdate> updates, OutgoingPacket oPacket)
        {
            // m_log.WarnFormat("[CLIENT] resending prim updates {0}, packet sequence number {1}", updates[0].UpdateTime, oPacket.SequenceNumber);

            // Remove the update packet from the list of packets waiting for acknowledgement
            // because we are requeuing the list of updates. They will be resent in new packets
            // with the most recent state and priority.
            m_udpClient.NeedAcks.Remove(oPacket.SequenceNumber);
            if (oPacket.Buffer is null)
                return;

            // Count this as a resent packet since we are going to requeue all of the updates contained in it
            Interlocked.Increment(ref m_udpClient.PacketsResent);

            m_udpServer.PacketsResentCount++;

            foreach (EntityUpdate update in updates)
                ResendPrimUpdate(update);
        }

        static private readonly byte[] objectUpdateHeader = new byte[] {
                Helpers.MSG_RELIABLE | Helpers.MSG_ZEROCODED,
                0, 0, 0, 0, // sequence number
                0, // extra
                12 // ID (high frequency)
                };

        static private readonly byte[] terseUpdateHeader = new byte[] {
                Helpers.MSG_RELIABLE | Helpers.MSG_ZEROCODED, // zero code is not as spec
                0, 0, 0, 0, // sequence number
                0, // extra
                15 // ID (high frequency)
                };

        static private readonly byte[] ObjectAnimationHeader = new byte[] {
                Helpers.MSG_RELIABLE,
                0, 0, 0, 0, // sequence number
                0, // extra
                30 // ID (high frequency)
                };

        static private readonly byte[] CompressedObjectHeader = new byte[] {
                Helpers.MSG_RELIABLE,
                0, 0, 0, 0, // sequence number
                0, // extra
                13 // ID (high frequency)
                };

        static private readonly byte[] ObjectUpdateCachedHeader = new byte[] {
                Helpers.MSG_RELIABLE,
                0, 0, 0, 0, // sequence number
                0, // extra
                14 // ID (high frequency)
                };

        private void ProcessEntityUpdates(int maxUpdatesBytes)
        {
            if (!IsActive)
                return;

            ScenePresence mysp = (ScenePresence)SceneAgent;
            if (mysp is null)
                return;

            List<EntityUpdate> objectUpdates = null;
            List<EntityUpdate> objectUpdateProbes = null;
            List<EntityUpdate> compressedUpdates = null;
            List<EntityUpdate> terseUpdates = null;
            List<SceneObjectPart> ObjectAnimationUpdates = null;
            List<SceneObjectPart> needMaterials = null;

            // Check to see if this is a flush
            if (maxUpdatesBytes <= 0)
            {
                maxUpdatesBytes = Int32.MaxValue;
            }

            EntityUpdate update;

            bool viewerCache = m_supportViewerCache;// && mysp.IsChildAgent; // only on child agents
            bool doCulling = m_scene.ObjectsCullingByDistance;
            float cullingrange = 64.0f;
            Vector3 mypos = Vector3.Zero;

            //bool orderedDequeue = m_scene.UpdatePrioritizationScheme  == UpdatePrioritizationSchemes.SimpleAngularDistance;
            bool orderedDequeue = false; // temporary off

            HashSet<SceneObjectGroup> GroupsNeedFullUpdate = new();
            bool useCompressUpdate = false;

            if (doCulling)
            {
                cullingrange = mysp.DrawDistance + m_scene.ReprioritizationDistance + 16f;
                mypos = mysp.AbsolutePosition;
            }

            while (maxUpdatesBytes > 0)
            {
                if (!IsActive)
                    return;

                if(orderedDequeue)
                {
                    if (!m_entityUpdates.TryOrderedDequeue(out update))
                        break;
                }
                else
                {
                    if (!m_entityUpdates.TryDequeue(out update))
                        break;
                }

                PrimUpdateFlags updateFlags = update.Flags;

                if ((updateFlags  & PrimUpdateFlags.Kill) != 0)
                {
                    m_killRecord.Add(update.Entity.LocalId);
                    maxUpdatesBytes -= 30;
                    update.Free();
                    continue;
                }

                useCompressUpdate = false;
                bool istree = false;
                bool hasMaterialOverride = false;

                if (update.Entity is SceneObjectPart part)
                {
                    SceneObjectGroup grp = part.ParentGroup;
                    if (grp.inTransit && ((update.Flags & PrimUpdateFlags.SendInTransit) == 0))
                    {
                        update.Free();
                        continue;
                    }

                    if (grp.IsDeleted)
                    {
                        // Don't send updates for objects that have been marked deleted.
                        // Instead send another kill object, because the first one may have gotten
                        // into a race condition
                        if (part == grp.RootPart && !m_killRecord.Contains(grp.LocalId))
                        {
                            m_killRecord.Add(grp.LocalId);
                            maxUpdatesBytes -= 30;
                        }
                        update.Free();
                        continue;
                    }

                    if (grp.IsAttachment)
                    {
                        // animated attachments are nasty if not supported by viewer
                        if(!m_SupportObjectAnimations && grp.RootPart.Shape.MeshFlagEntry)
                        {
                            update.Free();
                            continue;
                        }

                        // Someone else's HUD, why are we getting these?
                        if (grp.HasPrivateAttachmentPoint && grp.OwnerID != m_agentId)
                        {
                            update.Free();
                            continue;
                        }

                        // if owner gone don't update it to anyone
                        if (!m_scene.TryGetScenePresence(part.OwnerID, out ScenePresence sp))
                        {
                            update.Free();
                            continue;
                        }

                        // On vehicle crossing, the attachments are received
                        // while the avatar is still a child. Don't send
                        // updates here because the LocalId has not yet
                        // been updated and the viewer will derender the
                        // attachments until the avatar becomes root.
                        if (sp.IsChildAgent)
                        {
                            update.Free();
                            continue;
                        }

                        // It's an attachment of a valid avatar, but
                        // doesn't seem to be attached, skip
                        List<SceneObjectGroup> atts = sp.GetAttachments();
                        bool found = false;
                        for (int indx = 0; indx < atts.Count; ++indx)
                        {
                            if (atts[indx] == grp)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            update.Free();
                            continue;
                        }

                        if (m_disableFacelights)
                        {
                            if (grp.RootPart.Shape.State != (byte)AttachmentPoint.LeftHand &&
                                grp.RootPart.Shape.State != (byte)AttachmentPoint.RightHand)
                            {
                                part.Shape.LightEntry = false;
                            }
                        }
                    }

                    else if (doCulling)
                    {
                        if(GroupsNeedFullUpdate.Contains(grp))
                        {
                            update.Free();
                            continue;
                        }

                        bool inViewGroups = false;
                        lock(GroupsInView)
                            inViewGroups = GroupsInView.Contains(grp);

                        if(!inViewGroups)
                        {
                            Vector3 partpos = grp.getCenterOffset();
                            float dpos = (partpos - mypos).LengthSquared();
                            float maxview = grp.GetBoundsRadius() + cullingrange;
                            if (dpos > maxview * maxview)
                            {
                                update.Free();
                                continue;
                            }

                            if (!viewerCache || ((updateFlags & PrimUpdateFlags.UpdateProbe) == 0))
                            {
                                GroupsNeedFullUpdate.Add(grp);
                                update.Free();
                                continue;
                            }
                        }
                    }

                    if ((updateFlags & PrimUpdateFlags.UpdateProbe) != 0)
                    {
                        if (objectUpdateProbes is null)
                        {
                            objectUpdateProbes = new List<EntityUpdate>(64);
                            maxUpdatesBytes -= 18;
                        }
                        objectUpdateProbes.Add(update);
                        maxUpdatesBytes -= 12;
                        continue;
                    }

                    if ((updateFlags & PrimUpdateFlags.Animations) != 0)
                    {
                        if (m_SupportObjectAnimations && part.Animations != null)
                        {
                            ObjectAnimationUpdates ??= new List<SceneObjectPart>(8);
                            ObjectAnimationUpdates.Add(part);
                            maxUpdatesBytes -= 20 * part.Animations.Count + 24;
                        }
                    }

                    if(viewerCache)
                        useCompressUpdate = grp.IsViewerCachable;

                    istree = (part.Shape.PCode == (byte)PCode.Grass || part.Shape.PCode == (byte)PCode.NewTree || part.Shape.PCode == (byte)PCode.Tree);
                    if(!istree && part.Shape.RenderMaterials is not null &&
                        part.Shape.ReflectionProbe is null &&
                        part.Shape.RenderMaterials.overrides is not null &&
                        part.Shape.RenderMaterials.overrides.Length > 0)
                        hasMaterialOverride = true;
                }
                else if (update.Entity is ScenePresence presence)
                {
                    if (presence.IsDeleted)
                    {
                        update.Free();
                        continue;
                    }
                    // If ParentUUID is not UUID.Zero and ParentID is 0, this
                    // avatar is in the process of crossing regions while
                    // sat on an object. In this state, we don't want any
                    // updates because they will visually orbit the avatar.
                    // Update will be forced once crossing is completed anyway.
                    if (!presence.ParentUUID.IsZero() && presence.ParentID == 0)
                    {
                        update.Free();
                        continue;
                    }
                }
                else // what is this update ?
                {
                    update.Free();
                    continue;
                }

                #region UpdateFlags to packet type conversion

                updateFlags &= PrimUpdateFlags.FullUpdate; // clear other control bits already handled
                if(updateFlags == PrimUpdateFlags.None)
                {
                    update.Free();
                    continue;
                }

                const PrimUpdateFlags canNotUseImprovedMask = ~(
                        PrimUpdateFlags.AttachmentPoint |
                        PrimUpdateFlags.Position |
                        PrimUpdateFlags.Rotation |
                        PrimUpdateFlags.Velocity |
                        PrimUpdateFlags.Acceleration |
                        PrimUpdateFlags.AngularVelocity |
                        PrimUpdateFlags.CollisionPlane  |
                        PrimUpdateFlags.Textures
                        );

                #endregion UpdateFlags to packet type conversion

                #region Block Construction

                if ((updateFlags & canNotUseImprovedMask) == 0)
                {
                    if (terseUpdates is null)
                    {
                        terseUpdates = new List<EntityUpdate>(16);
                        maxUpdatesBytes -= 18;
                    }
                    terseUpdates.Add(update);

                    if (update.Entity is ScenePresence)
                        maxUpdatesBytes -= 63; // no texture entry
                    else
                    {
                        if ((updateFlags & PrimUpdateFlags.Textures) == 0)
                            maxUpdatesBytes -= 47;
                        else
                            maxUpdatesBytes -= 150; // aprox
                    }
                }
                else
                {
                    if (update.Entity is ScenePresence)
                    {
                        maxUpdatesBytes -= 150; // crude estimation

                        if (objectUpdates is null)
                        {
                            objectUpdates = new List<EntityUpdate>(16);
                            maxUpdatesBytes -= 18;
                        }
                        objectUpdates.Add(update);
                    }
                    else
                    {
                        if (useCompressUpdate)
                        {
                            if (istree)
                                maxUpdatesBytes -= 64;
                            else
                                maxUpdatesBytes -= 120; // crude estimation

                            if (compressedUpdates is null)
                            {
                                compressedUpdates = new List<EntityUpdate>(16);
                                maxUpdatesBytes -= 18;
                            }
                            compressedUpdates.Add(update);
                        }
                        else
                        {
                            if (istree)
                                maxUpdatesBytes -= 70;
                            else
                                maxUpdatesBytes -= 150; // crude estimation

                            if (objectUpdates is null)
                            {
                                objectUpdates = new List<EntityUpdate>(16);
                                maxUpdatesBytes -= 18;
                            }
                            objectUpdates.Add(update);
                        }
                        if(hasMaterialOverride)
                        {
                            needMaterials ??= new List<SceneObjectPart>(16);
                            needMaterials.Add((SceneObjectPart)update.Entity);
                        }
                    }
                }

                #endregion Block Construction
            }

            #region Packet Sending

            ushort timeDilation;

            if (!IsActive)
                return;

            timeDilation = Utils.FloatZeroOneToushort(m_scene.TimeDilation);
            if (objectUpdates is not null)
            {
                //List<EntityUpdate> tau = new List<EntityUpdate>(30);

                UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                Buffer.BlockCopy(objectUpdateHeader, 0, buf.Data, 0, 7);

                LLUDPZeroEncoder zc = new(buf.Data);
                zc.Position = 7;

                zc.AddUInt64(m_scene.RegionInfo.RegionHandle);
                zc.AddUInt16(timeDilation);

                zc.AddByte(1); // tmp block count

                int countposition = zc.Position - 1;

                int lastpos = 0;
                int lastzc = 0;

                int count = 0;
                bool shouldCreateSelected = false; //mantis 8639
                EntityUpdate eu;
                for(int indx = 0; indx < objectUpdates.Count; ++indx)
                {
                    eu = objectUpdates[indx];
                    lastpos = zc.Position;
                    lastzc = zc.ZeroCount;
                    if (eu.Entity is ScenePresence sp)
                        CreateAvatarUpdateBlock(sp, zc);
                    else
                    {
                        SceneObjectPart part = (SceneObjectPart)eu.Entity;
                        shouldCreateSelected = part.CreateSelected;
                        CreatePrimUpdateBlock(part, mysp, zc);
                    }

                    if (zc.Position < LLUDPServer.MAXPAYLOAD - 300)
                    {
                        //tau.Add(eu);
                        ++count;
                    }
                    else
                    {
                        // we need more packets
                        UDPPacketBuffer newbuf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                        Buffer.BlockCopy(buf.Data, 0, newbuf.Data, 0, countposition); // start is the same

                        buf.Data[countposition] = (byte)count;
                        // get pending zeros at cut point
                        if(lastzc > 0)
                        {
                            buf.Data[lastpos++] = 0;
                            buf.Data[lastpos++] = (byte)lastzc;
                        }
                        buf.DataLength = lastpos;

                        m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task,
                            //delegate (OutgoingPacket oPacket) { ResendPrimUpdates(tau, oPacket); }, false, false);
                            null, false);

                        buf = newbuf;
                        zc.Data = buf.Data;
                        zc.ZeroCount = 0;
                        zc.Position = countposition + 1;
                        // im lazy now, just do last again
                        if (eu.Entity is ScenePresence eusp)
                            CreateAvatarUpdateBlock(eusp, zc);
                        else
                        {
                            if(shouldCreateSelected) //mantis 8639 recover selected state
                                ((SceneObjectPart)eu.Entity).CreateSelected = true;
                            CreatePrimUpdateBlock((SceneObjectPart)eu.Entity, mysp, zc);
                        }

                        //tau = new List<EntityUpdate>(30);
                        //tau.Add(eu);
                        count = 1;
                    }
                    eu.Free(); //remove if using resend
                }

                if (count > 0)
                {
                    buf.Data[countposition] = (byte)count;
                    buf.DataLength = zc.Finish();
                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task,
                        //delegate (OutgoingPacket oPacket) { ResendPrimUpdates(tau, oPacket); }, false, false);
                        null, false);
                }
            }

            if (compressedUpdates is not null)
            {
                //List<EntityUpdate> tau = new List<EntityUpdate>(30);

                UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                byte[] data = buf.Data;

                Buffer.BlockCopy(CompressedObjectHeader, 0, data, 0, 7);
                data[0] |= Helpers.MSG_ZEROCODED;

                LLUDPZeroEncoder zc = new(buf.Data);
                zc.Position = 7;

                zc.AddUInt64(m_scene.RegionInfo.RegionHandle);
                zc.AddUInt16(timeDilation);

                zc.AddByte(1); // tmp block count

                int countposition = zc.Position - 1;

                int lastpos = 0;
                int lastzc = 0;

                int count = 0;
                bool shouldCreateSelected = false; //mantis 8639
                EntityUpdate eu;
                for (int indx = 0; indx < compressedUpdates.Count; ++indx)
                {
                    eu = compressedUpdates[indx];
                    SceneObjectPart sop = (SceneObjectPart)eu.Entity;
                    if (sop.ParentGroup is null || sop.ParentGroup.IsDeleted)
                        continue;

                    shouldCreateSelected = sop.CreateSelected;

                    lastpos = zc.Position;
                    lastzc = zc.ZeroCount;

                    CreateCompressedUpdateBlockZC(sop, mysp, zc);

                    if (zc.Position < LLUDPServer.MAXPAYLOAD - 200)
                    {
                        //tau.Add(eu);
                        ++count;
                    }
                    else
                    {
                        // we need more packets
                        UDPPacketBuffer newbuf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                        Buffer.BlockCopy(buf.Data, 0, newbuf.Data, 0, countposition); // start is the same

                        buf.Data[countposition] = (byte)count;
                        // get pending zeros at cut point
                        if (lastzc > 0)
                        {
                            buf.Data[lastpos++] = 0;
                            buf.Data[lastpos++] = (byte)lastzc;
                        }
                        buf.DataLength = lastpos;

                        m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task,
                            //delegate (OutgoingPacket oPacket) { ResendPrimUpdates(tau, oPacket); }, false, false);
                            null, false);

                        buf = newbuf;
                        zc.Data = buf.Data;

                        data[0] |= Helpers.MSG_ZEROCODED;

                        zc.ZeroCount = 0;
                        zc.Position = countposition + 1;

                        if (shouldCreateSelected) //mantis 8639 recover selected state
                            sop.CreateSelected = true;

                        // im lazy now, just do last again
                        CreateCompressedUpdateBlockZC(sop, mysp, zc);
                        //tau = new List<EntityUpdate>(30);
                        //tau.Add(eu);
                        count = 1;
                    }
                    eu.Free(); //remove if using resend
                }

                if (count > 0)
                {
                    buf.Data[countposition] = (byte)count;
                    buf.DataLength = zc.Finish();
                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task,
                        //delegate (OutgoingPacket oPacket) { ResendPrimUpdates(tau, oPacket); }, false, false);
                        null, false);
                }
            }

            if (objectUpdateProbes is not null)
            {
                UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                byte[] data = buf.Data;
                Buffer.BlockCopy(ObjectUpdateCachedHeader, 0, data, 0, 7);

                Utils.UInt64ToBytesSafepos(m_scene.RegionInfo.RegionHandle, data, 7); // 15
                Utils.UInt16ToBytes(timeDilation, data, 15); // 17

                const int countposition = 17; // blocks count index
                int pos = countposition + 1;

                int count = 0;
                EntityUpdate eu;

                int indx = 0;
                while (indx < objectUpdateProbes.Count)
                {
                    unsafe
                    {
                        fixed(byte* bdata = &data[countposition + 1])
                        {
                            byte* ptr = bdata;
                            while (indx < objectUpdateProbes.Count)
                            {
                                eu = objectUpdateProbes[indx++];
                                SceneObjectPart sop = (SceneObjectPart)eu.Entity;
                                if (sop.ParentGroup is null || sop.ParentGroup.IsDeleted)
                                    continue;
                                uint primflags = m_scene.Permissions.GenerateClientFlags(sop, mysp);
                                if (mysp.UUID.NotEqual(sop.OwnerID))
                                    primflags &= ~(uint)PrimFlags.CreateSelected;
                                else
                                {
                                    if (sop.CreateSelected)
                                        primflags |= (uint)PrimFlags.CreateSelected;
                                    else
                                        primflags &= ~(uint)PrimFlags.CreateSelected;
                                }

                                Utils.UIntToBytes(sop.LocalId, ptr); ptr += 4;
                                Utils.UIntToBytes((uint)sop.PseudoCRC, ptr); ptr += 4; //WRONG
                                Utils.UIntToBytes(primflags, ptr); ptr += 4;
                                eu.Free();

                                ++count;
                                pos += 12;

                                if (pos > (LLUDPServer.MAXPAYLOAD - 13))
                                {
                                    // we need more packets
                                    UDPPacketBuffer newbuf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                                    Buffer.BlockCopy(data, 0, newbuf.Data, 0, countposition); // start is the same

                                    data[countposition] = (byte)count;
                                    buf.DataLength = pos;
                                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task, null, false);

                                    buf = newbuf;
                                    data = buf.Data;
                                    count = 0;
                                    pos = countposition + 1;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (count > 0)
                {
                    data[countposition] = (byte)count;
                    buf.DataLength = pos;
                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task, null, false);
                }
            }

            if (terseUpdates is not null)
            {
                int blocks = terseUpdates.Count;
                //List<EntityUpdate> tau = new List<EntityUpdate>(30);

                UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);

                //setup header and regioninfo block
                Buffer.BlockCopy(terseUpdateHeader, 0, buf.Data, 0, 7);
                Utils.UInt64ToBytesSafepos(m_scene.RegionInfo.RegionHandle, buf.Data, 7);
                Utils.UInt16ToBytes(timeDilation, buf.Data, 15);
                const int COUNTINDEX = 17;
                int pos = COUNTINDEX + 1;
                int lastpos = 0;
                int count = 0;
                EntityUpdate eu;
                int indx = 0;
                while (indx < terseUpdates.Count)
                {
                    unsafe
                    {
                        fixed(byte* bdata = &buf.Data[0])
                        {
                            byte* data = bdata + pos;
                            while (indx < terseUpdates.Count)
                            {
                                eu = terseUpdates[indx++];
                                lastpos = pos;
                                if(eu.Entity is ScenePresence sp)
                                    CreateAvatartImprovedTerseBlock(sp, ref data);
                                else
                                    CreatePartImprovedTerseBlock((SceneObjectPart)eu.Entity, ref data, (eu.Flags & PrimUpdateFlags.Textures) != 0);
                                eu.Free();
                                pos = (int)(data - bdata);
                                --blocks;
                                if (pos < LLUDPServer.MAXPAYLOAD)
                                {
                                    //tau.Add(eu);
                                    ++count;
                                }
                                else if (blocks > 0)
                                {
                                    // we need more packets
                                    UDPPacketBuffer newbuf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                                    Buffer.BlockCopy(buf.Data, 0, newbuf.Data, 0, COUNTINDEX); // start is the same
                                    // copy what we done in excess
                                    int extralen = pos - lastpos;
                                    if(extralen > 0)
                                        Buffer.BlockCopy(buf.Data, lastpos, newbuf.Data, 18, extralen);

                                    pos = COUNTINDEX + 1 + extralen;

                                    buf.Data[COUNTINDEX] = (byte)count;
                                    buf.DataLength = lastpos;
                                    // zero encode is not as spec
                                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task,
                                        //delegate (OutgoingPacket oPacket) { ResendPrimUpdates(tau, oPacket); }, true);
                                        null, true);

                                    //tau = new List<EntityUpdate>(30);
                                    //tau.Add(eu);
                                    count = 1;
                                    buf = newbuf;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (count > 0)
                {
                    buf.Data[COUNTINDEX] = (byte)count;
                    buf.DataLength = pos;
                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task,
                        //delegate (OutgoingPacket oPacket) { ResendPrimUpdates(tau, oPacket); }, true);
                        null, true);
                }
            }

            if (ObjectAnimationUpdates is not null)
            {
                SceneObjectPart sop;
                for (int indx = 0; indx < ObjectAnimationUpdates.Count; ++indx)
                {
                    sop = ObjectAnimationUpdates[indx];
                    if (sop.Animations is null)
                        continue;

                    SceneObjectGroup sog = sop.ParentGroup;
                    if (sog is null || sog.IsDeleted)
                        continue;

                    SceneObjectPart root = sog.RootPart;
                    if (root is null || root.Shape is null || !root.Shape.MeshFlagEntry)
                        continue;

                    int count = sop.GetAnimations(out UUID[] ids, out int[] seqs);

                    UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                    byte[] data = buf.Data;

                    //setup header
                    Buffer.BlockCopy(ObjectAnimationHeader, 0, data , 0, 7);

                    // sender block
                    sop.UUID.ToBytes(data, 7); // 23

                    //animations block
                    if (count > 255)
                        count = 255;

                    data[23] = (byte)count;

                    int pos = 24;
                    for(int i = 0; i < count; i++)
                    {
                        ids[i].ToBytes(data, pos); pos += 16;
                        Utils.IntToBytesSafepos(seqs[i], data, pos); pos += 4;
                    }

                    buf.DataLength = pos;
                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
                }
            }

            IEventQueue eq = Scene.RequestModuleInterface<IEventQueue>();
            if (needMaterials is not null && eq is not null)
            {
                foreach (SceneObjectPart sop in needMaterials)
                {
                    Primitive.RenderMaterials.RenderMaterialOverrideEntry[] overrides = sop.Shape.RenderMaterials.overrides;
                    /*
                    osUTF8 sbinner = LLSDxmlEncode2.Start();
                    LLSDxmlEncode2.AddArray(sbinner);
                    LLSDxmlEncode2.AddMap(sbinner);
                        LLSDxmlEncode2.AddElem("object_id", sop.UUID, sbinner);
                        LLSDxmlEncode2.AddElem("region_handle_low", (int)m_scene.RegionInfo.WorldLocX, sbinner);
                        LLSDxmlEncode2.AddElem("region_handle_high", (int)m_scene.RegionInfo.WorldLocY, sbinner);
                        LLSDxmlEncode2.AddArray("sides", sbinner);
                            foreach (Primitive.RenderMaterials.RenderMaterialOverrideEntry ovr in overrides)
                                LLSDxmlEncode2.AddElem(ovr.te_index, sbinner);
                        LLSDxmlEncode2.AddEndArray(sbinner);
                        LLSDxmlEncode2.AddArray("gltf_json", sbinner);
                            foreach (Primitive.RenderMaterials.RenderMaterialOverrideEntry ovr in overrides)
                            {
                                LLSDxmlEncode2.AddElem(ovr.data, sbinner);
                            }
                        LLSDxmlEncode2.AddEndArray(sbinner);
                    LLSDxmlEncode2.AddEndMapAndArray(sbinner);
                    LLSDxmlEncode2.AddEnd(sbinner);
                    */
                    OSDMap data = new OSDMap();
                    data["object_id"] = sop.UUID;
                    data["region_handle_x"]= (int)m_scene.RegionInfo.WorldLocX;
                    data["region_handle_y"]= (int)m_scene.RegionInfo.WorldLocY;
                    OSDArray sides = new OSDArray();
                    foreach (Primitive.RenderMaterials.RenderMaterialOverrideEntry ovr in overrides)
                        sides.Add(ovr.te_index);
                    data["sides"] = sides;

                    OSDArray gltf = new OSDArray();
                    foreach (Primitive.RenderMaterials.RenderMaterialOverrideEntry ovr in overrides)
                        gltf.Add(ovr.data);
                    data["gltf_json"] = gltf;

                    string inner = OSDParser.SerializeLLSDNotationFull(data);

                    osUTF8 sb = eq.StartEvent("LargeGenericMessage");
                    LLSDxmlEncode2.AddArrayAndMap("AgentData", sb);
                        LLSDxmlEncode2.AddElem("AgentID", AgentId, sb);
                        //LLSDxmlEncode2.AddElem("TransactionID", transationID.Value, sb);
                        //LLSDxmlEncode2.AddElem("SessionID", sessionID.Value, sb);
                    LLSDxmlEncode2.AddEndMapAndArray(sb);

                    LLSDxmlEncode2.AddArrayAndMap("MethodData", sb);
                    LLSDxmlEncode2.AddElem("Method", "GLTFMaterialOverride", sb);
                    LLSDxmlEncode2.AddElem("Invoice", UUID.Zero, sb);
                    LLSDxmlEncode2.AddEndMapAndArray(sb);

                    LLSDxmlEncode2.AddArrayAndMap("ParamList", sb);
                        //LLSDxmlEncode2.AddElem("Parameter", sbinner, sb);
                        LLSDxmlEncode2.AddElem("Parameter", inner, sb);
                    LLSDxmlEncode2.AddEndMapAndArray(sb);

                    //OSUTF8Cached.Release(sbinner);
                    eq.Enqueue(eq.EndEventToBytes(sb), AgentId);
                }
            }
            #endregion Packet Sending

            #region Handle deleted objects
            if (m_killRecord.Count > 0)
            {
                SendKillObject(m_killRecord);
                m_killRecord.Clear();
            }

            if(GroupsNeedFullUpdate.Count > 0)
            {
                foreach(SceneObjectGroup grp in GroupsNeedFullUpdate)
                {
                    lock (GroupsInView)
                        GroupsInView.Add(grp);
                    PrimUpdateFlags flags = PrimUpdateFlags.CancelKill;
                    if(viewerCache && grp.IsViewerCachable)
                        flags |= PrimUpdateFlags.UpdateProbe;
                    foreach (SceneObjectPart p in grp.Parts)
                        SendEntityUpdate(p, flags);
                }
            }

            #endregion
        }

        // hack.. dont use
/*
        public void SendPartFullUpdate(ISceneEntity ent, uint? parentID)
        {
            if (ent is SceneObjectPart)
            {
                SceneObjectPart part = (SceneObjectPart)ent;
                ObjectUpdatePacket packet = (ObjectUpdatePacket)PacketPool.Instance.GetPacket(PacketType.ObjectUpdate);
                packet.RegionData.RegionHandle = m_scene.RegionInfo.RegionHandle;
                packet.RegionData.TimeDilation = Utils.FloatToUInt16(m_scene.TimeDilation, 0.0f, 1.0f);
                packet.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[1];

                ObjectUpdatePacket.ObjectDataBlock blk = CreatePrimUpdateBlock(part, mysp);
                if (parentID.HasValue)
                {
                    blk.ParentID = parentID.Value;
                }

                packet.ObjectData[0] = blk;

                OutPacket(packet, ThrottleOutPacketType.Task, true);
            }

//            m_log.DebugFormat(
//                "[LLCLIENTVIEW]: Sent {0} updates in ProcessEntityUpdates() for {1} {2} in {3}",
//                updatesThisCall, Name, SceneAgent.IsChildAgent ? "child" : "root", Scene.Name);
//
        }
*/
        public void ReprioritizeUpdates()
        {
            m_entityUpdates.Reprioritize(UpdatePriorityHandler);
            CheckGroupsInView();
        }

        private bool CheckGroupsInViewBusy = false;

        public void CheckGroupsInView()
        {
            if(!m_scene.ObjectsCullingByDistance)
                return;

            if (!IsActive)
                return;

            if (CheckGroupsInViewBusy)
                return;

            if(SceneAgent is not ScenePresence mysp || mysp.IsDeleted)
                return;

            CheckGroupsInViewBusy = true;

            float cullingrange = mysp.DrawDistance + m_scene.ReprioritizationDistance + 16f;
            Vector3 mypos = mysp.AbsolutePosition;

            HashSet<SceneObjectGroup> NewGroupsInView = new();
            HashSet<SceneObjectGroup> GroupsNeedFullUpdate = new();
            List<SceneObjectGroup> kills = new();

            EntityBase[] entities = m_scene.Entities.GetEntities();
            foreach (EntityBase e in entities.AsSpan())
            {
                if (!IsActive)
                    return;

                if (e is SceneObjectGroup grp)
                {
                    if(grp.IsDeleted || grp.IsAttachment )
                        continue;

                    bool inviewgroups;
                    lock (GroupsInView)
                        inviewgroups = GroupsInView.Contains(grp);

                    //temp handling of sits
                    if(grp.GetSittingAvatarsCount() > 0)
                    {
                        if (!inviewgroups)
                            GroupsNeedFullUpdate.Add(grp);
                        NewGroupsInView.Add(grp);
                    }
                    else
                    {
                        Vector3 grppos = grp.getCenterOffset();
                        float dpos = (grppos - mypos).LengthSquared();

                        float maxview = grp.GetBoundsRadius() + cullingrange;
                        if (dpos > maxview * maxview)
                        {
                            if(inviewgroups)
                                kills.Add(grp);
                        }
                        else
                        {
                            if (!inviewgroups)
                                GroupsNeedFullUpdate.Add(grp);
                            NewGroupsInView.Add(grp);
                        }
                    }
                }
            }

            lock(GroupsInView)
                GroupsInView = NewGroupsInView;

            if (kills.Count > 0)
            {
                List<uint> partIDs = new();
                foreach(SceneObjectGroup grp in CollectionsMarshal.AsSpan(kills))
                {
                    SendEntityUpdate(grp.RootPart, PrimUpdateFlags.Kill);
                    foreach(SceneObjectPart p in grp.Parts)
                    {
                        if(p != grp.RootPart)
                            partIDs.Add(p.LocalId);
                    }
                }
                kills.Clear();
                if(partIDs.Count > 0)
                {
                    m_entityProps.Remove(partIDs);
                    m_entityUpdates.Remove(partIDs);
                }
            }

            if(GroupsNeedFullUpdate.Count > 0)
            {
                bool sendProbes = m_supportViewerCache && (m_viewerHandShakeFlags & 2) == 0;

                if(sendProbes)
                {
                    foreach (SceneObjectGroup grp in GroupsNeedFullUpdate)
                    {
                        PrimUpdateFlags flags = PrimUpdateFlags.CancelKill;
                        if (grp.IsViewerCachable)
                            flags |= PrimUpdateFlags.UpdateProbe;
                        foreach (SceneObjectPart p in grp.Parts)
                            SendEntityUpdate(p, flags);
                    }
                }
                else
                {
                    m_viewerHandShakeFlags &= ~2U; // nexttime send probes
                    PrimUpdateFlags flags = PrimUpdateFlags.CancelKill;
                    foreach (SceneObjectGroup grp in GroupsNeedFullUpdate)
                    {
                        foreach (SceneObjectPart p in grp.Parts)
                            SendEntityUpdate(p, flags);
                    }
                }
            }
            CheckGroupsInViewBusy = false;
        }

        private bool UpdatePriorityHandler(ref int priority, ISceneEntity entity)
        {
            if (!IsActive)
                return false;

            priority = m_prioritizer.GetUpdatePriority(this, entity);
            return true;
        }

        public void FlushPrimUpdates()
        {
            m_log.WarnFormat("[CLIENT]: Flushing prim updates to " + m_firstName + " " + m_lastName);

            while (m_entityUpdates.Count > 0)
                ProcessEntityUpdates(-1);
        }

        #endregion Primitive Packet/Data Sending Methods

        // These are used to implement an adaptive backoff in the number
        // of updates converted to packets. Since we don't want packets
        // to sit in the queue with old data, only convert enough updates
        // to packets that can be sent in 30ms.

        void HandleQueueEmpty(ThrottleOutPacketTypeFlags categories)
        {
            if(m_scene is null)
                return;

            if ((categories & ThrottleOutPacketTypeFlags.Task) != 0)
            {
                int maxUpdateBytes = m_udpClient.GetCatBytesCanSend(ThrottleOutPacketType.Task, 30);

                if (m_entityUpdates.Count > 0)
                    ProcessEntityUpdates(maxUpdateBytes);

                if (m_entityProps.Count > 0)
                    ProcessEntityPropertyRequests(maxUpdateBytes);
            }

            if ((categories & ThrottleOutPacketTypeFlags.Texture) != 0)
                ImageManager.ProcessImageQueue(m_udpServer.TextureSendLimit);
    }

        internal bool HandleHasUpdates(ThrottleOutPacketTypeFlags categories)
        {
            if ((categories & ThrottleOutPacketTypeFlags.Task) != 0)
            {
                if (m_entityUpdates.Count > 0)
                    return true;
                if (m_entityProps.Count > 0)
                    return true;
            }

            if ((categories & ThrottleOutPacketTypeFlags.Texture) != 0)
            {
                if (ImageManager.HasUpdates())
                    return true;
            }

            return false;
        }

        public void SendAssetUploadCompleteMessage(sbyte AssetType, bool Success, UUID AssetFullID)
        {
            AssetUploadCompletePacket newPack = new();
            newPack.AssetBlock.Type = AssetType;
            newPack.AssetBlock.Success = Success;
            newPack.AssetBlock.UUID = AssetFullID;
            newPack.Header.Zerocoded = true;
            OutPacket(newPack, ThrottleOutPacketType.Asset);
        }

        public void SendXferRequest(ulong XferID, short AssetType, UUID vFileID, byte FilePath, byte[] FileName)
        {
            RequestXferPacket newPack = new();
            newPack.XferID.ID = XferID;
            newPack.XferID.VFileType = AssetType;
            newPack.XferID.VFileID = vFileID;
            newPack.XferID.FilePath = FilePath;
            newPack.XferID.Filename = FileName;
            newPack.Header.Zerocoded = true;
            OutPacket(newPack, ThrottleOutPacketType.Asset);
        }

        public void SendConfirmXfer(ulong xferID, uint PacketID)
        {
            ConfirmXferPacketPacket newPack = new();
            newPack.XferID.ID = xferID;
            newPack.XferID.Packet = PacketID;
            newPack.Header.Zerocoded = true;
            OutPacket(newPack, ThrottleOutPacketType.Asset);
        }

        public void SendInitiateDownload(string simFileName, string clientFileName)
        {
            InitiateDownloadPacket newPack = new();
            newPack.AgentData.AgentID = m_agentId;
            newPack.FileData.SimFilename = Utils.StringToBytes(simFileName);
            newPack.FileData.ViewerFilename = Utils.StringToBytes(clientFileName);
            OutPacket(newPack, ThrottleOutPacketType.Asset);
        }

        public void SendImageFirstPart(
            ushort numParts, UUID ImageUUID, uint ImageSize, byte[] ImageData, byte imageCodec)
        {
            ImageDataPacket im = new();
            im.ImageID.Packets = numParts;
            im.ImageID.ID = ImageUUID;

            if (ImageSize > 0)
                im.ImageID.Size = ImageSize;

            im.ImageData.Data = ImageData;
            im.ImageID.Codec = imageCodec;
            im.Header.Zerocoded = true;
            OutPacket(im, ThrottleOutPacketType.Texture);
        }

        public void SendImageNextPart(ushort partNumber, UUID imageUuid, byte[] imageData)
        {
            ImagePacketPacket im = new();
            im.ImageID.Packet = partNumber;
            im.ImageID.ID = imageUuid;
            im.ImageData.Data = imageData;

            OutPacket(im, ThrottleOutPacketType.Texture);
        }

        public void SendImageNotFound(UUID imageid)
        {
            ImageNotInDatabasePacket notFoundPacket
            = (ImageNotInDatabasePacket)PacketPool.Instance.GetPacket(PacketType.ImageNotInDatabase);

            notFoundPacket.ImageID.ID = imageid;

            OutPacket(notFoundPacket, ThrottleOutPacketType.Texture);
        }

        public void SendShutdownConnectionNotice()
        {
            OutPacket(PacketPool.Instance.GetPacket(PacketType.DisableSimulator), ThrottleOutPacketType.Unknown);
        }

        static private readonly byte[] SimStatsHeader = new byte[] {
                0,
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 0xff, 0, 140 // ID 140 (low frequency bigendian)
                };

        public void SendSimStats(SimStats stats)
        {
            UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
            byte[] data = buf.Data;

            //setup header
            Buffer.BlockCopy(SimStatsHeader, 0, data, 0, 10);

            // Region Block
            Utils.UIntToBytesSafepos(stats.RegionX, data, 10);
            Utils.UIntToBytesSafepos(stats.RegionY, data, 14);
            Utils.UIntToBytesSafepos(stats.RegionFlags, data, 18);
            Utils.UIntToBytesSafepos(stats.ObjectCapacity, data, 22); // 26

            // stats
            data[26] = (byte)StatsIndex.ViewerArraySize;
            int pos = 27;

            int i = 0;
            for (; i < (int)StatsIndex.UnAckedBytes; ++i)
            {
                Utils.UIntToBytesSafepos(SimStats.StatsIndexID[i], data, pos); pos += 4;
                Utils.FloatToBytesSafepos(stats.StatsValues[i], data, pos); pos += 4;
            }

            // unack Bytes is in KB
            Utils.UIntToBytesSafepos(SimStats.StatsIndexID[i], data, pos); pos += 4;
            Utils.FloatToBytesSafepos(stats.StatsValues[i] / 1024, data, pos); pos += 4;

            ++i;
            for (; i < (int)StatsIndex.ViewerArraySize; ++i)
            {
                Utils.UIntToBytesSafepos(SimStats.StatsIndexID[i], data, pos); pos += 4;
                Utils.FloatToBytesSafepos(stats.StatsValues[i], data, pos); pos += 4;
            }

            //no PID
            Utils.IntToBytesSafepos(0, data, pos); pos += 4;

            // no regioninfo (extended flags)
            data[pos++] = 0; // = 1;
            //Utils.UInt64ToBytesSafepos(RegionFlagsExtended, data, pos); pos += 8;

            buf.DataLength = pos;
            m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
        }

        public void SendObjectPropertiesFamilyData(ISceneEntity entity, uint requestFlags)
        {
            m_entityProps.Enqueue(0, EntityUpdatesPool.Get(entity, (PrimUpdateFlags)requestFlags, true, false));
        }

        private void ResendPropertyUpdate(EntityUpdate update)
        {
            m_entityProps.Enqueue(0, update);
        }

        private void ResendPropertyUpdates(List<EntityUpdate> updates, OutgoingPacket oPacket)
        {
            // m_log.WarnFormat("[CLIENT] resending object property {0}",updates[0].UpdateTime);

            // Remove the update packet from the list of packets waiting for acknowledgement
            // because we are requeuing the list of updates. They will be resent in new packets
            // with the most recent state.
            m_udpClient.NeedAcks.Remove(oPacket.SequenceNumber);

            // Count this as a resent packet since we are going to requeue all of the updates contained in it
            Interlocked.Increment(ref m_udpClient.PacketsResent);

            // We're not going to worry about interlock yet since its not currently critical that this total count
            // is 100% correct
            m_udpServer.PacketsResentCount++;

            foreach (EntityUpdate update in updates)
                ResendPropertyUpdate(update);
        }

        public void SendObjectPropertiesReply(ISceneEntity entity)
        {
            m_entityProps.Enqueue(0, EntityUpdatesPool.Get(entity,0,false,true));
        }

        static private readonly byte[] ObjectPropertyUpdateHeader = new byte[] {
                Helpers.MSG_RELIABLE | Helpers.MSG_ZEROCODED,
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 9 // ID (medium frequency)
                };

        static private readonly byte[] ObjectFamilyUpdateHeader = new byte[] {
                Helpers.MSG_RELIABLE | Helpers.MSG_ZEROCODED,
                0, 0, 0, 0, // sequence number
                0, // extra
                0xff, 10 // ID (medium frequency)
                };

        private void ProcessEntityPropertyRequests(int maxUpdateBytes)
        {
            List<EntityUpdate> objectPropertiesUpdates = null;
            List<EntityUpdate> objectPropertiesFamilyUpdates = null;
            List<EntityUpdate> used = new(64);
            List<SceneObjectPart> needPhysics = null;

            // bool orderedDequeue = m_scene.UpdatePrioritizationScheme  == UpdatePrioritizationSchemes.SimpleAngularDistance;
            bool orderedDequeue = false; // for now
            EntityUpdate update;

            while (maxUpdateBytes > 0)
            {
                if(orderedDequeue)
                {
                    if (!m_entityProps.TryOrderedDequeue(out update))
                        break;
                }
                else
                {
                    if (!m_entityProps.TryDequeue(out update))
                        break;
                }

                if (update.PropsFlags == 0)
                {
                    update.Free();
                    continue;
                }

                if(update.Entity is not SceneObjectPart sop)
                {
                    update.Free();
                    continue;
                }

                used.Add(update);

                if ((update.PropsFlags & ObjectPropertyUpdateFlags.Family) != 0)
                {
                    objectPropertiesFamilyUpdates ??= new List<EntityUpdate>();
                    objectPropertiesFamilyUpdates.Add(update);
                    maxUpdateBytes -= 100;
                }

                if ((update.PropsFlags & ObjectPropertyUpdateFlags.Object) != 0)
                {
                    needPhysics ??= new List<SceneObjectPart>();
                    needPhysics.Add(sop);
                    objectPropertiesUpdates ??= new List<EntityUpdate>();
                    objectPropertiesUpdates.Add(update);
                    maxUpdateBytes -= 200; // aprox
                }
            }

            if (objectPropertiesUpdates != null)
            {
                int blocks = objectPropertiesUpdates.Count;
                //List<EntityUpdate> tau = new List<EntityUpdate>(30);

                UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                Buffer.BlockCopy(ObjectPropertyUpdateHeader, 0, buf.Data, 0, 8);

                LLUDPZeroEncoder zc = new(buf.Data);
                zc.Position = 8;

                zc.AddByte(1); // tmp block count

                int countposition = zc.Position - 1;

                int lastpos;
                int lastzc;

                int count = 0;
                foreach (EntityUpdate eu in objectPropertiesUpdates)
                {
                    lastpos = zc.Position;
                    lastzc = zc.ZeroCount;
                    CreateObjectPropertiesBlock((SceneObjectPart)eu.Entity, zc);
                    if (zc.Position < LLUDPServer.MAXPAYLOAD)
                    {
                        //tau.Add(eu);
                        ++count;
                        --blocks;
                    }
                    else if (blocks > 0)
                    {
                        // we need more packets
                        UDPPacketBuffer newbuf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                        Buffer.BlockCopy(buf.Data, 0, newbuf.Data, 0, countposition); // start is the same

                        buf.Data[countposition] = (byte)count;
                        // get pending zeros at cut point
                        if (lastzc > 0)
                        {
                            buf.Data[lastpos++] = 0;
                            buf.Data[lastpos++] = (byte)lastzc;
                        }
                        buf.DataLength = lastpos;

                        //m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task,
                        //    delegate (OutgoingPacket oPacket) { ResendPrimUpdates(tau, oPacket); }, false, false);
                        m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
                        buf = newbuf;
                        zc.Data = buf.Data;
                        zc.ZeroCount = 0;
                        zc.Position = countposition + 1;
                        // im lazy now, just do last again
                        CreateObjectPropertiesBlock((SceneObjectPart)eu.Entity, zc);

                        //tau = new List<EntityUpdate>(30);
                        //tau.Add(eu);
                        count = 1;
                        --blocks;
                    }
                }

                if (count > 0)
                {
                    buf.Data[countposition] = (byte)count;
                    buf.DataLength = zc.Finish();
                    //m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task,
                    //    delegate (OutgoingPacket oPacket) { ResendPrimUpdates(tau, oPacket); }, false, false);
                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
                }
            }

            if (objectPropertiesFamilyUpdates != null)
            {
                foreach (EntityUpdate eu in objectPropertiesFamilyUpdates)
                {
                    UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);
                    Buffer.BlockCopy(ObjectFamilyUpdateHeader, 0, buf.Data, 0, 8);

                    LLUDPZeroEncoder zc = new(buf.Data);
                    zc.Position = 8;

                    CreateObjectPropertiesFamilyBlock((SceneObjectPart)eu.Entity, eu.Flags, zc);
                    buf.DataLength = zc.Finish();
                    //List<EntityUpdate> tau = new List<EntityUpdate>(1);
                    //tau.Add(new ObjectPropertyUpdate((ISceneEntity) eu, (uint)eu.Flags, true, false));
                    //m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task,
                    //    delegate (OutgoingPacket oPacket) { ResendPrimUpdates(tau, oPacket); }, false, false);
                    m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task);
                }
            }

            if (needPhysics != null)
            {
                IEventQueue eq = Scene.RequestModuleInterface<IEventQueue>();
                if(eq != null)
                {
                    osUTF8 sb = eq.StartEvent("ObjectPhysicsProperties");
                    LLSDxmlEncode2.AddArray("ObjectData", sb);
                    foreach (SceneObjectPart sop in needPhysics)
                    {
                        LLSDxmlEncode2.AddMap(sb);
                            LLSDxmlEncode2.AddElem("LocalID",(int)sop.LocalId, sb);
                            LLSDxmlEncode2.AddElem("Density", sop.Density, sb);
                            LLSDxmlEncode2.AddElem("Friction", sop.Friction, sb);
                            LLSDxmlEncode2.AddElem("GravityMultiplier", sop.GravityModifier, sb);
                            LLSDxmlEncode2.AddElem("Restitution", sop.Restitution, sb);
                            LLSDxmlEncode2.AddElem("PhysicsShapeType", (int)sop.PhysicsShapeType, sb);
                        LLSDxmlEncode2.AddEndMap(sb);
                    }
                    LLSDxmlEncode2.AddEndArray(sb);
                    eq.Enqueue(eq.EndEventToBytes(sb), m_agentId);
                }
            }

            foreach(EntityUpdate eu in used)
                eu.Free();
        }

        private static void CreateObjectPropertiesFamilyBlock(SceneObjectPart sop, PrimUpdateFlags requestFlags, LLUDPZeroEncoder zc)
        {
            SceneObjectPart root = sop.ParentGroup.RootPart;

            zc.AddUInt((uint)requestFlags);
            zc.AddUUID(sop.UUID);
            if (sop.OwnerID == sop.GroupID)
                zc.AddZeros(16);
            else
                zc.AddUUID(sop.OwnerID);
            zc.AddUUID(sop.GroupID);

            zc.AddUInt(root.BaseMask);
            zc.AddUInt(root.OwnerMask);
            zc.AddUInt(root.GroupMask);
            zc.AddUInt(root.EveryoneMask);
            zc.AddUInt(root.NextOwnerMask);

            zc.AddZeros(4); // int ownership cost

            //sale info block
            zc.AddByte(root.ObjectSaleType);
            zc.AddInt(root.SalePrice);

            zc.AddUInt(sop.Category); //Category

            zc.AddUUID(sop.LastOwnerID);

            //name
            zc.AddShortLimitedUTF8(sop.osUTF8Name);

            //Description
            zc.AddShortLimitedUTF8(sop.osUTF8Description);
        }

        private static void CreateObjectPropertiesBlock(SceneObjectPart sop, LLUDPZeroEncoder zc)
        {
            SceneObjectPart root = sop.ParentGroup.RootPart;

            zc.AddUUID(sop.UUID);
            zc.AddUUID(sop.CreatorID);
            if (sop.OwnerID == sop.GroupID)
                zc.AddZeros(16);
            else
                zc.AddUUID(sop.OwnerID);
            zc.AddUUID(sop.GroupID);

            zc.AddUInt64((ulong)sop.CreationDate * 1000000UL);

            zc.AddUInt(root.BaseMask);
            zc.AddUInt(root.OwnerMask);
            zc.AddUInt(root.GroupMask);
            zc.AddUInt(root.EveryoneMask);
            zc.AddUInt(root.NextOwnerMask);

            zc.AddZeros(4); // int ownership cost

            //sale info block
            zc.AddByte(root.ObjectSaleType);
            zc.AddInt(root.SalePrice);

            //aggregated perms we may will need to fix this
            zc.AddByte(0); //AggregatePerms
            zc.AddByte(0); //AggregatePermTextures;
            zc.AddByte(0); //AggregatePermTexturesOwner

            //inventory info
            zc.AddUInt(sop.Category); //Category
            zc.AddInt16((short)sop.InventorySerial);
            zc.AddUUID(sop.FromUserInventoryItemID);
            zc.AddUUID(UUID.Zero); //FolderID
            zc.AddUUID(UUID.Zero); //FromTaskID

            zc.AddUUID(sop.LastOwnerID);

            //name
            zc.AddShortLimitedUTF8(sop.osUTF8Name);

            //Description
            zc.AddShortLimitedUTF8(sop.osUTF8Description);

            // touch name
            zc.AddShortLimitedUTF8(sop.osUTF8TouchName);

            // sit name
            zc.AddShortLimitedUTF8(sop.osUTF8SitName);

            //texture ids block
            // still not sending, not clear the impact on viewers, if any.
            // does seem redundant
            // to send we will need proper list of face texture ids without having to unpack texture entry all the time
            zc.AddZeros(1);
        }

        #region Estate Data Sending Methods

        private static bool convertParamStringToBool(byte[] field)
        {
            string s = Utils.BytesToString(field);
            if (s == "1" || s.ToLower() == "y" || s.ToLower() == "yes" || s.ToLower() == "t" || s.ToLower() == "true")
            {
                return true;
            }
            return false;
        }

        public void SendEstateList(UUID invoice, int code, UUID[] Data, uint estateID)
        {
            int TotalnumberIDs = Data.Length;
            int numberIDs;
            int IDIndex = 0;

            do
            {
                if(TotalnumberIDs > 63)
                    numberIDs = 63;
                else
                    numberIDs = TotalnumberIDs;

                TotalnumberIDs -= numberIDs;

                EstateOwnerMessagePacket packet = new();
                packet.AgentData.TransactionID = UUID.Random();
                packet.AgentData.AgentID = m_agentId;
                packet.AgentData.SessionID = SessionId;
                packet.MethodData.Invoice = invoice;
                packet.MethodData.Method = Utils.StringToBytes("setaccess");

                EstateOwnerMessagePacket.ParamListBlock[] returnblock = new EstateOwnerMessagePacket.ParamListBlock[6 + numberIDs];

                for (int i = 0; i < (6 + numberIDs); i++)
                {
                    returnblock[i] = new EstateOwnerMessagePacket.ParamListBlock();
                }

                returnblock[0].Parameter = Utils.StringToBytes(estateID.ToString());
                returnblock[1].Parameter = Utils.StringToBytes(code.ToString());

                if((code & 1) != 0) // allowagents
                    returnblock[2].Parameter = Utils.StringToBytes(numberIDs.ToString());
                else
                    returnblock[2].Parameter = Utils.StringToBytes("0");

                if((code & 2) != 0) // groups
                    returnblock[3].Parameter = Utils.StringToBytes(numberIDs.ToString());
                else
                    returnblock[3].Parameter = Utils.StringToBytes("0");

                if((code & 4) != 0) // bans
                    returnblock[4].Parameter = Utils.StringToBytes(numberIDs.ToString());
                else
                    returnblock[4].Parameter = Utils.StringToBytes("0");

                if((code & 8) != 0) // managers
                    returnblock[5].Parameter = Utils.StringToBytes(numberIDs.ToString());
                else
                    returnblock[5].Parameter = Utils.StringToBytes("0");

                int j = 6;

                for (int i = 0; i < numberIDs; i++)
                {
                    returnblock[j].Parameter = Data[IDIndex].GetBytes();
                    j++;
                    IDIndex++;
                }
                packet.ParamList = returnblock;
                packet.Header.Reliable = true;
                OutPacket(packet, ThrottleOutPacketType.Task);
            } while (TotalnumberIDs > 0);
        }

        public void SendBannedUserList(UUID invoice, EstateBan[] bl, uint estateID)
        {
            List<UUID> BannedUsers = new();
            for (int i = 0; i < bl.Length; i++)
            {
                if (bl[i] is null)
                    continue;
                if (bl[i].BannedUserID.IsZero())
                    continue;
                BannedUsers.Add(bl[i].BannedUserID);
            }
            SendEstateList(invoice, 4, BannedUsers.ToArray(), estateID);
        }

        public void SendRegionInfoToEstateMenu(RegionInfoForEstateMenuArgs args)
        {
            RegionInfoPacket rinfopack = new();
            RegionInfoPacket.RegionInfoBlock rinfoblk = new();
            rinfopack.AgentData.AgentID = m_agentId;
            rinfopack.AgentData.SessionID = SessionId;
            rinfoblk.BillableFactor = args.billableFactor;
            rinfoblk.EstateID = args.estateID;
            rinfoblk.MaxAgents = (byte)args.maxAgents;
            rinfoblk.ObjectBonusFactor = args.objectBonusFactor;
            rinfoblk.ParentEstateID = args.parentEstateID;
            rinfoblk.PricePerMeter = args.pricePerMeter;
            rinfoblk.RedirectGridX = args.redirectGridX;
            rinfoblk.RedirectGridY = args.redirectGridY;
            rinfoblk.RegionFlags = args.regionFlags;
            rinfoblk.SimAccess = args.simAccess;
            rinfoblk.SunHour = args.sunHour;
            rinfoblk.TerrainLowerLimit = args.terrainLowerLimit;
            rinfoblk.TerrainRaiseLimit = args.terrainRaiseLimit;
            rinfoblk.UseEstateSun = args.useEstateSun;
            rinfoblk.WaterHeight = args.waterHeight;
            rinfoblk.SimName = Utils.StringToBytes(args.simName);

            rinfopack.RegionInfo2 = new RegionInfoPacket.RegionInfo2Block
            {
                HardMaxAgents = (uint)args.AgentCapacity,
                HardMaxObjects = (uint)args.ObjectsCapacity,
                MaxAgents32 = (uint)args.maxAgents,
                ProductName = Util.StringToBytes256(args.regionType),
                ProductSKU = Utils.EmptyBytes
            };

            rinfopack.HasVariableBlocks = true;
            rinfopack.RegionInfo = rinfoblk;
            rinfopack.AgentData = new RegionInfoPacket.AgentDataBlock
            {
                AgentID = m_agentId,
                SessionID = m_sessionId
            };
            rinfopack.RegionInfo3 = Array.Empty<RegionInfoPacket.RegionInfo3Block>();

            OutPacket(rinfopack, ThrottleOutPacketType.Task);
        }

        public void SendEstateCovenantInformation(UUID covenant)
        {
            //m_log.DebugFormat("[LLCLIENTVIEW]: Sending estate covenant asset id of {0} to {1}", covenant, Name);

            EstateCovenantReplyPacket einfopack = new();
            einfopack.Data.CovenantID = covenant;
            einfopack.Data.CovenantTimestamp = (uint)m_scene.RegionInfo.RegionSettings.CovenantChangedDateTime;
            einfopack.Data.EstateOwnerID = m_scene.RegionInfo.EstateSettings.EstateOwner;
            einfopack.Data.EstateName = Utils.StringToBytes(m_scene.RegionInfo.EstateSettings.EstateName);
            OutPacket(einfopack, ThrottleOutPacketType.Task);
        }

        public void SendDetailedEstateData(
            UUID invoice, string estateName, uint estateID, uint parentEstate, uint estateFlags, uint sunPosition,
            UUID covenant, uint covenantChanged, string abuseEmail, UUID estateOwner)
        {
            //m_log.DebugFormat(
            //   "[LLCLIENTVIEW]: Sending detailed estate data to {0} with covenant asset id {1}", Name, covenant);

            EstateOwnerMessagePacket packet = new();
            packet.MethodData.Invoice = invoice;
            packet.AgentData.TransactionID = UUID.Random();
            packet.MethodData.Method = Utils.StringToBytes("estateupdateinfo");
            EstateOwnerMessagePacket.ParamListBlock[] returnblock = new EstateOwnerMessagePacket.ParamListBlock[10];

            for (int i = 0; i < 10; i++)
            {
                returnblock[i] = new EstateOwnerMessagePacket.ParamListBlock();
            }

            //Sending Estate Settings
            returnblock[0].Parameter = Utils.StringToBytes(estateName);
            returnblock[1].Parameter = Utils.StringToBytes(estateOwner.ToString());
            returnblock[2].Parameter = Utils.StringToBytes(estateID.ToString());

            returnblock[3].Parameter = Utils.StringToBytes(estateFlags.ToString());
            returnblock[4].Parameter = Utils.StringToBytes(sunPosition.ToString());
            returnblock[5].Parameter = Utils.StringToBytes(parentEstate.ToString());
            returnblock[6].Parameter = Utils.StringToBytes(covenant.ToString());
            returnblock[7].Parameter = Utils.StringToBytes(covenantChanged.ToString());
            returnblock[8].Parameter = Utils.StringToBytes("1"); // what is this?
            returnblock[9].Parameter = Utils.StringToBytes(abuseEmail);

            packet.ParamList = returnblock;
            //m_log.Debug("[ESTATE]: SIM--->" + packet.ToString());
            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendTelehubInfo(UUID ObjectID, string ObjectName, Vector3 ObjectPos, Quaternion ObjectRot, List<Vector3> SpawnPoint)
        {
            TelehubInfoPacket packet = (TelehubInfoPacket)PacketPool.Instance.GetPacket(PacketType.TelehubInfo);
            packet.TelehubBlock.ObjectID = ObjectID;
            packet.TelehubBlock.ObjectName = Utils.StringToBytes(ObjectName);
            packet.TelehubBlock.TelehubPos = ObjectPos;
            packet.TelehubBlock.TelehubRot = ObjectRot;

            packet.SpawnPointBlock = new TelehubInfoPacket.SpawnPointBlockBlock[SpawnPoint.Count];
            for (int n = 0; n < SpawnPoint.Count; n++)
            {
                packet.SpawnPointBlock[n] = new TelehubInfoPacket.SpawnPointBlockBlock{SpawnPointPos = SpawnPoint[n]};
            }

            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        #endregion

        #region Land Data Sending Methods

        public void SendLandParcelOverlay(byte[] data, int sequence_id)
        {
            ParcelOverlayPacket packet = (ParcelOverlayPacket)PacketPool.Instance.GetPacket(PacketType.ParcelOverlay);
            packet.ParcelData.Data = data;
            packet.ParcelData.SequenceID = sequence_id;
            OutPacket(packet, ThrottleOutPacketType.Land);
        }

        public void SendLandProperties(
             int sequence_id, bool snap_selection, int request_result, ILandObject lo,
             float simObjectBonusFactor, int parcelObjectCapacity, int simObjectCapacity, uint regionFlags)
        {
            //m_log.DebugFormat("[LLCLIENTVIEW]: Sending land properties for {0} to {1}", lo.LandData.GlobalID, Name);

            IEventQueue eq = Scene.RequestModuleInterface<IEventQueue>();
            if (eq is null)
            {
                m_log.Warn("[LLCLIENTVIEW]: No EQ Interface when sending parcel data.");
                return;
            }

            LandData landData = lo.LandData;
            IPrimCounts pc = lo.PrimCounts;

            int cap = 4 * landData.Bitmap.Length / 3 + 2048;
            osUTF8 sb = eq.StartEvent("ParcelProperties", cap);

            LLSDxmlEncode2.AddArrayAndMap("ParcelData", sb);

            LLSDxmlEncode2.AddElem("LocalID", landData.LocalID, sb);
            LLSDxmlEncode2.AddElem("AABBMax", landData.AABBMax, sb);
            LLSDxmlEncode2.AddElem("AABBMin", landData.AABBMin, sb);
            LLSDxmlEncode2.AddElem("Area", landData.Area, sb);
            LLSDxmlEncode2.AddElem("AuctionID", (int)landData.AuctionID, sb);
            LLSDxmlEncode2.AddElem("AuthBuyerID", landData.AuthBuyerID, sb);
            LLSDxmlEncode2.AddElem("Bitmap", landData.Bitmap, sb);
            LLSDxmlEncode2.AddElem("Category", (int)landData.Category, sb);
            LLSDxmlEncode2.AddElem("ClaimDate", Util.ToDateTime(landData.ClaimDate), sb);
            LLSDxmlEncode2.AddElem("ClaimPrice", landData.ClaimPrice, sb);
            LLSDxmlEncode2.AddElem("Desc", landData.Description, sb);
            LLSDxmlEncode2.AddElem("ParcelFlags", landData.Flags, sb);
            LLSDxmlEncode2.AddElem("GroupID", landData.GroupID, sb);
            LLSDxmlEncode2.AddElem("GroupPrims", pc.Group, sb);
            LLSDxmlEncode2.AddElem("IsGroupOwned", landData.IsGroupOwned, sb);
            LLSDxmlEncode2.AddElem("LandingType", (int)landData.LandingType, sb);
            if (landData.Area > 0)
                LLSDxmlEncode2.AddElem("MaxPrims", parcelObjectCapacity, sb);
            else
                LLSDxmlEncode2.AddElem("MaxPrims", (int)0, sb);
            LLSDxmlEncode2.AddElem("MediaID", landData.MediaID, sb);
            LLSDxmlEncode2.AddElem("MediaURL", landData.MediaURL, sb);
            LLSDxmlEncode2.AddElem("MediaAutoScale", landData.MediaAutoScale != 0, sb);
            LLSDxmlEncode2.AddElem("MusicURL", landData.MusicURL, sb);
            LLSDxmlEncode2.AddElem("Name", landData.Name, sb);
            LLSDxmlEncode2.AddElem("OtherCleanTime", landData.OtherCleanTime, sb);
            LLSDxmlEncode2.AddElem("OtherCount", (int)0 , sb); //TODO
            LLSDxmlEncode2.AddElem("OtherPrims", pc.Others, sb);
            LLSDxmlEncode2.AddElem("OwnerID", landData.OwnerID, sb);
            LLSDxmlEncode2.AddElem("OwnerPrims", pc.Owner, sb);
            LLSDxmlEncode2.AddElem("ParcelPrimBonus", simObjectBonusFactor, sb);
            LLSDxmlEncode2.AddElem("PassHours", landData.PassHours, sb);
            LLSDxmlEncode2.AddElem("PassPrice", landData.PassPrice, sb);
            LLSDxmlEncode2.AddElem("PublicCount", (int)0, sb); //TODO
            LLSDxmlEncode2.AddElem("RegionDenyAnonymous", (regionFlags & (uint)RegionFlags.DenyAnonymous) != 0, sb);
            LLSDxmlEncode2.AddElem("RegionDenyIdentified", false, sb);
            LLSDxmlEncode2.AddElem("RegionDenyTransacted", false, sb);
            LLSDxmlEncode2.AddElem("RegionPushOverride", (regionFlags & (uint)RegionFlags.RestrictPushObject) != 0, sb);
            LLSDxmlEncode2.AddElem("RentPrice", (int) 0, sb);
            LLSDxmlEncode2.AddElem("RequestResult", request_result, sb);
            LLSDxmlEncode2.AddElem("SalePrice", landData.SalePrice, sb);
            LLSDxmlEncode2.AddElem("SelectedPrims", pc.Selected, sb);
            LLSDxmlEncode2.AddElem("SelfCount", (int)0, sb); //TODO
            LLSDxmlEncode2.AddElem("SequenceID", sequence_id, sb);
            if (landData.SimwideArea > 0)
                LLSDxmlEncode2.AddElem("SimWideMaxPrims", lo.GetSimulatorMaxPrimCount(), sb);
            else
                LLSDxmlEncode2.AddElem("SimWideMaxPrims", (int)0, sb);
            LLSDxmlEncode2.AddElem("SimWideTotalPrims", pc.Simulator, sb);
            LLSDxmlEncode2.AddElem("SnapSelection", snap_selection, sb);
            LLSDxmlEncode2.AddElem("SnapshotID", landData.SnapshotID, sb);
            LLSDxmlEncode2.AddElem("Status", (int)landData.Status, sb);
            LLSDxmlEncode2.AddElem("TotalPrims", pc.Total, sb);
            LLSDxmlEncode2.AddElem("UserLocation", landData.UserLocation, sb);
            LLSDxmlEncode2.AddElem("UserLookAt", landData.UserLookAt, sb);
            LLSDxmlEncode2.AddElem("SeeAVs", landData.SeeAVs, sb);
            LLSDxmlEncode2.AddElem("AnyAVSounds", landData.AnyAVSounds, sb);
            LLSDxmlEncode2.AddElem("GroupAVSounds", landData.GroupAVSounds, sb);

            LLSDxmlEncode2.AddEndMapAndArray(sb);

            LLSDxmlEncode2.AddArrayAndMap("MediaData", sb);

            LLSDxmlEncode2.AddElem("MediaDesc", landData.MediaDescription, sb);
            LLSDxmlEncode2.AddElem("MediaHeight", landData.MediaHeight, sb);
            LLSDxmlEncode2.AddElem("MediaWidth", landData.MediaWidth, sb);
            LLSDxmlEncode2.AddElem("MediaLoop", landData.MediaLoop, sb);
            LLSDxmlEncode2.AddElem("MediaType", landData.MediaType, sb);
            //LLSDxmlEncode2.AddElem("ObscureMedia", landData.ObscureMedia, sb);
            LLSDxmlEncode2.AddElem("ObscureMedia", false, sb); //obsolete
            //LLSDxmlEncode2.AddElem("ObscureMusic", landData.ObscureMusic, sb);
            LLSDxmlEncode2.AddElem("ObscureMusic", false, sb); //obsolete

            LLSDxmlEncode2.AddEndMapAndArray(sb);

            LLSDxmlEncode2.AddArrayAndMap("ParcelExtendedFlags", sb); // obscure moap
            LLSDxmlEncode2.AddElem("Flags", (uint)(landData.ObscureMedia ? 1 : (int)0), sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            LLSDxmlEncode2.AddArrayAndMap("AgeVerificationBlock", sb);

            LLSDxmlEncode2.AddElem("RegionDenyAgeUnverified", (regionFlags & (uint)RegionFlags.DenyAgeUnverified) != 0, sb);

            LLSDxmlEncode2.AddEndMapAndArray(sb);

            bool allowenvovr = (regionFlags & (uint)RegionFlags.AllowEnvironmentOverride) != 0;
            int envVersion;
            if(allowenvovr)
            {
                if (SceneAgent is ScenePresence sp && sp.EnvironmentVersion > 0)
                    envVersion = -1;
                else
                    envVersion = landData.EnvironmentVersion;
            }
            else
                envVersion = -1;

            LLSDxmlEncode2.AddArrayAndMap("ParcelEnvironmentBlock", sb);
            LLSDxmlEncode2.AddElem("ParcelEnvironmentVersion", envVersion, sb);
            LLSDxmlEncode2.AddElem("RegionAllowEnvironmentOverride", allowenvovr, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            bool accessovr = !Scene.RegionInfo.EstateSettings.TaxFree;
            LLSDxmlEncode2.AddArrayAndMap("RegionAllowAccessBlock", sb);
            LLSDxmlEncode2.AddElem("RegionAllowAccessOverride", accessovr, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            eq.Enqueue(eq.EndEventToBytes(sb), m_agentId);
        }

        public void SendLandAccessListData(List<LandAccessEntry> accessList, uint accessFlag, int localLandID)
        {
            ParcelAccessListReplyPacket replyPacket = (ParcelAccessListReplyPacket)PacketPool.Instance.GetPacket(PacketType.ParcelAccessListReply);
            replyPacket.Data.AgentID = m_agentId;
            replyPacket.Data.Flags = accessFlag;
            replyPacket.Data.LocalID = localLandID;
            replyPacket.Data.SequenceID = 0;

            List<ParcelAccessListReplyPacket.ListBlock> list = new();
            foreach (LandAccessEntry entry in accessList)
            {
                ParcelAccessListReplyPacket.ListBlock block = new()
                {
                    Flags = accessFlag,
                    ID = entry.AgentID,
                    Time = entry.Expires
                };
                list.Add(block);
            }

            replyPacket.List = list.ToArray();
            replyPacket.Header.Zerocoded = true;
            OutPacket(replyPacket, ThrottleOutPacketType.Task);
        }

        public void SendForceClientSelectObjects(List<uint> ObjectIDs)
        {
//            m_log.DebugFormat("[LLCLIENTVIEW] sending select with {0} objects", ObjectIDs.Count);

            bool firstCall = true;
            const int MAX_OBJECTS_PER_PACKET = 251;
            ForceObjectSelectPacket pack = (ForceObjectSelectPacket)PacketPool.Instance.GetPacket(PacketType.ForceObjectSelect);
            ForceObjectSelectPacket.DataBlock[] data;
            while (ObjectIDs.Count > 0)
            {
                if (firstCall)
                {
                    pack._Header.ResetList = true;
                    firstCall = false;
                }
                else
                {
                    pack._Header.ResetList = false;
                }

                if (ObjectIDs.Count > MAX_OBJECTS_PER_PACKET)
                {
                    data = new ForceObjectSelectPacket.DataBlock[MAX_OBJECTS_PER_PACKET];
                }
                else
                {
                    data = new ForceObjectSelectPacket.DataBlock[ObjectIDs.Count];
                }

                int i;
                for (i = 0; i < MAX_OBJECTS_PER_PACKET && ObjectIDs.Count > 0; i++)
                {
                    data[i] = new ForceObjectSelectPacket.DataBlock
                    {
                        LocalID = Convert.ToUInt32(ObjectIDs[0])
                    };
                    ObjectIDs.RemoveAt(0);
                }
                pack.Data = data;
                pack.Header.Zerocoded = true;
                OutPacket(pack, ThrottleOutPacketType.Task);
            }
        }

        public void SendCameraConstraint(Vector4 ConstraintPlane)
        {
            CameraConstraintPacket cpack = (CameraConstraintPacket)PacketPool.Instance.GetPacket(PacketType.CameraConstraint);
            cpack.CameraCollidePlane = new CameraConstraintPacket.CameraCollidePlaneBlock
            {
                Plane = ConstraintPlane
            };
            //m_log.DebugFormat("[CLIENTVIEW]: Constraint {0}", ConstraintPlane);
            OutPacket(cpack, ThrottleOutPacketType.Task);
        }

        public void SendLandObjectOwners(LandData land, List<UUID> groups, Dictionary<UUID, int> ownersAndCount)
        {
            int notifyCount = ownersAndCount.Count;
            ParcelObjectOwnersReplyPacket pack = (ParcelObjectOwnersReplyPacket)PacketPool.Instance.GetPacket(PacketType.ParcelObjectOwnersReply);

            if (notifyCount > 0)
            {
                ParcelObjectOwnersReplyPacket.DataBlock[] dataBlock
                    = new ParcelObjectOwnersReplyPacket.DataBlock[notifyCount];

                int num = 0;
                foreach (UUID owner in ownersAndCount.Keys)
                {
                    dataBlock[num++] = new ParcelObjectOwnersReplyPacket.DataBlock
                    {
                        Count = ownersAndCount[owner],
                        OnlineStatus = true, //TODO: fix me later
                        OwnerID = owner,
                        IsGroupOwned = land.GroupID.Equals(owner) || groups.Contains(owner)
                    };
                    if (num >= notifyCount)
                        break;
                }

                pack.Data = dataBlock;
            }
            else
            {
                pack.Data = Array.Empty<ParcelObjectOwnersReplyPacket.DataBlock>();
            }
            pack.Header.Zerocoded = true;
            OutPacket(pack, ThrottleOutPacketType.Task);
        }

        #endregion

        #region Helper Methods
        private static void ClampVectorForUint(ref Vector3 v, float max)
        {
            float a, b;

            a = MathF.Abs(v.X);
            b = MathF.Abs(v.Y);
            if (b > a)
                a = b;
            b = MathF.Abs(v.Z);
            if (b > a)
                a = b;

            if (a > max)
            {
                a = max / a;
                v.X *= a;
                v.Y *= a;
                v.Z *= a;
            }
        }

        protected static void CreateImprovedTerseBlock(ISceneEntity entity, byte[] data, ref int pos, bool includeTexture)
        {
            #region ScenePresence/SOP Handling

            bool avatar = (entity is ScenePresence);
            uint attachPoint;
            Vector4 collisionPlane;
            Vector3 position, velocity, acceleration, angularVelocity;
            Quaternion rotation;
            byte datasize;
            byte[] te = null;

            if (avatar)
            {
                ScenePresence presence = (ScenePresence)entity;

                position = presence.OffsetPosition;
                velocity = presence.Velocity;
                acceleration = Vector3.Zero;
                rotation = presence.Rotation;
                // tpvs can only see rotations around Z in some cases
                if (!presence.Flying && !presence.IsSatOnObject)
                {
                    rotation.X = 0f;
                    rotation.Y = 0f;
                }
                rotation.Normalize();
                angularVelocity = presence.AngularVelocity;

                //m_log.DebugFormat(
                //    "[LLCLIENTVIEW]: Sending terse update to {0} with position {1} in {2}", Name, presence.OffsetPosition, m_scene.Name);

                attachPoint = presence.State;
                collisionPlane = presence.CollisionPlane;

                datasize = 60;
            }
            else
            {
                SceneObjectPart part = (SceneObjectPart)entity;

                attachPoint = part.ParentGroup.AttachmentPoint;
                attachPoint = ((attachPoint % 16) * 16 + (attachPoint / 16));
                //m_log.DebugFormat(
                //    "[LLCLIENTVIEW]: Sending attachPoint {0} for {1} {2} to {3}",
                //    attachPoint, part.Name, part.LocalId, Name);

                collisionPlane = Vector4.Zero;
                position = part.RelativePosition;
                velocity = part.Velocity;
                acceleration = part.Acceleration;
                angularVelocity = part.AngularVelocity;
                rotation = part.RotationOffset;

                datasize = 44;
                if (includeTexture)
                    te = part.Shape.TextureEntry;
            }

            #endregion ScenePresence/SOP Handling
            //object block size
            data[pos++] = datasize;

            // LocalID
            Utils.UIntToBytes(entity.LocalId, data, pos);
            pos += 4;

            data[pos++] = (byte)attachPoint;

            // Avatar/CollisionPlane
            if (avatar)
            {
                data[pos++] = 1;

                //m_log.DebugFormat("CollisionPlane: {0}",collisionPlane);
                if (collisionPlane == Vector4.Zero)
                    Vector4.UnitW.ToBytes(data, pos);
                else
                    collisionPlane.ToBytes(data, pos);
                pos += 16;
            }
            else
            {
                data[pos++] = 0;
            }

            // Position
            position.ToBytes(data, pos);
            pos += 12;

            // Velocity
            ClampVectorForUint(ref velocity, 128f);
            Utils.FloatToUInt16Bytes(velocity.X, 128.0f, data, pos); pos += 2;
            Utils.FloatToUInt16Bytes(velocity.Y, 128.0f, data, pos); pos += 2;
            Utils.FloatToUInt16Bytes(velocity.Z, 128.0f, data, pos); pos += 2;

            // Acceleration
            ClampVectorForUint(ref acceleration, 64f);
            Utils.FloatToUInt16Bytes(acceleration.X, 64.0f, data, pos); pos += 2;
            Utils.FloatToUInt16Bytes(acceleration.Y, 64.0f, data, pos); pos += 2;
            Utils.FloatToUInt16Bytes(acceleration.Z, 64.0f, data, pos); pos += 2;

            // Rotation

            Utils.FloatToUInt16Bytes(rotation.X, 1.0f, data, pos); pos += 2;
            Utils.FloatToUInt16Bytes(rotation.Y, 1.0f, data, pos); pos += 2;
            Utils.FloatToUInt16Bytes(rotation.Z, 1.0f, data, pos); pos += 2;
            Utils.FloatToUInt16Bytes(rotation.W, 1.0f, data, pos); pos += 2;

            // Angular Velocity
            ClampVectorForUint(ref angularVelocity, 64f);
            Utils.FloatToUInt16Bytes(angularVelocity.X, 64.0f, data, pos); pos += 2;
            Utils.FloatToUInt16Bytes(angularVelocity.Y, 64.0f, data, pos); pos += 2;
            Utils.FloatToUInt16Bytes(angularVelocity.Z, 64.0f, data, pos); pos += 2;

            // texture entry block size
            if (te is null)
            {
                data[pos++] = 0;
                data[pos++] = 0;
            }
            else
            {
                int len = te.Length & 0x7fff;
                int totlen = len + 4;
                data[pos++] = (byte)totlen;
                data[pos++] = (byte)(totlen >> 8);
                data[pos++] = (byte)len; // wtf ???
                data[pos++] = (byte)(len >> 8);
                data[pos++] = 0;
                data[pos++] = 0;
                Buffer.BlockCopy(te, 0, data, pos, len);
                pos += len;
            }
            // total size 63 or 47 + (texture size + 4)
        }
        protected static void CreatePartImprovedTerseBlock(SceneObjectPart part, byte[] data, ref int pos, bool includeTexture)
        {
            //object block size
            data[pos++] = 44;

            // LocalID
            Utils.UIntToBytesSafepos(part.LocalId, data, pos);
            pos += 4;

            if (part.ParentGroup.AttachmentPoint == 0)
                data[pos++] = 0;
            else
            {
                uint attachPoint = 0xff & part.ParentGroup.AttachmentPoint;
                data[pos++] = (byte)(attachPoint << 4 | attachPoint >> 4);
            }

            // no Avatar/CollisionPlane
            data[pos++] = 0;

            // Position
            part.RelativePosition.ToBytes(data, pos);
            pos += 12;

            // Velocity
            part.Velocity.ClampedToShortsBytes(128f, data, pos); pos += 6;

            // Acceleration
            part.Acceleration.ClampedToShortsBytes(64f, data, pos); pos += 6;

            // Rotation
            part.RotationOffset.ToShortsBytes(data, pos); pos += 8;

            // Angular Velocity
            part.AngularVelocity.ClampedToShortsBytes(64f, data, pos); pos += 6;

            // texture entry block size
            if (includeTexture && part.Shape.TextureEntry != null)
            {
                byte[] te = part.Shape.TextureEntry;
                int len = te.Length & 0x7fff;
                int totlen = len + 4;
                data[pos++] = (byte)totlen;
                data[pos++] = (byte)(totlen >> 8);
                data[pos++] = (byte)len; // wtf ???
                data[pos++] = (byte)(len >> 8);
                data[pos++] = 0;
                data[pos++] = 0;
                Buffer.BlockCopy(te, 0, data, pos, len);
                pos += len;
            }
            else
            {
                data[pos++] = 0;
                data[pos++] = 0;
            }
            // total size 47 + (texture size + 4)
        }

        protected unsafe void CreatePartImprovedTerseBlock(SceneObjectPart part, ref byte* data, bool includeTexture)
        {
            //object block size
            *data++ = 44;

            // LocalID
            Utils.UIntToBytes(part.LocalId, data); data += 4;

            if (part.ParentGroup.AttachmentPoint == 0)
                *data++ = 0;
            else
            {
                uint attachPoint = 0xff & part.ParentGroup.AttachmentPoint;
                *data++ = (byte)(attachPoint << 4 | attachPoint >> 4);
            }

            // no Avatar/CollisionPlane
            *data++ = 0;

            // Position
            part.RelativePosition.ToBytes(data); data += 12;

            // Velocity
            part.Velocity.ClampedToShortsBytes(128f, data); data += 6;

            // Acceleration
            part.Acceleration.ClampedToShortsBytes(64f, data); data += 6;

            // Rotation
            part.RotationOffset.ToShortsBytes(data); data += 8;

            // Angular Velocity
            part.AngularVelocity.ClampedToShortsBytes(64f, data); data += 6;

            // texture entry block size
            if (includeTexture && part.Shape.TextureEntry != null)
            {
                byte[] te = part.Shape.TextureEntry;
                uint len = (uint)te.Length & 0x7fff;
                ushort totlen = (ushort)(len + 4);
                Utils.UInt16ToBytes(totlen, data); data += 2;
                Utils.UIntToBytes(len, data); data += 4;
                fixed(byte* t = &te[0])
                    Buffer.MemoryCopy(t, data, 4096, (long)len);
                data += len;
            }
            else
            {
                *data++ = 0;
                *data++ = 0;
            }
            // total size 47 + (texture size + 4)
        }

        protected static void CreateAvatartImprovedTerseBlock(ScenePresence presence, byte[] data, ref int pos)
        {
            //m_log.DebugFormat(
            //    "[LLCLIENTVIEW]: Sending terse update to {0} with position {1} in {2}", Name, presence.OffsetPosition, m_scene.Name);

            //object block size
            data[pos++] = 60;

            // LocalID
            Utils.UIntToBytesSafepos(presence.LocalId, data, pos);
            pos += 4;

            data[pos++] = presence.State;

            // Avatar/CollisionPlane
            data[pos++] = 1;

            //m_log.DebugFormat("CollisionPlane: {0}",collisionPlane);
            if (presence.CollisionPlane.IsZero())
                Vector4.UnitW.ToBytes(data, pos);
            else
                presence.CollisionPlane.ToBytes(data, pos);
            pos += 16;

            // Position
            presence.OffsetPosition.ToBytes(data, pos);
            pos += 12;

            // Velocity
            presence.Velocity.ClampedToShortsBytes(128f, data, pos); pos += 6;

            // Acceleration is zero
            Utils.UIntToBytesSafepos(0x7fff7fff, data, pos); pos += 4;
            Utils.UInt16ToBytes(0x7fff, data, pos); pos += 2;

            // Rotation
            // tpvs can only see rotations around Z in some cases
            Quaternion rotation = presence.Rotation;
            if (!presence.Flying && !presence.IsSatOnObject)
            {
                Utils.UIntToBytesSafepos(0x7fff7fff, data, pos); pos += 4;
                float rz = rotation.Z;
                float rw = rotation.W;
                float a = rz * rz + rw * rw;
                if (a > -1e-6f && a < 1e-6f)
                {
                    Utils.UIntToBytesSafepos(0xffff7fff, data, pos); pos += 4;
                }
                else
                {
                    a = 1.0f / MathF.Sqrt(a);
                    Utils.FloatToUInt16Bytes(rz * a, 1.0f, data, pos); pos += 2;
                    Utils.FloatToUInt16Bytes(rw * a, 1.0f, data, pos); pos += 2;
                }
            }
            else
            {
                rotation.ToShortsBytes(data, pos); pos += 8;
            }

            // Angular Velocity
            presence.AngularVelocity.ClampedToShortsBytes(64f, data, pos); pos += 6;

            //texture
            data[pos++] = 0;
            data[pos++] = 0;
            // total size 63
        }

        protected unsafe void CreateAvatartImprovedTerseBlock(ScenePresence presence, ref byte* data)
        {
            //m_log.DebugFormat(
            //    "[LLCLIENTVIEW]: Sending terse update to {0} with position {1} in {2}", Name, presence.OffsetPosition, m_scene.Name);

            //object block size
            *data++ = 60;
            // LocalID
            Utils.UIntToBytes(presence.LocalId, data); data += 4;

            *data++ = presence.State;

            // Avatar/CollisionPlane
            *data++ = 1;
            //m_log.DebugFormat("CollisionPlane: {0}",collisionPlane);
            if (presence.CollisionPlane.IsZero())
                Vector4.UnitW.ToBytes(data);
            else
                presence.CollisionPlane.ToBytes(data);
            data += 16;

            // Position
            presence.OffsetPosition.ToBytes(data); data += 12;

            // Velocity
            presence.Velocity.ClampedToShortsBytes(128f, data); data += 6;

            // Acceleration is zero
            Utils.UIntToBytes(0x7fff7fff, data); data += 4;
            Utils.UInt16ToBytes(0x7fff, data); data += 2;

            // Rotation
            // tpvs can only see rotations around Z in some cases
            Quaternion rotation = presence.Rotation;
            if (!presence.Flying && !presence.IsSatOnObject)
            {
                Utils.UIntToBytes(0x7fff7fff, data); data += 4;
                float rz = rotation.Z;
                float rw = rotation.W;
                float a = rz * rz + rw * rw;
                if (a > -1e-6f && a < 1e-6f)
                {
                    Utils.UIntToBytes(0xffff7fff, data); data += 4;
                }
                else
                {
                    a = 1.0f / MathF.Sqrt(a);
                    Utils.UInt16ToBytes(Utils.FloatToUnitUInt16(rz * a), data); data += 2;
                    Utils.UInt16ToBytes(Utils.FloatToUnitUInt16(rw * a), data); data += 2;
                }
            }
            else
            {
                rotation.ToShortsBytes(data); data += 8;
            }

            // Angular Velocity
            presence.AngularVelocity.ClampedToShortsBytes(64f, data); data += 6;

            //texture
            *data++ = 0;
            *data++ = 0;
            // total size 63
        }

        protected static void CreateAvatarUpdateBlock(ScenePresence data, byte[] dest, ref int pos)
        {
            Utils.UIntToBytesSafepos(data.LocalId, dest, pos); pos += 4;
            dest[pos++] = 0; // state
            data.UUID.ToBytes(dest, pos); pos += 16;
            Utils.UIntToBytesSafepos(0 , dest, pos); pos += 4; // crc
            dest[pos++] = (byte)PCode.Avatar;
            dest[pos++] = (byte)Material.Flesh;
            dest[pos++] = 0; // clickaction
            data.Appearance.AvatarSize.ToBytes(dest, pos); pos += 12;

            // objectdata block
            dest[pos++] = 76;
            data.CollisionPlane.ToBytes(dest, pos); pos += 16;
            data.OffsetPosition.ToBytes(dest, pos); pos += 12;
            data.Velocity.ToBytes(dest, pos); pos += 12;

            //acceleration.ToBytes(dest, pos); pos += 12;
            Array.Clear(dest, pos, 12); pos += 12;

            Quaternion rotation = data.Rotation;
            // tpvs can only see rotations around Z in some cases
            if (!data.Flying && !data.IsSatOnObject)
            {
                rotation.X = 0f;
                rotation.Y = 0f;
            }
            rotation.ToBytes(dest, pos); pos += 12;

            //angularvelocity.ToBytes(dest, pos); pos += 12;
            Array.Clear(dest, pos, 12); pos += 12;

            SceneObjectPart parentPart = data.ParentPart;
            if (parentPart != null)
            {
                Utils.UIntToBytesSafepos(parentPart.ParentGroup.LocalId, dest, pos);
                pos += 4;
            }
            else
            {
                //Utils.UIntToBytesSafepos(0, dest, pos);
                //pos += 4;
                dest[pos++] = 0;
                dest[pos++] = 0;
                dest[pos++] = 0;
                dest[pos++] = 0;
            }

            //Utils.UIntToBytesSafepos(0, dest, pos); pos += 4; //update flags
            dest[pos++] = 0;
            dest[pos++] = 0;
            dest[pos++] = 0;
            dest[pos++] = 0;

            //pbs
            dest[pos++] = 16;
            dest[pos++] = 1;
            //Utils.UInt16ToBytes(0, dest, pos); pos += 2;
            //Utils.UInt16ToBytes(0, dest, pos); pos += 2;
            dest[pos++] = 0;
            dest[pos++] = 0;
            dest[pos++] = 0;
            dest[pos++] = 0;

            dest[pos++] = 100;
            dest[pos++] = 100;

            // rest of pbs is 0 (15), texture entry (2) and texture anim (1)
            const int pbszeros = 15 + 2 + 1;
            Array.Clear(dest, pos, pbszeros); pos += pbszeros;

            //NameValue
            byte[] nv;
            if (data.HideTitle)
                nv = Utils.StringToBytes("FirstName STRING RW SV " + data.Firstname + "\nLastName STRING RW SV " +
                    data.Lastname + "\nTitle STRING RW SV ");
            else
                nv = Utils.StringToBytes("FirstName STRING RW SV " + data.Firstname + "\nLastName STRING RW SV " +
                    data.Lastname + "\nTitle STRING RW SV " + data.Grouptitle);
            int len = nv.Length;
            dest[pos++] = (byte)len;
            dest[pos++] = (byte)(len >> 8);
            Buffer.BlockCopy(nv, 0, dest, pos, len); pos += len;

            // data(2), text(1), text color(4), media url(1), PBblock(1), ExtramParams(1),
            // sound id(16), sound owner(16) gain (4), flags (1), radius (4)
            //  jointtype(1) joint pivot(12) joint offset(12)
            const int lastzeros = 2 + 1 + 4 + 1 + 1 + 1 + 16 + 16 + 4 + 1 + 4 + 1 + 12 + 12;
            Array.Clear(dest, pos, lastzeros); pos += lastzeros;
        }

        protected static void CreateAvatarUpdateBlock(ScenePresence data, LLUDPZeroEncoder zc)
        {
            Quaternion rotation = data.Rotation;
            // tpvs can only see rotations around Z in some cases
            if (!data.Flying && !data.IsSatOnObject)
            {
                rotation.X = 0f;
                rotation.Y = 0f;
            }

            zc.AddUInt(data.LocalId);
            zc.AddByte(0);
            zc.AddUUID(data.UUID);
            zc.AddZeros(4); // crc unused
            zc.AddByte((byte)PCode.Avatar);
            zc.AddByte((byte)Material.Flesh);
            zc.AddByte(0); // clickaction
            zc.AddVector3(data.Appearance.AvatarSize);

            // objectdata block
            zc.AddByte(76); // fixed avatar block size
            zc.AddVector4(data.CollisionPlane);
            zc.AddVector3(data.OffsetPosition);
            zc.AddVector3(data.Velocity);
            //zc.AddVector3(acceleration);
            zc.AddZeros(12);
            zc.AddNormQuat(rotation);
            //zc.AddVector3(angularvelocity);
            zc.AddZeros(12);

            SceneObjectPart parentPart = data.ParentPart;
            if (parentPart != null)
                zc.AddUInt(parentPart.ParentGroup.LocalId);
            else
                zc.AddZeros(4);

            zc.AddZeros(4); //update flags

            //pbs volume data 23
            //texture entry 2
            //texture anim (1)
            const int pbszeros = 23 + 2 + 1;
            zc.AddZeros(pbszeros);

            //NameValue
            byte[] nv;
            if (data.HideTitle)
                nv = Utils.StringToBytes("FirstName STRING RW SV " + data.Firstname + "\nLastName STRING RW SV " +
                    data.Lastname + "\nTitle STRING RW SV ");
            else
                nv = Utils.StringToBytes("FirstName STRING RW SV " + data.Firstname + "\nLastName STRING RW SV " +
                    data.Lastname + "\nTitle STRING RW SV " + data.Grouptitle);
            int len = nv.Length;
            zc.AddByte((byte)len);
            zc.AddByte((byte)(len >> 8));
            zc.AddBytes(nv, len);

            // data(2), text(1), text color(4), media url(1), PBblock(1), ExtramParams(1),
            // sound id(16), sound owner(16) gain (4), flags (1), radius (4)
            //  jointtype(1) joint pivot(12) joint offset(12)
            const int lastzeros = 2 + 1 + 4 + 1 + 1 + 1 + 16 + 16 + 4 + 1 + 4 + 1 + 12 + 12;
            zc.AddZeros(lastzeros);
        }

        protected void CreatePrimUpdateBlock(SceneObjectPart part, ScenePresence sp, LLUDPZeroEncoder zc)
        {
            // prepare data

            #region PrimFlags
            // prim/update flags
            PrimFlags primflags = (PrimFlags)m_scene.Permissions.GenerateClientFlags(part, sp);
            // Don't send the CreateSelected flag to everyone
            primflags &= ~PrimFlags.CreateSelected;
            if (sp.UUID == part.OwnerID)
            {
                if (part.CreateSelected)
                {
                    // Only send this flag once, then unset it
                    primflags |= PrimFlags.CreateSelected;
                    part.CreateSelected = false;
                }
            }
            #endregion PrimFlags

            // data block
            byte[] data = null;
            byte state = part.Shape.State;
            PCode pcode = (PCode)part.Shape.PCode;

            //vegetation is special so just do it inline
            if(pcode == PCode.Grass || pcode == PCode.Tree ||  pcode == PCode.NewTree)
            {
                zc.AddUInt(part.LocalId);
                zc.AddByte(state); // state
                zc.AddUUID(part.UUID);
                zc.AddUInt((uint)part.PseudoCRC);
                zc.AddByte((byte)pcode);
                // material 1
                // clickaction 1
                zc.AddZeros(2);
                zc.AddVector3(part.Shape.Scale);

                // objectdata block
                zc.AddByte(60); // fixed object block size
                zc.AddVector3(part.RelativePosition);
                if (pcode == PCode.Grass)
                    zc.AddZeros(48);
                else
                {
                    zc.AddZeros(24);
                    zc.AddNormQuat(part.RotationOffset);
                    zc.AddZeros(12);
                }

                zc.AddUInt(part.ParentID);
                zc.AddUInt((uint)primflags); //update flags

                /*
                if (pcode == PCode.Grass)
                {
                    //pbs volume data 23
                    //texture entry 2
                    //texture anim 1
                    //name value 2
                    // data 1
                    // text 5
                    // media url 1
                    // particle system 1
                    // Extraparams 1
                    // sound id 16
                    // ownwer 16
                    // sound gain 4
                    // sound flags 1
                    // sound radius 4
                    // jointtype 1
                    // joint pivot 12
                    // joint offset 12
                    zc.AddZeros(23 + 2 + 1 + 2 + 1 + 5 + 1 + 1 + 1 + 16 + 16 + 4 + 1 + 4 + 1 + 12 + 12);
                    return;
                }
                */

                //pbs volume data 23
                //texture entry 2
                //texture anim 1
                //name value 2
                zc.AddZeros(23 + 2 + 1 + 2);

                //data: the tree type
                zc.AddByte(1);
                zc.AddZeros(1);
                zc.AddByte(state);

                // text 5
                // media url 1
                // particle system 1
                // Extraparams 1
                // sound id 16
                // ownwer 16
                // sound gain 4
                // sound flags 1
                // sound radius 4
                // jointtype 1
                // joint pivot 12
                // joint offset 12
                zc.AddZeros(5 + 1 + 1 + 1 + 16 + 16 + 4 + 1 + 4 + 1 + 12 + 12);

                return;
            }

            //NameValue and state
            byte[] nv = null;
            
            if (part.ParentGroup.IsAttachment)
            {
                if (part.IsRoot)
                {
                    if (part.ParentGroup.FromItemID.IsZero())
                        nv = Util.StringToBytes256("AttachItemID STRING RW SV " + part.UUID.ToString());
                    else
                        nv = Util.StringToBytes256("AttachItemID STRING RW SV " + part.ParentGroup.FromItemID.ToString());
                }

                int st = 0xff & (int)part.ParentGroup.AttachmentPoint;
                state = (byte)((st >> 4) | (st << 4));
            }

            // filter out mesh faces hack
            ushort profileBegin = part.Shape.ProfileBegin;
            ushort profileHollow = part.Shape.ProfileHollow;
            byte profileCurve = part.Shape.ProfileCurve;
            byte pathScaleY = part.Shape.PathScaleY;

            if (part.Shape.SculptType == (byte)SculptType.Mesh) // filter out hack
            {
                profileCurve = (byte)(part.Shape.ProfileCurve & 0x0f);
                // fix old values that confused viewers
                if (profileBegin == 1)
                    profileBegin = 9375;
                if (profileHollow == 1)
                    profileHollow = 27500;
                // fix torus hole size Y that also confuse some viewers
                if (profileCurve == (byte)ProfileShape.Circle && pathScaleY < 150)
                    pathScaleY = 150;
            }

            // do encode the things
            zc.AddUInt(part.LocalId);
            zc.AddByte(state); // state
            zc.AddUUID(part.UUID);
            zc.AddUInt((uint)part.PseudoCRC);
            zc.AddByte((byte)pcode);
            zc.AddByte(part.Material);
            zc.AddByte(part.ClickAction); // clickaction
            zc.AddVector3(part.Shape.Scale);

            // objectdata block
            zc.AddByte(60); // fixed object block size
            zc.AddVector3(part.RelativePosition);
            zc.AddVector3(part.Velocity);
            zc.AddVector3(part.Acceleration);
            zc.AddNormQuat(part.RotationOffset);
            zc.AddVector3(part.AngularVelocity);

            zc.AddUInt(part.ParentID);
            zc.AddUInt((uint)primflags); //update flags

            //pbs
            zc.AddByte(part.Shape.PathCurve);
            zc.AddByte(profileCurve);
            zc.AddUInt16(part.Shape.PathBegin);
            zc.AddUInt16(part.Shape.PathEnd);
            zc.AddByte(part.Shape.PathScaleX);
            zc.AddByte(pathScaleY);
            zc.AddByte(part.Shape.PathShearX);
            zc.AddByte(part.Shape.PathShearY);
            zc.AddByte((byte)part.Shape.PathTwist);
            zc.AddByte((byte)part.Shape.PathTwistBegin);
            zc.AddByte((byte)part.Shape.PathRadiusOffset);
            zc.AddByte((byte)part.Shape.PathTaperX);
            zc.AddByte((byte)part.Shape.PathTaperY);
            zc.AddByte(part.Shape.PathRevolutions);
            zc.AddByte((byte)part.Shape.PathSkew);
            zc.AddUInt16(profileBegin);
            zc.AddUInt16(part.Shape.ProfileEnd);
            zc.AddUInt16(profileHollow);

            // texture
            byte[] tentry = part.Shape.TextureEntry;
            if (tentry is null)
                zc.AddZeros(2);
            else
            {
                int len = tentry.Length;
                zc.AddByte((byte)len);
                zc.AddByte((byte)(len >> 8));
                zc.AddBytes(tentry, len);
            }

            // texture animation
            byte[] tanim = part.TextureAnimation;
            if (tanim is null)
                zc.AddZeros(1);
            else
            {
                int len = tanim.Length;
                zc.AddByte((byte)len);
                zc.AddBytes(tanim, len);
            }

            //NameValue
            if(nv is null)
                zc.AddZeros(2);
            else
            {
                int len = nv.Length;
                zc.AddByte((byte)len);
                zc.AddByte((byte)(len >> 8));
                zc.AddBytes(nv, len);
            }

            // data
            if (data is null)
                zc.AddZeros(2);
            else
            {
                int len = data.Length;
                zc.AddByte((byte)len);
                zc.AddByte((byte)(len >> 8));
                zc.AddBytes(data, len);
            }

            //text
            osUTF8 osUTF8PartText = part.osUTF8Text;
            if (osUTF8PartText is null || osUTF8PartText.Length == 0)
                zc.AddZeros(5);
            else
            {
                zc.AddShortLimitedUTF8(osUTF8PartText);

                //textcolor
                byte[] tc = part.GetTextColor().GetBytes(false);
                zc.AddBytes(tc, 4);
            }

            //media url
            zc.AddShortLimitedUTF8(part.osUTFMediaUrl);

            bool hasps = false;
            //particle system
            byte[] ps = part.ParticleSystem;
            if (ps is null || ps.Length < 1)
                zc.AddZeros(1);
            else
            {
                int len = ps.Length;
                zc.AddByte((byte)len);
                zc.AddBytes(ps, len);
                hasps = true;
            }

            //Extraparams
            byte[] ep = part.Shape.ExtraParams;
            if (ep is null || ep.Length < 2)
                zc.AddZeros(1);
            else
            {
                int len = ep.Length;
                zc.AddByte((byte)len);
                zc.AddBytes(ep, len);
            }

            bool hassound = part.Sound.IsNotZero() || part.SoundFlags != 0;
            if (hassound)
                zc.AddUUID(part.Sound);
            else
                zc.AddZeros(16);

            if (hassound || hasps)
                zc.AddUUID(part.OwnerID);
            else
                zc.AddZeros(16);

            if (hassound)
            {
                zc.AddFloat((float)part.SoundGain);
                zc.AddByte(part.SoundFlags);
                zc.AddFloat((float)part.SoundRadius);
            }
            else
                zc.AddZeros(9);

            //  jointtype(1) joint pivot(12) joint offset(12)
            const int lastzeros = 1 + 12 + 12;
            zc.AddZeros(lastzeros);
        }

        [Flags]
        private enum CompressedFlags : uint
        {
            None = 0x00,
            /// <summary>Unknown</summary>
            ScratchPad = 0x01,
            /// <summary>Whether the object has a TreeSpecies</summary>
            Tree = 0x02,
            /// <summary>Whether the object has floating text ala llSetText</summary>
            HasText = 0x04,
            /// <summary>Whether the object has an active particle system</summary>
            HasParticlesLegacy = 0x08,
            /// <summary>Whether the object has sound attached to it</summary>
            HasSound = 0x10,
            /// <summary>Whether the object is attached to a root object or not</summary>
            HasParent = 0x20,
            /// <summary>Whether the object has texture animation settings</summary>
            TextureAnimation = 0x40,
            /// <summary>Whether the object has an angular velocity</summary>
            HasAngularVelocity = 0x80,
            /// <summary>Whether the object has a name value pairs string</summary>
            HasNameValues = 0x100,
            /// <summary>Whether the object has a Media URL set</summary>
            MediaURL = 0x200,
            HasParticlesNew = 0x400
        }

        /*
        protected void CreateCompressedUpdateBlock(SceneObjectPart part, ScenePresence sp, byte[] dest, ref int pos)
        {
            // prepare data
            CompressedFlags cflags = CompressedFlags.None;

            // prim/update flags

            PrimFlags primflags = (PrimFlags)m_scene.Permissions.GenerateClientFlags(part, sp);
            // Don't send the CreateSelected flag to everyone
            primflags &= ~PrimFlags.CreateSelected;
            if (sp.UUID == part.OwnerID)
            {
                if (part.CreateSelected)
                {
                    // Only send this flag once, then unset it
                    primflags |= PrimFlags.CreateSelected;
                    part.CreateSelected = false;
                }
            }

            byte state = part.Shape.State;
            PCode pcode = (PCode)part.Shape.PCode;

            bool hastree = false;
            if (pcode == PCode.Grass || pcode == PCode.Tree || pcode == PCode.NewTree)
            {
                cflags |= CompressedFlags.Tree;
                hastree = true;
            }

            //NameValue and state
            byte[] nv = null;
            if (part.ParentGroup.IsAttachment)
            {
                if (part.IsRoot)
                {
                    UUID fromID = part.ParentGroup.FromItemID;
                    if (fromID == UUID.Zero)
                        fromID = part.UUID;
                    nv = Util.StringToBytes256("AttachItemID STRING RW SV " + fromID.ToString());
                }

                int st = 0xff & (int)part.ParentGroup.AttachmentPoint;
                state = (byte)((st & >> 4) | (st << 4));
            }

            bool hastext = part.Text != null && part.Text.Length > 0;
            bool hassound = part.Sound != UUID.Zero || part.SoundFlags != 0;
            bool hasps = part.ParticleSystem != null && part.ParticleSystem.Length > 1;
            bool hastexanim = part.TextureAnimation != null && part.TextureAnimation.Length > 0;
            bool hasangvel = part.AngularVelocity.LengthSquared() > 1e-8f;
            bool hasmediaurl = part.MediaUrl != null && part.MediaUrl.Length > 1;

            bool haspsnew = false;
            if (hastext)
                cflags |= CompressedFlags.HasText;
            if (hasps)
            {
                if(part.ParticleSystem.Length > 86)
                {
                    hasps= false;
                    cflags |= CompressedFlags.HasParticlesNew;
                    haspsnew = true;
                }
                else
                    cflags |= CompressedFlags.HasParticlesLegacy;
            }
            if (hassound)
                cflags |= CompressedFlags.HasSound;
            if (part.ParentID != 0)
                cflags |= CompressedFlags.HasParent;
            if (hastexanim)
                cflags |= CompressedFlags.TextureAnimation;
            if (hasangvel)
                cflags |= CompressedFlags.HasAngularVelocity;
            if (hasmediaurl)
                cflags |= CompressedFlags.MediaURL;
            if (nv != null)
                cflags |= CompressedFlags.HasNameValues;

            // filter out mesh faces hack
            ushort profileBegin = part.Shape.ProfileBegin;
            ushort profileHollow = part.Shape.ProfileHollow;
            byte profileCurve = part.Shape.ProfileCurve;
            byte pathScaleY = part.Shape.PathScaleY;

            if (part.Shape.SculptType == (byte)SculptType.Mesh) // filter out hack
            {
                profileCurve = (byte)(part.Shape.ProfileCurve & 0x0f);
                // fix old values that confused viewers
                if (profileBegin == 1)
                    profileBegin = 9375;
                if (profileHollow == 1)
                    profileHollow = 27500;
                // fix torus hole size Y that also confuse some viewers
                if (profileCurve == (byte)ProfileShape.Circle && pathScaleY < 150)
                    pathScaleY = 150;
            }

            // first is primFlags
            Utils.UIntToBytesSafepos((uint)primflags, dest, pos); pos += 4;

            // datablock len to fill later
            int lenpos = pos;
            pos += 2;

            // data block
            part.UUID.ToBytes(dest, pos); pos += 16;
            Utils.UIntToBytesSafepos(part.LocalId, dest, pos); pos += 4;
            dest[pos++] = (byte)pcode;
            dest[pos++] = state;

            Utils.UIntToBytesSafepos((uint)part.ParentGroup.PseudoCRC, dest, pos); pos += 4;
            dest[pos++] = part.Material;
            dest[pos++] = part.ClickAction;
            part.Shape.Scale.ToBytes(dest, pos); pos += 12;
            part.RelativePosition.ToBytes(dest, pos); pos += 12;
            if(pcode == PCode.Grass)
                Vector3.Zero.ToBytes(dest, pos);
            else
            {
                Quaternion rotation = part.RotationOffset;
                rotation.Normalize();
                rotation.ToBytes(dest, pos);
            }
            pos += 12;

            Utils.UIntToBytesSafepos((uint)cflags, dest, pos); pos += 4;

            if (hasps || haspsnew || hassound)
                part.OwnerID.ToBytes(dest, pos);
            else
                UUID.Zero.ToBytes(dest, pos);
            pos += 16;

            if (hasangvel)
            {
                part.AngularVelocity.ToBytes(dest, pos); pos += 12;
            }
            if (part.ParentID != 0)
            {
                Utils.UIntToBytesSafepos(part.ParentID, dest, pos); pos += 4;
            }
            if (hastree)
                dest[pos++] = state;
            if (hastext)
            {
                byte[] text = Util.StringToBytes256(part.Text); // must be null term
                Buffer.BlockCopy(text, 0, dest, pos, text.Length); pos += text.Length;
                byte[] tc = part.GetTextColor().GetBytes(false);
                Buffer.BlockCopy(tc, 0, dest, pos, tc.Length); pos += tc.Length;
            }
            if (hasmediaurl)
            {
                byte[] mu = Util.StringToBytes256(part.MediaUrl); // must be null term
                Buffer.BlockCopy(mu, 0, dest, pos, mu.Length); pos += mu.Length;
            }
            if (hasps)
            {
                byte[] ps = part.ParticleSystem;
                Buffer.BlockCopy(ps, 0, dest, pos, ps.Length); pos += ps.Length;
            }
            byte[] ex = part.Shape.ExtraParams;
            if (ex is null || ex.Length < 2)
                dest[pos++] = 0;
            else
            {
                Buffer.BlockCopy(ex, 0, dest, pos, ex.Length); pos += ex.Length;
            }
            if (hassound)
            {
                part.Sound.ToBytes(dest, pos); pos += 16;
                Utils.FloatToBytesSafepos((float)part.SoundGain, dest, pos); pos += 4;
                dest[pos++] = part.SoundFlags;
                Utils.FloatToBytesSafepos((float)part.SoundRadius, dest, pos); pos += 4;
            }
            if (nv != null)
            {
                Buffer.BlockCopy(nv, 0, dest, pos, nv.Length); pos += nv.Length;
            }

            dest[pos++] = part.Shape.PathCurve;
            Utils.UInt16ToBytes(part.Shape.PathBegin, dest, pos); pos += 2;
            Utils.UInt16ToBytes(part.Shape.PathEnd, dest, pos); pos += 2;
            dest[pos++] = part.Shape.PathScaleX;
            dest[pos++] = pathScaleY;
            dest[pos++] = part.Shape.PathShearX;
            dest[pos++] = part.Shape.PathShearY;
            dest[pos++] = (byte)part.Shape.PathTwist;
            dest[pos++] = (byte)part.Shape.PathTwistBegin;
            dest[pos++] = (byte)part.Shape.PathRadiusOffset;
            dest[pos++] = (byte)part.Shape.PathTaperX;
            dest[pos++] = (byte)part.Shape.PathTaperY;
            dest[pos++] = part.Shape.PathRevolutions;
            dest[pos++] = (byte)part.Shape.PathSkew;
            dest[pos++] = profileCurve;
            Utils.UInt16ToBytes(profileBegin, dest, pos); pos += 2;
            Utils.UInt16ToBytes(part.Shape.ProfileEnd, dest, pos); pos += 2;
            Utils.UInt16ToBytes(profileHollow, dest, pos); pos += 2;

            byte[] te = part.Shape.TextureEntry;
            if (te is null)
            {
                dest[pos++] = 0;
                dest[pos++] = 0;
                dest[pos++] = 0;
                dest[pos++] = 0;
            }
            else
            {
                int len = te.Length & 0x7fff;
                dest[pos++] = (byte)len;
                dest[pos++] = (byte)(len >> 8);
                dest[pos++] = 0;
                dest[pos++] = 0;
                Buffer.BlockCopy(te, 0, dest, pos, len);
                pos += len;
            }
            if (hastexanim)
            {
                byte[] ta = part.TextureAnimation;
                int len = ta.Length & 0x7fff;
                dest[pos++] = (byte)len;
                dest[pos++] = (byte)(len >> 8);
                dest[pos++] = 0;
                dest[pos++] = 0;
                Buffer.BlockCopy(ta, 0, dest, pos, len);
                pos += len;
            }

            if (haspsnew)
            {
                byte[] ps = part.ParticleSystem;
                Buffer.BlockCopy(ps, 0, dest, pos, ps.Length); pos += ps.Length;
            }

            int totlen = pos - lenpos - 2;
            dest[lenpos++] = (byte)totlen;
            dest[lenpos++] = (byte)(totlen >> 8);
        }
        */

        protected void CreateCompressedUpdateBlockZC(SceneObjectPart part, ScenePresence sp, LLUDPZeroEncoder zc)
        {
            // prepare data
            CompressedFlags cflags = CompressedFlags.None;

            // prim/update flags

            PrimFlags primflags = (PrimFlags)m_scene.Permissions.GenerateClientFlags(part, sp);
            // Don't send the CreateSelected flag to everyone
            primflags &= ~PrimFlags.CreateSelected;
            if (sp.UUID.Equals(part.OwnerID))
            {
                if (part.CreateSelected)
                {
                    // Only send this flag once, then unset it
                    primflags |= PrimFlags.CreateSelected;
                    part.CreateSelected = false;
                }
            }

            byte state = part.Shape.State;
            PCode pcode = (PCode)part.Shape.PCode;

            // trees and grass are a lot more compact
            if (pcode == PCode.Grass || pcode == PCode.Tree || pcode == PCode.NewTree)
            {
                // first is primFlags
                zc.AddUInt((uint)primflags);

                // datablock len
                zc.AddByte(113);
                zc.AddZeros(1);

                // data block
                zc.AddUUID(part.UUID);
                zc.AddUInt(part.LocalId);
                zc.AddByte((byte)pcode);
                zc.AddByte(state);

                zc.AddUInt((uint)part.PseudoCRC);

                zc.AddZeros(2); // material and click action

                zc.AddVector3(part.Shape.Scale);
                zc.AddVector3(part.RelativePosition);
                if (pcode == PCode.Grass)
                    zc.AddZeros(12);
                else
                    zc.AddNormQuat(part.RotationOffset);

                zc.AddUInt((uint)CompressedFlags.Tree); // cflags

                zc.AddZeros(16); // owner id

                zc.AddByte(state); // tree parameter

                zc.AddZeros(28); //extraparameters 1, pbs 23, texture 4

                return;
            }

            //NameValue and state
            byte[] nv = null;
            if (part.ParentGroup.IsAttachment)
            {
                if (part.IsRoot)
                {
                    if (part.ParentGroup.FromItemID.IsZero())
                        nv = Util.StringToBytes256("AttachItemID STRING RW SV " + part.UUID.ToString());
                    else
                        nv = Util.StringToBytes256("AttachItemID STRING RW SV " + part.ParentGroup.FromItemID.ToString());
                }
                int st = 0xff & (int)part.ParentGroup.AttachmentPoint;
                state = (byte)((st >> 4) | (st << 4));
            }

            bool hastext = false;
            bool hassound = false;
            bool hasps = false;
            bool hastexanim = false;
            bool hasangvel = false;
            bool hasmediaurl = false;
            bool haspsnew = false;

            int BlockLengh = 111;

            byte[] extraParamBytes = part.Shape.ExtraParams;
            if (extraParamBytes is null || extraParamBytes.Length < 2)
            {
                ++BlockLengh;
                extraParamBytes = null;
            }
            else
                BlockLengh += extraParamBytes.Length;

            byte[] hoverTextColor = null;
            osUTF8 osUTF8PartText = part.osUTF8Text;
            if (osUTF8PartText != null && osUTF8PartText.Length > 0)
            {
                cflags |= CompressedFlags.HasText;
                BlockLengh += osUTF8PartText.Length;
                if (osUTF8PartText[^1] != 0)
                    ++BlockLengh;
                hoverTextColor = part.GetTextColor().GetBytes(false);
                BlockLengh += hoverTextColor.Length;
                hastext = true;
            }

            if (part.ParticleSystem != null && part.ParticleSystem.Length > 1)
            {
                BlockLengh += part.ParticleSystem.Length;
                if (part.ParticleSystem.Length > 86)
                {
                    hasps = false;
                    cflags |= CompressedFlags.HasParticlesNew;
                    haspsnew = true;
                }
                else
                {
                    cflags |= CompressedFlags.HasParticlesLegacy;
                    hasps = true;
                }
            }

            if (part.Sound.IsNotZero() || part.SoundFlags != 0)
            {
                BlockLengh += 25;
                cflags |= CompressedFlags.HasSound;
                hassound = true;
            }

            if (part.ParentID != 0)
            {
                BlockLengh += 4;
                cflags |= CompressedFlags.HasParent;
            }

            if (part.TextureAnimation != null && part.TextureAnimation.Length > 0)
            {
                BlockLengh += part.TextureAnimation.Length + 4;
                cflags |= CompressedFlags.TextureAnimation;
                hastexanim = true;
            }

            if (part.AngularVelocity.LengthSquared() > 1e-8f)
            {
                BlockLengh += 12;
                cflags |= CompressedFlags.HasAngularVelocity;
                hasangvel = true;
            }

            osUTF8 osUTFMediaUrl = part.osUTFMediaUrl;
            if (osUTFMediaUrl != null && osUTFMediaUrl.Length > 0)
            {
                BlockLengh += osUTFMediaUrl.Length;
                if (osUTFMediaUrl[^1] != 0)
                    ++BlockLengh;
                cflags |= CompressedFlags.MediaURL;
                hasmediaurl = true;
            }

            if (nv != null)
            {
                BlockLengh += nv.Length;
                cflags |= CompressedFlags.HasNameValues;
            }

            byte[] textureEntry = part.Shape.TextureEntry;
            if(textureEntry != null)
                BlockLengh += textureEntry.Length;

            // filter out mesh faces hack
            ushort profileBegin = part.Shape.ProfileBegin;
            ushort profileHollow = part.Shape.ProfileHollow;
            byte profileCurve = part.Shape.ProfileCurve;
            byte pathScaleY = part.Shape.PathScaleY;

            if (part.Shape.SculptType == (byte)SculptType.Mesh) // filter out hack
            {
                profileCurve = (byte)(part.Shape.ProfileCurve & 0x0f);
                // fix old values that confused viewers
                if (profileBegin == 1)
                    profileBegin = 9375;
                if (profileHollow == 1)
                    profileHollow = 27500;
                // fix torus hole size Y that also confuse some viewers
                if (profileCurve == (byte)ProfileShape.Circle && pathScaleY < 150)
                    pathScaleY = 150;
            }


            // first is primFlags
            zc.AddUInt((uint)primflags);

            // datablock len
            zc.AddByte((byte)BlockLengh);
            zc.AddByte((byte)(BlockLengh >> 8));

            // data block
            zc.AddUUID(part.UUID);
            zc.AddUInt(part.LocalId);
            zc.AddByte((byte)pcode);
            zc.AddByte(state);

            zc.AddUInt((uint)part.PseudoCRC);

            zc.AddByte(part.Material);
            zc.AddByte(part.ClickAction);
            zc.AddVector3(part.Shape.Scale);
            zc.AddVector3(part.RelativePosition);
            zc.AddNormQuat(part.RotationOffset);

            zc.AddUInt((uint)cflags);

            if (hasps || haspsnew || hassound)
                zc.AddUUID(part.OwnerID);
            else
                zc.AddZeros(16);

            if (hasangvel)
            {
                zc.AddVector3(part.AngularVelocity);
            }
            if (part.ParentID != 0)
            {
                zc.AddUInt(part.ParentID);
            }
            if (hastext)
            {
                zc.AddBytes(osUTF8PartText.GetArray(), osUTF8PartText.Length);
                if (osUTF8PartText[^1] != 0)
                    zc.AddZeros(1);
                zc.AddBytes(hoverTextColor, hoverTextColor.Length);
            }
            if (hasmediaurl)
            {
                zc.AddBytes(osUTFMediaUrl.GetArray(), osUTFMediaUrl.Length);
                if (osUTFMediaUrl[^1] != 0)
                    zc.AddZeros(1);
            }
            if (hasps)
            {
                byte[] ps = part.ParticleSystem;
                zc.AddBytes(ps, ps.Length);
            }
            if (extraParamBytes is null)
                zc.AddZeros(1);
            else
            {
                zc.AddBytes(extraParamBytes, extraParamBytes.Length);
            }
            if (hassound)
            {
                zc.AddUUID(part.Sound);
                zc.AddFloat((float)part.SoundGain);
                zc.AddByte(part.SoundFlags);
                zc.AddFloat((float)part.SoundRadius);
            }
            if (nv != null)
            {
                zc.AddBytes(nv, nv.Length);
            }

            zc.AddByte(part.Shape.PathCurve);
            zc.AddUInt16(part.Shape.PathBegin);
            zc.AddUInt16(part.Shape.PathEnd);
            zc.AddByte(part.Shape.PathScaleX);
            zc.AddByte(pathScaleY);
            zc.AddByte(part.Shape.PathShearX);
            zc.AddByte(part.Shape.PathShearY);
            zc.AddByte((byte)part.Shape.PathTwist);
            zc.AddByte((byte)part.Shape.PathTwistBegin);
            zc.AddByte((byte)part.Shape.PathRadiusOffset);
            zc.AddByte((byte)part.Shape.PathTaperX);
            zc.AddByte((byte)part.Shape.PathTaperY);
            zc.AddByte(part.Shape.PathRevolutions);
            zc.AddByte((byte)part.Shape.PathSkew);
            zc.AddByte(profileCurve);
            zc.AddUInt16(profileBegin);
            zc.AddUInt16(part.Shape.ProfileEnd);
            zc.AddUInt16(profileHollow);

            if (textureEntry is null)
            {
                zc.AddZeros(4);
            }
            else
            {
                int len = textureEntry.Length;
                zc.AddByte((byte)len);
                zc.AddByte((byte)(len >> 8));
                zc.AddZeros(2);
                zc.AddBytes(textureEntry, len);
            }
            if (hastexanim)
            {
                byte[] ta = part.TextureAnimation;
                int len = ta.Length;
                zc.AddByte((byte)len);
                zc.AddByte((byte)(len >> 8));
                zc.AddZeros(2);
                zc.AddBytes(ta, len);
            }

            if (haspsnew)
            {
                byte[] ps = part.ParticleSystem;
                zc.AddBytes(ps, ps.Length);
            }
        }

        public void SendNameReply(UUID profileId, string firstname, string lastname)
        {
            UUIDNameReplyPacket packet = (UUIDNameReplyPacket)PacketPool.Instance.GetPacket(PacketType.UUIDNameReply);
            // TODO: don't create new blocks if recycling an old packet
            packet.UUIDNameBlock = new UUIDNameReplyPacket.UUIDNameBlockBlock[1];
            packet.UUIDNameBlock[0] = new UUIDNameReplyPacket.UUIDNameBlockBlock
            {
                ID = profileId,
                FirstName = Util.StringToBytes256(firstname),
                LastName = Util.StringToBytes256(lastname)
            };

            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public Dictionary<UUID, ulong> GetGroupPowers()
        {
            lock(m_groupPowers)
            {
                return new Dictionary<UUID, ulong>(m_groupPowers);
            }
        }

        public void SetGroupPowers(Dictionary<UUID, ulong> powers)
        {
            lock(m_groupPowers)
            {
                m_groupPowers.Clear();
                m_groupPowers = powers;
            }
        }

        public ulong GetGroupPowers(UUID groupID)
        {
            lock(m_groupPowers)
            {
                if (m_groupPowers.ContainsKey(groupID))
                    return m_groupPowers[groupID];
            }
            return 0;
        }

        #endregion

        protected virtual void RegisterLocalPacketHandlers()
        {
            AddLocalPacketHandler(PacketType.LogoutRequest, HandleLogout);

            // If AgentUpdate is ever handled asynchronously, then we will also need to construct a new AgentUpdateArgs
            // for each AgentUpdate packet.
            AddLocalPacketHandler(PacketType.AgentUpdate, HandleAgentUpdate, false);

            AddLocalPacketHandler(PacketType.ViewerEffect, HandleViewerEffect, false);
            AddLocalPacketHandler(PacketType.VelocityInterpolateOff, HandleVelocityInterpolateOff, false);
            AddLocalPacketHandler(PacketType.VelocityInterpolateOn, HandleVelocityInterpolateOn, false);
            AddLocalPacketHandler(PacketType.AgentCachedTexture, HandleAgentTextureCached, false);
            AddLocalPacketHandler(PacketType.MultipleObjectUpdate, HandleMultipleObjUpdate, false);
            AddLocalPacketHandler(PacketType.MoneyTransferRequest, HandleMoneyTransferRequest, false);
            AddLocalPacketHandler(PacketType.ParcelBuy, HandleParcelBuyRequest, false);
            AddLocalPacketHandler(PacketType.UUIDGroupNameRequest, HandleUUIDGroupNameRequest);
            AddLocalPacketHandler(PacketType.ObjectGroup, HandleObjectGroupRequest);
            AddLocalPacketHandler(PacketType.GenericMessage, HandleGenericMessage);
            AddLocalPacketHandler(PacketType.AvatarPropertiesRequest, HandleAvatarPropertiesRequest);
            AddLocalPacketHandler(PacketType.ChatFromViewer, HandleChatFromViewer);
            AddLocalPacketHandler(PacketType.AvatarPropertiesUpdate, HandlerAvatarPropertiesUpdate);
            AddLocalPacketHandler(PacketType.ScriptDialogReply, HandlerScriptDialogReply);
            AddLocalPacketHandler(PacketType.ImprovedInstantMessage, HandlerImprovedInstantMessage);
            AddLocalPacketHandler(PacketType.AcceptFriendship, HandlerAcceptFriendship);
            AddLocalPacketHandler(PacketType.DeclineFriendship, HandlerDeclineFriendship);
            AddLocalPacketHandler(PacketType.TerminateFriendship, HandlerTerminateFriendship);
            AddLocalPacketHandler(PacketType.RezObject, HandlerRezObject);
            AddLocalPacketHandler(PacketType.DeRezObject, HandlerDeRezObject);
            AddLocalPacketHandler(PacketType.RezRestoreToWorld, HandlerRezRestoreToWorld);
            AddLocalPacketHandler(PacketType.ModifyLand, HandlerModifyLand);
            AddLocalPacketHandler(PacketType.RegionHandshakeReply, HandlerRegionHandshakeReply, false);
            AddLocalPacketHandler(PacketType.AgentWearablesRequest, HandlerAgentWearablesRequest);
            AddLocalPacketHandler(PacketType.AgentSetAppearance, HandlerAgentSetAppearance);
            AddLocalPacketHandler(PacketType.AgentIsNowWearing, HandlerAgentIsNowWearing);
            AddLocalPacketHandler(PacketType.RezSingleAttachmentFromInv, HandlerRezSingleAttachmentFromInv);
            AddLocalPacketHandler(PacketType.RezMultipleAttachmentsFromInv, HandleRezMultipleAttachmentsFromInv);
            AddLocalPacketHandler(PacketType.DetachAttachmentIntoInv, HandleDetachAttachmentIntoInv);
            AddLocalPacketHandler(PacketType.ObjectAttach, HandleObjectAttach);
            AddLocalPacketHandler(PacketType.ObjectDetach, HandleObjectDetach);
            AddLocalPacketHandler(PacketType.ObjectDrop, HandleObjectDrop);
            AddLocalPacketHandler(PacketType.SetAlwaysRun, HandleSetAlwaysRun, false);
            AddLocalPacketHandler(PacketType.CompleteAgentMovement, HandleCompleteAgentMovement);
            AddLocalPacketHandler(PacketType.AgentAnimation, HandleAgentAnimation, false);
            AddLocalPacketHandler(PacketType.AgentRequestSit, HandleAgentRequestSit);
            AddLocalPacketHandler(PacketType.AgentSit, HandleAgentSit);
            AddLocalPacketHandler(PacketType.SoundTrigger, HandleSoundTrigger);
            AddLocalPacketHandler(PacketType.AvatarPickerRequest, HandleAvatarPickerRequest);
            AddLocalPacketHandler(PacketType.AgentDataUpdateRequest, HandleAgentDataUpdateRequest);
            AddLocalPacketHandler(PacketType.UserInfoRequest, HandleUserInfoRequest);
            AddLocalPacketHandler(PacketType.UpdateUserInfo, HandleUpdateUserInfo);
            AddLocalPacketHandler(PacketType.SetStartLocationRequest, HandleSetStartLocationRequest);
            AddLocalPacketHandler(PacketType.AgentThrottle, HandleAgentThrottle, false);
            AddLocalPacketHandler(PacketType.AgentPause, HandleAgentPause, false);
            AddLocalPacketHandler(PacketType.AgentResume, HandleAgentResume, false);
            AddLocalPacketHandler(PacketType.ForceScriptControlRelease, HandleForceScriptControlRelease);
            AddLocalPacketHandler(PacketType.ObjectLink, HandleObjectLink);
            AddLocalPacketHandler(PacketType.ObjectDelink, HandleObjectDelink);
            AddLocalPacketHandler(PacketType.ObjectAdd, HandleObjectAdd);
            AddLocalPacketHandler(PacketType.ObjectShape, HandleObjectShape);
            AddLocalPacketHandler(PacketType.ObjectExtraParams, HandleObjectExtraParams);
            AddLocalPacketHandler(PacketType.ObjectDuplicate, HandleObjectDuplicate);
            AddLocalPacketHandler(PacketType.RequestMultipleObjects, HandleRequestMultipleObjects);
            AddLocalPacketHandler(PacketType.ObjectSelect, HandleObjectSelect);
            AddLocalPacketHandler(PacketType.ObjectDeselect, HandleObjectDeselect);
            AddLocalPacketHandler(PacketType.ObjectPosition, HandleObjectPosition);
            AddLocalPacketHandler(PacketType.ObjectScale, HandleObjectScale);
            AddLocalPacketHandler(PacketType.ObjectRotation, HandleObjectRotation);
            AddLocalPacketHandler(PacketType.ObjectFlagUpdate, HandleObjectFlagUpdate);

            // Handle ObjectImage (TextureEntry) updates synchronously, since when updating multiple prim faces at once,
            // some clients will send out a separate ObjectImage packet for each face
            AddLocalPacketHandler(PacketType.ObjectImage, HandleObjectImage, false);

            AddLocalPacketHandler(PacketType.ObjectGrab, HandleObjectGrab, false);
            AddLocalPacketHandler(PacketType.ObjectGrabUpdate, HandleObjectGrabUpdate, false);
            AddLocalPacketHandler(PacketType.ObjectDeGrab, HandleObjectDeGrab);
            AddLocalPacketHandler(PacketType.ObjectSpinStart, HandleObjectSpinStart, false);
            AddLocalPacketHandler(PacketType.ObjectSpinUpdate, HandleObjectSpinUpdate, false);
            AddLocalPacketHandler(PacketType.ObjectSpinStop, HandleObjectSpinStop, false);
            AddLocalPacketHandler(PacketType.ObjectDescription, HandleObjectDescription, false);
            AddLocalPacketHandler(PacketType.ObjectName, HandleObjectName, false);
            AddLocalPacketHandler(PacketType.ObjectPermissions, HandleObjectPermissions, false);
            AddLocalPacketHandler(PacketType.Undo, HandleUndo, false);
            AddLocalPacketHandler(PacketType.UndoLand, HandleLandUndo, false);
            AddLocalPacketHandler(PacketType.Redo, HandleRedo, false);
            AddLocalPacketHandler(PacketType.ObjectDuplicateOnRay, HandleObjectDuplicateOnRay);
            AddLocalPacketHandler(PacketType.RequestObjectPropertiesFamily, HandleRequestObjectPropertiesFamily, false);
            AddLocalPacketHandler(PacketType.ObjectIncludeInSearch, HandleObjectIncludeInSearch);
            AddLocalPacketHandler(PacketType.ScriptAnswerYes, HandleScriptAnswerYes, false);
            AddLocalPacketHandler(PacketType.ObjectClickAction, HandleObjectClickAction, false);
            AddLocalPacketHandler(PacketType.ObjectMaterial, HandleObjectMaterial, false);
            AddLocalPacketHandler(PacketType.RequestImage, HandleRequestImage, false);
            AddLocalPacketHandler(PacketType.TransferRequest, HandleTransferRequest, false);
            AddLocalPacketHandler(PacketType.AssetUploadRequest, HandleAssetUploadRequest);
            AddLocalPacketHandler(PacketType.RequestXfer, HandleRequestXfer);
            AddLocalPacketHandler(PacketType.SendXferPacket, HandleSendXferPacket);
            AddLocalPacketHandler(PacketType.ConfirmXferPacket, HandleConfirmXferPacket, false);
            AddLocalPacketHandler(PacketType.AbortXfer, HandleAbortXfer);
            AddLocalPacketHandler(PacketType.CreateInventoryFolder, HandleCreateInventoryFolder);
            AddLocalPacketHandler(PacketType.UpdateInventoryFolder, HandleUpdateInventoryFolder);
            AddLocalPacketHandler(PacketType.MoveInventoryFolder, HandleMoveInventoryFolder);
            AddLocalPacketHandler(PacketType.CreateInventoryItem, HandleCreateInventoryItem);
            AddLocalPacketHandler(PacketType.LinkInventoryItem, HandleLinkInventoryItem);
            AddLocalPacketHandler(PacketType.FetchInventory, HandleFetchInventory);
            AddLocalPacketHandler(PacketType.FetchInventoryDescendents, HandleFetchInventoryDescendents);
            AddLocalPacketHandler(PacketType.PurgeInventoryDescendents, HandlePurgeInventoryDescendents);
            AddLocalPacketHandler(PacketType.UpdateInventoryItem, HandleUpdateInventoryItem);
            AddLocalPacketHandler(PacketType.CopyInventoryItem, HandleCopyInventoryItem);
            AddLocalPacketHandler(PacketType.MoveInventoryItem, HandleMoveInventoryItem);
            AddLocalPacketHandler(PacketType.RemoveInventoryItem, HandleRemoveInventoryItem);
            AddLocalPacketHandler(PacketType.RemoveInventoryFolder, HandleRemoveInventoryFolder);
            AddLocalPacketHandler(PacketType.RemoveInventoryObjects, HandleRemoveInventoryObjects);
            AddLocalPacketHandler(PacketType.RequestTaskInventory, HandleRequestTaskInventory);
            AddLocalPacketHandler(PacketType.UpdateTaskInventory, HandleUpdateTaskInventory);
            AddLocalPacketHandler(PacketType.RemoveTaskInventory, HandleRemoveTaskInventory);
            AddLocalPacketHandler(PacketType.MoveTaskInventory, HandleMoveTaskInventory);
            AddLocalPacketHandler(PacketType.RezScript, HandleRezScript);
            AddLocalPacketHandler(PacketType.MapLayerRequest, HandleMapLayerRequest);
            AddLocalPacketHandler(PacketType.MapBlockRequest, HandleMapBlockRequest);
            AddLocalPacketHandler(PacketType.MapNameRequest, HandleMapNameRequest);
            AddLocalPacketHandler(PacketType.TeleportLandmarkRequest, HandleTeleportLandmarkRequest);
            AddLocalPacketHandler(PacketType.TeleportCancel, HandleTeleportCancel);
            AddLocalPacketHandler(PacketType.TeleportLocationRequest, HandleTeleportLocationRequest);
            AddLocalPacketHandler(PacketType.UUIDNameRequest, HandleUUIDNameRequest, false);
            AddLocalPacketHandler(PacketType.RegionHandleRequest, HandleRegionHandleRequest, false);
            AddLocalPacketHandler(PacketType.ParcelInfoRequest, HandleParcelInfoRequest);
            AddLocalPacketHandler(PacketType.ParcelAccessListRequest, HandleParcelAccessListRequest, false);
            AddLocalPacketHandler(PacketType.ParcelAccessListUpdate, HandleParcelAccessListUpdate, false);
            AddLocalPacketHandler(PacketType.ParcelPropertiesRequest, HandleParcelPropertiesRequest, false);
            AddLocalPacketHandler(PacketType.ParcelDivide, HandleParcelDivide);
            AddLocalPacketHandler(PacketType.ParcelJoin, HandleParcelJoin);
            AddLocalPacketHandler(PacketType.ParcelPropertiesUpdate, HandleParcelPropertiesUpdate);
            AddLocalPacketHandler(PacketType.ParcelSelectObjects, HandleParcelSelectObjects);
            AddLocalPacketHandler(PacketType.ParcelObjectOwnersRequest, HandleParcelObjectOwnersRequest);
            AddLocalPacketHandler(PacketType.ParcelGodForceOwner, HandleParcelGodForceOwner);
            AddLocalPacketHandler(PacketType.ParcelRelease, HandleParcelRelease);
            AddLocalPacketHandler(PacketType.ParcelReclaim, HandleParcelReclaim);
            AddLocalPacketHandler(PacketType.ParcelReturnObjects, HandleParcelReturnObjects);
            AddLocalPacketHandler(PacketType.ParcelSetOtherCleanTime, HandleParcelSetOtherCleanTime);
            AddLocalPacketHandler(PacketType.LandStatRequest, HandleLandStatRequest);
            AddLocalPacketHandler(PacketType.ParcelDwellRequest, HandleParcelDwellRequest);
            AddLocalPacketHandler(PacketType.EstateOwnerMessage, HandleEstateOwnerMessage);
            AddLocalPacketHandler(PacketType.RequestRegionInfo, HandleRequestRegionInfo, false);
            AddLocalPacketHandler(PacketType.EstateCovenantRequest, HandleEstateCovenantRequest);
            AddLocalPacketHandler(PacketType.RequestGodlikePowers, HandleRequestGodlikePowers);
            AddLocalPacketHandler(PacketType.GodKickUser, HandleGodKickUser);
            AddLocalPacketHandler(PacketType.MoneyBalanceRequest, HandleMoneyBalanceRequest);
            AddLocalPacketHandler(PacketType.EconomyDataRequest, HandleEconomyDataRequest);
            AddLocalPacketHandler(PacketType.RequestPayPrice, HandleRequestPayPrice);
            AddLocalPacketHandler(PacketType.ObjectSaleInfo, HandleObjectSaleInfo);
            AddLocalPacketHandler(PacketType.ObjectBuy, HandleObjectBuy);
            AddLocalPacketHandler(PacketType.GetScriptRunning, HandleGetScriptRunning);
            AddLocalPacketHandler(PacketType.SetScriptRunning, HandleSetScriptRunning);
            AddLocalPacketHandler(PacketType.ScriptReset, HandleScriptReset);
            AddLocalPacketHandler(PacketType.ActivateGestures, HandleActivateGestures);
            AddLocalPacketHandler(PacketType.DeactivateGestures, HandleDeactivateGestures);
            AddLocalPacketHandler(PacketType.ObjectOwner, HandleObjectOwner);
            AddLocalPacketHandler(PacketType.AgentFOV, HandleAgentFOV, false);
            AddLocalPacketHandler(PacketType.ViewerStats, HandleViewerStats);
            AddLocalPacketHandler(PacketType.MapItemRequest, HandleMapItemRequest, false);
            AddLocalPacketHandler(PacketType.TransferAbort, HandleTransferAbort, false);
            AddLocalPacketHandler(PacketType.MuteListRequest, HandleMuteListRequest, false);
            AddLocalPacketHandler(PacketType.UseCircuitCode, HandleUseCircuitCode);
            AddLocalPacketHandler(PacketType.CreateNewOutfitAttachments, HandleCreateNewOutfitAttachments);
            AddLocalPacketHandler(PacketType.AgentHeightWidth, HandleAgentHeightWidth, false);
            AddLocalPacketHandler(PacketType.InventoryDescendents, HandleInventoryDescendents);
            AddLocalPacketHandler(PacketType.DirPlacesQuery, HandleDirPlacesQuery);
            AddLocalPacketHandler(PacketType.DirFindQuery, HandleDirFindQuery);
            AddLocalPacketHandler(PacketType.DirLandQuery, HandleDirLandQuery);
            AddLocalPacketHandler(PacketType.DirPopularQuery, HandleDirPopularQuery);
            AddLocalPacketHandler(PacketType.DirClassifiedQuery, HandleDirClassifiedQuery);
            AddLocalPacketHandler(PacketType.EventInfoRequest, HandleEventInfoRequest);
            AddLocalPacketHandler(PacketType.OfferCallingCard, HandleOfferCallingCard);
            AddLocalPacketHandler(PacketType.AcceptCallingCard, HandleAcceptCallingCard);
            AddLocalPacketHandler(PacketType.DeclineCallingCard, HandleDeclineCallingCard);
            AddLocalPacketHandler(PacketType.ActivateGroup, HandleActivateGroup);
            AddLocalPacketHandler(PacketType.GroupTitlesRequest, HandleGroupTitlesRequest);
            AddLocalPacketHandler(PacketType.GroupProfileRequest, HandleGroupProfileRequest);
            AddLocalPacketHandler(PacketType.GroupMembersRequest, HandleGroupMembersRequest);
            AddLocalPacketHandler(PacketType.GroupRoleDataRequest, HandleGroupRoleDataRequest);
            AddLocalPacketHandler(PacketType.GroupRoleMembersRequest, HandleGroupRoleMembersRequest);
            AddLocalPacketHandler(PacketType.CreateGroupRequest, HandleCreateGroupRequest);
            AddLocalPacketHandler(PacketType.UpdateGroupInfo, HandleUpdateGroupInfo);
            AddLocalPacketHandler(PacketType.SetGroupAcceptNotices, HandleSetGroupAcceptNotices);
            AddLocalPacketHandler(PacketType.GroupTitleUpdate, HandleGroupTitleUpdate);
            AddLocalPacketHandler(PacketType.ParcelDeedToGroup, HandleParcelDeedToGroup);
            AddLocalPacketHandler(PacketType.GroupNoticesListRequest, HandleGroupNoticesListRequest);
            AddLocalPacketHandler(PacketType.GroupNoticeRequest, HandleGroupNoticeRequest);
            AddLocalPacketHandler(PacketType.GroupRoleUpdate, HandleGroupRoleUpdate);
            AddLocalPacketHandler(PacketType.GroupRoleChanges, HandleGroupRoleChanges);
            AddLocalPacketHandler(PacketType.JoinGroupRequest, HandleJoinGroupRequest);
            AddLocalPacketHandler(PacketType.LeaveGroupRequest, HandleLeaveGroupRequest);
            AddLocalPacketHandler(PacketType.EjectGroupMemberRequest, HandleEjectGroupMemberRequest);
            AddLocalPacketHandler(PacketType.InviteGroupRequest, HandleInviteGroupRequest);
            AddLocalPacketHandler(PacketType.StartLure, HandleStartLure);
            AddLocalPacketHandler(PacketType.TeleportLureRequest, HandleTeleportLureRequest);
            AddLocalPacketHandler(PacketType.ClassifiedInfoRequest, HandleClassifiedInfoRequest);
            AddLocalPacketHandler(PacketType.ClassifiedInfoUpdate, HandleClassifiedInfoUpdate);
            AddLocalPacketHandler(PacketType.ClassifiedDelete, HandleClassifiedDelete);
            AddLocalPacketHandler(PacketType.ClassifiedGodDelete, HandleClassifiedGodDelete);
            AddLocalPacketHandler(PacketType.EventGodDelete, HandleEventGodDelete);
            AddLocalPacketHandler(PacketType.EventNotificationAddRequest, HandleEventNotificationAddRequest);
            AddLocalPacketHandler(PacketType.EventNotificationRemoveRequest, HandleEventNotificationRemoveRequest);
            AddLocalPacketHandler(PacketType.RetrieveInstantMessages, HandleRetrieveInstantMessages);
            AddLocalPacketHandler(PacketType.PickDelete, HandlePickDelete);
            AddLocalPacketHandler(PacketType.PickGodDelete, HandlePickGodDelete);
            AddLocalPacketHandler(PacketType.PickInfoUpdate, HandlePickInfoUpdate);
            AddLocalPacketHandler(PacketType.AvatarNotesUpdate, HandleAvatarNotesUpdate);
            AddLocalPacketHandler(PacketType.AvatarInterestsUpdate, HandleAvatarInterestsUpdate);
            AddLocalPacketHandler(PacketType.GrantUserRights, HandleGrantUserRights);
            AddLocalPacketHandler(PacketType.PlacesQuery, HandlePlacesQuery);
            AddLocalPacketHandler(PacketType.UpdateMuteListEntry, HandleUpdateMuteListEntry);
            AddLocalPacketHandler(PacketType.RemoveMuteListEntry, HandleRemoveMuteListEntry);
            AddLocalPacketHandler(PacketType.UserReport, HandleUserReport);
            AddLocalPacketHandler(PacketType.FindAgent, HandleFindAgent);
            AddLocalPacketHandler(PacketType.TrackAgent, HandleTrackAgent);
            AddLocalPacketHandler(PacketType.GodUpdateRegionInfo, HandleGodUpdateRegionInfoUpdate);
            AddLocalPacketHandler(PacketType.GodlikeMessage, HandleGodlikeMessage);
            AddLocalPacketHandler(PacketType.StateSave, HandleSaveStatePacket);
            AddLocalPacketHandler(PacketType.GroupAccountDetailsRequest, HandleGroupAccountDetailsRequest);
            AddLocalPacketHandler(PacketType.GroupAccountSummaryRequest, HandleGroupAccountSummaryRequest);
            AddLocalPacketHandler(PacketType.GroupAccountTransactionsRequest, HandleGroupTransactionsDetailsRequest);
            AddLocalPacketHandler(PacketType.FreezeUser, HandleFreezeUser);
            AddLocalPacketHandler(PacketType.EjectUser, HandleEjectUser);
            AddLocalPacketHandler(PacketType.ParcelBuyPass, HandleParcelBuyPass);
            AddLocalPacketHandler(PacketType.ParcelGodMarkAsContent, HandleParcelGodMarkAsContent);
            AddLocalPacketHandler(PacketType.GroupActiveProposalsRequest, HandleGroupActiveProposalsRequest);
            AddLocalPacketHandler(PacketType.GroupVoteHistoryRequest, HandleGroupVoteHistoryRequest);
            AddLocalPacketHandler(PacketType.SimWideDeletes, HandleSimWideDeletes);
            AddLocalPacketHandler(PacketType.SendPostcard, HandleSendPostcard);
            AddLocalPacketHandler(PacketType.ChangeInventoryItemFlags, HandleChangeInventoryItemFlags);
            AddLocalPacketHandler(PacketType.RevokePermissions, HandleRevokePermissions);
            AddGenericPacketHandler("autopilot", HandleAutopilot);
        }

        #region Packet Handlers

        public int TotalAgentUpdates { get; set; }

        #region Scene/Avatar

        // Threshold for body rotation to be a significant agent update
        // use the abs of cos
        private const float QDELTABody = 1.0f - 0.00001f;
        private const float QDELTAHead = 1.0f - 0.00001f;
        // Threshold for camera rotation to be a significant agent update
        private const float VDELTA = 0.01f;

        /// <summary>
        /// This checks the update significance against the last update made.
        /// </summary>
        /// <remarks>Can only be called by one thread at a time</remarks>
        /// <returns></returns>
        /// <param name='x'></param>
        public bool CheckAgentUpdateSignificance(AgentUpdatePacket.AgentDataBlock x)
        {
            return CheckAgentMovementUpdateSignificance(x) || CheckAgentCameraUpdateSignificance(x);
        }

        /// <summary>
        /// This checks the movement/state update significance against the last update made.
        /// </summary>
        /// <remarks>Can only be called by one thread at a time</remarks>
        /// <returns></returns>
        /// <param name='x'></param>
        private bool CheckAgentMovementUpdateSignificance(AgentUpdatePacket.AgentDataBlock x)
        {
            if(
                (x.ControlFlags != m_thisAgentUpdateArgs.ControlFlags)   // significant if control flags changed
                || (x.ControlFlags & ~(uint)AgentManager.ControlFlags.AGENT_CONTROL_FINISH_ANIM) != (uint)AgentManager.ControlFlags.NONE
                || (x.Flags != m_thisAgentUpdateArgs.Flags)                 // significant if Flags changed
                || (x.State != m_thisAgentUpdateArgs.State)                 // significant if Stats changed
                || (MathF.Abs(x.Far - m_thisAgentUpdateArgs.Far) >= 32f)      // significant if far distance changed
                )
                return true;

            float qdelta = MathF.Abs(x.BodyRotation.Dot(m_thisAgentUpdateArgs.BodyRotation));
            return qdelta < QDELTABody; // significant if body rotation above(below cos) threshold
        }

        /// <summary>
        /// This checks the camera update significance against the last update made.
        /// </summary>
        /// <remarks>Can only be called by one thread at a time</remarks>
        /// <returns></returns>
        /// <param name='x'></param>
        private bool CheckAgentCameraUpdateSignificance(AgentUpdatePacket.AgentDataBlock x)
        {
             return (MathF.Abs(x.CameraCenter.X - m_thisAgentUpdateArgs.CameraCenter.X) > VDELTA ||
                     MathF.Abs(x.CameraCenter.Y - m_thisAgentUpdateArgs.CameraCenter.Y) > VDELTA ||
                     MathF.Abs(x.CameraCenter.Z - m_thisAgentUpdateArgs.CameraCenter.Z) > VDELTA ||

                     MathF.Abs(x.CameraAtAxis.X - m_thisAgentUpdateArgs.CameraAtAxis.X) > VDELTA ||
                     MathF.Abs(x.CameraAtAxis.Y - m_thisAgentUpdateArgs.CameraAtAxis.Y) > VDELTA ||

                     MathF.Abs(x.CameraUpAxis.X - m_thisAgentUpdateArgs.CameraUpAxis.X) > VDELTA ||
                     MathF.Abs(x.CameraUpAxis.Y - m_thisAgentUpdateArgs.CameraUpAxis.Y) > VDELTA
            );
         }

        private void HandleAgentUpdate(Packet packet)
        {
            if(OnAgentUpdate is null)
                return;

            AgentUpdatePacket agentUpdate = (AgentUpdatePacket)packet;
            AgentUpdatePacket.AgentDataBlock x = agentUpdate.AgentData;

            if (x.AgentID.NotEqual(m_agentId) || x.SessionID.NotEqual(m_sessionId))
                return;

            uint seq = packet.Header.Sequence;

            TotalAgentUpdates++;
            // dont let ignored updates pollute this throttles
            if(SceneAgent is null || SceneAgent.IsChildAgent ||
                    SceneAgent.IsInTransit || seq <= m_thisAgentUpdateArgs.lastpacketSequence )
            {
                // throttle reset is done at MoveAgentIntoRegion()
                // called by scenepresence on completemovement
                //PacketPool.Instance.ReturnPacket(packet);
                return;
            }

            m_thisAgentUpdateArgs.lastpacketSequence = seq;

            OnPreAgentUpdate?.Invoke(this, m_thisAgentUpdateArgs);

            bool movement;
            bool camera;

            double now = Util.GetTimeStampMS();
            if(now - m_thisAgentUpdateArgs.lastUpdateTS > 500.0) // at least 2 per sec
            {
                movement = true;
                camera = true;
            }
            else
            {
                movement = CheckAgentMovementUpdateSignificance(x);
                camera = CheckAgentCameraUpdateSignificance(x);
            }

            // Was there a significant movement/state change?
            if (movement)
            {
                m_thisAgentUpdateArgs.BodyRotation = x.BodyRotation;
                m_thisAgentUpdateArgs.ControlFlags = x.ControlFlags;
                m_thisAgentUpdateArgs.Far = x.Far;
                m_thisAgentUpdateArgs.Flags = x.Flags;
                m_thisAgentUpdateArgs.HeadRotation = x.HeadRotation;
                m_thisAgentUpdateArgs.State = x.State;

                m_thisAgentUpdateArgs.NeedsCameraCollision = !camera;

                OnAgentUpdate?.Invoke(this, m_thisAgentUpdateArgs);
            }

            // Was there a significant camera(s) change?
            if (camera)
            {
                m_thisAgentUpdateArgs.CameraAtAxis = x.CameraAtAxis;
                m_thisAgentUpdateArgs.CameraCenter = x.CameraCenter;
                m_thisAgentUpdateArgs.CameraLeftAxis = x.CameraLeftAxis;
                m_thisAgentUpdateArgs.CameraUpAxis = x.CameraUpAxis;

                m_thisAgentUpdateArgs.NeedsCameraCollision = true;

                OnAgentCameraUpdate?.Invoke(this, m_thisAgentUpdateArgs);
            }

            if(movement && camera)
                m_thisAgentUpdateArgs.lastUpdateTS = now;
        }

        private void HandleMoneyTransferRequest(Packet Pack)
        {
            if(OnMoneyTransferRequest is null)
                return;
            MoneyTransferRequestPacket money = (MoneyTransferRequestPacket)Pack;
            // validate the agent owns the agentID and sessionID
            if (money.MoneyData.SourceID.Equals(m_agentId) && money.AgentData.AgentID.Equals(m_agentId) &&
                money.AgentData.SessionID.Equals(m_sessionId))
            {
                OnMoneyTransferRequest?.Invoke(money.MoneyData.SourceID, money.MoneyData.DestID,
                                            money.MoneyData.Amount, money.MoneyData.TransactionType,
                                            Util.FieldToString(money.MoneyData.Description));
            }
        }

        private void HandleParcelGodMarkAsContent(Packet packet)
        {
            if(OnParcelGodMark is null)
                return;

            ParcelGodMarkAsContentPacket ParcelGodMarkAsContent = (ParcelGodMarkAsContentPacket)packet;
            if(m_sessionId != ParcelGodMarkAsContent.AgentData.SessionID || m_agentId != ParcelGodMarkAsContent.AgentData.AgentID)
                return;

            OnParcelGodMark?.Invoke(this,
                                m_agentId,
                                ParcelGodMarkAsContent.ParcelData.LocalID);
        }

        private void HandleFreezeUser(Packet packet)
        {
            if(OnParcelFreezeUser is null)
                return;

            FreezeUserPacket FreezeUser = (FreezeUserPacket)packet;
             if(m_sessionId != FreezeUser.AgentData.SessionID || m_agentId != FreezeUser.AgentData.AgentID)
                return;

            OnParcelFreezeUser?.Invoke(this,
                                m_agentId,
                                FreezeUser.Data.Flags,
                                FreezeUser.Data.TargetID);
        }

        private void HandleEjectUser(Packet packet)
        {
            if(OnParcelEjectUser is null)
                return;

            EjectUserPacket EjectUser = (EjectUserPacket)packet;
            if(m_sessionId != EjectUser.AgentData.SessionID || m_agentId != EjectUser.AgentData.AgentID)
                return;

            OnParcelEjectUser?.Invoke(this,
                                m_agentId,
                                EjectUser.Data.Flags,
                                EjectUser.Data.TargetID);
        }

        private void HandleParcelBuyPass(Packet packet)
        {
            if(OnParcelBuyPass is null)
                return;

            ParcelBuyPassPacket ParcelBuyPass = (ParcelBuyPassPacket)packet;

            if(m_sessionId != ParcelBuyPass.AgentData.SessionID || m_agentId != ParcelBuyPass.AgentData.AgentID)
                return;

            OnParcelBuyPass?.Invoke(this,
                                m_agentId,
                                ParcelBuyPass.ParcelData.LocalID);
        }

        private void HandleParcelBuyRequest(Packet Pack)
        {
            ParcelBuyPacket parcel = (ParcelBuyPacket)Pack;
            if (parcel.AgentData.AgentID.Equals(m_agentId) && parcel.AgentData.SessionID.Equals(m_sessionId))
            {
                OnParcelBuy?.Invoke(m_agentId, parcel.Data.GroupID, parcel.Data.Final,
                                    parcel.Data.IsGroupOwned,
                                    parcel.Data.RemoveContribution, parcel.Data.LocalID, parcel.ParcelData.Area,
                                    parcel.ParcelData.Price,
                                    false);
            }
        }

        private void HandleUUIDGroupNameRequest(Packet Pack)
        {
            if(OnUUIDGroupNameRequest is null)
                return;

            ScenePresence sp = (ScenePresence)SceneAgent;
            if(sp is null || sp.IsDeleted || (sp.IsInTransit && !sp.IsInLocalTransit))
                return;

            UUIDGroupNameRequestPacket upack = (UUIDGroupNameRequestPacket)Pack;
            for (int i = 0; i < upack.UUIDNameBlock.Length; i++)
            {
                OnUUIDGroupNameRequest?.Invoke(upack.UUIDNameBlock[i].ID, this);
            }
        }

        public void HandleGenericMessage(Packet pack)
        {
            if (m_genericPacketHandlers.Count == 0)
                return;

            GenericMessagePacket gmpack = (GenericMessagePacket)pack;
            if (gmpack.AgentData.SessionID.NotEqual(m_sessionId) || gmpack.AgentData.AgentID.NotEqual(m_agentId))
                return;

            string method = Util.FieldToString(gmpack.MethodData.Method).ToLower().Trim();

            if (m_genericPacketHandlers.TryGetValue(method, out GenericMessage handlerGenericMessage))
            {
                List<string> msg = new();
                List<byte[]> msgBytes = new();

                foreach (GenericMessagePacket.ParamListBlock block in gmpack.ParamList)
                {
                    msg.Add(Util.FieldToString(block.Parameter));
                    msgBytes.Add(block.Parameter);
                }

                try
                {
                    OnBinaryGenericMessage?.Invoke(this, method, msgBytes.ToArray());
                    handlerGenericMessage?.Invoke(this, method, msg);
                }
                catch (Exception e)
                {
                    m_log.ErrorFormat(
                        "[LLCLIENTVIEW]: Exception when handling generic message {0}{1}", e.Message, e.StackTrace);
                }
            }
        }

        public void HandleObjectGroupRequest(Packet Pack)
        {
            if(OnObjectGroupRequest is null)
                return;

            ObjectGroupPacket ogpack = (ObjectGroupPacket)Pack;
            if (ogpack.AgentData.SessionID.NotEqual(m_sessionId))
                return;

            for (int i = 0; i < ogpack.ObjectData.Length; i++)
            {
                OnObjectGroupRequest?.Invoke(this, ogpack.AgentData.GroupID, ogpack.ObjectData[i].ObjectLocalID, UUID.Zero);
            }
        }

        private void HandleViewerEffect(Packet Pack)
        {
            if(OnViewerEffect is null)
                return;

            ViewerEffectPacket viewer = (ViewerEffectPacket)Pack;
            if (viewer.AgentData.SessionID.NotEqual(m_sessionId) || m_agentId != viewer.AgentData.AgentID)
                return;

            int length = viewer.Effect.Length;
            List<ViewerEffectEventHandlerArg> args = new(length);
            for (int i = 0; i < length; i++)
            {
                //copy the effects block arguments into the event handler arg.
                ViewerEffectEventHandlerArg argument = new()
                {
                    AgentID = viewer.Effect[i].AgentID,
                    Color = viewer.Effect[i].Color,
                    Duration = viewer.Effect[i].Duration,
                    ID = viewer.Effect[i].ID,
                    Type = viewer.Effect[i].Type,
                    TypeData = viewer.Effect[i].TypeData
                };
                args.Add(argument);
            }
            OnViewerEffect?.Invoke(this, args);
        }

        private void HandleVelocityInterpolateOff(Packet Pack)
        {
            VelocityInterpolateOffPacket p = (VelocityInterpolateOffPacket)Pack;
            if (p.AgentData.SessionID.NotEqual(m_sessionId) || p.AgentData.AgentID.NotEqual(m_agentId))
                return ;

            m_VelocityInterpolate = false;
        }

        private void HandleVelocityInterpolateOn(Packet Pack)
        {
            VelocityInterpolateOnPacket p = (VelocityInterpolateOnPacket)Pack;
            if (p.AgentData.SessionID.NotEqual(m_sessionId) || p.AgentData.AgentID.NotEqual(m_agentId))
                return;

            m_VelocityInterpolate = true;
        }

        private void HandleAvatarPropertiesRequest(Packet Pack)
        {
            AvatarPropertiesRequestPacket avatarProperties = (AvatarPropertiesRequestPacket)Pack;

            if (avatarProperties.AgentData.SessionID.NotEqual(m_sessionId) || avatarProperties.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnRequestAvatarProperties?.Invoke(this, avatarProperties.AgentData.AvatarID);
        }

        private void HandleChatFromViewer(Packet Pack)
        {
            if (OnChatFromClient is null)
                return;

            ChatFromViewerPacket inchatpack = (ChatFromViewerPacket)Pack;

            if (inchatpack.AgentData.SessionID.NotEqual(m_sessionId) || inchatpack.AgentData.AgentID.NotEqual(m_agentId))
                return;

            ChatFromViewerPacket.ChatDataBlock packdata = inchatpack.ChatData;
            OSChatMessage args = new()
            {
                Channel = packdata.Channel,
                Message = Utils.BytesToString(packdata.Message),
                Type = (ChatTypeEnum)packdata.Type,
                Position = SceneAgent.AbsolutePosition,

                Scene = Scene,
                Sender = this
            };
            OnChatFromClient?.Invoke(this, args);
        }

        private void HandlerAvatarPropertiesUpdate(Packet Pack)
        {
            if (OnUpdateAvatarProperties is null)
                return;

            AvatarPropertiesUpdatePacket avatarProps = (AvatarPropertiesUpdatePacket)Pack;

            if (avatarProps.AgentData.SessionID.NotEqual(m_sessionId) || avatarProps.AgentData.AgentID.NotEqual(m_agentId))
               return;

            AvatarPropertiesUpdatePacket.PropertiesDataBlock Properties = avatarProps.PropertiesData;
            UserProfileProperties UserProfile = new()
            {
                UserId = AgentId,
                WebUrl = Utils.BytesToString(Properties.ProfileURL),
                ImageId = Properties.ImageID,
                FirstLifeImageId = Properties.FLImageID,
                AboutText = Utils.BytesToString(Properties.AboutText),
                FirstLifeText = Utils.BytesToString(Properties.FLAboutText),
                PublishProfile = Properties.AllowPublish,
                PublishMature = Properties.MaturePublish
            };
            OnUpdateAvatarProperties?.Invoke(this, UserProfile);
        }

        private void HandlerScriptDialogReply(Packet Pack)
        {
            if (OnChatFromClient is null)
                return;

            ScriptDialogReplyPacket rdialog = (ScriptDialogReplyPacket)Pack;
            //m_log.DebugFormat("[CLIENT]: Received ScriptDialogReply from {0}", rdialog.Data.ObjectID);
            if (rdialog.AgentData.SessionID.NotEqual(m_sessionId) || rdialog.AgentData.AgentID.NotEqual(m_agentId))
                return;

            ScriptDialogReplyPacket.DataBlock rdialogData = rdialog.Data;
            OSChatMessage args = new()
            {
                Channel = rdialogData.ChatChannel,
                Message = Utils.BytesToString(rdialogData.ButtonLabel),
                Type = ChatTypeEnum.Region, //Behaviour in SL is that the response can be heard from any distance
                Scene = Scene,
                Sender = this
            };
            OnChatFromClient?.Invoke(this, args);
        }

        private void HandlerImprovedInstantMessage(Packet Pack)
        {
            if(OnInstantMessage is null)
                return;

            ImprovedInstantMessagePacket msgpack = (ImprovedInstantMessagePacket)Pack;
            if (msgpack.AgentData.SessionID.NotEqual(m_sessionId) || msgpack.AgentData.AgentID.NotEqual(m_agentId))
                return;

            string IMfromName = Util.FieldToString(msgpack.MessageBlock.FromAgentName);
            string IMmessage = Utils.BytesToString(msgpack.MessageBlock.Message);

            GridInstantMessage im = new(Scene,
                    msgpack.AgentData.AgentID,
                    IMfromName,
                    msgpack.MessageBlock.ToAgentID,
                    msgpack.MessageBlock.Dialog,
                    msgpack.MessageBlock.FromGroup,
                    IMmessage,
                    msgpack.MessageBlock.ID,
                    msgpack.MessageBlock.Offline != 0,
                    msgpack.MessageBlock.Position,
                    msgpack.MessageBlock.BinaryBucket,
                    true);

            OnInstantMessage?.Invoke(this, im);
        }

        private void HandlerAcceptFriendship(Packet Pack)
        {
            if(OnApproveFriendRequest is null)
                return;

            AcceptFriendshipPacket afriendpack = (AcceptFriendshipPacket)Pack;
            if (afriendpack.AgentData.SessionID.NotEqual(m_sessionId) || afriendpack.AgentData.AgentID.NotEqual(m_agentId))
               return;

            // My guess is this is the folder to stick the calling card into
            List<UUID> callingCardFolders = new();
            UUID transactionID = afriendpack.TransactionBlock.TransactionID;

            for (int fi = 0; fi < afriendpack.FolderData.Length; fi++)
            {
                callingCardFolders.Add(afriendpack.FolderData[fi].FolderID);
            }

            OnApproveFriendRequest?.Invoke(this, transactionID, callingCardFolders);
        }

        private void HandlerDeclineFriendship(Packet Pack)
        {
            DeclineFriendshipPacket dfriendpack = (DeclineFriendshipPacket)Pack;

            if (dfriendpack.AgentData.SessionID.NotEqual(m_sessionId) || dfriendpack.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnDenyFriendRequest?.Invoke(this,
                                    dfriendpack.TransactionBlock.TransactionID,
                                    null);
        }

        private void HandlerTerminateFriendship(Packet Pack)
        {
            TerminateFriendshipPacket tfriendpack = (TerminateFriendshipPacket)Pack;

            if (tfriendpack.AgentData.SessionID.NotEqual(m_sessionId) || tfriendpack.AgentData.AgentID.NotEqual(m_agentId))
                return;

            UUID exFriendID = tfriendpack.ExBlock.OtherID;
            FriendshipTermination TerminateFriendshipHandler = OnTerminateFriendship;
            TerminateFriendshipHandler?.Invoke(this, exFriendID);
        }

        private void HandleFindAgent(Packet packet)
        {
            FindAgentPacket FindAgent = (FindAgentPacket)packet;
            OnFindAgent?.Invoke(this,FindAgent.AgentBlock.Hunter,FindAgent.AgentBlock.Prey);
        }

        private void HandleTrackAgent(Packet packet)
        {
            TrackAgentPacket TrackAgent = (TrackAgentPacket)packet;

            if(TrackAgent.AgentData.AgentID.NotEqual(m_agentId) || TrackAgent.AgentData.SessionID.NotEqual(m_sessionId))
                return;

            OnTrackAgent?.Invoke(this,
                                TrackAgent.AgentData.AgentID,
                                TrackAgent.TargetData.PreyID);
        }

        private void HandlerRezObject(Packet Pack)
        {
            RezObjectPacket rezPacket = (RezObjectPacket)Pack;

            if (rezPacket.AgentData.SessionID.NotEqual(m_sessionId) || rezPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            UUID rezGroupID = rezPacket.AgentData.GroupID;
            if(!IsGroupMember(rezGroupID))
                rezGroupID = UUID.Zero;
            OnRezObject?.Invoke(this, rezPacket.InventoryData.ItemID, rezGroupID, rezPacket.RezData.RayEnd,
                            rezPacket.RezData.RayStart, rezPacket.RezData.RayTargetID,
                            rezPacket.RezData.BypassRaycast, rezPacket.RezData.RayEndIsIntersection,
                            rezPacket.RezData.RezSelected, rezPacket.RezData.RemoveItem,
                            rezPacket.RezData.FromTaskID);
        }

        private class DeRezObjectInfo
        {
            public List<uint> objectids;
            public HashSet<int> rcvedpackets;
        }
        private Dictionary<UUID, DeRezObjectInfo> m_DeRezObjectDelayed;

        private void HandlerDeRezObject(Packet Pack)
        {
            if (OnDeRezObject is null)
                return;

            DeRezObjectPacket DeRezPacket = (DeRezObjectPacket)Pack;
            DeRezObjectPacket.AgentDataBlock DeRezPacketAgentData = DeRezPacket.AgentData;
            if (DeRezPacketAgentData.AgentID.NotEqual(m_agentId) || DeRezPacketAgentData.SessionID.NotEqual(m_sessionId))
                return;

            List<uint> deRezIDs;
            DeRezObjectPacket.AgentBlockBlock DeRezPacketAgentBlock = DeRezPacket.AgentBlock;
            DeRezAction action = (DeRezAction)DeRezPacketAgentBlock.Destination;
            int numberPackets = DeRezPacketAgentBlock.PacketCount;
            int curPacket = DeRezPacketAgentBlock.PacketNumber;
            UUID id = DeRezPacketAgentBlock.TransactionID;

            if (numberPackets > 1)
            {
                m_DeRezObjectDelayed ??= new Dictionary<UUID, DeRezObjectInfo>();

                if (!m_DeRezObjectDelayed.TryGetValue(id, out DeRezObjectInfo info))
                {
                    deRezIDs = new List<uint>(DeRezPacket.ObjectData.Length);
                    info = new DeRezObjectInfo
                    {
                        rcvedpackets = new HashSet<int>() { curPacket },
                        objectids = deRezIDs
                    };
                    m_DeRezObjectDelayed[id] = info;
                }
                else
                {
                    if(info.rcvedpackets.Contains(curPacket))
                        return;
                    info.rcvedpackets.Add(curPacket);
                    deRezIDs = info.objectids;
                }

                foreach (DeRezObjectPacket.ObjectDataBlock data in DeRezPacket.ObjectData)
                {
                    deRezIDs.Add(data.ObjectLocalID);
                }

                if (info.rcvedpackets.Count < numberPackets)
                    return;

                m_DeRezObjectDelayed.Remove(id);
                info.objectids = null;
                info.rcvedpackets = null;
            }
            else
            {
                deRezIDs = new List<uint>(DeRezPacket.ObjectData.Length);
                foreach (DeRezObjectPacket.ObjectDataBlock data in DeRezPacket.ObjectData)
                {
                    deRezIDs.Add(data.ObjectLocalID);
                }
            }

            OnDeRezObject?.Invoke(this, deRezIDs, DeRezPacketAgentBlock.GroupID,
                                action, DeRezPacketAgentBlock.DestinationID);
        }

        private void HandlerRezRestoreToWorld(Packet Pack)
        {
            RezRestoreToWorldPacket restore = (RezRestoreToWorldPacket)Pack;

            if (restore.AgentData.SessionID.NotEqual(m_sessionId) || restore.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnRezRestoreToWorld?.Invoke(this, restore.InventoryData.ItemID);
        }

        private void HandlerModifyLand(Packet Pack)
        {
            if (OnModifyTerrain is null)
                return;

            ModifyLandPacket modify = (ModifyLandPacket)Pack;

            if (modify.ParcelData.Length == 0)
                return;

            if (modify.AgentData.SessionID.NotEqual(m_sessionId) || modify.AgentData.AgentID.NotEqual(m_agentId))
            return;

            //m_log.Info("[LAND]: LAND:" + modify.ToString());
            for (int i = 0; i < modify.ParcelData.Length; i++)
            {
                OnModifyTerrain?.Invoke(m_agentId, modify.ModifyBlock.Height, modify.ModifyBlock.Seconds,
                                    modify.ModifyBlockExtended[i].BrushSize, modify.ModifyBlock.Action,
                                    modify.ParcelData[i].North, modify.ParcelData[i].West,
                                    modify.ParcelData[i].South, modify.ParcelData[i].East,
                                    modify.ParcelData[i].LocalID);
            }
        }

        public uint m_viewerHandShakeFlags = 0;

        private void HandlerRegionHandshakeReply(Packet Pack)
        {
            if (OnRegionHandShakeReply is null)
                return; // silence the warning

            RegionHandshakeReplyPacket rsrpkt = (RegionHandshakeReplyPacket)Pack;
            if(rsrpkt.AgentData.AgentID.NotEqual(m_agentId) || rsrpkt.AgentData.SessionID.NotEqual(m_sessionId))
                return;

            if(m_supportViewerCache)
                m_viewerHandShakeFlags = rsrpkt.RegionInfo.Flags;
            else
                m_viewerHandShakeFlags = 0;

            OnRegionHandShakeReply?.Invoke(this);
        }

        private void HandlerAgentWearablesRequest(Packet Pack)
        {
            OnRequestWearables?.Invoke(this);
            OnRequestAvatarsData?.Invoke(this);
        }

        private void HandlerAgentSetAppearance(Packet Pack)
        {
            if(OnSetAppearance is null)
                return;

            AgentSetAppearancePacket appear = (AgentSetAppearancePacket)Pack;
            if (appear.AgentData.SessionID.NotEqual(m_sessionId) || appear.AgentData.AgentID.NotEqual(m_agentId))
                return;

            try
            {
                Vector3 avSize = appear.AgentData.Size;
                byte[] visualparams = new byte[appear.VisualParam.Length];
                for (int i = 0; i < appear.VisualParam.Length; i++)
                    visualparams[i] = appear.VisualParam[i].ParamValue;
                //var b = appear.WearableData[0];

                Primitive.TextureEntry te = null;
                if (appear.ObjectData.TextureEntry.Length > 1)
                    te = new Primitive.TextureEntry(appear.ObjectData.TextureEntry, 0, appear.ObjectData.TextureEntry.Length);

                WearableCacheItem[] cacheitems = new WearableCacheItem[appear.WearableData.Length];
                for (int i=0; i<appear.WearableData.Length;i++)
                    cacheitems[i] = new WearableCacheItem(){
                        CacheId = appear.WearableData[i].CacheID,
                        TextureIndex=Convert.ToUInt32(appear.WearableData[i].TextureIndex)
                        };

                OnSetAppearance?.Invoke(this, te, visualparams,avSize, cacheitems);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat(
                    "[CLIENT VIEW]: AgentSetApperance packet handler threw an exception, {0}",
                    e);
            }
        }

        private void HandlerAgentIsNowWearing(Packet Pack)
        {
            if (OnAvatarNowWearing is null)
                return;

            AgentIsNowWearingPacket nowWearing = (AgentIsNowWearingPacket)Pack;

            if (nowWearing.AgentData.SessionID.NotEqual(m_sessionId) || nowWearing.AgentData.AgentID.NotEqual(m_agentId))
                return;

            AvatarWearingArgs wearingArgs = new();
            for (int i = 0; i < nowWearing.WearableData.Length; i++)
            {
                //m_log.DebugFormat("[XXX]: Wearable type {0} item {1}", nowWearing.WearableData[i].WearableType, nowWearing.WearableData[i].ItemID);
                AvatarWearingArgs.Wearable wearable = new(nowWearing.WearableData[i].ItemID,
                                                    nowWearing.WearableData[i].WearableType);
                wearingArgs.NowWearing.Add(wearable);
            }

            OnAvatarNowWearing?.Invoke(this, wearingArgs);
        }

        private void HandlerRezSingleAttachmentFromInv(Packet Pack)
        {
            RezSingleAttachmentFromInvPacket rez = (RezSingleAttachmentFromInvPacket)Pack;
            if (rez.AgentData.SessionID.NotEqual(m_sessionId) || rez.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnRezSingleAttachmentFromInv?.Invoke(this, rez.ObjectData.ItemID,
                                           rez.ObjectData.AttachmentPt);
        }

        private void HandleRezMultipleAttachmentsFromInv(Packet Pack)
        {
            if(OnRezMultipleAttachmentsFromInv is null)
                return;

            RezMultipleAttachmentsFromInvPacket rez = (RezMultipleAttachmentsFromInvPacket)Pack;
            RezMultipleAttachmentsFromInvPacket.AgentDataBlock rezAgentData = rez.AgentData;
            if (rezAgentData.SessionID.NotEqual(m_sessionId) || rezAgentData.AgentID.NotEqual(m_agentId))
                return;

            List<KeyValuePair<UUID, uint>> rezlist = new();
            foreach (RezMultipleAttachmentsFromInvPacket.ObjectDataBlock obj in rez.ObjectData)
                rezlist.Add(new KeyValuePair<UUID, uint>(obj.ItemID, obj.AttachmentPt));

            OnRezMultipleAttachmentsFromInv?.Invoke(this, rezlist);
        }

        private void HandleDetachAttachmentIntoInv(Packet Pack)
        {
            if (OnDetachAttachmentIntoInv is null)
                return;

                DetachAttachmentIntoInvPacket detachtoInv = (DetachAttachmentIntoInvPacket)Pack;
                if(detachtoInv.ObjectData.AgentID.NotEqual(m_agentId))
                    return;

                OnDetachAttachmentIntoInv?.Invoke(detachtoInv.ObjectData.ItemID, this);
        }

        private void HandleObjectAttach(Packet Pack)
        {
            if (OnObjectAttach is null)
                return;

            ObjectAttachPacket att = (ObjectAttachPacket)Pack;
            if (att.AgentData.SessionID.NotEqual(m_sessionId) || att.AgentData.AgentID.NotEqual(m_agentId))
                return;

            if (att.ObjectData.Length > 0)
                OnObjectAttach?.Invoke(this, att.ObjectData[0].ObjectLocalID, att.AgentData.AttachmentPoint, false);
        }

        private void HandleObjectDetach(Packet Pack)
        {
            if(OnObjectDetach is null)
                return;

            ObjectDetachPacket dett = (ObjectDetachPacket)Pack;
            if (dett.AgentData.SessionID.NotEqual(m_sessionId) || dett.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int j = 0; j < dett.ObjectData.Length; j++)
            {
                uint obj = dett.ObjectData[j].ObjectLocalID;
                OnObjectDetach?.Invoke(obj, this);
            }
        }

        private void HandleObjectDrop(Packet Pack)
        {
            if(OnObjectDrop is null)
                return;

            ObjectDropPacket dropp = (ObjectDropPacket)Pack;

            if (dropp.AgentData.SessionID.NotEqual(m_sessionId) || dropp.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int j = 0; j < dropp.ObjectData.Length; j++)
            {
                uint obj = dropp.ObjectData[j].ObjectLocalID;
                OnObjectDrop?.Invoke(obj, this);
            }
        }

        private void HandleSetAlwaysRun(Packet Pack)
        {
            SetAlwaysRunPacket run = (SetAlwaysRunPacket)Pack;

            if (run.AgentData.SessionID.NotEqual(m_sessionId) || run.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnSetAlwaysRun?.Invoke(this, run.AgentData.AlwaysRun);
        }

        private void HandleCompleteAgentMovement(Packet Pack)
        {
            //m_log.DebugFormat("[LLClientView] HandleCompleteAgentMovement");

            CompleteAgentMovementPacket cmp = (CompleteAgentMovementPacket)Pack;
            if(cmp.AgentData.AgentID.NotEqual(m_agentId) || cmp.AgentData.SessionID.NotEqual(m_sessionId) || cmp.AgentData.CircuitCode != m_circuitCode)
                return;

            OnCompleteMovementToRegion?.Invoke(this, true);
        }

        private void HandleAgentAnimation(Packet Pack)
        {
            if(OnChangeAnim is null)
                return;

            AgentAnimationPacket AgentAni = (AgentAnimationPacket)Pack;
            if (AgentAni.AgentData.SessionID.NotEqual(m_sessionId) || AgentAni.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < AgentAni.AnimationList.Length; i++)
                    OnChangeAnim?.Invoke(AgentAni.AnimationList[i].AnimID, AgentAni.AnimationList[i].StartAnim, false);

            OnChangeAnim?.Invoke(UUID.Zero, false, true);
        }

        private void HandleAgentRequestSit(Packet Pack)
        {
            if (OnAgentRequestSit is null)
                return;

            AgentRequestSitPacket agentRequestSit = (AgentRequestSitPacket)Pack;

            if (agentRequestSit.AgentData.SessionID.NotEqual(m_sessionId) || agentRequestSit.AgentData.AgentID.NotEqual(m_agentId))
                return;

            if (SceneAgent.IsChildAgent)
            {
                SendCantSitBecauseChildAgentResponse();
                return;
            }

            OnAgentRequestSit?.Invoke(this, agentRequestSit.AgentData.AgentID,
                                        agentRequestSit.TargetObject.TargetID, agentRequestSit.TargetObject.Offset);
        }

        private void HandleAgentSit(Packet Pack)
        {
            if (OnAgentSit is null)
                return;

            AgentSitPacket agentSit = (AgentSitPacket)Pack;
            if (agentSit.AgentData.SessionID.NotEqual(m_sessionId) || agentSit.AgentData.AgentID.NotEqual(m_agentId))
                return;

            if (SceneAgent.IsChildAgent)
            {
                SendCantSitBecauseChildAgentResponse();
                return;
            }

            OnAgentSit?.Invoke(this, agentSit.AgentData.AgentID);
        }

        /// <summary>
        /// Used when a child agent gets a sit response which should not be fulfilled.
        /// </summary>
        private void SendCantSitBecauseChildAgentResponse()
        {
            SendAlertMessage("Try moving closer.  Can't sit on object because it is not in the same region as you.");
        }

        private void HandleSoundTrigger(Packet Pack)
        {
            SoundTriggerPacket soundTriggerPacket = (SoundTriggerPacket)Pack;

                // UUIDS are sent as zeroes by the client, substitute agent's id
            OnSoundTrigger?.Invoke(soundTriggerPacket.SoundData.SoundID, m_agentId,
                m_agentId, m_agentId,
                soundTriggerPacket.SoundData.Gain, soundTriggerPacket.SoundData.Position,
                soundTriggerPacket.SoundData.Handle);
        }

        private void HandleAvatarPickerRequest(Packet Pack)
        {
            AvatarPickerRequestPacket avRequestQuery = (AvatarPickerRequestPacket)Pack;

            if (avRequestQuery.AgentData.SessionID.NotEqual(m_sessionId) || avRequestQuery.AgentData.AgentID.NotEqual(m_agentId))
                return;

            AvatarPickerRequestPacket.AgentDataBlock Requestdata = avRequestQuery.AgentData;
            AvatarPickerRequestPacket.DataBlock querydata = avRequestQuery.Data;
            //m_log.Debug("Agent Sends:" + Utils.BytesToString(querydata.Name));

            OnAvatarPickerRequest?.Invoke(this, Requestdata.AgentID, Requestdata.QueryID,
                                        Utils.BytesToString(querydata.Name));
        }

        private void HandleAgentDataUpdateRequest(Packet Pack)
        {
            AgentDataUpdateRequestPacket avRequestDataUpdatePacket = (AgentDataUpdateRequestPacket)Pack;

            if (avRequestDataUpdatePacket.AgentData.SessionID.NotEqual(m_sessionId) || avRequestDataUpdatePacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnAgentDataUpdateRequest?.Invoke(this, avRequestDataUpdatePacket.AgentData.AgentID, avRequestDataUpdatePacket.AgentData.SessionID);
        }

        private void HandleUserInfoRequest(Packet Pack)
        {
            if (OnUserInfoRequest != null)
            {
                OnUserInfoRequest(this);
            }
            else
            {
                SendUserInfoReply(false, true, "");
            }
        }

        private void HandleUpdateUserInfo(Packet Pack)
        {
            if(OnUpdateUserInfo is null)
                return;

            UpdateUserInfoPacket updateUserInfo = (UpdateUserInfoPacket)Pack;
            if (updateUserInfo.AgentData.SessionID.NotEqual(m_sessionId) || updateUserInfo.AgentData.AgentID.NotEqual(m_agentId))
                return;

            bool visible = true;
            string DirectoryVisibility = Utils.BytesToString(updateUserInfo.UserData.DirectoryVisibility);
            if (DirectoryVisibility == "hidden")
                visible = false;

            OnUpdateUserInfo?.Invoke( updateUserInfo.UserData.IMViaEMail, visible, this);
        }

        private void HandleSetStartLocationRequest(Packet Pack)
        {
            SetStartLocationRequestPacket avSetStartLocationRequestPacket = (SetStartLocationRequestPacket)Pack;

            if (avSetStartLocationRequestPacket.AgentData.SessionID.NotEqual(m_sessionId) ||
                    avSetStartLocationRequestPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            float packX = avSetStartLocationRequestPacket.StartLocationData.LocationPos.X;
            float packY = avSetStartLocationRequestPacket.StartLocationData.LocationPos.Y;
            // Linden Client limitation..
            if (packX == 255.5f || packY == 255.5f)
            {
                if (((Scene)m_scene).TryGetScenePresence(m_agentId, out ScenePresence avatar))
                {
                    if (packX == 255.5f)
                    {
                        avSetStartLocationRequestPacket.StartLocationData.LocationPos.X = avatar.AbsolutePosition.X;
                    }
                    if (packY == 255.5f)
                    {
                        avSetStartLocationRequestPacket.StartLocationData.LocationPos.Y = avatar.AbsolutePosition.Y;
                    }
                }

            }
            OnSetStartLocationRequest?.Invoke(this, 0, avSetStartLocationRequestPacket.StartLocationData.LocationPos,
                                                avSetStartLocationRequestPacket.StartLocationData.LocationLookAt,
                                                avSetStartLocationRequestPacket.StartLocationData.LocationID);
        }

        private void HandleAgentThrottle(Packet Pack)
        {
            AgentThrottlePacket atpack = (AgentThrottlePacket)Pack;

            if (atpack.AgentData.SessionID.NotEqual(m_sessionId) || atpack.AgentData.AgentID.NotEqual(m_agentId))
                return;

            m_udpClient.SetThrottles(atpack.Throttle.Throttles);
            OnUpdateThrottles?.Invoke();
        }

        private void HandleAgentPause(Packet Pack)
        {
            m_udpClient.IsPaused = true;
        }

        private void HandleAgentResume(Packet Pack)
        {
            m_udpClient.IsPaused = false;
            m_udpServer.SendPing(m_udpClient);
        }

        private void HandleForceScriptControlRelease(Packet Pack)
        {
            OnForceReleaseControls?.Invoke(this, m_agentId);
        }

        #endregion Scene/Avatar

        #region Objects/m_sceneObjects

        private void HandleObjectLink(Packet Pack)
        {
            ObjectLinkPacket link = (ObjectLinkPacket)Pack;

            if (link.AgentData.SessionID.NotEqual(m_sessionId) || link.AgentData.AgentID.NotEqual(m_agentId))
                return;

            uint parentprimid = 0;
            List<uint> childrenprims = new();
            if (link.ObjectData.Length > 1)
            {
                parentprimid = link.ObjectData[0].ObjectLocalID;

                for (int i = 1; i < link.ObjectData.Length; i++)
                {
                    childrenprims.Add(link.ObjectData[i].ObjectLocalID);
                }
            }
            OnLinkObjects?.Invoke(this, parentprimid, childrenprims);
        }

        private void HandleObjectDelink(Packet Pack)
        {
            ObjectDelinkPacket delink = (ObjectDelinkPacket)Pack;

            if (delink.AgentData.SessionID.NotEqual(m_sessionId) || delink.AgentData.AgentID.NotEqual(m_agentId))
                return;

            // It appears the prim at index 0 is not always the root prim (for
            // instance, when one prim of a link set has been edited independently
            // of the others).  Therefore, we'll pass all the ids onto the delink
            // method for it to decide which is the root.
            List<uint> prims = new();
            for (int i = 0; i < delink.ObjectData.Length; i++)
            {
                prims.Add(delink.ObjectData[i].ObjectLocalID);
            }

            OnDelinkObjects?.Invoke(prims, this);
        }

        private void HandleObjectAdd(Packet Pack)
        {
                ObjectAddPacket addPacket = (ObjectAddPacket)Pack;

                if (addPacket.AgentData.SessionID.NotEqual(m_sessionId) || addPacket.AgentData.AgentID.NotEqual(m_agentId))
                    return;

                ObjectAddPacket.ObjectDataBlock datablk = addPacket.ObjectData;
                PrimitiveBaseShape shape = GetShapeFromAddPacket(addPacket);
                OnAddPrim?.Invoke(m_agentId, addPacket.AgentData.GroupID, datablk.RayEnd,
                    datablk.Rotation, shape,
                    datablk.BypassRaycast, datablk.RayStart, datablk.RayTargetID, datablk.RayEndIsIntersection,
                    datablk.AddFlags);
        }

        private void HandleObjectShape(Packet Pack)
        {
            if(OnUpdatePrimShape is null)
                return;

            ObjectShapePacket shapePacket = (ObjectShapePacket)Pack;
            if (shapePacket.AgentData.SessionID.NotEqual(m_sessionId) || shapePacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < shapePacket.ObjectData.Length; i++)
            {
                uint id = shapePacket.ObjectData[i].ObjectLocalID;
                UpdateShapeArgs shapeData = new()
                {
                    ObjectLocalID = id,
                    PathBegin = shapePacket.ObjectData[i].PathBegin,
                    PathCurve = shapePacket.ObjectData[i].PathCurve,
                    PathEnd = shapePacket.ObjectData[i].PathEnd,
                    PathRadiusOffset = shapePacket.ObjectData[i].PathRadiusOffset,
                    PathRevolutions = shapePacket.ObjectData[i].PathRevolutions,
                    PathScaleX = shapePacket.ObjectData[i].PathScaleX,
                    PathScaleY = shapePacket.ObjectData[i].PathScaleY,
                    PathShearX = shapePacket.ObjectData[i].PathShearX,
                    PathShearY = shapePacket.ObjectData[i].PathShearY,
                    PathSkew = shapePacket.ObjectData[i].PathSkew,
                    PathTaperX = shapePacket.ObjectData[i].PathTaperX,
                    PathTaperY = shapePacket.ObjectData[i].PathTaperY,
                    PathTwist = shapePacket.ObjectData[i].PathTwist,
                    PathTwistBegin = shapePacket.ObjectData[i].PathTwistBegin,
                    ProfileBegin = shapePacket.ObjectData[i].ProfileBegin,
                    ProfileCurve = shapePacket.ObjectData[i].ProfileCurve,
                    ProfileEnd = shapePacket.ObjectData[i].ProfileEnd,
                    ProfileHollow = shapePacket.ObjectData[i].ProfileHollow
                };

                OnUpdatePrimShape?.Invoke(m_agentId, id, shapeData);
            }
        }

        private void HandleObjectExtraParams(Packet Pack)
        {
            ObjectExtraParamsPacket extraPar = (ObjectExtraParamsPacket)Pack;
            if (extraPar.AgentData.SessionID.NotEqual(m_sessionId) || extraPar.AgentData.AgentID.NotEqual(m_agentId))
                return;

            ObjectExtraParams handlerUpdateExtraParams = OnUpdateExtraParams;
            if (handlerUpdateExtraParams != null)
            {
                for (int i = 0; i < extraPar.ObjectData.Length; i++)
                {
                    OnUpdateExtraParams?.Invoke(m_agentId, extraPar.ObjectData[i].ObjectLocalID,
                                             extraPar.ObjectData[i].ParamType,
                                             extraPar.ObjectData[i].ParamInUse, extraPar.ObjectData[i].ParamData);
                }
            }
        }

        private void HandleObjectDuplicate(Packet Pack)
        {
            if(OnObjectDuplicate is null)
                return;

            ObjectDuplicatePacket dupe = (ObjectDuplicatePacket)Pack;
            if (dupe.AgentData.SessionID.NotEqual(m_sessionId) || dupe.AgentData.AgentID.NotEqual(m_agentId))
                return;

//            ObjectDuplicatePacket.AgentDataBlock AgentandGroupData = dupe.AgentData;

            for (int i = 0; i < dupe.ObjectData.Length; i++)
            {
                UUID rezGroupID = dupe.AgentData.GroupID;
                if(!IsGroupMember(rezGroupID))
                    rezGroupID = UUID.Zero;
                OnObjectDuplicate?.Invoke(dupe.ObjectData[i].ObjectLocalID, dupe.SharedData.Offset,
                                        dupe.SharedData.DuplicateFlags, m_agentId,
                                        rezGroupID);
            }
        }

        private void HandleRequestMultipleObjects(Packet Pack)
        {
            if (OnObjectRequest is null)
                return;

            RequestMultipleObjectsPacket incomingRequest = (RequestMultipleObjectsPacket)Pack;
            if (incomingRequest.AgentData.SessionID.NotEqual(m_sessionId) || incomingRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < incomingRequest.ObjectData.Length; i++)
                OnObjectRequest?.Invoke(incomingRequest.ObjectData[i].ID, this);
        }

        private void HandleObjectSelect(Packet Pack)
        {
            if(OnObjectSelect is null)
                return;

            ObjectSelectPacket incomingselect = (ObjectSelectPacket)Pack;
            if (incomingselect.AgentData.SessionID.NotEqual(m_sessionId) || incomingselect.AgentData.AgentID.NotEqual(m_agentId))
                return;

            List<uint> thisSelection = new();
            for (int i = 0; i < incomingselect.ObjectData.Length; i++)
                thisSelection.Add(incomingselect.ObjectData[i].ObjectLocalID);

            OnObjectSelect?.Invoke(thisSelection, this);
        }

        private void HandleObjectDeselect(Packet Pack)
        {
            if(OnObjectDeselect is null)
                return;

            ObjectDeselectPacket incomingdeselect = (ObjectDeselectPacket)Pack;
            if (incomingdeselect.AgentData.SessionID.NotEqual(m_sessionId) || incomingdeselect.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < incomingdeselect.ObjectData.Length; i++)
            {
                OnObjectDeselect?.Invoke(incomingdeselect.ObjectData[i].ObjectLocalID, this);
            }
        }

        private void HandleObjectPosition(Packet Pack)
        {
            if (OnUpdatePrimGroupPosition is null)
                return;

            // DEPRECATED: but till libsecondlife removes it, people will use it
            ObjectPositionPacket position = (ObjectPositionPacket)Pack;
            if (position.AgentData.SessionID.NotEqual(m_sessionId) || position.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < position.ObjectData.Length; i++)
                 OnUpdatePrimGroupPosition?.Invoke(position.ObjectData[i].ObjectLocalID, position.ObjectData[i].Position, this);
        }

        private void HandleObjectScale(Packet Pack)
        {
            if (OnUpdatePrimGroupScale is null)
                return;

            // DEPRECATED: but till libsecondlife removes it, people will use it
            ObjectScalePacket scale = (ObjectScalePacket)Pack;
            if (scale.AgentData.SessionID.NotEqual(m_sessionId) || scale.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < scale.ObjectData.Length; i++)
                 OnUpdatePrimGroupScale?.Invoke(scale.ObjectData[i].ObjectLocalID, scale.ObjectData[i].Scale, this);
        }

        private void HandleObjectRotation(Packet Pack)
        {
            if (OnUpdatePrimGroupRotation is null)
                return;

            // DEPRECATED: but till libsecondlife removes it, people will use it
            ObjectRotationPacket rotation = (ObjectRotationPacket)Pack;

            if (rotation.AgentData.SessionID.NotEqual(m_sessionId) || rotation.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < rotation.ObjectData.Length; i++)
                OnUpdatePrimGroupRotation?.Invoke(rotation.ObjectData[i].ObjectLocalID, rotation.ObjectData[i].Rotation, this);
        }

        private void HandleObjectFlagUpdate(Packet Pack)
        {
            if(OnUpdatePrimFlags is null)
                return;

            ObjectFlagUpdatePacket flags = (ObjectFlagUpdatePacket)Pack;
            if (flags.AgentData.SessionID.NotEqual(m_sessionId) || flags.AgentData.AgentID.NotEqual(m_agentId))
                return;

//                byte[] data = Pack.ToBytes();
            // 46,47,48 are special positions within the packet
            // This may change so perhaps we need a better way
            // of storing this (OMV.FlagUpdatePacket.UsePhysics,etc?)
            /*
                            bool UsePhysics = (data[46] != 0) ? true : false;
                            bool IsTemporary = (data[47] != 0) ? true : false;
                            bool IsPhantom = (data[48] != 0) ? true : false;
                            handlerUpdatePrimFlags(flags.AgentData.ObjectLocalID, UsePhysics, IsTemporary, IsPhantom, this);
                */
            bool UsePhysics = flags.AgentData.UsePhysics;
            bool IsPhantom = flags.AgentData.IsPhantom;
            bool IsTemporary = flags.AgentData.IsTemporary;
            ObjectFlagUpdatePacket.ExtraPhysicsBlock[] blocks = flags.ExtraPhysics;
            ExtraPhysicsData physdata = new();

            if (blocks is null || blocks.Length == 0)
            {
                physdata.PhysShapeType = PhysShapeType.invalid;
            }
            else
            {
                ObjectFlagUpdatePacket.ExtraPhysicsBlock phsblock = blocks[0];
                physdata.PhysShapeType = (PhysShapeType)phsblock.PhysicsShapeType;
                physdata.Bounce = phsblock.Restitution;
                physdata.Density = phsblock.Density;
                physdata.Friction = phsblock.Friction;
                physdata.GravitationModifier = phsblock.GravityMultiplier;
            }

            OnUpdatePrimFlags?.Invoke(flags.AgentData.ObjectLocalID, UsePhysics, IsTemporary, IsPhantom, physdata, this);
        }

        Dictionary<uint, uint> objImageSeqs = null;
        double lastobjImageSeqsMS = 0.0;

        private void HandleObjectImage(Packet Pack)
        {
            if (OnUpdatePrimTexture is null)
                return;

            ObjectImagePacket imagePack = (ObjectImagePacket)Pack;
            if (imagePack.AgentData.SessionID.NotEqual(m_sessionId) || imagePack.AgentData.AgentID.NotEqual(m_agentId))
                return;

            double now = Util.GetTimeStampMS();
            if(objImageSeqs is null || ( now - lastobjImageSeqsMS > 30000.0))
            {
                objImageSeqs = null; // yeah i know superstition...
                objImageSeqs = new Dictionary<uint, uint>(16);
            }

            lastobjImageSeqsMS = now;
            uint seq = Pack.Header.Sequence;
            uint id;

            ObjectImagePacket.ObjectDataBlock o;
            for (int i = 0; i < imagePack.ObjectData.Length; i++)
            {
                o = imagePack.ObjectData[i];
                id = o.ObjectLocalID;
                if(objImageSeqs.TryGetValue(id, out uint lastseq))
                {
                    if(seq <= lastseq)
                        continue;
                }
                objImageSeqs[id] = seq;
                OnUpdatePrimTexture?.Invoke(id, o.TextureEntry, this);
            }
        }

        private void HandleObjectGrab(Packet Pack)
        {
            if(OnGrabObject is null)
                return;

            ObjectGrabPacket grab = (ObjectGrabPacket)Pack;
            if (grab.AgentData.SessionID.NotEqual(m_sessionId) || grab.AgentData.AgentID.NotEqual(m_agentId))
                return;

            List<SurfaceTouchEventArgs> touchArgs = new();
            if ((grab.SurfaceInfo != null) && (grab.SurfaceInfo.Length > 0))
            {
                foreach (ObjectGrabPacket.SurfaceInfoBlock surfaceInfo in grab.SurfaceInfo)
                {
                    SurfaceTouchEventArgs arg = new()
                    {
                        Binormal = surfaceInfo.Binormal,
                        FaceIndex = surfaceInfo.FaceIndex,
                        Normal = surfaceInfo.Normal,
                        Position = surfaceInfo.Position,
                        STCoord = surfaceInfo.STCoord,
                        UVCoord = surfaceInfo.UVCoord
                    };
                    touchArgs.Add(arg);
                }
            }
            OnGrabObject?.Invoke(grab.ObjectData.LocalID, grab.ObjectData.GrabOffset, this, touchArgs);
        }

        private void HandleObjectGrabUpdate(Packet Pack)
        {
            if (OnGrabUpdate is null)
                return;

            ObjectGrabUpdatePacket grabUpdate = (ObjectGrabUpdatePacket)Pack;
            if (grabUpdate.AgentData.SessionID.NotEqual(m_sessionId) || grabUpdate.AgentData.AgentID.NotEqual(m_agentId))
                return;

            List<SurfaceTouchEventArgs> touchArgs = new();
            if ((grabUpdate.SurfaceInfo != null) && (grabUpdate.SurfaceInfo.Length > 0))
            {
                foreach (ObjectGrabUpdatePacket.SurfaceInfoBlock surfaceInfo in grabUpdate.SurfaceInfo)
                {
                    SurfaceTouchEventArgs arg = new()
                    {
                        Binormal = surfaceInfo.Binormal,
                        FaceIndex = surfaceInfo.FaceIndex,
                        Normal = surfaceInfo.Normal,
                        Position = surfaceInfo.Position,
                        STCoord = surfaceInfo.STCoord,
                        UVCoord = surfaceInfo.UVCoord
                    };
                    touchArgs.Add(arg);
                }
            }

            OnGrabUpdate?.Invoke(grabUpdate.ObjectData.ObjectID, grabUpdate.ObjectData.GrabOffsetInitial,
                                  grabUpdate.ObjectData.GrabPosition, this, touchArgs);
        }

        private void HandleObjectDeGrab(Packet Pack)
        {
            if (OnDeGrabObject is null)
                return;

            ObjectDeGrabPacket deGrab = (ObjectDeGrabPacket)Pack;

            if (deGrab.AgentData.SessionID.NotEqual(m_sessionId) || deGrab.AgentData.AgentID.NotEqual(m_agentId))
                return;

            List<SurfaceTouchEventArgs> touchArgs = new();
            if ((deGrab.SurfaceInfo != null) && (deGrab.SurfaceInfo.Length > 0))
            {
                foreach (ObjectDeGrabPacket.SurfaceInfoBlock surfaceInfo in deGrab.SurfaceInfo)
                {
                    SurfaceTouchEventArgs arg = new()
                    {
                        Binormal = surfaceInfo.Binormal,
                        FaceIndex = surfaceInfo.FaceIndex,
                        Normal = surfaceInfo.Normal,
                        Position = surfaceInfo.Position,
                        STCoord = surfaceInfo.STCoord,
                        UVCoord = surfaceInfo.UVCoord
                    };
                    touchArgs.Add(arg);
                }
            }
            OnDeGrabObject?.Invoke(deGrab.ObjectData.LocalID, this, touchArgs);
        }

        private void HandleObjectSpinStart(Packet Pack)
        {
            //m_log.Warn("[CLIENT]: unhandled ObjectSpinStart packet");
            ObjectSpinStartPacket spinStart = (ObjectSpinStartPacket)Pack;
            if (spinStart.AgentData.SessionID.NotEqual(m_sessionId) || spinStart.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnSpinStart?.Invoke(spinStart.ObjectData.ObjectID, this);
        }

        private void HandleObjectSpinUpdate(Packet Pack)
        {
            //m_log.Warn("[CLIENT]: unhandled ObjectSpinUpdate packet");
            ObjectSpinUpdatePacket spinUpdate = (ObjectSpinUpdatePacket)Pack;
            if (spinUpdate.AgentData.SessionID.NotEqual(m_sessionId) || spinUpdate.AgentData.AgentID.NotEqual(m_agentId))
                return;

            spinUpdate.ObjectData.Rotation.GetAxisAngle(out Vector3 axis, out float angle);
            //m_log.Warn("[CLIENT]: ObjectSpinUpdate packet rot axis:" + axis + " angle:" + angle);

            OnSpinUpdate?.Invoke(spinUpdate.ObjectData.ObjectID, spinUpdate.ObjectData.Rotation, this);
        }

        private void HandleObjectSpinStop(Packet Pack)
        {
            //m_log.Warn("[CLIENT]: unhandled ObjectSpinStop packet");
            ObjectSpinStopPacket spinStop = (ObjectSpinStopPacket)Pack;
            if (spinStop.AgentData.SessionID.NotEqual(m_sessionId) || spinStop.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnSpinStop?.Invoke(spinStop.ObjectData.ObjectID, this);
        }

        private void HandleObjectDescription(Packet Pack)
        {
            if(OnObjectDescription is null)
                return;

            ObjectDescriptionPacket objDes = (ObjectDescriptionPacket)Pack;
            if (objDes.AgentData.SessionID.NotEqual(m_sessionId) || objDes.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < objDes.ObjectData.Length; i++)
                 OnObjectDescription?.Invoke(this, objDes.ObjectData[i].LocalID,
                                             Util.FieldToString(objDes.ObjectData[i].Description));
        }

        private void HandleObjectName(Packet Pack)
        {
            if(OnObjectName is null)
                return;

            ObjectNamePacket objName = (ObjectNamePacket)Pack;
            if (objName.AgentData.SessionID.NotEqual(m_sessionId) || objName.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < objName.ObjectData.Length; i++)
                OnObjectName?.Invoke(this, objName.ObjectData[i].LocalID,
                                      Util.FieldToString(objName.ObjectData[i].Name));
        }

        private void HandleObjectPermissions(Packet Pack)
        {
            if (OnObjectPermissions is null)
                return;

            ObjectPermissionsPacket newobjPerms = (ObjectPermissionsPacket)Pack;
            UUID SessionID = newobjPerms.AgentData.SessionID;
            if (SessionID.NotEqual(m_sessionId))
                return;
            UUID AgentID = newobjPerms.AgentData.AgentID;
            if(AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < newobjPerms.ObjectData.Length; i++)
            {
                ObjectPermissionsPacket.ObjectDataBlock permChanges = newobjPerms.ObjectData[i];

                byte field = permChanges.Field;
                uint localID = permChanges.ObjectLocalID;
                uint mask = permChanges.Mask;
                byte set = permChanges.Set;

                OnObjectPermissions?.Invoke(this, AgentID, SessionID, field, localID, mask, set);
            }

            // Here's our data,
            // PermField contains the field the info goes into
            // PermField determines which mask we're changing
            //
            // chmask is the mask of the change
            // setTF is whether we're adding it or taking it away
            //
            // objLocalID is the localID of the object.

            // Unfortunately, we have to pass the event the packet because objData is an array
            // That means multiple object perms may be updated in a single packet.
        }

        private void HandleUndo(Packet Pack)
        {
            if(OnUndo is null)
                return;

            UndoPacket undoitem = (UndoPacket)Pack;
            if (undoitem.AgentData.SessionID.NotEqual(m_sessionId) || undoitem.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < undoitem.ObjectData.Length; i++)
                    OnUndo?.Invoke(this, undoitem.ObjectData[i].ObjectID);
        }

        private void HandleLandUndo(Packet Pack)
        {
            UndoLandPacket undolanditem = (UndoLandPacket)Pack;
            if (undolanditem.AgentData.SessionID.NotEqual(m_sessionId) || undolanditem.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnLandUndo?.Invoke(this);
        }

        private void HandleRedo(Packet Pack)
        {
            if(OnRedo is null)
                return;

            RedoPacket redoitem = (RedoPacket)Pack;
            if (redoitem.AgentData.SessionID.NotEqual(m_sessionId) || redoitem.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < redoitem.ObjectData.Length; i++)
                OnRedo?.Invoke(this, redoitem.ObjectData[i].ObjectID);
        }

        private void HandleObjectDuplicateOnRay(Packet Pack)
        {
            if(OnObjectDuplicateOnRay is null)
                return;

            ObjectDuplicateOnRayPacket dupeOnRay = (ObjectDuplicateOnRayPacket)Pack;
            if (dupeOnRay.AgentData.SessionID.NotEqual(m_sessionId) || dupeOnRay.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < dupeOnRay.ObjectData.Length; i++)
            {
                    UUID rezGroupID = dupeOnRay.AgentData.GroupID;
                    if(!IsGroupMember(rezGroupID))
                        rezGroupID = UUID.Zero;

                    OnObjectDuplicateOnRay?.Invoke(dupeOnRay.ObjectData[i].ObjectLocalID,
                                    dupeOnRay.AgentData.DuplicateFlags, m_agentId, rezGroupID,
                                    dupeOnRay.AgentData.RayTargetID, dupeOnRay.AgentData.RayEnd,
                                    dupeOnRay.AgentData.RayStart, dupeOnRay.AgentData.BypassRaycast,
                                    dupeOnRay.AgentData.RayEndIsIntersection,
                                    dupeOnRay.AgentData.CopyCenters, dupeOnRay.AgentData.CopyRotates);
            }
        }

        private void HandleRequestObjectPropertiesFamily(Packet Pack)
        {
            //This powers the little tooltip that appears when you move your mouse over an object
            RequestObjectPropertiesFamilyPacket packToolTip = (RequestObjectPropertiesFamilyPacket)Pack;
            if (packToolTip.AgentData.SessionID.NotEqual(m_sessionId) || packToolTip.AgentData.AgentID.NotEqual(m_agentId))
                return;

            RequestObjectPropertiesFamilyPacket.ObjectDataBlock packObjBlock = packToolTip.ObjectData;

            OnRequestObjectPropertiesFamily?.Invoke(this, m_agentId, packObjBlock.RequestFlags,
                                                     packObjBlock.ObjectID);
        }

        private void HandleObjectIncludeInSearch(Packet Pack)
        {
            if(OnObjectIncludeInSearch is null)
                return;

            //This lets us set objects to appear in search (stuff like DataSnapshot, etc)
            ObjectIncludeInSearchPacket packInSearch = (ObjectIncludeInSearchPacket)Pack;
            if (packInSearch.AgentData.SessionID.NotEqual(m_sessionId) || packInSearch.AgentData.AgentID.NotEqual(m_agentId))
                return;

            foreach (ObjectIncludeInSearchPacket.ObjectDataBlock objData in packInSearch.ObjectData)
            {
                bool inSearch = objData.IncludeInSearch;
                uint localID = objData.ObjectLocalID;
                OnObjectIncludeInSearch?.Invoke(this, inSearch, localID);
            }
        }

        private void HandleScriptAnswerYes(Packet Pack)
        {
            ScriptAnswerYesPacket scriptAnswer = (ScriptAnswerYesPacket)Pack;
            if (scriptAnswer.AgentData.SessionID.NotEqual(m_sessionId) || scriptAnswer.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnScriptAnswer?.Invoke(this, scriptAnswer.Data.TaskID, scriptAnswer.Data.ItemID, scriptAnswer.Data.Questions);
        }

        private void HandleObjectClickAction(Packet Pack)
        {
            if(OnObjectClickAction is null)
                return;

            ObjectClickActionPacket ocpacket = (ObjectClickActionPacket)Pack;
            if (ocpacket.AgentData.SessionID.NotEqual(m_sessionId) || ocpacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            foreach (ObjectClickActionPacket.ObjectDataBlock odata in ocpacket.ObjectData)
            {
                byte action = odata.ClickAction;
                uint localID = odata.ObjectLocalID;
                OnObjectClickAction?.Invoke(this, localID, action.ToString());
            }
        }

        private void HandleObjectMaterial(Packet Pack)
        {
            if(OnObjectMaterial is null)
                return;

            ObjectMaterialPacket ompacket = (ObjectMaterialPacket)Pack;
            if (ompacket.AgentData.SessionID.NotEqual(m_sessionId) || ompacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            foreach (ObjectMaterialPacket.ObjectDataBlock odata in ompacket.ObjectData)
            {
                byte material = odata.Material;
                uint localID = odata.ObjectLocalID;
                OnObjectMaterial?.Invoke(this, localID, material.ToString());
            }
        }

        #endregion Objects/m_sceneObjects

        #region Inventory/Asset/Other related packets

        private void HandleRequestImage(Packet Pack)
        {
            RequestImagePacket imageRequest = (RequestImagePacket)Pack;
            if (imageRequest.AgentData.SessionID.NotEqual(m_sessionId) || imageRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            //handlerTextureRequest = null;
            for (int i = 0; i < imageRequest.RequestImage.Length; i++)
            {
                TextureRequestArgs args = new();

                RequestImagePacket.RequestImageBlock block = imageRequest.RequestImage[i];

                args.RequestedAssetID = block.Image;
                args.DiscardLevel = block.DiscardLevel;
                args.PacketNumber = block.Packet;
                args.Priority = block.DownloadPriority;
                args.requestSequence = imageRequest.Header.Sequence;

                // NOTE: This is not a built in part of the LLUDP protocol, but we double the
                // priority of avatar textures to get avatars rezzing in faster than the
                // surrounding scene
                if ((ImageType)block.Type == ImageType.Baked)
                    args.Priority *= 2.0f;

                ImageManager.EnqueueReq(args);
            }
        }

        /// <summary>
        /// This is the entry point for the UDP route by which the client can retrieve asset data.  If the request
        /// is successful then a TransferInfo packet will be sent back, followed by one or more TransferPackets
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="Pack"></param>
        /// <returns>This parameter may be ignored since we appear to return true whatever happens</returns>
        private void HandleTransferRequest(Packet Pack)
        {
            //m_log.Debug("ClientView.ProcessPackets.cs:ProcessInPacket() - Got transfer request");

            TransferRequestPacket transfer = (TransferRequestPacket)Pack;
            UUID taskID = UUID.Zero;
            if (transfer.TransferInfo.SourceType == (int)SourceType.SimInventoryItem)
            {
                if (!(((Scene)m_scene).Permissions.BypassPermissions()))
                {
                    // We're spawning a thread because the permissions check can block this thread
                    Util.FireAndForget(delegate
                    {
                        // This requests the asset if needed
                        HandleSimInventoryTransferRequestWithPermsCheck(this, transfer);
                    }, null, "LLClientView.HandleTransferRequest");

                    return;
                }
            }
            else if (transfer.TransferInfo.SourceType == (int)SourceType.SimEstate)
            {
                //TransferRequestPacket does not include covenant uuid?
                //get scene covenant uuid
                taskID = m_scene.RegionInfo.RegionSettings.Covenant;
            }

            // This is non-blocking
            MakeAssetRequest(transfer, taskID);
        }

        private void HandleSimInventoryTransferRequestWithPermsCheck(IClientAPI sender, TransferRequestPacket transfer)
        {
            UUID taskID = new(transfer.TransferInfo.Params, 48);
            UUID itemID = new(transfer.TransferInfo.Params, 64);
            UUID requestID = new(transfer.TransferInfo.Params, 80);

            //m_log.DebugFormat(
            //    "[CLIENT]: Got request for asset {0} from item {1} in prim {2} by {3}",
            //    requestID, itemID, taskID, Name);

            //m_log.Debug("Transfer Request: " + transfer.ToString());
            // Validate inventory transfers
            // Has to be done here, because AssetCache can't do it
            //
            if (!taskID.IsZero()) // Prim
            {
                SceneObjectPart part = m_scene.GetSceneObjectPart(taskID);

                if (part is null)
                {
                    m_log.WarnFormat(
                        "[CLIENT]: {0} requested asset {1} from item {2} in prim {3} but prim does not exist",
                        Name, requestID, itemID, taskID);
                    return;
                }

                TaskInventoryItem tii = part.Inventory.GetInventoryItem(itemID);
                if (tii is null)
                {
                    m_log.WarnFormat(
                        "[CLIENT]: {0} requested asset {1} from item {2} in prim {3} but item does not exist",
                        Name, requestID, itemID, taskID);
                    return;
                }

                if (tii.Type == (int)AssetType.LSLText)
                {
                    if (!((Scene)m_scene).Permissions.CanEditScript(itemID, taskID, m_agentId))
                        return;
                }
                else if (tii.Type == (int)AssetType.Notecard)
                {
                    if (!((Scene)m_scene).Permissions.CanEditNotecard(itemID, taskID, m_agentId))
                        return;
                }
                else
                {
                    // TODO: Change this code to allow items other than notecards and scripts to be successfully
                    // shared with group.  In fact, this whole block of permissions checking should move to an IPermissionsModule
                    if (part.OwnerID != m_agentId)
                    {
                        m_log.WarnFormat(
                            "[CLIENT]: {0} requested asset {1} from item {2} in prim {3} but the prim is owned by {4}",
                            Name, requestID, itemID, taskID, part.OwnerID);
                        return;
                    }

                    if ((part.OwnerMask & (uint)PermissionMask.Modify) == 0)
                    {
                        m_log.WarnFormat(
                            "[CLIENT]: {0} requested asset {1} from item {2} in prim {3} but modify permissions are not set",
                            Name, requestID, itemID, taskID);
                        return;
                    }

                    if (tii.OwnerID != m_agentId)
                    {
                        m_log.WarnFormat(
                            "[CLIENT]: {0} requested asset {1} from item {2} in prim {3} but the item is owned by {4}",
                            Name, requestID, itemID, taskID, tii.OwnerID);
                        return;
                    }

                    if ((
                        tii.CurrentPermissions & ((uint)PermissionMask.Modify | (uint)PermissionMask.Copy | (uint)PermissionMask.Transfer))
                            != ((uint)PermissionMask.Modify | (uint)PermissionMask.Copy | (uint)PermissionMask.Transfer))
                    {
                        m_log.WarnFormat(
                            "[CLIENT]: {0} requested asset {1} from item {2} in prim {3} but item permissions are not modify/copy/transfer",
                            Name, requestID, itemID, taskID);
                        return;
                    }

                    if (tii.AssetID != requestID)
                    {
                        m_log.WarnFormat(
                            "[CLIENT]: {0} requested asset {1} from item {2} in prim {3} but this does not match item's asset {4}",
                            Name, requestID, itemID, taskID, tii.AssetID);
                        return;
                    }
                }
            }
            else // Agent
            {
                IInventoryAccessModule invAccess = m_scene.RequestModuleInterface<IInventoryAccessModule>();
                if (invAccess != null)
                {
                    if (!invAccess.CanGetAgentInventoryItem(this, itemID, requestID))
                        return;
                }
                else
                {
                    return;
                }
            }

            // Permissions out of the way, let's request the asset
            MakeAssetRequest(transfer, taskID);

        }


        private void HandleAssetUploadRequest(Packet Pack)
        {
            AssetUploadRequestPacket request = (AssetUploadRequestPacket)Pack;

            // m_log.Debug("upload request " + request.ToString());
            // m_log.Debug("upload request was for assetid: " + request.AssetBlock.TransactionID.Combine(this.SecureSessionId).ToString());
            UUID temp = UUID.Combine(request.AssetBlock.TransactionID, SecureSessionId);

            OnAssetUploadRequest?.Invoke(this, temp,
                                          request.AssetBlock.TransactionID, request.AssetBlock.Type,
                                          request.AssetBlock.AssetData, request.AssetBlock.StoreLocal,
                                          request.AssetBlock.Tempfile);
        }

        private void HandleRequestXfer(Packet Pack)
        {
            RequestXferPacket xferReq = (RequestXferPacket)Pack;
            OnRequestXfer?.Invoke(this, xferReq.XferID.ID, Util.FieldToString(xferReq.XferID.Filename));
        }

        private void HandleSendXferPacket(Packet Pack)
        {
            SendXferPacketPacket xferRec = (SendXferPacketPacket)Pack;
            OnXferReceive?.Invoke(this, xferRec.XferID.ID, xferRec.XferID.Packet, xferRec.DataPacket.Data);
        }

        private void HandleConfirmXferPacket(Packet Pack)
        {
            ConfirmXferPacketPacket confirmXfer = (ConfirmXferPacketPacket)Pack;
            OnConfirmXfer?.Invoke(this, confirmXfer.XferID.ID, confirmXfer.XferID.Packet);
        }

        private void HandleAbortXfer(Packet Pack)
        {
            AbortXferPacket abortXfer = (AbortXferPacket)Pack;
            OnAbortXfer?.Invoke(this, abortXfer.XferID.ID);
        }

        private void HandleCreateInventoryFolder(Packet Pack)
        {
            CreateInventoryFolderPacket invFolder = (CreateInventoryFolderPacket)Pack;
            if (invFolder.AgentData.SessionID.NotEqual(m_sessionId) || invFolder.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnCreateNewInventoryFolder?.Invoke(this, invFolder.FolderData.FolderID,
                                             (ushort)invFolder.FolderData.Type,
                                             Util.FieldToString(invFolder.FolderData.Name),
                                             invFolder.FolderData.ParentID);
        }

        private void HandleUpdateInventoryFolder(Packet Pack)
        {
            if (OnUpdateInventoryFolder is null)
                return;

            UpdateInventoryFolderPacket invFolderx = (UpdateInventoryFolderPacket)Pack;
            if (invFolderx.AgentData.SessionID.NotEqual(m_sessionId) || invFolderx.AgentData.AgentID.NotEqual(m_agentId))
                return;
            for (int i = 0; i < invFolderx.FolderData.Length; i++)
            {
                OnUpdateInventoryFolder?.Invoke(this, invFolderx.FolderData[i].FolderID,
                                            (ushort)invFolderx.FolderData[i].Type,
                                            Util.FieldToString(invFolderx.FolderData[i].Name),
                                            invFolderx.FolderData[i].ParentID);
            }
        }

        private void HandleMoveInventoryFolder(Packet Pack)
        {
            if (OnMoveInventoryFolder is null)
                return;

            MoveInventoryFolderPacket invFoldery = (MoveInventoryFolderPacket)Pack;
            if (invFoldery.AgentData.SessionID.NotEqual(m_sessionId) || invFoldery.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < invFoldery.InventoryData.Length; i++)
            {
                OnMoveInventoryFolder?.Invoke(this, invFoldery.InventoryData[i].FolderID,
                                            invFoldery.InventoryData[i].ParentID);
            }
        }

        private void HandleCreateInventoryItem(Packet Pack)
        {
            CreateInventoryItemPacket createItem = (CreateInventoryItemPacket)Pack;
            if (createItem.AgentData.SessionID.NotEqual(m_sessionId) || createItem.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnCreateNewInventoryItem?.Invoke(this, createItem.InventoryBlock.TransactionID,
                                              createItem.InventoryBlock.FolderID,
                                              createItem.InventoryBlock.CallbackID,
                                              Util.FieldToString(createItem.InventoryBlock.Description),
                                              Util.FieldToString(createItem.InventoryBlock.Name),
                                              createItem.InventoryBlock.InvType,
                                              createItem.InventoryBlock.Type,
                                              createItem.InventoryBlock.WearableType,
                                              createItem.InventoryBlock.NextOwnerMask,
                                              Util.UnixTimeSinceEpoch());
        }

        private void HandleLinkInventoryItem(Packet Pack)
        {
            LinkInventoryItemPacket createLink = (LinkInventoryItemPacket)Pack;
            if (createLink.AgentData.SessionID.NotEqual(m_sessionId) || createLink.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnLinkInventoryItem?.Invoke(
                this,
                createLink.InventoryBlock.TransactionID,
                createLink.InventoryBlock.FolderID,
                createLink.InventoryBlock.CallbackID,
                Util.FieldToString(createLink.InventoryBlock.Description),
                Util.FieldToString(createLink.InventoryBlock.Name),
                createLink.InventoryBlock.InvType,
                createLink.InventoryBlock.Type,
                createLink.InventoryBlock.OldItemID);
        }

        private void HandleFetchInventory(Packet Pack)
        {
            if (OnFetchInventory is null)
                return;

            FetchInventoryPacket FetchInventoryx = (FetchInventoryPacket)Pack;
            if (FetchInventoryx.AgentData.SessionID.NotEqual(m_sessionId) || FetchInventoryx.AgentData.AgentID.NotEqual(m_agentId))
                return;

            FetchInventoryPacket.InventoryDataBlock[] data = FetchInventoryx.InventoryData;

            UUID[] items = new UUID[data.Length];
            UUID[]  owners = new UUID[data.Length];

            for (int i = 0; i < data.Length; ++i)
            {
                items[i] =data[i].ItemID;
                owners[i] = data[i].OwnerID;
            }

            OnFetchInventory?.Invoke(this, items, owners);
        }

        private void HandleFetchInventoryDescendents(Packet Pack)
        {
            FetchInventoryDescendentsPacket Fetch = (FetchInventoryDescendentsPacket)Pack;
            if (Fetch.AgentData.SessionID.NotEqual(m_sessionId) || Fetch.AgentData.AgentID.NotEqual(m_agentId))
                return;

            FetchInventoryDescendentsPacket.InventoryDataBlock data = Fetch.InventoryData;
            OnFetchInventoryDescendents?.Invoke(this, data.FolderID, data.OwnerID,
                                                data.FetchFolders, data.FetchItems,
                                                data.SortOrder);
        }

        private void HandlePurgeInventoryDescendents(Packet Pack)
        {
            PurgeInventoryDescendentsPacket Purge = (PurgeInventoryDescendentsPacket)Pack;
            if (Purge.AgentData.SessionID.NotEqual(m_sessionId) || Purge.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnPurgeInventoryDescendents?.Invoke(this, Purge.InventoryData.FolderID);
        }

        private void HandleUpdateInventoryItem(Packet Pack)
        {
            if (OnUpdateInventoryItem is null)
                return;

            UpdateInventoryItemPacket inventoryItemUpdate = (UpdateInventoryItemPacket)Pack;
            if (inventoryItemUpdate.AgentData.SessionID.NotEqual(m_sessionId) || inventoryItemUpdate.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0; i < inventoryItemUpdate.InventoryData.Length; i++)
            {
                InventoryItemBase itemUpd = new()
                {
                    ID = inventoryItemUpdate.InventoryData[i].ItemID,
                    Name = Util.FieldToString(inventoryItemUpdate.InventoryData[i].Name),
                    Description = Util.FieldToString(inventoryItemUpdate.InventoryData[i].Description),
                    GroupID = inventoryItemUpdate.InventoryData[i].GroupID,
                    GroupOwned = inventoryItemUpdate.InventoryData[i].GroupOwned,
                    GroupPermissions = inventoryItemUpdate.InventoryData[i].GroupMask,
                    NextPermissions = inventoryItemUpdate.InventoryData[i].NextOwnerMask,
                    EveryOnePermissions = inventoryItemUpdate.InventoryData[i].EveryoneMask,
                    CreationDate = inventoryItemUpdate.InventoryData[i].CreationDate,
                    Folder = inventoryItemUpdate.InventoryData[i].FolderID,
                    InvType = inventoryItemUpdate.InventoryData[i].InvType,
                    SalePrice = inventoryItemUpdate.InventoryData[i].SalePrice,
                    SaleType = inventoryItemUpdate.InventoryData[i].SaleType,
                    Flags = inventoryItemUpdate.InventoryData[i].Flags
                };

                OnUpdateInventoryItem?.Invoke(this, inventoryItemUpdate.InventoryData[i].TransactionID,
                                        inventoryItemUpdate.InventoryData[i].ItemID,
                                        itemUpd);
            }
        }

        private void HandleCopyInventoryItem(Packet Pack)
        {
            if(OnCopyInventoryItem is null)
                return;

            CopyInventoryItemPacket copyitem = (CopyInventoryItemPacket)Pack;
            if (copyitem.AgentData.SessionID.NotEqual(m_sessionId) || copyitem.AgentData.AgentID.NotEqual(m_agentId))
                return;

            foreach (CopyInventoryItemPacket.InventoryDataBlock datablock in copyitem.InventoryData)
            {
                OnCopyInventoryItem?.Invoke(this, datablock.CallbackID, datablock.OldAgentID,
                                                datablock.OldItemID, datablock.NewFolderID,
                                                Util.FieldToString(datablock.NewName));
            }
        }

        private void HandleMoveInventoryItem(Packet Pack)
        {
            if (OnMoveInventoryItem is null)
                return;

            MoveInventoryItemPacket moveitem = (MoveInventoryItemPacket)Pack;
            if (moveitem.AgentData.SessionID.NotEqual(m_sessionId) || moveitem.AgentData.AgentID.NotEqual(m_agentId))
                return;

            InventoryItemBase itm = null;
            List<InventoryItemBase> items = new();
            foreach (MoveInventoryItemPacket.InventoryDataBlock datablock in moveitem.InventoryData)
            {
                itm = new InventoryItemBase(datablock.ItemID, m_agentId)
                {
                    Folder = datablock.FolderID,
                    Name = Util.FieldToString(datablock.NewName)
                };
                // weird, comes out as empty string
                //m_log.DebugFormat("[XXX] new name: {0}", itm.Name);
                items.Add(itm);
            }
            OnMoveInventoryItem?.Invoke(this, items);
        }

        private void HandleRemoveInventoryItem(Packet Pack)
        {
            if(OnRemoveInventoryItem is null)
                return;

            RemoveInventoryItemPacket removeItem = (RemoveInventoryItemPacket)Pack;
            if (removeItem.AgentData.SessionID.NotEqual(m_sessionId) || removeItem.AgentData.AgentID.NotEqual(m_agentId))
                return;

            List<UUID> uuids = new(removeItem.InventoryData.Length);
            foreach (RemoveInventoryItemPacket.InventoryDataBlock datablock in removeItem.InventoryData)
            {
                uuids.Add(datablock.ItemID);
            }
            OnRemoveInventoryItem?.Invoke(this, uuids);
        }

        private void HandleRemoveInventoryFolder(Packet Pack)
        {
            if (OnRemoveInventoryFolder is null)
                return;

            RemoveInventoryFolderPacket removeFolder = (RemoveInventoryFolderPacket)Pack;
            if (removeFolder.AgentData.SessionID.NotEqual(m_sessionId) || removeFolder.AgentData.AgentID.NotEqual(m_agentId))
                return;

            List<UUID> uuids = new(removeFolder.FolderData.Length);
            foreach (RemoveInventoryFolderPacket.FolderDataBlock datablock in removeFolder.FolderData)
            {
                uuids.Add(datablock.FolderID);
            }
            OnRemoveInventoryFolder?.Invoke(this, uuids);
        }

        private void HandleRemoveInventoryObjects(Packet Pack)
        {
            if (OnRemoveInventoryFolder is null || OnRemoveInventoryItem is null)
                return;

            RemoveInventoryObjectsPacket removeObject = (RemoveInventoryObjectsPacket)Pack;
            if (removeObject.AgentData.SessionID.NotEqual(m_sessionId) || removeObject.AgentData.AgentID.NotEqual(m_agentId))
                return;

            List<UUID> uuids = new(removeObject.FolderData.Length);
            foreach (RemoveInventoryObjectsPacket.FolderDataBlock datablock in removeObject.FolderData)
                uuids.Add(datablock.FolderID);
            OnRemoveInventoryFolder?.Invoke(this, uuids);

            uuids.Clear();
            foreach (RemoveInventoryObjectsPacket.ItemDataBlock datablock in removeObject.ItemData)
                uuids.Add(datablock.ItemID);
            OnRemoveInventoryItem?.Invoke(this, uuids);
        }

        private void HandleRequestTaskInventory(Packet Pack)
        {
            RequestTaskInventoryPacket requesttask = (RequestTaskInventoryPacket)Pack;
            if (requesttask.AgentData.SessionID.NotEqual(m_sessionId) || requesttask.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnRequestTaskInventory?.Invoke(this, requesttask.InventoryData.LocalID);
        }

        private void HandleUpdateTaskInventory(Packet Pack)
        {
            if (OnUpdateTaskInventory is null)
                return;

            UpdateTaskInventoryPacket updatetask = (UpdateTaskInventoryPacket)Pack;
            if (updatetask.UpdateData.Key != 0)
                return; // only do inventory not assets

            if (updatetask.AgentData.SessionID.NotEqual(m_sessionId) || updatetask.AgentData.AgentID.NotEqual(m_agentId))
                return;

            TaskInventoryItem newTaskItem = new()
            {
                ItemID = updatetask.InventoryData.ItemID,
                ParentID = updatetask.InventoryData.FolderID,
                CreatorID = updatetask.InventoryData.CreatorID,
                OwnerID = updatetask.InventoryData.OwnerID,
                GroupID = updatetask.InventoryData.GroupID,
                BasePermissions = updatetask.InventoryData.BaseMask,
                CurrentPermissions = updatetask.InventoryData.OwnerMask,
                GroupPermissions = updatetask.InventoryData.GroupMask,
                EveryonePermissions = updatetask.InventoryData.EveryoneMask,
                NextPermissions = updatetask.InventoryData.NextOwnerMask,

                // Unused?  Clicking share with group sets GroupPermissions instead, so perhaps this is something
                // different
                //newTaskItem.GroupOwned=updatetask.InventoryData.GroupOwned,
                Type = updatetask.InventoryData.Type,
                InvType = updatetask.InventoryData.InvType,
                Flags = updatetask.InventoryData.Flags,
                //newTaskItem.SaleType=updatetask.InventoryData.SaleType,
                //newTaskItem.SalePrice=updatetask.InventoryData.SalePrice,
                Name = Util.FieldToString(updatetask.InventoryData.Name),
                Description = Util.FieldToString(updatetask.InventoryData.Description),
                CreationDate = (uint)updatetask.InventoryData.CreationDate
            };

            OnUpdateTaskInventory?.Invoke(this, updatetask.InventoryData.TransactionID,
                                            newTaskItem, updatetask.UpdateData.LocalID);
        }

        private void HandleRemoveTaskInventory(Packet Pack)
        {
            RemoveTaskInventoryPacket removeTask = (RemoveTaskInventoryPacket)Pack;
            if (removeTask.AgentData.SessionID.NotEqual(m_sessionId) || removeTask.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnRemoveTaskItem?.Invoke(this, removeTask.InventoryData.ItemID, removeTask.InventoryData.LocalID);
        }

        private void HandleMoveTaskInventory(Packet Pack)
        {
            MoveTaskInventoryPacket moveTaskInventoryPacket = (MoveTaskInventoryPacket)Pack;
            if (moveTaskInventoryPacket.AgentData.SessionID.NotEqual(m_sessionId) || moveTaskInventoryPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnMoveTaskItem?.Invoke(
                    this, moveTaskInventoryPacket.AgentData.FolderID,
                    moveTaskInventoryPacket.InventoryData.LocalID,
                    moveTaskInventoryPacket.InventoryData.ItemID);
        }

        private void HandleRezScript(Packet Pack)
        {
            if(OnRezScript is null)
                return;

            //m_log.Debug(Pack.ToString());
            RezScriptPacket rezScriptx = (RezScriptPacket)Pack;
            if (rezScriptx.AgentData.SessionID.NotEqual(m_sessionId) || rezScriptx.AgentData.AgentID.NotEqual(m_agentId))
                return;

            InventoryItemBase item = new()
            {
                ID = rezScriptx.InventoryBlock.ItemID,
                Folder = rezScriptx.InventoryBlock.FolderID,
                CreatorId = rezScriptx.InventoryBlock.CreatorID.ToString(),
                Owner = rezScriptx.InventoryBlock.OwnerID,
                BasePermissions = rezScriptx.InventoryBlock.BaseMask,
                CurrentPermissions = rezScriptx.InventoryBlock.OwnerMask,
                EveryOnePermissions = rezScriptx.InventoryBlock.EveryoneMask,
                NextPermissions = rezScriptx.InventoryBlock.NextOwnerMask,
                GroupPermissions = rezScriptx.InventoryBlock.GroupMask,
                GroupOwned = rezScriptx.InventoryBlock.GroupOwned,
                GroupID = rezScriptx.InventoryBlock.GroupID,
                AssetType = rezScriptx.InventoryBlock.Type,
                InvType = rezScriptx.InventoryBlock.InvType,
                Flags = rezScriptx.InventoryBlock.Flags,
                SaleType = rezScriptx.InventoryBlock.SaleType,
                SalePrice = rezScriptx.InventoryBlock.SalePrice,
                Name = Util.FieldToString(rezScriptx.InventoryBlock.Name),
                Description = Util.FieldToString(rezScriptx.InventoryBlock.Description),
                CreationDate = rezScriptx.InventoryBlock.CreationDate
            };

            OnRezScript?.Invoke(this, item, rezScriptx.InventoryBlock.TransactionID, rezScriptx.UpdateBlock.ObjectLocalID);
        }

        private void HandleMapLayerRequest(Packet Pack)
        {
            RequestMapLayer();
        }

        private void HandleMapBlockRequest(Packet Pack)
        {
            MapBlockRequestPacket MapRequest = (MapBlockRequestPacket)Pack;
            if (MapRequest.AgentData.SessionID.NotEqual(m_sessionId) || MapRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnRequestMapBlocks?.Invoke(this, MapRequest.PositionData.MinX, MapRequest.PositionData.MinY,
                                        MapRequest.PositionData.MaxX, MapRequest.PositionData.MaxY, MapRequest.AgentData.Flags);
        }

        private void HandleMapNameRequest(Packet Pack)
        {
            MapNameRequestPacket map = (MapNameRequestPacket)Pack;
            if (map.AgentData.SessionID.NotEqual(m_sessionId) || map.AgentData.AgentID.NotEqual(m_agentId))
                return;

            string mapName = (map.NameData.Name.Length == 0) ? m_scene.RegionInfo.RegionName :
                Util.UTF8.GetString(map.NameData.Name, 0, map.NameData.Name.Length - 1);

            OnMapNameRequest?.Invoke(this, mapName, map.AgentData.Flags);
        }

        private void HandleTeleportLandmarkRequest(Packet Pack)
        {
            TeleportLandmarkRequestPacket tpReq = (TeleportLandmarkRequestPacket)Pack;
            if (tpReq.Info.SessionID.NotEqual(m_sessionId) || tpReq.Info.AgentID.NotEqual(m_agentId))
                return;

            UUID lmid = tpReq.Info.LandmarkID;
            AssetLandmark lm;
            if (!lmid.IsZero())
            {
                //AssetBase lma = m_assetCache.GetAsset(lmid, false);
                AssetBase lma = m_assetService.Get(lmid.ToString());

                if (lma is null)
                {
                    // Failed to find landmark

                    // Let's try to search in the user's home asset server
                    lma = FindAssetInUserAssetServer(lmid.ToString());

                    if (lma is null)
                    {
                        // Really doesn't exist
                        m_log.WarnFormat("[llClient]: landmark asset {0} not found",lmid.ToString());
                        SendTeleportFailed("Could not find the landmark asset data");
                        return;
                    }
                }

                try
                {
                    lm = new AssetLandmark(lma);
                }
                catch (NullReferenceException)
                {
                    // asset not found generates null ref inside the assetlandmark constructor.
                    SendTeleportFailed("Could not find the landmark asset data");
                    return;
                }
            }
            else
            {
                // Teleport home request
                OnTeleportHomeRequest?.Invoke(m_agentId, this);
                return;
            }

            OnTeleportLandmarkRequest?.Invoke(this, lm);
        }

        private void HandleTeleportCancel(Packet Pack)
        {
            TeleportCancelPacket pkt = (TeleportCancelPacket) Pack;
            if(pkt.Info.AgentID.NotEqual(m_agentId) || pkt.Info.SessionID.NotEqual(m_sessionId))
                return;

            OnTeleportCancel?.Invoke(this);
        }

        private AssetBase FindAssetInUserAssetServer(string id)
        {
            AgentCircuitData aCircuit = ((Scene)Scene).AuthenticateHandler.GetAgentCircuitData(CircuitCode);
            if (aCircuit != null && aCircuit.ServiceURLs != null && aCircuit.ServiceURLs.ContainsKey("AssetServerURI"))
            {
                string assetServer = aCircuit.ServiceURLs["AssetServerURI"].ToString();
                if (!string.IsNullOrEmpty(assetServer))
                    return ((Scene)Scene).AssetService.Get(id, assetServer, false);
            }

            return null;
        }

        private void HandleTeleportLocationRequest(Packet Pack)
        {
            if(OnTeleportLocationRequest is null)
            {
                SendTeleportFailed("Could not process the teleport");
                return;
            }

            TeleportLocationRequestPacket tpLocReq = (TeleportLocationRequestPacket)Pack;
            if (tpLocReq.AgentData.SessionID.NotEqual(m_sessionId) || tpLocReq.AgentData.AgentID.NotEqual(m_agentId))
                return;

            // Adjust teleport location to base of a larger region if requested to teleport to a sub-region
            Util.RegionHandleToWorldLoc(tpLocReq.Info.RegionHandle, out uint locX, out uint locY);
            if ((locX >= m_scene.RegionInfo.WorldLocX)
                        && (locX < (m_scene.RegionInfo.WorldLocX + m_scene.RegionInfo.RegionSizeX))
                        && (locY >= m_scene.RegionInfo.WorldLocY)
                        && (locY < (m_scene.RegionInfo.WorldLocY + m_scene.RegionInfo.RegionSizeY)))
            {
                tpLocReq.Info.RegionHandle = m_scene.RegionInfo.RegionHandle;
                tpLocReq.Info.Position.X += locX - m_scene.RegionInfo.WorldLocX;
                tpLocReq.Info.Position.Y += locY - m_scene.RegionInfo.WorldLocY;
            }

            OnTeleportLocationRequest?.Invoke(this, tpLocReq.Info.RegionHandle, tpLocReq.Info.Position,
                                               tpLocReq.Info.LookAt, 16);
        }

        #endregion Inventory/Asset/Other related packets

        private void HandleUUIDNameRequest(Packet Pack)
        {
            if(OnNameFromUUIDRequest is null)
                return;

            ScenePresence sp = (ScenePresence)SceneAgent;
            if(sp is null || sp.IsDeleted || (sp.IsInTransit && !sp.IsInLocalTransit))
                return;

            UUIDNameRequestPacket incoming = (UUIDNameRequestPacket)Pack;

            foreach (UUIDNameRequestPacket.UUIDNameBlockBlock UUIDBlock in incoming.UUIDNameBlock)
                OnNameFromUUIDRequest?.Invoke(UUIDBlock.ID, this);
        }

        #region Parcel related packets

        private void HandleRegionHandleRequest(Packet Pack)
        {
            RegionHandleRequestPacket rhrPack = (RegionHandleRequestPacket)Pack;
            OnRegionHandleRequest?.Invoke(this, rhrPack.RequestBlock.RegionID);
        }

        private void HandleParcelInfoRequest(Packet Pack)
        {
            if(OnParcelInfoRequest is null)
                return;

            ParcelInfoRequestPacket pirPack = (ParcelInfoRequestPacket)Pack;
            if (pirPack.AgentData.SessionID.NotEqual(m_sessionId) || pirPack.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnParcelInfoRequest?.Invoke(this, pirPack.Data.ParcelID);
        }

        private void HandleParcelAccessListRequest(Packet Pack)
        {
            ParcelAccessListRequestPacket requestPacket = (ParcelAccessListRequestPacket)Pack;
            if (requestPacket.AgentData.SessionID.NotEqual(m_sessionId) || requestPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnParcelAccessListRequest?.Invoke(requestPacket.AgentData.AgentID, requestPacket.AgentData.SessionID,
                                               requestPacket.Data.Flags, requestPacket.Data.SequenceID,
                                               requestPacket.Data.LocalID, this);
        }

        private void HandleParcelAccessListUpdate(Packet Pack)
        {
            if(OnParcelAccessListUpdateRequest is null)
                return;

            ParcelAccessListUpdatePacket updatePacket = (ParcelAccessListUpdatePacket)Pack;
            if (updatePacket.AgentData.SessionID.NotEqual(m_sessionId) || updatePacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            // viewers do send estimated number of packets and sequenceID, but don't seem reliable.
            List<LandAccessEntry> entries = new(updatePacket.List.Length);
            foreach (ParcelAccessListUpdatePacket.ListBlock block in updatePacket.List)
            {
                LandAccessEntry entry = new()
                {
                    AgentID = block.ID,
                    Flags = (AccessList)block.Flags,
                    Expires = block.Time
                };
                entries.Add(entry);
            }

            OnParcelAccessListUpdateRequest?.Invoke(updatePacket.AgentData.AgentID,
                                                     updatePacket.Data.Flags,
                                                     updatePacket.Data.TransactionID,
                                                     updatePacket.Data.LocalID,
                                                     entries, this);
        }

        private void HandleParcelPropertiesRequest(Packet Pack)
        {
            ParcelPropertiesRequestPacket propertiesRequest = (ParcelPropertiesRequestPacket)Pack;
            if (propertiesRequest.AgentData.SessionID.NotEqual(m_sessionId) || propertiesRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            ParcelPropertiesRequestPacket.ParcelDataBlock pdb = propertiesRequest.ParcelData;
            OnParcelPropertiesRequest?.Invoke((int)MathF.Round(pdb.West), (int)MathF.Round(pdb.South),
                                              (int)MathF.Round(pdb.East), (int)MathF.Round(pdb.North),
                                              pdb.SequenceID, pdb.SnapSelection, this);
        }

        private void HandleParcelDivide(Packet Pack)
        {
            ParcelDividePacket landDivide = (ParcelDividePacket)Pack;
            if (landDivide.AgentData.SessionID.NotEqual(m_sessionId) || landDivide.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnParcelDivideRequest?.Invoke((int)MathF.Round(landDivide.ParcelData.West),
                                           (int)MathF.Round(landDivide.ParcelData.South),
                                           (int)Math.Round(landDivide.ParcelData.East),
                                           (int)MathF.Round(landDivide.ParcelData.North), this);
        }

        private void HandleParcelJoin(Packet Pack)
        {
            ParcelJoinPacket landJoin = (ParcelJoinPacket)Pack;
            if (landJoin.AgentData.SessionID.NotEqual(m_sessionId) || landJoin.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnParcelJoinRequest?.Invoke((int)MathF.Round(landJoin.ParcelData.West),
                                         (int)MathF.Round(landJoin.ParcelData.South),
                                         (int)MathF.Round(landJoin.ParcelData.East),
                                         (int)MathF.Round(landJoin.ParcelData.North), this);
        }

        private void HandleParcelPropertiesUpdate(Packet Pack)
        {
            if (OnParcelPropertiesUpdateRequest is null)
                return;

            ParcelPropertiesUpdatePacket parcelPropertiesPacket = (ParcelPropertiesUpdatePacket)Pack;
            if (parcelPropertiesPacket.AgentData.SessionID.NotEqual(m_sessionId) || parcelPropertiesPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            LandUpdateArgs args = new()
            {
                AuthBuyerID = parcelPropertiesPacket.ParcelData.AuthBuyerID,
                Category = (ParcelCategory)parcelPropertiesPacket.ParcelData.Category,
                Desc = Utils.BytesToString(parcelPropertiesPacket.ParcelData.Desc),
                GroupID = parcelPropertiesPacket.ParcelData.GroupID,
                LandingType = parcelPropertiesPacket.ParcelData.LandingType,
                MediaAutoScale = parcelPropertiesPacket.ParcelData.MediaAutoScale,
                MediaID = parcelPropertiesPacket.ParcelData.MediaID,
                MediaURL = Utils.BytesToString(parcelPropertiesPacket.ParcelData.MediaURL),
                MusicURL = Utils.BytesToString(parcelPropertiesPacket.ParcelData.MusicURL),
                Name = Utils.BytesToString(parcelPropertiesPacket.ParcelData.Name),
                ParcelFlags = parcelPropertiesPacket.ParcelData.ParcelFlags,
                PassHours = parcelPropertiesPacket.ParcelData.PassHours,
                PassPrice = parcelPropertiesPacket.ParcelData.PassPrice,
                SalePrice = parcelPropertiesPacket.ParcelData.SalePrice,
                SnapshotID = parcelPropertiesPacket.ParcelData.SnapshotID,
                UserLocation = parcelPropertiesPacket.ParcelData.UserLocation,
                UserLookAt = parcelPropertiesPacket.ParcelData.UserLookAt
            };

            OnParcelPropertiesUpdateRequest?.Invoke(args, parcelPropertiesPacket.ParcelData.LocalID, this);
        }

        private void HandleParcelSelectObjects(Packet Pack)
        {
            if(OnParcelSelectObjects is null)
                return;

            ParcelSelectObjectsPacket selectPacket = (ParcelSelectObjectsPacket)Pack;
            if (selectPacket.AgentData.SessionID.NotEqual(m_sessionId) || selectPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            List<UUID> returnIDs = new(selectPacket.ReturnIDs.Length);
            foreach (ParcelSelectObjectsPacket.ReturnIDsBlock rb in selectPacket.ReturnIDs)
            {
                returnIDs.Add(rb.ReturnID);
            }

            OnParcelSelectObjects?.Invoke(selectPacket.ParcelData.LocalID,
                                           Convert.ToInt32(selectPacket.ParcelData.ReturnType), returnIDs, this);
        }

        private void HandleParcelObjectOwnersRequest(Packet Pack)
        {
            ParcelObjectOwnersRequestPacket reqPacket = (ParcelObjectOwnersRequestPacket)Pack;
            if (reqPacket.AgentData.SessionID.NotEqual(m_sessionId) || reqPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnParcelObjectOwnerRequest?.Invoke(reqPacket.ParcelData.LocalID, this);
        }

        private void HandleParcelGodForceOwner(Packet Pack)
        {
            ParcelGodForceOwnerPacket godForceOwnerPacket = (ParcelGodForceOwnerPacket)Pack;
            if (godForceOwnerPacket.AgentData.SessionID.NotEqual(m_sessionId) || godForceOwnerPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnParcelGodForceOwner?.Invoke(godForceOwnerPacket.Data.LocalID, godForceOwnerPacket.Data.OwnerID, this);
        }

        private void HandleParcelRelease(Packet Pack)
        {
            ParcelReleasePacket releasePacket = (ParcelReleasePacket)Pack;
            if (releasePacket.AgentData.SessionID.NotEqual(m_sessionId) || releasePacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnParcelAbandonRequest?.Invoke(releasePacket.Data.LocalID, this);
        }

        private void HandleParcelReclaim(Packet Pack)
        {
            ParcelReclaimPacket reclaimPacket = (ParcelReclaimPacket)Pack;
            if (reclaimPacket.AgentData.SessionID.NotEqual(m_sessionId) || reclaimPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnParcelReclaim?.Invoke(reclaimPacket.Data.LocalID, this);
        }

        private void HandleParcelReturnObjects(Packet Pack)
        {
            if(OnParcelReturnObjectsRequest is null)
                return;

            ParcelReturnObjectsPacket parcelReturnObjects = (ParcelReturnObjectsPacket)Pack;
            if (parcelReturnObjects.AgentData.SessionID.NotEqual(m_sessionId) || parcelReturnObjects.AgentData.AgentID.NotEqual(m_agentId))
                return;

            UUID[] puserselectedOwnerIDs = new UUID[parcelReturnObjects.OwnerIDs.Length];
            for (int parceliterator = 0; parceliterator < parcelReturnObjects.OwnerIDs.Length; parceliterator++)
                puserselectedOwnerIDs[parceliterator] = parcelReturnObjects.OwnerIDs[parceliterator].OwnerID;

            UUID[] puserselectedTaskIDs = new UUID[parcelReturnObjects.TaskIDs.Length];
            for (int parceliterator = 0; parceliterator < parcelReturnObjects.TaskIDs.Length; parceliterator++)
                puserselectedTaskIDs[parceliterator] = parcelReturnObjects.TaskIDs[parceliterator].TaskID;

            OnParcelReturnObjectsRequest?.Invoke(parcelReturnObjects.ParcelData.LocalID, parcelReturnObjects.ParcelData.ReturnType, puserselectedOwnerIDs, puserselectedTaskIDs, this);
        }

        private void HandleParcelSetOtherCleanTime(Packet Pack)
        {
            ParcelSetOtherCleanTimePacket parcelSetOtherCleanTimePacket = (ParcelSetOtherCleanTimePacket)Pack;
            if (parcelSetOtherCleanTimePacket.AgentData.SessionID.NotEqual(m_sessionId) || parcelSetOtherCleanTimePacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnParcelSetOtherCleanTime?.Invoke(this,
                                               parcelSetOtherCleanTimePacket.ParcelData.LocalID,
                                               parcelSetOtherCleanTimePacket.ParcelData.OtherCleanTime);
        }

        private void HandleLandStatRequest(Packet Pack)
        {
            LandStatRequestPacket lsrp = (LandStatRequestPacket)Pack;
            if (lsrp.AgentData.SessionID.NotEqual(m_sessionId) || lsrp.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnLandStatRequest?.Invoke(lsrp.RequestData.ParcelLocalID, lsrp.RequestData.ReportType, lsrp.RequestData.RequestFlags, Utils.BytesToString(lsrp.RequestData.Filter), this);
        }

        private void HandleParcelDwellRequest(Packet Pack)
        {
            ParcelDwellRequestPacket dwellrq = (ParcelDwellRequestPacket)Pack;
            if (dwellrq.AgentData.SessionID.NotEqual(m_sessionId) || dwellrq.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnParcelDwellRequest?.Invoke(dwellrq.Data.LocalID, this);
        }

        #endregion Parcel related packets

        #region Estate Packets
        private static double m_lastMapRegenTime = Double.MinValue;

        private void HandleEstateOwnerMessage(Packet Pack)
        {
            EstateOwnerMessagePacket messagePacket = (EstateOwnerMessagePacket)Pack;
            if (messagePacket.AgentData.SessionID.NotEqual(m_sessionId) || messagePacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            string method = Utils.BytesToString(messagePacket.MethodData.Method);
            switch (method)
            {
                case "getinfo":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        OnDetailedEstateDataRequest?.Invoke(this, messagePacket.MethodData.Invoice);
                    }
                    return;
                case "setregioninfo":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        OnSetEstateFlagsRequest?.Invoke(convertParamStringToBool(messagePacket.ParamList[0].Parameter), convertParamStringToBool(messagePacket.ParamList[1].Parameter),
                                                convertParamStringToBool(messagePacket.ParamList[2].Parameter), !convertParamStringToBool(messagePacket.ParamList[3].Parameter),
                                                Convert.ToInt16(Convert.ToDecimal(Utils.BytesToString(messagePacket.ParamList[4].Parameter), Culture.NumberFormatInfo)),
                                                (float)Convert.ToDecimal(Utils.BytesToString(messagePacket.ParamList[5].Parameter), Culture.NumberFormatInfo),
                                                Convert.ToInt16(Utils.BytesToString(messagePacket.ParamList[6].Parameter)),
                                                convertParamStringToBool(messagePacket.ParamList[7].Parameter), convertParamStringToBool(messagePacket.ParamList[8].Parameter));
                    }
                    return;
                //                            case "texturebase":
                //                                if (((Scene)m_scene).Permissions.CanIssueEstateCommand(m_agentId, false))
                //                                {
                //                                    foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList)
                //                                    {
                //                                        string s = Utils.BytesToString(block.Parameter);
                //                                        string[] splitField = s.Split(' ');
                //                                        if (splitField.Length == 2)
                //                                        {
                //                                            UUID tempUUID = new UUID(splitField[1]);
                //                                            OnSetEstateTerrainBaseTexture(this, Convert.ToInt16(splitField[0]), tempUUID);
                //                                        }
                //                                    }
                //                                }
                //                                break;
                case "texturedetail":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList)
                        {
                            string s = Utils.BytesToString(block.Parameter);
                            string[] splitField = s.Split(' ');
                            if (splitField.Length == 2)
                            {
                                Int16 corner = Convert.ToInt16(splitField[0]);
                                UUID textureUUID = new(splitField[1]);

                                OnSetEstateTerrainDetailTexture?.Invoke(this, corner, textureUUID);
                            }
                        }
                    }

                    return;
                case "textureheights":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList)
                        {
                            string s = Utils.BytesToString(block.Parameter);
                            string[] splitField = s.Split(' ');
                            if (splitField.Length == 3)
                            {
                                Int16 corner = Convert.ToInt16(splitField[0]);
                                float lowValue = Convert.ToSingle(splitField[1], Culture.NumberFormatInfo);
                                float highValue = Convert.ToSingle(splitField[2], Culture.NumberFormatInfo);

                                OnSetEstateTerrainTextureHeights?.Invoke(this, corner, lowValue, highValue);
                            }
                        }
                    }
                    return;
                case "texturecommit":
                    OnCommitEstateTerrainTextureRequest?.Invoke(this);
                    return;
                case "setregionterrain":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        if (messagePacket.ParamList.Length != 9)
                        {
                            m_log.Error("EstateOwnerMessage: SetRegionTerrain method has a ParamList of invalid length");
                        }
                        else
                        {
                            try
                            {
                                float WaterHeight = Convert.ToSingle(Utils.BytesToString(messagePacket.ParamList[0].Parameter), Culture.NumberFormatInfo);
                                float TerrainRaiseLimit = Convert.ToSingle(Utils.BytesToString(messagePacket.ParamList[1].Parameter), Culture.NumberFormatInfo);
                                float TerrainLowerLimit = Convert.ToSingle(Utils.BytesToString(messagePacket.ParamList[2].Parameter), Culture.NumberFormatInfo);
                                bool UseEstateSun = convertParamStringToBool(messagePacket.ParamList[3].Parameter);
                                bool UseFixedSun = convertParamStringToBool(messagePacket.ParamList[4].Parameter);
                                float SunHour = Convert.ToSingle(Utils.BytesToString(messagePacket.ParamList[5].Parameter), Culture.NumberFormatInfo);
                                bool UseGlobal = convertParamStringToBool(messagePacket.ParamList[6].Parameter);
                                bool EstateFixedSun = convertParamStringToBool(messagePacket.ParamList[7].Parameter);
                                float EstateSunHour = Convert.ToSingle(Utils.BytesToString(messagePacket.ParamList[8].Parameter), Culture.NumberFormatInfo);

                                OnSetRegionTerrainSettings?.Invoke(WaterHeight, TerrainRaiseLimit, TerrainLowerLimit, UseEstateSun, UseFixedSun, SunHour, UseGlobal, EstateFixedSun, EstateSunHour);
                            }
                            catch (Exception ex)
                            {
                                m_log.Error("EstateOwnerMessage: Exception while setting terrain settings: \n" + messagePacket + "\n" + ex);
                            }
                        }
                    }

                    return;
                case "restart":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        // There's only 1 block in the estateResetSim..   and that's the number of seconds till restart.
                        foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList)
                        {
                            if(Utils.TryParseSingle(Utils.BytesToString(block.Parameter), out float timeSeconds))
                                OnEstateRestartSimRequest?.Invoke(this, (int)timeSeconds);
                        }
                    }
                    return;
                case "estatechangecovenantid":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        foreach (EstateOwnerMessagePacket.ParamListBlock block in messagePacket.ParamList)
                        {
                            UUID newCovenantID = new(Utils.BytesToString(block.Parameter));
                            OnEstateChangeCovenantRequest?.Invoke(this, newCovenantID);
                        }
                    }
                    return;
                case "estateaccessdelta": // Estate access delta manages the banlist and allow list too.
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        int estateAccessType = Convert.ToInt16(Utils.BytesToString(messagePacket.ParamList[1].Parameter));

                        OnUpdateEstateAccessDeltaRequest?.Invoke(this, messagePacket.MethodData.Invoice, estateAccessType, new UUID(Utils.BytesToString(messagePacket.ParamList[2].Parameter)));

                    }
                    return;
                case "simulatormessage":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        UUID SenderID = new(Utils.BytesToString(messagePacket.ParamList[2].Parameter));
                        string SenderName = Utils.BytesToString(messagePacket.ParamList[3].Parameter);
                        string Message = Utils.BytesToString(messagePacket.ParamList[4].Parameter);
                        OnSimulatorBlueBoxMessageRequest?.Invoke(this, messagePacket.MethodData.Invoice,
                            SenderID, messagePacket.AgentData.SessionID, SenderName, Message);
                    }
                    return;
                case "instantmessage":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        if (messagePacket.ParamList.Length < 2)
                            return;

                        UUID SenderID;
                        string SenderName;
                        string Message;

                        if (messagePacket.ParamList.Length < 5)
                        {
                            SenderID = m_agentId;
                            SenderName = Utils.BytesToString(messagePacket.ParamList[0].Parameter);
                            Message = Utils.BytesToString(messagePacket.ParamList[1].Parameter);
                        }
                        else
                        {
                            SenderID = new UUID(Utils.BytesToString(messagePacket.ParamList[2].Parameter));
                            SenderName = Utils.BytesToString(messagePacket.ParamList[3].Parameter);
                            Message = Utils.BytesToString(messagePacket.ParamList[4].Parameter);
                        }

                        OnEstateBlueBoxMessageRequest?.Invoke(this, messagePacket.MethodData.Invoice,
                            SenderID, m_sessionId, SenderName, Message);
                    }
                    return;
                case "setregiondebug":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        if(OnEstateDebugRegionRequest is not null)
                        {
                            bool scripted = convertParamStringToBool(messagePacket.ParamList[0].Parameter);
                            bool collisionEvents = convertParamStringToBool(messagePacket.ParamList[1].Parameter);
                            bool physics = convertParamStringToBool(messagePacket.ParamList[2].Parameter);

                            OnEstateDebugRegionRequest?.Invoke(this, messagePacket.MethodData.Invoice,
                                m_agentId, scripted, collisionEvents, physics);
                        }
                    }
                    return;
                case "teleporthomeuser":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        if(UUID.TryParse(Utils.BytesToString(messagePacket.ParamList[1].Parameter), out UUID Prey))
                            OnEstateTeleportOneUserHomeRequest?.Invoke(this, messagePacket.MethodData.Invoice, m_agentId, Prey, false);
                    }
                    return;
                case "teleporthomeallusers":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        OnEstateTeleportAllUsersHomeRequest?.Invoke(this, messagePacket.MethodData.Invoice, m_agentId);
                    }
                    return;
                case "colliders":
                    OnLandStatRequest?.Invoke(0, 1, 0, "", this);
                    return;
                case "scripts":
                    OnLandStatRequest?.Invoke(0, 0, 0, "", this);
                    return;
                case "terrain":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        if (messagePacket.ParamList.Length > 0)
                        {
                            string p0 = Utils.BytesToString(messagePacket.ParamList[0].Parameter);
                            switch(p0)
                            {
                                case "bake":
                                    OnBakeTerrain?.Invoke(this);
                                    break;

                                case "download filename":
                                    if (messagePacket.ParamList.Length > 1)
                                        OnRequestTerrain?.Invoke(this, Utils.BytesToString(messagePacket.ParamList[1].Parameter));
                                    break;

                                case "upload filename":
                                    if (messagePacket.ParamList.Length > 1)
                                        OnUploadTerrain?.Invoke(this, Utils.BytesToString(messagePacket.ParamList[1].Parameter));
                                    break;

                                default:
                                    break;
                            }
                        }
                    }
                    return;

                case "estatechangeinfo":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        UInt32 param1 = Convert.ToUInt32(Utils.BytesToString(messagePacket.ParamList[1].Parameter));
                        UInt32 param2 = Convert.ToUInt32(Utils.BytesToString(messagePacket.ParamList[2].Parameter));
                        OnEstateChangeInfo?.Invoke(this, messagePacket.MethodData.Invoice, m_agentId, param1, param2);
                    }
                    return;

                case "telehub":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        UInt32 param1 = 0u;
                        string command = Utils.BytesToString(messagePacket.ParamList[0].Parameter);
                        if (command != "info ui")
                        {
                            try
                            {
                                param1 = Convert.ToUInt32(Utils.BytesToString(messagePacket.ParamList[1].Parameter));
                            }
                            catch {}
                        }
                        OnEstateManageTelehub?.Invoke(this, messagePacket.MethodData.Invoice, messagePacket.AgentData.AgentID, command, param1);
                    }
                    return;

                case "refreshmapvisibility":
                    if (m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        IWorldMapModule mapModule = Scene.RequestModuleInterface<IWorldMapModule>();
                        if (mapModule is null)
                        {
                            SendAlertMessage("Terrain map generator not avaiable");
                            return;
                        }
                        if (m_lastMapRegenTime == Double.MaxValue)
                        {
                            SendAlertMessage("Terrain map generation still in progress");
                            return;
                        }

                        double now = Util.GetTimeStamp();
                        if (now - m_lastMapRegenTime < 120) // 2 minutes global cool down
                        {
                            SendAlertMessage("Please wait at least 2 minutes between map generation commands");
                            return;
                        }

                        m_lastMapRegenTime = Double.MaxValue;
                        m_scene.RegenerateMaptileAndReregister(this, null);
                        SendAlertMessage("Terrain map generated");
                        m_lastMapRegenTime = now;
                    }
                    return;

                case "kickestate":

                    if(m_scene.Permissions.CanIssueEstateCommand(m_agentId, false))
                    {
                        if(UUID.TryParse(Utils.BytesToString(messagePacket.ParamList[0].Parameter), out UUID Prey))
                            OnEstateTeleportOneUserHomeRequest?.Invoke(this, messagePacket.MethodData.Invoice,
                                m_agentId, Prey, true);
                    }
                    return;

                default:
                    m_log.WarnFormat(
                        "[LLCLIENTVIEW]: EstateOwnerMessage: Unknown method {0} requested for {1} in {2}",
                        method, Name, Scene.Name);

                    for (int i = 0; i < messagePacket.ParamList.Length; i++)
                    {
                        EstateOwnerMessagePacket.ParamListBlock block = messagePacket.ParamList[i];
                        string data = (string)Utils.BytesToString(block.Parameter);
                        m_log.DebugFormat("[LLCLIENTVIEW]: Param {0}={1}", i, data);
                    }

                    return;
            }

            //int parcelID, uint reportType, uint requestflags, string filter

            //lsrp.RequestData.ParcelLocalID;
            //lsrp.RequestData.ReportType; // 1 = colliders, 0 = scripts
            //lsrp.RequestData.RequestFlags;
            //lsrp.RequestData.Filter;
        }

        private void HandleRequestRegionInfo(Packet Pack)
        {
            RequestRegionInfoPacket.AgentDataBlock mPacket = ((RequestRegionInfoPacket)Pack).AgentData;
            if (mPacket.SessionID.NotEqual(m_sessionId) || mPacket.AgentID.NotEqual(m_agentId))
                return;

            OnRegionInfoRequest?.Invoke(this);
        }

        private void HandleEstateCovenantRequest(Packet Pack)
        {
            EstateCovenantRequestPacket.AgentDataBlock epack = ((EstateCovenantRequestPacket)Pack).AgentData;
            if (epack.SessionID.NotEqual(m_sessionId) || epack.AgentID.NotEqual(m_agentId))
                return;

            OnEstateCovenantRequest?.Invoke(this);
        }

        #endregion Estate Packets

        #region GodPackets

        private void HandleRequestGodlikePowers(Packet Pack)
        {
            RequestGodlikePowersPacket rglpPack = (RequestGodlikePowersPacket)Pack;
            RequestGodlikePowersPacket.AgentDataBlock ablock = rglpPack.AgentData;
            if (ablock.SessionID.NotEqual(m_sessionId) || ablock.AgentID.NotEqual(m_agentId))
                return;

            RequestGodlikePowersPacket.RequestBlockBlock rblock = rglpPack.RequestBlock;
            UUID token = rblock.Token;
            OnRequestGodlikePowers?.Invoke(ablock.AgentID, ablock.SessionID, token, rblock.Godlike);
        }

        private void HandleGodUpdateRegionInfoUpdate(Packet packet)
        {
            GodUpdateRegionInfoPacket GodUpdateRegionInfo = (GodUpdateRegionInfoPacket)packet;
            if (GodUpdateRegionInfo.AgentData.SessionID.NotEqual(m_sessionId) || GodUpdateRegionInfo.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnGodUpdateRegionInfoUpdate?.Invoke(this,
                                           GodUpdateRegionInfo.RegionInfo.BillableFactor,
                                           GodUpdateRegionInfo.RegionInfo.EstateID,
                                           GodUpdateRegionInfo.RegionInfo.RegionFlags,
                                           GodUpdateRegionInfo.RegionInfo.SimName,
                                           GodUpdateRegionInfo.RegionInfo.RedirectGridX,
                                           GodUpdateRegionInfo.RegionInfo.RedirectGridY);
        }

        private void HandleSimWideDeletes(Packet packet)
        {
            SimWideDeletesPacket SimWideDeletesRequest = (SimWideDeletesPacket)packet;
            if(SimWideDeletesRequest.AgentData.AgentID.NotEqual(m_agentId) || SimWideDeletesRequest.AgentData.SessionID.NotEqual(m_sessionId))
                return;

            OnSimWideDeletes?.Invoke(this, SimWideDeletesRequest.AgentData.AgentID,(int)SimWideDeletesRequest.DataBlock.Flags,SimWideDeletesRequest.DataBlock.TargetID);
        }

        private void HandleGodlikeMessage(Packet packet)
        {
            GodlikeMessagePacket GodlikeMessage = (GodlikeMessagePacket)packet;

            if (GodlikeMessage.AgentData.SessionID.NotEqual(m_sessionId) || GodlikeMessage.AgentData.AgentID.NotEqual(m_agentId))
                return;

            onGodlikeMessage?.Invoke(this,
                                      GodlikeMessage.MethodData.Invoice,
                                      GodlikeMessage.MethodData.Method,
                                      GodlikeMessage.ParamList[0].Parameter);
        }

        private void HandleSaveStatePacket(Packet packet)
        {
            StateSavePacket SaveStateMessage = (StateSavePacket)packet;

            if (SaveStateMessage.AgentData.SessionID.NotEqual(m_sessionId) || SaveStateMessage.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnSaveState?.Invoke(this,SaveStateMessage.AgentData.AgentID);
        }

        private void HandleGodKickUser(Packet Pack)
        {
            GodKickUserPacket gkupack = (GodKickUserPacket)Pack;
            if (gkupack.UserInfo.GodSessionID.NotEqual(m_sessionId) || gkupack.UserInfo.GodID.NotEqual(m_agentId))
                return;

            OnGodKickUser?.Invoke(gkupack.UserInfo.GodID, gkupack.UserInfo.AgentID, gkupack.UserInfo.KickFlags, gkupack.UserInfo.Reason);
        }
        #endregion GodPackets

        #region Economy/Transaction Packets

        private void HandleMoneyBalanceRequest(Packet Pack)
        {
            MoneyBalanceRequestPacket moneybalancerequestpacket = (MoneyBalanceRequestPacket)Pack;
            if (moneybalancerequestpacket.AgentData.SessionID.NotEqual(m_sessionId) || moneybalancerequestpacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnMoneyBalanceRequest?.Invoke(this, moneybalancerequestpacket.AgentData.AgentID, moneybalancerequestpacket.AgentData.SessionID, moneybalancerequestpacket.MoneyData.TransactionID);
        }

        private void HandleEconomyDataRequest(Packet Pack)
        {
            OnEconomyDataRequest?.Invoke(this);
        }

        private void HandleRequestPayPrice(Packet Pack)
        {
            RequestPayPricePacket requestPayPricePacket = (RequestPayPricePacket)Pack;
            OnRequestPayPrice?.Invoke(this, requestPayPricePacket.ObjectData.ObjectID);
        }

        private void HandleObjectSaleInfo(Packet Pack)
        {
            if(OnObjectSaleInfo is null)
                return;

            ObjectSaleInfoPacket objectSaleInfoPacket = (ObjectSaleInfoPacket)Pack;
            if (objectSaleInfoPacket.AgentData.SessionID.NotEqual(m_sessionId) || objectSaleInfoPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            foreach (ObjectSaleInfoPacket.ObjectDataBlock d in objectSaleInfoPacket.ObjectData)
            {
                OnObjectSaleInfo?.Invoke(this,
                                        m_agentId,
                                        SessionId,
                                        d.LocalID,
                                        d.SaleType,
                                        d.SalePrice);
            }
        }

        private void HandleObjectBuy(Packet Pack)
        {
            if(OnObjectBuy is null)
                return;

            ObjectBuyPacket objectBuyPacket = (ObjectBuyPacket)Pack;
            if (objectBuyPacket.AgentData.SessionID.NotEqual(m_sessionId) || objectBuyPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            foreach (ObjectBuyPacket.ObjectDataBlock d in objectBuyPacket.ObjectData)
            {
                OnObjectBuy?.Invoke(this,
                                    objectBuyPacket.AgentData.AgentID,
                                    objectBuyPacket.AgentData.SessionID,
                                    objectBuyPacket.AgentData.GroupID,
                                    objectBuyPacket.AgentData.CategoryID,
                                    d.ObjectLocalID,
                                    d.SaleType,
                                    d.SalePrice);
            }
        }

        #endregion Economy/Transaction Packets

        #region Script Packets
        private void HandleGetScriptRunning(Packet Pack)
        {
            GetScriptRunningPacket scriptRunning = (GetScriptRunningPacket)Pack;

            OnGetScriptRunning?.Invoke(this, scriptRunning.Script.ObjectID, scriptRunning.Script.ItemID);
        }

        private void HandleSetScriptRunning(Packet Pack)
        {
            SetScriptRunningPacket setScriptRunning = (SetScriptRunningPacket)Pack;
            if (setScriptRunning.AgentData.SessionID.NotEqual(m_sessionId) || setScriptRunning.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnSetScriptRunning?.Invoke(this, setScriptRunning.Script.ObjectID, setScriptRunning.Script.ItemID, setScriptRunning.Script.Running);
        }

        private void HandleScriptReset(Packet Pack)
        {
            ScriptResetPacket scriptResetPacket = (ScriptResetPacket)Pack;
            if (scriptResetPacket.AgentData.SessionID.NotEqual(m_sessionId) || scriptResetPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnScriptReset?.Invoke(this, scriptResetPacket.Script.ObjectID, scriptResetPacket.Script.ItemID);
        }

        #endregion Script Packets

        #region Gesture Managment

        private void HandleActivateGestures(Packet Pack)
        {
            if(OnActivateGesture is null)
                return;

            ActivateGesturesPacket activateGesturePacket = (ActivateGesturesPacket)Pack;
            if (activateGesturePacket.AgentData.SessionID.NotEqual(m_sessionId) || activateGesturePacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            ActivateGesturesPacket.DataBlock[] data = activateGesturePacket.Data;

            for (int i= 0; i < data.Length; ++i)
            {
                OnActivateGesture?.Invoke(this,
                                       data[i].AssetID,
                                       data[i].ItemID);
            }
        }

        private void HandleDeactivateGestures(Packet Pack)
        {
            if(OnDeactivateGesture is null)
                return;

            DeactivateGesturesPacket deactivateGesturePacket = (DeactivateGesturesPacket)Pack;
            if (deactivateGesturePacket.AgentData.SessionID.NotEqual(m_sessionId) || deactivateGesturePacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            DeactivateGesturesPacket.DataBlock[] data = deactivateGesturePacket.Data;

            for (int i = 0; i < data.Length; ++i)
            {
                OnDeactivateGesture?.Invoke(this, data[i].ItemID);
            }
        }

        #endregion Gesture Managment

        private void HandleObjectOwner(Packet Pack)
        {
            if (OnObjectOwner is null)
                return;

            ObjectOwnerPacket objectOwnerPacket = (ObjectOwnerPacket)Pack;
            if (objectOwnerPacket.AgentData.SessionID.NotEqual(m_sessionId) || objectOwnerPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            List<uint> localIDs = new();
            foreach (ObjectOwnerPacket.ObjectDataBlock d in objectOwnerPacket.ObjectData)
                localIDs.Add(d.ObjectLocalID);

            OnObjectOwner?.Invoke(this, objectOwnerPacket.HeaderData.OwnerID, objectOwnerPacket.HeaderData.GroupID, localIDs);
        }

        private void HandleAgentFOV(Packet Pack)
        {
            AgentFOVPacket fovPacket = (AgentFOVPacket)Pack;
            if(fovPacket.AgentData.AgentID.NotEqual(m_agentId) || fovPacket.AgentData.SessionID.NotEqual(m_sessionId))
                return;

            uint genCounter = fovPacket.FOVBlock.GenCounter;
            if (genCounter == 0 || genCounter > m_agentFOVCounter)
            {
                m_agentFOVCounter = genCounter;
                OnAgentFOV?.Invoke(this, fovPacket.FOVBlock.VerticalAngle);
            }
        }

        #region unimplemented handlers

        private void HandleViewerStats(Packet Pack)
        {
            // TODO: handle this packet
            //m_log.Warn("[CLIENT]: unhandled ViewerStats packet");
        }

        private void HandleMapItemRequest(Packet Pack)
        {
            MapItemRequestPacket mirpk = (MapItemRequestPacket)Pack;
            if (mirpk.AgentData.SessionID.NotEqual(m_sessionId) || mirpk.AgentData.AgentID.NotEqual(m_agentId))
                return;

            //m_log.Debug(mirpk.ToString());
            try
            {
                OnMapItemRequest?.Invoke(this, mirpk.AgentData.Flags, mirpk.AgentData.EstateID,
                                    mirpk.AgentData.Godlike, mirpk.RequestData.ItemType,
                                    mirpk.RequestData.RegionHandle);
            }
            catch( Exception e)
            {
                m_log.ErrorFormat("{0} HandleMapItemRequest exception: {1} : {2}", LogHeader, e.Message, e.StackTrace);
            } 
        }

        private void HandleTransferAbort(Packet Pack)
        {
        }

        private void HandleMuteListRequest(Packet Pack)
        {
            MuteListRequestPacket muteListRequest = (MuteListRequestPacket)Pack;
            if (muteListRequest.AgentData.SessionID.NotEqual(m_sessionId) || muteListRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            if (OnMuteListRequest != null)
            {
                OnMuteListRequest?.Invoke(this, muteListRequest.MuteData.MuteCRC);
            }
            else
            {
                 if(muteListRequest.MuteData.MuteCRC == 0)
                    SendEmpytMuteList();
                else
                    SendUseCachedMuteList();
            }
        }

        private void HandleUpdateMuteListEntry(Packet packet)
        {
            UpdateMuteListEntryPacket UpdateMuteListEntry = (UpdateMuteListEntryPacket)packet;
            if (UpdateMuteListEntry.AgentData.SessionID.NotEqual(m_sessionId) || UpdateMuteListEntry.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnUpdateMuteListEntry?.Invoke(this, UpdateMuteListEntry.MuteData.MuteID,
                                           Utils.BytesToString(UpdateMuteListEntry.MuteData.MuteName),
                                           UpdateMuteListEntry.MuteData.MuteType,
                                           UpdateMuteListEntry.MuteData.MuteFlags);
        }

        private void HandleRemoveMuteListEntry(Packet packet)
        {
            RemoveMuteListEntryPacket RemoveMuteListEntry = (RemoveMuteListEntryPacket)packet;
            if (RemoveMuteListEntry.AgentData.SessionID.NotEqual(m_sessionId) || RemoveMuteListEntry.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnRemoveMuteListEntry?.Invoke(this,
                                           RemoveMuteListEntry.MuteData.MuteID,
                                           Utils.BytesToString(RemoveMuteListEntry.MuteData.MuteName));
        }

        private void HandleUserReport(Packet packet)
        {
            UserReportPacket UserReport = (UserReportPacket)packet;
            if (UserReport.AgentData.SessionID.NotEqual(m_sessionId) || UserReport.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnUserReport?.Invoke(this,
                    Utils.BytesToString(UserReport.ReportData.AbuseRegionName),
                    UserReport.ReportData.AbuserID,
                    UserReport.ReportData.Category,
                    UserReport.ReportData.CheckFlags,
                    Utils.BytesToString(UserReport.ReportData.Details),
                    UserReport.ReportData.ObjectID,
                    UserReport.ReportData.Position,
                    UserReport.ReportData.ReportType,
                    UserReport.ReportData.ScreenshotID,
                    Utils.BytesToString(UserReport.ReportData.Summary),
                    UserReport.AgentData.AgentID);
        }

        private void HandleSendPostcard(Packet packet)
        {
            //            SendPostcardPacket SendPostcard =
            //                (SendPostcardPacket)packet;
            OnSendPostcard?.Invoke(this);
        }

        private void HandleChangeInventoryItemFlags(Packet packet)
        {
            if(OnChangeInventoryItemFlags is null)
                return;

            ChangeInventoryItemFlagsPacket ChangeInventoryItemFlags = (ChangeInventoryItemFlagsPacket)packet;
            if (ChangeInventoryItemFlags.AgentData.SessionID.NotEqual(m_sessionId) || ChangeInventoryItemFlags.AgentData.AgentID.NotEqual(m_agentId))
                return;

            foreach(ChangeInventoryItemFlagsPacket.InventoryDataBlock b in ChangeInventoryItemFlags.InventoryData)
                    OnChangeInventoryItemFlags?.Invoke(this, b.ItemID, b.Flags);
        }

        private void HandleUseCircuitCode(Packet Pack)
        {
            /*
            UseCircuitCodePacket uccp = (UseCircuitCodePacket)Pack;
            if(uccp.CircuitCode.ID == m_agentId &&
                uccp.CircuitCode.SessionID.Equals(m_sessionId) &&
                uccp.CircuitCode.Code == m_circuitCode &&
                SceneAgent != null &&
               !((ScenePresence)SceneAgent).IsDeleted
            )
                SendRegionHandshake(); // possible someone returning
            */
        }

        private void HandleCreateNewOutfitAttachments(Packet Pack)
        {
            if(OnMoveItemsAndLeaveCopy is null)
                return;

            CreateNewOutfitAttachmentsPacket packet = (CreateNewOutfitAttachmentsPacket)Pack;
            if (packet.AgentData.SessionID.NotEqual(m_sessionId) || packet.AgentData.AgentID.NotEqual(m_agentId))
                return;

            List<InventoryItemBase> items = new();
            foreach (CreateNewOutfitAttachmentsPacket.ObjectDataBlock n in packet.ObjectData)
            {
                InventoryItemBase b = new()
                {
                    ID = n.OldItemID,
                    Folder = n.OldFolderID
                };
                items.Add(b);
            }

            OnMoveItemsAndLeaveCopy?.Invoke(this, items, packet.HeaderData.NewFolderID);
        }

        private void HandleAgentHeightWidth(Packet Pack)
        {
        }


        private void HandleInventoryDescendents(Packet Pack)
        {
        }

        #endregion unimplemented handlers

        #region Dir handlers

        private void HandleDirPlacesQuery(Packet Pack)
        {
            DirPlacesQueryPacket dirPlacesQueryPacket = (DirPlacesQueryPacket)Pack;
            if (dirPlacesQueryPacket.AgentData.SessionID.NotEqual(m_sessionId) || dirPlacesQueryPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnDirPlacesQuery?.Invoke(this,
                                      dirPlacesQueryPacket.QueryData.QueryID,
                                      Utils.BytesToString(
                                          dirPlacesQueryPacket.QueryData.QueryText),
                                      (int)dirPlacesQueryPacket.QueryData.QueryFlags,
                                      (int)dirPlacesQueryPacket.QueryData.Category,
                                      Utils.BytesToString(
                                          dirPlacesQueryPacket.QueryData.SimName),
                                      dirPlacesQueryPacket.QueryData.QueryStart);
        }

        private void HandleDirFindQuery(Packet Pack)
        {
            DirFindQueryPacket dirFindQueryPacket = (DirFindQueryPacket)Pack;
            if (dirFindQueryPacket.AgentData.SessionID.NotEqual(m_sessionId) || dirFindQueryPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnDirFindQuery?.Invoke(this,
                                    dirFindQueryPacket.QueryData.QueryID,
                                    Utils.BytesToString(
                                        dirFindQueryPacket.QueryData.QueryText).Trim(),
                                    dirFindQueryPacket.QueryData.QueryFlags,
                                    dirFindQueryPacket.QueryData.QueryStart);
        }

        private void HandleDirLandQuery(Packet Pack)
        {
            DirLandQueryPacket dirLandQueryPacket = (DirLandQueryPacket)Pack;
            if (dirLandQueryPacket.AgentData.SessionID.NotEqual(m_sessionId) || dirLandQueryPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnDirLandQuery?.Invoke(this,
                                    dirLandQueryPacket.QueryData.QueryID,
                                    dirLandQueryPacket.QueryData.QueryFlags,
                                    dirLandQueryPacket.QueryData.SearchType,
                                    dirLandQueryPacket.QueryData.Price,
                                    dirLandQueryPacket.QueryData.Area,
                                    dirLandQueryPacket.QueryData.QueryStart);
        }

        private void HandleDirPopularQuery(Packet Pack)
        {
            DirPopularQueryPacket dirPopularQueryPacket = (DirPopularQueryPacket)Pack;
            if (dirPopularQueryPacket.AgentData.SessionID.NotEqual(m_sessionId) || dirPopularQueryPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnDirPopularQuery?.Invoke(this,
                                       dirPopularQueryPacket.QueryData.QueryID,
                                       dirPopularQueryPacket.QueryData.QueryFlags);
        }

        private void HandleDirClassifiedQuery(Packet Pack)
        {
            DirClassifiedQueryPacket dirClassifiedQueryPacket = (DirClassifiedQueryPacket)Pack;
            if (dirClassifiedQueryPacket.AgentData.SessionID.NotEqual(m_sessionId) || dirClassifiedQueryPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnDirClassifiedQuery?.Invoke(this,
                                          dirClassifiedQueryPacket.QueryData.QueryID,
                                          Utils.BytesToString(
                                              dirClassifiedQueryPacket.QueryData.QueryText),
                                          dirClassifiedQueryPacket.QueryData.QueryFlags,
                                          dirClassifiedQueryPacket.QueryData.Category,
                                          dirClassifiedQueryPacket.QueryData.QueryStart);
        }

        private void HandleEventInfoRequest(Packet Pack)
        {
            EventInfoRequestPacket eventInfoRequestPacket = (EventInfoRequestPacket)Pack;
            if (eventInfoRequestPacket.AgentData.SessionID.NotEqual(m_sessionId) || eventInfoRequestPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnEventInfoRequest?.Invoke(this, eventInfoRequestPacket.EventData.EventID);
        }

        #endregion

        #region Calling Card

        private void HandleOfferCallingCard(Packet Pack)
        {
            OfferCallingCardPacket offerCallingCardPacket = (OfferCallingCardPacket)Pack;
            if (offerCallingCardPacket.AgentData.SessionID.NotEqual(m_sessionId) || offerCallingCardPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnOfferCallingCard?.Invoke(this,
                                   offerCallingCardPacket.AgentBlock.DestID,
                                   offerCallingCardPacket.AgentBlock.TransactionID);
        }

        private void HandleAcceptCallingCard(Packet Pack)
        {
            AcceptCallingCardPacket acceptCallingCardPacket = (AcceptCallingCardPacket)Pack;
            if (acceptCallingCardPacket.AgentData.SessionID.NotEqual(m_sessionId) || acceptCallingCardPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            // according to http://wiki.secondlife.com/wiki/AcceptCallingCard FolderData should
            // contain exactly one entry
            if (acceptCallingCardPacket.FolderData.Length > 0)
            {
                OnAcceptCallingCard?.Invoke(this,
                                    acceptCallingCardPacket.TransactionBlock.TransactionID,
                                    acceptCallingCardPacket.FolderData[0].FolderID);
            }
        }

        private void HandleDeclineCallingCard(Packet Pack)
        {
            DeclineCallingCardPacket declineCallingCardPacket = (DeclineCallingCardPacket)Pack;
            if (declineCallingCardPacket.AgentData.SessionID.NotEqual(m_sessionId) || declineCallingCardPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnDeclineCallingCard?.Invoke(this,
                                     declineCallingCardPacket.TransactionBlock.TransactionID);
        }

        #endregion Calling Card

        #region Groups

        private void HandleActivateGroup(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            ActivateGroupPacket activateGroupPacket = (ActivateGroupPacket)Pack;
            if (activateGroupPacket.AgentData.SessionID.NotEqual(m_sessionId) || activateGroupPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            m_GroupsModule.ActivateGroup(this, activateGroupPacket.AgentData.GroupID);
        }

        private void HandleGroupVoteHistoryRequest(Packet packet)
        {
            GroupVoteHistoryRequestPacket GroupVoteHistoryRequest = (GroupVoteHistoryRequestPacket)packet;
            if (GroupVoteHistoryRequest.AgentData.SessionID.NotEqual(m_sessionId) || GroupVoteHistoryRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnGroupVoteHistoryRequest?.Invoke(this, m_agentId, SessionId, GroupVoteHistoryRequest.GroupData.GroupID, GroupVoteHistoryRequest.TransactionData.TransactionID);
        }

        private void HandleGroupActiveProposalsRequest(Packet packet)
        {
            GroupActiveProposalsRequestPacket GroupActiveProposalsRequest = (GroupActiveProposalsRequestPacket)packet;
            if (GroupActiveProposalsRequest.AgentData.SessionID.NotEqual(m_sessionId) || GroupActiveProposalsRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnGroupActiveProposalsRequest?.Invoke(this, m_agentId, SessionId, GroupActiveProposalsRequest.GroupData.GroupID, GroupActiveProposalsRequest.TransactionData.TransactionID);
        }

        private void HandleGroupAccountDetailsRequest(Packet packet)
        {
            GroupAccountDetailsRequestPacket GroupAccountDetailsRequest = (GroupAccountDetailsRequestPacket)packet;
            if (GroupAccountDetailsRequest.AgentData.SessionID.NotEqual(m_sessionId) || GroupAccountDetailsRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnGroupAccountDetailsRequest?.Invoke(this, m_agentId, GroupAccountDetailsRequest.AgentData.GroupID, GroupAccountDetailsRequest.MoneyData.RequestID, SessionId);
        }

        private void HandleGroupAccountSummaryRequest(Packet packet)
        {
            GroupAccountSummaryRequestPacket GroupAccountSummaryRequest = (GroupAccountSummaryRequestPacket)packet;
            if (GroupAccountSummaryRequest.AgentData.SessionID.NotEqual(m_sessionId) || GroupAccountSummaryRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnGroupAccountSummaryRequest?.Invoke(this, m_agentId, GroupAccountSummaryRequest.AgentData.GroupID);
        }

        private void HandleGroupTransactionsDetailsRequest(Packet packet)
        {
            GroupAccountTransactionsRequestPacket GroupAccountTransactionsRequest = (GroupAccountTransactionsRequestPacket)packet;
            if (GroupAccountTransactionsRequest.AgentData.SessionID.NotEqual(m_sessionId) || GroupAccountTransactionsRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnGroupAccountTransactionsRequest?.Invoke(this, m_agentId, GroupAccountTransactionsRequest.AgentData.GroupID,GroupAccountTransactionsRequest.MoneyData.RequestID, SessionId);
        }

        private void HandleGroupTitlesRequest(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            GroupTitlesRequestPacket groupTitlesRequest = (GroupTitlesRequestPacket)Pack;
            if (groupTitlesRequest.AgentData.SessionID.NotEqual(m_sessionId) || groupTitlesRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            GroupTitlesReplyPacket groupTitlesReply = (GroupTitlesReplyPacket)PacketPool.Instance.GetPacket(PacketType.GroupTitlesReply);

            groupTitlesReply.AgentData = new GroupTitlesReplyPacket.AgentDataBlock
            {
                AgentID = m_agentId,
                GroupID = groupTitlesRequest.AgentData.GroupID,
                RequestID = groupTitlesRequest.AgentData.RequestID
            };
            List<GroupTitlesData> titles = m_GroupsModule.GroupTitlesRequest(this,
                                                    groupTitlesRequest.AgentData.GroupID);
            groupTitlesReply.GroupData = new GroupTitlesReplyPacket.GroupDataBlock[titles.Count];

            int i = 0;
            foreach (GroupTitlesData d in titles)
            {
                groupTitlesReply.GroupData[i++] = new GroupTitlesReplyPacket.GroupDataBlock
                {
                    Title = Util.StringToBytes256(d.Name),
                    RoleID = d.UUID,
                    Selected = d.Selected
                };
            }

            OutPacket(groupTitlesReply, ThrottleOutPacketType.Task);
        }

        UUID lastGroupProfileRequestID = UUID.Zero;
        double lastGroupProfileRequestTS = Util.GetTimeStampMS();

        private void HandleGroupProfileRequest(Packet Pack)
        {
            if(m_GroupsModule is null)
                return;

            GroupProfileRequestPacket groupProfileRequest = (GroupProfileRequestPacket)Pack;
            if (groupProfileRequest.AgentData.SessionID.NotEqual(m_sessionId) || groupProfileRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            UUID grpID = groupProfileRequest.GroupData.GroupID;
            double ts = Util.GetTimeStampMS();
            if(grpID == lastGroupProfileRequestID && ts - lastGroupProfileRequestTS < 10000)
                return;

            lastGroupProfileRequestID = grpID;
            lastGroupProfileRequestTS = ts;

            GroupProfileReplyPacket groupProfileReply = (GroupProfileReplyPacket)PacketPool.Instance.GetPacket(PacketType.GroupProfileReply);

            groupProfileReply.AgentData = new GroupProfileReplyPacket.AgentDataBlock();
            groupProfileReply.GroupData = new GroupProfileReplyPacket.GroupDataBlock();
            groupProfileReply.AgentData.AgentID = m_agentId;

            GroupProfileData d = m_GroupsModule.GroupProfileRequest(this, groupProfileRequest.GroupData.GroupID);

            if(d.GroupID.IsZero()) // don't send broken data
                return;

            groupProfileReply.GroupData.GroupID = d.GroupID;
            groupProfileReply.GroupData.Name = Util.StringToBytes256(d.Name);
            groupProfileReply.GroupData.Charter = Util.StringToBytes1024(d.Charter);
            groupProfileReply.GroupData.ShowInList = d.ShowInList;
            groupProfileReply.GroupData.MemberTitle = Util.StringToBytes256(d.MemberTitle);
            groupProfileReply.GroupData.PowersMask = d.PowersMask;
            groupProfileReply.GroupData.InsigniaID = d.InsigniaID;
            groupProfileReply.GroupData.FounderID = d.FounderID;
            groupProfileReply.GroupData.MembershipFee = d.MembershipFee;
            groupProfileReply.GroupData.OpenEnrollment = d.OpenEnrollment;
            groupProfileReply.GroupData.Money = d.Money;
            groupProfileReply.GroupData.GroupMembershipCount = d.GroupMembershipCount;
            groupProfileReply.GroupData.GroupRolesCount = d.GroupRolesCount;
            groupProfileReply.GroupData.AllowPublish = d.AllowPublish;
            groupProfileReply.GroupData.MaturePublish = d.MaturePublish;
            groupProfileReply.GroupData.OwnerRole = d.OwnerRole;

            if (m_scene.Permissions.IsGod(m_agentId) && (!IsGroupMember(groupProfileRequest.GroupData.GroupID)))
            {
                if (m_scene.TryGetScenePresence(m_agentId, out ScenePresence p))
                {
                    if (p.IsViewerUIGod)
                    {
                        groupProfileReply.GroupData.OpenEnrollment = true;
                        groupProfileReply.GroupData.MembershipFee = 0;
                    }
                }
            }

            OutPacket(groupProfileReply, ThrottleOutPacketType.Task);

            if(grpID == lastGroupProfileRequestID)
                lastGroupProfileRequestTS = Util.GetTimeStampMS() - 7000;
        }

        private void HandleGroupMembersRequest(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            GroupMembersRequestPacket groupMembersRequestPacket =
                        (GroupMembersRequestPacket)Pack;
            if (groupMembersRequestPacket.AgentData.SessionID.NotEqual(m_sessionId) || groupMembersRequestPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            List<GroupMembersData> members =
                m_GroupsModule.GroupMembersRequest(this, groupMembersRequestPacket.GroupData.GroupID);

            int memberCount = members.Count;
            int indx = 0;
            while (indx < memberCount)
            {
                int blockCount = memberCount - indx;
                if (blockCount > 25)
                    blockCount = 25;

                GroupMembersReplyPacket groupMembersReply = (GroupMembersReplyPacket)PacketPool.Instance.GetPacket(PacketType.GroupMembersReply);

                groupMembersReply.AgentData = new GroupMembersReplyPacket.AgentDataBlock();
                groupMembersReply.GroupData = new GroupMembersReplyPacket.GroupDataBlock();
                groupMembersReply.MemberData = new GroupMembersReplyPacket.MemberDataBlock[blockCount];

                groupMembersReply.AgentData.AgentID = m_agentId;
                groupMembersReply.GroupData.GroupID = groupMembersRequestPacket.GroupData.GroupID;
                groupMembersReply.GroupData.RequestID = groupMembersRequestPacket.GroupData.RequestID;
                groupMembersReply.GroupData.MemberCount = memberCount;

                for (int i = 0; i < blockCount; i++)
                {
                    GroupMembersData m = members[indx++];
                    groupMembersReply.MemberData[i] = new GroupMembersReplyPacket.MemberDataBlock
                    {
                        AgentID = m.AgentID,
                        Contribution = m.Contribution,
                        OnlineStatus = Util.StringToBytes256(m.OnlineStatus),
                        AgentPowers = m.AgentPowers,
                        Title = Util.StringToBytes256(m.Title),
                        IsOwner = m.IsOwner
                    };
                }
                OutPacket(groupMembersReply, ThrottleOutPacketType.Task);
            }
        }

        private void HandleGroupRoleDataRequest(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            GroupRoleDataRequestPacket groupRolesRequest = (GroupRoleDataRequestPacket)Pack;
            if (groupRolesRequest.AgentData.SessionID.NotEqual(m_sessionId) || groupRolesRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            GroupRoleDataReplyPacket groupRolesReply = (GroupRoleDataReplyPacket)PacketPool.Instance.GetPacket(PacketType.GroupRoleDataReply);

            groupRolesReply.AgentData = new GroupRoleDataReplyPacket.AgentDataBlock { AgentID = m_agentId };
            groupRolesReply.GroupData = new GroupRoleDataReplyPacket.GroupDataBlock
            {
                GroupID = groupRolesRequest.GroupData.GroupID,
                RequestID = groupRolesRequest.GroupData.RequestID
            };
            List<GroupRolesData> titles = m_GroupsModule.GroupRoleDataRequest(this,
                                                    groupRolesRequest.GroupData.GroupID);
            groupRolesReply.GroupData.RoleCount = titles.Count;
            groupRolesReply.RoleData = new GroupRoleDataReplyPacket.RoleDataBlock[titles.Count];

            int i = 0;
            foreach (GroupRolesData d in titles)
            {
                groupRolesReply.RoleData[i] = new GroupRoleDataReplyPacket.RoleDataBlock
                {
                    RoleID = d.RoleID,
                    Name = Util.StringToBytes256(d.Name),
                    Title = Util.StringToBytes256(d.Title),
                    Description = Util.StringToBytes1024(d.Description),
                    Powers = d.Powers,
                    Members = (uint)d.Members
                };
                i++;
            }

            OutPacket(groupRolesReply, ThrottleOutPacketType.Task);
        }

        private void HandleGroupRoleMembersRequest(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            GroupRoleMembersRequestPacket groupRoleMembersRequest = (GroupRoleMembersRequestPacket)Pack;
            if (groupRoleMembersRequest.AgentData.SessionID.NotEqual(m_sessionId) || groupRoleMembersRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            List<GroupRoleMembersData> mappings = m_GroupsModule.GroupRoleMembersRequest(this,
                            groupRoleMembersRequest.GroupData.GroupID);

            int mappingsCount = mappings.Count;

            while (mappings.Count > 0)
            {
                int pairs = mappings.Count;
                if (pairs > 32)
                    pairs = 32;

                GroupRoleMembersReplyPacket groupRoleMembersReply = (GroupRoleMembersReplyPacket)PacketPool.Instance.GetPacket(PacketType.GroupRoleMembersReply);
                groupRoleMembersReply.AgentData = new GroupRoleMembersReplyPacket.AgentDataBlock
                {
                    AgentID = m_agentId,
                    GroupID = groupRoleMembersRequest.GroupData.GroupID,
                    RequestID = groupRoleMembersRequest.GroupData.RequestID,

                    TotalPairs = (uint)mappingsCount
                };

                groupRoleMembersReply.MemberData = new GroupRoleMembersReplyPacket.MemberDataBlock[pairs];

                for (int i = 0; i < pairs; i++)
                {
                    GroupRoleMembersData d = mappings[0];
                    mappings.RemoveAt(0);

                    groupRoleMembersReply.MemberData[i] = new GroupRoleMembersReplyPacket.MemberDataBlock
                    {
                        RoleID = d.RoleID,
                        MemberID = d.MemberID
                    };
                }

                OutPacket(groupRoleMembersReply, ThrottleOutPacketType.Task);
            }
        }

        private void HandleCreateGroupRequest(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            CreateGroupRequestPacket createGroupRequest = (CreateGroupRequestPacket)Pack;
            if (createGroupRequest.AgentData.SessionID.NotEqual(m_sessionId) || createGroupRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            m_GroupsModule.CreateGroup(this,
                                           Utils.BytesToString(createGroupRequest.GroupData.Name),
                                           Utils.BytesToString(createGroupRequest.GroupData.Charter),
                                           createGroupRequest.GroupData.ShowInList,
                                           createGroupRequest.GroupData.InsigniaID,
                                           createGroupRequest.GroupData.MembershipFee,
                                           createGroupRequest.GroupData.OpenEnrollment,
                                           createGroupRequest.GroupData.AllowPublish,
                                           createGroupRequest.GroupData.MaturePublish);
        }

        private void HandleUpdateGroupInfo(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            UpdateGroupInfoPacket updateGroupInfo = (UpdateGroupInfoPacket)Pack;
            if (updateGroupInfo.AgentData.SessionID.NotEqual(m_sessionId) || updateGroupInfo.AgentData.AgentID.NotEqual(m_agentId))
                return;

            m_GroupsModule.UpdateGroupInfo(this,
                                               updateGroupInfo.GroupData.GroupID,
                                               Utils.BytesToString(updateGroupInfo.GroupData.Charter),
                                               updateGroupInfo.GroupData.ShowInList,
                                               updateGroupInfo.GroupData.InsigniaID,
                                               updateGroupInfo.GroupData.MembershipFee,
                                               updateGroupInfo.GroupData.OpenEnrollment,
                                               updateGroupInfo.GroupData.AllowPublish,
                                               updateGroupInfo.GroupData.MaturePublish);
        }

        private void HandleSetGroupAcceptNotices(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            SetGroupAcceptNoticesPacket setGroupAcceptNotices = (SetGroupAcceptNoticesPacket)Pack;
            if (setGroupAcceptNotices.AgentData.SessionID.NotEqual(m_sessionId) || setGroupAcceptNotices.AgentData.AgentID.NotEqual(m_agentId))
                return;

            m_GroupsModule.SetGroupAcceptNotices(this,
                                                     setGroupAcceptNotices.Data.GroupID,
                                                     setGroupAcceptNotices.Data.AcceptNotices,
                                                     setGroupAcceptNotices.NewData.ListInProfile);
        }

        private void HandleGroupTitleUpdate(Packet Pack)
        {

            GroupTitleUpdatePacket groupTitleUpdate = (GroupTitleUpdatePacket)Pack;
            if (groupTitleUpdate.AgentData.SessionID.NotEqual(m_sessionId) || groupTitleUpdate.AgentData.AgentID.NotEqual(m_agentId))
                return;

            m_GroupsModule.GroupTitleUpdate(this,
                                                groupTitleUpdate.AgentData.GroupID,
                                                groupTitleUpdate.AgentData.TitleRoleID);
        }

        private void HandleParcelDeedToGroup(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            ParcelDeedToGroupPacket parcelDeedToGroup = (ParcelDeedToGroupPacket)Pack;
            if (parcelDeedToGroup.AgentData.SessionID.NotEqual(m_sessionId) || parcelDeedToGroup.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnParcelDeedToGroup?.Invoke(parcelDeedToGroup.Data.LocalID, parcelDeedToGroup.Data.GroupID, this);
        }

        private void HandleGroupNoticesListRequest(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            GroupNoticesListRequestPacket groupNoticesListRequest = (GroupNoticesListRequestPacket)Pack;
            if (groupNoticesListRequest.AgentData.SessionID.NotEqual(m_sessionId) || groupNoticesListRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            GroupNoticeData[] gn = m_GroupsModule.GroupNoticesListRequest(this, groupNoticesListRequest.Data.GroupID);

            GroupNoticesListReplyPacket groupNoticesListReply = (GroupNoticesListReplyPacket)PacketPool.Instance.GetPacket(PacketType.GroupNoticesListReply);
            groupNoticesListReply.AgentData = new GroupNoticesListReplyPacket.AgentDataBlock
            {
                AgentID = m_agentId,
                GroupID = groupNoticesListRequest.Data.GroupID
            };

            groupNoticesListReply.Data = new GroupNoticesListReplyPacket.DataBlock[gn.Length];

            int i = 0;
            foreach (GroupNoticeData g in gn)
            {
                groupNoticesListReply.Data[i++] = new GroupNoticesListReplyPacket.DataBlock
                {
                    NoticeID = g.NoticeID,
                    Timestamp = g.Timestamp,
                    FromName = Util.StringToBytes256(g.FromName),
                    Subject = Util.StringToBytes256(g.Subject),
                    HasAttachment = g.HasAttachment,
                    AssetType = g.AssetType
                };
            }

            OutPacket(groupNoticesListReply, ThrottleOutPacketType.Task);
        }

        private void HandleGroupNoticeRequest(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            GroupNoticeRequestPacket groupNoticeRequest = (GroupNoticeRequestPacket)Pack;
            if (groupNoticeRequest.AgentData.SessionID.NotEqual(m_sessionId) || groupNoticeRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            m_GroupsModule.GroupNoticeRequest(this, groupNoticeRequest.Data.GroupNoticeID);
        }

        private void HandleGroupRoleUpdate(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            GroupRoleUpdatePacket groupRoleUpdate = (GroupRoleUpdatePacket)Pack;
            if (groupRoleUpdate.AgentData.SessionID.NotEqual(m_sessionId) || groupRoleUpdate.AgentData.AgentID.NotEqual(m_agentId))
                return;

            foreach (GroupRoleUpdatePacket.RoleDataBlock d in groupRoleUpdate.RoleData)
            {
                m_GroupsModule.GroupRoleUpdate(this,
                                                groupRoleUpdate.AgentData.GroupID,
                                                d.RoleID,
                                                Utils.BytesToString(d.Name),
                                                Utils.BytesToString(d.Description),
                                                Utils.BytesToString(d.Title),
                                                d.Powers,
                                                d.UpdateType);
            }
            m_GroupsModule.NotifyChange(groupRoleUpdate.AgentData.GroupID);
        }

        private void HandleGroupRoleChanges(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            GroupRoleChangesPacket groupRoleChanges = (GroupRoleChangesPacket)Pack;
            if (groupRoleChanges.AgentData.SessionID.NotEqual(m_sessionId) || groupRoleChanges.AgentData.AgentID.NotEqual(m_agentId))
                return;

            foreach (GroupRoleChangesPacket.RoleChangeBlock d in groupRoleChanges.RoleChange)
            {
                m_GroupsModule.GroupRoleChanges(this,
                                                groupRoleChanges.AgentData.GroupID,
                                                d.RoleID,
                                                d.MemberID,
                                                d.Change);
            }
            m_GroupsModule.NotifyChange(groupRoleChanges.AgentData.GroupID);
        }

        private void HandleJoinGroupRequest(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            JoinGroupRequestPacket joinGroupRequest = (JoinGroupRequestPacket)Pack;
            if (joinGroupRequest.AgentData.SessionID.NotEqual(m_sessionId) || joinGroupRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            m_GroupsModule.JoinGroupRequest(this, joinGroupRequest.GroupData.GroupID);
        }

        private void HandleLeaveGroupRequest(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            LeaveGroupRequestPacket leaveGroupRequest = (LeaveGroupRequestPacket)Pack;
            if (leaveGroupRequest.AgentData.SessionID.NotEqual(m_sessionId) || leaveGroupRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

             m_GroupsModule.LeaveGroupRequest(this, leaveGroupRequest.GroupData.GroupID);
        }

        private void HandleEjectGroupMemberRequest(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            EjectGroupMemberRequestPacket ejectGroupMemberRequest = (EjectGroupMemberRequestPacket)Pack;
            if (ejectGroupMemberRequest.AgentData.SessionID.NotEqual(m_sessionId) || ejectGroupMemberRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            foreach (EjectGroupMemberRequestPacket.EjectDataBlock e in ejectGroupMemberRequest.EjectData)
            {
                m_GroupsModule.EjectGroupMemberRequest(this,
                        ejectGroupMemberRequest.GroupData.GroupID,
                        e.EjecteeID);
            }
        }

        private void HandleInviteGroupRequest(Packet Pack)
        {
            if (m_GroupsModule is null)
                return;

            InviteGroupRequestPacket inviteGroupRequest = (InviteGroupRequestPacket)Pack;
            if (inviteGroupRequest.AgentData.SessionID.NotEqual(m_sessionId) || inviteGroupRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            foreach (InviteGroupRequestPacket.InviteDataBlock b in inviteGroupRequest.InviteData)
            {
                m_GroupsModule.InviteGroupRequest(this,
                        inviteGroupRequest.GroupData.GroupID,
                        b.InviteeID,
                        b.RoleID);
            }
    }

        #endregion Groups

        private void HandleStartLure(Packet Pack)
        {
            if(OnStartLure is null)
                return;

            StartLurePacket startLureRequest = (StartLurePacket)Pack;
            if (startLureRequest.AgentData.SessionID.NotEqual(m_sessionId) || startLureRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            for (int i = 0 ; i < startLureRequest.TargetData.Length ; i++)
            {
                OnStartLure?.Invoke(startLureRequest.Info.LureType,
                                    Utils.BytesToString(
                                            startLureRequest.Info.Message),
                                    startLureRequest.TargetData[i].TargetID,
                                    this);
            }
        }

        private void HandleTeleportLureRequest(Packet Pack)
        {
            TeleportLureRequestPacket teleportLureRequest = (TeleportLureRequestPacket)Pack;
            if (teleportLureRequest.Info.SessionID.NotEqual(m_sessionId) || teleportLureRequest.Info.AgentID.NotEqual(m_agentId))
                return;

            OnTeleportLureRequest?.Invoke(
                         teleportLureRequest.Info.LureID,
                         teleportLureRequest.Info.TeleportFlags,
                         this);
        }

        private void HandleClassifiedInfoRequest(Packet Pack)
        {
            ClassifiedInfoRequestPacket classifiedInfoRequest = (ClassifiedInfoRequestPacket)Pack;
            if (classifiedInfoRequest.AgentData.SessionID.NotEqual(m_sessionId) || classifiedInfoRequest.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnClassifiedInfoRequest?.Invoke(
                         classifiedInfoRequest.Data.ClassifiedID,
                         this);
        }

        private void HandleClassifiedInfoUpdate(Packet Pack)
        {
            ClassifiedInfoUpdatePacket classifiedInfoUpdate = (ClassifiedInfoUpdatePacket)Pack;
            if (classifiedInfoUpdate.AgentData.SessionID.NotEqual(m_sessionId) || classifiedInfoUpdate.AgentData.AgentID.NotEqual(m_agentId))
                return;

            // fix classifiedFlags maturity
            byte classifiedFlags = classifiedInfoUpdate.Data.ClassifiedFlags;
            if ((classifiedFlags & 0x4e) == 0) // if none
                classifiedFlags |= 0x4; // pg

            OnClassifiedInfoUpdate?.Invoke(
                        classifiedInfoUpdate.Data.ClassifiedID,
                        classifiedInfoUpdate.Data.Category,
                        Utils.BytesToString(
                                classifiedInfoUpdate.Data.Name),
                        Utils.BytesToString(
                                classifiedInfoUpdate.Data.Desc),
                        classifiedInfoUpdate.Data.ParcelID,
                        classifiedInfoUpdate.Data.ParentEstate,
                        classifiedInfoUpdate.Data.SnapshotID,
                        new Vector3(
                            classifiedInfoUpdate.Data.PosGlobal),
                        classifiedFlags,
                        classifiedInfoUpdate.Data.PriceForListing,
                        this);
        }

        private void HandleClassifiedDelete(Packet Pack)
        {
            ClassifiedDeletePacket classifiedDelete = (ClassifiedDeletePacket)Pack;
            if (classifiedDelete.AgentData.SessionID.NotEqual(m_sessionId) || classifiedDelete.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnClassifiedDelete?.Invoke(
                         classifiedDelete.Data.ClassifiedID,
                         this);
        }
        
        private void HandleClassifiedGodDelete(Packet Pack)
        {
            ClassifiedGodDeletePacket classifiedGodDelete = (ClassifiedGodDeletePacket)Pack;
            if (classifiedGodDelete.AgentData.SessionID.NotEqual(m_sessionId) || classifiedGodDelete.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnClassifiedGodDelete?.Invoke(
                         classifiedGodDelete.Data.ClassifiedID,
                         classifiedGodDelete.Data.QueryID,
                         this);
        }

        private void HandleEventGodDelete(Packet Pack)
        {
            EventGodDeletePacket eventGodDelete = (EventGodDeletePacket)Pack;
            if (eventGodDelete.AgentData.SessionID.NotEqual(m_sessionId) || eventGodDelete.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnEventGodDelete?.Invoke(
                        eventGodDelete.EventData.EventID,
                        eventGodDelete.QueryData.QueryID,
                        Utils.BytesToString(
                                eventGodDelete.QueryData.QueryText),
                        eventGodDelete.QueryData.QueryFlags,
                        eventGodDelete.QueryData.QueryStart,
                        this);
        }

        private void HandleEventNotificationAddRequest(Packet Pack)
        {
            EventNotificationAddRequestPacket eventNotificationAdd = (EventNotificationAddRequestPacket)Pack;
            if (eventNotificationAdd.AgentData.SessionID.NotEqual(m_sessionId) || eventNotificationAdd.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnEventNotificationAddRequest?.Invoke(eventNotificationAdd.EventData.EventID, this);
        }

        private void HandleEventNotificationRemoveRequest(Packet Pack)
        {
            EventNotificationRemoveRequestPacket eventNotificationRemove = (EventNotificationRemoveRequestPacket)Pack;
            if (eventNotificationRemove.AgentData.SessionID.NotEqual(m_sessionId) || eventNotificationRemove.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnEventNotificationRemoveRequest?.Invoke(eventNotificationRemove.EventData.EventID, this);
        }

        private void HandleRetrieveInstantMessages(Packet Pack)
        {
            RetrieveInstantMessagesPacket rimpInstantMessagePack = (RetrieveInstantMessagesPacket)Pack;
            if (rimpInstantMessagePack.AgentData.SessionID.NotEqual(m_sessionId) || rimpInstantMessagePack.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnRetrieveInstantMessages?.Invoke(this);
        }

        private void HandlePickDelete(Packet Pack)
        {
            PickDeletePacket pickDelete = (PickDeletePacket)Pack;
            if (pickDelete.AgentData.SessionID.NotEqual(m_sessionId) || pickDelete.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnPickDelete?.Invoke(this, pickDelete.Data.PickID);
        }

        private void HandlePickGodDelete(Packet Pack)
        {
            PickGodDeletePacket pickGodDelete = (PickGodDeletePacket)Pack;
            if (pickGodDelete.AgentData.SessionID.NotEqual(m_sessionId) || pickGodDelete.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnPickGodDelete?.Invoke(this,
                        pickGodDelete.AgentData.AgentID,
                        pickGodDelete.Data.PickID,
                        pickGodDelete.Data.QueryID);
        }

        private void HandlePickInfoUpdate(Packet Pack)
        {
            PickInfoUpdatePacket pickInfoUpdate = (PickInfoUpdatePacket)Pack;
            if (pickInfoUpdate.AgentData.SessionID.NotEqual(m_sessionId) || pickInfoUpdate.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnPickInfoUpdate?.Invoke(this,
                        pickInfoUpdate.Data.PickID,
                        pickInfoUpdate.Data.CreatorID,
                        pickInfoUpdate.Data.TopPick,
                        Utils.BytesToString(pickInfoUpdate.Data.Name),
                        Utils.BytesToString(pickInfoUpdate.Data.Desc),
                        pickInfoUpdate.Data.SnapshotID,
                        pickInfoUpdate.Data.SortOrder,
                        pickInfoUpdate.Data.Enabled);
        }

        private void HandleAvatarNotesUpdate(Packet Pack)
        {
            AvatarNotesUpdatePacket avatarNotesUpdate = (AvatarNotesUpdatePacket)Pack;
            if (avatarNotesUpdate.AgentData.SessionID.NotEqual(m_sessionId) || avatarNotesUpdate.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnAvatarNotesUpdate?.Invoke(this,
                        avatarNotesUpdate.Data.TargetID,
                        Utils.BytesToString(avatarNotesUpdate.Data.Notes));
        }

        private void HandleAvatarInterestsUpdate(Packet Pack)
        {
            AvatarInterestsUpdatePacket avatarInterestUpdate = (AvatarInterestsUpdatePacket)Pack;
            if (avatarInterestUpdate.AgentData.SessionID.NotEqual(m_sessionId) || avatarInterestUpdate.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnAvatarInterestUpdate?.Invoke(this,
                    avatarInterestUpdate.PropertiesData.WantToMask,
                    Utils.BytesToString(avatarInterestUpdate.PropertiesData.WantToText),
                    avatarInterestUpdate.PropertiesData.SkillsMask,
                    Utils.BytesToString(avatarInterestUpdate.PropertiesData.SkillsText),
                    Utils.BytesToString(avatarInterestUpdate.PropertiesData.LanguagesText));
        }

        private void HandleGrantUserRights(Packet Pack)
        {
            GrantUserRightsPacket GrantUserRights = (GrantUserRightsPacket)Pack;
            if (GrantUserRights.AgentData.SessionID.NotEqual(m_sessionId) || GrantUserRights.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnGrantUserRights?.Invoke(this,
                    GrantUserRights.Rights[0].AgentRelated,
                    GrantUserRights.Rights[0].RelatedRights);
        }

        private double m_nextRevokePermissionsTime = Double.MinValue;
        private uint m_lastRevokePermissionsSeq = uint.MinValue;

        private void HandleRevokePermissions(Packet Pack)
        {
            RevokePermissionsPacket pkt = (RevokePermissionsPacket)Pack;
            if (pkt.AgentData.SessionID.NotEqual(m_sessionId) || pkt .AgentData.AgentID.NotEqual(m_agentId))
                return;

            uint thisSeq = pkt.Header.Sequence;
            if (thisSeq == m_lastRevokePermissionsSeq)
                return;
            m_lastRevokePermissionsSeq = thisSeq;

            ScenePresence sp = (ScenePresence)SceneAgent;
            if(sp != null && !sp.IsDeleted && !sp.IsInTransit)
            {
                UUID objectID = pkt.Data.ObjectID;

                double now = Util.GetTimeStampMS();
                if (now < m_nextRevokePermissionsTime)
                    return;

                if (objectID == m_scene.RegionInfo.RegionID)
                    m_nextRevokePermissionsTime = now + 2000;
                else
                    m_nextRevokePermissionsTime = now + 50;

                uint permissions = pkt.Data.ObjectPermissions;
                sp.HandleRevokePermissions(objectID , permissions);
            }
        }

        private void HandlePlacesQuery(Packet Pack)
        {
            PlacesQueryPacket placesQueryPacket = (PlacesQueryPacket)Pack;
            if (placesQueryPacket.AgentData.SessionID.NotEqual(m_sessionId) || placesQueryPacket.AgentData.AgentID.NotEqual(m_agentId))
                return;

            OnPlacesQuery?.Invoke(placesQueryPacket.AgentData.QueryID,
                        placesQueryPacket.TransactionData.TransactionID,
                        Utils.BytesToString(
                                placesQueryPacket.QueryData.QueryText),
                        placesQueryPacket.QueryData.QueryFlags,
                        (byte)placesQueryPacket.QueryData.Category,
                        Utils.BytesToString(
                                placesQueryPacket.QueryData.SimName),
                        this);
        }

        #endregion Packet Handlers

        public void SendScriptQuestion(UUID taskID, string taskName, string ownerName, UUID itemID, int question)
        {
            ScriptQuestionPacket scriptQuestion = (ScriptQuestionPacket)PacketPool.Instance.GetPacket(PacketType.ScriptQuestion);
            scriptQuestion.Data.TaskID = taskID;
            scriptQuestion.Data.ItemID = itemID;
            scriptQuestion.Data.Questions = question;
            scriptQuestion.Data.ObjectName = Util.StringToBytes256(taskName);
            scriptQuestion.Data.ObjectOwner = Util.StringToBytes256(ownerName);
            OutPacket(scriptQuestion, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// Handler called when we receive a logout packet.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="packet"></param>
        /// <returns></returns>
        protected virtual void HandleLogout(Packet packet)
        {
            if (packet.Type == PacketType.LogoutRequest)
            {
                if (((LogoutRequestPacket)packet).AgentData.SessionID.NotEqual(m_sessionId))
                    return;
            }
            Logout(this);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        protected virtual void Logout(IClientAPI client)
        {
            m_log.Info($"[CLIENT]: Got a logout request for {Name} in {Scene.Name}");
            OnLogout?.Invoke(client);
        }

        /// <summary>
        /// </summary>
        /// <remarks>
        /// At the moment, we always reply that there is no cached texture.
        /// </remarks>
        /// <param name="simclient"></param>
        /// <param name="packet"></param>
        /// <returns></returns>

        protected void HandleAgentTextureCached(Packet packet)
        {
            //m_log.Debug("texture cached: " + packet.ToString());
            AgentCachedTexturePacket cachedtex = (AgentCachedTexturePacket)packet;
            AgentCachedTextureResponsePacket cachedresp =
                (AgentCachedTextureResponsePacket)PacketPool.Instance.GetPacket(PacketType.AgentCachedTextureResponse);

            if (cachedtex.AgentData.SessionID.NotEqual(m_sessionId) || cachedtex.AgentData.AgentID.NotEqual(m_agentId))
                return;

            ScenePresence p = m_scene.GetScenePresence(m_agentId);
            if(p is null)
                return;

            WearableCacheItem[] cacheItems = p.Appearance?.WearableCacheItems;

            cachedresp.AgentData.AgentID = m_agentId;
            cachedresp.AgentData.SessionID = m_sessionId;
            cachedresp.AgentData.SerialNum = cachedtex.AgentData.SerialNum;
            cachedresp.WearableData = new AgentCachedTextureResponsePacket.WearableDataBlock[cachedtex.WearableData.Length];

            if (cacheItems is null)
            {
                for (int i = 0; i < cachedtex.WearableData.Length; i++)
                {
                    cachedresp.WearableData[i] = new AgentCachedTextureResponsePacket.WearableDataBlock()
                    {
                        TextureIndex = cachedtex.WearableData[i].TextureIndex,
                        TextureID = UUID.Zero,
                        HostName = Array.Empty<byte>()
                    };
                }
            }
            else
            {
                for (int i = 0; i < cachedtex.WearableData.Length; i++)
                {
                    var checkdWear = cachedtex.WearableData[i];
                    int idx = checkdWear.TextureIndex;
                    if (idx < cacheItems.Length)
                    {
                        var cachedIt = cacheItems[idx];
                        cachedresp.WearableData[i] = new AgentCachedTextureResponsePacket.WearableDataBlock()
                        {
                            TextureIndex = (byte)idx,
                            TextureID = checkdWear.ID.Equals(cachedIt.CacheId) ? cachedIt.TextureID : UUID.Zero,
                            HostName = Array.Empty<byte>()
                        };
                    }
                    else
                    {
                        cachedresp.WearableData[i] = new AgentCachedTextureResponsePacket.WearableDataBlock()
                        {
                            TextureIndex = (byte)idx,
                            TextureID = UUID.Zero,
                            HostName = Array.Empty<byte>()
                        };
                    }
                }
            }
            cachedresp.Header.Zerocoded = true;
            OutPacket(cachedresp, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// Send a response back to a client when it asks the asset server (via the region server) if it has
        /// its appearance texture cached.
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="serial"></param>
        /// <param name="cachedTextures"></param>
        /// <returns></returns>
        public void SendCachedTextureResponse(ISceneEntity avatar, int serial, List<CachedTextureResponseArg> cachedTextures)
        {
            if (avatar is not ScenePresence)
                return;

            AgentCachedTextureResponsePacket cachedresp = (AgentCachedTextureResponsePacket)PacketPool.Instance.GetPacket(PacketType.AgentCachedTextureResponse);

            // TODO: don't create new blocks if recycling an old packet
            cachedresp.AgentData.AgentID = m_agentId;
            cachedresp.AgentData.SessionID = m_sessionId;
            cachedresp.AgentData.SerialNum = serial;
            cachedresp.WearableData = new AgentCachedTextureResponsePacket.WearableDataBlock[cachedTextures.Count];

            for (int i = 0; i < cachedTextures.Count; i++)
            {
                cachedresp.WearableData[i] = new AgentCachedTextureResponsePacket.WearableDataBlock()
                {
                    TextureIndex = (byte)cachedTextures[i].BakedTextureIndex,
                    TextureID = cachedTextures[i].BakedTextureID,
                    HostName = Array.Empty<byte>()
                };
            }

            cachedresp.Header.Zerocoded = true;
            OutPacket(cachedresp, ThrottleOutPacketType.Task);
        }

        protected void HandleMultipleObjUpdate(Packet packet)
        {
            MultipleObjectUpdatePacket multipleupdate = (MultipleObjectUpdatePacket)packet;

            if (multipleupdate.AgentData.SessionID.NotEqual(m_sessionId) || multipleupdate.AgentData.AgentID.NotEqual(m_agentId))
                return;

//            m_log.DebugFormat(
//                "[CLIENT]: Incoming MultipleObjectUpdatePacket contained {0} blocks", multipleupdate.ObjectData.Length);

            Scene tScene = (Scene)m_scene;

            for (int i = 0; i < multipleupdate.ObjectData.Length; i++)
            {
                MultipleObjectUpdatePacket.ObjectDataBlock block = multipleupdate.ObjectData[i];

                // Can't act on Null Data
                if (block.Data != null)
                {
                    uint localId = block.ObjectLocalID;
                    SceneObjectPart part = tScene.GetSceneObjectPart(localId);

                    if (part is null)
                    {
                        // It's a ghost! tell the client to delete it from view.
                        SendKillObject(new List<uint> { localId });
                    }
                    else
                    {
                        ClientChangeObject updatehandler = onClientChangeObject;

                        if (updatehandler != null)
                        {
                            ObjectChangeData udata = new();

                            /*ubit from ll JIRA:
                             * 0x01 position
                             * 0x02 rotation
                             * 0x04 scale

                             * 0x08 LINK_SET
                             * 0x10 UNIFORM for scale
                             */

                            // translate to internal changes
                            // not all cases .. just the ones older code did

                            switch (block.Type)
                            {
                                case 1: //change position sp
                                    udata.position = new Vector3(block.Data, 0);

                                    udata.change = ObjectChangeType.primP;
                                    updatehandler(localId, udata, this);
                                    break;

                                case 2: // rotation sp
                                    udata.rotation = new Quaternion(block.Data, 0, true);

                                    udata.change = ObjectChangeType.primR;
                                    updatehandler(localId, udata, this);
                                    break;

                                case 3: // position plus rotation
                                    udata.position = new Vector3(block.Data, 0);
                                    udata.rotation = new Quaternion(block.Data, 12, true);

                                    udata.change = ObjectChangeType.primPR;
                                    updatehandler(localId, udata, this);
                                    break;

                                case 4: // scale sp
                                    udata.scale = new Vector3(block.Data, 0);
                                    udata.change = ObjectChangeType.primS;

                                    updatehandler(localId, udata, this);
                                    break;

                                case 0x14: // uniform scale sp
                                    udata.scale = new Vector3(block.Data, 0);

                                    udata.change = ObjectChangeType.primUS;
                                    updatehandler(localId, udata, this);
                                    break;

                                case 5: // scale and position sp
                                    udata.position = new Vector3(block.Data, 0);
                                    udata.scale = new Vector3(block.Data, 12);

                                    udata.change = ObjectChangeType.primPS;
                                    updatehandler(localId, udata, this);
                                    break;

                                case 0x15: //uniform scale and position
                                    udata.position = new Vector3(block.Data, 0);
                                    udata.scale = new Vector3(block.Data, 12);

                                    udata.change = ObjectChangeType.primPUS;
                                    updatehandler(localId, udata, this);
                                    break;

                                // now group related (bit 4)
                                case 9: //( 8 + 1 )group position
                                    udata.position = new Vector3(block.Data, 0);

                                    udata.change = ObjectChangeType.groupP;
                                    updatehandler(localId, udata, this);
                                    break;

                                case 0x0A: // (8 + 2) group rotation
                                    udata.rotation = new Quaternion(block.Data, 0, true);

                                    udata.change = ObjectChangeType.groupR;
                                    updatehandler(localId, udata, this);
                                    break;

                                case 0x0B: //( 8 + 2 + 1) group rotation and position
                                    udata.position = new Vector3(block.Data, 0);
                                    udata.rotation = new Quaternion(block.Data, 12, true);

                                    udata.change = ObjectChangeType.groupPR;
                                    updatehandler(localId, udata, this);
                                    break;

                                case 0x0C: // (8 + 4) group scale
                                    // only afects root prim and only sent by viewer editor object tab scaling
                                    // mouse edition only allows uniform scaling
                                    // SL MAY CHANGE THIS in viewers

                                    udata.scale = new Vector3(block.Data, 0);

                                    udata.change = ObjectChangeType.groupS;
                                    updatehandler(localId, udata, this);

                                    break;

                                case 0x0D: //(8 + 4 + 1) group scale and position
                                    // exception as above

                                    udata.position = new Vector3(block.Data, 0);
                                    udata.scale = new Vector3(block.Data, 12);

                                    udata.change = ObjectChangeType.groupPS;
                                    updatehandler(localId, udata, this);
                                    break;

                                case 0x1C: // (0x10 + 8 + 4 ) group scale UNIFORM
                                    udata.scale = new Vector3(block.Data, 0);

                                    udata.change = ObjectChangeType.groupUS;
                                    updatehandler(localId, udata, this);
                                    break;

                                case 0x1D: // (UNIFORM + GROUP + SCALE + POS)
                                    udata.position = new Vector3(block.Data, 0);
                                    udata.scale = new Vector3(block.Data, 12);

                                    udata.change = ObjectChangeType.groupPUS;
                                    updatehandler(localId, udata, this);
                                    break;

                                default:
                                    m_log.Debug("[CLIENT]: MultipleObjUpdate recieved an unknown packet type: " + (block.Type));
                                    break;
                            }
                        }

                    }
                }
            }
        }

        public void RequestMapLayer()
        {
            //should be getting the map layer from the grid server
            //send a layer covering the 800,800 - 1200,1200 area (should be covering the requested area)
            MapLayerReplyPacket mapReply = (MapLayerReplyPacket)PacketPool.Instance.GetPacket(PacketType.MapLayerReply);
            // TODO: don't create new blocks if recycling an old packet
            mapReply.AgentData.AgentID = m_agentId;
            mapReply.AgentData.Flags = 0;
            mapReply.LayerData = new MapLayerReplyPacket.LayerDataBlock[1];
            mapReply.LayerData[0] = new MapLayerReplyPacket.LayerDataBlock
            {
                Bottom = 0,
                Left = 0,
                Top = 30000,
                Right = 30000,
                ImageID = new UUID("00000000-0000-1111-9999-000000000006")
            };
            mapReply.Header.Zerocoded = true;
            OutPacket(mapReply, ThrottleOutPacketType.Land);
        }

        public static void RequestMapBlocksX(int minX, int minY, int maxX, int maxY)
        {
            /*
            IList simMapProfiles = m_gridServer.RequestMapBlocks(minX, minY, maxX, maxY);
            MapBlockReplyPacket mbReply = new MapBlockReplyPacket();
            mbReply.AgentData.AgentId = m_agentId;
            int len;
            if (simMapProfiles is null)
                len = 0;
            else
                len = simMapProfiles.Count;

            mbReply.Data = new MapBlockReplyPacket.DataBlock[len];
            int iii;
            for (iii = 0; iii < len; iii++)
            {
                Hashtable mp = (Hashtable)simMapProfiles[iii];
                mbReply.Data[iii] = new MapBlockReplyPacket.DataBlock();
                mbReply.Data[iii].Name = Util.UTF8.GetBytes((string)mp["name"]);
                mbReply.Data[iii].Access = System.Convert.ToByte(mp["access"]);
                mbReply.Data[iii].Agents = System.Convert.ToByte(mp["agents"]);
                mbReply.Data[iii].MapImageID = new UUID((string)mp["map-image-id"]);
                mbReply.Data[iii].RegionFlags = System.Convert.ToUInt32(mp["region-flags"]);
                mbReply.Data[iii].WaterHeight = System.Convert.ToByte(mp["water-height"]);
                mbReply.Data[iii].X = System.Convert.ToUInt16(mp["x"]);
                mbReply.Data[iii].Y = System.Convert.ToUInt16(mp["y"]);
            }
            this.OutPacket(mbReply, ThrottleOutPacketType.Land);
             */
        }

        /// <summary>
        /// Sets the throttles from values supplied by the client
        /// </summary>
        /// <param name="throttles"></param>
        public void SetChildAgentThrottle(byte[] throttles)
        {
            SetChildAgentThrottle(throttles, 1.0f);
        }

        public void SetChildAgentThrottle(byte[] throttles,float factor)
        {
            m_udpClient.SetThrottles(throttles, factor);
            OnUpdateThrottles?.Invoke();
        }

        /// <summary>
        /// Sets the throttles from values supplied caller
        /// </summary>
        /// <param name="throttles"></param>
        public void SetAgentThrottleSilent(int throttle, int setting)
        {
            m_udpClient.ForceThrottleSetting(throttle,setting);
        }

        public int GetAgentThrottleSilent(int throttle)
        {
            return m_udpClient.GetThrottleSetting(throttle);
        }

        /// <summary>
        /// Get the current throttles for this client as a packed byte array
        /// </summary>
        /// <param name="multiplier">Unused</param>
        /// <returns></returns>
        public byte[] GetThrottlesPacked(float multiplier)
        {
            return m_udpClient.GetThrottlesPacked(multiplier);
        }

        /// <summary>
        /// Cruft?
        /// </summary>
        public virtual void InPacket(object NewPack)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// This is the starting point for sending a simulator packet out to the client
        /// </summary>
        /// <param name="packet">Packet to send</param>
        /// <param name="throttlePacketType">Throttling category for the packet</param>
        protected void OutPacket(Packet packet, ThrottleOutPacketType throttlePacketType)
        {
            #region BinaryStats
            LLUDPServer.LogPacketHeader(false, m_circuitCode, 0, packet.Type, (ushort)packet.Length);
            #endregion BinaryStats

            OutPacket(packet, throttlePacketType, true, null);
        }

        /// <summary>
        /// This is the starting point for sending a simulator packet out to the client
        /// </summary>
        /// <param name="packet">Packet to send</param>
        /// <param name="throttlePacketType">Throttling category for the packet</param>
        /// <param name="doAutomaticSplitting">True to automatically split oversized
        /// packets (the default), or false to disable splitting if the calling code
        /// handles splitting manually</param>
        protected void OutPacket(Packet packet, ThrottleOutPacketType throttlePacketType, bool doAutomaticSplitting)
        {
            OutPacket(packet, throttlePacketType, doAutomaticSplitting, null);
        }

        /// <summary>
        /// This is the starting point for sending a simulator packet out to the client
        /// </summary>
        /// <param name="packet">Packet to send</param>
        /// <param name="throttlePacketType">Throttling category for the packet</param>
        /// <param name="doAutomaticSplitting">True to automatically split oversized
        /// packets (the default), or false to disable splitting if the calling code
        /// handles splitting manually</param>
        /// <param name="method">The method to be called in the event this packet is reliable
        /// and unacknowledged. The server will provide normal resend capability if you do not
        /// provide your own method.</param>
        protected void OutPacket(Packet packet, ThrottleOutPacketType throttlePacketType, bool doAutomaticSplitting, UnackedPacketMethod method)
        {
            if (m_outPacketsToDrop is not null)
            {
                if (m_outPacketsToDrop.Contains(packet.Type.ToString()))
                {
                    PacketPool.Instance.ReturnPacket(packet);
                    return;
                }
            }

            if (DebugPacketLevel > 0)
            {
                bool logPacket = true;

                if (DebugPacketLevel <= 255
                    && (packet.Type == PacketType.SimStats || packet.Type == PacketType.SimulatorViewerTimeMessage))
                    logPacket = false;

                if (DebugPacketLevel <= 200
                    && (packet.Type == PacketType.ImagePacket
                        || packet.Type == PacketType.ImageData
                        || packet.Type == PacketType.LayerData
                        || packet.Type == PacketType.CoarseLocationUpdate))
                    logPacket = false;

                if (DebugPacketLevel <= 100 && (packet.Type == PacketType.AvatarAnimation || packet.Type == PacketType.ViewerEffect))
                    logPacket = false;

                if (DebugPacketLevel <= 50
                    && (packet.Type == PacketType.ImprovedTerseObjectUpdate || packet.Type == PacketType.ObjectUpdate))
                    logPacket = false;

                if (DebugPacketLevel <= 25 && packet.Type == PacketType.ObjectPropertiesFamily)
                    logPacket = false;

                if (logPacket)
                    m_log.DebugFormat(
                        "[CLIENT]: PACKET OUT to   {0} ({1}) in {2} - {3}",
                        Name, SceneAgent.IsChildAgent ? "child" : "root ", m_scene.RegionInfo.RegionName, packet.Type);
            }

            m_udpServer.SendPacket(m_udpClient, packet, throttlePacketType, doAutomaticSplitting, method);
        }

        protected void HandleAutopilot(Object sender, string method, List<String> args)
        {
            if(OnAutoPilotGo is null)
                return;

            Utils.LongToUInts(m_scene.RegionInfo.RegionHandle, out uint regionX, out uint regionY);
            float locx = (float)(Convert.ToDouble(args[0]) - (double)regionX);
            float locy = (float)(Convert.ToDouble(args[1]) - (double)regionY);
            float locz = Convert.ToSingle(args[2]);

            OnAutoPilotGo?.Invoke(new Vector3(locx, locy, locz), false, true);
        }

        /// <summary>
        /// Entryway from the client to the simulator.  All UDP packets from the client will end up here
        /// </summary>
        /// <param name="Pack">OpenMetaverse.packet</param>
        public void ProcessInPacket(Packet packet)
        {
            if (m_inPacketsToDrop != null)
                if (m_inPacketsToDrop.Contains(packet.Type.ToString()))
                    return;

            if (DebugPacketLevel > 0)
            {
                bool logPacket = true;

                if (DebugPacketLevel <= 255 && packet.Type == PacketType.AgentUpdate)
                    logPacket = false;

                if (DebugPacketLevel <= 200 && packet.Type == PacketType.RequestImage)
                    logPacket = false;

                if (DebugPacketLevel <= 100 && (packet.Type == PacketType.ViewerEffect || packet.Type == PacketType.AgentAnimation))
                    logPacket = false;

                if (DebugPacketLevel <= 25 && packet.Type == PacketType.RequestObjectPropertiesFamily)
                    logPacket = false;

                if (logPacket)
                    m_log.DebugFormat(
                        "[CLIENT]: PACKET IN  from {0} ({1}) in {2} - {3}",
                        Name, SceneAgent.IsChildAgent ? "child" : "root ", Scene.Name, packet.Type);
            }

            if (!ProcessPacketMethod(packet))
                m_log.WarnFormat(
                    "[CLIENT]: Unhandled packet {0} from {1} ({2}) in {3}.  Ignoring.",
                    packet.Type, Name, SceneAgent.IsChildAgent ? "child" : "root ", Scene.Name);
        }

        private static PrimitiveBaseShape GetShapeFromAddPacket(ObjectAddPacket addPacket)
        {
            PrimitiveBaseShape shape = new()
            {
                PCode = addPacket.ObjectData.PCode,
                State = addPacket.ObjectData.State,
                LastAttachPoint = addPacket.ObjectData.State,
                PathBegin = addPacket.ObjectData.PathBegin,
                PathEnd = addPacket.ObjectData.PathEnd,
                PathScaleX = addPacket.ObjectData.PathScaleX,
                PathScaleY = addPacket.ObjectData.PathScaleY,
                PathShearX = addPacket.ObjectData.PathShearX,
                PathShearY = addPacket.ObjectData.PathShearY,
                PathSkew = addPacket.ObjectData.PathSkew,
                ProfileBegin = addPacket.ObjectData.ProfileBegin,
                ProfileEnd = addPacket.ObjectData.ProfileEnd,
                Scale = addPacket.ObjectData.Scale,
                PathCurve = addPacket.ObjectData.PathCurve,
                ProfileCurve = addPacket.ObjectData.ProfileCurve,
                ProfileHollow = addPacket.ObjectData.ProfileHollow,
                PathRadiusOffset = addPacket.ObjectData.PathRadiusOffset,
                PathRevolutions = addPacket.ObjectData.PathRevolutions,
                PathTaperX = addPacket.ObjectData.PathTaperX,
                PathTaperY = addPacket.ObjectData.PathTaperY,
                PathTwist = addPacket.ObjectData.PathTwist,
                PathTwistBegin = addPacket.ObjectData.PathTwistBegin
            };
            Primitive.TextureEntry ntex = new(new UUID("89556747-24cb-43ed-920b-47caed15465f"));
            shape.TextureEntry = ntex.GetBytes();
            //shape.Textures = ntex;
            return shape;
        }

        public ClientInfo GetClientInfo()
        {
            ClientInfo info = m_udpClient.GetClientInfo();

            info.proxyEP = null;
            info.agentcircuit ??= RequestClientInfo();

            return info;
        }

        public void SetClientInfo(ClientInfo info)
        {
            m_udpClient.SetClientInfo(info);
        }

        #region Media Parcel Members

        public void SendParcelMediaCommand(uint flags, ParcelMediaCommandEnum command, float time)
        {
            ParcelMediaCommandMessagePacket commandMessagePacket = new();
            commandMessagePacket.CommandBlock.Flags = flags;
            commandMessagePacket.CommandBlock.Command = (uint)command;
            commandMessagePacket.CommandBlock.Time = time;

            OutPacket(commandMessagePacket, ThrottleOutPacketType.Task);
        }

        public void SendParcelMediaUpdate(string mediaUrl, UUID mediaTextureID,
                                   byte autoScale, string mediaType, string mediaDesc, int mediaWidth, int mediaHeight,
                                   byte mediaLoop)
        {
            ParcelMediaUpdatePacket updatePacket = new();
            updatePacket.DataBlock.MediaURL = Util.StringToBytes256(mediaUrl);
            updatePacket.DataBlock.MediaID = mediaTextureID;
            updatePacket.DataBlock.MediaAutoScale = autoScale;

            updatePacket.DataBlockExtended.MediaType = Util.StringToBytes256(mediaType);
            updatePacket.DataBlockExtended.MediaDesc = Util.StringToBytes256(mediaDesc);
            updatePacket.DataBlockExtended.MediaWidth = mediaWidth;
            updatePacket.DataBlockExtended.MediaHeight = mediaHeight;
            updatePacket.DataBlockExtended.MediaLoop = mediaLoop;

            OutPacket(updatePacket, ThrottleOutPacketType.Task);
        }

        #endregion

        #region Camera

        public void SendSetFollowCamProperties(UUID objectID, SortedDictionary<int, float> parameters)
        {
            SetFollowCamPropertiesPacket packet = (SetFollowCamPropertiesPacket)PacketPool.Instance.GetPacket(PacketType.SetFollowCamProperties);
            packet.ObjectData.ObjectID = objectID;
            SetFollowCamPropertiesPacket.CameraPropertyBlock[] camPropBlock = new SetFollowCamPropertiesPacket.CameraPropertyBlock[parameters.Count];
            uint idx = 0;
            foreach (KeyValuePair<int, float> pair in parameters)
            {
                SetFollowCamPropertiesPacket.CameraPropertyBlock block = new()
                {
                    Type = pair.Key,
                    Value = pair.Value
                };

                camPropBlock[idx++] = block;
            }
            packet.CameraProperty = camPropBlock;
            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendClearFollowCamProperties(UUID objectID)
        {
            ClearFollowCamPropertiesPacket packet = (ClearFollowCamPropertiesPacket)PacketPool.Instance.GetPacket(PacketType.ClearFollowCamProperties);
            packet.ObjectData.ObjectID = objectID;
            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        #endregion

        public void SetClientOption(string option, string value)
        {
            switch (option)
            {
                default:
                    break;
            }
        }

        public string GetClientOption(string option)
        {
            switch (option)
            {
                default:
                    break;
            }
            return string.Empty;
        }

        #region IClientCore

        private readonly Dictionary<Type, object> m_clientInterfaces = new();

        /// <summary>
        /// Register an interface on this client, should only be called in the constructor.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="iface"></param>
        protected void RegisterInterface<T>(T iface)
        {
            lock (m_clientInterfaces)
            {
                if (!m_clientInterfaces.ContainsKey(typeof(T)))
                {
                    m_clientInterfaces.Add(typeof(T), iface);
                }
            }
        }

        public bool TryGet<T>(out T iface)
        {
            if(m_clientInterfaces.TryGetValue(typeof(T), out object o))
            {
                iface = (T)o;
                return true;
            }
            iface = default;
            return false;
        }

        public T Get<T>()
        {
            return (T)m_clientInterfaces[typeof(T)];
        }

        public void Disconnect(string reason)
        {
            Kick(reason);
            Thread.Sleep(1000);
            Disconnect();
        }

        public void Disconnect()
        {
            Close();
        }

        #endregion

        public void RefreshGroupMembership()
        {
            lock(m_groupPowers)
            {
                GroupMembershipData activeMembership = null;
                if (m_GroupsModule != null)
                {
                    GroupMembershipData[] GroupMembership =
                        m_GroupsModule.GetMembershipData(m_agentId);

                    m_groupPowers.Clear();

                    if (GroupMembership != null)
                    {
                        for (int i = 0; i < GroupMembership.Length; i++)
                        {
                            m_groupPowers[GroupMembership[i].GroupID] = GroupMembership[i].GroupPowers;
                        }
                    }

                    activeMembership = m_GroupsModule.GetActiveMembershipData(m_agentId);
                    if(activeMembership != null)
                    {
                        if(!m_groupPowers.ContainsKey(activeMembership.GroupID))
                            activeMembership = null;
                        else
                        {
                            m_activeGroupID = activeMembership.GroupID;
                            m_activeGroupName = activeMembership.GroupName;
                            m_activeGroupPowers = ActiveGroupPowers;
                        }
                    }
                }

                if(activeMembership is null)
                {
                    m_activeGroupID = UUID.Zero;
                    m_activeGroupName = "";
                    m_activeGroupPowers = 0;
                }
            }
        }

        public void UpdateGroupMembership(GroupMembershipData[] data)
        {
            lock(m_groupPowers)
            {
                m_groupPowers.Clear();

                if (data != null)
                {
                    for (int i = 0; i < data.Length; i++)
                        m_groupPowers[data[i].GroupID] = data[i].GroupPowers;
                }
            }
        }

        public void GroupMembershipRemove(UUID GroupID)
        {
            lock(m_groupPowers)
            {
                if(m_groupPowers.ContainsKey(GroupID))
                    m_groupPowers.Remove(GroupID);
            }
        }

        public void GroupMembershipAddReplace(UUID GroupID,ulong GroupPowers)
        {
            lock(m_groupPowers)
            {
                m_groupPowers[GroupID] = GroupPowers;
            }
        }

        public string Report()
        {
            return m_udpClient.GetStats();
        }

        public string XReport(string uptime, string version)
        {
            return String.Empty;
        }

        public OSDMap OReport(string uptime, string version)
        {
            return new OSDMap();
        }

        /// <summary>
        /// Make an asset request to the asset service in response to a client request.
        /// </summary>
        /// <param name="transferRequest"></param>
        /// <param name="taskID"></param>
        protected void MakeAssetRequest(TransferRequestPacket transferRequest, UUID taskID)
        {
            int sourceType = transferRequest.TransferInfo.SourceType;
            var requestID = sourceType switch
            {
                (int)SourceType.Asset => new UUID(transferRequest.TransferInfo.Params, 0),
                (int)SourceType.SimInventoryItem => new UUID(transferRequest.TransferInfo.Params, 80),
                (int)SourceType.SimEstate => taskID,
                _ => UUID.Zero,
            };
            //m_log.DebugFormat(
            //    "[LLCLIENTVIEW]: Received transfer request for {0} in {1} type {2} by {3}",
            //    requestID, taskID, (SourceType)sourceType, Name);

            //Note, the bool returned from the below function is useless since it is always false.
            m_assetService.Get(requestID.ToString(), transferRequest, AssetReceived);

        }

        /// <summary>
        /// When we get a reply back from the asset service in response to a client request, send back the data.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="sender"></param>
        /// <param name="asset"></param>
        protected void AssetReceived(string id, Object sender, AssetBase asset)
        {
            TransferRequestPacket transferRequest = (TransferRequestPacket)sender;

            UUID requestID = UUID.Zero;
            byte source = (byte)SourceType.Asset;

            if (asset is null)
            {
                // Try the user's asset server
                IInventoryAccessModule inventoryAccessModule = Scene.RequestModuleInterface<IInventoryAccessModule>();
                if (inventoryAccessModule.IsForeignUser(m_agentId, out string assetServerURL) && !string.IsNullOrEmpty(assetServerURL))
                {
                    if (!assetServerURL.EndsWith('/') && !assetServerURL.EndsWith('='))
                        assetServerURL += "/";

                    //m_log.DebugFormat("[LLCLIENTVIEW]: asset {0} not found in local storage. Trying user's storage.", assetServerURL + id);
                    asset = m_scene.AssetService.Get(assetServerURL + id);
                }

                if (asset is null)
                {
                    SendAssetNotFound( new AssetRequestToClient()
                    {
                        AssetInf = null,
                        AssetRequestSource = source,
                        IsTextureRequest = false,
                        NumPackets = 0,
                        Params = transferRequest.TransferInfo.Params,
                        RequestAssetID = requestID,
                        TransferRequestID = transferRequest.TransferInfo.TransferID
                    });
                    return;
                }
            }

            if (transferRequest.TransferInfo.SourceType == (int)SourceType.Asset)
            {
                requestID = new UUID(transferRequest.TransferInfo.Params, 0);
            }
            else if (transferRequest.TransferInfo.SourceType == (int)SourceType.SimInventoryItem)
            {
                requestID = new UUID(transferRequest.TransferInfo.Params, 80);
                source = (byte)SourceType.SimInventoryItem;
                //m_log.Debug("asset request " + requestID);
            }

            // Scripts cannot be retrieved by direct request
            if (transferRequest.TransferInfo.SourceType == (int)SourceType.Asset && asset.Type == (sbyte)AssetType.LSLText)
                return;

            // The asset is known to exist and is in our cache, so add it to the AssetRequests list
            SendAsset(new AssetRequestToClient()
            {
                AssetInf = asset,
                AssetRequestSource = source,
                IsTextureRequest = false,
                NumPackets = CalculateNumPackets(asset.Data),
                Params = transferRequest.TransferInfo.Params,
                RequestAssetID = requestID,
                TransferRequestID = transferRequest.TransferInfo.TransferID
            });
        }

        /// <summary>
        /// Calculate the number of packets required to send the asset to the client.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private static int CalculateNumPackets(byte[] data)
        {
            if (data is null || data.Length == 0)
                return 0;
            if (data.LongLength > MaxTransferBytesPerPacket)
            {
                // over max number of bytes so split up file
                long restData = data.LongLength - MaxTransferBytesPerPacket;
                int restPackets = (int)((restData + MaxTransferBytesPerPacket - 1) / MaxTransferBytesPerPacket);
                return restPackets + 1;
            }
            else
                return 1;
        }

        public void SendRebakeAvatarTextures(UUID textureID)
        {
            RebakeAvatarTexturesPacket pack =
                (RebakeAvatarTexturesPacket)PacketPool.Instance.GetPacket(PacketType.RebakeAvatarTextures);

            pack.TextureData = new RebakeAvatarTexturesPacket.TextureDataBlock { TextureID = textureID };
            OutPacket(pack, ThrottleOutPacketType.Task);
        }

        public struct PacketProcessor
        {
            /// <summary>
            /// Packet handling method.
            /// </summary>
            public PacketMethod method;

            /// <summary>
            /// Should this packet be handled asynchronously?
            /// </summary>
            public bool Async;

        }

        public void SendAvatarInterestsReply(UUID avatarID, uint wantMask, string wantText, uint skillsMask, string skillsText, string languages)
        {
            AvatarInterestsReplyPacket packet = (AvatarInterestsReplyPacket)PacketPool.Instance.GetPacket(PacketType.AvatarInterestsReply);

            packet.AgentData.AgentID = m_agentId;
            packet.AgentData.AvatarID = avatarID;

            packet.PropertiesData.WantToMask = wantMask;
            packet.PropertiesData.WantToText = Utils.StringToBytes(wantText);
            packet.PropertiesData.SkillsMask = skillsMask;
            packet.PropertiesData.SkillsText = Utils.StringToBytes(skillsText);
            packet.PropertiesData.LanguagesText = Utils.StringToBytes(languages);

            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendChangeUserRights(UUID agentID, UUID friendID, int rights)
        {
            ChangeUserRightsPacket packet = (ChangeUserRightsPacket)PacketPool.Instance.GetPacket(PacketType.ChangeUserRights);

            packet.AgentData.AgentID = agentID;

            packet.Rights = new ChangeUserRightsPacket.RightsBlock[]
            {
                new ChangeUserRightsPacket.RightsBlock
                {
                    AgentRelated = friendID,
                    RelatedRights = rights
                }
            };
            OutPacket(packet, ThrottleOutPacketType.Task);
        }

        public void SendTextBoxRequest(string message, int chatChannel, string objectname, UUID ownerID, string ownerFirstName, string ownerLastName, UUID objectId)
        {
            ScriptDialogPacket dialog = (ScriptDialogPacket)PacketPool.Instance.GetPacket(PacketType.ScriptDialog);
            dialog.Data.ObjectID = objectId;
            dialog.Data.ChatChannel = chatChannel;
            dialog.Data.ImageID = UUID.Zero;
            dialog.Data.ObjectName = Util.StringToBytes256(objectname);
            // this is the username of the *owner*
            dialog.Data.FirstName = Util.StringToBytes256(ownerFirstName);
            dialog.Data.LastName = Util.StringToBytes256(ownerLastName);
            dialog.Data.Message =  Util.StringToBytes(message,512);

            dialog.Buttons = new ScriptDialogPacket.ButtonsBlock[]
            {
                new ScriptDialogPacket.ButtonsBlock
                {
                    ButtonLabel = Util.StringToBytes256("!!llTextBox!!")
                }
            };

            dialog.OwnerData = new ScriptDialogPacket.OwnerDataBlock[]
            {
                new ScriptDialogPacket.OwnerDataBlock
                {
                    OwnerID = ownerID
                }
            };

            OutPacket(dialog, ThrottleOutPacketType.Task);
        }

        public void SendAgentTerseUpdate(ISceneEntity p)
        {
            if (p is ScenePresence sp)
            {
                UDPPacketBuffer buf = m_udpServer.GetNewUDPBuffer(m_udpClient.RemoteEndPoint);

                //setup header and regioninfo block
                Buffer.BlockCopy(terseUpdateHeader, 0, buf.Data, 0, 7);
                unsafe
                {
                    fixed(byte* bdata = &buf.Data[0])
                    {
                        Utils.UInt64ToBytes(m_scene.RegionInfo.RegionHandle, bdata + 7);
                        Utils.UInt16ToBytes(Utils.FloatZeroOneToushort(m_scene.TimeDilation), bdata + 15);
                        bdata[17] = 1;
                        byte* data = bdata + 18;
                        CreateAvatartImprovedTerseBlock(sp, ref data);
                        buf.DataLength = (int)(data - bdata);
                    }
                }
                m_udpServer.SendUDPPacket(m_udpClient, buf, ThrottleOutPacketType.Task, null, true);
            }
        }

        public void SendPlacesReply(UUID queryID, UUID transactionID, PlacesReplyData[] data)
        {
            PlacesReplyPacket reply = null;
            PlacesReplyPacket.QueryDataBlock[] dataBlocks = Array.Empty<PlacesReplyPacket.QueryDataBlock>();

            for (int i = 0 ; i < data.Length ; i++)
            {
                PlacesReplyPacket.QueryDataBlock block = new()
                {
                    OwnerID = data[i].OwnerID,
                    Name = Util.StringToBytes256(data[i].Name),
                    Desc = Util.StringToBytes1024(data[i].Desc),
                    ActualArea = data[i].ActualArea,
                    BillableArea = data[i].BillableArea,
                    Flags = data[i].Flags,
                    GlobalX = data[i].GlobalX,
                    GlobalY = data[i].GlobalY,
                    GlobalZ = data[i].GlobalZ,
                    SimName = Util.StringToBytes256(data[i].SimName),
                    SnapshotID = data[i].SnapshotID,
                    Dwell = data[i].Dwell,
                    Price = data[i].Price
                };

                if (reply is not null && reply.Length + block.Length > 1400)
                {
                    OutPacket(reply, ThrottleOutPacketType.Task);

                    reply = null;
                    dataBlocks = Array.Empty<PlacesReplyPacket.QueryDataBlock>();
                }

                if (reply is null)
                {
                    reply = (PlacesReplyPacket)PacketPool.Instance.GetPacket(PacketType.PlacesReply);
                    reply.AgentData.AgentID = m_agentId;
                    reply.AgentData.QueryID = queryID;

                    reply.TransactionData.TransactionID = transactionID;

                    reply.QueryData = dataBlocks;
                }

                Array.Resize(ref dataBlocks, dataBlocks.Length + 1);
                dataBlocks[^1] = block;
                reply.QueryData = dataBlocks;
            }
            if (reply != null)
                OutPacket(reply, ThrottleOutPacketType.Task);
        }

        public void SendRemoveInventoryItems(UUID[] items)
        {
            IEventQueue eq = Scene.RequestModuleInterface<IEventQueue>();
            if (eq is null)
            {
                m_log.DebugFormat("[LLCLIENT]: Null event queue");
                return;
            }

            osUTF8 sb = eq.StartEvent("RemoveInventoryItem");

            LLSDxmlEncode2.AddArrayAndMap("AgentData", sb);
                LLSDxmlEncode2.AddElem("AgentID", m_agentId, sb);
                LLSDxmlEncode2.AddElem("SessionID", SessionId, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            LLSDxmlEncode2.AddArray("InventoryData", sb);
            foreach (UUID item in items)
            {
                LLSDxmlEncode2.AddMap(sb);
                    LLSDxmlEncode2.AddElem("ItemID",item, sb);
                LLSDxmlEncode2.AddEndMap(sb);
            }
            LLSDxmlEncode2.AddEndArray(sb);

            eq.Enqueue(eq.EndEventToBytes(sb), m_agentId);
        }

        public void SendRemoveInventoryFolders(UUID[] folders)
        {
            IEventQueue eq = Scene.RequestModuleInterface<IEventQueue>();

            if (eq is null)
            {
                m_log.DebugFormat("[LLCLIENT]: Null event queue");
                return;
            }

            osUTF8 sb = eq.StartEvent("RemoveInventoryFolder");

            LLSDxmlEncode2.AddArrayAndMap("AgentData", sb);
                LLSDxmlEncode2.AddElem("AgentID", m_agentId, sb);
                LLSDxmlEncode2.AddElem("SessionID", SessionId, sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            LLSDxmlEncode2.AddArray("FolderData", sb);
            foreach (UUID folder in folders)
            {
                LLSDxmlEncode2.AddMap(sb);
                    LLSDxmlEncode2.AddElem("FolderID", folder, sb);
                LLSDxmlEncode2.AddEndMap(sb);
            }
            LLSDxmlEncode2.AddEndArray(sb);

            eq.Enqueue(eq.EndEventToBytes(sb), m_agentId);
        }

        public void SendBulkUpdateInventory(InventoryFolderBase[] folders, InventoryItemBase[] items)
        {
            IEventQueue eq = Scene.RequestModuleInterface<IEventQueue>();

            if (eq is null)
            {
                m_log.DebugFormat("[LLCLIENT]: Null event queue");
                return;
            }

            osUTF8 sb = eq.StartEvent("BulkUpdateInventory");

            LLSDxmlEncode2.AddArrayAndMap("AgentData", sb);
            LLSDxmlEncode2.AddElem("AgentID", m_agentId, sb);
            LLSDxmlEncode2.AddElem("TransactionID", UUID.Random(), sb);
            LLSDxmlEncode2.AddEndMapAndArray(sb);

            if(folders.Length == 0)
            {
                LLSDxmlEncode2.AddEmptyArray("FolderData", sb);
            }
            else
            { 
                LLSDxmlEncode2.AddArray("FolderData", sb);
                foreach (InventoryFolderBase folder in folders)
                {
                    LLSDxmlEncode2.AddMap(sb);
                    LLSDxmlEncode2.AddElem("FolderID", folder.ID, sb);
                    LLSDxmlEncode2.AddElem("ParentID", folder.ParentID, sb);
                    LLSDxmlEncode2.AddElem("Type", (int)folder.Type, sb);
                    LLSDxmlEncode2.AddElem("Name", folder.Name, sb);
                    LLSDxmlEncode2.AddEndMap(sb);
                }
                LLSDxmlEncode2.AddEndArray(sb);
            }

            if(items.Length == 0)
            {
                LLSDxmlEncode2.AddEmptyArray("ItemData", sb);
            }
            else
            {
                LLSDxmlEncode2.AddArray("ItemData", sb);
                foreach (InventoryItemBase item in items)
                {
                    LLSDxmlEncode2.AddMap(sb);
                    LLSDxmlEncode2.AddElem("ItemID", item.ID, sb);
                    LLSDxmlEncode2.AddElem("CallbackID", (uint)0, sb);
                    LLSDxmlEncode2.AddElem("FolderID", item.Folder, sb);
                    LLSDxmlEncode2.AddElem("CreatorID", item.CreatorIdAsUuid, sb);
                    LLSDxmlEncode2.AddElem("OwnerID", item.Owner, sb);
                    LLSDxmlEncode2.AddElem("GroupID", item.GroupID, sb);
                    LLSDxmlEncode2.AddElem("BaseMask", item.BasePermissions, sb);
                    LLSDxmlEncode2.AddElem("OwnerMask", item.CurrentPermissions, sb);
                    LLSDxmlEncode2.AddElem("GroupMask", item.GroupPermissions, sb);
                    LLSDxmlEncode2.AddElem("EveryoneMask", item.EveryOnePermissions, sb);
                    LLSDxmlEncode2.AddElem("NextOwnerMask", item.NextPermissions, sb);
                    LLSDxmlEncode2.AddElem("GroupOwned", item.GroupOwned, sb);
                    LLSDxmlEncode2.AddElem("AssetID", item.AssetID, sb);
                    LLSDxmlEncode2.AddElem("Type", item.AssetType, sb);
                    LLSDxmlEncode2.AddElem("InvType", item.InvType, sb);
                    LLSDxmlEncode2.AddElem("Flags", item.Flags, sb);
                    LLSDxmlEncode2.AddElem("SaleType", item.SaleType, sb);
                    LLSDxmlEncode2.AddElem("SalePrice", item.SalePrice, sb);
                    LLSDxmlEncode2.AddElem("Name", item.Name, sb);
                    LLSDxmlEncode2.AddElem("Description", item.Description, sb);
                    LLSDxmlEncode2.AddElem("CreationDate", item.CreationDate, sb);
                    LLSDxmlEncode2.AddElem("CRC", 
                            Helpers.InventoryCRC(1000, 0, (sbyte)item.InvType,
                            (sbyte)item.AssetType, item.AssetID,
                            item.GroupID, 100,
                            item.Owner, item.CreatorIdAsUuid,
                            item.ID, item.Folder,
                            (uint)PermissionMask.All, 1, (uint)PermissionMask.All, (uint)PermissionMask.All,
                            (uint)PermissionMask.All),
                            sb);
                    LLSDxmlEncode2.AddEndMap(sb);
                }
                LLSDxmlEncode2.AddEndArray(sb);
            }

            eq.Enqueue(eq.EndEventToBytes(sb), m_agentId);
        }

        private HashSet<string> m_outPacketsToDrop;

        public bool AddOutPacketToDropSet(string packetName)
        {
            m_outPacketsToDrop ??= new HashSet<string>();

            return m_outPacketsToDrop.Add(packetName);
        }

        public bool RemoveOutPacketFromDropSet(string packetName)
        {
            if (m_outPacketsToDrop is null)
                return false;

            return m_outPacketsToDrop.Remove(packetName);
        }

        public HashSet<string> GetOutPacketDropSet()
        {
            return new HashSet<string>(m_outPacketsToDrop);
        }

        private HashSet<string> m_inPacketsToDrop;

        public bool AddInPacketToDropSet(string packetName)
        {
            m_inPacketsToDrop ??= new HashSet<string>();

            return m_inPacketsToDrop.Add(packetName);
        }

        public bool RemoveInPacketFromDropSet(string packetName)
        {
            if (m_inPacketsToDrop is null)
                return false;

            return m_inPacketsToDrop.Remove(packetName);
        }

        public HashSet<string> GetInPacketDropSet()
        {
            return new HashSet<string>(m_inPacketsToDrop);
        }

        public uint GetViewerCaps()
        {
            m_SupportObjectAnimations = false;
            uint ret;
            if(m_supportViewerCache)
                ret = m_viewerHandShakeFlags;
            else
                ret = (m_viewerHandShakeFlags & 4) | 2; // disable probes

            if (m_scene.CapsModule != null)
            {
                Caps cap = m_scene.CapsModule.GetCapsForUser(CircuitCode);
                if(cap != null)
                {
                    if((cap.Flags & Caps.CapsFlags.SentSeeds) != 0)
                        ret |= 0x1000;
                    if ((cap.Flags & Caps.CapsFlags.ObjectAnim) != 0)
                    {
                        m_SupportObjectAnimations = true;
                        ret |= 0x2000;
                    }
                    if ((cap.Flags & Caps.CapsFlags.WLEnv) != 0)
                        ret |= 0x4000;
                    if ((cap.Flags & Caps.CapsFlags.AdvEnv) != 0)
                        ret |= 0x8000;
                }
            }

            return ret;
        }
    }
}
