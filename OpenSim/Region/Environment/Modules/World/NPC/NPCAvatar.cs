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
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.World.NPC
{
    public class NPCAvatar : IClientAPI
    {
        private readonly string m_firstname;
        private readonly string m_lastname;
        private readonly Vector3 m_startPos;
        private readonly UUID m_uuid = UUID.Random();
        private readonly Scene m_scene;


        public NPCAvatar(string firstname, string lastname, Vector3 position, Scene scene)
        {
            m_firstname = firstname;
            m_lastname = lastname;
            m_startPos = position;
            m_scene = scene;
        }

        public IScene Scene
        {
            get { return m_scene; }
        }

        public void Say(string message)
        {
            SendOnChatFromViewer(message, ChatTypeEnum.Say);
        }

        public void Shout(string message)
        {
            SendOnChatFromViewer(message, ChatTypeEnum.Shout);
        }

        public void Whisper(string message)
        {
            SendOnChatFromViewer(message, ChatTypeEnum.Whisper);
        }

        public void Broadcast(string message)
        {
            SendOnChatFromViewer(message, ChatTypeEnum.Broadcast);
        }

        public void GiveMoney(UUID target, int amount)
        {
            OnMoneyTransferRequest(m_uuid, target, amount, 1, "Payment");
        }

        public void InstantMessage(UUID target, string message)
        {
            OnInstantMessage(this, m_uuid, SessionId, target, UUID.Combine(m_uuid, target),
                             (uint) Util.UnixTimeSinceEpoch(), Name, message, 0, false, 0, 0,
                             Position, m_scene.RegionInfo.RegionID, new byte[0]);
        }

        public void SendAgentOffline(UUID[] agentIDs)
        {

        }

        public void SendAgentOnline(UUID[] agentIDs)
        {

        }
        public void SendSitResponse(UUID TargetID, Vector3 OffsetPos, Quaternion SitOrientation, bool autopilot,
                                        Vector3 CameraAtOffset, Vector3 CameraEyeOffset, bool ForceMouseLook)
        {

        }

        public void SendAdminResponse(UUID Token, uint AdminLevel)
        {

        }

        public void SendGroupMembership(GroupData[] GroupMembership)
        {

        }

        public UUID GetDefaultAnimation(string name)
        {
            return UUID.Zero;
        }

        public Vector3 Position
        {
            get { return m_scene.Entities[m_uuid].AbsolutePosition; }
            set { m_scene.Entities[m_uuid].AbsolutePosition = value; }
        }

        public bool SendLogoutPacketWhenClosing
        {
            set { }
        }

        #region Internal Functions

        private void SendOnChatFromViewer(string message, ChatTypeEnum chatType)
        {
            OSChatMessage chatFromViewer = new OSChatMessage();
            chatFromViewer.Channel = 0;
            chatFromViewer.From = Name;
            chatFromViewer.Message = message;
            chatFromViewer.Position = StartPos;
            chatFromViewer.Scene = m_scene;
            chatFromViewer.Sender = this;
            chatFromViewer.SenderUUID = AgentId;
            chatFromViewer.Type = chatType;

            OnChatFromViewer(this, chatFromViewer);
        }

        #endregion

        #region Event Definitions IGNORE

// disable warning: public events constituting public API
#pragma warning disable 67
        public event Action<IClientAPI> OnLogout;
        public event ObjectPermissions OnObjectPermissions;

        public event MoneyTransferRequest OnMoneyTransferRequest;
        public event ParcelBuy OnParcelBuy;
        public event Action<IClientAPI> OnConnectionClosed;
        public event GenericMessage OnGenericMessage;
        public event ImprovedInstantMessage OnInstantMessage;
        public event ChatMessage OnChatFromViewer;
        public event TextureRequest OnRequestTexture;
        public event RezObject OnRezObject;
        public event ModifyTerrain OnModifyTerrain;
        public event SetAppearance OnSetAppearance;
        public event AvatarNowWearing OnAvatarNowWearing;
        public event RezSingleAttachmentFromInv OnRezSingleAttachmentFromInv;
        public event UUIDNameRequest OnDetachAttachmentIntoInv;
        public event ObjectAttach OnObjectAttach;
        public event ObjectDeselect OnObjectDetach;
        public event StartAnim OnStartAnim;
        public event StopAnim OnStopAnim;
        public event LinkObjects OnLinkObjects;
        public event DelinkObjects OnDelinkObjects;
        public event RequestMapBlocks OnRequestMapBlocks;
        public event RequestMapName OnMapNameRequest;
        public event TeleportLocationRequest OnTeleportLocationRequest;
        public event TeleportLandmarkRequest OnTeleportLandmarkRequest;
        public event DisconnectUser OnDisconnectUser;
        public event RequestAvatarProperties OnRequestAvatarProperties;
        public event SetAlwaysRun OnSetAlwaysRun;

        public event GenericCall4 OnDeRezObject;
        public event Action<IClientAPI> OnRegionHandShakeReply;
        public event GenericCall2 OnRequestWearables;
        public event GenericCall2 OnCompleteMovementToRegion;
        public event UpdateAgent OnAgentUpdate;
        public event AgentRequestSit OnAgentRequestSit;
        public event AgentSit OnAgentSit;
        public event AvatarPickerRequest OnAvatarPickerRequest;
        public event Action<IClientAPI> OnRequestAvatarsData;
        public event AddNewPrim OnAddPrim;
        public event RequestGodlikePowers OnRequestGodlikePowers;
        public event GodKickUser OnGodKickUser;
        public event ObjectDuplicate OnObjectDuplicate;
        public event UpdateVector OnGrabObject;
        public event ObjectSelect OnDeGrabObject;
        public event MoveObject OnGrabUpdate;
        public event ViewerEffectEventHandler OnViewerEffect;

        public event FetchInventory OnAgentDataUpdateRequest;
        public event FetchInventory OnUserInfoRequest;
        public event TeleportLocationRequest OnSetStartLocationRequest;

        public event UpdateShape OnUpdatePrimShape;
        public event ObjectExtraParams OnUpdateExtraParams;
        public event RequestObjectPropertiesFamily OnRequestObjectPropertiesFamily;
        public event ObjectSelect OnObjectSelect;
        public event GenericCall7 OnObjectDescription;
        public event GenericCall7 OnObjectName;
        public event GenericCall7 OnObjectClickAction;
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

        public event CreateNewInventoryItem OnCreateNewInventoryItem;
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
        public event XferReceive OnXferReceive;
        public event RequestXfer OnRequestXfer;
        public event ConfirmXfer OnConfirmXfer;
        public event RezScript OnRezScript;
        public event UpdateTaskInventory OnUpdateTaskInventory;
        public event MoveTaskInventory OnMoveTaskItem;
        public event RemoveTaskInventory OnRemoveTaskItem;
        public event RequestAsset OnRequestAsset;

        public event UUIDNameRequest OnNameFromUUIDRequest;
        public event UUIDNameRequest OnUUIDGroupNameRequest;

        public event ParcelPropertiesRequest OnParcelPropertiesRequest;
        public event ParcelDivideRequest OnParcelDivideRequest;
        public event ParcelJoinRequest OnParcelJoinRequest;
        public event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest;
        public event ParcelAbandonRequest OnParcelAbandonRequest;
        public event ParcelReclaim OnParcelReclaim;
        public event ParcelReturnObjectsRequest OnParcelReturnObjectsRequest;
        public event ParcelAccessListRequest OnParcelAccessListRequest;
        public event ParcelAccessListUpdateRequest OnParcelAccessListUpdateRequest;
        public event ParcelSelectObjects OnParcelSelectObjects;
        public event ParcelObjectOwnerRequest OnParcelObjectOwnerRequest;
        public event ObjectDeselect OnObjectDeselect;
        public event RegionInfoRequest OnRegionInfoRequest;
        public event EstateCovenantRequest OnEstateCovenantRequest;

        public event ObjectDuplicateOnRay OnObjectDuplicateOnRay;

        public event FriendActionDelegate OnApproveFriendRequest;
        public event FriendActionDelegate OnDenyFriendRequest;
        public event FriendshipTermination OnTerminateFriendship;

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
        public event BakeTerrain OnBakeTerrain;
        public event EstateRestartSimRequest OnEstateRestartSimRequest;
        public event EstateChangeCovenantRequest OnEstateChangeCovenantRequest;
        public event UpdateEstateAccessDeltaRequest OnUpdateEstateAccessDeltaRequest;
        public event SimulatorBlueBoxMessageRequest OnSimulatorBlueBoxMessageRequest;
        public event EstateBlueBoxMessageRequest OnEstateBlueBoxMessageRequest;
        public event EstateDebugRegionRequest OnEstateDebugRegionRequest;
        public event EstateTeleportOneUserHomeRequest OnEstateTeleportOneUserHomeRequest;
        public event EstateChangeInfo OnEstateChangeInfo;
        public event ScriptReset OnScriptReset;
        public event GetScriptRunning OnGetScriptRunning;
        public event SetScriptRunning OnSetScriptRunning;
        public event UpdateVector OnAutoPilotGo;

        public event TerrainUnacked OnUnackedTerrain;

        public event RegionHandleRequest OnRegionHandleRequest;
        public event ParcelInfoRequest OnParcelInfoRequest;

        public event ActivateGesture OnActivateGesture;
        public event DeactivateGesture OnDeactivateGesture;

#pragma warning restore 67

        #endregion

        public void ActivateGesture(UUID assetId, UUID gestureId)
        {
        }
        public void DeactivateGesture(UUID assetId, UUID gestureId)
        {
        }

        #region Overrriden Methods IGNORE

        public virtual Vector3 StartPos
        {
            get { return m_startPos; }
            set { }
        }

        public virtual UUID AgentId
        {
            get { return m_uuid; }
        }

        public UUID SessionId
        {
            get { return UUID.Zero; }
        }

        public UUID SecureSessionId
        {
            get { return UUID.Zero; }
        }

        public virtual string FirstName
        {
            get { return m_firstname; }
        }

        public virtual string LastName
        {
            get { return m_lastname; }
        }

        public virtual String Name
        {
            get { return FirstName + " " + LastName; }
        }

        public bool IsActive
        {
            get { return true; }
            set { }
        }

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

        public ulong GetGroupPowers(UUID groupID)
        {
            return 0;
        }

        public virtual int NextAnimationSequenceNumber
        {
            get { return 1; }
        }

        public virtual void OutPacket(Packet newPack, ThrottleOutPacketType packType)
        {
        }

        public virtual void SendWearables(AvatarWearable[] wearables, int serial)
        {
        }

        public virtual void SendAppearance(UUID agentID, byte[] visualParams, byte[] textureEntry)
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

        public virtual void SendKiPrimitive(ulong regionHandle, uint localID)
        {
        }

        public virtual void SetChildAgentThrottle(byte[] throttle)
        {
        }
        public byte[] GetThrottlesPacked(float multiplier)
        {
            return new byte[0];
        }


        public virtual void SendAnimations(UUID[] animations, int[] seqs, UUID sourceAgentId)
        {
        }

        public virtual void SendChatMessage(string message, byte type, Vector3 fromPos, string fromName,
                                            UUID fromAgentID, byte source, byte audible)
        {
        }

        public virtual void SendChatMessage(byte[] message, byte type, Vector3 fromPos, string fromName,
                                            UUID fromAgentID, byte source, byte audible)
        {
        }

        public virtual void SendInstantMessage(UUID fromAgent, UUID fromAgentSession, string message, UUID toAgent,
                                               UUID imSessionID, string fromName, byte dialog, uint timeStamp)
        {
        }

        public virtual void SendInstantMessage(UUID fromAgent, UUID fromAgentSession, string message, UUID toAgent,
                                               UUID imSessionID, string fromName, byte dialog, uint timeStamp,
                                               byte[] binaryBucket)
        {
        }

        public void SendGenericMessage(string method, List<string> message)
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

        public virtual void MoveAgentIntoRegion(RegionInfo regInfo, Vector3 pos, Vector3 look)
        {
        }

        public virtual void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourExternalEndPoint)
        {
        }

        public virtual AgentCircuitData RequestClientInfo()
        {
            return new AgentCircuitData();
        }

        public virtual void CrossRegion(ulong newRegionHandle, Vector3 pos, Vector3 lookAt,
                                        IPEndPoint newRegionExternalEndPoint, string capsURL)
        {
        }

        public virtual void SendMapBlock(List<MapBlockData> mapBlocks, uint flag)
        {
        }

        public virtual void SendLocalTeleport(Vector3 position, Vector3 lookAt, uint flags)
        {
        }

        public virtual void SendRegionTeleport(ulong regionHandle, byte simAccess, IPEndPoint regionExternalEndPoint,
                                               uint locationID, uint flags, string capsURL)
        {
        }

        public virtual void SendTeleportFailed(string reason)
        {
        }

        public virtual void SendTeleportLocationStart()
        {
        }

        public virtual void SendMoneyBalance(UUID transaction, bool success, byte[] description, int balance)
        {
        }

        public virtual void SendPayPrice(UUID objectID, int[] payPrice)
        {
        }

        public virtual void SendAvatarData(ulong regionHandle, string firstName, string lastName, UUID avatarID,
                                           uint avatarLocalID, Vector3 Pos, byte[] textureEntry, uint parentID, Quaternion rotation)
        {
        }

        public virtual void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID,
                                                  Vector3 position, Vector3 velocity, Quaternion rotation)
        {
        }

        public virtual void SendCoarseLocationUpdate(List<Vector3> CoarseLocations)
        {
        }

        public virtual void AttachObject(uint localID, Quaternion rotation, byte attachPoint)
        {
        }

        public virtual void SendDialog(string objectname, UUID objectID, UUID ownerID, string msg, UUID textureID, int ch, string[] buttonlabels)
        {
        }

        public virtual void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID,
                                                  PrimitiveBaseShape primShape, Vector3 pos, Vector3 vel,
                                                  Vector3 acc, Quaternion rotation, Vector3 rvel, uint flags,
                                                  UUID objectID, UUID ownerID, string text, byte[] color,
                                                  uint parentID,
                                                  byte[] particleSystem, byte clickAction)
        {
        }
        public virtual void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID,
                                                  PrimitiveBaseShape primShape, Vector3 pos, Vector3 vel,
                                                  Vector3 acc, Quaternion rotation, Vector3 rvel, uint flags,
                                                  UUID objectID, UUID ownerID, string text, byte[] color,
                                                  uint parentID,
                                                  byte[] particleSystem, byte clickAction, byte[] textureanimation,
                                                  bool attachment, uint AttachmentPoint, UUID AssetId, UUID SoundId, double SoundVolume, byte SoundFlags, double SoundRadius)
        {
        }
        public virtual void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID,
                                                Vector3 position, Quaternion rotation, Vector3 velocity,
                                                Vector3 rotationalvelocity, byte state, UUID AssetId)
        {
        }

        public virtual void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID,
                                                Vector3 position, Quaternion rotation, Vector3 velocity,
                                                Vector3 rotationalvelocity)
        {
        }

        public virtual void SendInventoryFolderDetails(UUID ownerID, UUID folderID,
                                                       List<InventoryItemBase> items,
                                                       List<InventoryFolderBase> folders,
                                                       bool fetchFolders,
                                                       bool fetchItems)
        {
        }

        public virtual void SendInventoryItemDetails(UUID ownerID, InventoryItemBase item)
        {
        }

        public virtual void SendInventoryItemCreateUpdate(InventoryItemBase Item)
        {
        }

        public virtual void SendRemoveInventoryItem(UUID itemID)
        {
        }

        /// <see>IClientAPI.SendBulkUpdateInventory(InventoryItemBase)</see>
        public virtual void SendBulkUpdateInventory(InventoryItemBase item)
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

            if (OnCompleteMovementToRegion != null)
            {
                OnCompleteMovementToRegion();
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

        public void SendImagePart(ushort numParts, UUID ImageUUID, uint ImageSize, byte[] ImageData, byte imageCodec)
        {
        }

        public void SendShutdownConnectionNotice()
        {
        }

        public void SendSimStats(Packet pack)
        {
        }

        public void SendObjectPropertiesFamilyData(uint RequestFlags, UUID ObjectUUID, UUID OwnerID, UUID GroupID,
                                                   uint BaseMask, uint OwnerMask, uint GroupMask, uint EveryoneMask,
                                                   uint NextOwnerMask, int OwnershipCost, byte SaleType, int SalePrice, uint Category,
                                                   UUID LastOwnerID, string ObjectName, string Description)
        {
        }

        public void SendObjectPropertiesReply(UUID ItemID, ulong CreationDate, UUID CreatorUUID, UUID FolderUUID, UUID FromTaskUUID,
                                              UUID GroupUUID, short InventorySerial, UUID LastOwnerUUID, UUID ObjectUUID,
                                              UUID OwnerUUID, string TouchTitle, byte[] TextureID, string SitTitle, string ItemName,
                                              string ItemDescription, uint OwnerMask, uint NextOwnerMask, uint GroupMask, uint EveryoneMask,
                                              uint BaseMask, byte saleType, int salePrice)
        {
        }

        public bool AddMoney(int debit)
        {
            return false;
        }

        public void SendSunPos(Vector3 sunPos, Vector3 sunVel, ulong time, uint dlen, uint ylen, float phase)
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

        public void SendAsset(AssetRequestToClient req)
        {
        }

        public void SendTexture(AssetBase TextureAsset)
        {
        }

        public void SetDebug(int newDebug)
        {
        }

        public void InPacket(object NewPack)
        {
        }

        public void ProcessInPacket(Packet NewPack)
        {
        }

        public void Close(bool ShutdownCircuit)
        {
        }

        public void Stop()
        {
        }

        private uint m_circuitCode;

        public uint CircuitCode
        {
            get { return m_circuitCode; }
            set { m_circuitCode = value; }
        }

        public void SendBlueBoxMessage(UUID FromAvatarID, UUID fromSessionID, String FromAvatarName, String Message)
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

        public void SendEstateManagersList(UUID invoice, UUID[] EstateManagers, uint estateID)
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
        public void SendDetailedEstateData(UUID invoice, string estateName, uint estateID, uint parentEstate, uint estateFlags, uint sunPosition, UUID covenant, string abuseEmail)
        {
        }

        public void SendLandProperties(IClientAPI remote_client, int sequence_id, bool snap_selection, int request_result, LandData landData, float simObjectBonusFactor,int parcelObjectCapacity, int simObjectCapacity, uint regionFlags)
        {
        }
        public void SendLandAccessListData(List<UUID> avatars, uint accessFlag, int localLandID)
        {
        }
        public void SendForceClientSelectObjects(List<uint> objectIDs)
        {
        }
        public void SendLandObjectOwners(Dictionary<UUID, int> ownersAndCount)
        {
        }
        public void SendLandParcelOverlay(byte[] data, int sequence_id)
        {
        }

        public void SendGroupNameReply(UUID groupLLUID, string GroupName)
        {
        }

        public void SendScriptRunningReply(UUID objectID, UUID itemID, bool running)
        {
        }

        public void SendLandStatReply(uint reportType, uint requestFlags, uint resultCount, LandStatReportItem[] lsrpia)
        {
        }
        #endregion


        public void SendParcelMediaCommand(uint flags, ParcelMediaCommandEnum command, float time)
        {
        }

        public void SendParcelMediaUpdate(string mediaUrl, UUID mediaTextureID,
                                   byte autoScale, string mediaType, string mediaDesc, int mediaWidth, int mediaHeight,
                                   byte mediaLoop)
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

        public void KillEndDone()
        {
        }
    }
}
