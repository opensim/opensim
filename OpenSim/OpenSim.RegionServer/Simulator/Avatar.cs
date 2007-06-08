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
using OpenSim.RegionServer.Client;

using Axiom.MathLib;

namespace OpenSim.RegionServer.Simulator
{
    public partial class Avatar : Entity
    {
        public static bool PhysicsEngineFlying = false;
        public static AvatarAnimations Animations;
        public string firstname;
        public string lastname;
        public ClientView ControllingClient;
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
        private LLVector3 positionFrameBeforeLast = new LLVector3(0, 0, 0);

        private int positionRoundedX = 0;
        private int positionRoundedY = 0;

        private int positionParcelHoverLocalID = -1; //Local ID of the last parcel they were over
        private int parcelUpdateSequenceIncrement = 1;

        private bool childAvatar = false;

        public Avatar(ClientView TheClient, World world)
        {
            m_world = world;
            ControllingClient = TheClient;

            OpenSim.Framework.Console.MainConsole.Instance.Verbose("Avatar.cs - Loading details from grid (DUMMY)");
            localid = 8880000 + (this.m_world._localNumber++);
            Pos = ControllingClient.startpos;
            visualParams = new byte[218];
            for (int i = 0; i < 218; i++)
            {
                visualParams[i] = 100;
            }
            Wearables = new AvatarWearable[13]; //should be 13 of these
            for (int i = 0; i < 13; i++)
            {
                Wearables[i] = new AvatarWearable();
            }
            this.Wearables[0].AssetID = new LLUUID("66c41e39-38f9-f75a-024e-585989bfab73");
            this.Wearables[0].ItemID = LLUUID.Random();

            this.avatarAppearanceTexture = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));

