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
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.World.NPC
{
    public class NPCAvatar : IClientAPI
    {
        private readonly string m_firstname;
        private readonly string m_lastname;
        private readonly LLVector3 m_startPos;
        private readonly LLUUID m_uuid = LLUUID.Random();
        private readonly Scene m_scene;

        public NPCAvatar(string firstname, string lastname, LLVector3 position, Scene scene)
        {
            m_firstname = firstname;
            m_lastname = lastname;
            m_startPos = position;
            m_scene = scene;
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

        public void GiveMoney(LLUUID target, int amount)
        {
            OnMoneyTransferRequest(m_uuid, target, amount, 1, "Payment");
        }

        public void InstantMessage(LLUUID target, string message)
        {
            OnInstantMessage(this, m_uuid, SessionId, target, LLUUID.Combine(m_uuid, target),
                             (uint) Util.UnixTimeSinceEpoch(), Name, message, 0, false, 0, 0,
                             Position, m_scene.RegionInfo.RegionID, new byte[0]);
        }

        public void SendAgentOffline(LLUUID[] agentIDs)
        {

        }

        public void SendAgentOnline(LLUUID[] agentIDs)
        {

        }
        public void SendSitResponse(LLUUID TargetID, LLVector3 OffsetPos, LLQuaternion SitOrientation, bool autopilot,
                                        LLVector3 CameraAtOffset, LLVector3 CameraEyeOffset, bool ForceMouseLook)
        {

        }

        public void SendAdminResponse(LLUUID Token, uint AdminLevel)
        {

        }

        public void SendGroupMembership(GroupData[] GroupMembership)
        {

        }

        public LLUUID GetDefaultAnimation(string name)
        {
            return LLUUID.Zero;
        }

        public LLVector3 Position
        {
            get { return m_scene.Entities[m_uuid].AbsolutePosition; }
            set { m_scene.Entities[m_uuid].AbsolutePosition = value; }
        }

        #region Internal Functions

        private void SendOnChatFromViewer(string message, ChatTypeEnum chatType)
        {
            ChatFromViewerArgs chatFromViewer = new ChatFromViewerArgs();
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

        public event ImprovedInstantMessage OnInstantMessage;
        public event ChatFromViewer OnChatFromViewer;
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
        public event Action<LLUUID> OnRemoveAvatar;

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
        public event PacketStats OnPacketStats;

        public event EconomyDataRequest OnEconomyDataRequest;
        public event MoneyBalanceRequest OnMoneyBalanceRequest;
        public event UpdateAvatarProperties OnUpdateAvatarProperties;

        public event ObjectIncludeInSearch OnObjectIncludeInSearch;
        public event UUIDNameRequest OnTeleportHomeRequest;

        public event ScriptAnswer OnScriptAnswer;
        public event RequestPayPrice OnRequestPayPrice;
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
#pragma warning restore 67

        #endregion

        #region Overrriden Methods IGNORE

        public virtual LLVector3 StartPos
        {
            get { return m_startPos; }
            set { }
        }

        public virtual LLUUID AgentId
        {
            get { return m_uuid; }
        }

        public LLUUID SessionId
        {
            get { return LLUUID.Zero; }
        }

        public LLUUID SecureSessionId
        {
            get { return LLUUID.Zero; }
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

        public LLUUID ActiveGroupId
        {
            get { return LLUUID.Zero; }
        }

        public string ActiveGroupName
        {
            get { return String.Empty; }
        }

        public ulong ActiveGroupPowers
        {
            get { return 0; }
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

        public virtual void SendAppearance(LLUUID agentID, byte[] visualParams, byte[] textureEntry)
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

        public virtual void SendAgentDataUpdate(LLUUID agentid, LLUUID activegroupid, string firstname, string lastname, ulong grouppowers, string groupname, string grouptitle)
        {

        }

        public virtual void SendKillObject(ulong regionHandle, uint localID)
        {
        }

        public virtual void SetChildAgentThrottle(byte[] throttle)
        {
        }
        public byte[] GetThrottlesPacked(float multiplier)
        {
            return new byte[0];
        }


        public virtual void SendAnimations(LLUUID[] animations, int[] seqs, LLUUID sourceAgentId)
        {
        }

        public virtual void SendChatMessage(string message, byte type, LLVector3 fromPos, string fromName,
                                            LLUUID fromAgentID, byte source, byte audible)
        {
        }

        public virtual void SendChatMessage(byte[] message, byte type, LLVector3 fromPos, string fromName,
                                            LLUUID fromAgentID, byte source, byte audible)
        {
        }

        public virtual void SendInstantMessage(LLUUID fromAgent, LLUUID fromAgentSession, string message, LLUUID toAgent,
                                               LLUUID imSessionID, string fromName, byte dialog, uint timeStamp)
        {
        }

        public virtual void SendInstantMessage(LLUUID fromAgent, LLUUID fromAgentSession, string message, LLUUID toAgent,
                                               LLUUID imSessionID, string fromName, byte dialog, uint timeStamp,
                                               byte[] binaryBucket)
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

        public virtual void MoveAgentIntoRegion(RegionInfo regInfo, LLVector3 pos, LLVector3 look)
        {
        }

        public virtual void InformClientOfNeighbour(ulong neighbourHandle, IPEndPoint neighbourExternalEndPoint)
        {
        }

        public virtual AgentCircuitData RequestClientInfo()
        {
            return new AgentCircuitData();
        }

        public virtual void CrossRegion(ulong newRegionHandle, LLVector3 pos, LLVector3 lookAt,
                                        IPEndPoint newRegionExternalEndPoint, string capsURL)
        {
        }

        public virtual void SendMapBlock(List<MapBlockData> mapBlocks, uint flag)
        {
        }

        public virtual void SendLocalTeleport(LLVector3 position, LLVector3 lookAt, uint flags)
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

        public virtual void SendMoneyBalance(LLUUID transaction, bool success, byte[] description, int balance)
        {
        }

        public virtual void SendPayPrice(LLUUID objectID, int[] payPrice)
        {
        }

        public virtual void SendAvatarData(ulong regionHandle, string firstName, string lastName, LLUUID avatarID,
                                           uint avatarLocalID, LLVector3 Pos, byte[] textureEntry, uint parentID)
        {
        }

        public virtual void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID,
                                                  LLVector3 position, LLVector3 velocity, LLQuaternion rotation)
        {
        }

        public virtual void SendCoarseLocationUpdate(List<LLVector3> CoarseLocations)
        {
        }

        public virtual void AttachObject(uint localID, LLQuaternion rotation, byte attachPoint)
        {
        }

        public virtual void SendDialog(string objectname, LLUUID objectID, LLUUID ownerID, string msg, LLUUID textureID, int ch, string[] buttonlabels)
        {
        }

        public virtual void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID,
                                                  PrimitiveBaseShape primShape, LLVector3 pos, LLVector3 vel,
                                                  LLVector3 acc, LLQuaternion rotation, LLVector3 rvel, uint flags,
                                                  LLUUID objectID, LLUUID ownerID, string text, byte[] color,
                                                  uint parentID,
                                                  byte[] particleSystem, byte clickAction, bool track)
        {
        }
        public virtual void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID,
                                                  PrimitiveBaseShape primShape, LLVector3 pos, LLVector3 vel,
                                                  LLVector3 acc, LLQuaternion rotation, LLVector3 rvel, uint flags,
                                                  LLUUID objectID, LLUUID ownerID, string text, byte[] color,
                                                  uint parentID,
                                                  byte[] particleSystem, byte clickAction, byte[] textureanimation,
                                                  bool attachment, uint AttachmentPoint, LLUUID AssetId, LLUUID SoundId, double SoundVolume, byte SoundFlags, double SoundRadius, bool track)
        {
        }
        public virtual void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID,
                                                LLVector3 position, LLQuaternion rotation, LLVector3 velocity,
                                                LLVector3 rotationalvelocity, byte state, LLUUID AssetId)
        {
        }

        public virtual void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID,
                                                LLVector3 position, LLQuaternion rotation, LLVector3 velocity,
                                                LLVector3 rotationalvelocity)
        {
        }

        public virtual void SendInventoryFolderDetails(LLUUID ownerID, LLUUID folderID,
                                                       List<InventoryItemBase> items,
                                                       List<InventoryFolderBase> folders,
                                                       bool fetchFolders,
                                                       bool fetchItems)
        {
        }

        public virtual void SendInventoryItemDetails(LLUUID ownerID, InventoryItemBase item)
        {
        }

        public virtual void SendInventoryItemCreateUpdate(InventoryItemBase Item)
        {
        }

        public virtual void SendRemoveInventoryItem(LLUUID itemID)
        {
        }

        /// <see>IClientAPI.SendBulkUpdateInventory(InventoryItemBase)</see>
        public virtual void SendBulkUpdateInventory(InventoryItemBase item)
        {
        }

        public void SendTakeControls(int controls, bool passToAgent, bool TakeControls)
        {
        }

        public virtual void SendTaskInventory(LLUUID taskID, short serial, byte[] fileName)
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
        public virtual void SendNameReply(LLUUID profileId, string firstname, string lastname)
        {
        }

        public virtual void SendPreLoadSound(LLUUID objectID, LLUUID ownerID, LLUUID soundID)
        {
        }

        public virtual void SendPlayAttachedSound(LLUUID soundID, LLUUID objectID, LLUUID ownerID, float gain,
                                                  byte flags)
        {
        }

        public void SendTriggeredSound(LLUUID soundID, LLUUID ownerID, LLUUID objectID, LLUUID parentID, ulong handle, LLVector3 position, float gain)
        {
        }

        public void SendAttachedSoundGainChange(LLUUID objectID, float gain)
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

        public void SendLoadURL(string objectname, LLUUID objectID, LLUUID ownerID, bool groupOwned, string message,
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
        public void SendAssetUploadCompleteMessage(sbyte AssetType, bool Success, LLUUID AssetFullID)
        {
        }

        public void SendConfirmXfer(ulong xferID, uint PacketID)
        {
        }

        public void SendXferRequest(ulong XferID, short AssetType, LLUUID vFileID, byte FilePath, byte[] FileName)
        {
        }

        public void SendImagePart(ushort numParts, LLUUID ImageUUID, uint ImageSize, byte[] ImageData, byte imageCodec)
        {
        }

        public void SendShutdownConnectionNotice()
        {
        }

        public void SendSimStats(Packet pack)
        {
        }

        public void SendObjectPropertiesFamilyData(uint RequestFlags, LLUUID ObjectUUID, LLUUID OwnerID, LLUUID GroupID,
                                                   uint BaseMask, uint OwnerMask, uint GroupMask, uint EveryoneMask,
                                                   uint NextOwnerMask, int OwnershipCost, byte SaleType, int SalePrice, uint Category,
                                                   LLUUID LastOwnerID, string ObjectName, string Description)
        {
        }

        public void SendObjectPropertiesReply(LLUUID ItemID, ulong CreationDate, LLUUID CreatorUUID, LLUUID FolderUUID, LLUUID FromTaskUUID,
                                              LLUUID GroupUUID, short InventorySerial, LLUUID LastOwnerUUID, LLUUID ObjectUUID,
                                              LLUUID OwnerUUID, string TouchTitle, byte[] TextureID, string SitTitle, string ItemName,
                                              string ItemDescription, uint OwnerMask, uint NextOwnerMask, uint GroupMask, uint EveryoneMask,
                                              uint BaseMask)
        {
        }

        public bool AddMoney(int debit)
        {
            return false;
        }

        public void SendSunPos(LLVector3 sunPos, LLVector3 sunVel, ulong time, uint dlen, uint ylen, float phase)
        {
        }

        public void SendViewerTime(int phase)
        {
        }

        public void SendAvatarProperties(LLUUID avatarID, string aboutText, string bornOn, string charterMember,
                                         string flAbout, uint flags, LLUUID flImageID, LLUUID imageID, string profileURL,
                                         LLUUID partnerID)
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

        public void InPacket(Packet NewPack)
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
        public void SendBlueBoxMessage(LLUUID FromAvatarID, LLUUID fromSessionID, String FromAvatarName, String Message)
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

        public void SendScriptQuestion(LLUUID objectID, string taskName, string ownerName, LLUUID itemID, int question)
        {
        }
        public void SendHealth(float health)
        {
        }

        public void SendEstateManagersList(LLUUID invoice, LLUUID[] EstateManagers, uint estateID)
        {
        }

        public void SendBannedUserList(LLUUID invoice, EstateBan[] banlist, uint estateID)
        {
        }

        public void SendRegionInfoToEstateMenu(RegionInfoForEstateMenuArgs args)
        {
        }
        public void SendEstateCovenantInformation(LLUUID covenant)
        {
        }
        public void SendDetailedEstateData(LLUUID invoice, string estateName, uint estateID, uint parentEstate, uint estateFlags, uint sunPosition, LLUUID covenant)
        {
        }

        public void SendLandProperties(IClientAPI remote_client, int sequence_id, bool snap_selection, int request_result, LandData landData, float simObjectBonusFactor,int parcelObjectCapacity, int simObjectCapacity, uint regionFlags)
        {
        }
        public void SendLandAccessListData(List<LLUUID> avatars, uint accessFlag, int localLandID)
        {
        }
        public void SendForceClientSelectObjects(List<uint> objectIDs)
        {
        }
        public void SendLandObjectOwners(Dictionary<LLUUID, int> ownersAndCount)
        {
        }
        public void SendLandParcelOverlay(byte[] data, int sequence_id)
        {
        }

        public void SendGroupNameReply(LLUUID groupLLUID, string GroupName)
        {
        }

        public void SendScriptRunningReply(LLUUID objectID, LLUUID itemID, bool running)
        {
        }

        public void SendLandStatReply(uint reportType, uint requestFlags, uint resultCount, LandStatReportItem[] lsrpia)
        {
        }
        #endregion


        public void SendParcelMediaCommand(uint flags, ParcelMediaCommandEnum command, float time)
        {           
        }

        public void SendParcelMediaUpdate(string mediaUrl, LLUUID mediaTextureID,
                                   byte autoScale, string mediaType, string mediaDesc, int mediaWidth, int mediaHeight,
                                   byte mediaLoop)
        {  
        }
    }
}
