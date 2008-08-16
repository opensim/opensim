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
using System.Reflection;
using System.Runtime.Serialization;
using System.Security.Permissions;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Environment.Types;
using OpenSim.Region.Physics.Manager;


namespace OpenSim.Region.Environment.Scenes
{
    enum ScriptControlled : int
    {
        CONTROL_ZERO = 0,
        CONTROL_FWD = 1,
        CONTROL_BACK = 2,
        CONTROL_LEFT = 4,
        CONTROL_RIGHT = 8,
        CONTROL_UP = 16,
        CONTROL_DOWN = 32,
        CONTROL_ROT_LEFT = 256,
        CONTROL_ROT_RIGHT = 512,
        CONTROL_LBUTTON = 268435456,
        CONTROL_ML_LBUTTON = 1073741824
    }

    struct ScriptControllers
    {
        public LLUUID itemID;
        public uint objID;
        public ScriptControlled ignoreControls;
        public ScriptControlled eventControls;
    }

    [Serializable]
    public class ScenePresence : EntityBase, ISerializable
    {
//        ~ScenePresence()
//        {
//            System.Console.WriteLine("[ScenePresence] Destructor called");
//        }

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static byte[] DefaultTexture;

        public LLUUID currentParcelUUID = LLUUID.Zero;
        private AnimationSet m_animations = new AnimationSet();
        private Dictionary<LLUUID, ScriptControllers> scriptedcontrols = new Dictionary<LLUUID, ScriptControllers>();
        private ScriptControlled IgnoredControls = ScriptControlled.CONTROL_ZERO;
        private ScriptControlled LastCommands = ScriptControlled.CONTROL_ZERO;
        private SceneObjectGroup proxyObjectGroup = null;
        //private SceneObjectPart proxyObjectPart = null;

        public Vector3 lastKnownAllowedPosition = new Vector3();
        public bool sentMessageAboutRestrictedParcelFlyingDown = false;

        private bool m_updateflag = false;
        private byte m_movementflag = 0;
        private readonly List<NewForce> m_forcesList = new List<NewForce>();
        private short m_updateCount = 0;
        private uint m_requestedSitTargetID = 0;
        private LLUUID m_requestedSitTargetUUID = LLUUID.Zero;

        private LLVector3 m_requestedSitOffset = new LLVector3();

        private LLVector3 m_LastFinitePos = new LLVector3();

        private float m_sitAvatarHeight = 2.0f;

        // experimentally determined "fudge factor" to make sit-target positions
        // the same as in SecondLife. Fudge factor was tested for 36 different
        // test cases including prims of type box, sphere, cylinder, and torus,
        // with varying parameters for sit target location, prim size, prim
        // rotation, prim cut, prim twist, prim taper, and prim shear. See mantis
        // issue #1716
        private static readonly LLVector3 m_sitTargetCorrectionOffset = new LLVector3(0.1f, 0.0f, 0.3f);
        private float m_godlevel = 0;

        private bool m_attachmentsTransported = false;

        private bool m_invulnerable = true;

        private LLVector3 m_LastChildAgentUpdatePosition = new LLVector3();

        private int m_perfMonMS = 0;

        private bool m_setAlwaysRun = false;

        private Quaternion m_bodyRot= Quaternion.Identity;

        public bool IsRestrictedToRegion = false;

        public string JID = string.Empty;

        // Agent moves with a PID controller causing a force to be exerted.
        private bool m_newForce = false;
        private bool m_newCoarseLocations = true;
        private float m_health = 100f;

        private LLVector3 m_lastVelocity = LLVector3.Zero;

        // Default AV Height
        private float m_avHeight = 127.0f;

        protected RegionInfo m_regionInfo;
        protected ulong crossingFromRegion = 0;

        private readonly Vector3[] Dir_Vectors = new Vector3[6];
        private LLVector3 lastPhysPos = new LLVector3();

        // Position of agent's camera in world (region cordinates)
        protected Vector3 m_CameraCenter = new Vector3(0, 0, 0);

        // Use these three vectors to figure out what the agent is looking at
        // Convert it to a Matrix and/or Quaternion
        protected Vector3 m_CameraAtAxis = new Vector3(0, 0, 0);
        protected Vector3 m_CameraLeftAxis = new Vector3(0, 0, 0);
        protected Vector3 m_CameraUpAxis = new Vector3(0, 0, 0);
        private uint m_AgentControlFlags = (uint) 0;
        private LLQuaternion m_headrotation = new LLQuaternion();
        private byte m_state = (byte) 0;

        //Reuse the LLVector3 instead of creating a new one on the UpdateMovement method
        private LLVector3 movementvector = new LLVector3();

        private bool m_autopilotMoving = false;
        private LLVector3 m_autoPilotTarget = LLVector3.Zero;
        private bool m_sitAtAutoTarget = false;

        // Agent's Draw distance.
        protected float m_DrawDistance = 0f;

        protected AvatarAppearance m_appearance;

        protected List<SceneObjectGroup> m_attachments = new List<SceneObjectGroup>();

        //neighbouring regions we have enabled a child agent in
        private readonly List<ulong> m_knownChildRegions = new List<ulong>();

        /// <summary>
        /// Implemented Control Flags
        /// </summary>
        private enum Dir_ControlFlags
        {
            DIR_CONTROL_FLAG_FORWARD = AgentManager.ControlFlags.AGENT_CONTROL_AT_POS,
            DIR_CONTROL_FLAG_BACK = AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG,
            DIR_CONTROL_FLAG_LEFT = AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS,
            DIR_CONTROL_FLAG_RIGHT = AgentManager.ControlFlags.AGENT_CONTROL_LEFT_NEG,
            DIR_CONTROL_FLAG_UP = AgentManager.ControlFlags.AGENT_CONTROL_UP_POS,
            DIR_CONTROL_FLAG_DOWN = AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG,
            DIR_CONTROL_FLAG_DOWN_NUDGE = AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_NEG
        }

        /// <summary>
        /// Position at which a significant movement was made
        /// </summary>
        private LLVector3 posLastSignificantMove = new LLVector3();

        private UpdateQueue m_partsUpdateQueue = new UpdateQueue();
        private Queue<SceneObjectGroup> m_pendingObjects = null;

        private Dictionary<LLUUID, ScenePartUpdate> m_updateTimes = new Dictionary<LLUUID, ScenePartUpdate>();

        #region Properties

        /// <summary>
        /// Physical scene representation of this Avatar.
        /// </summary>
        public PhysicsActor PhysicsActor
        {
            set { m_physicsActor = value; }
            get { return m_physicsActor; }
        }

        public byte MovementFlag
        {
            set { m_movementflag = value; }
            get { return m_movementflag; }
        }

        public bool Updated
        {
            set { m_updateflag = value; }
            get { return m_updateflag; }
        }

        public bool Invulnerable
        {
            set { m_invulnerable = value; }
            get { return m_invulnerable; }
        }

        public float GodLevel
        {
            get { return m_godlevel; }
        }

        private readonly ulong m_regionHandle;

        public ulong RegionHandle
        {
            get { return m_regionHandle; }
        }

        public Vector3 CameraPosition
        {
            get { return m_CameraCenter; }
        }

        private readonly string m_firstname;

        public string Firstname
        {
            get { return m_firstname; }
        }

        private readonly string m_lastname;

        public string Lastname
        {
            get { return m_lastname; }
        }

        public float DrawDistance
        {
            get { return m_DrawDistance; }
        }

        protected bool m_allowMovement = true;

        public bool AllowMovement
        {
            get { return m_allowMovement; }
            set { m_allowMovement = value; }
        }

        /// <summary>
        /// This works out to be the ClientView object associated with this avatar, or it's client connection manager
        /// </summary>
        private IClientAPI m_controllingClient;

        protected PhysicsActor m_physicsActor;

        public IClientAPI ControllingClient
        {
            get { return m_controllingClient; }
            set { m_controllingClient = value; }
        }

        protected LLVector3 m_parentPosition = new LLVector3();

