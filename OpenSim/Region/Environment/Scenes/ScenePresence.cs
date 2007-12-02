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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using OpenSim.Framework.Communications;
using OpenSim.Region.Environment.Types;
using OpenSim.Region.Physics.Manager;


namespace OpenSim.Region.Environment.Scenes
{
    public partial class ScenePresence : EntityBase
    {
        public static AvatarAnimations Animations;
        public static byte[] DefaultTexture;

        public LLUUID CurrentAnimation;
        public int AnimationSeq;

        private bool m_updateflag = false;
        private byte m_movementflag = 0;
        private readonly List<NewForce> m_forcesList = new List<NewForce>();
        private short m_updateCount = 0;
        private uint m_requestedSitTargetID = 0;
        private LLVector3 m_requestedSitOffset = new LLVector3();
        private float m_sitAvatarHeight = 2.0f;
        private bool m_oldColliding = true;

        private bool m_isTyping = false;
        private bool m_setAlwaysRun = false;

        private Quaternion m_bodyRot;
        private byte[] m_visualParams;
        private AvatarWearable[] m_wearables;
        private LLObject.TextureEntry m_textureEntry;

        public bool IsRestrictedToRegion = false;

        private bool m_newForce = false;
        private bool m_newAvatar = false;
        private bool m_newCoarseLocations = true;
        private bool m_gotAllObjectsInScene = false;
        private float m_avHeight = 127.0f;

        protected RegionInfo m_regionInfo;
        protected ulong crossingFromRegion = 0;

        private readonly Vector3[] Dir_Vectors = new Vector3[6];
        private LLVector3 lastPhysPos = new LLVector3();
        private int m_wearablesSerial = 1;


        // Position of agent's camera in world
        protected Vector3 m_CameraCenter = new Vector3(0, 0, 0);

        // Use these three vectors to figure out what the agent is looking at
        // Convert it to a Matrix and/or Quaternion
        protected Vector3 m_CameraAtAxis = new Vector3(0, 0, 0);
        protected Vector3 m_CameraLeftAxis = new Vector3(0, 0, 0);
        protected Vector3 m_CameraUpAxis = new Vector3(0, 0, 0);

        // Agent's Draw distance.
        protected float m_DrawDistance = 0f;

        private readonly List<ulong> m_knownChildRegions = new List<ulong>(); //neighbouring regions we have enabled a child agent in

        private enum Dir_ControlFlags
        {
            DIR_CONTROL_FLAG_FOWARD = MainAvatar.ControlFlags.AGENT_CONTROL_AT_POS,
            DIR_CONTROL_FLAG_BACK = MainAvatar.ControlFlags.AGENT_CONTROL_AT_NEG,
            DIR_CONTROL_FLAG_LEFT = MainAvatar.ControlFlags.AGENT_CONTROL_LEFT_POS,
            DIR_CONTROL_FLAG_RIGHT = MainAvatar.ControlFlags.AGENT_CONTROL_LEFT_NEG,
            DIR_CONTROL_FLAG_UP = MainAvatar.ControlFlags.AGENT_CONTROL_UP_POS,
            DIR_CONTROL_FLAG_DOWN = MainAvatar.ControlFlags.AGENT_CONTROL_UP_NEG
        }

        /// <summary>
        /// Position at which a significant movement was made
        /// </summary>
        private LLVector3 posLastSignificantMove = new LLVector3();

        public delegate void SignificantClientMovement(IClientAPI remote_client);

        public event SignificantClientMovement OnSignificantClientMovement;

        //public List<SceneObjectGroup> InterestList = new List<SceneObjectGroup>();

        // private string m_currentQuadNode = " ";

        // private Queue<SceneObjectPart> m_fullPartUpdates = new Queue<SceneObjectPart>();
        //private Queue<SceneObjectPart> m_tersePartUpdates = new Queue<SceneObjectPart>();

        private UpdateQueue m_partsUpdateQueue = new UpdateQueue();
        private Dictionary<LLUUID, ScenePartUpdate> m_updateTimes = new Dictionary<LLUUID, ScenePartUpdate>();

        #region Properties

        /// <summary>
        /// 
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

        private readonly IClientAPI m_controllingClient;
        protected PhysicsActor m_physicsActor;

        public IClientAPI ControllingClient
        {
            get { return m_controllingClient; }
        }

        protected LLVector3 m_parentPosition = new LLVector3();

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

