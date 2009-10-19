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
using System.Text;
using log4net;
using MXP;
using MXP.Messages;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Client;
using Packet=OpenMetaverse.Packets.Packet;
using MXP.Extentions.OpenMetaverseFragments.Proto;
using MXP.Util;
using MXP.Fragments;
using MXP.Common.Proto;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Client.MXP.ClientStack
{
    public class MXPClientView : IClientAPI, IClientCore
    {
        internal static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region Constants
        private Vector3 FORWARD = new Vector3(1, 0, 0);
        private Vector3 BACKWARD = new Vector3(-1, 0, 0);
        private Vector3 LEFT = new Vector3(0, 1, 0);
        private Vector3 RIGHT = new Vector3(0, -1, 0);
        private Vector3 UP = new Vector3(0, 0, 1);
        private Vector3 DOWN = new Vector3(0, 0, -1);
        #endregion

        #region Fields
        private readonly Session m_session;
        private readonly UUID m_sessionID;
        private readonly UUID m_userID;
        private readonly IScene m_scene;
        private readonly string m_firstName;
        private readonly string m_lastName;
        private int m_objectsToSynchronize = 0;
        private int m_objectsSynchronized = -1;

        private Vector3 m_startPosition=new Vector3(128f, 128f, 128f);
        #endregion

        #region Properties

        public Session Session
        {
            get { return m_session; }
        }

        public Vector3 StartPos
        {
            get { return m_startPosition; }
            set { m_startPosition = value; }
        }

        public UUID AgentId
        {
            get { return m_userID; }
        }

        public UUID SessionId
        {
            get { return m_sessionID; }
        }

        public UUID SecureSessionId
        {
            get { return m_sessionID; }
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
            get { return m_firstName; }
        }

        public string LastName
        {
            get { return m_lastName; }
        }

        public IScene Scene
        {
            get { return m_scene; }
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

        #endregion

        #region Constructors
        public MXPClientView(Session mxpSession, UUID mxpSessionID, UUID userID, IScene mxpHostBubble, string mxpFirstName, string mxpLastName)
        {
            this.m_session = mxpSession;
            this.m_userID = userID;
            this.m_firstName = mxpFirstName;
            this.m_lastName = mxpLastName;
            this.m_scene = mxpHostBubble;
            this.m_sessionID = mxpSessionID;
        }
        #endregion

        #region MXP Incoming Message Processing

        public void MXPPRocessMessage(Message message)
        {
            if (message.GetType() == typeof(ModifyRequestMessage))
            {
                MXPProcessModifyRequest((ModifyRequestMessage)message);
            }
            else
            {
                m_log.Warn("[MXP ClientStack] Received messaged unhandled: " + message);
            }
        }

        private void MXPProcessModifyRequest(ModifyRequestMessage modifyRequest)
        {
            ObjectFragment objectFragment=modifyRequest.ObjectFragment;
            if (objectFragment.ObjectId == m_userID.Guid)
            {
                OmAvatarExt avatarExt = modifyRequest.GetExtension<OmAvatarExt>();

                AgentUpdateArgs agentUpdate = new AgentUpdateArgs();
                agentUpdate.AgentID = new UUID(objectFragment.ObjectId);
                agentUpdate.SessionID = m_sessionID;
                agentUpdate.State = (byte)avatarExt.State;

                Quaternion avatarOrientation = FromOmQuaternion(objectFragment.Orientation);
                if (avatarOrientation.X == 0 && avatarOrientation.Y == 0 && avatarOrientation.Z == 0 && avatarOrientation.W == 0)
                {
                    avatarOrientation = Quaternion.Identity;
                }
                Vector3 avatarLocation=FromOmVector(objectFragment.Location);

                if (avatarExt.MovementDirection != null)
                {
                    Vector3 direction = FromOmVector(avatarExt.MovementDirection);

                    direction = direction * Quaternion.Inverse(avatarOrientation);

                    if ((direction - FORWARD).Length() < 0.5)
                    {
                        agentUpdate.ControlFlags += (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_POS;
                    }
                    if ((direction - BACKWARD).Length() < 0.5)
                    {
                        agentUpdate.ControlFlags += (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG;
                    }
                    if ((direction - LEFT).Length() < 0.5)
                    {
                        agentUpdate.ControlFlags += (uint)AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS;
                    }
                    if ((direction - RIGHT).Length() < 0.5)
                    {
                        agentUpdate.ControlFlags += (uint)AgentManager.ControlFlags.AGENT_CONTROL_LEFT_NEG;
                    }
                    if ((direction - UP).Length() < 0.5)
                    {
                        agentUpdate.ControlFlags += (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_POS;
                    }
                    if ((direction - DOWN).Length() < 0.5)
                    {
                        agentUpdate.ControlFlags += (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG;
                    }

                }
                if (avatarExt.TargetOrientation != null)
                {
                    agentUpdate.BodyRotation = FromOmQuaternion(avatarExt.TargetOrientation);
                }
                else
                {
                    agentUpdate.BodyRotation = FromOmQuaternion(objectFragment.Orientation);
                }

                if (avatarExt.Body != null)
                {
                    foreach (OmBipedBoneOrientation boneOrientation in avatarExt.Body.BipedBoneOrientations)
                    {
                        if (boneOrientation.Bone == OmBipedBones.Head)
                        {
                            agentUpdate.HeadRotation = FromOmQuaternion(boneOrientation.Orientation);
                        }
                    }
                }
                else
                {
                    agentUpdate.HeadRotation = Quaternion.Identity;
                }

                if (avatarExt.Camera != null)
                {
                    Quaternion cameraOrientation = FromOmQuaternion(avatarExt.Camera.Orientation);
                    agentUpdate.CameraCenter = FromOmVector(avatarExt.Camera.Location);
                    agentUpdate.CameraAtAxis = FORWARD * cameraOrientation;
                    agentUpdate.CameraLeftAxis = LEFT * cameraOrientation;
                    agentUpdate.CameraUpAxis = UP * cameraOrientation;
                }
                else
                {
                    agentUpdate.CameraCenter = avatarLocation;
                    agentUpdate.CameraAtAxis = FORWARD * avatarOrientation;
                    agentUpdate.CameraLeftAxis = LEFT * avatarOrientation;
                    agentUpdate.CameraUpAxis = UP * avatarOrientation;
                }

                OnAgentUpdate(this, agentUpdate);

                ModifyResponseMessage modifyResponse = new ModifyResponseMessage();
                modifyResponse.FailureCode = MxpResponseCodes.SUCCESS;
                modifyResponse.RequestMessageId = modifyRequest.MessageId;
                m_session.Send(modifyResponse);
            }
            else
            {
                ModifyResponseMessage modifyResponse = new ModifyResponseMessage();
                modifyResponse.FailureCode = MxpResponseCodes.UNAUTHORIZED_OPERATION;
                modifyResponse.RequestMessageId = modifyRequest.MessageId;
                m_session.Send(modifyResponse);
            }
        }

        #endregion

        #region MXP Outgoing Message Processing

        private void MXPSendPrimitive(uint localID, UUID ownerID, Vector3 acc, Vector3 rvel, PrimitiveBaseShape primShape, Vector3 pos, UUID objectID, Vector3 vel, Quaternion rotation, uint flags, string text, byte[] textColor, uint parentID, byte[] particleSystem, byte clickAction, byte material, byte[] textureanim)
        {
            String typeName = ToOmType(primShape.PCode);
            m_log.Info("[MXP ClientStack] Transmitting Primitive" + typeName);

            PerceptionEventMessage pe = new PerceptionEventMessage();
            pe.ObjectFragment.ObjectId = objectID.Guid;

            pe.ObjectFragment.ParentObjectId = Guid.Empty;

            // Resolving parent UUID.
            OpenSim.Region.Framework.Scenes.Scene scene = (OpenSim.Region.Framework.Scenes.Scene)Scene;
            if (scene.Entities.ContainsKey(parentID))
            {
                pe.ObjectFragment.ParentObjectId = scene.Entities[parentID].UUID.Guid;
            }

            pe.ObjectFragment.ObjectIndex = localID;
            pe.ObjectFragment.ObjectName = typeName + " Object";
            pe.ObjectFragment.OwnerId = ownerID.Guid;
            pe.ObjectFragment.TypeId = Guid.Empty;
            pe.ObjectFragment.TypeName = typeName;
            pe.ObjectFragment.Acceleration = ToOmVector(acc);
            pe.ObjectFragment.AngularAcceleration=new MsdQuaternion4f();
            pe.ObjectFragment.AngularVelocity = ToOmQuaternion(rvel);
            pe.ObjectFragment.BoundingSphereRadius = primShape.Scale.Length();

            pe.ObjectFragment.Location = ToOmVector(pos);

            pe.ObjectFragment.Mass = 1.0f;
            pe.ObjectFragment.Orientation =  ToOmQuaternion(rotation);
            pe.ObjectFragment.Velocity =ToOmVector(vel);

            OmSlPrimitiveExt ext = new OmSlPrimitiveExt();

            if (!((primShape.PCode == (byte)PCode.NewTree) || (primShape.PCode == (byte)PCode.Tree) || (primShape.PCode == (byte)PCode.Grass)))
            {

                ext.PathBegin = primShape.PathBegin;
                ext.PathEnd = primShape.PathEnd;
                ext.PathScaleX = primShape.PathScaleX;
                ext.PathScaleY = primShape.PathScaleY;
                ext.PathShearX = primShape.PathShearX;
                ext.PathShearY = primShape.PathShearY;
                ext.PathSkew = primShape.PathSkew;
                ext.ProfileBegin = primShape.ProfileBegin;
                ext.ProfileEnd = primShape.ProfileEnd;
                ext.PathCurve = primShape.PathCurve;
                ext.ProfileCurve = primShape.ProfileCurve;
                ext.ProfileHollow = primShape.ProfileHollow;
                ext.PathRadiusOffset = primShape.PathRadiusOffset;
                ext.PathRevolutions = primShape.PathRevolutions;
                ext.PathTaperX = primShape.PathTaperX;
                ext.PathTaperY = primShape.PathTaperY;
                ext.PathTwist = primShape.PathTwist;
                ext.PathTwistBegin = primShape.PathTwistBegin;


            }

            ext.UpdateFlags = flags;
            ext.ExtraParams = primShape.ExtraParams;
            ext.State = primShape.State;
            ext.TextureEntry = primShape.TextureEntry;
            ext.TextureAnim = textureanim;
            ext.Scale = ToOmVector(primShape.Scale);
            ext.Text = text;
            ext.TextColor = ToOmColor(textColor);
            ext.PSBlock = particleSystem;
            ext.ClickAction = clickAction;
            ext.Material = material;

            pe.SetExtension<OmSlPrimitiveExt>(ext);

            Session.Send(pe);

            if (m_objectsSynchronized != -1)
            {
                m_objectsSynchronized++;

                if (m_objectsToSynchronize >= m_objectsSynchronized)
                {
                    SynchronizationEndEventMessage synchronizationEndEventMessage = new SynchronizationEndEventMessage();
                    Session.Send(synchronizationEndEventMessage);
                    m_objectsSynchronized = -1;
                }
            }
        }

        public void MXPSendAvatarData(string participantName, UUID ownerID, UUID parentId, UUID avatarID, uint avatarLocalID, Vector3 position, Quaternion rotation)
        {
            m_log.Info("[MXP ClientStack] Transmitting Avatar Data " + participantName);

            PerceptionEventMessage pe = new PerceptionEventMessage();

            pe.ObjectFragment.ObjectId = avatarID.Guid;
            pe.ObjectFragment.ParentObjectId = parentId.Guid;
            pe.ObjectFragment.ObjectIndex = avatarLocalID;
            pe.ObjectFragment.ObjectName = participantName;
            pe.ObjectFragment.OwnerId = ownerID.Guid;
            pe.ObjectFragment.TypeId = Guid.Empty;
            pe.ObjectFragment.TypeName = "Avatar";
            pe.ObjectFragment.Acceleration = new MsdVector3f();
            pe.ObjectFragment.AngularAcceleration = new MsdQuaternion4f();
            pe.ObjectFragment.AngularVelocity = new MsdQuaternion4f();

            pe.ObjectFragment.BoundingSphereRadius = 1.0f; // TODO Fill in appropriate value

            pe.ObjectFragment.Location = ToOmVector(position);

            pe.ObjectFragment.Mass = 1.0f; // TODO Fill in appropriate value
            pe.ObjectFragment.Orientation = ToOmQuaternion(rotation);
            pe.ObjectFragment.Velocity = new MsdVector3f();

            Session.Send(pe);
        }

        public void MXPSendTerrain(float[] map)
        {
            m_log.Info("[MXP ClientStack] Transmitting terrain for " + m_scene.RegionInfo.RegionName);

            PerceptionEventMessage pe = new PerceptionEventMessage();

            // Hacking terrain object uuid to zero and index to hashcode of regionuuid
            pe.ObjectFragment.ObjectId = m_scene.RegionInfo.RegionSettings.RegionUUID.Guid;
            pe.ObjectFragment.ObjectIndex = (uint)(m_scene.RegionInfo.RegionSettings.RegionUUID.GetHashCode() + ((long)int.MaxValue) / 2);
            pe.ObjectFragment.ParentObjectId = UUID.Zero.Guid;
            pe.ObjectFragment.ObjectName = "Terrain of " + m_scene.RegionInfo.RegionName;
            pe.ObjectFragment.OwnerId = m_scene.RegionInfo.MasterAvatarAssignedUUID.Guid;
            pe.ObjectFragment.TypeId = Guid.Empty;
            pe.ObjectFragment.TypeName = "Terrain";
            pe.ObjectFragment.Acceleration = new MsdVector3f();
            pe.ObjectFragment.AngularAcceleration = new MsdQuaternion4f();
            pe.ObjectFragment.AngularVelocity = new MsdQuaternion4f();
            pe.ObjectFragment.BoundingSphereRadius = 128f;

            pe.ObjectFragment.Location = new MsdVector3f();

            pe.ObjectFragment.Mass = 1.0f;
            pe.ObjectFragment.Orientation = new MsdQuaternion4f();
            pe.ObjectFragment.Velocity = new MsdVector3f();

            OmBitmapTerrainExt terrainExt = new OmBitmapTerrainExt();
            terrainExt.Width = 256;
            terrainExt.Height = 256;
            terrainExt.WaterLevel = (float) m_scene.RegionInfo.RegionSettings.WaterHeight;
            terrainExt.Offset = 0;
            terrainExt.Scale = 10;
            terrainExt.HeightMap = CompressUtil.CompressHeightMap(map, 0, 10);

            pe.SetExtension<OmBitmapTerrainExt>(terrainExt);

            Session.Send(pe);
        }

        public void MXPSendSynchronizationBegin(int objectCount)
        {
            m_objectsToSynchronize = objectCount;
            m_objectsSynchronized = 0;
            SynchronizationBeginEventMessage synchronizationBeginEventMessage = new SynchronizationBeginEventMessage();
            synchronizationBeginEventMessage.ObjectCount = (uint)objectCount;
            Session.Send(synchronizationBeginEventMessage);
        }

        #endregion

        #region MXP Conversions

        private MsdVector3f ToOmVector(Vector3 value)
        {
            MsdVector3f encodedValue = new MsdVector3f();
            encodedValue.X = value.X;
            encodedValue.Y = value.Y;
            encodedValue.Z = value.Z;
            return encodedValue;
        }

        private MsdQuaternion4f ToOmQuaternion(Vector3 value)
        {
            Quaternion quaternion=Quaternion.CreateFromEulers(value);
            MsdQuaternion4f encodedValue = new MsdQuaternion4f();
            encodedValue.X = quaternion.X;
            encodedValue.Y = quaternion.Y;
            encodedValue.Z = quaternion.Z;
            encodedValue.W = quaternion.W;
            return encodedValue;
        }

        private MsdQuaternion4f ToOmQuaternion(Quaternion value)
        {
            MsdQuaternion4f encodedValue = new MsdQuaternion4f();
            encodedValue.X = value.X;
            encodedValue.Y = value.Y;
            encodedValue.Z = value.Z;
            encodedValue.W = value.W;
            return encodedValue;
        }

        private Vector3 FromOmVector(MsdVector3f vector)
        {
            return new Vector3(vector.X, vector.Y, vector.Z);
        }

//        private Vector3 FromOmVector(float[] vector)
//        {
//            return new Vector3(vector[0], vector[1], vector[2]);
//        }

        private Quaternion FromOmQuaternion(MsdQuaternion4f quaternion)
        {
            return new Quaternion(quaternion.X, quaternion.Y, quaternion.Z, quaternion.W);
        }

//        private Quaternion FromOmQuaternion(float[] quaternion)
//        {
//            return new Quaternion(quaternion[0], quaternion[1], quaternion[2], quaternion[3]);
//        }

        private MsdColor4f ToOmColor(byte[] value)
        {
            MsdColor4f encodedValue = new MsdColor4f();
            encodedValue.R = value[0];
            encodedValue.G = value[1];
            encodedValue.B = value[2];
            encodedValue.A = value[3];
            return encodedValue;
        }

        private string ToOmType(byte value)
        {
            if (value == (byte)PCodeEnum.Avatar)
            {
                return "Avatar";
            }
            if (value == (byte)PCodeEnum.Grass)
            {
                return "Grass";
            }
            if (value == (byte)PCodeEnum.NewTree)
            {
                return "NewTree";
            }
            if (value == (byte)PCodeEnum.ParticleSystem)
            {
                return "ParticleSystem";
            }
            if (value == (byte)PCodeEnum.Primitive)
            {
                return "Primitive";
            }
            if (value == (byte)PCodeEnum.Tree)
            {
                return "Tree";
            }
            throw new Exception("Unsupported PCode value: " + value);
        }

        #endregion

        #region OpenSim Event Handlers

        #pragma warning disable 67
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
        public event UpdatePrimSingleRotationPosition OnUpdatePrimSingleRotationPosition;
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
        public event ViewerEffectEventHandler OnViewerEffect;
        public event Action<IClientAPI> OnLogout;
        public event Action<IClientAPI> OnConnectionClosed;
        public event RetrieveInstantMessages OnRetrieveInstantMessages;
        public event PickDelete OnPickDelete;
        public event PickGodDelete OnPickGodDelete;
        public event PickInfoUpdate OnPickInfoUpdate;
        public event AvatarNotesUpdate OnAvatarNotesUpdate;
        public event MuteListRequest OnMuteListRequest;
        public event AvatarInterestUpdate OnAvatarInterestUpdate;

        public event PlacesQuery OnPlacesQuery;

        #pragma warning restore 67

        #endregion

        #region OpenSim ClientView Public Methods
        // Do we need this?
        public bool SendLogoutPacketWhenClosing
        {
            set { }
        }

        public uint CircuitCode
        {
            get { return m_sessionID.CRC(); }
        }

        public IPEndPoint RemoteEndPoint
        {
            get { return Session.RemoteEndPoint; }
        }

        public void SetDebugPacketLevel(int newDebug)
        {
            //m_debugLevel = newDebug;
        }

        public void InPacket(object NewPack)
        {
            //throw new System.NotImplementedException();
        }

        public void ProcessInPacket(Packet NewPack)
        {
            //throw new System.NotImplementedException();
        }

        public void OnClean()
        {
            if (OnLogout != null)
                OnLogout(this);

            if (OnConnectionClosed != null)
                OnConnectionClosed(this);
        }

        public void Close()
        {
            m_log.Info("[MXP ClientStack] Close Called");

            // Tell the client to go
            SendLogoutPacket();

            // Let MXPPacketServer clean it up
            if (Session.SessionState != SessionState.Disconnected)
            {
                Session.SetStateDisconnected();
            }

        }

        public void Kick(string message)
        {
            Close();
        }

        public void Start()
        {
            Scene.AddNewClient(this);

            // Mimicking LLClientView which gets always set appearance from client.
            OpenSim.Region.Framework.Scenes.Scene scene=(OpenSim.Region.Framework.Scenes.Scene)Scene;
            AvatarAppearance appearance;
            scene.GetAvatarAppearance(this,out appearance);
            OnSetAppearance(appearance.Texture, (byte[])appearance.VisualParams.Clone());
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
            m_log.Info("[MXP ClientStack] Completing Handshake to Region");

            if (OnRegionHandShakeReply != null)
            {
                OnRegionHandShakeReply(this);
            }

            if (OnCompleteMovementToRegion != null)
            {
                OnCompleteMovementToRegion();
            }

            // Need to translate to MXP somehow
        }

        public void SendChatMessage(string message, byte type, Vector3 fromPos, string fromName, UUID fromAgentID, byte source, byte audible)
        {
            ActionEventMessage chatActionEvent = new ActionEventMessage();
            chatActionEvent.ActionFragment.ActionName = "Chat";
            chatActionEvent.ActionFragment.SourceObjectId = fromAgentID.Guid;
            chatActionEvent.ActionFragment.ObservationRadius = 180.0f;
            chatActionEvent.ActionFragment.ExtensionDialect = "TEXT";
            chatActionEvent.SetPayloadData(Util.UTF8.GetBytes(message));

            Session.Send(chatActionEvent);
        }

        public void SendInstantMessage(GridInstantMessage im)
        {
            // Need to translate to MXP somehow
        }

        public void SendGenericMessage(string method, List<string> message)
        {
            // Need to translate to MXP somehow
        }

        public void SendLayerData(float[] map)
        {
            MXPSendTerrain(map);
        }

        public void SendLayerData(int px, int py, float[] map)
        {
        }

        public void SendWindData(Vector2[] windSpeeds)
        {
            // Need to translate to MXP somehow
        }

        public void SendCloudData(float[] cloudCover)
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

        public void SendAvatarData(SendAvatarData data)
        {
            //ScenePresence presence=((Scene)this.Scene).GetScenePresence(avatarID);
            UUID ownerID = data.AvatarID;
            MXPSendAvatarData(data.FirstName + " " + data.LastName, ownerID, UUID.Zero, data.AvatarID, data.AvatarLocalID, data.Position, data.Rotation);
        }

        public void SendAvatarTerseUpdate(SendAvatarTerseData data)
        {
            MovementEventMessage me = new MovementEventMessage();
            me.ObjectIndex = data.LocalID;
            me.Location = ToOmVector(data.Position);
            me.Orientation = ToOmQuaternion(data.Rotation);

            Session.Send(me);
        }

        public void SendCoarseLocationUpdate(List<UUID> users, List<Vector3> CoarseLocations)
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

        public void SendPrimitiveToClient(SendPrimitiveData data)
        {
            MXPSendPrimitive(data.localID, data.ownerID, data.acc, data.rvel, data.primShape, data.pos, data.objectID, data.vel,
                data.rotation, (uint)data.flags, data.text, data.color, data.parentID, data.particleSystem, data.clickAction,
                data.material, data.textureanim);
        }

        public void SendPrimTerseUpdate(SendPrimitiveTerseData data)
        {
            MovementEventMessage me = new MovementEventMessage();
            me.ObjectIndex = data.LocalID;
            me.Location = ToOmVector(data.Position);
            me.Orientation = ToOmQuaternion(data.Rotation);
            Session.Send(me);
        }

        public void ReprioritizeUpdates(StateUpdateTypes type, UpdatePriorityHandler handler)
        {
        }

        public void FlushPrimUpdates()
        {
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

        public void SendDialog(string objectname, UUID objectID, string ownerFirstName, string ownerLastName, string msg, UUID textureID, int ch, string[] buttonlabels)
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

        public void SendLandObjectOwners(LandData land, List<UUID> groups, Dictionary<UUID, int> ownersAndCount)
        {
            // Need to translate to MXP somehow
        }

        public void SendCameraConstraint(Vector4 ConstraintPlane)
        {

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

        public void SendBlueBoxMessage(UUID FromAvatarID, string FromAvatarName, string Message)
        {
            SendChatMessage(Message, 0, Vector3.Zero, FromAvatarName, UUID.Zero, 0, 0);
        }

        public void SendLogoutPacket()
        {
            LeaveRequestMessage lrm = new LeaveRequestMessage();
            Session.Send(lrm);
        }

        public EndPoint GetClientEP()
        {
            return null;
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
            Close();
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

        public void Disconnect(string reason)
        {
            Kick(reason);
            Close();
        }

        public void Disconnect()
        {
            Close();
        }

        #endregion
    
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
        
        public void SendRebakeAvatarTextures(UUID textureID)
        {
        }
    }
}
