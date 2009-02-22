using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
using MXP;
using MXP.Messages;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using Packet=OpenMetaverse.Packets.Packet;

namespace OpenSim.Client.MXP.ClientStack
{
    class MXPClientView : IClientAPI, IClientCore
    {
        internal static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Session mxpSession;
        private readonly UUID mxpSessionID;
        private readonly IScene mxpHostBubble;
        private readonly string mxpUsername;

        private int debugLevel;

        public MXPClientView(Session mxpSession, UUID mxpSessionID, IScene mxpHostBubble, string mxpUsername)
        {
            this.mxpSession = mxpSession;
            this.mxpUsername = mxpUsername;
            this.mxpHostBubble = mxpHostBubble;
            this.mxpSessionID = mxpSessionID;
        }

        public Session Session
        {
            get { return mxpSession; }
        }

        public bool ProcessMXPPacket(Message msg)
        {
            if (debugLevel > 0)
                m_log.Warn("[MXP] Got Action/Command Packet: " + msg);

            return false;
        }

        #region IClientAPI

        public Vector3 StartPos
        {
            get { return new Vector3(128f, 128f, 128f); }
            set {  } // TODO: Implement Me
        }

        public UUID AgentId
        {
            get { return mxpSessionID; }
        }

        public UUID SessionId
        {
            get { return mxpSessionID; }
        }

        public UUID SecureSessionId
        {
            get { return mxpSessionID; }
        }

        public UUID ActiveGroupId
        {
            get { return UUID.Zero; }
        }

        public string ActiveGroupName
        {
            get { return ""; }
        }

        public ulong ActiveGroupPowers
        {
            get { return 0; }
        }

        public ulong GetGroupPowers(UUID groupID)
        {
            return 0;
        }

        public bool IsGroupMember(UUID GroupID)
        {
            return false;
        }

        public string FirstName
        {
            get { return mxpUsername; }
        }

        public string LastName
        {
            get { return "@mxp://" + Session.RemoteEndPoint.Address; }
        }

        public IScene Scene
        {
            get { return mxpHostBubble; }
        }

        public int NextAnimationSequenceNumber
        {
            get { return 0; }
        }

        public string Name
        {
            get { return FirstName; }
        }

        public bool IsActive
        {
            get { return Session.SessionState == SessionState.Connected; }
            set
            {
                if (!value)
                    Stop();
            }
        }

        // Do we need this?
        public bool SendLogoutPacketWhenClosing
        {
            set { }
        }