        public List<ulong> KnownChildRegions
        {
            get { return m_knownChildRegions; }
        }
        #endregion

        #region Constructor(s)

        public ScenePresence(IClientAPI client, Scene world, RegionInfo reginfo, byte[] visualParams,
                             AvatarWearable[] wearables)
        {
            m_scene = world;
            m_uuid = client.AgentId;

            m_regionInfo = reginfo;
            m_regionHandle = reginfo.RegionHandle;
            m_controllingClient = client;
            m_firstname = m_controllingClient.FirstName;
            m_lastname = m_controllingClient.LastName;
            m_localId = m_scene.NextLocalId;
            AbsolutePosition = m_controllingClient.StartPos;

            m_visualParams = visualParams;
            m_wearables = wearables;

            Animations = new AvatarAnimations();
            Animations.LoadAnims();

            //register for events
            m_controllingClient.OnRequestWearables += SendOwnAppearance;
            m_controllingClient.OnSetAppearance += SetAppearance;
            m_controllingClient.OnCompleteMovementToRegion += CompleteMovement;
            m_controllingClient.OnCompleteMovementToRegion += SendInitialData;
            m_controllingClient.OnAgentUpdate += HandleAgentUpdate;
            m_controllingClient.OnAgentRequestSit += HandleAgentRequestSit;
            m_controllingClient.OnAgentSit += HandleAgentSit;
            m_controllingClient.OnSetAlwaysRun += HandleSetAlwaysRun;
            
            // ControllingClient.OnStartAnim += new StartAnim(this.SendAnimPack);
            // ControllingClient.OnChildAgentStatus += new StatusChange(this.ChildStatusChange);
            //ControllingClient.OnStopMovement += new GenericCall2(this.StopMovement);

            Dir_Vectors[0] = new Vector3(1, 0, 0); //FOWARD
            Dir_Vectors[1] = new Vector3(-1, 0, 0); //BACK
            Dir_Vectors[2] = new Vector3(0, 1, 0); //LEFT
            Dir_Vectors[3] = new Vector3(0, -1, 0); //RIGHT
            Dir_Vectors[4] = new Vector3(0, 0, 1); //UP
            Dir_Vectors[5] = new Vector3(0, 0, -1); //DOWN

            m_textureEntry = new LLObject.TextureEntry(DefaultTexture, 0, DefaultTexture.Length);

            //temporary until we move some code into the body classes

            if (m_newAvatar)
            {
                //do we need to use newAvatar? not sure so have added this to kill the compile warning
            }

            m_scene.LandManager.sendLandUpdate(this);
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
                            part.SendFullUpdate(ControllingClient);
                            
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
                        part.SendFullUpdate(ControllingClient);
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

        #region Status Methods

        public void MakeRootAgent(LLVector3 pos, bool isFlying)
        {
            m_newAvatar = true;
            m_isChildAgent = false;

            AbsolutePosition = pos;

            AddToPhysicalScene();
            m_physicsActor.Flying = isFlying;

            //if (!m_gotAllObjectsInScene)
            //{
                //m_scene.SendAllSceneObjectsToClient(this);
                //m_gotAllObjectsInScene = true;
            //}

        }

        public void MakeChildAgent()
        {
            Velocity = new LLVector3(0, 0, 0);
            m_isChildAgent = true;

            RemoveFromPhysicalScene();
            
            //this.Pos = new LLVector3(128, 128, 70);  
        }

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
        /// 
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="visualParam"></param>
        public void SetAppearance(byte[] texture, AgentSetAppearancePacket.VisualParamBlock[] visualParam)
        {
            LLObject.TextureEntry textureEnt = new LLObject.TextureEntry(texture, 0, texture.Length);
            m_textureEntry = textureEnt;
            
            for (int i = 0; i < visualParam.Length; i++)
            {
                m_visualParams[i] = visualParam[i].ParamValue;
                //OpenSim.Framework.Console.MainLog.Instance.Verbose("CLIENT", "VisualData[" + i.ToString() + "]: " + visualParam[i].ParamValue.ToString() + "m");  
            }
            
            // Teravus : Nifty AV Height Getting Maaaaagical formula.  Oh how we love turning 0-255 into meters.
            // (float)m_visualParams[25] = Height
            // (float)m_visualParams[125] = LegLength
            m_avHeight = (1.50856f + (((float)m_visualParams[25] / 255.0f) * (2.525506f - 1.50856f)))
                + (((float)m_visualParams[125] / 255.0f) / 1.5f);
            if (PhysicsActor != null)
            {
                PhysicsVector SetSize = new PhysicsVector(0.45f, 0.6f, m_avHeight);
                PhysicsActor.Size = SetSize;
            }
            //OpenSim.Framework.Console.MainLog.Instance.Verbose("CLIENT", "Set Avatar Height to: " + (1.50856f + (((float)m_visualParams[25] / 255.0f) * (2.525506f - 1.50856f))).ToString() + "m" + " Leglength: " + ((float)m_visualParams[125]).ToString() + ":" + (((float)m_visualParams[125] / 255.0f)).ToString() + "m");
            SendAppearanceToAllOtherAgents();
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

        public void HandleAgentUpdate(IClientAPI remoteClient,AgentUpdatePacket agentData )
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

            // We don't know the agent's draw distance until the first agentUpdate packet
            //if (m_DrawDistance > 0)
            //{
                //if (!m_gotAllObjectsInScene && m_DrawDistance > 0)
                //{
                    // This will need to end up being a space based invalidator
                    // where we send object updates on spaces in 3d space (possibily a cube)
                    // that the avatar hasn't been surrounding it's draw distance.
                    // It would be better if the distance increased incrementally 
                    // until there was no space to update because either the avatar's draw
                    // distance is smaller then the space they've been or the avatar has explored
                    // all the space in the sim.

                    //m_scene.SendAllSceneObjectsToClient(this);
                    //m_gotAllObjectsInScene = true;
                //}
            //}
            //MainLog.Instance.Verbose("CAMERA", "AtAxis:" + m_CameraAtAxis.ToString() + " Center:" + m_CameraCenter.ToString() + " LeftAxis:" + m_CameraLeftAxis.ToString() + " UpAxis:" + m_CameraUpAxis.ToString() + " Far:" + m_CameraFar);
           

            if ((flags & (uint) MainAvatar.ControlFlags.AGENT_CONTROL_STAND_UP) != 0)
            {
                StandUp();
            }

            if (PhysicsActor == null)
            {
                // Console.WriteLine("DEBUG: HandleAgentUpdate: null PhysicsActor!");
                return;
            }

            if (m_allowMovement)
            {
                int i = 0;
                bool update_movementflag = false;
                bool update_rotation = false;
                bool DCFlagKeyPressed = false;
                Vector3 agent_control_v3 = new Vector3(0, 0, 0);
                Quaternion q = new Quaternion(bodyRotation.W, bodyRotation.X, bodyRotation.Y, bodyRotation.Z);
                bool oldflying = PhysicsActor.Flying;


                PhysicsActor.Flying = ((flags & (uint)MainAvatar.ControlFlags.AGENT_CONTROL_FLY) != 0);
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
                    foreach (Dir_ControlFlags DCF in Enum.GetValues(typeof(Dir_ControlFlags)))
                    {
                        if ((flags & (uint)DCF) != 0)
                        {
                            DCFlagKeyPressed = true;
                            agent_control_v3 += Dir_Vectors[i];
                            if ((m_movementflag & (uint)DCF) == 0)
                            {
                                m_movementflag += (byte)(uint)DCF;
                                update_movementflag = true;
                            }
                        }
                        else
                        {
                            if ((m_movementflag & (uint)DCF) != 0)
                            {
                                m_movementflag -= (byte)(uint)DCF;
                                update_movementflag = true;
                            }
                        }
                        i++;
                    }
                }

                if ((update_movementflag) || (update_rotation && DCFlagKeyPressed))
                {
                    AddNewMovement(agent_control_v3, q);
                    UpdateMovementAnimations(update_movementflag);
                }
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
                m_pos += m_parentPosition + new LLVector3(0.0f, 0.0f, 2.0f*m_sitAvatarHeight);
                m_parentPosition = new LLVector3();

                AddToPhysicalScene();

                m_parentID = 0;
                SendFullUpdateToAllClients();
            }
            
            UpdateMovementAnimations(true);
        }

