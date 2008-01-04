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
* 
*/
using System;
using System.Collections.Generic;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Types;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public class ScenePresence : EntityBase
    {
        public static AvatarAnimations Animations;
        public static byte[] DefaultTexture;
        public LLUUID currentParcelUUID = LLUUID.Zero;
        private List<LLUUID> m_animations = new List<LLUUID>();
        private List<int> m_animationSeqs = new List<int>();
        public Vector3 lastKnownAllowedPosition = new Vector3();
        public bool sentMessageAboutRestrictedParcelFlyingDown = false;

        private bool m_updateflag = false;
        private byte m_movementflag = 0;
        private readonly List<NewForce> m_forcesList = new List<NewForce>();
        private short m_updateCount = 0;
        private uint m_requestedSitTargetID = 0;
        private LLVector3 m_requestedSitOffset = new LLVector3();
        private float m_sitAvatarHeight = 2.0f;
        private float m_godlevel = 0;

        private bool m_setAlwaysRun = false;

        private Quaternion m_bodyRot;

        public bool IsRestrictedToRegion = false;

        // Agent moves with a PID controller causing a force to be exerted.
        private bool m_newForce = false;
        private bool m_newCoarseLocations = true;
        private bool m_gotAllObjectsInScene = false;

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

        // Agent's Draw distance.
        protected float m_DrawDistance = 0f;

        protected AvatarAppearance m_appearance;

        private readonly List<ulong> m_knownChildRegions = new List<ulong>();
                                     //neighbouring regions we have enabled a child agent in


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

        public delegate void SignificantClientMovement(IClientAPI remote_client);

        public event SignificantClientMovement OnSignificantClientMovement;

        private UpdateQueue m_partsUpdateQueue = new UpdateQueue();
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

        public bool Updated
        {
            set { m_updateflag = value; }
            get { return m_updateflag; }
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
        /// This works out to be the ClientView object associated with this avatar, or it's UDP connection manager
        /// </summary>
        private readonly IClientAPI m_controllingClient;

        protected PhysicsActor m_physicsActor;

        public IClientAPI ControllingClient
        {
            get { return m_controllingClient; }
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

            Animations = new AvatarAnimations();
            Animations.LoadAnims();

            // TODO: m_animations and m_animationSeqs should always be of the same length.
            // Move them into an object to (hopefully) avoid threading issues.
            try
            {
                m_animations.Add(Animations.AnimsLLUUID["STAND"]);
            }
            catch (KeyNotFoundException)
            {
                MainLog.Instance.Warn("AVATAR", "KeyNotFound Exception playing avatar stand animation");
            }
            m_animationSeqs.Add(1);

            RegisterToEvents();
            SetDirectionVectors();

            m_scene.LandManager.sendLandUpdate(this);
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

        private void RegisterToEvents()
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
            return m_scene.PermissionsMngr.GenerateClientFlags(m_uuid, ObjectID);
        }

        public void SendPrimUpdates()
        {
            // if (m_scene.QuadTree.GetNodeID(this.AbsolutePosition.X, this.AbsolutePosition.Y) != m_currentQuadNode)
            //{
            //  this.UpdateQuadTreeNode();
            //this.RefreshQuadObject();
            //}
            if (!m_gotAllObjectsInScene)
            {
                if (!m_isChildAgent || m_scene.m_sendTasksToChild)
                {
                    m_scene.SendAllSceneObjectsToClient(this);
                    m_gotAllObjectsInScene = true;
                }
            }
            if (m_partsUpdateQueue.Count > 0)
            {
                bool runUpdate = true;
                int updateCount = 0;
                while (runUpdate)
                {
                    SceneObjectPart part = m_partsUpdateQueue.Dequeue();
                    if (m_updateTimes.ContainsKey(part.UUID))
                    {
                        ScenePartUpdate update = m_updateTimes[part.UUID];

                        // Two updates can occur with the same timestamp (especially
                        // since our timestamp resolution is to the nearest second).  The first
                        // could have been sent in the last update - we still need to send the 
                        // second here.

                        if (update.LastFullUpdateTime < part.TimeStampFull)
                        {
                            //need to do a full update
                            part.SendFullUpdate(ControllingClient, GenerateClientFlags(part.UUID));

                            // We'll update to the part's timestamp rather than the current to 
                            // avoid the race condition whereby the next tick occurs while we are
                            // doing this update.  If this happened, then subsequent updates which occurred
                            // on the same tick or the next tick of the last update would be ignored.
                            update.LastFullUpdateTime = part.TimeStampFull;

                            updateCount++;
                        }
                        else if (update.LastTerseUpdateTime <= part.TimeStampTerse)
                        {
                            part.SendTerseUpdate(ControllingClient);

                            update.LastTerseUpdateTime = part.TimeStampTerse;
                            updateCount++;
                        }
                    }
                    else
                    {
                        //never been sent to client before so do full update
                        part.SendFullUpdate(ControllingClient, GenerateClientFlags(part.UUID));
                        ScenePartUpdate update = new ScenePartUpdate();
                        update.FullID = part.UUID;
                        update.LastFullUpdateTime = part.TimeStampFull;
                        m_updateTimes.Add(part.UUID, update);
                        updateCount++;
                    }

                    if (m_partsUpdateQueue.Count < 1 || updateCount > 60)
                    {
                        runUpdate = false;
                    }
                }
            }
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
            m_isChildAgent = false;

            AbsolutePosition = pos;

            AddToPhysicalScene();
            m_physicsActor.Flying = isFlying;

            m_scene.SwapRootAgentCount(false);
            m_scene.CommsManager.UserProfileCacheService.UpdateUserInventory(m_uuid);
            //if (!m_gotAllObjectsInScene)
            //{
            //m_scene.SendAllSceneObjectsToClient(this);
            //m_gotAllObjectsInScene = true;
            //}
        }

        /// <summary>
        /// This turns a root agent into a child agent
        /// when an agent departs this region for a neighbor, this gets called.
        /// 
        /// It doesn't get called for a teleport.  Reason being, an agent that 
        /// teleports out may not be anywhere near this region
        /// </summary>
        public void MakeChildAgent()
        {
            Velocity = new LLVector3(0, 0, 0);
            m_isChildAgent = true;
            m_scene.SwapRootAgentCount(true);
            RemoveFromPhysicalScene();

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
            AbsolutePosition = pos;
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
            SetMovementAnimation(Animations.AnimsLLUUID["LAND"], 2);
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

                //this.m_scene.SendAllSceneObjectsToClient(this.ControllingClient);
                MakeRootAgent(AbsolutePosition, false);
            }
        }

        /// <summary>
        /// This is the event handler for client movement.   If a client is moving, this event is triggering.
        /// </summary>
        public void HandleAgentUpdate(IClientAPI remoteClient, AgentUpdatePacket agentData)
        {
            //if (m_isChildAgent)
            //{
            //    // Console.WriteLine("DEBUG: HandleAgentUpdate: child agent");
            //    return;
            //}

            // Must check for standing up even when PhysicsActor is null,
            // since sitting currently removes avatar from physical scene

            uint flags = agentData.AgentData.ControlFlags;
            LLQuaternion bodyRotation = agentData.AgentData.BodyRotation;

            // Camera location in world.  We'll need to raytrace 
            // from this location from time to time.
            m_CameraCenter.x = agentData.AgentData.CameraCenter.X;
            m_CameraCenter.y = agentData.AgentData.CameraCenter.Y;
            m_CameraCenter.z = agentData.AgentData.CameraCenter.Z;

            // Use these three vectors to figure out what the agent is looking at
            // Convert it to a Matrix and/or Quaternion
            m_CameraAtAxis.x = agentData.AgentData.CameraAtAxis.X;
            m_CameraAtAxis.y = agentData.AgentData.CameraAtAxis.Y;
            m_CameraAtAxis.z = agentData.AgentData.CameraAtAxis.Z;

            m_CameraLeftAxis.x = agentData.AgentData.CameraLeftAxis.X;
            m_CameraLeftAxis.y = agentData.AgentData.CameraLeftAxis.Y;
            m_CameraLeftAxis.z = agentData.AgentData.CameraLeftAxis.Z;

            m_CameraUpAxis.x = agentData.AgentData.CameraUpAxis.X;
            m_CameraUpAxis.y = agentData.AgentData.CameraUpAxis.Y;
            m_CameraUpAxis.z = agentData.AgentData.CameraUpAxis.Z;

            // The Agent's Draw distance setting
            m_DrawDistance = agentData.AgentData.Far;

            if ((flags & (uint) AgentManager.ControlFlags.AGENT_CONTROL_STAND_UP) != 0)
            {
                StandUp();
            }

            if (PhysicsActor == null)
            {
                return;
            }

            if ((flags & (uint) AgentManager.ControlFlags.AGENT_CONTROL_SIT_ON_GROUND) != 0)
            {
                // TODO: This doesn't quite work yet -- probably a parent ID problem
                // m_parentID = (what should this be?)
                SetMovementAnimation(Animations.AnimsLLUUID["SIT_GROUND"], 1);
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
                bool oldflying = PhysicsActor.Flying;

                PhysicsActor.Flying = ((flags & (uint) AgentManager.ControlFlags.AGENT_CONTROL_FLY) != 0);
                if (PhysicsActor.Flying != oldflying)
                {
                    update_movementflag = true;
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
                            agent_control_v3 += Dir_Vectors[i];
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

                // Skip if there's no physicsactor
                if (m_physicsActor != null)
                {
                    // Only do this if we're flying
                    if (m_physicsActor.Flying)
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
                }
                if ((update_movementflag) || (update_rotation && DCFlagKeyPressed))
                {
                    AddNewMovement(agent_control_v3, q);
                    UpdateMovementAnimations(update_movementflag);
                }
            }

            m_scene.EventManager.TriggerOnClientMovement(this);
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
                }

                m_pos += m_parentPosition + new LLVector3(0.0f, 0.0f, 2.0f*m_sitAvatarHeight);
                m_parentPosition = new LLVector3();

                if (m_physicsActor == null)
                    AddToPhysicalScene();

                m_parentID = 0;
                SendFullUpdateToAllClients();
            }

            SetMovementAnimation(Animations.AnimsLLUUID["STAND"], 1);
        }

        private void SendSitResponse(IClientAPI remoteClient, LLUUID targetID, LLVector3 offset)
        {
            AvatarSitResponsePacket avatarSitResponse = new AvatarSitResponsePacket();

            avatarSitResponse.SitObject.ID = targetID;

            bool autopilot = true;
            LLVector3 pos = new LLVector3();
            LLQuaternion sitOrientation = new LLQuaternion(0, 0, 0, 1);

            SceneObjectPart part = m_scene.GetSceneObjectPart(targetID);
            if (part != null)
            {
                // TODO: determine position to sit at based on scene geometry; don't trust offset from client
                // see http://wiki.secondlife.com/wiki/User:Andrew_Linden/Office_Hours/2007_11_06 for details on how LL does it


                // Is a sit target available?
                Vector3 avSitOffSet = part.GetSitTargetPosition();
                Quaternion avSitOrientation = part.GetSitTargetOrientation();
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
                        new LLQuaternion(avSitOrientation.w, avSitOrientation.x, avSitOrientation.y, avSitOrientation.z);
                    autopilot = false;
                }


                pos = part.AbsolutePosition + offset;

                if (m_physicsActor != null)
                {
                    // 
                    // If we're not using the client autopilot, we're immediately warping the avatar to the location
                    // We can remove the physicsActor until they stand up.
                    //
                    m_sitAvatarHeight = m_physicsActor.Size.Z;

                    if (autopilot)
                    {
                        if (Util.GetDistanceTo(AbsolutePosition, pos) < 4.5)
                        {
                            autopilot = false;

                            RemoveFromPhysicalScene();
                            AbsolutePosition = pos + new LLVector3(0.0f, 0.0f, m_sitAvatarHeight);
                        }
                        else
                        {
                        }
                    }
                    else
                    {
                        RemoveFromPhysicalScene();
                    }
                } // Physactor != null
            } // part != null


            avatarSitResponse.SitTransform.AutoPilot = autopilot;
            avatarSitResponse.SitTransform.SitPosition = offset;
            avatarSitResponse.SitTransform.SitRotation = sitOrientation;

            remoteClient.OutPacket(avatarSitResponse, ThrottleOutPacketType.Task);

            // This calls HandleAgentSit twice, once from here, and the client calls 
            // HandleAgentSit itself after it gets to the location
            // It doesn't get to the location until we've moved them there though 
            // which happens in HandleAgentSit :P
            if (!autopilot)
                HandleAgentSit(remoteClient, UUID);
        }

        public void HandleAgentRequestSit(IClientAPI remoteClient, LLUUID agentID, LLUUID targetID, LLVector3 offset)
        {
            if (m_parentID != 0)
            {
                StandUp();
            }


            SceneObjectPart part = m_scene.GetSceneObjectPart(targetID);

            if (part != null)
            {
                m_requestedSitTargetID = part.LocalID;
                m_requestedSitOffset = offset;
            }
            else
            {
                MainLog.Instance.Warn("Sit requested on unknown object: " + targetID.ToString());
            }
            SendSitResponse(remoteClient, targetID, offset);
        }

        public void HandleAgentSit(IClientAPI remoteClient, LLUUID agentID)
        {
            SceneObjectPart part = m_scene.GetSceneObjectPart(m_requestedSitTargetID);

            if (part != null)
            {
                if (part.GetAvatarOnSitTarget() == UUID)
                {
                    Vector3 sitTargetPos = part.GetSitTargetPosition();
                    Quaternion sitTargetOrient = part.GetSitTargetOrientation();

                    //Quaternion vq = new Quaternion(sitTargetPos.x, sitTargetPos.y+0.2f, sitTargetPos.z+0.2f, 0);
                    //Quaternion nq = new Quaternion(sitTargetOrient.w, -sitTargetOrient.x, -sitTargetOrient.y, -sitTargetOrient.z);

                    //Quaternion result = (sitTargetOrient * vq) * nq;

                    m_pos = new LLVector3(sitTargetPos.x, sitTargetPos.y, sitTargetPos.z);
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

            m_parentID = m_requestedSitTargetID;

            Velocity = new LLVector3(0, 0, 0);
            RemoveFromPhysicalScene();

            SetMovementAnimation(Animations.AnimsLLUUID["SIT"], 1);
            SendFullUpdateToAllClients();
            // This may seem stupid, but Our Full updates don't send avatar rotation :P
            // So we're also sending a terse update (which has avatar rotation)
            SendTerseUpdateToAllClients();
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

        public void AddAnimation(LLUUID animID, int seq)
        {
            if (!m_animations.Contains(animID))
            {
                m_animations.Add(animID);
                m_animationSeqs.Add(seq);
                SendAnimPack();
            }
        }

        public void RemoveAnimation(LLUUID animID)
        {
            if (m_animations.Contains(animID))
            {
                if (m_animations[0] == animID)
                {
                    SetMovementAnimation(Animations.AnimsLLUUID["STAND"], 1);
                }
                else
                {
                    m_animations.Remove(animID);
                    SendAnimPack();
                }
            }
        }

        public void HandleStartAnim(IClientAPI remoteClient, LLUUID animID, int seq)
        {
            AddAnimation(animID, seq);
        }

        public void HandleStopAnim(IClientAPI remoteClient, LLUUID animID)
        {
            RemoveAnimation(animID);
        }

        /// <summary>
        /// The movement animation is the first element of the animation list,
        /// reserved for "main" animations that are mutually exclusive,
        /// like flying and sitting, for example.
        /// </summary>
        protected void SetMovementAnimation(LLUUID anim, int seq)
        {
            if (m_animations[0] != anim)
            {
                m_animations[0] = anim;
                m_animationSeqs[0] = seq;
                SendAnimPack();
            }
        }

        /// <summary>
        /// This method handles agent movement related animations
        /// </summary>
        protected void UpdateMovementAnimations(bool update_movementflag)
        {
            if (update_movementflag)
            {
                // Are we moving?
                if (m_movementflag != 0)
                {
                    // We are moving

                    if (m_physicsActor.Flying)
                    {
                        // We are flying
                        SetMovementAnimation(Animations.AnimsLLUUID["FLY"], 1);
                    }
                    else if (((m_movementflag & (uint) AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG) != 0) &&
                             PhysicsActor.IsColliding)
                    {
                        // Client is pressing the page down button and moving and is colliding with something
                        SetMovementAnimation(Animations.AnimsLLUUID["CROUCHWALK"], 1);
                    }
                    else if (!PhysicsActor.IsColliding && m_physicsActor.Velocity.Z < -6)
                    {
                        // Client is moving and falling at a velocity greater then 6 meters per unit
                        SetMovementAnimation(Animations.AnimsLLUUID["FALLDOWN"], 1);
                    }
                    else if (!PhysicsActor.IsColliding && Velocity.Z > 0 &&
                             (m_movementflag & (uint) AgentManager.ControlFlags.AGENT_CONTROL_UP_POS) != 0)
                    {
                        // Client is moving, and colliding and pressing the page up button but isn't flying
                        SetMovementAnimation(Animations.AnimsLLUUID["JUMP"], 1);
                    }
                    else if (m_setAlwaysRun)
                    {
                        // We are running
                        SetMovementAnimation(Animations.AnimsLLUUID["RUN"], 1);
                    }
                    else
                    {
                        // We're moving, but we're not doing anything else..   so play the stand animation
                        SetMovementAnimation(Animations.AnimsLLUUID["WALK"], 1);
                    }
                }
                else
                {
                    // Not moving

                    if (((m_movementflag & (uint) AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG) != 0) &&
                        PhysicsActor.IsColliding)
                    {
                        // Client pressing the page down button
                        SetMovementAnimation(Animations.AnimsLLUUID["CROUCH"], 1);
                    }
                    else if (!PhysicsActor.IsColliding && m_physicsActor.Velocity.Z < -6 && !m_physicsActor.Flying)
                    {
                        // Not colliding and not flying, and we're falling at high speed
                        SetMovementAnimation(Animations.AnimsLLUUID["FALLDOWN"], 1);
                    }
                    else if (!PhysicsActor.IsColliding && Velocity.Z > 0 && !m_physicsActor.Flying &&
                             (m_movementflag & (uint) AgentManager.ControlFlags.AGENT_CONTROL_UP_POS) != 0)
                    {
                        // This is the standing jump
                        SetMovementAnimation(Animations.AnimsLLUUID["JUMP"], 1);
                    }
                    else if (m_physicsActor.Flying)
                    {
                        // We're flying but not moving
                        SetMovementAnimation(Animations.AnimsLLUUID["HOVER"], 1);
                    }
                    else
                    {
                        // We're not moving..   and we're not doing anything..   so play the stand animation
                        SetMovementAnimation(Animations.AnimsLLUUID["STAND"], 1);
                    }
                }
            }
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
                //    MainLog.Instance.Verbose("AGENT","landCommand");
                //if (colliding ) 
                //    MainLog.Instance.Verbose("AGENT","colliding");
                //if (m_physicsActor.Flying && colliding && controlland)
                //{
                //    StopFlying();
                //    MainLog.Instance.Verbose("AGENT", "Stop FLying");
                //}
            }
            else
            {
                if (!m_physicsActor.Flying && m_physicsActor.IsColliding)
                {
                    //direc.z *= 40;
                    if (direc.z > 2.0f)
                    {
                        direc.z *= 3;
                        //System.Console.WriteLine("Jump");
                        // PreJump and jump happen too quickly.  Many times prejump gets ignored.
                        SetMovementAnimation(Animations.AnimsLLUUID["PREJUMP"], 1);
                        SetMovementAnimation(Animations.AnimsLLUUID["JUMP"], 1);
                    }
                }
            }

            newVelocity.X = direc.x;
            newVelocity.Y = direc.y;
            newVelocity.Z = direc.z;
            m_forcesList.Add(newVelocity);
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
                else if (Util.GetDistanceTo(lastPhysPos, AbsolutePosition) > 0.02) // physics-related movement
                {
                    SendTerseUpdateToAllClients();
                    m_updateCount = 0;
                    lastPhysPos = AbsolutePosition;
                }
                CheckForSignificantMovement();
                CheckForBorderCrossing();
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
            LLVector3 pos = m_pos;
            LLVector3 vel = Velocity;
            LLQuaternion rot = new LLQuaternion(m_bodyRot.x, m_bodyRot.y, m_bodyRot.z, m_bodyRot.w);
            remoteClient.SendAvatarTerseUpdate(m_regionHandle, 64096, LocalId, new LLVector3(pos.X, pos.Y, pos.Z),
                                               new LLVector3(vel.X, vel.Y, vel.Z), rot);
        }

        /// <summary>
        /// Send a location/velocity/accelleration update to all agents in scene
        /// </summary>
        public void SendTerseUpdateToAllClients()
        {
            m_scene.Broadcast(SendTerseUpdateToClient);
        }

        public void SendCoarseLocations()
        {
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
            remoteAvatar.m_controllingClient.SendAvatarData(m_regionInfo.RegionHandle, m_firstname, m_lastname, m_uuid,
                                                            LocalId, m_pos, m_appearance.TextureEntry.ToBytes(),
                                                            m_parentID);
        }

        /// <summary>
        /// Tell *ALL* agents about this agent
        /// </summary>
        public void SendFullUpdateToAllClients()
        {
            List<ScenePresence> avatars = m_scene.GetScenePresences();
            foreach (ScenePresence avatar in avatars)
            {
                SendFullUpdateToOtherClient(avatar);
                if (avatar.LocalId != LocalId)
                {
                    if (!avatar.m_isChildAgent || m_scene.m_sendTasksToChild)
                    {
                        avatar.SendFullUpdateToOtherClient(this);
                        avatar.SendAppearanceToOtherAgent(this);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendInitialData()
        {
            m_controllingClient.SendAvatarData(m_regionInfo.RegionHandle, m_firstname, m_lastname, m_uuid, LocalId,
                                               m_pos, m_appearance.TextureEntry.ToBytes(), m_parentID);
            if (!m_isChildAgent)
            {
                m_scene.InformClientOfNeighbours(this);
            }

            SendFullUpdateToAllClients();
            SendAppearanceToAllOtherAgents();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public void SendOwnAppearance()
        {
            m_appearance.SendOwnWearables(ControllingClient);

            // TODO: remove this once the SunModule is slightly more tested
            // m_controllingClient.SendViewerTime(m_scene.TimePhase);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendAppearanceToAllOtherAgents()
        {
            m_scene.ForEachScenePresence(delegate(ScenePresence scenePresence)
                                             {
                                                 if (scenePresence.UUID != UUID)
                                                 {
                                                     m_appearance.SendAppearanceToOtherAgent(scenePresence);
                                                 }
                                             });
        }

        public void SendAppearanceToOtherAgent(ScenePresence avatar)
        {
            m_appearance.SendAppearanceToOtherAgent(avatar);
        }

        public void SetAppearance(byte[] texture, AgentSetAppearancePacket.VisualParamBlock[] visualParam)
        {
            m_appearance.SetAppearance(texture, visualParam);
            SetHeight(m_appearance.AvatarHeight);

            SendAppearanceToAllOtherAgents();
        }

        public void SetWearable(int wearableId, AvatarWearable wearable)
        {
            m_appearance.SetWearable(ControllingClient, wearableId, wearable);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="animations"></param>
        /// <param name="seqs"></param>
        public void SendAnimPack(LLUUID[] animations, int[] seqs)
        {
            m_scene.Broadcast(
                delegate(IClientAPI client) { client.SendAnimations(animations, seqs, m_controllingClient.AgentId); });
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendAnimPack()
        {
            SendAnimPack(m_animations.ToArray(), m_animationSeqs.ToArray());
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
                if (OnSignificantClientMovement != null)
                {
                    OnSignificantClientMovement(m_controllingClient);
                    m_scene.NotifyMyCoarseLocationChange();
                }
            }
        }

        #endregion

        #region Border Crossing Methods

        /// <summary>
        /// Checks to see if the avatar is in range of a border and calls CrossToNewRegion
        /// </summary>
        protected void CheckForBorderCrossing()
        {
            LLVector3 pos2 = AbsolutePosition;
            LLVector3 vel = Velocity;

            float timeStep = 0.1f;
            pos2.X = pos2.X + (vel.X*timeStep);
            pos2.Y = pos2.Y + (vel.Y*timeStep);
            pos2.Z = pos2.Z + (vel.Z*timeStep);

            if ((pos2.X < 0) || (pos2.X > 256))
            {
                CrossToNewRegion();
            }

            if ((pos2.Y < 0) || (pos2.Y > 256))
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

            // region size
            // TODO: this should be hard-coded in some common place
            const float regionWidth = 256;
            const float regionHeight = 256;

            if (pos.X < boundaryDistance)
            {
                neighbourx--;
                newpos.X = regionWidth - enterDistance;
            }
            else if (pos.X > regionWidth - boundaryDistance)
            {
                neighbourx++;
                newpos.X = enterDistance;
            }

            if (pos.Y < boundaryDistance)
            {
                neighboury--;
                newpos.Y = regionHeight - enterDistance;
            }
            else if (pos.Y > regionHeight - boundaryDistance)
            {
                neighboury++;
                newpos.Y = enterDistance;
            }

            LLVector3 vel = m_velocity;
            ulong neighbourHandle = Helpers.UIntsToLong((uint) (neighbourx*256), (uint) (neighboury*256));
            SimpleRegionInfo neighbourRegion = m_scene.RequestNeighbouringRegionInfo(neighbourHandle);
            if (neighbourRegion != null)
            {
                bool res =
                    m_scene.InformNeighbourOfCrossing(neighbourHandle, m_controllingClient.AgentId, newpos,
                                                      m_physicsActor.Flying);
                if (res)
                {
                    AgentCircuitData circuitdata = m_controllingClient.RequestClientInfo();
                    string capsPath = Util.GetCapsURL(m_controllingClient.AgentId);
                    m_controllingClient.CrossRegion(neighbourHandle, newpos, vel, neighbourRegion.ExternalEndPoint,
                                                    capsPath);
                    MakeChildAgent();
                    m_scene.SendKillObject(m_localId);
                    m_scene.NotifyMyCoarseLocationChange();
                }
            }
        }

        #endregion

        /// <summary>
        /// This allows the Sim owner the abiility to kick users from their sim currently.
        /// It tells the client that the agent has permission to do so.
        /// </summary>
        public void GrantGodlikePowers(LLUUID agentID, LLUUID sessionID, LLUUID token)
        {
            GrantGodlikePowersPacket respondPacket = new GrantGodlikePowersPacket();
            GrantGodlikePowersPacket.GrantDataBlock gdb = new GrantGodlikePowersPacket.GrantDataBlock();
            GrantGodlikePowersPacket.AgentDataBlock adb = new GrantGodlikePowersPacket.AgentDataBlock();

            adb.AgentID = agentID;
            adb.SessionID = sessionID; // More security

            gdb.GodLevel = (byte) 100;
            gdb.Token = token;
            //respondPacket.AgentData = (GrantGodlikePowersPacket.AgentDataBlock)ablock;
            respondPacket.GrantData = gdb;
            respondPacket.AgentData = adb;
            ControllingClient.OutPacket(respondPacket, ThrottleOutPacketType.Task);
        }

        /// <summary>
        /// This updates important decision making data about a child agent
        /// The main purpose is to figure out what objects to send to a child agent that's in a neighboring region
        /// </summary>
        public void ChildAgentDataUpdate(ChildAgentDataUpdate cAgentData)
        {
            // 
            m_DrawDistance = cAgentData.drawdistance;
            m_pos = new LLVector3(cAgentData.Position.x, cAgentData.Position.y, cAgentData.Position.z);
            m_CameraCenter =
                new Vector3(cAgentData.cameraPosition.x, cAgentData.cameraPosition.y, cAgentData.cameraPosition.z);
            m_godlevel = cAgentData.godlevel;
            ControllingClient.SetChildAgentThrottle(cAgentData.throttles);
            //cAgentData.AVHeight;
            //cAgentData.regionHandle;
            //m_velocity = cAgentData.Velocity;
        }

        /// <summary>
        /// 
        /// </summary>
        public static void LoadAnims()
        {
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

                        Velocity = new LLVector3(force.X, force.Y, force.Z);
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
            LLObject.TextureEntry textu = AvatarAppearance.GetDefaultTextureEntry();
            DefaultTexture = textu.ToBytes();
        }

        public class NewForce
        {
            public float X;
            public float Y;
            public float Z;

            public NewForce()
            {
            }
        }

        public class ScenePartUpdate
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

            m_physicsActor = scene.AddAvatar(Firstname + "." + Lastname, pVec);
            m_physicsActor.OnRequestTerseUpdate += SendTerseUpdateToAllClients;
            m_physicsActor.OnCollisionUpdate += PhysicsCollisionUpdate;
        }

        // Event called by the physics plugin to tell the avatar about a collision.
        private void PhysicsCollisionUpdate(EventArgs e)
        {
            bool isUserMoving = Velocity.X > 0 || Velocity.Y > 0;
            UpdateMovementAnimations(isUserMoving);
        }

        internal void Close()
        {
            RemoveFromPhysicalScene();
        }
    }
}