        public uint CircuitCode
        {
            get { return mxpSessionID.CRC(); }
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
        public event GenericCall2 OnCompleteMovementToRegion;
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
        public event ObjectSelect OnDeGrabObject;
        public event MoveObject OnGrabUpdate;
        public event UpdateShape OnUpdatePrimShape;
        public event ObjectExtraParams OnUpdateExtraParams;
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
        public event UpdatePrimGroupRotation OnUpdatePrimGroupMouseRotation;
        public event UpdateVector OnUpdatePrimScale;
        public event UpdateVector OnUpdatePrimGroupScale;
        public event StatusChange OnChildAgentStatus;
        public event GenericCall2 OnStopMovement;
        public event Action<UUID> OnRemoveAvatar;
        public event ObjectPermissions OnObjectPermissions;
        public event CreateNewInventoryItem OnCreateNewInventoryItem;
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

        public void SetDebugPacketLevel(int newDebug)
        {
            debugLevel = newDebug;
        }

        public void InPacket(object NewPack)
        {
            //throw new System.NotImplementedException();
        }

        public void ProcessInPacket(Packet NewPack)
        {
            //throw new System.NotImplementedException();
        }

        public void Close(bool ShutdownCircuit)
        {
            m_log.Info("[MXP ClientStack] Close Called with SC=" + ShutdownCircuit);

            // Tell the client to go
            SendLogoutPacket();

            // Let MXPPacketServer clean it up
            if (Session.SessionState != SessionState.Disconnected)
            {
                Session.SetStateDisconnected();
            }

            // Handle OpenSim cleanup
            if (ShutdownCircuit)
            {
                if (OnConnectionClosed != null)
                    OnConnectionClosed(this);
            }
            else
            {
                Scene.RemoveClient(AgentId);
            }
        }

        public void Kick(string message)
        {
            Close(false);
        }

        public void Start()
        {
            // We dont do this
        }

        public void Stop()
        {
            // Nor this
        }

        public void SendWearables(AvatarWearable[] wearables, int serial)
        {
            // Need to translate to MXP somehow
        }

        public void SendAppearance(UUID agentID, byte[] visualParams, byte[] textureEntry)
        {
            // Need to translate to MXP somehow
        }

        public void SendStartPingCheck(byte seq)
        {
            // Need to translate to MXP somehow
        }

        public void SendKillObject(ulong regionHandle, uint localID)
        {
            DisappearanceEventMessage de = new DisappearanceEventMessage();
            de.ObjectIndex = localID;

            Session.Send(de);
        }

        public void SendAnimations(UUID[] animID, int[] seqs, UUID sourceAgentId, UUID[] objectIDs)
        {
            // Need to translate to MXP somehow
        }

        public void SendRegionHandshake(RegionInfo regionInfo, RegionHandshakeArgs args)
        {
            // Need to translate to MXP somehow
        }

        public void SendChatMessage(string message, byte type, Vector3 fromPos, string fromName, UUID fromAgentID, byte source, byte audible)
        {
            ActionEventMessage chatActionEvent = new ActionEventMessage();
            chatActionEvent.ActionFragment.ActionName = "Chat";
            chatActionEvent.ActionFragment.SourceObjectId = fromAgentID.Guid;
            chatActionEvent.ActionFragment.ObservationRadius = 180.0f;
            chatActionEvent.ActionFragment.ActionPayloadDialect = "TEXT";
            chatActionEvent.SetPayloadData(Encoding.UTF8.GetBytes(message));
            chatActionEvent.ActionFragment.ActionPayloadLength = (uint)chatActionEvent.GetPayloadData().Length;

            Session.Send(chatActionEvent);
        }

        public void SendInstantMessage(UUID fromAgent, string message, UUID toAgent, string fromName, byte dialog, uint timeStamp)
        {
            // Need to translate to MXP somehow
        }

        public void SendInstantMessage(UUID fromAgent, string message, UUID toAgent, string fromName, byte dialog, uint timeStamp, UUID transactionID, bool fromGroup, byte[] binaryBucket)
        {
            // Need to translate to MXP somehow
        }

        public void SendGenericMessage(string method, List<string> message)
        {
            // Need to translate to MXP somehow
        }

        public void SendLayerData(float[] map)
        {
            // Need to translate to MXP somehow
        }

        public void SendLayerData(int px, int py, float[] map)
        {
            // Need to translate to MXP somehow
        }

        public void SendWindData(Vector2[] windSpeeds)
        {
            // Need to translate to MXP somehow
        }

        public void MoveAgentIntoRegion(RegionInfo regInfo, Vector3 pos, Vector3 look)
        {
            //throw new System.NotImplementedException();
        }

        public void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourExternalEndPoint)
        {
            //throw new System.NotImplementedException();
        }

        public AgentCircuitData RequestClientInfo()
        {
            AgentCircuitData clientinfo = new AgentCircuitData();
            clientinfo.AgentID = AgentId;
            clientinfo.Appearance = new AvatarAppearance();
            clientinfo.BaseFolder = UUID.Zero;
            clientinfo.CapsPath = "";
            clientinfo.child = false;
            clientinfo.ChildrenCapSeeds = new Dictionary<ulong, string>();
            clientinfo.circuitcode = CircuitCode;
            clientinfo.firstname = FirstName;
            clientinfo.InventoryFolder = UUID.Zero;
            clientinfo.lastname = LastName;
            clientinfo.SecureSessionID = SecureSessionId;
            clientinfo.SessionID = SessionId;
            clientinfo.startpos = StartPos;

            return clientinfo;
        }

