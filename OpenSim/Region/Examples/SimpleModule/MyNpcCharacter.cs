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

namespace OpenSim.Region.Examples.SimpleModule
{
    public class MyNpcCharacter : IClientAPI
    {
        private uint movementFlag = 0;
        private short flyState = 0;
        private LLQuaternion bodyDirection = LLQuaternion.Identity;
        private short count = 0;
        private short frame = 0;

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
        public event ObjectAttach OnObjectAttach;
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
        public event RemoveTaskInventory OnRemoveTaskItem;
        public event RequestAsset OnRequestAsset;

        public event UUIDNameRequest OnNameFromUUIDRequest;

        public event ParcelPropertiesRequest OnParcelPropertiesRequest;
        public event ParcelDivideRequest OnParcelDivideRequest;
        public event ParcelJoinRequest OnParcelJoinRequest;
        public event ParcelPropertiesUpdateRequest OnParcelPropertiesUpdateRequest;

        public event ParcelAccessListRequest OnParcelAccessListRequest;
        public event ParcelAccessListUpdateRequest OnParcelAccessListUpdateRequest;
        public event ParcelSelectObjects OnParcelSelectObjects;
        public event ParcelObjectOwnerRequest OnParcelObjectOwnerRequest;
        public event ObjectDeselect OnObjectDeselect;
        public event EstateOwnerMessageRequest OnEstateOwnerMessage;
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


#pragma warning restore 67

        private LLUUID myID = LLUUID.Random();

        public MyNpcCharacter(EventManager eventManager)
        {
            // startPos = new LLVector3(128, (float)(Util.RandomClass.NextDouble()*100), 2);
            eventManager.OnFrame += Update;
        }

        private LLVector3 startPos = new LLVector3(128, 128, 2);

        public virtual LLVector3 StartPos
        {
            get { return startPos; }
            set { }
        }

        public virtual LLUUID AgentId
        {
            get { return myID; }
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

        public virtual void SendAvatarPickerReply(AvatarPickerReplyPacket response)
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
                                            LLUUID fromAgentID)
        {
        }

        public virtual void SendChatMessage(byte[] message, byte type, LLVector3 fromPos, string fromName,
                                            LLUUID fromAgentID)
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

        public virtual void SendMapBlock(List<MapBlockData> mapBlocks)
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
                                                  PrimitiveBaseShape primShape, LLVector3 pos, uint flags,
                                                  LLUUID objectID, LLUUID ownerID, string text, byte[] color,
                                                  uint parentID,
                                                  byte[] particleSystem, LLQuaternion rotation, byte clickAction)
        {
        }
        public virtual void SendPrimitiveToClient(ulong regionHandle, ushort timeDilation, uint localID,
                                                  PrimitiveBaseShape primShape, LLVector3 pos, uint flags,
                                                  LLUUID objectID, LLUUID ownerID, string text, byte[] color,
                                                  uint parentID,
                                                  byte[] particleSystem, LLQuaternion rotation, byte clickAction, byte[] textureanimation)
        {
        }
        public virtual void SendPrimTerseUpdate(ulong regionHandle, ushort timeDilation, uint localID,
                                                LLVector3 position, LLQuaternion rotation, byte state)
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

        public virtual void SendRegionHandshake(RegionInfo regionInfo)
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

        private void Update()
        {
            frame++;
            if (frame > 20)
            {
                frame = 0;
                if (OnAgentUpdate != null)
                {
                    AgentUpdatePacket pack = new AgentUpdatePacket();
                    pack.AgentData.ControlFlags = movementFlag;
                    pack.AgentData.BodyRotation = bodyDirection;
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
                    if (OnChatFromViewer != null)
                    {
                        ChatFromViewerArgs args = new ChatFromViewerArgs();
                        args.Message = "Hey You! Get out of my Home. This is my Region";
                        args.Channel = 0;
                        args.From = FirstName + " " + LastName;
                        args.Position = new LLVector3(128, 128, 26);
                        args.Sender = this;
                        args.Type = ChatTypeEnum.Shout;

                        OnChatFromViewer(this, args);
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

        public void SendSunPos(LLVector3 sunPos, LLVector3 sunVel)
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
    }
}