        private void SendSitResponse(IClientAPI remoteClient, LLUUID targetID, LLVector3 offset)
        {
            AvatarSitResponsePacket avatarSitResponse = new AvatarSitResponsePacket();

            avatarSitResponse.SitObject.ID = targetID;

            bool autopilot = true;
            LLVector3 pos = new LLVector3();

            SceneObjectPart part = m_scene.GetSceneObjectPart(targetID);
            if (part != null)
            {
                pos = part.AbsolutePosition + offset;

                if (m_physicsActor != null)
                {
                    m_sitAvatarHeight = m_physicsActor.Size.Z;
                }

// this doesn't seem to quite work yet....
//                 // if we're close, set the avatar position to the target position and forgo autopilot
//                 if (AbsolutePosition.GetDistanceTo(pos) < 2.5)
//                 {
//                     autopilot = false;
//                     AbsolutePosition = pos + new LLVector3(0.0f, 0.0f, m_sitAvatarHeight);
//                 }
            }

            avatarSitResponse.SitTransform.AutoPilot = autopilot;
            avatarSitResponse.SitTransform.SitPosition = offset;
            avatarSitResponse.SitTransform.SitRotation = new LLQuaternion(0.0f, 0.0f, 0.0f, 1.0f);

            remoteClient.OutPacket(avatarSitResponse, ThrottleOutPacketType.Task);
        }

