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
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Inventory;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using Axiom.MathLib;

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
        private Axiom.MathLib.Quaternion bodyRot;
        private LLObject.TextureEntry avatarAppearanceTexture = null;
        private byte[] visualParams;
        private AvatarWearable[] Wearables;
        private LLVector3 positionLastFrame = new LLVector3(0, 0, 0);
        private ulong m_regionHandle;
        private bool childAgent = false;
        private bool newForce = false;
        private bool newAvatar = false;
        private IScenePresenceBody m_body;

        protected RegionInfo m_regionInfo;

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

            m_world = world;
            this.uuid = theClient.AgentId;

            m_regionInfo = reginfo;
            m_regionHandle = reginfo.RegionHandle;
            OpenSim.Framework.Console.MainLog.Instance.Verbose("Avatar.cs ");
            ControllingClient = theClient;
            this.firstname = ControllingClient.FirstName;
            this.lastname = ControllingClient.LastName;
            m_localId = m_world.NextLocalId;
            Pos = ControllingClient.StartPos;
            visualParams = new byte[218];
            for (int i = 0; i < 218; i++)
            {
                visualParams[i] = 100;
            }

            Wearables = AvatarWearable.DefaultWearables;
            
            this.avatarAppearanceTexture = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));

            //register for events
            ControllingClient.OnRequestWearables += this.SendOurAppearance;
            //ControllingClient.OnSetAppearance += new SetAppearance(this.SetAppearance);
            ControllingClient.OnCompleteMovementToRegion += this.CompleteMovement;
            ControllingClient.OnCompleteMovementToRegion += this.SendInitialData;
            ControllingClient.OnAgentUpdate += this.HandleAgentUpdate;
            // ControllingClient.OnStartAnim += new StartAnim(this.SendAnimPack);
            // ControllingClient.OnChildAgentStatus += new StatusChange(this.ChildStatusChange);
            //ControllingClient.OnStopMovement += new GenericCall2(this.StopMovement);

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
        public void MakeAvatar(LLVector3 pos)
        {
            //this.childAvatar = false;
            this.Pos = pos;
            this.newAvatar = true;
            this.childAgent = false;
        }

        protected void MakeChildAgent()
        {
            this.Velocity = new LLVector3(0, 0, 0);
            this.Pos = new LLVector3(128, 128, 70);
            this.childAgent = true;
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
            if ((flags & (uint)MainAvatar.ControlFlags.AGENT_CONTROL_AT_POS) != 0)
            {
                Axiom.MathLib.Quaternion q = new Axiom.MathLib.Quaternion(bodyRotation.W, bodyRotation.X, bodyRotation.Y, bodyRotation.Z);
                if (((movementflag & 1) == 0) || (q != this.bodyRot))
                {
                    Axiom.MathLib.Vector3 v3 = new Axiom.MathLib.Vector3(1, 0, 0);
                    this.AddNewMovement(v3, q);
                    movementflag = 1;
                    this.bodyRot = q;
                }
            }
            else if ((flags & (uint)MainAvatar.ControlFlags.AGENT_CONTROL_AT_NEG) != 0)
            {
                Axiom.MathLib.Quaternion q = new Axiom.MathLib.Quaternion(bodyRotation.W, bodyRotation.X, bodyRotation.Y, bodyRotation.Z);
                if (((movementflag & 2) == 0) || (q != this.bodyRot))
                {  
                    Axiom.MathLib.Vector3 v3 = new Axiom.MathLib.Vector3(-1, 0, 0);
                    this.AddNewMovement(v3, q);
                    movementflag = 2;
                    this.bodyRot = q;
                }
            }
            else
            {
                if ((movementflag) != 0)
                {
                    NewForce newVelocity = new NewForce();
                    newVelocity.X = 0;
                    newVelocity.Y = 0;
                    newVelocity.Z = 0;
                    this.forcesList.Add(newVelocity);
                    movementflag = 0;
                }
            }

        }

        protected void AddNewMovement(Axiom.MathLib.Vector3 vec, Axiom.MathLib.Quaternion rotation)
        {
            NewForce newVelocity = new NewForce();
            Axiom.MathLib.Vector3 direc = rotation * vec;
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
        public override void update()
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
            List<ScenePresence> avatars = this.m_world.RequestAvatarList();
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
            remoteAvatar.ControllingClient.SendAvatarData(m_regionInfo.RegionHandle, this.firstname, this.lastname, this.uuid, this.LocalId, this.Pos, DefaultTexture);
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendInitialData()
        {
            this.ControllingClient.SendAvatarData(m_regionInfo.RegionHandle, this.firstname, this.lastname, this.uuid, this.LocalId, this.Pos, DefaultTexture);
            if (this.newAvatar)
            {
                this.m_world.InformClientOfNeighbours(this.ControllingClient);
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


        }

        /// <summary>
        /// 
        /// </summary>
        public void SendAnimPack()
        {

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

            LLVector3 vel = this.velocity;
            ulong neighbourHandle = Helpers.UIntsToLong((uint)(neighbourx * 256), (uint)(neighboury * 256));
            RegionInfo neighbourRegion = this.m_world.RequestNeighbouringRegionInfo(neighbourHandle);
            if (neighbourRegion != null)
            {
                bool res = this.m_world.InformNeighbourOfCrossing(neighbourHandle, this.ControllingClient.AgentId, newpos);
                if (res)
                {
                    this.MakeChildAgent();
                    this.ControllingClient.CrossRegion(neighbourHandle, newpos, vel, System.Net.IPAddress.Parse(neighbourRegion.CommsIPListenAddr), (ushort)neighbourRegion.CommsIPListenPort);
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
        public override void updateMovement()
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
