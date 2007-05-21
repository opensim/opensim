using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.world
{
    partial class Avatar
    {
        public override void update()
        {
            if (this._physActor == null)
            {
                //HACKHACK: Note to work out why this entity does not have a physics actor
                //          and prehaps create one.
                return;
            }
            libsecondlife.LLVector3 pos2 = new LLVector3(this._physActor.Position.X, this._physActor.Position.Y, this._physActor.Position.Z);
            if (this.updateflag)
            {
                //need to send movement info
                //so create the improvedterseobjectupdate packet
                //use CreateTerseBlock()
                ImprovedTerseObjectUpdatePacket.ObjectDataBlock terseBlock = CreateTerseBlock();
                ImprovedTerseObjectUpdatePacket terse = new ImprovedTerseObjectUpdatePacket();
                terse.RegionData.RegionHandle = m_regionHandle; // FIXME
                terse.RegionData.TimeDilation = 64096;
                terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
                terse.ObjectData[0] = terseBlock;
                List<Avatar> avList = this.m_world.RequestAvatarList();
                foreach (Avatar client in avList)
                {
                    client.SendPacketToViewer(terse);
                }

                updateflag = false;
                //this._updateCount = 0;
            }
            else
            {

                if ((pos2 != this.positionLastFrame) || (this.movementflag == 16))
                {
                    _updateCount++;
                    if (((!PhysicsEngineFlying) && (_updateCount > 3)) || (PhysicsEngineFlying) && (_updateCount > 0))
                    {
                        //It has been a while since last update was sent so lets send one.
                        ImprovedTerseObjectUpdatePacket.ObjectDataBlock terseBlock = CreateTerseBlock();
                        ImprovedTerseObjectUpdatePacket terse = new ImprovedTerseObjectUpdatePacket();
                        terse.RegionData.RegionHandle = m_regionHandle; // FIXME
                        terse.RegionData.TimeDilation = 64096;
                        terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
                        terse.ObjectData[0] = terseBlock;
                        List<Avatar> avList = this.m_world.RequestAvatarList();
                        foreach (Avatar client in avList)
                        {
                            client.SendPacketToViewer(terse);
                        }
                        _updateCount = 0;
                    }

                    if (this.movementflag == 16)
                    {
                        movementflag = 0;
                    }
                }

            }
            this.positionLastFrame = pos2;

            if (!this.ControllingClient.m_sandboxMode)
            {
                if (pos2.X < 0)
                {
                    ControllingClient.CrossSimBorder(new LLVector3(this._physActor.Position.X, this._physActor.Position.Y, this._physActor.Position.Z));
                }

                if (pos2.Y < 0)
                {
                    ControllingClient.CrossSimBorder(new LLVector3(this._physActor.Position.X, this._physActor.Position.Y, this._physActor.Position.Z));
                }

                if (pos2.X > 255)
                {
                    ControllingClient.CrossSimBorder(new LLVector3(this._physActor.Position.X, this._physActor.Position.Y, this._physActor.Position.Z));
                }

                if (pos2.Y > 255)
                {
                    ControllingClient.CrossSimBorder(new LLVector3(this._physActor.Position.X, this._physActor.Position.Y, this._physActor.Position.Z));
                }
            }

        }

        public ObjectUpdatePacket CreateUpdatePacket()
        {
            System.Text.Encoding _enc = System.Text.Encoding.ASCII;
            //send a objectupdate packet with information about the clients avatar
            ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
            objupdate.RegionData.RegionHandle = m_regionHandle;
            objupdate.RegionData.TimeDilation = 64096;
            objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];

            objupdate.ObjectData[0] = AvatarTemplate;
            //give this avatar object a local id and assign the user a name
            objupdate.ObjectData[0].ID = this.localid;
            objupdate.ObjectData[0].FullID = ControllingClient.AgentID;
            objupdate.ObjectData[0].NameValue = _enc.GetBytes("FirstName STRING RW SV " + firstname + "\nLastName STRING RW SV " + lastname + " \0");

            libsecondlife.LLVector3 pos2 = new LLVector3((float)this._physActor.Position.X, (float)this._physActor.Position.Y, (float)this._physActor.Position.Z);

            byte[] pb = pos2.GetBytes();

            Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 16, pb.Length);
            return objupdate;
        }

        public void SendInitialPosition()
        {
            System.Text.Encoding _enc = System.Text.Encoding.ASCII;
            //send a objectupdate packet with information about the clients avatar

            ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
            objupdate.RegionData.RegionHandle = m_regionHandle;
            objupdate.RegionData.TimeDilation = 64096;
            objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
            objupdate.ObjectData[0] = AvatarTemplate;
            //give this avatar object a local id and assign the user a name

            objupdate.ObjectData[0].ID = this.localid;
            this.uuid = objupdate.ObjectData[0].FullID = ControllingClient.AgentID;
            objupdate.ObjectData[0].NameValue = _enc.GetBytes("FirstName STRING RW SV " + firstname + "\nLastName STRING RW SV " + lastname + " \0");
            libsecondlife.LLVector3 pos2 = new LLVector3((float)this.Pos.X, (float)this.Pos.Y, (float)this.Pos.Z);
            byte[] pb = pos2.GetBytes();
            Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 16, pb.Length);
            m_world._localNumber++;

            List<Avatar> avList = this.m_world.RequestAvatarList();
            foreach (Avatar client in avList)
            {
                client.SendPacketToViewer(objupdate);
                if (client.ControllingClient.AgentID != this.ControllingClient.AgentID)
                {
                    SendAppearanceToOtherAgent(client);
                }
            }
        }

        public void SendOurAppearance()
        {
            ControllingClient.SendAppearance(this.Wearables);
        }

        public void SendOurAppearance(ClientView OurClient)
        {
            //event handler for wearables request
            this.SendOurAppearance();
        }

        public void SendAppearanceToOtherAgent(Avatar avatarInfo)
        {
            AvatarAppearancePacket avp = new AvatarAppearancePacket();
            avp.VisualParam = new AvatarAppearancePacket.VisualParamBlock[218];
            avp.ObjectData.TextureEntry = this.avatarAppearanceTexture.ToBytes();

            AvatarAppearancePacket.VisualParamBlock avblock = null;
            for (int i = 0; i < 218; i++)
            {
                avblock = new AvatarAppearancePacket.VisualParamBlock();
                avblock.ParamValue = visualParams[i];
                avp.VisualParam[i] = avblock;
            }

            avp.Sender.IsTrial = false;
            avp.Sender.ID = ControllingClient.AgentID;
            avatarInfo.SendPacketToViewer(avp);
        }

        public void SetAppearance(byte[] texture, AgentSetAppearancePacket.VisualParamBlock[] visualParam)
        {
            LLObject.TextureEntry tex = new LLObject.TextureEntry(texture, 0, texture.Length);
            this.avatarAppearanceTexture = tex;

            for (int i = 0; i < visualParam.Length; i++)
            {
                this.visualParams[i] = visualParam[i].ParamValue;
            }

            List<Avatar> avList = this.m_world.RequestAvatarList();
            foreach (Avatar client in avList)
            {
                if (client.ControllingClient.AgentID != this.ControllingClient.AgentID)
                {
                    SendAppearanceToOtherAgent(client);
                }
            }
        }

        public ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreateTerseBlock()
        {
            byte[] bytes = new byte[60];
            int i = 0;
            ImprovedTerseObjectUpdatePacket.ObjectDataBlock dat = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();

            dat.TextureEntry = new byte[0];// AvatarTemplate.TextureEntry;
            libsecondlife.LLVector3 pos2 = new LLVector3(0, 0, 0);
            lock (m_world.LockPhysicsEngine)
            {
                pos2 = new LLVector3(this._physActor.Position.X, this._physActor.Position.Y, this._physActor.Position.Z);
            }

            uint ID = this.localid;

            bytes[i++] = (byte)(ID % 256);
            bytes[i++] = (byte)((ID >> 8) % 256);
            bytes[i++] = (byte)((ID >> 16) % 256);
            bytes[i++] = (byte)((ID >> 24) % 256);
            bytes[i++] = 0;
            bytes[i++] = 1;
            i += 14;
            bytes[i++] = 128;
            bytes[i++] = 63;

            byte[] pb = pos2.GetBytes();
            Array.Copy(pb, 0, bytes, i, pb.Length);
            i += 12;
            ushort InternVelocityX;
            ushort InternVelocityY;
            ushort InternVelocityZ;
            Axiom.MathLib.Vector3 internDirec = new Axiom.MathLib.Vector3(0, 0, 0);
            lock (m_world.LockPhysicsEngine)
            {
                internDirec = new Axiom.MathLib.Vector3(this._physActor.Velocity.X, this._physActor.Velocity.Y, this._physActor.Velocity.Z);
            }
            internDirec = internDirec / 128.0f;
            internDirec.x += 1;
            internDirec.y += 1;
            internDirec.z += 1;

            InternVelocityX = (ushort)(32768 * internDirec.x);
            InternVelocityY = (ushort)(32768 * internDirec.y);
            InternVelocityZ = (ushort)(32768 * internDirec.z);

            ushort ac = 32767;
            bytes[i++] = (byte)(InternVelocityX % 256);
            bytes[i++] = (byte)((InternVelocityX >> 8) % 256);
            bytes[i++] = (byte)(InternVelocityY % 256);
            bytes[i++] = (byte)((InternVelocityY >> 8) % 256);
            bytes[i++] = (byte)(InternVelocityZ % 256);
            bytes[i++] = (byte)((InternVelocityZ >> 8) % 256);

            //accel
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            //rot
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            //rotation vel
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            dat.Data = bytes;
            return (dat);
        }

        // Sends animation update
        public void SendAnimPack(LLUUID animID, int seq)
        {
            AvatarAnimationPacket ani = new AvatarAnimationPacket();
            ani.AnimationSourceList = new AvatarAnimationPacket.AnimationSourceListBlock[1];
            ani.AnimationSourceList[0] = new AvatarAnimationPacket.AnimationSourceListBlock();
            ani.AnimationSourceList[0].ObjectID = ControllingClient.AgentID;
            ani.Sender = new AvatarAnimationPacket.SenderBlock();
            ani.Sender.ID = ControllingClient.AgentID;
            ani.AnimationList = new AvatarAnimationPacket.AnimationListBlock[1];
            ani.AnimationList[0] = new AvatarAnimationPacket.AnimationListBlock();
            ani.AnimationList[0].AnimID = this.current_anim = animID;
            ani.AnimationList[0].AnimSequenceID = this.anim_seq = seq;

            List<Avatar> avList = this.m_world.RequestAvatarList();
            foreach (Avatar client in avList)
            {
                client.SendPacketToViewer(ani);
            }
          
        }

        public void SendAnimPack()
        {
            this.SendAnimPack(this.current_anim, this.anim_seq);
        }

    }
}