        public void HandleAgentRequestSit(IClientAPI remoteClient, LLUUID agentID, LLUUID targetID, LLVector3 offset)
        {
            SendSitResponse(remoteClient, targetID, offset);

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
        }

        public void HandleAgentSit(IClientAPI remoteClient, LLUUID agentID)
        {
            SceneObjectPart part = m_scene.GetSceneObjectPart(m_requestedSitTargetID);

            if (part != null)
            {
                m_pos -= part.AbsolutePosition;
                m_parentPosition = part.AbsolutePosition;
            }

            m_parentID = m_requestedSitTargetID;

            Velocity = new LLVector3(0, 0, 0);
            RemoveFromPhysicalScene();

            SendAnimPack(Animations.AnimsLLUUID["SIT"], 1);
            SendFullUpdateToAllClients();
        }

        public void HandleSetAlwaysRun(IClientAPI remoteClient, bool SetAlwaysRun)
        {
            m_setAlwaysRun = SetAlwaysRun;
            if (PhysicsActor != null)
            {
                PhysicsActor.SetAlwaysRun = SetAlwaysRun;
            }

        }

        protected void UpdateMovementAnimations(bool update_movementflag)
        {
            if (update_movementflag)
            {
                if (m_movementflag != 0)
                {
                    if (m_physicsActor.Flying)
                    {
                        SendAnimPack(Animations.AnimsLLUUID["FLY"], 1);
                    }
                    else
                    {
                        if (((m_movementflag & (uint) MainAvatar.ControlFlags.AGENT_CONTROL_UP_NEG) != 0) &&
                            PhysicsActor.IsColliding)
                        {
                            SendAnimPack(Animations.AnimsLLUUID["CROUCHWALK"], 1);
                        }
                        else
                        {
                            if (!PhysicsActor.IsColliding && m_physicsActor.Velocity.Z < -6)
                            {
                                SendAnimPack(Animations.AnimsLLUUID["FALLDOWN"], 1);
                            }
                            else if (!PhysicsActor.IsColliding && Velocity.Z > 0 && (m_movementflag & (uint) MainAvatar.ControlFlags.AGENT_CONTROL_UP_POS) != 0)
                            {
                                SendAnimPack(Animations.AnimsLLUUID["JUMP"], 1);
                            }
                            else
                            {
                                if (!m_setAlwaysRun)
                                {
                                    SendAnimPack(Animations.AnimsLLUUID["WALK"], 1);
                                }
                                else
                                {
                                    SendAnimPack(Animations.AnimsLLUUID["RUN"], 1);
                                }
                            }
                        }
                    }
                }
                else if (m_parentID != 0)
                {
                    if (m_isTyping)
                    {
                        SendAnimPack(Animations.AnimsLLUUID["TYPE"], 1);
                    }
                    else
                    {
                        // TODO: stop the typing animation, continue sitting
                        SendAnimPack(Animations.AnimsLLUUID["SIT"], 1);
                    }
                }
                else
                {
                    if (((m_movementflag & (uint) MainAvatar.ControlFlags.AGENT_CONTROL_UP_NEG) != 0) &&
                        PhysicsActor.IsColliding)
                    {
                        SendAnimPack(Animations.AnimsLLUUID["CROUCH"], 1);
                    }
                    else if (m_isTyping)
                    {
                        SendAnimPack(Animations.AnimsLLUUID["TYPE"], 1);
                    }
                    else
                    {
                        if (!PhysicsActor.IsColliding && m_physicsActor.Velocity.Z < -6 && !m_physicsActor.Flying)
                        {
                            SendAnimPack(Animations.AnimsLLUUID["FALLDOWN"], 1);
                        }
                        else if (!PhysicsActor.IsColliding && Velocity.Z > 0 && !m_physicsActor.Flying && (m_movementflag & (uint) MainAvatar.ControlFlags.AGENT_CONTROL_UP_POS) != 0)
                        {
                            SendAnimPack(Animations.AnimsLLUUID["JUMP"], 1);
                        }
                        else
                        {
                            if (!m_physicsActor.Flying)
                            {
                                SendAnimPack(Animations.AnimsLLUUID["STAND"], 1);
                            }
                        }
                        
                    }
                }
            }
        }