        public void CrossRegion(ulong newRegionHandle, Vector3 pos, Vector3 lookAt, IPEndPoint newRegionExternalEndPoint, string capsURL)
        {
            // TODO: We'll want to get this one working.
            // Need to translate to MXP somehow
        }

        public void SendMapBlock(List<MapBlockData> mapBlocks, uint flag)
        {
            // Need to translate to MXP somehow
        }

        public void SendLocalTeleport(Vector3 position, Vector3 lookAt, uint flags)
        {
            //throw new System.NotImplementedException();
        }

        public void SendRegionTeleport(ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint, uint locationID, uint flags, string capsURL)
        {
            // Need to translate to MXP somehow
        }

        public void SendTeleportFailed(string reason)
        {
            // Need to translate to MXP somehow
        }

        public void SendTeleportLocationStart()
        {
            // Need to translate to MXP somehow
        }

        public void SendMoneyBalance(UUID transaction, bool success, byte[] description, int balance)
        {
            // Need to translate to MXP somehow
        }

        public void SendPayPrice(UUID objectID, int[] payPrice)
        {
            // Need to translate to MXP somehow
        }

        public void SendAvatarData(ulong regionHandle, string firstName, string lastName, string grouptitle, UUID avatarID, uint avatarLocalID, Vector3 Pos, byte[] textureEntry, uint parentID, Quaternion rotation)
        {
            // TODO: This needs handling - to display other avatars
        }

        public void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, Vector3 position, Vector3 velocity, Quaternion rotation)
        {
            // TODO: This probably needs handling - update other avatar positions
        }

        public void SendCoarseLocationUpdate(List<Vector3> CoarseLocations)
        {
            // Minimap function, not used.
        }

        public void AttachObject(uint localID, Quaternion rotation, byte attachPoint, UUID ownerID)
        {
            // Need to translate to MXP somehow
        }

        public void SetChildAgentThrottle(byte[] throttle)
        {
            // Need to translate to MXP somehow
        }

