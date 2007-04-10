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
                foreach (SimClient client in m_clientThreads.Values)
                {
                    client.OutPacket(terse);
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
                        foreach (SimClient client in m_clientThreads.Values)
                        {
                            client.OutPacket(terse);
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

            foreach (SimClient client in m_clientThreads.Values)
            {
                client.OutPacket(objupdate);
                if (client.AgentID != ControllingClient.AgentID)
                {
                    //the below line is already in Simclient.cs at line number 245 , directly below the call to this method
                    //if there is a problem/bug with that , then lets fix it there rather than duplicating it here
                    //client.ClientAvatar.SendAppearanceToOtherAgent(this.ControllingClient);

                    SendAppearanceToOtherAgent(client);
                }
            }
        }

        public void SendInitialAppearance()
        {
            AgentWearablesUpdatePacket aw = new AgentWearablesUpdatePacket();
            aw.AgentData.AgentID = this.ControllingClient.AgentID;
            aw.AgentData.SerialNum = 0;
            aw.AgentData.SessionID = ControllingClient.SessionID;

            aw.WearableData = new AgentWearablesUpdatePacket.WearableDataBlock[13];
            AgentWearablesUpdatePacket.WearableDataBlock awb;
            for (int i = 0; i < 13; i++)
            {
                awb = new AgentWearablesUpdatePacket.WearableDataBlock();
                awb.WearableType = (byte)i;
                awb.AssetID = this.Wearables[i].AssetID;
                awb.ItemID = this.Wearables[i].ItemID;
                aw.WearableData[i] = awb;
            }

            ControllingClient.OutPacket(aw);
        }

        public void SendAppearanceToOtherAgent(SimClient userInfo)
        {
            AvatarAppearancePacket avp = new AvatarAppearancePacket();
            avp.VisualParam = new AvatarAppearancePacket.VisualParamBlock[218];
            avp.ObjectData.TextureEntry = this.avatarAppearanceTexture.ToBytes();

            //a wearable update packets should only be sent about the viewers/agents own avatar not for other avatars
            //but it seems that the following code only created the packets and never actually sent them anyway
            /*AgentWearablesUpdatePacket aw = new AgentWearablesUpdatePacket();
            aw.AgentData.AgentID = this.ControllingClient.AgentID;
            aw.AgentData.SessionID = userInfo.SessionID;
            aw.AgentData.SerialNum = 0; //removed the use of a random number as a random number could be less than the last number, should have a counter variable for this

            aw.WearableData = new AgentWearablesUpdatePacket.WearableDataBlock[13];
            AgentWearablesUpdatePacket.WearableDataBlock awb;
            for (int i = 0; i < 13; i++)
            {
                awb = new AgentWearablesUpdatePacket.WearableDataBlock();
                awb.WearableType = (byte)i;
                awb.AssetID = this.Wearables[i].AssetID;
                awb.ItemID = this.Wearables[i].ItemID;
                aw.WearableData[i] = awb;
            }*/

            AvatarAppearancePacket.VisualParamBlock avblock = null;
            for (int i = 0; i < 218; i++)
            {
                avblock = new AvatarAppearancePacket.VisualParamBlock();
                avblock.ParamValue = visualParams[i];
                avp.VisualParam[i] = avblock;
            }

            avp.Sender.IsTrial = false;
            avp.Sender.ID = ControllingClient.AgentID;
            userInfo.OutPacket(avp);
        }

        public void SetAppearance(AgentSetAppearancePacket appear)
        {
            LLObject.TextureEntry tex = new LLObject.TextureEntry(appear.ObjectData.TextureEntry, 0, appear.ObjectData.TextureEntry.Length);
            this.avatarAppearanceTexture = tex;
            for (int i = 0; i < appear.VisualParam.Length; i++)
            {
                this.visualParams[i] = appear.VisualParam[i].ParamValue;
            }
            foreach (SimClient client in m_clientThreads.Values)
            {
                if (client.AgentID != ControllingClient.AgentID)
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
        public void SendAnimPack()
        {
            AvatarAnimationPacket ani = new AvatarAnimationPacket();
            ani.AnimationSourceList = new AvatarAnimationPacket.AnimationSourceListBlock[1];
            ani.AnimationSourceList[0] = new AvatarAnimationPacket.AnimationSourceListBlock();
            ani.AnimationSourceList[0].ObjectID = ControllingClient.AgentID;
            ani.Sender = new AvatarAnimationPacket.SenderBlock();
            ani.Sender.ID = ControllingClient.AgentID;
            ani.AnimationList = new AvatarAnimationPacket.AnimationListBlock[1];
            ani.AnimationList[0] = new AvatarAnimationPacket.AnimationListBlock();
            ani.AnimationList[0].AnimID = this.current_anim;
            ani.AnimationList[0].AnimSequenceID = this.anim_seq;

            //ControllingClient.OutPacket(ani);
            foreach (SimClient client in m_clientThreads.Values)
            {
                client.OutPacket(ani);
            }
        }

    }
}