        protected void AddNewMovement(Vector3 vec, Quaternion rotation)
        {
            if (m_isChildAgent)
            {
                Console.WriteLine("DEBUG: AddNewMovement: child agent");
                return;
            }

            NewForce newVelocity = new NewForce();
            Vector3 direc = rotation*vec;
            direc.Normalize();

            direc = direc*((0.03f)*128f);
            if (m_physicsActor.Flying)
            {
                direc *= 4;
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
                        SendAnimPack(Animations.AnimsLLUUID["PREJUMP"], 1);
                        SendAnimPack(Animations.AnimsLLUUID["JUMP"], 1);
                    }
                }
            }

            newVelocity.X = direc.x;
            newVelocity.Y = direc.y;
            newVelocity.Z = direc.z;
            m_forcesList.Add(newVelocity);
        }

        public void setTyping(bool typing)
        {
            if (m_isChildAgent)
            {
                MainLog.Instance.Warn("setTyping called on child agent");
                return;
            }

            m_isTyping = typing;

            UpdateMovementAnimations(true);
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
                /// check for user movement 'forces' (ie commands to move)
                if (m_newForce)
                {
                    SendTerseUpdateToAllClients();
                    m_updateCount = 0;
                }

                    /// check for scripted movement (?)
                else if (m_movementflag != 0)
                {
                    m_updateCount++;
                    if (m_updateCount > 3)
                    {
                        SendTerseUpdateToAllClients();
                        m_updateCount = 0;
                    }
                }

                    /// check for physics-related movement
                else if (lastPhysPos.GetDistanceTo(AbsolutePosition) > 0.02)
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
        /// 
        /// </summary>
        /// <param name="RemoteClient"></param>
        public void SendTerseUpdateToClient(IClientAPI RemoteClient)
        {
            LLVector3 pos = m_pos;
            LLVector3 vel = Velocity;
            LLQuaternion rot;
            rot.X = m_bodyRot.x;
            rot.Y = m_bodyRot.y;
            rot.Z = m_bodyRot.z;
            rot.W = m_bodyRot.w;
            RemoteClient.SendAvatarTerseUpdate(m_regionHandle, 64096, LocalId, new LLVector3(pos.X, pos.Y, pos.Z),
                                               new LLVector3(vel.X, vel.Y, vel.Z), rot);
        }

        /// <summary>
        /// 
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
        /// 
        /// </summary>
        /// <param name="remoteAvatar"></param>
        public void SendFullUpdateToOtherClient(ScenePresence remoteAvatar)
        {
            remoteAvatar.m_controllingClient.SendAvatarData(m_regionInfo.RegionHandle, m_firstname, m_lastname, m_uuid,
                                                            LocalId, m_pos, m_textureEntry.ToBytes(), m_parentID);
        }

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
                                               m_pos, m_textureEntry.ToBytes(), m_parentID);
            if (!m_isChildAgent)
            {
                m_scene.InformClientOfNeighbours(this);
                m_newAvatar = false;
            }

            SendFullUpdateToAllClients();
            SendAppearanceToAllOtherAgents();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="client"></param>
        public void SendOwnAppearance( )
        {
            SendOwnWearables( );

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
                                                 // if (scenePresence != this)
                                                 // {
                                                 SendAppearanceToOtherAgent(scenePresence);
                                                 // }
                                             });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="avatarInfo"></param>
        public void SendAppearanceToOtherAgent(ScenePresence avatarInfo)
        {
            avatarInfo.m_controllingClient.SendAppearance(m_controllingClient.AgentId, m_visualParams,
                                                          m_textureEntry.ToBytes());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="animID"></param>
        /// <param name="seq"></param>
        public void SendAnimPack(LLUUID animID, int seq)
        {
            CurrentAnimation = animID;
            AnimationSeq = seq;
            LLUUID sourceAgentId = m_controllingClient.AgentId;

            m_scene.Broadcast(delegate(IClientAPI client) { client.SendAnimation(animID, seq, sourceAgentId); });
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendAnimPack()
        {
            SendAnimPack(CurrentAnimation, AnimationSeq);
        }

        #endregion

        #region Significant Movement Method

        protected void CheckForSignificantMovement()
        {
            if (AbsolutePosition.GetDistanceTo(posLastSignificantMove) > 0.02)
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
        /// 
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
        /// 
        /// </summary>
        protected void CrossToNewRegion()
        {
            LLVector3 pos = AbsolutePosition;
            LLVector3 newpos = new LLVector3(pos.X, pos.Y, pos.Z);
            uint neighbourx = m_regionInfo.RegionLocX;
            uint neighboury = m_regionInfo.RegionLocY;

            if (pos.X < 1.7F)
            {
                neighbourx -= 1;
                newpos.X = 255.9F;
            }
            if (pos.X > 254.3F)
            {
                neighbourx += 1;
                newpos.X = 0.1F;
            }
            if (pos.Y < 1.7F)
            {
                neighboury -= 1;
                newpos.Y = 255.9F;
            }
            if (pos.Y > 254.3F)
            {
                neighboury += 1;
                newpos.Y = 0.1F;
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


        public void GrantGodlikePowers(LLUUID agentID, LLUUID sessionID, LLUUID token)
        {
            GrantGodlikePowersPacket respondPacket = new GrantGodlikePowersPacket();
            GrantGodlikePowersPacket.GrantDataBlock gdb = new GrantGodlikePowersPacket.GrantDataBlock();
            GrantGodlikePowersPacket.AgentDataBlock adb = new GrantGodlikePowersPacket.AgentDataBlock();

            adb.AgentID = agentID;
            adb.SessionID = sessionID; // More security

            gdb.GodLevel = (byte)100;
            gdb.Token = token;
            //respondPacket.AgentData = (GrantGodlikePowersPacket.AgentDataBlock)ablock;
            respondPacket.GrantData = gdb;
            respondPacket.AgentData = adb;
            ControllingClient.OutPacket(respondPacket, ThrottleOutPacketType.Task);



        }
        /// <summary>
        /// 
        /// </summary>
        public static void LoadAnims()
        {
        }

        /// <summary>
        /// 
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
            LLObject.TextureEntry textu = new LLObject.TextureEntry(new LLUUID("C228D1CF-4B5D-4BA8-84F4-899A0796AA97"));
            textu.CreateFace(0).TextureID = new LLUUID("00000000-0000-1111-9999-000000000012");
            textu.CreateFace(1).TextureID = new LLUUID("5748decc-f629-461c-9a36-a35a221fe21f");
            textu.CreateFace(2).TextureID = new LLUUID("5748decc-f629-461c-9a36-a35a221fe21f");
            textu.CreateFace(3).TextureID = new LLUUID("6522E74D-1660-4E7F-B601-6F48C1659A77");
            textu.CreateFace(4).TextureID = new LLUUID("7CA39B4C-BD19-4699-AFF7-F93FD03D3E7B");
            textu.CreateFace(5).TextureID = new LLUUID("00000000-0000-1111-9999-000000000010");
            textu.CreateFace(6).TextureID = new LLUUID("00000000-0000-1111-9999-000000000011");
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
        private void PhysicsCollisionUpdate(EventArgs e)
        {
            bool isUserMoving = false;
            if (Velocity.X > 0 || Velocity.Y > 0)
                isUserMoving = true;
            UpdateMovementAnimations(isUserMoving);

        }
        internal void Close()
        {
            RemoveFromPhysicalScene();
        }

        public void SetWearable(int wearableId, AvatarWearable wearable)
        {
            m_wearables[wearableId] = wearable;
            SendOwnWearables();
            
        }

        private void SendOwnWearables()
        {
            m_controllingClient.SendWearables(m_wearables, m_wearablesSerial++);
        }
       
    }
}
