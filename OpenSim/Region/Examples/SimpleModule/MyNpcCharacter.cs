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

namespace OpenSim.Region.Examples.SimpleModule
{
    public class MyNpcCharacter : IClientAPI
    {
        private uint movementFlag = 0;
        private short flyState = 0;
        private Quaternion bodyDirection = Quaternion.Identity;
        private short count = 0;
        private short frame = 0;
        private Scene m_scene;

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
        public event DisconnectUser OnDisconnectUser;
        public event RequestAvatarProperties OnRequestAvatarProperties;
        public event SetAlwaysRun OnSetAlwaysRun;

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
        public event RequestGodlikePowers OnRequestGodlikePowers;
        public event GodKickUser OnGodKickUser;
        public event ObjectDuplicate OnObjectDuplicate;
        public event GrabObject OnGrabObject;
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
        public event GenericCall7 OnObjectMaterial;
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
        public event ObjectDeselect OnObjectDeselect;
        public event RegionInfoRequest OnRegionInfoRequest;
        public event EstateCovenantRequest OnEstateCovenantRequest;
        public event EstateChangeInfo OnEstateChangeInfo;

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
        public event UpdateVector OnAutoPilotGo;

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


#pragma warning restore 67

        private UUID myID = UUID.Random();

        public MyNpcCharacter(Scene scene)
        {

            // startPos = new Vector3(128, (float)(Util.RandomClass.NextDouble()*100), 2);
            m_scene = scene;
            m_scene.EventManager.OnFrame += Update;
        }

        private Vector3 startPos = new Vector3(128, 128, 2);

        public virtual Vector3 StartPos
        {
            get { return startPos; }
            set { }
        }

        public virtual UUID AgentId
        {
            get { return myID; }
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
            get { return "Only"; }
        }

        private string lastName = "Today" + Util.RandomClass.Next(1, 1000);

        public virtual string LastName
        {
            get { return lastName; }
        }

        public virtual String Name
        {
            get { return FirstName + LastName; }
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

        public IScene Scene
        {
            get { return m_scene; }
        }

        public bool SendLogoutPacketWhenClosing
        {
            set { }
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

        public void SendInstantMessage(UUID fromAgent, string message, UUID toAgent, string fromName, byte dialog, uint timeStamp)
        {
            
        }

        public void SendInstantMessage(UUID fromAgent, string message, UUID toAgent, string fromName, byte dialog, uint timeStamp, bool fromGroup, byte[] binaryBucket)
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

        public virtual void SendWindData(Vector2[] windSpeeds) { }

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

        public virtual void SendAvatarData(ulong regionHandle, string firstName, string lastName, string grouptitle, UUID avatarID,
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
                                                  byte[] particleSystem, byte clickAction, byte material)
        {
        }
        public virtual void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID,
                                                  PrimitiveBaseShape primShape, Vector3 pos, Vector3 vel,
                                                  Vector3 acc, Quaternion rotation, Vector3 rvel, uint flags,
                                                  UUID objectID, UUID ownerID, string text, byte[] color,
                                                  uint parentID,
                                                  byte[] particleSystem, byte clickAction, byte material, byte[] textureanimation,
                                                  bool attachment, uint AttachmentPoint, UUID AssetId, UUID SoundId, double SoundVolume, byte SoundFlags, double SoundRadius)
        {
        }
        public virtual void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID,
                                                Vector3 position, Quaternion rotation, Vector3 velocity,
                                                Vector3 rotationalvelocity, byte state, UUID AssetId)
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

        public UUID GetDefaultAnimation(string name)
        {
            return UUID.Zero;
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

        public void SendImageFirstPart(ushort numParts, UUID ImageUUID, uint ImageSize, byte[] ImageData, byte imageCodec)
        {
        }
        
        public void SendImageNextPart(ushort partNumber, UUID imageUuid, byte[] imageData)
        {
        }
         
        public void SendImageNotFound(UUID imageid)
        {
        }
        
        public void SendShutdownConnectionNotice()
        {
        }

        public void SendSimStats(SimStats stats)
        {
        }

        public void SendObjectPropertiesFamilyData(uint RequestFlags, UUID ObjectUUID, UUID OwnerID, UUID GroupID,
                                                    uint BaseMask, uint OwnerMask, uint GroupMask, uint EveryoneMask,
                                                    uint NextOwnerMask, int OwnershipCost, byte SaleType,int SalePrice, uint Category,
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

        public void SendGroupMembership(GroupMembershipData[] GroupMembership)
        {

        }

        private void Update()
        {
            frame++;
            if (frame > 20)
            {
                frame = 0;
                if (OnAgentUpdate != null)
                {
                    AgentUpdateArgs pack = new AgentUpdateArgs();
                    pack.ControlFlags = movementFlag;
                    pack.BodyRotation = bodyDirection;

                    OnAgentUpdate(this, pack);
                }
                if (flyState == 0)
                {
                    movementFlag = (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY |
                                   (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG;
                    flyState = 1;
                }
                else if (flyState == 1)
                {
                    movementFlag = (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY |
                                   (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_POS;
                    flyState = 2;
                }
                else
                {
                    movementFlag = (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY;
                    flyState = 0;
                }

                if (count >= 10)
                {
                    if (OnChatFromClient != null)
                    {
                        OSChatMessage args = new OSChatMessage();
                        args.Message = "Hey You! Get out of my Home. This is my Region";
                        args.Channel = 0;
                        args.From = FirstName + " " + LastName;
                        args.Position = new Vector3(128, 128, 26);
                        args.Sender = this;
                        args.Type = ChatTypeEnum.Shout;

                        OnChatFromClient(this, args);
                    }
                    count = -1;
                }

                count++;
            }
        }

        public bool AddMoney(int debit)
        {
            return false;
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

        public void SetDebugPacketLevel(int newDebug)
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
        
        public void SendDetailedEstateData(UUID invoice, string estateName, uint estateID, uint parentEstate, uint estateFlags, uint sunPosition, UUID covenant, string abuseEmail, UUID estateOwner)
        {
        }

        public void SendLandProperties(int sequence_id, bool snap_selection, int request_result, LandData landData, float simObjectBonusFactor, int parcelObjectCapacity, int simObjectCapacity, uint regionFlags)
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

        public void KillEndDone()
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

        public void SendTerminateFriend(UUID exFriendID)
        {
        }
    }
}