        public void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape, Vector3 pos, Vector3 vel, Vector3 acc, Quaternion rotation, Vector3 rvel, uint flags, UUID objectID, UUID ownerID, string text, byte[] color, uint parentID, byte[] particleSystem, byte clickAction, byte material, byte[] textureanim, bool attachment, uint AttachPoint, UUID AssetId, UUID SoundId, double SoundVolume, byte SoundFlags, double SoundRadius)
        {
            MXPSendPrimitive(localID, ownerID, acc, rvel, primShape, pos, objectID, vel, rotation);
        }

        private void MXPSendPrimitive(uint localID, UUID ownerID, Vector3 acc, Vector3 rvel, PrimitiveBaseShape primShape, Vector3 pos, UUID objectID, Vector3 vel, Quaternion rotation)
        {
            PerceptionEventMessage pe = new PerceptionEventMessage();

            pe.ObjectFragment.ObjectIndex = localID;
            pe.ObjectFragment.ObjectName = "Object";
            pe.ObjectFragment.OwnerId = ownerID.Guid;
            pe.ObjectFragment.TypeId = Guid.Empty;

            pe.ObjectFragment.Acceleration = new[] { acc.X, acc.Y, acc.Z };
            pe.ObjectFragment.AngularAcceleration = new float[4];
            pe.ObjectFragment.AngularVelocity = new[] { rvel.X, rvel.Y, rvel.Z, 0.0f };
            pe.ObjectFragment.BoundingSphereRadius = primShape.Scale.Length()/2.0f;
            pe.ObjectFragment.Location = new[] { pos.X, pos.Y, pos.Z };
            pe.ObjectFragment.Mass = 1.0f;
            pe.ObjectFragment.ObjectId = objectID.Guid;
            pe.ObjectFragment.Orientation = new[] {rotation.X, rotation.Y, rotation.Z, rotation.W};
            pe.ObjectFragment.ParentObjectId = Guid.Empty;
            pe.ObjectFragment.Velocity = new[] { vel.X, vel.Y, vel.Z };

            pe.ObjectFragment.StatePayloadDialect = "";
            pe.ObjectFragment.StatePayloadLength = 0;
            pe.ObjectFragment.SetStatePayloadData(new byte[0]);

            Session.Send(pe);
        }

        public void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape, Vector3 pos, Vector3 vel, Vector3 acc, Quaternion rotation, Vector3 rvel, uint flags, UUID objectID, UUID ownerID, string text, byte[] color, uint parentID, byte[] particleSystem, byte clickAction, byte material)
        {
            MXPSendPrimitive(localID, ownerID, acc, rvel, primShape, pos, objectID, vel, rotation);
        }

        public void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 rotationalvelocity, byte state, UUID AssetId, UUID owner, int attachPoint)
        {
            MovementEventMessage me = new MovementEventMessage();
            me.ObjectIndex = localID;
            me.Location = new[] {position.X, position.Y, position.Z};
            me.Orientation = new[] {rotation.X, rotation.Y, rotation.Z, rotation.W};

            Session.Send(me);
        }

        public void SendInventoryFolderDetails(UUID ownerID, UUID folderID, List<InventoryItemBase> items, List<InventoryFolderBase> folders, bool fetchFolders, bool fetchItems)
        {
            // Need to translate to MXP somehow
        }

        public void SendInventoryItemDetails(UUID ownerID, InventoryItemBase item)
        {
            // Need to translate to MXP somehow
        }

        public void SendInventoryItemCreateUpdate(InventoryItemBase Item, uint callbackId)
        {
            // Need to translate to MXP somehow
        }

        public void SendRemoveInventoryItem(UUID itemID)
        {
            // Need to translate to MXP somehow
        }

        public void SendTakeControls(int controls, bool passToAgent, bool TakeControls)
        {
            // Need to translate to MXP somehow
        }

        public void SendTaskInventory(UUID taskID, short serial, byte[] fileName)
        {
            // Need to translate to MXP somehow
        }

        public void SendBulkUpdateInventory(InventoryNodeBase node)
        {
            // Need to translate to MXP somehow
        }

        public void SendXferPacket(ulong xferID, uint packet, byte[] data)
        {
            // SL Specific, Ignore. (Remove from IClient)
        }

        public void SendEconomyData(float EnergyEfficiency, int ObjectCapacity, int ObjectCount, int PriceEnergyUnit, int PriceGroupCreate, int PriceObjectClaim, float PriceObjectRent, float PriceObjectScaleFactor, int PriceParcelClaim, float PriceParcelClaimFactor, int PriceParcelRent, int PricePublicObjectDecay, int PricePublicObjectDelete, int PriceRentLight, int PriceUpload, int TeleportMinPrice, float TeleportPriceExponent)
        {
            // SL Specific, Ignore. (Remove from IClient)
        }

        public void SendAvatarPickerReply(AvatarPickerReplyAgentDataArgs AgentData, List<AvatarPickerReplyDataArgs> Data)
        {
            // Need to translate to MXP somehow
        }

        public void SendAgentDataUpdate(UUID agentid, UUID activegroupid, string firstname, string lastname, ulong grouppowers, string groupname, string grouptitle)
        {
            // Need to translate to MXP somehow
            // TODO: This may need doing - involves displaying the users avatar name
        }

        public void SendPreLoadSound(UUID objectID, UUID ownerID, UUID soundID)
        {
            // Need to translate to MXP somehow
        }

        public void SendPlayAttachedSound(UUID soundID, UUID objectID, UUID ownerID, float gain, byte flags)
        {
            // Need to translate to MXP somehow
        }

        public void SendTriggeredSound(UUID soundID, UUID ownerID, UUID objectID, UUID parentID, ulong handle, Vector3 position, float gain)
        {
            // Need to translate to MXP somehow
        }

        public void SendAttachedSoundGainChange(UUID objectID, float gain)
        {
            // Need to translate to MXP somehow
        }

        public void SendNameReply(UUID profileId, string firstname, string lastname)
        {
            // SL Specific
        }

        public void SendAlertMessage(string message)
        {
            SendChatMessage(message, 0, Vector3.Zero, "System", UUID.Zero, 0, 0);
        }

        public void SendAgentAlertMessage(string message, bool modal)
        {
            SendChatMessage(message, 0, Vector3.Zero, "System" + (modal ? " Notice" : ""), UUID.Zero, 0, 0);
        }

        public void SendLoadURL(string objectname, UUID objectID, UUID ownerID, bool groupOwned, string message, string url)
        {
            // TODO: Probably can do this better
            SendChatMessage("Please visit: " + url, 0, Vector3.Zero, objectname, UUID.Zero, 0, 0);
        }

        public void SendDialog(string objectname, UUID objectID, UUID ownerID, string msg, UUID textureID, int ch, string[] buttonlabels)
        {
            // TODO: Probably can do this better
            SendChatMessage("Dialog: " + msg, 0, Vector3.Zero, objectname, UUID.Zero, 0, 0);
        }

        public bool AddMoney(int debit)
        {
            SendChatMessage("You were paid: " + debit, 0, Vector3.Zero, "System", UUID.Zero, 0, 0);
            return true;
        }

        public void SendSunPos(Vector3 sunPos, Vector3 sunVel, ulong CurrentTime, uint SecondsPerSunCycle, uint SecondsPerYear, float OrbitalPosition)
        {
            // Need to translate to MXP somehow
            // Send a light object?
        }

        public void SendViewerEffect(ViewerEffectPacket.EffectBlock[] effectBlocks)
        {
            // Need to translate to MXP somehow
        }

        public void SendViewerTime(int phase)
        {
            // Need to translate to MXP somehow
        }

        public UUID GetDefaultAnimation(string name)
        {
            return UUID.Zero;
        }

        public void SendAvatarProperties(UUID avatarID, string aboutText, string bornOn, byte[] charterMember, string flAbout, uint flags, UUID flImageID, UUID imageID, string profileURL, UUID partnerID)
        {
            // Need to translate to MXP somehow
        }

        public void SendScriptQuestion(UUID taskID, string taskName, string ownerName, UUID itemID, int question)
        {
            // Need to translate to MXP somehow
        }

        public void SendHealth(float health)
        {
            // Need to translate to MXP somehow
        }

        public void SendEstateManagersList(UUID invoice, UUID[] EstateManagers, uint estateID)
        {
            // Need to translate to MXP somehow
        }

        public void SendBannedUserList(UUID invoice, EstateBan[] banlist, uint estateID)
        {
            // Need to translate to MXP somehow
        }

        public void SendRegionInfoToEstateMenu(RegionInfoForEstateMenuArgs args)
        {
            // Need to translate to MXP somehow
        }

        public void SendEstateCovenantInformation(UUID covenant)
        {
            // Need to translate to MXP somehow
        }

        public void SendDetailedEstateData(UUID invoice, string estateName, uint estateID, uint parentEstate, uint estateFlags, uint sunPosition, UUID covenant, string abuseEmail, UUID estateOwner)
        {
            // Need to translate to MXP somehow
        }

        public void SendLandProperties(int sequence_id, bool snap_selection, int request_result, LandData landData, float simObjectBonusFactor, int parcelObjectCapacity, int simObjectCapacity, uint regionFlags)
        {
            // Need to translate to MXP somehow
        }

        public void SendLandAccessListData(List<UUID> avatars, uint accessFlag, int localLandID)
        {
            // Need to translate to MXP somehow
        }

        public void SendForceClientSelectObjects(List<uint> objectIDs)
        {
            // Need to translate to MXP somehow
        }

        public void SendLandObjectOwners(Dictionary<UUID, int> ownersAndCount)
        {
            // Need to translate to MXP somehow
        }

        public void SendLandParcelOverlay(byte[] data, int sequence_id)
        {
            // Need to translate to MXP somehow
        }

        public void SendParcelMediaCommand(uint flags, ParcelMediaCommandEnum command, float time)
        {
            // Need to translate to MXP somehow
        }

        public void SendParcelMediaUpdate(string mediaUrl, UUID mediaTextureID, byte autoScale, string mediaType, string mediaDesc, int mediaWidth, int mediaHeight, byte mediaLoop)
        {
            // Need to translate to MXP somehow
        }

        public void SendAssetUploadCompleteMessage(sbyte AssetType, bool Success, UUID AssetFullID)
        {
            // Need to translate to MXP somehow
        }

        public void SendConfirmXfer(ulong xferID, uint PacketID)
        {
            // Need to translate to MXP somehow
        }

        public void SendXferRequest(ulong XferID, short AssetType, UUID vFileID, byte FilePath, byte[] FileName)
        {
            // Need to translate to MXP somehow
        }

        public void SendInitiateDownload(string simFileName, string clientFileName)
        {
            // Need to translate to MXP somehow
        }

        public void SendImageFirstPart(ushort numParts, UUID ImageUUID, uint ImageSize, byte[] ImageData, byte imageCodec)
        {
            // Need to translate to MXP somehow
        }

        public void SendImageNextPart(ushort partNumber, UUID imageUuid, byte[] imageData)
        {
            // Need to translate to MXP somehow
        }

        public void SendImageNotFound(UUID imageid)
        {
            // Need to translate to MXP somehow
        }

        public void SendShutdownConnectionNotice()
        {
            // Need to translate to MXP somehow
        }

        public void SendSimStats(SimStats stats)
        {
            // Need to translate to MXP somehow
        }

        public void SendObjectPropertiesFamilyData(uint RequestFlags, UUID ObjectUUID, UUID OwnerID, UUID GroupID, uint BaseMask, uint OwnerMask, uint GroupMask, uint EveryoneMask, uint NextOwnerMask, int OwnershipCost, byte SaleType, int SalePrice, uint Category, UUID LastOwnerID, string ObjectName, string Description)
        {
            //throw new System.NotImplementedException();
        }

        public void SendObjectPropertiesReply(UUID ItemID, ulong CreationDate, UUID CreatorUUID, UUID FolderUUID, UUID FromTaskUUID, UUID GroupUUID, short InventorySerial, UUID LastOwnerUUID, UUID ObjectUUID, UUID OwnerUUID, string TouchTitle, byte[] TextureID, string SitTitle, string ItemName, string ItemDescription, uint OwnerMask, uint NextOwnerMask, uint GroupMask, uint EveryoneMask, uint BaseMask, byte saleType, int salePrice)
        {
            //throw new System.NotImplementedException();
        }

        public void SendAgentOffline(UUID[] agentIDs)
        {
            // Need to translate to MXP somehow (Friends List)
        }

        public void SendAgentOnline(UUID[] agentIDs)
        {
            // Need to translate to MXP somehow (Friends List)
        }

        public void SendSitResponse(UUID TargetID, Vector3 OffsetPos, Quaternion SitOrientation, bool autopilot, Vector3 CameraAtOffset, Vector3 CameraEyeOffset, bool ForceMouseLook)
        {
            // Need to translate to MXP somehow
        }

        public void SendAdminResponse(UUID Token, uint AdminLevel)
        {
            // Need to translate to MXP somehow
        }

        public void SendGroupMembership(GroupMembershipData[] GroupMembership)
        {
            // Need to translate to MXP somehow
        }

        public void SendGroupNameReply(UUID groupLLUID, string GroupName)
        {
            // Need to translate to MXP somehow
        }

        public void SendJoinGroupReply(UUID groupID, bool success)
        {
            // Need to translate to MXP somehow
        }

        public void SendEjectGroupMemberReply(UUID agentID, UUID groupID, bool success)
        {
            // Need to translate to MXP somehow
        }

        public void SendLeaveGroupReply(UUID groupID, bool success)
        {
            // Need to translate to MXP somehow
        }

        public void SendLandStatReply(uint reportType, uint requestFlags, uint resultCount, LandStatReportItem[] lsrpia)
        {
            // Need to translate to MXP somehow
        }

        public void SendScriptRunningReply(UUID objectID, UUID itemID, bool running)
        {
            // Need to translate to MXP somehow
        }

        public void SendAsset(AssetRequestToClient req)
        {
            // Need to translate to MXP somehow
        }

        public void SendTexture(AssetBase TextureAsset)
        {
            // Need to translate to MXP somehow
        }

        public byte[] GetThrottlesPacked(float multiplier)
        {
            // LL Specific, get out of IClientAPI

            const int singlefloat = 4;
            float tResend = multiplier;
            float tLand = multiplier;
            float tWind = multiplier;
            float tCloud = multiplier;
            float tTask = multiplier;
            float tTexture = multiplier;
            float tAsset = multiplier;

            byte[] throttles = new byte[singlefloat * 7];
            int i = 0;
            Buffer.BlockCopy(BitConverter.GetBytes(tResend), 0, throttles, singlefloat * i, singlefloat);
            i++;
            Buffer.BlockCopy(BitConverter.GetBytes(tLand), 0, throttles, singlefloat * i, singlefloat);
            i++;
            Buffer.BlockCopy(BitConverter.GetBytes(tWind), 0, throttles, singlefloat * i, singlefloat);
            i++;
            Buffer.BlockCopy(BitConverter.GetBytes(tCloud), 0, throttles, singlefloat * i, singlefloat);
            i++;
            Buffer.BlockCopy(BitConverter.GetBytes(tTask), 0, throttles, singlefloat * i, singlefloat);
            i++;
            Buffer.BlockCopy(BitConverter.GetBytes(tTexture), 0, throttles, singlefloat * i, singlefloat);
            i++;
            Buffer.BlockCopy(BitConverter.GetBytes(tAsset), 0, throttles, singlefloat * i, singlefloat);

            return throttles;
        }

        public event ViewerEffectEventHandler OnViewerEffect;
        public event Action<IClientAPI> OnLogout;
        public event Action<IClientAPI> OnConnectionClosed;


        public void SendBlueBoxMessage(UUID FromAvatarID, string FromAvatarName, string Message)
        {
            SendChatMessage(Message, 0, Vector3.Zero, FromAvatarName, UUID.Zero, 0, 0);
        }

        public void SendLogoutPacket()
        {
            LeaveRequestMessage lrm = new LeaveRequestMessage();
            Session.Send(lrm);
        }

        public ClientInfo GetClientInfo()
        {
            return null;
            //throw new System.NotImplementedException();
        }

        public void SetClientInfo(ClientInfo info)
        {
            //throw new System.NotImplementedException();
        }

        public void SetClientOption(string option, string value)
        {
            // Need to translate to MXP somehow
        }

        public string GetClientOption(string option)
        {
            // Need to translate to MXP somehow
            return "";
        }

        public void Terminate()
        {
            Close(false);
        }

        public void SendSetFollowCamProperties(UUID objectID, SortedDictionary<int, float> parameters)
        {
            // Need to translate to MXP somehow
        }

        public void SendClearFollowCamProperties(UUID objectID)
        {
            // Need to translate to MXP somehow
        }

        public void SendRegionHandle(UUID regoinID, ulong handle)
        {
            // Need to translate to MXP somehow
        }

        public void SendParcelInfo(RegionInfo info, LandData land, UUID parcelID, uint x, uint y)
        {
            // Need to translate to MXP somehow
        }

        public void SendScriptTeleportRequest(string objName, string simName, Vector3 pos, Vector3 lookAt)
        {
            // Need to translate to MXP somehow
        }

        public void SendDirPlacesReply(UUID queryID, DirPlacesReplyData[] data)
        {
            // Need to translate to MXP somehow
        }

        public void SendDirPeopleReply(UUID queryID, DirPeopleReplyData[] data)
        {
            // Need to translate to MXP somehow
        }

        public void SendDirEventsReply(UUID queryID, DirEventsReplyData[] data)
        {
            // Need to translate to MXP somehow
        }

        public void SendDirGroupsReply(UUID queryID, DirGroupsReplyData[] data)
        {
            // Need to translate to MXP somehow
        }

        public void SendDirClassifiedReply(UUID queryID, DirClassifiedReplyData[] data)
        {
            // Need to translate to MXP somehow
        }

        public void SendDirLandReply(UUID queryID, DirLandReplyData[] data)
        {
            // Need to translate to MXP somehow
        }

        public void SendDirPopularReply(UUID queryID, DirPopularReplyData[] data)
        {
            // Need to translate to MXP somehow
        }

        public void SendEventInfoReply(EventData info)
        {
            // Need to translate to MXP somehow
        }

        public void SendMapItemReply(mapItemReply[] replies, uint mapitemtype, uint flags)
        {
            // Need to translate to MXP somehow
        }

        public void SendAvatarGroupsReply(UUID avatarID, GroupMembershipData[] data)
        {
            // Need to translate to MXP somehow
        }

        public void SendOfferCallingCard(UUID srcID, UUID transactionID)
        {
            // Need to translate to MXP somehow
        }

        public void SendAcceptCallingCard(UUID transactionID)
        {
            // Need to translate to MXP somehow
        }

        public void SendDeclineCallingCard(UUID transactionID)
        {
            // Need to translate to MXP somehow
        }

        public void SendTerminateFriend(UUID exFriendID)
        {
            // Need to translate to MXP somehow
        }

        public void SendAvatarClassifiedReply(UUID targetID, UUID[] classifiedID, string[] name)
        {
            // Need to translate to MXP somehow
        }

        public void SendClassifiedInfoReply(UUID classifiedID, UUID creatorID, uint creationDate, uint expirationDate, uint category, string name, string description, UUID parcelID, uint parentEstate, UUID snapshotID, string simName, Vector3 globalPos, string parcelName, byte classifiedFlags, int price)
        {
            // Need to translate to MXP somehow
        }

        public void SendAgentDropGroup(UUID groupID)
        {
            // Need to translate to MXP somehow
        }

        public void SendAvatarNotesReply(UUID targetID, string text)
        {
            // Need to translate to MXP somehow
        }

        public void SendAvatarPicksReply(UUID targetID, Dictionary<UUID, string> picks)
        {
            // Need to translate to MXP somehow
        }

        public void SendAvatarClassifiedReply(UUID targetID, Dictionary<UUID, string> classifieds)
        {
            // Need to translate to MXP somehow
        }

        public void SendParcelDwellReply(int localID, UUID parcelID, float dwell)
        {
            // Need to translate to MXP somehow
        }

        public void SendUserInfoReply(bool imViaEmail, bool visible, string email)
        {
            // Need to translate to MXP somehow
        }

        public void KillEndDone()
        {
            Stop();
        }

        public bool AddGenericPacketHandler(string MethodName, GenericMessage handler)
        {
            // Need to translate to MXP somehow
            return true;
        }

        #endregion

        #region IClientCore

        public bool TryGet<T>(out T iface)
        {
            iface = default(T);
            return false;
        }

        public T Get<T>()
        {
            return default(T);
        }

        #endregion
    }
}
