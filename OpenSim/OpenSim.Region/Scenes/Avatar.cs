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

namespace OpenSim.Region.Scenes
{
    public partial class Avatar : Entity
    {
        public static bool PhysicsEngineFlying = false;
        public static AvatarAnimations Animations;
        public string firstname;
        public string lastname;
        public IClientAPI ControllingClient;
        public LLUUID current_anim;
        public int anim_seq;
        private static libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock AvatarTemplate;
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
        private bool childAvatar = false;
        private bool newForce = false;

        protected RegionInfo m_regionInfo;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="theClient"></param>
        /// <param name="world"></param>
        /// <param name="clientThreads"></param>
        /// <param name="regionDat"></param>
        public Avatar(IClientAPI theClient, Scene world, RegionInfo reginfo)
        {

            m_world = world;
            this.uuid = theClient.AgentId;

            m_regionInfo = reginfo;
            m_regionHandle = reginfo.RegionHandle;
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "Avatar.cs - Loading details from grid (DUMMY)");
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
            ControllingClient.OnRequestWearables += new GenericCall(this.SendOurAppearance);
            //ControllingClient.OnSetAppearance += new SetAppearance(this.SetAppearance);
            ControllingClient.OnCompleteMovementToRegion += new GenericCall2(this.CompleteMovement);
            ControllingClient.OnCompleteMovementToRegion += new GenericCall2(this.SendInitialPosition);
            ControllingClient.OnAgentUpdate += new UpdateAgent(this.HandleAgentUpdate);
           // ControllingClient.OnStartAnim += new StartAnim(this.SendAnimPack);
           // ControllingClient.OnChildAgentStatus += new StatusChange(this.ChildStatusChange);
            //ControllingClient.OnStopMovement += new GenericCall2(this.StopMovement);
            
        }

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="status"></param>
        public void ChildStatusChange(bool status)
        {
            this.childAvatar = status;

            if (this.childAvatar == true)
            {
                this.Velocity = new LLVector3(0, 0, 0);
                this.Pos = new LLVector3(128, 128, 70);
                
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pos"></param>
        public void UpGradeAvatar(LLVector3 pos)
        {
            //this.childAvatar = false;
            this.Pos = pos;
        }

        protected void DownGradeAvatar()
        {
            this.Velocity = new LLVector3(0, 0, 0);
            this.Pos = new LLVector3(128, 128, 70);
            this.childAvatar = true;
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
        public override void addForces()
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
                        this.Velocity = new LLVector3(force.X, force.Y, force.Z); //shouldn't really be doing this
                        this.newForce = true;
                    }
                    for (int i = 0; i < this.forcesList.Count; i++)
                    {
                        this.forcesList.RemoveAt(0);
                    }
                }
            }
        }

        public void SendTerseUpdateToClient(IClientAPI RemoteClient)
        {
            LLVector3 pos = this.Pos;
            LLVector3 vel = this.Velocity;
            RemoteClient.SendAvatarTerseUpdate(this.m_regionHandle,  64096, this.LocalId, new LLVector3(pos.X, pos.Y, pos.Z), new LLVector3(vel.X, vel.Y, vel.Z)); 
        }

        /// <summary>
        /// 
        /// </summary>
        public void SendTerseUpdateToALLClients()
        {
            List<Avatar> avatars = this.m_world.RequestAvatarList();
            for (int i = 0; i < avatars.Count; i++)
            {
                this.SendTerseUpdateToClient(avatars[i].ControllingClient);
            }
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
            if (this.childAvatar)
            {
                this.childAvatar = false;
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
                    //we should add a new force to the list
                    // but for now we will deal with velocities
                    NewForce newVelocity = new NewForce();
                    Axiom.MathLib.Vector3 v3 = new Axiom.MathLib.Vector3(1, 0, 0);
                    Axiom.MathLib.Vector3 direc = q * v3;
                    direc.Normalize();

                    //work out velocity for sim physics system
                    direc = direc * ((0.03f) * 128f);
                    if (this._physActor.Flying)
                        direc *= 4;

                    newVelocity.X = direc.x;
                    newVelocity.Y = direc.y;
                    newVelocity.Z = direc.z;
                    this.forcesList.Add(newVelocity);
                    movementflag = 1;
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

        /// <summary>
        /// 
        /// </summary>
        public static void LoadAnims()
        {

        }
        
        /// <summary>
        /// 
        /// </summary>
        public override void LandRenegerated()
        {

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
