/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
using System.IO;
using Axiom.Math;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Physics.Manager;

namespace OpenSim.Region.Environment.Scenes
{
    public partial class ScenePresence : Entity
    {
        public static bool PhysicsEngineFlying = false;
        public static AvatarAnimations Animations;
        public static byte[] DefaultTexture;
        public string firstname;
        public string lastname;
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
        private ulong m_regionHandle;

        public bool childAgent = false;
        public bool IsRestrictedToRegion = false;

        private bool newForce = false;
        private bool newAvatar = false;

        protected RegionInfo m_regionInfo;
        protected ulong crossingFromRegion = 0;

        private Vector3[] Dir_Vectors = new Vector3[6];
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

        #region Properties
        /// <summary>
        /// 
        /// </summary>
        public PhysicsActor PhysActor
        {
            set
            {
                this._physActor = value;
            }
            get
            {
                return _physActor;
            }
        }

        public bool Updated
        {
            set
            {
                this.updateflag = value;
            }
            get
            {
                return this.updateflag;
            }
        }

	public ulong RegionHandle
	{
	    get { return m_regionHandle; }
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
            this.m_uuid = theClient.AgentId;

            m_regionInfo = reginfo;
            m_regionHandle = reginfo.RegionHandle;
            MainLog.Instance.Verbose("Avatar.cs ");
            ControllingClient = theClient;
            this.firstname = ControllingClient.FirstName;
            this.lastname = ControllingClient.LastName;
            m_localId = m_scene.NextLocalId;
            Pos = ControllingClient.StartPos;
            visualParams = new byte[218];
            for (int i = 0; i < 218; i++)
            {
                visualParams[i] = 100;
            }

            Wearables = AvatarWearable.DefaultWearables;
            Animations = new ScenePresence.AvatarAnimations();
            Animations.LoadAnims();

          //  this.avatarAppearanceTexture = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));

            //register for events
            ControllingClient.OnRequestWearables += this.SendOurAppearance;
            //ControllingClient.OnSetAppearance += new SetAppearance(this.SetAppearance);
            ControllingClient.OnCompleteMovementToRegion += this.CompleteMovement;
            ControllingClient.OnCompleteMovementToRegion += this.SendInitialData;
            ControllingClient.OnAgentUpdate += this.HandleAgentUpdate;
            // ControllingClient.OnStartAnim += new StartAnim(this.SendAnimPack);
            // ControllingClient.OnChildAgentStatus += new StatusChange(this.ChildStatusChange);
            //ControllingClient.OnStopMovement += new GenericCall2(this.StopMovement);
            
            Dir_Vectors[0] = new Vector3(1, 0, 0);  //FOWARD
            Dir_Vectors[1] = new Vector3(-1, 0, 0); //BACK
            Dir_Vectors[2] = new Vector3(0, 1, 0);  //LEFT
            Dir_Vectors[3] = new Vector3(0, -1, 0); //RIGHT
            Dir_Vectors[4] = new Vector3(0, 0, 1);  //UP
            Dir_Vectors[5] = new Vector3(0, 0, -1); //DOWN

        }
        #endregion