        /// <summary>
        /// Absolute position of this avatar in 'region cordinates'
        /// </summary>
        public override LLVector3 AbsolutePosition
        {
            get
            {
                if (m_physicsActor != null)
                {
                    m_pos.X = m_physicsActor.Position.X;
                    m_pos.Y = m_physicsActor.Position.Y;
                    m_pos.Z = m_physicsActor.Position.Z;
                }

                return m_parentPosition + m_pos;
            }
            set
            {
                if (m_physicsActor != null)
                {
                    try
                    {
                        lock (m_scene.SyncRoot)
                        {
                            m_physicsActor.Position = new PhysicsVector(value.X, value.Y, value.Z);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }

                m_pos = value;
                m_parentPosition=new LLVector3(0, 0, 0);
            }
        }

        /// <summary>
        /// Current Velocity of the avatar.
        /// </summary>
        public override LLVector3 Velocity
        {
            get
            {
                if (m_physicsActor != null)
                {
                    m_velocity.X = m_physicsActor.Velocity.X;
                    m_velocity.Y = m_physicsActor.Velocity.Y;
                    m_velocity.Z = m_physicsActor.Velocity.Z;
                }

                return m_velocity;
            }
            set
            {
                if (m_physicsActor != null)
                {
                    try
                    {
                        lock (m_scene.SyncRoot)
                        {
                            m_physicsActor.Velocity = new PhysicsVector(value.X, value.Y, value.Z);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }

                m_velocity = value;
            }
        }

        /// <summary>
        /// If this is true, agent doesn't have a representation in this scene.
        ///    this is an agent 'looking into' this scene from a nearby scene(region)
        ///
        /// if False, this agent has a representation in this scene
        /// </summary>
        private bool m_isChildAgent = true;

        public bool IsChildAgent
        {
            get { return m_isChildAgent; }
            set { m_isChildAgent = value; }
        }

        private uint m_parentID = 0;

        public uint ParentID
        {
            get { return m_parentID; }
            set { m_parentID = value; }
        }
        public float Health
        {
            get { return m_health; }
            set { m_health = value; }
        }

        /// <summary>
        /// These are the region handles known by the avatar.
        /// </summary>
        public List<ulong> KnownChildRegions
        {
            get { return m_knownChildRegions; }
        }

        #endregion

        #region Constructor(s)

        private ScenePresence(IClientAPI client, Scene world, RegionInfo reginfo)
        {
            m_regionHandle = reginfo.RegionHandle;
            m_controllingClient = client;
            m_firstname = m_controllingClient.FirstName;
            m_lastname = m_controllingClient.LastName;

            m_scene = world;
            m_uuid = client.AgentId;
            m_regionInfo = reginfo;
            m_localId = m_scene.NextLocalId;

            AbsolutePosition = m_controllingClient.StartPos;

            TrySetMovementAnimation("STAND");

            RegisterToEvents();
            SetDirectionVectors();


        }

        public ScenePresence(IClientAPI client, Scene world, RegionInfo reginfo, byte[] visualParams,
                             AvatarWearable[] wearables)
            : this(client, world, reginfo)
        {
            m_appearance = new AvatarAppearance(m_uuid, wearables, visualParams);
        }

        public ScenePresence(IClientAPI client, Scene world, RegionInfo reginfo, AvatarAppearance appearance)
            : this(client, world, reginfo)
        {
            m_appearance = appearance;
        }

        public void RegisterToEvents()
        {
            m_controllingClient.OnRequestWearables += SendOwnAppearance;
            m_controllingClient.OnSetAppearance += SetAppearance;
            m_controllingClient.OnCompleteMovementToRegion += CompleteMovement;
            m_controllingClient.OnCompleteMovementToRegion += SendInitialData;
            m_controllingClient.OnAgentUpdate += HandleAgentUpdate;
            m_controllingClient.OnAgentRequestSit += HandleAgentRequestSit;
            m_controllingClient.OnAgentSit += HandleAgentSit;
            m_controllingClient.OnSetAlwaysRun += HandleSetAlwaysRun;
            m_controllingClient.OnStartAnim += HandleStartAnim;
            m_controllingClient.OnStopAnim += HandleStopAnim;
            m_controllingClient.OnForceReleaseControls += HandleForceReleaseControls;
            m_controllingClient.OnAutoPilotGo += DoAutoPilot;

            // ControllingClient.OnChildAgentStatus += new StatusChange(this.ChildStatusChange);
            // ControllingClient.OnStopMovement += new GenericCall2(this.StopMovement);
        }

        private void SetDirectionVectors()
        {
            Dir_Vectors[0] = new Vector3(1, 0, 0); //FORWARD
            Dir_Vectors[1] = new Vector3(-1, 0, 0); //BACK
            Dir_Vectors[2] = new Vector3(0, 1, 0); //LEFT
            Dir_Vectors[3] = new Vector3(0, -1, 0); //RIGHT
            Dir_Vectors[4] = new Vector3(0, 0, 1); //UP
            Dir_Vectors[5] = new Vector3(0, 0, -1); //DOWN
            Dir_Vectors[5] = new Vector3(0, 0, -0.5f); //DOWN_Nudge
        }

        #endregion

        /// <summary>
        /// Add the part to the queue of parts for which we need to send an update to the client
        /// </summary>
        /// <param name="part"></param>
        public void QueuePartForUpdate(SceneObjectPart part)
        {
            //if (InterestList.Contains(part.ParentGroup))
            //{
            lock (m_partsUpdateQueue)
            {
                m_partsUpdateQueue.Enqueue(part);
            }
            // }
        }

        public uint GenerateClientFlags(LLUUID ObjectID)
        {
            return m_scene.ExternalChecks.ExternalChecksGenerateClientFlags(m_uuid, ObjectID);
        }

        /// <summary>
        /// Send updates to the client about prims which have been placed on the update queue.  We don't
        /// necessarily send updates for all the parts on the queue, e.g. if an updates with a more recent
        /// timestamp has already been sent.
        /// </summary>
        public void SendPrimUpdates()
        {
            // if (m_scene.QuadTree.GetNodeID(this.AbsolutePosition.X, this.AbsolutePosition.Y) != m_currentQuadNode)
            //{
            //  this.UpdateQuadTreeNode();
            //this.RefreshQuadObject();
            //}
            m_perfMonMS = System.Environment.TickCount;

            if (m_pendingObjects == null)
            {
                if (!m_isChildAgent || m_scene.m_seeIntoRegionFromNeighbor)
                {
                    m_pendingObjects = new Queue<SceneObjectGroup>();

                    foreach (EntityBase e in m_scene.Entities.Values)
                        if (e is SceneObjectGroup)
                            m_pendingObjects.Enqueue((SceneObjectGroup)e);
                }
            }

            while (m_pendingObjects.Count > 0 && m_partsUpdateQueue.Count < 60)
            {
                SceneObjectGroup g = m_pendingObjects.Dequeue();

                // This is where we should check for draw distance
                // do culling and stuff. Problem with that is that until
                // we recheck in movement, that won't work right.
                // So it's not implemented now.
                //

                // Don't even queue if we have seent this one
                //
                if (!m_updateTimes.ContainsKey(g.UUID))
                    g.ScheduleFullUpdateToAvatar(this);
            }

            int updateCount = 0;

            while (m_partsUpdateQueue.Count > 0)
            {
                SceneObjectPart part = m_partsUpdateQueue.Dequeue();
                if (m_updateTimes.ContainsKey(part.UUID))
                {
                    ScenePartUpdate update = m_updateTimes[part.UUID];

                    // We deal with the possibility that two updates occur at
                    // the same unix time at the update point itself.

                    if (update.LastFullUpdateTime < part.TimeStampFull)
                    {
//                            m_log.DebugFormat(
//                                "[SCENE PRESENCE]: Fully   updating prim {0}, {1} - part timestamp {2}",
//                                part.Name, part.UUID, part.TimeStampFull);

                        part.SendFullUpdate(ControllingClient,
                                GenerateClientFlags(part.UUID));

                        // We'll update to the part's timestamp rather than
                        // the current time to avoid the race condition
                        // whereby the next tick occurs while we are doing
                        // this update. If this happened, then subsequent
                        // updates which occurred on the same tick or the
                        // next tick of the last update would be ignored.

                        update.LastFullUpdateTime = part.TimeStampFull;

                        updateCount++;
                    }
                    else if (update.LastTerseUpdateTime <= part.TimeStampTerse)
                    {
//                            m_log.DebugFormat(
//                                "[SCENE PRESENCE]: Tersely updating prim {0}, {1} - part timestamp {2}",
//                                part.Name, part.UUID, part.TimeStampTerse);

                        part.SendTerseUpdate(ControllingClient);

                        update.LastTerseUpdateTime = part.TimeStampTerse;
                        updateCount++;
                    }
                }
                else
                {
                    //never been sent to client before so do full update

                    part.SendFullUpdate(ControllingClient,
                            GenerateClientFlags(part.UUID));
                    ScenePartUpdate update = new ScenePartUpdate();
                    update.FullID = part.UUID;
                    update.LastFullUpdateTime = part.TimeStampFull;
                    m_updateTimes.Add(part.UUID, update);
                    updateCount++;
                }

                if (updateCount > 60)
                    break;
            }

            m_scene.AddAgentTime(System.Environment.TickCount - m_perfMonMS);
        }

        public void forceAvatarMovement(Vector3 position, Quaternion rotation)
        {
            AddNewMovement(position, rotation);
        }

        #region Status Methods

        /// <summary>
        /// This turns a child agent, into a root agent
        /// This is called when an agent teleports into a region, or if an
        /// agent crosses into this region from a neighbor over the border
        /// </summary>
        public void MakeRootAgent(LLVector3 pos, bool isFlying)
        {
//            m_log.DebugFormat(
//                 "[SCENE PRESENCE]: Upgrading child agent {0}, {1} to a root agent in {2} at pos {3}",
//                 Name, UUID, m_scene.RegionInfo.RegionName, pos);

            if (pos.X < 0 || pos.X > Constants.RegionSize || pos.Y < 0 || pos.Y > Constants.RegionSize || pos.Z < 0)
            {
                LLVector3 emergencyPos = new LLVector3(128, 128, 128);

                m_log.WarnFormat(
                    "[SCENE PRESENCE]: MakeRootAgent() was given an illegal position of {0} for avatar {1}, {2}.  Substituting {3}",
                    pos, Name, UUID, emergencyPos);

                pos = emergencyPos;
            }

            m_isChildAgent = false;

            float localAVHeight = 1.56f;
            if (m_avHeight != 127.0f)
            {
                localAVHeight = m_avHeight;
            }

            float posZLimit = (float)m_scene.GetLandHeight((int)pos.X, (int)pos.Y);
            float newPosZ = posZLimit + localAVHeight;
            if (posZLimit >= (pos.Z - (localAVHeight / 2)) && !(Single.IsInfinity(newPosZ) || Single.IsNaN(newPosZ)))
            {
                pos.Z = newPosZ;
            }
            AbsolutePosition = pos;

            AddToPhysicalScene();
            m_physicsActor.Flying = isFlying;
            SendAnimPack();

            m_scene.SwapRootAgentCount(false);
            m_scene.CommsManager.UserProfileCacheService.RequestInventoryForUser(m_uuid);
            m_scene.AddCapsHandler(m_uuid);

            // On the next prim update, all objects will be sent
            //
            m_pendingObjects = null;

            m_scene.EventManager.TriggerOnMakeRootAgent(this);
            m_scene.CommsManager.UserService.UpdateUserCurrentRegion(UUID, m_scene.RegionInfo.RegionID, m_scene.RegionInfo.RegionHandle);
        }

        /// <summary>
        /// This turns a root agent into a child agent
        /// when an agent departs this region for a neighbor, this gets called.
        ///
        /// It doesn't get called for a teleport.  Reason being, an agent that
        /// teleports out may not end up anywhere near this region
        /// </summary>
        public void MakeChildAgent()
        {
            m_animations.Clear();

//            m_log.DebugFormat(
//                 "[SCENEPRESENCE]: Downgrading root agent {0}, {1} to a child agent in {2}",
//                 Name, UUID, m_scene.RegionInfo.RegionName);

            Velocity = new LLVector3(0, 0, 0);
            m_isChildAgent = true;
            m_scene.SwapRootAgentCount(true);
            RemoveFromPhysicalScene();
            m_scene.EventManager.TriggerOnMakeChildAgent(this);
            //this.Pos = new LLVector3(128, 128, 70);
        }

        /// <summary>
        /// Removes physics plugin scene representation of this agent if it exists.
        /// </summary>
        private void RemoveFromPhysicalScene()
        {
            if (PhysicsActor != null)
            {
                m_scene.PhysicsScene.RemoveAvatar(PhysicsActor);
                m_physicsActor.OnRequestTerseUpdate -= SendTerseUpdateToAllClients;
                m_physicsActor.UnSubscribeEvents();
                m_physicsActor.OnCollisionUpdate -= PhysicsCollisionUpdate;
                PhysicsActor = null;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="pos"></param>
        public void Teleport(LLVector3 pos)
        {
            RemoveFromPhysicalScene();
            Velocity = new LLVector3(0, 0, 0);
            AbsolutePosition = pos;
            AddToPhysicalScene();
            SendTerseUpdateToAllClients();
        }

        /// <summary>
        ///
        /// </summary>
        public void StopMovement()
        {
        }

        public void StopFlying()
        {
            // It turns out to get the agent to stop flying, you have to feed it stop flying velocities
            // and send a full object update.
            // There's no message to send the client to tell it to stop flying

            // Add 1/6 the avatar's height to it's position so it doesn't shoot into the air
            // when the avatar stands up

            if (m_avHeight != 127.0f)
            {
                AbsolutePosition = AbsolutePosition + new LLVector3(0, 0, (m_avHeight / 6));
            }
            else
            {
                AbsolutePosition = AbsolutePosition + new LLVector3(0, 0, (1.56f / 6));
            }
            TrySetMovementAnimation("LAND");
            SendFullUpdateToAllClients();
        }

        public void AddNeighbourRegion(ulong regionHandle)
        {
            if (!m_knownChildRegions.Contains(regionHandle))
            {
                m_knownChildRegions.Add(regionHandle);
            }
        }

        public void RemoveNeighbourRegion(ulong regionHandle)
        {
            if (!m_knownChildRegions.Contains(regionHandle))
            {
                m_knownChildRegions.Remove(regionHandle);
            }
        }

        public List<ulong> GetKnownRegionList()
        {
            return m_knownChildRegions;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Sets avatar height in the phyiscs plugin
        /// </summary>
        internal void SetHeight(float height)
        {
            m_avHeight = height;
            if (PhysicsActor != null)
            {
                PhysicsVector SetSize = new PhysicsVector(0.45f, 0.6f, m_avHeight);
                PhysicsActor.Size = SetSize;
            }
        }

        /// <summary>
        /// Complete Avatar's movement into the region
        /// </summary>
        public void CompleteMovement()
        {
            LLVector3 look = Velocity;
            if ((look.X == 0) && (look.Y == 0) && (look.Z == 0))
            {
                look = new LLVector3(0.99f, 0.042f, 0);
            }

            m_controllingClient.MoveAgentIntoRegion(m_regionInfo, AbsolutePosition, look);

            if (m_isChildAgent)
            {
                m_isChildAgent = false;

                MakeRootAgent(AbsolutePosition, false);
            }
        }

        /// <summary>
        /// This is the event handler for client movement.   If a client is moving, this event is triggering.
        /// </summary>
        public void HandleAgentUpdate(IClientAPI remoteClient, AgentUpdateArgs agentData)
        {
            //if (m_isChildAgent)
            //{
            //    // Console.WriteLine("DEBUG: HandleAgentUpdate: child agent");
            //    return;
            //}

            // Must check for standing up even when PhysicsActor is null,
            // since sitting currently removes avatar from physical scene
            //m_log.Debug("agentPos:" + AbsolutePosition.ToString());

            // This is irritating.  Really.
            if (!AbsolutePosition.IsFinite())
            {
                RemoveFromPhysicalScene();
                m_log.Error("[AVATAR]: NonFinite Avatar position detected...   Reset Position.  Mantis this please. Error# 9999902");

                m_pos = m_LastFinitePos;
                if (!m_pos.IsFinite())
                {
                    m_pos.X = 127f;
                    m_pos.Y = 127f;
                    m_pos.Z = 127f;
                    m_log.Error("[AVATAR]: NonFinite Avatar position detected...   Reset Position.  Mantis this please. Error# 9999903");
                }

                AddToPhysicalScene();
            }
            else
            {
                m_LastFinitePos = m_pos;
            }
            //m_physicsActor.AddForce(new PhysicsVector(999999999, 99999999, 999999999999999), true);


            //ILandObject land = LandChannel.GetLandObject(agent.startpos.X, agent.startpos.Y);
            //if (land != null)
            //{
                //if (land.landData.landingType == (byte)1 && land.landData.userLocation != LLVector3.Zero)
                //{
                //    agent.startpos = land.landData.userLocation;
                //}
            //}

            m_perfMonMS = System.Environment.TickCount;

            uint flags = agentData.ControlFlags;
            LLQuaternion bodyRotation = agentData.BodyRotation;

            // Camera location in world.  We'll need to raytrace
            // from this location from time to time.
            m_CameraCenter.x = agentData.CameraCenter.X;
            m_CameraCenter.y = agentData.CameraCenter.Y;
            m_CameraCenter.z = agentData.CameraCenter.Z;

            // Use these three vectors to figure out what the agent is looking at
            // Convert it to a Matrix and/or Quaternion
            m_CameraAtAxis.x = agentData.CameraAtAxis.X;
            m_CameraAtAxis.y = agentData.CameraAtAxis.Y;
            m_CameraAtAxis.z = agentData.CameraAtAxis.Z;

            m_CameraLeftAxis.x = agentData.CameraLeftAxis.X;
            m_CameraLeftAxis.y = agentData.CameraLeftAxis.Y;
            m_CameraLeftAxis.z = agentData.CameraLeftAxis.Z;

            m_CameraUpAxis.x = agentData.CameraUpAxis.X;
            m_CameraUpAxis.y = agentData.CameraUpAxis.Y;
            m_CameraUpAxis.z = agentData.CameraUpAxis.Z;

            // The Agent's Draw distance setting
            m_DrawDistance = agentData.Far;

            if ((flags & (uint) AgentManager.ControlFlags.AGENT_CONTROL_STAND_UP) != 0)
            {
                StandUp();
            }
            lock (scriptedcontrols)
            {
                if (scriptedcontrols.Count > 0)
                {
                    SendControlToScripts(flags);
                    flags = this.RemoveIgnoredControls(flags, IgnoredControls);

                }
            }
            if (PhysicsActor == null)
            {
                return;
            }

            if (m_autopilotMoving)
                CheckAtSitTarget();

            if ((flags & (uint) AgentManager.ControlFlags.AGENT_CONTROL_SIT_ON_GROUND) != 0)
            {
                // TODO: This doesn't prevent the user from walking yet.
                // Setting parent ID would fix this, if we knew what value
                // to use.  Or we could add a m_isSitting variable.

                TrySetMovementAnimation("SIT_GROUND_CONSTRAINED");
            }
            // In the future, these values might need to go global.
            // Here's where you get them.

            // m_AgentControlFlags = flags;
            // m_headrotation = agentData.AgentData.HeadRotation;
            // m_state = agentData.AgentData.State;

            if (m_allowMovement)
            {
                int i = 0;
                bool update_movementflag = false;
                bool update_rotation = false;
                bool DCFlagKeyPressed = false;
                Vector3 agent_control_v3 = new Vector3(0, 0, 0);
                Quaternion q = new Quaternion(bodyRotation.W, bodyRotation.X, bodyRotation.Y, bodyRotation.Z);
                if (PhysicsActor != null)
                {
                    bool oldflying = PhysicsActor.Flying;

                    PhysicsActor.Flying = ((flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_FLY) != 0);
                    if (PhysicsActor.Flying != oldflying)
                    {
                        update_movementflag = true;
                    }
                }
                if (q != m_bodyRot)
                {
                    m_bodyRot = q;
                    update_rotation = true;
                }

                if (m_parentID == 0)
                {
                    foreach (Dir_ControlFlags DCF in Enum.GetValues(typeof (Dir_ControlFlags)))
                    {
                        if ((flags & (uint) DCF) != 0)
                        {
                            DCFlagKeyPressed = true;
                            try
                            {
                                agent_control_v3 += Dir_Vectors[i];
                            }
                            catch (IndexOutOfRangeException)
                            {
                                // Why did I get this?
                            }
                            if ((m_movementflag & (uint) DCF) == 0)
                            {
                                m_movementflag += (byte) (uint) DCF;
                                update_movementflag = true;
                            }
                        }
                        else
                        {
                            if ((m_movementflag & (uint) DCF) != 0)
                            {
                                m_movementflag -= (byte) (uint) DCF;
                                update_movementflag = true;
                            }
                        }
                        i++;
                    }
                }
                // Cause the avatar to stop flying if it's colliding
                // with something with the down arrow pressed.

                // Only do this if we're flying
                if (m_physicsActor != null && m_physicsActor.Flying)
                {
                    // Are the landing controls requirements filled?
                    bool controlland = (((flags & (uint) AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG) != 0) ||
                                        ((flags & (uint) AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_NEG) != 0));

                    // Are the collision requirements fulfilled?
                    bool colliding = (m_physicsActor.IsColliding == true);

                    if (m_physicsActor.Flying && colliding && controlland)
                    {
                        StopFlying();
                    }
                }

                if (update_movementflag || (update_rotation && DCFlagKeyPressed))
                {
                    AddNewMovement(agent_control_v3, q);

                    if (update_movementflag)
                        UpdateMovementAnimations();
                }
            }

            m_scene.EventManager.TriggerOnClientMovement(this);

            m_scene.AddAgentTime(System.Environment.TickCount - m_perfMonMS);
        }

        public void DoAutoPilot(uint not_used, LLVector3 Pos, IClientAPI remote_client)
        {
            m_autopilotMoving = true;
            m_autoPilotTarget = Pos;
            m_sitAtAutoTarget = false;
            PrimitiveBaseShape proxy = PrimitiveBaseShape.Default;
            //proxy.PCode = (byte)PCode.ParticleSystem;
            uint nextUUID = m_scene.NextLocalId;

            proxyObjectGroup = new SceneObjectGroup(m_scene, m_scene.RegionInfo.RegionHandle, UUID, nextUUID, Pos, new LLQuaternion(Rotation.x, Rotation.y, Rotation.z, Rotation.w), proxy);
            if (proxyObjectGroup != null)
            {
                proxyObjectGroup.SendGroupFullUpdate();
                remote_client.SendSitResponse(proxyObjectGroup.UUID, LLVector3.Zero, LLQuaternion.Identity, true, LLVector3.Zero, LLVector3.Zero, false);
                m_scene.DeleteSceneObject(proxyObjectGroup);
            }
            else
            {
                m_autopilotMoving = false;
                m_autoPilotTarget = LLVector3.Zero;
                ControllingClient.SendAlertMessage("Autopilot cancelled");
            }

        }

        private void CheckAtSitTarget()
        {
            //m_log.Debug("[AUTOPILOT]: " + Util.GetDistanceTo(AbsolutePosition, m_autoPilotTarget).ToString());
            if (Util.GetDistanceTo(AbsolutePosition, m_autoPilotTarget) <= 1.5)
            {
                if (m_sitAtAutoTarget)
                {
                    SceneObjectPart part = m_scene.GetSceneObjectPart(m_requestedSitTargetUUID);
                    if (part != null)
                    {
                        AbsolutePosition = part.AbsolutePosition;
                        Velocity = new LLVector3(0, 0, 0);
                        SendFullUpdateToAllClients();

                        //HandleAgentSit(ControllingClient, m_requestedSitTargetUUID);
                    }
                    //ControllingClient.SendSitResponse(m_requestedSitTargetID, m_requestedSitOffset, LLQuaternion.Identity, false, LLVector3.Zero, LLVector3.Zero, false);
                    m_requestedSitTargetUUID = LLUUID.Zero;
                }
                else
                {
                    //ControllingClient.SendAlertMessage("Autopilot cancelled");
                    //SendTerseUpdateToAllClients();
                    //PrimitiveBaseShape proxy = PrimitiveBaseShape.Default;
                    //proxy.PCode = (byte)PCode.ParticleSystem;
                    ////uint nextUUID = m_scene.NextLocalId;

                    //proxyObjectGroup = new SceneObjectGroup(m_scene, m_scene.RegionInfo.RegionHandle, UUID, nextUUID, m_autoPilotTarget, LLQuaternion.Identity, proxy);
                    //if (proxyObjectGroup != null)
                    //{
                        //proxyObjectGroup.SendGroupFullUpdate();
                        //ControllingClient.SendSitResponse(LLUUID.Zero, m_autoPilotTarget, LLQuaternion.Identity, true, LLVector3.Zero, LLVector3.Zero, false);
                        //m_scene.DeleteSceneObject(proxyObjectGroup);
                    //}
                }
                m_autoPilotTarget = LLVector3.Zero;
                m_autopilotMoving = false;
            }
        }
        /// <summary>
        /// Perform the logic necessary to stand the client up.  This method also executes
        /// the stand animation.
        /// </summary>
        public void StandUp()
        {
            if (m_parentID != 0)
            {
                SceneObjectPart part = m_scene.GetSceneObjectPart(m_parentID);
                if (part != null)
                {
                    // Reset sit target.
                    if (part.GetAvatarOnSitTarget() == UUID)
                        part.SetAvatarOnSitTarget(LLUUID.Zero);

                    m_parentPosition = part.GetWorldPosition();
                }

                if (m_physicsActor == null)
                {
                    AddToPhysicalScene();
                }

                m_pos += m_parentPosition + new LLVector3(0.0f, 0.0f, 2.0f*m_sitAvatarHeight);
                m_parentPosition = new LLVector3();

                m_parentID = 0;
                SendFullUpdateToAllClients();
                m_requestedSitTargetID = 0;
                if (m_physicsActor != null)
                {
                    SetHeight(m_avHeight);
                }
            }

            TrySetMovementAnimation("STAND");
        }

        private SceneObjectPart FindNextAvailableSitTarget(LLUUID targetID)
        {
            SceneObjectPart targetPart = m_scene.GetSceneObjectPart(targetID);
            if (targetPart == null)
                return null;

            // If the primitive the player clicked on has a sit target and that sit target is not full, that sit target is used.
            // If the primitive the player clicked on has no sit target, and one or more other linked objects have sit targets that are not full, the sit target of the object with the lowest link number will be used.

            // Get our own copy of the part array, and sort into the order we want to test
            SceneObjectPart[] partArray = targetPart.ParentGroup.GetParts();
            Array.Sort(partArray, delegate(SceneObjectPart p1, SceneObjectPart p2)
                       {
                           // we want the originally selected part first, then the rest in link order -- so make the selected part link num (-1)
                           int linkNum1 = p1==targetPart ? -1 : p1.LinkNum;
                           int linkNum2 = p2==targetPart ? -1 : p2.LinkNum;
                           return linkNum1 - linkNum2;
                       }
                );

            //look for prims with explicit sit targets that are available
            foreach (SceneObjectPart part in partArray)
            {
                // Is a sit target available?
                Vector3 avSitOffSet = part.SitTargetPosition;
                Quaternion avSitOrientation = part.SitTargetOrientation;
                LLUUID avOnTargetAlready = part.GetAvatarOnSitTarget();

                bool SitTargetUnOccupied = (!(avOnTargetAlready != LLUUID.Zero));
                bool SitTargetisSet =
                    (!(avSitOffSet.x == 0 && avSitOffSet.y == 0 && avSitOffSet.z == 0 && avSitOrientation.w == 0 &&
                       avSitOrientation.x == 0 && avSitOrientation.y == 0 && avSitOrientation.z == 1));

                if (SitTargetisSet && SitTargetUnOccupied)
                {
                    //switch the target to this prim
                    return part;
                }
            }

            // no explicit sit target found - use original target
            return targetPart;
        }

        private void SendSitResponse(IClientAPI remoteClient, LLUUID targetID, LLVector3 offset)
        {
            bool autopilot = true;
            LLVector3 pos = new LLVector3();
            LLQuaternion sitOrientation = new LLQuaternion(0, 0, 0, 1);
            LLVector3 cameraEyeOffset = LLVector3.Zero;
            LLVector3 cameraAtOffset = LLVector3.Zero;
            bool forceMouselook = false;

            //SceneObjectPart part =  m_scene.GetSceneObjectPart(targetID);
            SceneObjectPart part =  FindNextAvailableSitTarget(targetID);
            if (part != null)
            {
                // TODO: determine position to sit at based on scene geometry; don't trust offset from client
                // see http://wiki.secondlife.com/wiki/User:Andrew_Linden/Office_Hours/2007_11_06 for details on how LL does it

                // Is a sit target available?
                Vector3 avSitOffSet = part.SitTargetPosition;
                Quaternion avSitOrientation = part.SitTargetOrientation;
                LLUUID avOnTargetAlready = part.GetAvatarOnSitTarget();

                bool SitTargetUnOccupied = (!(avOnTargetAlready != LLUUID.Zero));
                bool SitTargetisSet =
                    (!(avSitOffSet.x == 0 && avSitOffSet.y == 0 && avSitOffSet.z == 0 && avSitOrientation.w == 0 &&
                       avSitOrientation.x == 0 && avSitOrientation.y == 0 && avSitOrientation.z == 1));

                if (SitTargetisSet && SitTargetUnOccupied)
                {
                    part.SetAvatarOnSitTarget(UUID);
                    offset = new LLVector3(avSitOffSet.x, avSitOffSet.y, avSitOffSet.z);
                    sitOrientation =
                        new LLQuaternion(avSitOrientation.x, avSitOrientation.y, avSitOrientation.z, avSitOrientation.w);
                    autopilot = false;
                }

                pos = part.AbsolutePosition + offset;
                //if (Math.Abs(part.AbsolutePosition.Z - AbsolutePosition.Z) > 1)
                //{
                   // offset = pos;
                    //autopilot = false;
                //}
                if (m_physicsActor != null)
                {
                    // If we're not using the client autopilot, we're immediately warping the avatar to the location
                    // We can remove the physicsActor until they stand up.
                    m_sitAvatarHeight = m_physicsActor.Size.Z;

                    if (autopilot)
                    {
                        if (Util.GetDistanceTo(AbsolutePosition, pos) < 4.5)
                        {
                            autopilot = false;

                            RemoveFromPhysicalScene();
                            AbsolutePosition = pos + new LLVector3(0.0f, 0.0f, m_sitAvatarHeight);
                        }
                    }
                    else
                    {
                        RemoveFromPhysicalScene();
                    }
                }
                
                cameraAtOffset = part.GetCameraAtOffset();
                cameraEyeOffset = part.GetCameraEyeOffset();
                forceMouselook = part.GetForceMouselook();
            }

            ControllingClient.SendSitResponse(targetID, offset, sitOrientation, autopilot, cameraAtOffset, cameraEyeOffset, forceMouselook);
            m_requestedSitTargetUUID = targetID;
            // This calls HandleAgentSit twice, once from here, and the client calls
            // HandleAgentSit itself after it gets to the location
            // It doesn't get to the location until we've moved them there though
            // which happens in HandleAgentSit :P
            m_autopilotMoving = autopilot;
            m_autoPilotTarget = pos;
            m_sitAtAutoTarget = autopilot;
            if (!autopilot)
                HandleAgentSit(remoteClient, UUID);
        }

        public void HandleAgentRequestSit(IClientAPI remoteClient, LLUUID agentID, LLUUID targetID, LLVector3 offset)
        {
            if (m_parentID != 0)
            {
                StandUp();
            }

            //SceneObjectPart part = m_scene.GetSceneObjectPart(targetID);
            SceneObjectPart part =  FindNextAvailableSitTarget(targetID);

            if (part != null)
            {
                m_requestedSitTargetID = part.LocalId;
                m_requestedSitOffset = offset;
            }
            else
            {
                m_log.Warn("Sit requested on unknown object: " + targetID.ToString());
            }
            SendSitResponse(remoteClient, targetID, offset);
        }

        public void HandleAgentSit(IClientAPI remoteClient, LLUUID agentID)
        {
            SceneObjectPart part = m_scene.GetSceneObjectPart(m_requestedSitTargetID);

            if (m_sitAtAutoTarget || !m_autopilotMoving)
            {
                if (part != null)
                {
                    if (part.GetAvatarOnSitTarget() == UUID)
                    {
                        Vector3 sitTargetPos = part.SitTargetPosition;
                        Quaternion sitTargetOrient = part.SitTargetOrientation;

                        //Quaternion vq = new Quaternion(sitTargetPos.x, sitTargetPos.y+0.2f, sitTargetPos.z+0.2f, 0);
                        //Quaternion nq = new Quaternion(sitTargetOrient.w, -sitTargetOrient.x, -sitTargetOrient.y, -sitTargetOrient.z);

                        //Quaternion result = (sitTargetOrient * vq) * nq;

                        m_pos = new LLVector3(sitTargetPos.x, sitTargetPos.y, sitTargetPos.z);
                        m_pos += m_sitTargetCorrectionOffset;
                        m_bodyRot = sitTargetOrient;
                        //Rotation = sitTargetOrient;
                        m_parentPosition = part.AbsolutePosition;

                        //SendTerseUpdateToAllClients();
                    }
                    else
                    {
                        m_pos -= part.AbsolutePosition;
                        m_parentPosition = part.AbsolutePosition;
                    }
                }
                else
                {
                    return;
                }
            }
            m_parentID = m_requestedSitTargetID;

            Velocity = new LLVector3(0, 0, 0);
            RemoveFromPhysicalScene();

            TrySetMovementAnimation("SIT");
            SendFullUpdateToAllClients();
            // This may seem stupid, but Our Full updates don't send avatar rotation :P
            // So we're also sending a terse update (which has avatar rotation)
            // [Update] We do now.
            //SendTerseUpdateToAllClients();
        }

        /// <summary>
        /// Event handler for the 'Always run' setting on the client
        /// Tells the physics plugin to increase speed of movement.
        /// </summary>
        public void HandleSetAlwaysRun(IClientAPI remoteClient, bool SetAlwaysRun)
        {
            m_setAlwaysRun = SetAlwaysRun;
            if (PhysicsActor != null)
            {
                PhysicsActor.SetAlwaysRun = SetAlwaysRun;
            }
        }

        public void AddAnimation(LLUUID animID)
        {
            if (m_isChildAgent)
                return;

            if (m_animations.Add(animID, m_controllingClient.NextAnimationSequenceNumber))
            {
                SendAnimPack();
            }
        }

        public void AddAnimation(string name)
        {
            if (m_isChildAgent)
                return;

            LLUUID animID = m_controllingClient.GetDefaultAnimation(name);
            if (animID == LLUUID.Zero)
                return;

            AddAnimation(animID);
        }

        public void RemoveAnimation(LLUUID animID)
        {
            if (m_isChildAgent)
                return;

            if (m_animations.Remove(animID))
            {
                SendAnimPack();
            }
        }

        public void RemoveAnimation(string name)
        {
            if (m_isChildAgent)
                return;

            LLUUID animID = m_controllingClient.GetDefaultAnimation(name);
            if (animID == LLUUID.Zero)
                return;

            RemoveAnimation(animID);
        }
        
        public LLUUID[] GetAnimationArray()
        {
            LLUUID[] animIDs;
            int[] sequenceNums;
            m_animations.GetArrays( out animIDs, out sequenceNums );
            return animIDs;
        }

        public void HandleStartAnim(IClientAPI remoteClient, LLUUID animID)
        {
            AddAnimation(animID);
        }

        public void HandleStopAnim(IClientAPI remoteClient, LLUUID animID)
        {
            RemoveAnimation(animID);
        }

        /// <summary>
        /// The movement animation is reserved for "main" animations
        /// that are mutually exclusive, e.g. flying and sitting.
        /// </summary>
        protected void SetMovementAnimation(LLUUID animID)
        {
            if (m_animations.SetDefaultAnimation(animID, m_controllingClient.NextAnimationSequenceNumber))
            {
                SendAnimPack();
            }
        }

        /// <summary>
        /// The movement animation is reserved for "main" animations
        /// that are mutually exclusive, e.g. flying and sitting.
        /// </summary>
        protected void TrySetMovementAnimation(string anim)
        {
            if (m_animations.TrySetDefaultAnimation(anim, m_controllingClient.NextAnimationSequenceNumber))
            {
                SendAnimPack();
            }
        }

        /// <summary>
        /// This method determines the proper movement related animation
        /// </summary>
        protected string GetMovementAnimation()
        {
            if (m_movementflag != 0)
            {
                // We are moving
                if (PhysicsActor != null && PhysicsActor.Flying)
                {
                    return "FLY";
                }
                else if (PhysicsActor != null && (m_movementflag & (uint) AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG) != 0 &&
                         PhysicsActor.IsColliding)
                {
                    if ((m_movementflag & (uint) AgentManager.ControlFlags.AGENT_CONTROL_AT_POS) != 0 ||
                        (m_movementflag & (uint) AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG) != 0)
                    {
                        return "CROUCHWALK";
                    }
                    else
                    {
                        return "CROUCH";
                    }
                }
                else if (PhysicsActor != null && !PhysicsActor.IsColliding && PhysicsActor.Velocity.Z < -6)
                {
                    return "FALLDOWN";
                }
                else if (PhysicsActor != null && !PhysicsActor.IsColliding && Velocity.Z > 1e-6 &&
                         (m_movementflag & (uint) AgentManager.ControlFlags.AGENT_CONTROL_UP_POS) != 0)
                {
                    return "JUMP";
                }
                else if (m_setAlwaysRun)
                {
                    return "RUN";
                }
                else
                {
                    return "WALK";
                }
            }
            else
            {
                // We are not moving
                if (PhysicsActor != null && !PhysicsActor.IsColliding && PhysicsActor.Velocity.Z < -6 && !PhysicsActor.Flying)
                {
                    return "FALLDOWN";
                }
                else if (PhysicsActor != null && !PhysicsActor.IsColliding && Velocity.Z > 6 && !PhysicsActor.Flying)
                {
                    // HACK: We check if Velocity.Z > 6 for this animation in order to avoid false positives during normal movement.
                    // TODO: set this animation only when on the ground and UP_POS is received?

                    // This is the standing jump
                    return "JUMP";
                }
                else if (PhysicsActor != null && PhysicsActor.Flying)
                {
                    return "HOVER";
                }
                else
                {
                    return "STAND";
                }
            }
        }

        protected void UpdateMovementAnimations()
        {
            TrySetMovementAnimation(GetMovementAnimation());
        }

        /// <summary>
        /// Adds a new movement
        /// </summary>
        protected void AddNewMovement(Vector3 vec, Quaternion rotation)
        {
            if (m_isChildAgent)
            {
                Console.WriteLine("DEBUG: AddNewMovement: child agent");
                return;
            }

            m_perfMonMS = System.Environment.TickCount;

            m_rotation = rotation;
            NewForce newVelocity = new NewForce();
            Vector3 direc = rotation*vec;
            direc.Normalize();

            direc *= 0.03f*128f;
            if (m_physicsActor.Flying)
            {
                direc *= 4;
                //bool controlland = (((m_AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG) != 0) || ((m_AgentControlFlags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_NEG) != 0));
                //bool colliding = (m_physicsActor.IsColliding==true);
                //if (controlland)
                //    m_log.Info("[AGENT]: landCommand");
                //if (colliding)
                //    m_log.Info("[AGENT]: colliding");
                //if (m_physicsActor.Flying && colliding && controlland)
                //{
                //    StopFlying();
                //    m_log.Info("[AGENT]: Stop FLying");
                //}
            }
            else
            {
                if (!m_physicsActor.Flying && m_physicsActor.IsColliding)
                {
                    if (direc.z > 2.0f)
                    {
                        direc.z *= 3;

                        // TODO: PreJump and jump happen too quickly.  Many times prejump gets ignored.
                        TrySetMovementAnimation("PREJUMP");
                        TrySetMovementAnimation("JUMP");
                    }
                }
            }

            newVelocity.X = direc.x;
            newVelocity.Y = direc.y;
            newVelocity.Z = direc.z;
            m_forcesList.Add(newVelocity);

            m_scene.AddAgentTime(System.Environment.TickCount - m_perfMonMS);
        }

        #endregion

        #region Overridden Methods

        /// <summary>
        ///
        /// </summary>
        public override void Update()
        {
            SendPrimUpdates();

            if (m_newCoarseLocations)
            {
                SendCoarseLocations();
                m_newCoarseLocations = false;
            }

            if (m_isChildAgent == false)
            {
                if (m_newForce) // user movement 'forces' (ie commands to move)
                {
                    SendTerseUpdateToAllClients();
                    m_updateCount = 0;
                }
                else if (m_movementflag != 0) // scripted movement (?)
                {
                    m_updateCount++;
                    if (m_updateCount > 3)
                    {
                        SendTerseUpdateToAllClients();
                        m_updateCount = 0;
                    }
                }
                else if ((Util.GetDistanceTo(lastPhysPos, AbsolutePosition) > 0.02) || (Util.GetDistanceTo(m_lastVelocity, m_velocity) > 0.02)) // physics-related movement
                {
                    // Send Terse Update to all clients updates lastPhysPos and m_lastVelocity
                    // doing the above assures us that we know what we sent the clients last
                    SendTerseUpdateToAllClients();
                    m_updateCount = 0;
                }

                // followed suggestion from mic bowman. reversed the two lines below.
                CheckForBorderCrossing();
                CheckForSignificantMovement(); // sends update to the modules.
            }
        }

        #endregion

        #region Update Client(s)

        /// <summary>
        /// Sends a location update to the client connected to this scenePresence
        /// </summary>
        /// <param name="remoteClient"></param>
        public void SendTerseUpdateToClient(IClientAPI remoteClient)
        {
            m_perfMonMS = System.Environment.TickCount;

            LLVector3 pos = m_pos;
            LLVector3 vel = Velocity;
            LLQuaternion rot = new LLQuaternion(m_bodyRot.x, m_bodyRot.y, m_bodyRot.z, m_bodyRot.w);
            remoteClient.SendAvatarTerseUpdate(m_regionHandle, (ushort)(m_scene.TimeDilation * (float)ushort.MaxValue), LocalId, new LLVector3(pos.X, pos.Y, pos.Z),
                                               new LLVector3(vel.X, vel.Y, vel.Z), rot);

            m_scene.AddAgentTime(System.Environment.TickCount - m_perfMonMS);
            m_scene.AddAgentUpdates(1);
        }

        /// <summary>
        /// Send a location/velocity/accelleration update to all agents in scene
        /// </summary>
        public void SendTerseUpdateToAllClients()
        {
            m_perfMonMS = System.Environment.TickCount;

            m_scene.Broadcast(SendTerseUpdateToClient);

            m_lastVelocity = m_velocity;
            lastPhysPos = AbsolutePosition;

            m_scene.AddAgentTime(System.Environment.TickCount - m_perfMonMS);

        }

        public void SendCoarseLocations()
        {
            m_perfMonMS = System.Environment.TickCount;

            List<LLVector3> CoarseLocations = new List<LLVector3>();
            List<ScenePresence> avatars = m_scene.GetAvatars();
            for (int i = 0; i < avatars.Count; i++)
            {
                if (avatars[i] != this)
                {
                    CoarseLocations.Add(avatars[i].m_pos);
                }
            }

            m_controllingClient.SendCoarseLocationUpdate(CoarseLocations);

            m_scene.AddAgentTime(System.Environment.TickCount - m_perfMonMS);
        }

        public void CoarseLocationChange()
        {
            m_newCoarseLocations = true;
        }

        /// <summary>
        /// Tell other client about this avatar (The client previously didn't know or had outdated details about this avatar)
        /// </summary>
        /// <param name="remoteAvatar"></param>
        public void SendFullUpdateToOtherClient(ScenePresence remoteAvatar)
        {
            // 2 stage check is needed.
            if (remoteAvatar == null)
                return;
            IClientAPI cl=remoteAvatar.ControllingClient;
            if (cl == null)
                return;
            if (m_appearance.Texture == null)
                return;

            // Note: because LLQuaternion is a struct, it can't be null
            LLQuaternion rot = new LLQuaternion(m_bodyRot.x, m_bodyRot.y, m_bodyRot.z, m_bodyRot.w);

            remoteAvatar.m_controllingClient.SendAvatarData(m_regionInfo.RegionHandle, m_firstname, m_lastname, m_uuid,
                                                            LocalId, m_pos, m_appearance.Texture.ToBytes(),
                                                            m_parentID, rot);
            m_scene.AddAgentUpdates(1);
        }

        /// <summary>
        /// Tell *ALL* agents about this agent
        /// </summary>
        public void SendInitialFullUpdateToAllClients()
        {
            m_perfMonMS = System.Environment.TickCount;

            List<ScenePresence> avatars = m_scene.GetScenePresences();
            foreach (ScenePresence avatar in avatars)
            {
                SendFullUpdateToOtherClient(avatar);

                if (avatar.LocalId != LocalId)
                {
                    if (!avatar.m_isChildAgent || m_scene.m_seeIntoRegionFromNeighbor)
                    {
                        avatar.SendFullUpdateToOtherClient(this);
                        avatar.SendAppearanceToOtherAgent(this);
                        avatar.SendAnimPackToClient(this.ControllingClient);
                    }
                }
            }
            m_scene.AddAgentUpdates(avatars.Count);
            m_scene.AddAgentTime(System.Environment.TickCount - m_perfMonMS);
        }

        public void SendFullUpdateToAllClients()
        {
            m_perfMonMS = System.Environment.TickCount;

            List<ScenePresence> avatars = m_scene.GetScenePresences();
            foreach (ScenePresence avatar in avatars)
            {
                SendFullUpdateToOtherClient(avatar);

            }
            m_scene.AddAgentUpdates(avatars.Count);
            m_scene.AddAgentTime(System.Environment.TickCount - m_perfMonMS);
        }


        /// <summary>
        /// Do everything required once a client completes its movement into a region 
        /// </summary>
        public void SendInitialData()
        {
            // Needed for standalone
            m_scene.GetAvatarAppearance(m_controllingClient, out m_appearance);

            // Note: because LLQuaternion is a struct, it can't be null
            LLQuaternion rot = new LLQuaternion(m_bodyRot.x, m_bodyRot.y, m_bodyRot.z, m_bodyRot.w);

            m_controllingClient.SendAvatarData(m_regionInfo.RegionHandle, m_firstname, m_lastname, m_uuid, LocalId,
                                               m_pos, m_appearance.Texture.ToBytes(), m_parentID, rot);

            if (!m_isChildAgent)
            {
                m_scene.InformClientOfNeighbours(this);
            }

            SendInitialFullUpdateToAllClients();
            SendAppearanceToAllOtherAgents();
         }

        /// <summary>
        ///
        /// </summary>
        /// <param name="client"></param>
        public void SendOwnAppearance()
        {
            m_log.Info("[APPEARANCE] Sending Own Appearance");
            ControllingClient.SendWearables(m_appearance.Wearables, m_appearance.Serial++);
            // ControllingClient.SendAppearance(
            //                                 m_appearance.Owner,
            //                                 m_appearance.VisualParams,
            //                                 m_appearance.Texture.ToBytes()
            // );
        }

        /// <summary>
        ///
        /// </summary>
        public void SendAppearanceToAllOtherAgents()
        {
            m_log.Info("[APPEARANCE] Sending Appearance to All Other Agents");
            m_perfMonMS=System.Environment.TickCount;

            m_scene.ForEachScenePresence(delegate(ScenePresence scenePresence)
                                         {
                                             if (scenePresence.UUID != UUID)
                                             {
                                                 SendAppearanceToOtherAgent(scenePresence);
                                             }
                                         });
            m_scene.AddAgentTime(System.Environment.TickCount - m_perfMonMS);
        }

        public void SendAppearanceToOtherAgent(ScenePresence avatar)
        {
            avatar.ControllingClient.SendAppearance(
                                                    m_appearance.Owner,
                                                    m_appearance.VisualParams,
                                                    m_appearance.Texture.ToBytes()
                                                    );
        }

        public void SetAppearance(byte[] texture, List<byte> visualParam)
        {
            m_log.Info("[APPEARANCE] Setting Appearance");
            m_appearance.SetAppearance(texture, visualParam);
            SetHeight(m_appearance.AvatarHeight);
            m_scene.CommsManager.AvatarService.UpdateUserAppearance(m_controllingClient.AgentId, m_appearance);

            SendAppearanceToAllOtherAgents();
            SendOwnAppearance();
        }

        public void SetWearable(int wearableId, AvatarWearable wearable)
        {
            m_log.Info("[APPEARANCE] Setting Wearable");
            m_appearance.SetWearable(wearableId, wearable);
            m_scene.CommsManager.AvatarService.UpdateUserAppearance(m_controllingClient.AgentId, m_appearance);
            m_controllingClient.SendWearables(m_appearance.Wearables, m_appearance.Serial++);
        }

        // Because appearance setting is in a module, we actually need
        // to give it access to our appearance directly, otherwise we
        // get a synchronization issue.
        public AvatarAppearance Appearance
        {
            get { return m_appearance; }
            set { m_appearance = value; }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="animations"></param>
        /// <param name="seqs"></param>
        public void SendAnimPack(LLUUID[] animations, int[] seqs)
        {
            if (m_isChildAgent)
                return;

            m_scene.Broadcast(
                delegate(IClientAPI client) { client.SendAnimations(animations, seqs, m_controllingClient.AgentId); });
        }

        public void SendAnimPackToClient(IClientAPI client)
        {
            if (m_isChildAgent)
                return;
            LLUUID[] animIDs;
            int[] sequenceNums;

            m_animations.GetArrays(out animIDs, out sequenceNums);

            client.SendAnimations(animIDs, sequenceNums, m_controllingClient.AgentId);
        }

        /// <summary>
        ///
        /// </summary>
        public void SendAnimPack()
        {
            if (m_isChildAgent)
                return;

            LLUUID[] animIDs;
            int[] sequenceNums;

            m_animations.GetArrays(out animIDs, out sequenceNums);

            SendAnimPack(animIDs, sequenceNums);
        }

        #endregion

        #region Significant Movement Method

        /// <summary>
        /// This checks for a significant movement and sends a courselocationchange update
        /// </summary>
        protected void CheckForSignificantMovement()
        {
            if (Util.GetDistanceTo(AbsolutePosition, posLastSignificantMove) > 0.5)
            {
                posLastSignificantMove = AbsolutePosition;
                m_scene.EventManager.TriggerSignificantClientMovement(m_controllingClient);
                m_scene.NotifyMyCoarseLocationChange();
            }

            // Minimum Draw distance is 64 meters, the Radius of the draw distance sphere is 32m
            if (Util.GetDistanceTo(AbsolutePosition,m_LastChildAgentUpdatePosition) > 32)
            {
                ChildAgentDataUpdate cadu = new ChildAgentDataUpdate();
                cadu.ActiveGroupID=LLUUID.Zero.UUID;
                cadu.AgentID = UUID.UUID;
                cadu.alwaysrun = m_setAlwaysRun;
                cadu.AVHeight = m_avHeight;
                LLVector3 tempCameraCenter = new LLVector3(m_CameraCenter.x, m_CameraCenter.y, m_CameraCenter.z);
                cadu.cameraPosition = new sLLVector3(tempCameraCenter);
                cadu.drawdistance = m_DrawDistance;
                cadu.godlevel = m_godlevel;
                cadu.GroupAccess = 0;
                cadu.Position = new sLLVector3(AbsolutePosition);
                cadu.regionHandle = m_scene.RegionInfo.RegionHandle;
                float multiplier = 1;
                int innacurateNeighbors = m_scene.GetInaccurateNeighborCount();
                if (innacurateNeighbors != 0)
                {
                    multiplier = 1f / (float)innacurateNeighbors;
                }
                if (multiplier <= 0f)
                {
                    multiplier = 0.25f;
                }

                //m_log.Info("[NeighborThrottle]: " + m_scene.GetInaccurateNeighborCount().ToString() + " - m: " + multiplier.ToString());
                cadu.throttles = ControllingClient.GetThrottlesPacked(multiplier);





                cadu.Velocity = new sLLVector3(Velocity);
                m_scene.SendOutChildAgentUpdates(cadu,this);
                m_LastChildAgentUpdatePosition.X = AbsolutePosition.X;
                m_LastChildAgentUpdatePosition.Y = AbsolutePosition.Y;
                m_LastChildAgentUpdatePosition.Z = AbsolutePosition.Z;
            }
        }

        #endregion

        #region Border Crossing Methods

        /// <summary>
        /// Checks to see if the avatar is in range of a border and calls CrossToNewRegion
        /// </summary>
        protected void CheckForBorderCrossing()
        {
            if (IsChildAgent)
                return;
            
            LLVector3 pos2 = AbsolutePosition;
            LLVector3 vel = Velocity;

            float timeStep = 0.1f;
            pos2.X = pos2.X + (vel.X*timeStep);
            pos2.Y = pos2.Y + (vel.Y*timeStep);
            pos2.Z = pos2.Z + (vel.Z*timeStep);

            if ((pos2.X < 0) || (pos2.X > Constants.RegionSize))
            {
                CrossToNewRegion();
            }

            if ((pos2.Y < 0) || (pos2.Y > Constants.RegionSize))
            {
                CrossToNewRegion();
            }
        }

        /// <summary>
        /// Moves the agent outside the region bounds
        /// Tells neighbor region that we're crossing to it
        /// If the neighbor accepts, remove the agent's viewable avatar from this scene
        /// set them to a child agent.
        /// </summary>
        protected void CrossToNewRegion()
        {
            LLVector3 pos = AbsolutePosition;
            LLVector3 newpos = new LLVector3(pos.X, pos.Y, pos.Z);
            uint neighbourx = m_regionInfo.RegionLocX;
            uint neighboury = m_regionInfo.RegionLocY;

            // distance to edge that will trigger crossing
            const float boundaryDistance = 1.7f;

            // distance into new region to place avatar
            const float enterDistance = 0.1f;

            if (pos.X < boundaryDistance)
            {
                neighbourx--;
                newpos.X = Constants.RegionSize - enterDistance;
            }
            else if (pos.X > Constants.RegionSize - boundaryDistance)
            {
                neighbourx++;
                newpos.X = enterDistance;
            }

            if (pos.Y < boundaryDistance)
            {
                neighboury--;
                newpos.Y = Constants.RegionSize - enterDistance;
            }
            else if (pos.Y > Constants.RegionSize - boundaryDistance)
            {
                neighboury++;
                newpos.Y = enterDistance;
            }

            LLVector3 vel = m_velocity;
            ulong neighbourHandle = Helpers.UIntsToLong((uint)(neighbourx * Constants.RegionSize), (uint)(neighboury * Constants.RegionSize));
            SimpleRegionInfo neighbourRegion = m_scene.RequestNeighbouringRegionInfo(neighbourHandle);
            if (neighbourRegion != null)
            {
                // When the neighbour is informed of the border crossing, it will set up CAPS handlers for the avatar
                // This means we need to remove the current caps handler here and possibly compensate later,
                // in case both scenes are being hosted on the same region server.  Messy
                m_scene.RemoveCapsHandler(UUID);
                newpos = newpos + (vel);                

                CachedUserInfo userInfo = m_scene.CommsManager.UserProfileCacheService.GetUserDetails(UUID);                
                if (userInfo != null)
                {
                    userInfo.DropInventory();
                }
                else
                {
                    m_log.WarnFormat("[SCENE PRESENCE]: No cached user info found for {0} {1} on leaving region", Name, UUID);
                }   
                
                bool crossingSuccessful =
                    m_scene.InformNeighbourOfCrossing(neighbourHandle, m_controllingClient.AgentId, newpos,
                                                      m_physicsActor.Flying);                
                if (crossingSuccessful)
                {
                    AgentCircuitData circuitdata = m_controllingClient.RequestClientInfo();

                    // TODO Should construct this behind a method
                    string capsPath =
                        "http://" + neighbourRegion.ExternalHostName + ":" + neighbourRegion.HttpPort
                         + "/CAPS/" + circuitdata.CapsPath + "0000/";

                    m_log.DebugFormat(
                        "[CAPS]: Sending new CAPS seed url {0} to client {1}", capsPath, m_uuid);

                    m_controllingClient.CrossRegion(neighbourHandle, newpos, vel, neighbourRegion.ExternalEndPoint,
                                                    capsPath);
                    MakeChildAgent();
                    CrossAttachmentsIntoNewRegion(neighbourHandle);                                                                                   
                        
                    m_scene.SendKillObject(m_localId);
                    m_scene.NotifyMyCoarseLocationChange();
                    // the user may change thier profile information in other region,
                    // so the userinfo in UserProfileCache is not reliable any more, delete it
                    m_scene.CommsManager.UserProfileCacheService.RemoveUser(UUID);
                    m_log.InfoFormat("User {0} is going to another region, profile cache removed", UUID);
                }
                else
                {
                    // Restore the user structures that we needed to delete before asking the receiving region to complete the crossing
                    m_scene.CommsManager.UserProfileCacheService.RequestInventoryForUser(UUID);                    
                    m_scene.AddCapsHandler(UUID);
                }
            }
        }

        #endregion

        /// <summary>
        /// This allows the Sim owner the abiility to kick users from their sim currently.
        /// It tells the client that the agent has permission to do so.
        /// </summary>
        public void GrantGodlikePowers(LLUUID agentID, LLUUID sessionID, LLUUID token, bool godStatus)
        {
            if (godStatus)
            {
                // For now, assign god level 200 to anyone
                // who is granted god powers, but has no god level set.
                //
                CachedUserInfo profile = m_scene.CommsManager.UserProfileCacheService.GetUserDetails(agentID);
                if(profile.UserProfile.GodLevel > 0)
                    m_godlevel = profile.UserProfile.GodLevel;
                else
                    m_godlevel = 200;
            }
            else
            {
                m_godlevel = 0;
            }

            ControllingClient.SendAdminResponse(token, (uint)m_godlevel);
        }

        /// <summary>
        /// This updates important decision making data about a child agent
        /// The main purpose is to figure out what objects to send to a child agent that's in a neighboring region
        /// </summary>
        public void ChildAgentDataUpdate(ChildAgentDataUpdate cAgentData, uint tRegionX, uint tRegionY, uint rRegionX, uint rRegionY)
        {
            //
            if (!IsChildAgent)
                return;

            int shiftx = ((int)rRegionX - (int)tRegionX) * (int)Constants.RegionSize;
            int shifty = ((int)rRegionY - (int)tRegionY) * (int)Constants.RegionSize;

            m_DrawDistance = cAgentData.drawdistance;
            m_pos = new LLVector3(cAgentData.Position.x + shiftx, cAgentData.Position.y + shifty, cAgentData.Position.z);

            // It's hard to say here..   We can't really tell where the camera position is unless it's in world cordinates from the sending region
            m_CameraCenter =
                new Vector3(cAgentData.cameraPosition.x, cAgentData.cameraPosition.y, cAgentData.cameraPosition.z);


            m_godlevel = cAgentData.godlevel;
            m_avHeight = cAgentData.AVHeight;
            //SetHeight(cAgentData.AVHeight);

            ControllingClient.SetChildAgentThrottle(cAgentData.throttles);



            // Sends out the objects in the user's draw distance if m_sendTasksToChild is true.
            if (m_scene.m_seeIntoRegionFromNeighbor)
                m_pendingObjects = null;

            //cAgentData.AVHeight;
            //cAgentData.regionHandle;
            //m_velocity = cAgentData.Velocity;
        }

        /// <summary>
        /// Handles part of the PID controller function for moving an avatar.
        /// </summary>
        public override void UpdateMovement()
        {
            m_newForce = false;
            lock (m_forcesList)
            {
                if (m_forcesList.Count > 0)
                {
                    for (int i = 0; i < m_forcesList.Count; i++)
                    {
                        NewForce force = m_forcesList[i];

                        m_updateflag = true;
                        try
                        {
                            movementvector.X = force.X;
                            movementvector.Y = force.Y;
                            movementvector.Z = force.Z;
                            Velocity = movementvector;
                        }
                        catch (NullReferenceException)
                        {
                            // Under extreme load, this returns a NullReference Exception that we can ignore.
                            // Ignoring this causes no movement to be sent to the physics engine...
                            // which when the scene is moving at 1 frame every 10 seconds, it doesn't really matter!
                        }
                        m_newForce = true;
                    }
                    for (int i = 0; i < m_forcesList.Count; i++)
                    {
                        m_forcesList.RemoveAt(0);
                    }
                }
            }
        }

        static ScenePresence()
        {

            LLObject.TextureEntry textu = AvatarAppearance.GetDefaultTexture();
            DefaultTexture = textu.ToBytes();
        }

        [Serializable]
        public class NewForce
        {
            public float X;
            public float Y;
            public float Z;

            public NewForce()
            {
            }
        }

        [Serializable]
        public class ScenePartUpdate : ISerializable
        {
            public LLUUID FullID;
            public uint LastFullUpdateTime;
            public uint LastTerseUpdateTime;

            public ScenePartUpdate()
            {
                FullID = LLUUID.Zero;
                LastFullUpdateTime = 0;
                LastTerseUpdateTime = 0;
            }

            protected ScenePartUpdate(SerializationInfo info, StreamingContext context)
            {
                //System.Console.WriteLine("ScenePartUpdate Deserialize BGN");

                if (info == null)
                {
                    throw new ArgumentNullException("info");
                }

                FullID = new LLUUID((Guid)info.GetValue("FullID", typeof(Guid)));
                LastFullUpdateTime = (uint)info.GetValue("LastFullUpdateTime", typeof(uint));
                LastTerseUpdateTime = (uint)info.GetValue("LastTerseUpdateTime", typeof(uint));

                //System.Console.WriteLine("ScenePartUpdate Deserialize END");
            }

            [SecurityPermission(SecurityAction.LinkDemand,
                                Flags = SecurityPermissionFlag.SerializationFormatter)]
            public virtual void GetObjectData(
                SerializationInfo info, StreamingContext context)
            {
                if (info == null)
                {
                    throw new ArgumentNullException("info");
                }

                info.AddValue("FullID", FullID.UUID);
                info.AddValue("LastFullUpdateTime", LastFullUpdateTime);
                info.AddValue("LastTerseUpdateTime", LastTerseUpdateTime);
            }
        }

        public override void SetText(string text, Vector3 color, double alpha)
        {
            throw new Exception("Can't set Text on avatar.");
        }

        /// <summary>
        /// Adds a physical representation of the avatar to the Physics plugin
        /// </summary>
        public void AddToPhysicalScene()
        {
            PhysicsScene scene = m_scene.PhysicsScene;

            PhysicsVector pVec =
                new PhysicsVector(AbsolutePosition.X, AbsolutePosition.Y,
                                  AbsolutePosition.Z);
            if (m_avHeight == 127.0f)
            {
                m_physicsActor = scene.AddAvatar(Firstname + "." + Lastname, pVec, new PhysicsVector(0, 0, 1.56f));
            }
            else
            {
                m_physicsActor = scene.AddAvatar(Firstname + "." + Lastname, pVec, new PhysicsVector(0, 0, m_avHeight));
            }
            //m_physicsActor.OnRequestTerseUpdate += SendTerseUpdateToAllClients;
            m_physicsActor.OnCollisionUpdate += PhysicsCollisionUpdate;
            m_physicsActor.SubscribeEvents(1000);
            m_physicsActor.LocalID = LocalId;
        }

        // Event called by the physics plugin to tell the avatar about a collision.
        private void PhysicsCollisionUpdate(EventArgs e)
        {
            if (e == null)
                return;
            CollisionEventUpdate collisionData = (CollisionEventUpdate)e;
            Dictionary<uint, float> coldata = collisionData.m_objCollisionList;
            float starthealth = Health;
            uint killerObj = 0;
            foreach (uint localid in coldata.Keys)
            {
                if (coldata[localid] <= 0.10f || m_invulnerable)
                    continue;
                //if (localid == 0)
                    //continue;

                Health -= coldata[localid] * 5;

                if (Health <= 0)
                {
                    if (localid != 0)
                        killerObj = localid;
                }
                //m_log.Debug("[AVATAR]: Collision with localid: " + localid.ToString() + " at depth: " + coldata[localid].ToString());
            }
            //Health = 100;
            if (!m_invulnerable)
            {
                if (starthealth != Health)
                {
                    ControllingClient.SendHealth(Health);
                }
                if (m_health <= 0)
                    m_scene.EventManager.TriggerAvatarKill(killerObj, this);
            }

            if (Velocity.X > 0 || Velocity.Y > 0)
                UpdateMovementAnimations();
        }

        public void setHealthWithUpdate(float health)
        {
            Health = health;
            ControllingClient.SendHealth(Health);
        }

        internal void Close()
        {
            lock (m_attachments)
            {
                if (!m_attachmentsTransported)
                {
                    try
                    {
                        foreach (SceneObjectGroup grp in m_attachments)
                        {
                            // ControllingClient may be null at this point!
                            m_scene.m_innerScene.DetachSingleAttachmentToInv(grp.GetFromAssetID(), ControllingClient);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        m_log.Info("[CLIENT]: Couldn't save attachments. :(");
                    }
                    m_attachments.Clear();
                }
            }
            lock (m_knownChildRegions)
            {
                m_knownChildRegions.Clear();
            }
            lock (m_updateTimes)
            {
                m_updateTimes.Clear();
            }
            lock (m_partsUpdateQueue)
            {
                m_partsUpdateQueue.Clear();
            }

            RemoveFromPhysicalScene();
            GC.Collect();
        }

        public ScenePresence()
        {
/* JB
            if (Animations == null)
            {
                Animations = new AvatarAnimations();
                Animations.LoadAnims();
            }
*/
            if (DefaultTexture == null)
            {
                LLObject.TextureEntry textu = AvatarAppearance.GetDefaultTexture();
                DefaultTexture = textu.ToBytes();
            }
        }

        public void AddAttachment(SceneObjectGroup gobj)
        {
            lock (m_attachments)
            {
                m_attachments.Add(gobj);
            }
        }

        public void RemoveAttachment(SceneObjectGroup gobj)
        {
            lock (m_attachments)
            {
                if (m_attachments.Contains(gobj))
                {
                    m_attachments.Remove(gobj);
                }
            }
        }

        public void CrossAttachmentsIntoNewRegion(ulong regionHandle)
        {
            m_attachmentsTransported = true;
            lock (m_attachments)
            {
                foreach (SceneObjectGroup gobj in m_attachments)
                {
                    // If the prim group is null then something must have happened to it!
                    if (gobj != null)
                    {
                        // Set the parent localID to 0 so it transfers over properly.
                        gobj.RootPart.SetParentLocalId(0);
                        gobj.RootPart.IsAttachment = false;
                        gobj.AbsolutePosition = gobj.RootPart.AttachedPos;
                        gobj.RootPart.LastOwnerID = gobj.GetFromAssetID();
                        m_scene.CrossPrimGroupIntoNewRegion(regionHandle, gobj);
                    }
                }
                m_attachments.Clear();
            }
        }

        public void initializeScenePresence(IClientAPI client, RegionInfo region, Scene scene)
        {
            m_controllingClient = client;
            m_regionInfo = region;
            m_scene = scene;

            RegisterToEvents();

            /*
            AbsolutePosition = client.StartPos;

            Animations = new AvatarAnimations();
            Animations.LoadAnims();

            m_animations = new List<LLUUID>();
            m_animations.Add(Animations.AnimsLLUUID["STAND"]);
            m_animationSeqs.Add(m_controllingClient.NextAnimationSequenceNumber);

            SetDirectionVectors();
            */
        }

        protected ScenePresence(SerializationInfo info, StreamingContext context)
            : base (info, context)
        {
            //System.Console.WriteLine("ScenePresence Deserialize BGN");

            if (info == null)
            {
                throw new ArgumentNullException("info");
            }
/* JB
            if (Animations == null)
            {
                Animations = new AvatarAnimations();
                Animations.LoadAnims();
            }
*/
            if (DefaultTexture == null)
            {
                LLObject.TextureEntry textu = AvatarAppearance.GetDefaultTexture();
                DefaultTexture = textu.ToBytes();
            }

            m_animations = (AnimationSet)info.GetValue("m_animations", typeof(AnimationSet));
            m_updateflag = (bool)info.GetValue("m_updateflag", typeof(bool));
            m_movementflag = (byte)info.GetValue("m_movementflag", typeof(byte));
            m_forcesList = (List<NewForce>)info.GetValue("m_forcesList", typeof(List<NewForce>));
            m_updateCount = (short)info.GetValue("m_updateCount", typeof(short));
            m_requestedSitTargetID = (uint)info.GetValue("m_requestedSitTargetID", typeof(uint));

            m_requestedSitOffset
                = new LLVector3(
                    (float)info.GetValue("m_requestedSitOffset.X", typeof(float)),
                    (float)info.GetValue("m_requestedSitOffset.Y", typeof(float)),
                    (float)info.GetValue("m_requestedSitOffset.Z", typeof(float)));

            m_sitAvatarHeight = (float)info.GetValue("m_sitAvatarHeight", typeof(float));
            m_godlevel = (float)info.GetValue("m_godlevel", typeof(float));
            m_setAlwaysRun = (bool)info.GetValue("m_setAlwaysRun", typeof(bool));

            m_bodyRot
                = new Quaternion(
                    (float)info.GetValue("m_bodyRot.w", typeof(float)),
                    (float)info.GetValue("m_bodyRot.x", typeof(float)),
                    (float)info.GetValue("m_bodyRot.y", typeof(float)),
                    (float)info.GetValue("m_bodyRot.z", typeof(float)));

            IsRestrictedToRegion = (bool)info.GetValue("IsRestrictedToRegion", typeof(bool));
            m_newForce = (bool)info.GetValue("m_newForce", typeof(bool));
            //m_newAvatar = (bool)info.GetValue("m_newAvatar", typeof(bool));
            m_newCoarseLocations = (bool)info.GetValue("m_newCoarseLocations", typeof(bool));
            m_avHeight = (float)info.GetValue("m_avHeight", typeof(float));
            crossingFromRegion = (ulong)info.GetValue("crossingFromRegion", typeof(ulong));

            List<float[]> Dir_Vectors_work = (List<float[]>)info.GetValue("Dir_Vectors", typeof(List<float[]>));
            List<Vector3> Dir_Vectors_work2 = new List<Vector3>();

            foreach (float[] f3 in Dir_Vectors_work)
            {
                Dir_Vectors_work2.Add(new Vector3(f3[0], f3[1], f3[2]));
            }

            Dir_Vectors = Dir_Vectors_work2.ToArray();

            lastPhysPos
                = new LLVector3(
                    (float)info.GetValue("lastPhysPos.X", typeof(float)),
                    (float)info.GetValue("lastPhysPos.Y", typeof(float)),
                    (float)info.GetValue("lastPhysPos.Z", typeof(float)));

            m_CameraCenter
                = new Vector3(
                    (float)info.GetValue("m_CameraCenter.X", typeof(float)),
                    (float)info.GetValue("m_CameraCenter.Y", typeof(float)),
                    (float)info.GetValue("m_CameraCenter.Z", typeof(float)));

            m_CameraAtAxis
                = new Vector3(
                    (float)info.GetValue("m_CameraAtAxis.X", typeof(float)),
                    (float)info.GetValue("m_CameraAtAxis.Y", typeof(float)),
                    (float)info.GetValue("m_CameraAtAxis.Z", typeof(float)));

            m_CameraLeftAxis
                = new Vector3(
                    (float)info.GetValue("m_CameraLeftAxis.X", typeof(float)),
                    (float)info.GetValue("m_CameraLeftAxis.Y", typeof(float)),
                    (float)info.GetValue("m_CameraLeftAxis.Z", typeof(float)));

            m_CameraUpAxis
                = new Vector3(
                    (float)info.GetValue("m_CameraUpAxis.X", typeof(float)),
                    (float)info.GetValue("m_CameraUpAxis.Y", typeof(float)),
                    (float)info.GetValue("m_CameraUpAxis.Z", typeof(float)));

            m_DrawDistance = (float)info.GetValue("m_DrawDistance", typeof(float));
            m_appearance = (AvatarAppearance)info.GetValue("m_appearance", typeof(AvatarAppearance));
            m_knownChildRegions = (List<ulong>)info.GetValue("m_knownChildRegions", typeof(List<ulong>));

            posLastSignificantMove
                = new LLVector3(
                    (float)info.GetValue("posLastSignificantMove.X", typeof(float)),
                    (float)info.GetValue("posLastSignificantMove.Y", typeof(float)),
                    (float)info.GetValue("posLastSignificantMove.Z", typeof(float)));

            // m_partsUpdateQueue = (UpdateQueue)info.GetValue("m_partsUpdateQueue", typeof(UpdateQueue));

            /*
            Dictionary<Guid, ScenePartUpdate> updateTimes_work
                = (Dictionary<Guid, ScenePartUpdate>)info.GetValue("m_updateTimes", typeof(Dictionary<Guid, ScenePartUpdate>));

            foreach (Guid id in updateTimes_work.Keys)
            {
                m_updateTimes.Add(new LLUUID(id), updateTimes_work[id]);
            }
            */
            m_regionHandle = (ulong)info.GetValue("m_regionHandle", typeof(ulong));
            m_firstname = (string)info.GetValue("m_firstname", typeof(string));
            m_lastname = (string)info.GetValue("m_lastname", typeof(string));
            m_allowMovement = (bool)info.GetValue("m_allowMovement", typeof(bool));
            m_parentPosition = new LLVector3((float)info.GetValue("m_parentPosition.X", typeof(float)),
                                             (float)info.GetValue("m_parentPosition.Y", typeof(float)),
                                             (float)info.GetValue("m_parentPosition.Z", typeof(float)));

            m_isChildAgent = (bool)info.GetValue("m_isChildAgent", typeof(bool));
            m_parentID = (uint)info.GetValue("m_parentID", typeof(uint));

// for OpenSim_v0.5
            currentParcelUUID = new LLUUID((Guid)info.GetValue("currentParcelUUID", typeof(Guid)));

            lastKnownAllowedPosition
                = new Vector3(
                    (float)info.GetValue("lastKnownAllowedPosition.X", typeof(float)),
                    (float)info.GetValue("lastKnownAllowedPosition.Y", typeof(float)),
                    (float)info.GetValue("lastKnownAllowedPosition.Z", typeof(float)));

            sentMessageAboutRestrictedParcelFlyingDown = (bool)info.GetValue("sentMessageAboutRestrictedParcelFlyingDown", typeof(bool));

            m_LastChildAgentUpdatePosition
                = new LLVector3(
                    (float)info.GetValue("m_LastChildAgentUpdatePosition.X", typeof(float)),
                    (float)info.GetValue("m_LastChildAgentUpdatePosition.Y", typeof(float)),
                    (float)info.GetValue("m_LastChildAgentUpdatePosition.Z", typeof(float)));

            m_perfMonMS = (int)info.GetValue("m_perfMonMS", typeof(int));
            m_AgentControlFlags = (uint)info.GetValue("m_AgentControlFlags", typeof(uint));

            m_headrotation
                = new LLQuaternion(
                    (float)info.GetValue("m_headrotation.W", typeof(float)),
                    (float)info.GetValue("m_headrotation.X", typeof(float)),
                    (float)info.GetValue("m_headrotation.Y", typeof(float)),
                    (float)info.GetValue("m_headrotation.Z", typeof(float)));

            m_state = (byte)info.GetValue("m_state", typeof(byte));

            //System.Console.WriteLine("ScenePresence Deserialize END");
        }

        [SecurityPermission(SecurityAction.LinkDemand,
                            Flags = SecurityPermissionFlag.SerializationFormatter)]
        public override void GetObjectData(
            SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException("info");
            }

            base.GetObjectData(info, context);

            info.AddValue("m_animations", m_animations);
            info.AddValue("m_updateflag", m_updateflag);
            info.AddValue("m_movementflag", m_movementflag);
            info.AddValue("m_forcesList", m_forcesList);
            info.AddValue("m_updateCount", m_updateCount);
            info.AddValue("m_requestedSitTargetID", m_requestedSitTargetID);

            // LLVector3
            info.AddValue("m_requestedSitOffset.X", m_requestedSitOffset.X);
            info.AddValue("m_requestedSitOffset.Y", m_requestedSitOffset.Y);
            info.AddValue("m_requestedSitOffset.Z", m_requestedSitOffset.Z);

            info.AddValue("m_sitAvatarHeight", m_sitAvatarHeight);
            info.AddValue("m_godlevel", m_godlevel);
            info.AddValue("m_setAlwaysRun", m_setAlwaysRun);

            // Quaternion
            info.AddValue("m_bodyRot.w", m_bodyRot.w);
            info.AddValue("m_bodyRot.x", m_bodyRot.x);
            info.AddValue("m_bodyRot.y", m_bodyRot.y);
            info.AddValue("m_bodyRot.z", m_bodyRot.z);

            info.AddValue("IsRestrictedToRegion", IsRestrictedToRegion);
            info.AddValue("m_newForce", m_newForce);
            //info.AddValue("m_newAvatar", m_newAvatar);
            info.AddValue("m_newCoarseLocations", m_newCoarseLocations);
            info.AddValue("m_gotAllObjectsInScene", false);
            info.AddValue("m_avHeight", m_avHeight);

            // info.AddValue("m_regionInfo", m_regionInfo);

            info.AddValue("crossingFromRegion", crossingFromRegion);

            List<float[]> Dir_Vectors_work = new List<float[]>();

            foreach (Vector3 v3 in Dir_Vectors)
            {
                Dir_Vectors_work.Add(new float[] { v3.x, v3.y, v3.z });
            }

            info.AddValue("Dir_Vectors", Dir_Vectors_work);

            // LLVector3
            info.AddValue("lastPhysPos.X", lastPhysPos.X);
            info.AddValue("lastPhysPos.Y", lastPhysPos.Y);
            info.AddValue("lastPhysPos.Z", lastPhysPos.Z);

            // Vector3
            info.AddValue("m_CameraCenter.X", m_CameraCenter.x);
            info.AddValue("m_CameraCenter.Y", m_CameraCenter.y);
            info.AddValue("m_CameraCenter.Z", m_CameraCenter.z);

            // Vector3
            info.AddValue("m_CameraAtAxis.X", m_CameraAtAxis.x);
            info.AddValue("m_CameraAtAxis.Y", m_CameraAtAxis.y);
            info.AddValue("m_CameraAtAxis.Z", m_CameraAtAxis.z);

            // Vector3
            info.AddValue("m_CameraLeftAxis.X", m_CameraLeftAxis.x);
            info.AddValue("m_CameraLeftAxis.Y", m_CameraLeftAxis.y);
            info.AddValue("m_CameraLeftAxis.Z", m_CameraLeftAxis.z);

            // Vector3
            info.AddValue("m_CameraUpAxis.X", m_CameraUpAxis.x);
            info.AddValue("m_CameraUpAxis.Y", m_CameraUpAxis.y);
            info.AddValue("m_CameraUpAxis.Z", m_CameraUpAxis.z);

            info.AddValue("m_DrawDistance", m_DrawDistance);
            info.AddValue("m_appearance", m_appearance);
            info.AddValue("m_knownChildRegions", m_knownChildRegions);

            // LLVector3
            info.AddValue("posLastSignificantMove.X", posLastSignificantMove.X);
            info.AddValue("posLastSignificantMove.Y", posLastSignificantMove.Y);
            info.AddValue("posLastSignificantMove.Z", posLastSignificantMove.Z);

            //info.AddValue("m_partsUpdateQueue", m_partsUpdateQueue);

            /*
            Dictionary<Guid, ScenePartUpdate> updateTimes_work = new Dictionary<Guid, ScenePartUpdate>();

            foreach (LLUUID id in m_updateTimes.Keys)
            {
                updateTimes_work.Add(id.UUID, m_updateTimes[id]);
            }

            info.AddValue("m_updateTimes", updateTimes_work);
            */

            info.AddValue("m_regionHandle", m_regionHandle);
            info.AddValue("m_firstname", m_firstname);
            info.AddValue("m_lastname", m_lastname);
            info.AddValue("m_allowMovement", m_allowMovement);
            //info.AddValue("m_physicsActor", m_physicsActor);
            info.AddValue("m_parentPosition.X", m_parentPosition.X);
            info.AddValue("m_parentPosition.Y", m_parentPosition.Y);
            info.AddValue("m_parentPosition.Z", m_parentPosition.Z);
            info.AddValue("m_isChildAgent", m_isChildAgent);
            info.AddValue("m_parentID", m_parentID);

// for OpenSim_v0.5
            info.AddValue("currentParcelUUID", currentParcelUUID.UUID);

            info.AddValue("lastKnownAllowedPosition.X", lastKnownAllowedPosition.x);
            info.AddValue("lastKnownAllowedPosition.Y", lastKnownAllowedPosition.y);
            info.AddValue("lastKnownAllowedPosition.Z", lastKnownAllowedPosition.z);

            info.AddValue("sentMessageAboutRestrictedParcelFlyingDown", sentMessageAboutRestrictedParcelFlyingDown);

            info.AddValue("m_LastChildAgentUpdatePosition.X", m_LastChildAgentUpdatePosition.X);
            info.AddValue("m_LastChildAgentUpdatePosition.Y", m_LastChildAgentUpdatePosition.Y);
            info.AddValue("m_LastChildAgentUpdatePosition.Z", m_LastChildAgentUpdatePosition.Z);

            info.AddValue("m_perfMonMS", m_perfMonMS);
            info.AddValue("m_AgentControlFlags", m_AgentControlFlags);

            info.AddValue("m_headrotation.W", m_headrotation.W);
            info.AddValue("m_headrotation.X", m_headrotation.X);
            info.AddValue("m_headrotation.Y", m_headrotation.Y);
            info.AddValue("m_headrotation.Z", m_headrotation.Z);

            info.AddValue("m_state", m_state);

            List<Guid> knownPrimUUID_work = new List<Guid>();

            info.AddValue("m_knownPrimUUID", knownPrimUUID_work);
        }

        internal void PushForce(PhysicsVector impulse)
        {
            if (PhysicsActor != null)
            {
                PhysicsActor.AddForce(impulse,true);
            }
        }

        public void RegisterControlEventsToScript(int controls, int accept, int pass_on, uint Obj_localID, LLUUID Script_item_LLUUID)
        {
            ScriptControllers obj = new ScriptControllers();
            obj.ignoreControls = ScriptControlled.CONTROL_ZERO;
            obj.eventControls = ScriptControlled.CONTROL_ZERO;

            obj.itemID = Script_item_LLUUID;
            obj.objID = Obj_localID;
            if (pass_on == 0 && accept == 0)
            {
                IgnoredControls |= (ScriptControlled)controls;
                obj.ignoreControls = (ScriptControlled)controls;
            }

            if (pass_on == 0 && accept == 1)
            {
                IgnoredControls |= (ScriptControlled)controls;
                obj.ignoreControls = (ScriptControlled)controls;
                obj.eventControls = (ScriptControlled)controls;
            }
            if (pass_on == 1 && accept == 1)
            {
                IgnoredControls = ScriptControlled.CONTROL_ZERO;
                obj.eventControls = (ScriptControlled)controls;
                obj.ignoreControls = ScriptControlled.CONTROL_ZERO;
            }

            lock (scriptedcontrols)
            {
                if (pass_on == 1 && accept == 0)
                {
                    IgnoredControls &= ~(ScriptControlled)controls;
                    if (scriptedcontrols.ContainsKey(Script_item_LLUUID))
                        scriptedcontrols.Remove(Script_item_LLUUID);

                }
                else
                {

                    if (scriptedcontrols.ContainsKey(Script_item_LLUUID))
                    {
                        scriptedcontrols[Script_item_LLUUID] = obj;
                    }
                    else
                    {
                        scriptedcontrols.Add(Script_item_LLUUID, obj);
                    }
                }
            }
            ControllingClient.SendTakeControls(controls, pass_on == 1 ? true : false, true);
        }

        public void HandleForceReleaseControls(IClientAPI remoteClient, LLUUID agentID)
        {
            IgnoredControls = ScriptControlled.CONTROL_ZERO;
            lock (scriptedcontrols)
            {
                scriptedcontrols.Clear();
            }
            ControllingClient.SendTakeControls(int.MaxValue, false, false);
        }

        public void UnRegisterControlEventsToScript(uint Obj_localID, LLUUID Script_item_LLUUID)
        {
            lock (scriptedcontrols)
            {
                if (scriptedcontrols.ContainsKey(Script_item_LLUUID))
                {
                    scriptedcontrols.Remove(Script_item_LLUUID);
                    IgnoredControls = ScriptControlled.CONTROL_ZERO;
                    foreach (ScriptControllers scData in scriptedcontrols.Values)
                    {
                        IgnoredControls |= scData.ignoreControls;
                    }
                }
            }
        }

        internal void SendControlToScripts(uint flags)
        {

            ScriptControlled allflags = ScriptControlled.CONTROL_ZERO;

            // find all activated controls, whether the scripts are interested in them or not
            if ((flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_POS) != 0 || (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_POS) != 0)
            {
                allflags |= ScriptControlled.CONTROL_FWD;
            }
            if ((flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG) != 0 || (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_NEG) != 0)
            {
                allflags |= ScriptControlled.CONTROL_BACK;
            }
            if ((flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_POS) != 0 || (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_POS) != 0)
            {
                allflags |= ScriptControlled.CONTROL_UP;
            }
            if ((flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG) != 0 || (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_NEG) != 0)
            {
                allflags |= ScriptControlled.CONTROL_DOWN;
            }
            if ((flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS) != 0 || (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_POS) != 0)
            {
                allflags |= ScriptControlled.CONTROL_LEFT;
            }
            if ((flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_LEFT_NEG) != 0 || (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_NEG) != 0)
            {
                allflags |= ScriptControlled.CONTROL_RIGHT;
            }
            if ((flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_YAW_NEG) != 0)
            {
                allflags |= ScriptControlled.CONTROL_ROT_RIGHT;
            }
            if ((flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_YAW_POS) != 0)
            {
                allflags |= ScriptControlled.CONTROL_ROT_LEFT;
            }
            if ((flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_ML_LBUTTON_DOWN) != 0)
            {
                allflags |= ScriptControlled.CONTROL_ML_LBUTTON;
            }
            if ((flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_LBUTTON_UP) != 0 || (flags & (uint)AgentManager.ControlFlags.AGENT_CONTROL_LBUTTON_DOWN) != 0)
            {
                allflags |= ScriptControlled.CONTROL_LBUTTON;
            }

            // optimization; we have to check per script, but if nothing is pressed and nothing changed, we can skip that
            if (allflags != ScriptControlled.CONTROL_ZERO || allflags != LastCommands)
            {
                lock (scriptedcontrols)
                {
                    foreach (LLUUID scriptUUID in scriptedcontrols.Keys)
                    {
                        ScriptControllers scriptControlData = scriptedcontrols[scriptUUID];
                        ScriptControlled localHeld = allflags & scriptControlData.eventControls;     // the flags interesting for us
                        ScriptControlled localLast = LastCommands & scriptControlData.eventControls; // the activated controls in the last cycle
                        ScriptControlled localChange = localHeld ^ localLast;                        // the changed bits
                        if (localHeld != ScriptControlled.CONTROL_ZERO || localChange != ScriptControlled.CONTROL_ZERO)
                        {
                            // only send if still pressed or just changed
                            m_scene.EventManager.TriggerControlEvent(scriptControlData.objID, scriptUUID, UUID, (uint)localHeld, (uint)localChange);
                        }
                    }
                }
            }
            
            LastCommands = allflags;
        }

        internal uint RemoveIgnoredControls(uint flags, ScriptControlled Ignored)
        {
            if (Ignored == ScriptControlled.CONTROL_ZERO)
                return flags;
            if ((Ignored & ScriptControlled.CONTROL_BACK) != 0)
                flags &= ~((uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG | (uint)AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_NEG);
            if ((Ignored & ScriptControlled.CONTROL_FWD) != 0)
                flags &= ~((uint)AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_POS | (uint)AgentManager.ControlFlags.AGENT_CONTROL_AT_POS);
            if ((Ignored & ScriptControlled.CONTROL_DOWN) != 0)
                flags &= ~((uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG | (uint)AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_NEG);
            if ((Ignored & ScriptControlled.CONTROL_UP) != 0)
                flags &= ~((uint)AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_POS | (uint)AgentManager.ControlFlags.AGENT_CONTROL_UP_POS);
            if ((Ignored & ScriptControlled.CONTROL_LEFT) != 0)
                flags &= ~((uint)AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS | (uint)AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_POS);
            if ((Ignored & ScriptControlled.CONTROL_RIGHT) != 0)
                flags &= ~((uint)AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_NEG | (uint)AgentManager.ControlFlags.AGENT_CONTROL_LEFT_NEG);
            if ((Ignored & ScriptControlled.CONTROL_ROT_LEFT) != 0)
                flags &= ~((uint)AgentManager.ControlFlags.AGENT_CONTROL_YAW_NEG);
            if ((Ignored & ScriptControlled.CONTROL_ROT_RIGHT) != 0)
                flags &= ~((uint)AgentManager.ControlFlags.AGENT_CONTROL_YAW_POS);
            if ((Ignored & ScriptControlled.CONTROL_ML_LBUTTON) != 0)
                flags &= ~((uint)AgentManager.ControlFlags.AGENT_CONTROL_ML_LBUTTON_DOWN);
            if ((Ignored & ScriptControlled.CONTROL_LBUTTON) != 0)
                flags &= ~((uint)AgentManager.ControlFlags.AGENT_CONTROL_LBUTTON_UP | (uint)AgentManager.ControlFlags.AGENT_CONTROL_LBUTTON_DOWN);
                //DIR_CONTROL_FLAG_FORWARD = AgentManager.ControlFlags.AGENT_CONTROL_AT_POS,
                //DIR_CONTROL_FLAG_BACK = AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG,
                //DIR_CONTROL_FLAG_LEFT = AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS,
                //DIR_CONTROL_FLAG_RIGHT = AgentManager.ControlFlags.AGENT_CONTROL_LEFT_NEG,
                //DIR_CONTROL_FLAG_UP = AgentManager.ControlFlags.AGENT_CONTROL_UP_POS,
                //DIR_CONTROL_FLAG_DOWN = AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG,
                //DIR_CONTROL_FLAG_DOWN_NUDGE = AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_NEG
            return flags;
        }
    }
}
