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
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Client.VWoHTTP.ClientStack
{
    class VWHClientView : IClientAPI 
    {
        private Scene m_scene;


        public bool ProcessInMsg(OSHttpRequest req, OSHttpResponse resp)
        {
            //                              0        1          2       3
            // http://simulator.com:9000/vwohttp/sessionid/methodname/param
            string[] urlparts = req.Url.AbsolutePath.Split(new char[] {'/'}, StringSplitOptions.RemoveEmptyEntries);

            UUID sessionID;
            // Check for session
            if (!UUID.TryParse(urlparts[1], out sessionID))
                return false;
            // Check we match session
            if (sessionID != SessionId)
                return false;

            string method = urlparts[2];

            string param = String.Empty;
            if (urlparts.Length > 3)
                param = urlparts[3];

            bool found;

            switch (method.ToLower())
            {
                case "textures":
                    found = ProcessTextureRequest(param, resp);
                    break;
                default:
                    found = false;
                    break;
            }

            return found;
        }

        private bool ProcessTextureRequest(string param, OSHttpResponse resp)
        {
            UUID assetID;
            if (!UUID.TryParse(param, out assetID))
                return false;

            AssetBase asset = m_scene.AssetService.Get(assetID.ToString());

            if (asset == null)
                return false;

            ManagedImage tmp;
            Image imgData;

            OpenJPEG.DecodeToImage(asset.Data, out tmp, out imgData);
            
            MemoryStream ms = new MemoryStream();

            imgData.Save(ms, ImageFormat.Jpeg);

            byte[] jpegdata = ms.GetBuffer();

            ms.Close();

            resp.ContentType = "image/jpeg";
            resp.ContentLength = jpegdata.Length;
            resp.StatusCode = 200;
            resp.Body.Write(jpegdata, 0, jpegdata.Length);

            return true;
        }

        public VWHClientView(UUID sessionID, UUID agentID, string agentName, Scene scene)
        {
            m_scene = scene;
        }

        #region Implementation of IClientAPI

        public Vector3 StartPos
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public UUID AgentId
        {
            get { throw new System.NotImplementedException(); }
        }

        public UUID SessionId
        {
            get { throw new System.NotImplementedException(); }
        }

        public UUID SecureSessionId
        {
            get { throw new System.NotImplementedException(); }
        }

        public UUID ActiveGroupId
        {
            get { throw new System.NotImplementedException(); }
        }

        public string ActiveGroupName
        {
            get { throw new System.NotImplementedException(); }
        }

        public ulong ActiveGroupPowers
        {
            get { throw new System.NotImplementedException(); }
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
            get { throw new System.NotImplementedException(); }
        }

        public string LastName
        {
            get { throw new System.NotImplementedException(); }
        }

        public IScene Scene
        {
            get { throw new System.NotImplementedException(); }
        }

        public int NextAnimationSequenceNumber
        {
            get { throw new System.NotImplementedException(); }
        }

        public string Name
        {
            get { throw new System.NotImplementedException(); }
        }

        public bool IsActive
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool SendLogoutPacketWhenClosing
        {
            set { throw new System.NotImplementedException(); }
        }

        public uint CircuitCode
        {
            get { throw new System.NotImplementedException(); }
        }

        public event GenericMessage OnGenericMessage = delegate { };
        public event ImprovedInstantMessage OnInstantMessage = delegate { };
        public event ChatMessage OnChatFromClient = delegate { };
        public event TextureRequest OnRequestTexture = delegate { };
        public event RezObject OnRezObject = delegate { };
        public event ModifyTerrain OnModifyTerrain = delegate { };
        public event BakeTerrain OnBakeTerrain = delegate { };
        public event EstateChangeInfo OnEstateChangeInfo = delegate { };
        public event SetAppearance OnSetAppearance = delegate { };
        public event AvatarNowWearing OnAvatarNowWearing = delegate { };
        public event RezSingleAttachmentFromInv OnRezSingleAttachmentFromInv = delegate { return new UUID(); };
        public event RezMultipleAttachmentsFromInv OnRezMultipleAttachmentsFromInv = delegate { };
        public event UUIDNameRequest OnDetachAttachmentIntoInv = delegate { };
        public event ObjectAttach OnObjectAttach = delegate { };
        public event ObjectDeselect OnObjectDetach = delegate { };
        public event ObjectDrop OnObjectDrop = delegate { };
        public event StartAnim OnStartAnim = delegate { };
        public event StopAnim OnStopAnim = delegate { };
        public event LinkObjects OnLinkObjects = delegate { };
        public event DelinkObjects OnDelinkObjects = delegate { };
        public event RequestMapBlocks OnRequestMapBlocks = delegate { };
        public event RequestMapName OnMapNameRequest = delegate { };
        public event TeleportLocationRequest OnTeleportLocationRequest = delegate { };
        public event DisconnectUser OnDisconnectUser = delegate { };
        public event RequestAvatarProperties OnRequestAvatarProperties = delegate { };
        public event SetAlwaysRun OnSetAlwaysRun = delegate { };
        public event TeleportLandmarkRequest OnTeleportLandmarkRequest = delegate { };
        public event DeRezObject OnDeRezObject = delegate { };
        public event Action<IClientAPI> OnRegionHandShakeReply = delegate { };
        public event GenericCall2 OnRequestWearables = delegate { };
        public event GenericCall2 OnCompleteMovementToRegion = delegate { };
        public event UpdateAgent OnAgentUpdate = delegate { };
        public event AgentRequestSit OnAgentRequestSit = delegate { };
        public event AgentSit OnAgentSit = delegate { };
        public event AvatarPickerRequest OnAvatarPickerRequest = delegate { };
        public event Action<IClientAPI> OnRequestAvatarsData = delegate { };
        public event AddNewPrim OnAddPrim = delegate { };
        public event FetchInventory OnAgentDataUpdateRequest = delegate { };
        public event TeleportLocationRequest OnSetStartLocationRequest = delegate { };
        public event RequestGodlikePowers OnRequestGodlikePowers = delegate { };
        public event GodKickUser OnGodKickUser = delegate { };
        public event ObjectDuplicate OnObjectDuplicate = delegate { };
        public event ObjectDuplicateOnRay OnObjectDuplicateOnRay = delegate { };
        public event GrabObject OnGrabObject = delegate { };
        public event DeGrabObject OnDeGrabObject = delegate { };
        public event MoveObject OnGrabUpdate = delegate { };
        public event SpinStart OnSpinStart = delegate { };
        public event SpinObject OnSpinUpdate = delegate { };
        public event SpinStop OnSpinStop = delegate { };
        public event UpdateShape OnUpdatePrimShape = delegate { };
        public event ObjectExtraParams OnUpdateExtraParams = delegate { };
        public event ObjectRequest OnObjectRequest = delegate { };
        public event ObjectSelect OnObjectSelect = delegate { };
        public event ObjectDeselect OnObjectDeselect = delegate { };
        public event GenericCall7 OnObjectDescription = delegate { };
        public event GenericCall7 OnObjectName = delegate { };
        public event GenericCall7 OnObjectClickAction = delegate { };
        public event GenericCall7 OnObjectMaterial = delegate { };
        public event RequestObjectPropertiesFamily OnRequestObjectPropertiesFamily = delegate { };
        public event UpdatePrimFlags OnUpdatePrimFlags = delegate { };
        public event UpdatePrimTexture OnUpdatePrimTexture = delegate { };
        public event UpdateVector OnUpdatePrimGroupPosition = delegate { };
        public event UpdateVector OnUpdatePrimSinglePosition = delegate { };
        public event UpdatePrimRotation OnUpdatePrimGroupRotation = delegate { };
        public event UpdatePrimSingleRotation OnUpdatePrimSingleRotation = delegate { };
        public event UpdatePrimSingleRotationPosition OnUpdatePrimSingleRotationPosition = delegate { };
        public event UpdatePrimGroupRotation OnUpdatePrimGroupMouseRotation = delegate { };
        public event UpdateVector OnUpdatePrimScale = delegate { };
        public event UpdateVector OnUpdatePrimGroupScale = delegate { };
        public event StatusChange OnChildAgentStatus = delegate { };
        public event GenericCall2 OnStopMovement = delegate { };
        public event Action<UUID> OnRemoveAvatar = delegate { };
        public event ObjectPermissions OnObjectPermissions = delegate { };
        public event CreateNewInventoryItem OnCreateNewInventoryItem = delegate { };
        public event CreateInventoryFolder OnCreateNewInventoryFolder = delegate { };
        public event UpdateInventoryFolder OnUpdateInventoryFolder = delegate { };
        public event MoveInventoryFolder OnMoveInventoryFolder = delegate { };
        public event FetchInventoryDescendents OnFetchInventoryDescendents = delegate { };
        public event PurgeInventoryDescendents OnPurgeInventoryDescendents = delegate { };
        public event FetchInventory OnFetchInventory = delegate { };
        public event RequestTaskInventory OnRequestTaskInventory = delegate { };
        public event UpdateInventoryItem OnUpdateInventoryItem = delegate { };
        public event CopyInventoryItem OnCopyInventoryItem = delegate { };
        public event MoveInventoryItem OnMoveInventoryItem = delegate { };
        public event RemoveInventoryFolder OnRemoveInventoryFolder = delegate { };
        public event RemoveInventoryItem OnRemoveInventoryItem = delegate { };
        public event UDPAssetUploadRequest OnAssetUploadRequest = delegate { };
        public event XferReceive OnXferReceive = delegate { };
        public event RequestXfer OnRequestXfer = delegate { };
        public event ConfirmXfer OnConfirmXfer = delegate { };
        public event AbortXfer OnAbortXfer = delegate { };
        public event RezScript OnRezScript = delegate { };
        public event UpdateTaskInventory OnUpdateTaskInventory = delegate { };
        public event MoveTaskInventory OnMoveTaskItem = delegate { };
        public event RemoveTaskInventory OnRemoveTaskItem = delegate { };
        public event RequestAsset OnRequestAsset = delegate { };
        public event UUIDNameRequest OnNameFromUUIDRequest = delegate { };
        public event ParcelAccessListRequest OnParcelAccessListRequest = delegate { };
        public event ParcelAccessListUpdateRequest OnParcelAccessListUpdateRequest = delegate { };
        public event ParcelPropertiesRequest OnParcelPropertiesRequest = delegate { };
        public event ParcelDivideRequest OnParcelDivideRequest = delegate { };
        public event ParcelJoinRequest OnParcelJoinRequest = delegate { };
        public event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest = delegate { };
        public event ParcelSelectObjects OnParcelSelectObjects = delegate { };
        public event ParcelObjectOwnerRequest OnParcelObjectOwnerRequest = delegate { };
        public event ParcelAbandonRequest OnParcelAbandonRequest = delegate { };
        public event ParcelGodForceOwner OnParcelGodForceOwner = delegate { };
        public event ParcelReclaim OnParcelReclaim = delegate { };
        public event ParcelReturnObjectsRequest OnParcelReturnObjectsRequest = delegate { };
        public event ParcelDeedToGroup OnParcelDeedToGroup = delegate { };
        public event RegionInfoRequest OnRegionInfoRequest = delegate { };
        public event EstateCovenantRequest OnEstateCovenantRequest = delegate { };
        public event FriendActionDelegate OnApproveFriendRequest = delegate { };
        public event FriendActionDelegate OnDenyFriendRequest = delegate { };
        public event FriendshipTermination OnTerminateFriendship = delegate { };
        public event MoneyTransferRequest OnMoneyTransferRequest = delegate { };
        public event EconomyDataRequest OnEconomyDataRequest = delegate { };
        public event MoneyBalanceRequest OnMoneyBalanceRequest = delegate { };
        public event UpdateAvatarProperties OnUpdateAvatarProperties = delegate { };
        public event ParcelBuy OnParcelBuy = delegate { };
        public event RequestPayPrice OnRequestPayPrice = delegate { };
        public event ObjectSaleInfo OnObjectSaleInfo = delegate { };
        public event ObjectBuy OnObjectBuy = delegate { };
        public event BuyObjectInventory OnBuyObjectInventory = delegate { };
        public event RequestTerrain OnRequestTerrain = delegate { };
        public event RequestTerrain OnUploadTerrain = delegate { };
        public event ObjectIncludeInSearch OnObjectIncludeInSearch = delegate { };
        public event UUIDNameRequest OnTeleportHomeRequest = delegate { };
        public event ScriptAnswer OnScriptAnswer = delegate { };
        public event AgentSit OnUndo = delegate { };
        public event ForceReleaseControls OnForceReleaseControls = delegate { };
        public event GodLandStatRequest OnLandStatRequest = delegate { };
        public event DetailedEstateDataRequest OnDetailedEstateDataRequest = delegate { };
        public event SetEstateFlagsRequest OnSetEstateFlagsRequest = delegate { };
        public event SetEstateTerrainBaseTexture OnSetEstateTerrainBaseTexture = delegate { };
        public event SetEstateTerrainDetailTexture OnSetEstateTerrainDetailTexture = delegate { };
        public event SetEstateTerrainTextureHeights OnSetEstateTerrainTextureHeights = delegate { };
        public event CommitEstateTerrainTextureRequest OnCommitEstateTerrainTextureRequest = delegate { };
        public event SetRegionTerrainSettings OnSetRegionTerrainSettings = delegate { };
        public event EstateRestartSimRequest OnEstateRestartSimRequest = delegate { };
        public event EstateChangeCovenantRequest OnEstateChangeCovenantRequest = delegate { };
        public event UpdateEstateAccessDeltaRequest OnUpdateEstateAccessDeltaRequest = delegate { };
        public event SimulatorBlueBoxMessageRequest OnSimulatorBlueBoxMessageRequest = delegate { };
        public event EstateBlueBoxMessageRequest OnEstateBlueBoxMessageRequest = delegate { };
        public event EstateDebugRegionRequest OnEstateDebugRegionRequest = delegate { };
        public event EstateTeleportOneUserHomeRequest OnEstateTeleportOneUserHomeRequest = delegate { };
        public event EstateTeleportAllUsersHomeRequest OnEstateTeleportAllUsersHomeRequest = delegate { };
        public event UUIDNameRequest OnUUIDGroupNameRequest = delegate { };
        public event RegionHandleRequest OnRegionHandleRequest = delegate { };
        public event ParcelInfoRequest OnParcelInfoRequest = delegate { };
        public event RequestObjectPropertiesFamily OnObjectGroupRequest = delegate { };
        public event ScriptReset OnScriptReset = delegate { };
        public event GetScriptRunning OnGetScriptRunning = delegate { };
        public event SetScriptRunning OnSetScriptRunning = delegate { };
        public event UpdateVector OnAutoPilotGo = delegate { };
        public event TerrainUnacked OnUnackedTerrain = delegate { };
        public event ActivateGesture OnActivateGesture = delegate { };
        public event DeactivateGesture OnDeactivateGesture = delegate { };
        public event ObjectOwner OnObjectOwner = delegate { };
        public event DirPlacesQuery OnDirPlacesQuery = delegate { };
        public event DirFindQuery OnDirFindQuery = delegate { };
        public event DirLandQuery OnDirLandQuery = delegate { };
        public event DirPopularQuery OnDirPopularQuery = delegate { };
        public event DirClassifiedQuery OnDirClassifiedQuery = delegate { };
        public event EventInfoRequest OnEventInfoRequest = delegate { };
        public event ParcelSetOtherCleanTime OnParcelSetOtherCleanTime = delegate { };
        public event MapItemRequest OnMapItemRequest = delegate { };
        public event OfferCallingCard OnOfferCallingCard = delegate { };
        public event AcceptCallingCard OnAcceptCallingCard = delegate { };
        public event DeclineCallingCard OnDeclineCallingCard = delegate { };
        public event SoundTrigger OnSoundTrigger = delegate { };
        public event StartLure OnStartLure = delegate { };
        public event TeleportLureRequest OnTeleportLureRequest = delegate { };
        public event NetworkStats OnNetworkStatsUpdate = delegate { };
        public event ClassifiedInfoRequest OnClassifiedInfoRequest = delegate { };
        public event ClassifiedInfoUpdate OnClassifiedInfoUpdate = delegate { };
        public event ClassifiedDelete OnClassifiedDelete = delegate { };
        public event ClassifiedDelete OnClassifiedGodDelete = delegate { };
        public event EventNotificationAddRequest OnEventNotificationAddRequest = delegate { };
        public event EventNotificationRemoveRequest OnEventNotificationRemoveRequest = delegate { };
        public event EventGodDelete OnEventGodDelete = delegate { };
        public event ParcelDwellRequest OnParcelDwellRequest = delegate { };
        public event UserInfoRequest OnUserInfoRequest = delegate { };
        public event UpdateUserInfo OnUpdateUserInfo = delegate { };
        public event RetrieveInstantMessages OnRetrieveInstantMessages = delegate { };
        public event PickDelete OnPickDelete = delegate { };
        public event PickGodDelete OnPickGodDelete = delegate { };
        public event PickInfoUpdate OnPickInfoUpdate = delegate { };
        public event AvatarNotesUpdate OnAvatarNotesUpdate = delegate { };
        public event MuteListRequest OnMuteListRequest = delegate { };
        public event PlacesQuery OnPlacesQuery = delegate { };


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

        public void Close(bool ShutdownCircuit)
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

        public void SendGenericMessage(string method, List<string> message)
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

        public void SendAvatarData(ulong regionHandle, string firstName, string lastName, string grouptitle, UUID avatarID, uint avatarLocalID, Vector3 Pos, byte[] textureEntry, uint parentID, Quaternion rotation)
        {
            throw new System.NotImplementedException();
        }

        public void SendAvatarTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, Vector3 position, Vector3 velocity, Quaternion rotation, UUID uuid)
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

        public void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape, Vector3 pos, Vector3 vel, Vector3 acc, Quaternion rotation, Vector3 rvel, uint flags, UUID objectID, UUID ownerID, string text, byte[] color, uint parentID, byte[] particleSystem, byte clickAction, byte material, byte[] textureanim, bool attachment, uint AttachPoint, UUID AssetId, UUID SoundId, double SoundVolume, byte SoundFlags, double SoundRadius)
        {
            throw new System.NotImplementedException();
        }

        public void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID, PrimitiveBaseShape primShape, Vector3 pos, Vector3 vel, Vector3 acc, Quaternion rotation, Vector3 rvel, uint flags, UUID objectID, UUID ownerID, string text, byte[] color, uint parentID, byte[] particleSystem, byte clickAction, byte material)
        {
            throw new System.NotImplementedException();
        }

        public void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 rotationalvelocity, byte state, UUID AssetId, UUID owner, int attachPoint)
        {
            throw new System.NotImplementedException();
        }

        public void FlushPrimUpdates()
        {
            throw new System.NotImplementedException();
        }

        public void SendInventoryFolderDetails(UUID ownerID, UUID folderID, List<InventoryItemBase> items, List<InventoryFolderBase> folders, bool fetchFolders, bool fetchItems)
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

        public void SendEstateManagersList(UUID invoice, UUID[] EstateManagers, uint estateID)
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
            return null;
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

        public void Terminate()
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

        #endregion

        public void SendRebakeAvatarTextures(UUID textureID)
        {
        }
    }
}