        #region Status Methods
        /// <summary>
        /// Not Used, most likely can be deleted
        /// </summary>
        /// <param name="status"></param>
        public void ChildStatusChange(bool status)
        {
            this.childAgent = status;

            if (this.childAgent == true)
            {
                this.Velocity = new LLVector3(0, 0, 0);
                this.Pos = new LLVector3(128, 128, 70);

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void MakeAvatar(LLVector3 pos, bool isFlying)
        {
            //this.childAvatar = false;
            this.Pos = pos;
            this._physActor.Flying = isFlying;
            this.newAvatar = true;
            this.childAgent = false;
        }

        protected void MakeChildAgent()
        {
            this.Velocity = new LLVector3(0, 0, 0);
            this.childAgent = true;
            //this.Pos = new LLVector3(128, 128, 70);  
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void Teleport(LLVector3 pos)
        {
            this.Pos = pos;
            this.SendTerseUpdateToALLClients();
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

        }

        /// <summary>
        /// Complete Avatar's movement into the region
        /// </summary>
        public void CompleteMovement()
        {
            LLVector3 look = this.Velocity;
            if ((look.X == 0) && (look.Y == 0) && (look.Z == 0))
            {
                look = new LLVector3(0.99f, 0.042f, 0);
            }
            this.ControllingClient.MoveAgentIntoRegion(m_regionInfo, Pos, look);
            if (this.childAgent)
            {
                this.childAgent = false;
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

            this.PhysActor.Flying = ((flags & (uint)MainAvatar.ControlFlags.AGENT_CONTROL_FLY) != 0);

            if (q != this.bodyRot)
            {
                this.bodyRot = q;
                update_rotation = true;
            }
            foreach (Dir_ControlFlags DCF in Enum.GetValues(typeof(Dir_ControlFlags)))
            {
                if ((flags & (uint)DCF) != 0)
                {
                    DCFlagKeyPressed = true;
                    agent_control_v3 += Dir_Vectors[i];
                    if ((movementflag & (uint)DCF) == 0)
                    {
                        movementflag += (byte)(uint)DCF;
                        update_movementflag = true;
                    }
                }
                else
                {
                    if ((movementflag & (uint)DCF) != 0)
                    {
                        movementflag -= (byte)(uint)DCF;
                        update_movementflag = true;
                      
                    }
                }
                i++;
            }
            if ((update_movementflag) || (update_rotation && DCFlagKeyPressed))
            {
                this.AddNewMovement(agent_control_v3, q);
            }
            UpdateMovementAnimations(update_movementflag);
        }

        protected void UpdateMovementAnimations(bool update_movementflag)
        {
            if (update_movementflag)
            {
                if (movementflag != 0)
                {
                    if (this._physActor.Flying)
                    {
                        this.SendAnimPack(Animations.AnimsLLUUID["FLY"], 1);
                    }
                    else
                    {
                        this.SendAnimPack(Animations.AnimsLLUUID["WALK"], 1);
                    }
                }
                else
                {
                    this.SendAnimPack(Animations.AnimsLLUUID["STAND"], 1);
                }
            }

        }


        protected void AddNewMovement(Vector3 vec, Quaternion rotation)
        {
            NewForce newVelocity = new NewForce();
            Vector3 direc = rotation * vec;
            direc.Normalize();

            direc = direc * ((0.03f) * 128f);
            if (this._physActor.Flying)
                direc *= 4;

            newVelocity.X = direc.x;
            newVelocity.Y = direc.y;
            newVelocity.Z = direc.z;
            this.forcesList.Add(newVelocity);
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
            if (this.childAgent == false)
            {
                if (this.newForce)
                {
                    this.SendTerseUpdateToALLClients();
                    _updateCount = 0;
                }
                else if (movementflag != 0)
                {
                    _updateCount++;
                    if (_updateCount > 3)
                    {
                        this.SendTerseUpdateToALLClients();
                        _updateCount = 0;
                    }
                }

                this.CheckForSignificantMovement();
                this.CheckForBorderCrossing();
                
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
            LLVector3 pos = this.Pos;
            LLVector3 vel = this.Velocity;
            RemoteClient.SendAvatarTerseUpdate(this.m_regionHandle, 64096, this.LocalId, new LLVector3(pos.X, pos.Y, pos.Z), new LLVector3(vel.X, vel.Y, vel.Z));
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendTerseUpdateToALLClients()
        {
            List<ScenePresence> avatars = this.m_scene.RequestAvatarList();
            for (int i = 0; i < avatars.Count; i++)
            {
                this.SendTerseUpdateToClient(avatars[i].ControllingClient);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteAvatar"></param>
        public void SendFullUpdateToOtherClient(ScenePresence remoteAvatar)
        {
            remoteAvatar.ControllingClient.SendAvatarData(m_regionInfo.RegionHandle, this.firstname, this.lastname, this.m_uuid, this.LocalId, this.Pos, DefaultTexture);
        }

        public void SendFullUpdateToALLClients()
        {
            List<ScenePresence> avatars = this.m_scene.RequestAvatarList();
            foreach (ScenePresence avatar in this.m_scene.RequestAvatarList())
            {
                this.SendFullUpdateToOtherClient(avatar);
                avatar.SendFullUpdateToOtherClient(this);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendInitialData()
        {
            this.ControllingClient.SendAvatarData(m_regionInfo.RegionHandle, this.firstname, this.lastname, this.m_uuid, this.LocalId, this.Pos, DefaultTexture);
            if (this.newAvatar)
            {
                this.m_scene.InformClientOfNeighbours(this.ControllingClient);
                this.newAvatar = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="OurClient"></param>
        public void SendOurAppearance(IClientAPI OurClient)
        {
            this.ControllingClient.SendWearables(this.Wearables);
            this.SendFullUpdateToALLClients();
            this.m_scene.SendAllSceneObjectsToClient(this.ControllingClient);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="avatarInfo"></param>
        public void SendAppearanceToOtherAgent(ScenePresence avatarInfo)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="animID"></param>
        /// <param name="seq"></param>
        public void SendAnimPack(LLUUID animID, int seq)
        {
	    this.current_anim = animID;
	    this.anim_seq = seq;
	    List<ScenePresence> avatars = this.m_scene.RequestAvatarList();
	    for (int i = 0; i < avatars.Count; i++)
	    {
	    	avatars[i].ControllingClient.SendAnimation(animID, seq, this.ControllingClient.AgentId);
	    }
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendAnimPack()
        {
            this.SendAnimPack(this.current_anim, this.anim_seq);
        }
        #endregion

        #region Significant Movement Method

        protected void CheckForSignificantMovement()
        {
            if (libsecondlife.Helpers.VecDist(this.Pos, this.posLastSignificantMove) > 2.0)
            {
                this.posLastSignificantMove = this.Pos;
                if (OnSignificantClientMovement != null)
                {
                    OnSignificantClientMovement(this.ControllingClient);
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
            LLVector3 pos2 = this.Pos;
            LLVector3 vel = this.Velocity;

            float timeStep = 0.2f;
            pos2.X = pos2.X + (vel.X * timeStep);
            pos2.Y = pos2.Y + (vel.Y * timeStep);
            pos2.Z = pos2.Z + (vel.Z * timeStep);

            if ((pos2.X < 0) || (pos2.X > 256))
            {
                this.CrossToNewRegion();
            }

            if ((pos2.Y < 0) || (pos2.Y > 256))
            {
                this.CrossToNewRegion();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        protected void CrossToNewRegion()
        {
            LLVector3 pos = this.Pos;
            LLVector3 newpos = new LLVector3(pos.X, pos.Y, pos.Z);
            uint neighbourx = this.m_regionInfo.RegionLocX;
            uint neighboury = this.m_regionInfo.RegionLocY;

            if (pos.X < 2)
            {
                neighbourx -= 1;
                newpos.X = 254;
            }
            if (pos.X > 253)
            {
                neighbourx += 1;
                newpos.X = 1;
            }
            if (pos.Y < 2)
            {
                neighboury -= 1;
                newpos.Y = 254;
            }
            if (pos.Y > 253)
            {
                neighboury += 1;
                newpos.Y = 1;
            }

            LLVector3 vel = this.m_velocity;
            ulong neighbourHandle = Helpers.UIntsToLong((uint)(neighbourx * 256), (uint)(neighboury * 256));
            RegionInfo neighbourRegion = this.m_scene.RequestNeighbouringRegionInfo(neighbourHandle);
            if (neighbourRegion != null)
            {
                bool res = this.m_scene.InformNeighbourOfCrossing(neighbourHandle, this.ControllingClient.AgentId, newpos, this._physActor.Flying);
                if (res)
                {
                    this.ControllingClient.CrossRegion(neighbourHandle, newpos, vel, neighbourRegion.ExternalEndPoint);
                    this.MakeChildAgent();
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
            lock (this.forcesList)
            {
                if (this.forcesList.Count > 0)
                {
                    for (int i = 0; i < this.forcesList.Count; i++)
                    {
                        NewForce force = this.forcesList[i];

                        this.updateflag = true;
                        this.Velocity = new LLVector3(force.X, force.Y, force.Z);
                        this.newForce = true;
                    }
                    for (int i = 0; i < this.forcesList.Count; i++)
                    {
                        this.forcesList.RemoveAt(0);
                    }
                }
            }
        }

        public static void LoadTextureFile(string name)
        {
            FileInfo fInfo = new FileInfo(name);
            long numBytes = fInfo.Length;
            FileStream fStream = new FileStream(name, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fStream);
            byte[] data1 = br.ReadBytes((int)numBytes);
            br.Close();
            fStream.Close();
            DefaultTexture = data1;
           // LLObject.TextureEntry textu = new LLObject.TextureEntry(data1, 0, data1.Length);
           // Console.WriteLine("default texture entry: " + textu.ToString());
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
    }

}
