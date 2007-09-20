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
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public partial class ScenePresence : Entity
    {
        public static bool PhysicsEngineFlying = false;
        public static AvatarAnimations Animations;
        public static byte[] DefaultTexture;
        public IClientAPI ControllingClient;
        public LLUUID current_anim;
        public int anim_seq;
        private bool updateflag = false;
        private byte movementflag = 0;
        private List<NewForce> forcesList = new List<NewForce>();
        private short _updateCount = 0;

        private Quaternion bodyRot;
        private byte[] visualParams;
        private AvatarWearable[] Wearables;
        private LLObject.TextureEntry m_textureEntry;

        private ulong m_regionHandle;

        public bool childAgent = true;
        public bool IsRestrictedToRegion = false;

        private bool newForce = false;
        private bool newAvatar = false;
        private bool newCoarseLocations = true;

        protected RegionInfo m_regionInfo;
        protected ulong crossingFromRegion = 0;

        private IScenePresenceBody m_body;

        private Vector3[] Dir_Vectors = new Vector3[6];
        private LLVector3 lastPhysPos = new LLVector3();

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

        // private Queue<SceneObjectGroup> m_fullGroupUpdates = new Queue<SceneObjectGroup>();
        // private Queue<SceneObjectGroup> m_terseGroupUpdates = new Queue<SceneObjectGroup>();

        private Queue<SceneObjectPart> m_fullPartUpdates = new Queue<SceneObjectPart>();
        private Queue<SceneObjectPart> m_tersePartUpdates = new Queue<SceneObjectPart>();

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public PhysicsActor PhysActor
        {
            set { _physActor = value; }
            get { return _physActor; }
        }

        public bool Updated
        {
            set { updateflag = value; }
            get { return updateflag; }
        }

        public ulong RegionHandle
        {
            get { return m_regionHandle; }
        }

        private string m_firstname;

        public string Firstname
        {
            get { return m_firstname; }
        }

        private string m_lastname;

        public string Lastname
        {
            get { return m_lastname; }
        }

        #endregion

        #region Constructor(s)

        /// <summary>
        /// 
        /// </summary>
        /// <param name="theClient"></param>
        /// <param name="world"></param>
        /// <param name="clientThreads"></param>
        /// <param name="regionDat"></param>
        public ScenePresence(IClientAPI theClient, Scene world, RegionInfo reginfo)
        {
            m_scene = world;
            m_uuid = theClient.AgentId;

            m_regionInfo = reginfo;
            m_regionHandle = reginfo.RegionHandle;
            MainLog.Instance.Verbose("Avatar.cs ");
            ControllingClient = theClient;
            m_firstname = ControllingClient.FirstName;
            m_lastname = ControllingClient.LastName;
            m_localId = m_scene.NextLocalId;
            AbsolutePosition = ControllingClient.StartPos;

            visualParams = new byte[218];
            for (int i = 0; i < 218; i++)
            {
                visualParams[i] = 100;
            }

            Wearables = AvatarWearable.DefaultWearables;
            Animations = new AvatarAnimations();
            Animations.LoadAnims();

            //register for events
            ControllingClient.OnRequestWearables += SendOurAppearance;
            ControllingClient.OnSetAppearance += new SetAppearance(SetAppearance);
            ControllingClient.OnCompleteMovementToRegion += CompleteMovement;
            ControllingClient.OnCompleteMovementToRegion += SendInitialData;
            ControllingClient.OnAgentUpdate += HandleAgentUpdate;
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
            m_body = new ChildAgent();

            if (newAvatar)
            {
                //do we need to use newAvatar? not sure so have added this to kill the compile warning
            }
        }

        #endregion

        public void AddTersePart(SceneObjectPart part)
        {
            m_tersePartUpdates.Enqueue(part);
        }

        public void AddFullPart(SceneObjectPart part)
        {
            m_fullPartUpdates.Enqueue(part);
        }

        public void SendPrimUpdates()
        {
            if (m_tersePartUpdates.Count > 0)
            {
                bool terse = true;
                int terseCount = 0;

                while (terse)
                {
                    SceneObjectPart part = m_tersePartUpdates.Dequeue();
                    part.SendTerseUpdate(ControllingClient);
                    terseCount++;

                    if ((m_tersePartUpdates.Count < 1) | (terseCount > 30))
                    {
                        terse = false;
                    }
                }
            }
            if (m_fullPartUpdates.Count > 0)
            {
                bool full = true;
                int fullCount = 0;

                while (full)
                {
                    SceneObjectPart part = m_fullPartUpdates.Dequeue();
                    part.SendFullUpdate(ControllingClient);
                    fullCount++;
                    if ((m_fullPartUpdates.Count < 1) | (fullCount > 40))
                    {
                        full = false;
                    }
                }
            }
        }

        #region Status Methods

        /// <summary>
        /// Not Used, most likely can be deleted
        /// </summary>
        /// <param name="status"></param>
        public void ChildStatusChange(bool status)
        {
            childAgent = status;

            if (childAgent == true)
            {
                Velocity = new LLVector3(0, 0, 0);
                AbsolutePosition = new LLVector3(128, 128, 70);
            }
        }

        public void MakeAvatar(LLVector3 pos, bool isFlying)
        {
            newAvatar = true;
            childAgent = false;

            AbsolutePosition = pos;

            AddToPhysicalScene( );
            _physActor.Flying = isFlying;


            m_scene.SendAllSceneObjectsToClient(this);
        }

        protected void MakeChildAgent()
        {
            Velocity = new LLVector3(0, 0, 0);
            childAgent = true;

            RemoveFromPhysicalScene();

            //this.Pos = new LLVector3(128, 128, 70);  
        }

        private void RemoveFromPhysicalScene()
        {
            m_scene.PhysScene.RemoveAvatar( this.PhysActor );
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
                visualParams[i] = visualParam[i].ParamValue;
            }

            SendArrearanceToAllOtherAgents();
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
            ControllingClient.MoveAgentIntoRegion(m_regionInfo, AbsolutePosition, look);
            if (childAgent)
            {
                childAgent = false;

                //this.m_scene.SendAllSceneObjectsToClient(this.ControllingClient);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pack"></param>
        public void HandleAgentUpdate(IClientAPI remoteClient, uint flags, LLQuaternion bodyRotation)
        {
            int i = 0;
            bool update_movementflag = false;
            bool update_rotation = false;
            bool DCFlagKeyPressed = false;
            Vector3 agent_control_v3 = new Vector3(0, 0, 0);
            Quaternion q = new Quaternion(bodyRotation.W, bodyRotation.X, bodyRotation.Y, bodyRotation.Z);
            bool oldflying = PhysActor.Flying;
            PhysActor.Flying = ((flags & (uint) MainAvatar.ControlFlags.AGENT_CONTROL_FLY) != 0);
            if (PhysActor.Flying != oldflying)
            {
                update_movementflag = true;
            }

            if (q != bodyRot)
            {
                bodyRot = q;
                update_rotation = true;
            }
            foreach (Dir_ControlFlags DCF in Enum.GetValues(typeof (Dir_ControlFlags)))
            {
                if ((flags & (uint) DCF) != 0)
                {
                    DCFlagKeyPressed = true;
                    agent_control_v3 += Dir_Vectors[i];
                    if ((movementflag & (uint) DCF) == 0)
                    {
                        movementflag += (byte) (uint) DCF;
                        update_movementflag = true;
                    }
                }
                else
                {
                    if ((movementflag & (uint) DCF) != 0)
                    {
                        movementflag -= (byte) (uint) DCF;
                        update_movementflag = true;
                    }
                }
                i++;
            }
            if ((update_movementflag) || (update_rotation && DCFlagKeyPressed))
            {
                AddNewMovement(agent_control_v3, q);
            }
            UpdateMovementAnimations(update_movementflag);
        }

        protected void UpdateMovementAnimations(bool update_movementflag)
        {
            if (update_movementflag)
            {
                if (movementflag != 0)
                {
                    if (_physActor.Flying)
                    {
                        SendAnimPack(Animations.AnimsLLUUID["FLY"], 1);
                    }
                    else
                    {
                        SendAnimPack(Animations.AnimsLLUUID["WALK"], 1);
                    }
                }
                else
                {
                    SendAnimPack(Animations.AnimsLLUUID["STAND"], 1);
                }
            }
        }


        protected void AddNewMovement(Vector3 vec, Quaternion rotation)
        {
            NewForce newVelocity = new NewForce();
            Vector3 direc = rotation*vec;
            direc.Normalize();

            direc = direc*((0.03f)*128f);
            if (_physActor.Flying)
                direc *= 4;

            newVelocity.X = direc.x;
            newVelocity.Y = direc.y;
            newVelocity.Z = direc.z;
            forcesList.Add(newVelocity);
        }

        #endregion

        #region Overridden Methods

        /// <summary>
        /// 
        /// </summary>
        public override void LandRenegerated()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        public override void Update()
        {
            SendPrimUpdates();

            if (newCoarseLocations)
            {
                SendCoarseLocations();
                newCoarseLocations = false;
            }

            if (childAgent == false)
            {
                /// check for user movement 'forces' (ie commands to move)
                if (newForce)
                {
                    SendTerseUpdateToAllClients();
                    _updateCount = 0;
                }

                    /// check for scripted movement (?)
                else if (movementflag != 0)
                {
                    _updateCount++;
                    if (_updateCount > 3)
                    {
                        SendTerseUpdateToAllClients();
                        _updateCount = 0;
                    }
                }

                    /// check for physics-related movement
                else if (lastPhysPos.GetDistanceTo(AbsolutePosition) > 0.02)
                {
                    SendTerseUpdateToAllClients();
                    _updateCount = 0;
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
            LLVector3 pos = AbsolutePosition;
            LLVector3 vel = Velocity;
            LLQuaternion rot;
            rot.X = bodyRot.x;
            rot.Y = bodyRot.y;
            rot.Z = bodyRot.z;
            rot.W = bodyRot.w;
            RemoteClient.SendAvatarTerseUpdate(m_regionHandle, 64096, LocalId, new LLVector3(pos.X, pos.Y, pos.Z),
                                               new LLVector3(vel.X, vel.Y, vel.Z), rot);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendTerseUpdateToAllClients()
        {
            m_scene.ForEachScenePresence(delegate(ScenePresence presence)
                                             {
                                                 SendTerseUpdateToClient(presence.ControllingClient);
                                             });
        }


        public void SendCoarseLocations()
        {
            List<LLVector3> CoarseLocations = new List<LLVector3>();
            List<ScenePresence> avatars = m_scene.GetAvatars();
            for (int i = 0; i < avatars.Count; i++)
            {
                if (avatars[i] != this )
                {
                    CoarseLocations.Add(avatars[i].AbsolutePosition);
                }
            }

            ControllingClient.SendCoarseLocationUpdate(CoarseLocations);
        }

        public void CoarseLocationChange(ScenePresence avatar)
        {
            newCoarseLocations = true;
        }

        private void NotifyMyCoarseLocationChange()
        {
            m_scene.ForEachScenePresence(delegate(ScenePresence presence)
                                             {
                                                 if (presence != this)
                                                 {
                                                     presence.CoarseLocationChange(this);
                                                 }
                                             });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteAvatar"></param>
        public void SendFullUpdateToOtherClient(ScenePresence remoteAvatar)
        {
            remoteAvatar.ControllingClient.SendAvatarData(m_regionInfo.RegionHandle, m_firstname, m_lastname, m_uuid,
                                                          LocalId, AbsolutePosition, m_textureEntry.ToBytes());
        }

        public void SendFullUpdateToAllClients()
        {
            List<ScenePresence> avatars = m_scene.GetScenePresences();
            foreach (ScenePresence avatar in avatars)
            {
                SendFullUpdateToOtherClient(avatar);
                if (avatar.LocalId != LocalId)
                {
                    if (!avatar.childAgent)
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
            ControllingClient.SendAvatarData(m_regionInfo.RegionHandle, m_firstname, m_lastname, m_uuid, LocalId,
                                             AbsolutePosition, m_textureEntry.ToBytes());
            if (!childAgent)
            {
                m_scene.InformClientOfNeighbours(ControllingClient);
                newAvatar = false;
            }

            SendFullUpdateToAllClients();
            SendArrearanceToAllOtherAgents();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="OurClient"></param>
        public void SendOurAppearance(IClientAPI OurClient)
        {
            ControllingClient.SendWearables(Wearables);

            //this.SendFullUpdateToAllClients();
            //this.SendArrearanceToAllOtherAgents();

            m_scene.SendAllSceneObjectsToClient(this);
            ControllingClient.SendViewerTime(m_scene.TimePhase);

            //Please don't remove the following code (at least not yet), just leave it commented out
            //gives the user god powers, should help with debuging things in the future
            /*
            GrantGodlikePowersPacket grant = new GrantGodlikePowersPacket();
            grant.AgentData.AgentID = this.ControllingClient.AgentId;
            grant.AgentData.SessionID = this.ControllingClient.SessionId;
            grant.GrantData.GodLevel = 255;
            grant.GrantData.Token = LLUUID.Random();
            this.ControllingClient.OutPacket(grant);*/
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendArrearanceToAllOtherAgents()
        {
            m_scene.ForEachScenePresence(delegate(ScenePresence scenePresence)
                                             {
                                                 SendAppearanceToOtherAgent(scenePresence);
                                             });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="avatarInfo"></param>
        public void SendAppearanceToOtherAgent(ScenePresence avatarInfo)
        {
            avatarInfo.ControllingClient.SendAppearance(ControllingClient.AgentId, visualParams,
                                                        m_textureEntry.ToBytes());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="animID"></param>
        /// <param name="seq"></param>
        public void SendAnimPack(LLUUID animID, int seq)
        {
            current_anim = animID;
            anim_seq = seq;

            m_scene.ForEachScenePresence(delegate(ScenePresence scenePresence)
                                             {
                                                 scenePresence.ControllingClient.SendAnimation(animID, seq,
                                                                                               ControllingClient.AgentId);
                                             });
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendAnimPack()
        {
            SendAnimPack(current_anim, anim_seq);
        }

        #endregion

        #region Significant Movement Method

        protected void CheckForSignificantMovement()
        {
            if (Helpers.VecDist(AbsolutePosition, posLastSignificantMove) > 2.0)
            {
                posLastSignificantMove = AbsolutePosition;
                if (OnSignificantClientMovement != null)
                {
                    OnSignificantClientMovement(ControllingClient);
                    NotifyMyCoarseLocationChange();
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
            RegionInfo neighbourRegion = m_scene.RequestNeighbouringRegionInfo(neighbourHandle);
            if (neighbourRegion != null)
            {
                bool res =
                    m_scene.InformNeighbourOfCrossing(neighbourHandle, ControllingClient.AgentId, newpos,
                                                      _physActor.Flying);
                if (res)
                {
                    //TODO: following line is hard coded to port 9000, really need to change this as soon as possible
                    AgentCircuitData circuitdata = ControllingClient.RequestClientInfo();
                    string capsPath = Util.GetCapsURL(ControllingClient.AgentId);
                    ControllingClient.CrossRegion(neighbourHandle, newpos, vel, neighbourRegion.ExternalEndPoint,
                                                  capsPath);
                    MakeChildAgent();
                    m_scene.SendKillObject(m_localId);
                    NotifyMyCoarseLocationChange();
                }
            }
        }

        #endregion

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
            newForce = false;
            lock (forcesList)
            {
                if (forcesList.Count > 0)
                {
                    for (int i = 0; i < forcesList.Count; i++)
                    {
                        NewForce force = forcesList[i];

                        updateflag = true;
                        Velocity = new LLVector3(force.X, force.Y, force.Z);
                        newForce = true;
                    }
                    for (int i = 0; i < forcesList.Count; i++)
                    {
                        forcesList.RemoveAt(0);
                    }
                }
            }
        }

        public static void CreateDefaultTextureEntry()
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

        public override void SetText(string text, Vector3 color, double alpha)
        {
            throw new Exception("Can't set Text on avatar.");
        }

        public void AddToPhysicalScene( )
        {
            PhysicsScene scene = m_scene.PhysScene;

            PhysicsVector pVec =
                new PhysicsVector(AbsolutePosition.X, AbsolutePosition.Y,
                                  AbsolutePosition.Z);

            _physActor = scene.AddAvatar(pVec);           
        }
    }
}