            //register for events
            ControllingClient.OnRequestWearables += new ClientView.GenericCall(this.SendOurAppearance);
            ControllingClient.OnSetAppearance += new SetAppearance(this.SetAppearance);
            ControllingClient.OnCompleteMovementToRegion += new ClientView.GenericCall2(this.CompleteMovement);
            ControllingClient.OnCompleteMovementToRegion += new ClientView.GenericCall2(this.SendInitialPosition);
            ControllingClient.OnAgentUpdate += new ClientView.GenericCall3(this.HandleAgentUpdate);
            ControllingClient.OnStartAnim += new StartAnim(this.SendAnimPack);
            ControllingClient.OnChildAgentStatus += new ClientView.StatusChange(this.ChildStatusChange);

        }

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

        public void ChildStatusChange(bool status)
        {
            this.childAvatar = status;

            if (this.childAvatar == true)
            {
                this._physActor.Velocity = new PhysicsVector(0, 0, 0);
                ImprovedTerseObjectUpdatePacket.ObjectDataBlock terseBlock = CreateTerseBlock();
                ImprovedTerseObjectUpdatePacket terse = new ImprovedTerseObjectUpdatePacket();
                terse.RegionData.RegionHandle = m_world.m_regInfo.RegionHandle;
                terse.RegionData.TimeDilation = 64096;
                terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
                terse.ObjectData[0] = terseBlock;
                List<Avatar> avList = this.m_world.RequestAvatarList();
                foreach (Avatar client in avList)
                {
                    client.SendPacketToViewer(terse);
                }
            }
            else
            {
                LLVector3 startp = ControllingClient.StartPos;
                lock (m_world.LockPhysicsEngine)
                {
                    this._physActor.Position = new PhysicsVector(startp.X, startp.Y, startp.Z);
                }
            }
        }

        public override void addForces()
        {
            lock (this.forcesList)
            {
                if (this.forcesList.Count > 0)
                {
                    for (int i = 0; i < this.forcesList.Count; i++)
                    {
                        NewForce force = this.forcesList[i];
                        PhysicsVector phyVector = new PhysicsVector(force.X, force.Y, force.Z);
                        lock (m_world.LockPhysicsEngine)
                        {
                            this._physActor.Velocity = phyVector;
                        }
                        this.updateflag = true;
                        this.velocity = new LLVector3(force.X, force.Y, force.Z); //shouldn't really be doing this
                        // but as we are setting the velocity (rather than using real forces) at the moment it is okay.
                    }
                    for (int i = 0; i < this.forcesList.Count; i++)
                    {
                        this.forcesList.RemoveAt(0);
                    }
                }
            }
        }

        public static void SetupTemplate(string name)
        {
            FileInfo fInfo = new FileInfo(name);
            long numBytes = fInfo.Length;
            FileStream fStream = new FileStream(name, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fStream);
            byte[] data1 = br.ReadBytes((int)numBytes);
            br.Close();
            fStream.Close();
           
            libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock objdata = new ObjectUpdatePacket.ObjectDataBlock(); //  new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock(data1, ref i);

            SetDefaultPacketValues(objdata);
            objdata.TextureEntry = data1;
            objdata.UpdateFlags = 61 + (9 << 8) + (130 << 16) + (16 << 24);
            objdata.PathCurve = 16;
            objdata.ProfileCurve = 1;
            objdata.PathScaleX = 100;
            objdata.PathScaleY = 100;
            objdata.ParentID = 0;
            objdata.OwnerID = LLUUID.Zero;
            objdata.Scale = new LLVector3(1, 1, 1);
            objdata.PCode = 47;
            System.Text.Encoding enc = System.Text.Encoding.ASCII;
            libsecondlife.LLVector3 pos = new LLVector3(objdata.ObjectData, 16);
            pos.X = 100f;
            objdata.ID = 8880000;
            objdata.NameValue = enc.GetBytes("FirstName STRING RW SV Test \nLastName STRING RW SV User \0");
            libsecondlife.LLVector3 pos2 = new LLVector3(100f, 100f, 23f);
            //objdata.FullID=user.AgentID;
            byte[] pb = pos.GetBytes();
            Array.Copy(pb, 0, objdata.ObjectData, 16, pb.Length);

            Avatar.AvatarTemplate = objdata;
        }

        protected static void SetDefaultPacketValues(ObjectUpdatePacket.ObjectDataBlock objdata)
        {
            objdata.PSBlock = new byte[0];
            objdata.ExtraParams = new byte[1];
            objdata.MediaURL = new byte[0];
            objdata.NameValue = new byte[0];
            objdata.Text = new byte[0];
            objdata.TextColor = new byte[4];
            objdata.JointAxisOrAnchor = new LLVector3(0, 0, 0);
            objdata.JointPivot = new LLVector3(0, 0, 0);
            objdata.Material = 4;
            objdata.TextureAnim = new byte[0];
            objdata.Sound = LLUUID.Zero;
            LLObject.TextureEntry ntex = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));
            objdata.TextureEntry = ntex.ToBytes();
            objdata.State = 0;
            objdata.Data = new byte[0];

            objdata.ObjectData = new byte[76];
            objdata.ObjectData[15] = 128;
            objdata.ObjectData[16] = 63;
            objdata.ObjectData[56] = 128;
            objdata.ObjectData[61] = 102;
            objdata.ObjectData[62] = 40;
            objdata.ObjectData[63] = 61;
            objdata.ObjectData[64] = 189;


        }

        public void CompleteMovement()
        {
            OpenSim.Framework.Console.MainConsole.Instance.Verbose("Avatar.cs:CompleteMovement() - Constructing AgentMovementComplete packet");
            AgentMovementCompletePacket mov = new AgentMovementCompletePacket();
            mov.AgentData.SessionID = this.ControllingClient.SessionID;
            mov.AgentData.AgentID = this.ControllingClient.AgentID;
            mov.Data.RegionHandle = this.m_world.m_regInfo.RegionHandle;
            // TODO - dynamicalise this stuff
            mov.Data.Timestamp = 1172750370;
            mov.Data.Position = this.ControllingClient.startpos;
            mov.Data.LookAt = new LLVector3(0.99f, 0.042f, 0);

            ControllingClient.OutPacket(mov);
        }

        public void HandleAgentUpdate(Packet pack)
        {
            this.HandleUpdate((AgentUpdatePacket)pack);
        }

        public void HandleUpdate(AgentUpdatePacket pack)
        {
            if (((uint)pack.AgentData.ControlFlags & (uint)MainAvatar.ControlFlags.AGENT_CONTROL_FLY) != 0)
            {
                if (this._physActor.Flying == false)
                {
                    this.current_anim = Animations.AnimsLLUUID["FLY"];
                    this.anim_seq = 1;
                    this.SendAnimPack();
                }
                this._physActor.Flying = true;

            }
            else
            {
                if (this._physActor.Flying == true)
                {
                    this.current_anim = Animations.AnimsLLUUID["STAND"];
                    this.anim_seq = 1;
                    this.SendAnimPack();
                }
                this._physActor.Flying = false;
            }
            if (((uint)pack.AgentData.ControlFlags & (uint)MainAvatar.ControlFlags.AGENT_CONTROL_AT_POS) != 0)
            {
                Axiom.MathLib.Quaternion q = new Axiom.MathLib.Quaternion(pack.AgentData.BodyRotation.W, pack.AgentData.BodyRotation.X, pack.AgentData.BodyRotation.Y, pack.AgentData.BodyRotation.Z);
                if (((movementflag & 1) == 0) || (q != this.bodyRot))
                {

                    if (((movementflag & 1) == 0) && (!this._physActor.Flying))
                    {
                        this.current_anim = Animations.AnimsLLUUID["WALK"];
                        this.anim_seq = 1;
                        this.SendAnimPack();
                    }


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
            else if ((((uint)pack.AgentData.ControlFlags & (uint)MainAvatar.ControlFlags.AGENT_CONTROL_UP_POS) != 0) && (PhysicsEngineFlying))
            {
                if (((movementflag & 2) == 0) && this._physActor.Flying)
                {
                    //we should add a new force to the list
                    // but for now we will deal with velocities
                    NewForce newVelocity = new NewForce();
                    Axiom.MathLib.Vector3 v3 = new Axiom.MathLib.Vector3(0, 0, 1);
                    Axiom.MathLib.Vector3 direc = v3;
                    direc.Normalize();

                    //work out velocity for sim physics system
                    direc = direc * ((0.03f) * 128f * 2);
                    newVelocity.X = direc.x;
                    newVelocity.Y = direc.y;
                    newVelocity.Z = direc.z;
                    this.forcesList.Add(newVelocity);
                    movementflag = 2;
                }
            }
            else if ((((uint)pack.AgentData.ControlFlags & (uint)MainAvatar.ControlFlags.AGENT_CONTROL_UP_NEG) != 0) && (PhysicsEngineFlying))
            {
                if (((movementflag & 4) == 0) && this._physActor.Flying)
                {
                    //we should add a new force to the list
                    // but for now we will deal with velocities
                    NewForce newVelocity = new NewForce();
                    Axiom.MathLib.Vector3 v3 = new Axiom.MathLib.Vector3(0, 0, -1);
                    //Axiom.MathLib.Quaternion q = new Axiom.MathLib.Quaternion(pack.AgentData.BodyRotation.W, pack.AgentData.BodyRotation.X, pack.AgentData.BodyRotation.Y, pack.AgentData.BodyRotation.Z);
                    Axiom.MathLib.Vector3 direc = v3;
                    direc.Normalize();

                    //work out velocity for sim physics system
                    direc = direc * ((0.03f) * 128f * 2);
                    newVelocity.X = direc.x;
                    newVelocity.Y = direc.y;
                    newVelocity.Z = direc.z;
                    this.forcesList.Add(newVelocity);
                    movementflag = 4;
                }
            }
            else if (((uint)pack.AgentData.ControlFlags & (uint)MainAvatar.ControlFlags.AGENT_CONTROL_AT_NEG) != 0)
            {
                Axiom.MathLib.Quaternion q = new Axiom.MathLib.Quaternion(pack.AgentData.BodyRotation.W, pack.AgentData.BodyRotation.X, pack.AgentData.BodyRotation.Y, pack.AgentData.BodyRotation.Z);
                if (((movementflag & 8) == 0) || (q != this.bodyRot))
                {
                    //we should add a new force to the list
                    // but for now we will deal with velocities
                    NewForce newVelocity = new NewForce();
                    Axiom.MathLib.Vector3 v3 = new Axiom.MathLib.Vector3(-1, 0, 0);
                    Axiom.MathLib.Vector3 direc = q * v3;
                    direc.Normalize();

                    //work out velocity for sim physics system
                    direc = direc * ((0.03f) * 128f);
                    if (this._physActor.Flying)
                        direc *= 2;

                    newVelocity.X = direc.x;
                    newVelocity.Y = direc.y;
                    newVelocity.Z = direc.z;
                    this.forcesList.Add(newVelocity);
                    movementflag = 8;
                    this.bodyRot = q;
                }
            }
            else
            {
                if (movementflag == 16)
                {
                    movementflag = 0;
                }
                if ((movementflag) != 0)
                {
                    NewForce newVelocity = new NewForce();
                    newVelocity.X = 0;
                    newVelocity.Y = 0;
                    newVelocity.Z = 0;
                    this.forcesList.Add(newVelocity);
                    movementflag = 0;
                    // We're standing still, so make it show!
                    if (this._physActor.Flying == false)
                    {
                        this.current_anim = Animations.AnimsLLUUID["STAND"];
                        this.anim_seq = 1;
                        this.SendAnimPack();
                    }
                    this.movementflag = 16;

                }
            }
        }

        

        public static void LoadAnims()
        {
            Avatar.Animations = new AvatarAnimations();
            Avatar.Animations.LoadAnims();
        }

        public override void LandRenegerated()
        {
            Pos = new LLVector3(100.0f, 100.0f, m_world.Terrain[(int)Pos.X, (int)Pos.Y] + 50.0f);
        }
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
