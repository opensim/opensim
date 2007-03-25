using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.types;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Assets;

namespace OpenSim.world
{
    public class Primitive : Entity
    {
        protected float mesh_cutbegin;
        protected float mesh_cutend;
        protected PrimData primData;
        protected bool newPrimFlag = false;
        protected bool updateFlag = false;
        protected bool dirtyFlag = false;
        private ObjectUpdatePacket OurPacket;
        private PhysicsActor _physActor;
        private bool physicsEnabled = false;
        private bool physicstest = false; //just added for testing 

        public bool PhysicsEnabled
        {
            get
            {
                return physicsEnabled;
            }
            set
            {
                physicsEnabled = value;
            }
        }
        public bool UpdateFlag
        {
            get
            {
                return updateFlag;
            }
            set
            {
                updateFlag = value;
            }
        }
        public LLVector3 Scale
        {
            set
            {
                this.primData.Scale = value;
                this.dirtyFlag = true;
            }
            get
            {
                return this.primData.Scale;
            }
        }
        public PhysicsActor PhysActor
        {
            set
            {
                this._physActor = value;
            }
        }

        public Primitive()
        {
            mesh_cutbegin = 0.0f;
            mesh_cutend = 1.0f;
        }

        public override Mesh getMesh()
        {
            Mesh mesh = new Mesh();
            Triangle tri = new Triangle(
                new Axiom.MathLib.Vector3(0.0f, 1.0f, 1.0f),
                new Axiom.MathLib.Vector3(1.0f, 0.0f, 1.0f),
                new Axiom.MathLib.Vector3(1.0f, 1.0f, 0.0f));

            mesh.AddTri(tri);
            mesh += base.getMesh();

            return mesh;
        }

        public void UpdatePosition(LLVector3 pos)
        {
            this.position = pos;
            if (this._physActor != null) // && this.physicsEnabled)
            {
                this._physActor.Position = new PhysicsVector(pos.X, pos.Y, pos.Z);
            }
            this.updateFlag = true;
        }

        public override void update()
        {
            if (this.newPrimFlag)
            {
                foreach (SimClient client in OpenSimRoot.Instance.ClientThreads.Values)
                {
                    client.OutPacket(OurPacket);
                }
                this.newPrimFlag = false;
            }
            else if (this.updateFlag)
            {
                ImprovedTerseObjectUpdatePacket terse = new ImprovedTerseObjectUpdatePacket();
                terse.RegionData.RegionHandle = OpenSimRoot.Instance.Cfg.RegionHandle; // FIXME
                terse.RegionData.TimeDilation = 64096;
                terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
                terse.ObjectData[0] = this.CreateImprovedBlock();
                foreach (SimClient client in OpenSimRoot.Instance.ClientThreads.Values)
                {
                    client.OutPacket(terse);
                }
                this.updateFlag = false;
            }
            else if (this.dirtyFlag)
            {
                foreach (SimClient client in OpenSimRoot.Instance.ClientThreads.Values)
                {
                    UpdateClient(client);
                }
                this.dirtyFlag = false;
            }
            else
            {
                if (this._physActor != null && this.physicsEnabled)
                {
                    ImprovedTerseObjectUpdatePacket terse = new ImprovedTerseObjectUpdatePacket();
                    terse.RegionData.RegionHandle = OpenSimRoot.Instance.Cfg.RegionHandle; // FIXME
                    terse.RegionData.TimeDilation = 64096;
                    terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
                    terse.ObjectData[0] = this.CreateImprovedBlock();
                    foreach (SimClient client in OpenSimRoot.Instance.ClientThreads.Values)
                    {
                        client.OutPacket(terse);
                    }
                }
            }

            if (this.physicstest)
            {
                LLVector3 pos = this.position;
                pos.Z += 0.0001f;
                this.UpdatePosition(pos);
                this.physicstest = false;
            }
        }

        public void UpdateClient(SimClient RemoteClient)
        {

            LLVector3 lPos;
            if (this._physActor != null && this.physicsEnabled)
            {
                PhysicsVector pPos = this._physActor.Position;
                lPos = new LLVector3(pPos.X, pPos.Y, pPos.Z);
            }
            else
            {
                lPos = this.position;
            }
            byte[] pb = lPos.GetBytes();
            Array.Copy(pb, 0, OurPacket.ObjectData[0].ObjectData, 0, pb.Length);

            // OurPacket should be update with the follwing in updateShape() rather than having to do it here
            OurPacket.ObjectData[0].OwnerID = this.primData.OwnerID;
            OurPacket.ObjectData[0].PCode = this.primData.PCode;
            OurPacket.ObjectData[0].PathBegin = this.primData.PathBegin;
            OurPacket.ObjectData[0].PathEnd = this.primData.PathEnd;
            OurPacket.ObjectData[0].PathScaleX = this.primData.PathScaleX;
            OurPacket.ObjectData[0].PathScaleY = this.primData.PathScaleY;
            OurPacket.ObjectData[0].PathShearX = this.primData.PathShearX;
            OurPacket.ObjectData[0].PathShearY = this.primData.PathShearY;
            OurPacket.ObjectData[0].PathSkew = this.primData.PathSkew;
            OurPacket.ObjectData[0].ProfileBegin = this.primData.ProfileBegin;
            OurPacket.ObjectData[0].ProfileEnd = this.primData.ProfileEnd;
            OurPacket.ObjectData[0].Scale = this.primData.Scale;
            OurPacket.ObjectData[0].PathCurve = this.primData.PathCurve;
            OurPacket.ObjectData[0].ProfileCurve = this.primData.ProfileCurve;
            OurPacket.ObjectData[0].ParentID = 0;
            OurPacket.ObjectData[0].ProfileHollow = this.primData.ProfileHollow;
            //finish off copying rest of shape data
            OurPacket.ObjectData[0].PathRadiusOffset = this.primData.PathRadiusOffset;
            OurPacket.ObjectData[0].PathRevolutions = this.primData.PathRevolutions;
            OurPacket.ObjectData[0].PathTaperX = this.primData.PathTaperX;
            OurPacket.ObjectData[0].PathTaperY = this.primData.PathTaperY;
            OurPacket.ObjectData[0].PathTwist = this.primData.PathTwist;
            OurPacket.ObjectData[0].PathTwistBegin = this.primData.PathTwistBegin;

            RemoteClient.OutPacket(OurPacket);
        }

        public void UpdateShape(ObjectShapePacket.ObjectDataBlock addPacket)
        {
            this.primData.PathBegin = addPacket.PathBegin;
            this.primData.PathEnd = addPacket.PathEnd;
            this.primData.PathScaleX = addPacket.PathScaleX;
            this.primData.PathScaleY = addPacket.PathScaleY;
            this.primData.PathShearX = addPacket.PathShearX;
            this.primData.PathShearY = addPacket.PathShearY;
            this.primData.PathSkew = addPacket.PathSkew;
            this.primData.ProfileBegin = addPacket.ProfileBegin;
            this.primData.ProfileEnd = addPacket.ProfileEnd;
            this.primData.PathCurve = addPacket.PathCurve;
            this.primData.ProfileCurve = addPacket.ProfileCurve;
            this.primData.ProfileHollow = addPacket.ProfileHollow;
            this.primData.PathRadiusOffset = addPacket.PathRadiusOffset;
            this.primData.PathRevolutions = addPacket.PathRevolutions;
            this.primData.PathTaperX = addPacket.PathTaperX;
            this.primData.PathTaperY = addPacket.PathTaperY;
            this.primData.PathTwist = addPacket.PathTwist;
            this.primData.PathTwistBegin = addPacket.PathTwistBegin;
            this.dirtyFlag = true;
        }

        public void UpdateTexture(byte[] tex)
        {
            this.OurPacket.ObjectData[0].TextureEntry = tex;
            this.primData.Texture = tex;
            this.dirtyFlag = true;
        }

        public void UpdateObjectFlags(ObjectFlagUpdatePacket pack)
        {
            if (this._physActor != null)
            {
                if (this._physActor.Kinematic == pack.AgentData.UsePhysics)
                {
                    this._physActor.Kinematic = !pack.AgentData.UsePhysics; //if Usephysics = true, then Kinematic should = false
                }
                this.physicsEnabled = pack.AgentData.UsePhysics;
                if (this._physActor.Kinematic == false)
                {
                    LLVector3 pos = this.position;
                    this.UpdatePosition(pos);
                    pos.Z += 0.000001f;
                    this.UpdatePosition(pos);
                    this.physicstest = true;
                }
                else
                {
                    PhysicsVector vec = this._physActor.Position;
                    LLVector3 pos = new LLVector3(vec.X, vec.Y, vec.Z);
                    this.position = pos;
                    this.updateFlag = true;
                }
            }
        }

        public void CreateFromPacket(ObjectAddPacket addPacket, LLUUID agentID, uint localID)
        {
            ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
            objupdate.RegionData.RegionHandle = OpenSimRoot.Instance.Cfg.RegionHandle;
            objupdate.RegionData.TimeDilation = 64096;

            objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
            PrimData PData = new PrimData();
            this.primData = PData;
            objupdate.ObjectData[0] = new ObjectUpdatePacket.ObjectDataBlock();
            objupdate.ObjectData[0].PSBlock = new byte[0];
            objupdate.ObjectData[0].ExtraParams = new byte[1];
            objupdate.ObjectData[0].MediaURL = new byte[0];
            objupdate.ObjectData[0].NameValue = new byte[0];
            objupdate.ObjectData[0].Text = new byte[0];
            objupdate.ObjectData[0].TextColor = new byte[4];
            objupdate.ObjectData[0].JointAxisOrAnchor = new LLVector3(0, 0, 0);
            objupdate.ObjectData[0].JointPivot = new LLVector3(0, 0, 0);
            objupdate.ObjectData[0].Material = 3;
            objupdate.ObjectData[0].UpdateFlags = 32 + 65536 + 131072 + 256 + 4 + 8 + 2048 + 524288 + 268435456;
            objupdate.ObjectData[0].TextureAnim = new byte[0];
            objupdate.ObjectData[0].Sound = LLUUID.Zero;
            LLObject.TextureEntry ntex = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));
            objupdate.ObjectData[0].TextureEntry = ntex.ToBytes();
            objupdate.ObjectData[0].State = 0;
            objupdate.ObjectData[0].Data = new byte[0];
            PData.OwnerID = objupdate.ObjectData[0].OwnerID = agentID;
            PData.PCode = objupdate.ObjectData[0].PCode = addPacket.ObjectData.PCode;
            PData.PathBegin = objupdate.ObjectData[0].PathBegin = addPacket.ObjectData.PathBegin;
            PData.PathEnd = objupdate.ObjectData[0].PathEnd = addPacket.ObjectData.PathEnd;
            PData.PathScaleX = objupdate.ObjectData[0].PathScaleX = addPacket.ObjectData.PathScaleX;
            PData.PathScaleY = objupdate.ObjectData[0].PathScaleY = addPacket.ObjectData.PathScaleY;
            PData.PathShearX = objupdate.ObjectData[0].PathShearX = addPacket.ObjectData.PathShearX;
            PData.PathShearY = objupdate.ObjectData[0].PathShearY = addPacket.ObjectData.PathShearY;
            PData.PathSkew = objupdate.ObjectData[0].PathSkew = addPacket.ObjectData.PathSkew;
            PData.ProfileBegin = objupdate.ObjectData[0].ProfileBegin = addPacket.ObjectData.ProfileBegin;
            PData.ProfileEnd = objupdate.ObjectData[0].ProfileEnd = addPacket.ObjectData.ProfileEnd;
            PData.Scale = objupdate.ObjectData[0].Scale = addPacket.ObjectData.Scale;
            PData.PathCurve = objupdate.ObjectData[0].PathCurve = addPacket.ObjectData.PathCurve;
            PData.ProfileCurve = objupdate.ObjectData[0].ProfileCurve = addPacket.ObjectData.ProfileCurve;
            PData.ParentID = objupdate.ObjectData[0].ParentID = 0;
            PData.ProfileHollow = objupdate.ObjectData[0].ProfileHollow = addPacket.ObjectData.ProfileHollow;

            PData.PathRadiusOffset = objupdate.ObjectData[0].PathRadiusOffset = addPacket.ObjectData.PathRadiusOffset;
            PData.PathRevolutions = objupdate.ObjectData[0].PathRevolutions = addPacket.ObjectData.PathRevolutions;
            PData.PathTaperX = objupdate.ObjectData[0].PathTaperX = addPacket.ObjectData.PathTaperX;
            PData.PathTaperY = objupdate.ObjectData[0].PathTaperY = addPacket.ObjectData.PathTaperY;
            PData.PathTwist = objupdate.ObjectData[0].PathTwist = addPacket.ObjectData.PathTwist;
            PData.PathTwistBegin = objupdate.ObjectData[0].PathTwistBegin = addPacket.ObjectData.PathTwistBegin;

            objupdate.ObjectData[0].ID = (uint)(localID);
            objupdate.ObjectData[0].FullID = new LLUUID("edba7151-5857-acc5-b30b-f01efef" + (localID - 702000).ToString("00000"));
            objupdate.ObjectData[0].ObjectData = new byte[60];
            objupdate.ObjectData[0].ObjectData[46] = 128;
            objupdate.ObjectData[0].ObjectData[47] = 63;
            LLVector3 pos1 = addPacket.ObjectData.RayEnd;
            //update position
            byte[] pb = pos1.GetBytes();
            Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 0, pb.Length);

            this.newPrimFlag = true;
            this.uuid = objupdate.ObjectData[0].FullID;
            this.localid = objupdate.ObjectData[0].ID;
            this.position = pos1;
            this.OurPacket = objupdate;
        }

        public void CreateFromStorage(PrimData store)
        {
            //need to clean this up as it shares a lot of code with CreateFromPacket()
            ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
            objupdate.RegionData.RegionHandle = OpenSimRoot.Instance.Cfg.RegionHandle;
            objupdate.RegionData.TimeDilation = 64096;
            objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];

            this.primData = store;
            objupdate.ObjectData[0] = new ObjectUpdatePacket.ObjectDataBlock();
            objupdate.ObjectData[0].PSBlock = new byte[0];
            objupdate.ObjectData[0].ExtraParams = new byte[1];
            objupdate.ObjectData[0].MediaURL = new byte[0];
            objupdate.ObjectData[0].NameValue = new byte[0];
            objupdate.ObjectData[0].Text = new byte[0];
            objupdate.ObjectData[0].TextColor = new byte[4];
            objupdate.ObjectData[0].JointAxisOrAnchor = new LLVector3(0, 0, 0);
            objupdate.ObjectData[0].JointPivot = new LLVector3(0, 0, 0);
            objupdate.ObjectData[0].Material = 3;
            objupdate.ObjectData[0].UpdateFlags = 32 + 65536 + 131072 + 256 + 4 + 8 + 2048 + 524288 + 268435456;
            objupdate.ObjectData[0].TextureAnim = new byte[0];
            objupdate.ObjectData[0].Sound = LLUUID.Zero;

            if (store.Texture == null)
            {
                LLObject.TextureEntry ntex = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));
                objupdate.ObjectData[0].TextureEntry = ntex.ToBytes();
            }
            else
            {
                objupdate.ObjectData[0].TextureEntry = store.Texture;
            }

            objupdate.ObjectData[0].State = 0;
            objupdate.ObjectData[0].Data = new byte[0];
            objupdate.ObjectData[0].OwnerID = this.primData.OwnerID;
            objupdate.ObjectData[0].PCode = this.primData.PCode;
            objupdate.ObjectData[0].PathBegin = this.primData.PathBegin;
            objupdate.ObjectData[0].PathEnd = this.primData.PathEnd;
            objupdate.ObjectData[0].PathScaleX = this.primData.PathScaleX;
            objupdate.ObjectData[0].PathScaleY = this.primData.PathScaleY;
            objupdate.ObjectData[0].PathShearX = this.primData.PathShearX;
            objupdate.ObjectData[0].PathShearY = this.primData.PathShearY;
            objupdate.ObjectData[0].PathSkew = this.primData.PathSkew;
            objupdate.ObjectData[0].ProfileBegin = this.primData.ProfileBegin;
            objupdate.ObjectData[0].ProfileEnd = this.primData.ProfileEnd;
            objupdate.ObjectData[0].Scale = this.primData.Scale;
            objupdate.ObjectData[0].PathCurve = this.primData.PathCurve;
            objupdate.ObjectData[0].ProfileCurve = this.primData.ProfileCurve;
            objupdate.ObjectData[0].ParentID = 0;
            objupdate.ObjectData[0].ProfileHollow = this.primData.ProfileHollow;
            //finish off copying rest of shape data
            objupdate.ObjectData[0].PathRadiusOffset = this.primData.PathRadiusOffset;
            objupdate.ObjectData[0].PathRevolutions = this.primData.PathRevolutions;
            objupdate.ObjectData[0].PathTaperX = this.primData.PathTaperX;
            objupdate.ObjectData[0].PathTaperY = this.primData.PathTaperY;
            objupdate.ObjectData[0].PathTwist = this.primData.PathTwist;
            objupdate.ObjectData[0].PathTwistBegin = this.primData.PathTwistBegin;

            objupdate.ObjectData[0].ID = (uint)store.LocalID;
            objupdate.ObjectData[0].FullID = store.FullID;

            objupdate.ObjectData[0].ObjectData = new byte[60];
            objupdate.ObjectData[0].ObjectData[46] = 128;
            objupdate.ObjectData[0].ObjectData[47] = 63;
            LLVector3 pos1 = store.Position;
            //update position
            byte[] pb = pos1.GetBytes();
            Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 0, pb.Length);

            this.uuid = objupdate.ObjectData[0].FullID;
            this.localid = objupdate.ObjectData[0].ID;
            this.position = pos1;
            this.OurPacket = objupdate;

        }
        public ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreateImprovedBlock()
        {
            uint ID = this.localid;
            byte[] bytes = new byte[60];

            int i = 0;
            ImprovedTerseObjectUpdatePacket.ObjectDataBlock dat = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();
            //dat.TextureEntry = this.OurPacket.ObjectData[0].TextureEntry;
            dat.TextureEntry = new byte[0];
            //Console.WriteLine("texture-entry length in improvedterse block is " + this.OurPacket.ObjectData[0].TextureEntry.Length);
            bytes[i++] = (byte)(ID % 256);
            bytes[i++] = (byte)((ID >> 8) % 256);
            bytes[i++] = (byte)((ID >> 16) % 256);
            bytes[i++] = (byte)((ID >> 24) % 256);
            bytes[i++] = 0;
            bytes[i++] = 0;

            LLVector3 lPos;
            Axiom.MathLib.Quaternion lRot;
            if (this._physActor != null && this.physicsEnabled)
            {
                PhysicsVector pPos = this._physActor.Position;
                lPos = new LLVector3(pPos.X, pPos.Y, pPos.Z);
                lRot = this._physActor.Orientation;
            }
            else
            {
                lPos = this.position;
                lRot = this.rotation;
            }
            byte[] pb = lPos.GetBytes();
            Array.Copy(pb, 0, bytes, i, pb.Length);
            i += 12;
            ushort ac = 32767;

            //vel
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            //accel
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            ushort rw, rx, ry, rz;
            rw = (ushort)(32768 * (lRot.w + 1));
            rx = (ushort)(32768 * (lRot.x + 1));
            ry = (ushort)(32768 * (lRot.y + 1));
            rz = (ushort)(32768 * (lRot.z + 1));

            //rot
            bytes[i++] = (byte)(rx % 256);
            bytes[i++] = (byte)((rx >> 8) % 256);
            bytes[i++] = (byte)(ry % 256);
            bytes[i++] = (byte)((ry >> 8) % 256);
            bytes[i++] = (byte)(rz % 256);
            bytes[i++] = (byte)((rz >> 8) % 256);
            bytes[i++] = (byte)(rw % 256);
            bytes[i++] = (byte)((rw >> 8) % 256);

            //rotation vel
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);
            bytes[i++] = (byte)(ac % 256);
            bytes[i++] = (byte)((ac >> 8) % 256);

            dat.Data = bytes;
            return dat;
        }

        public override void BackUp()
        {
            this.primData.FullID = this.uuid;
            this.primData.LocalID = this.localid;
            this.primData.Position = this.position;
            this.primData.Rotation = new LLQuaternion(this.rotation.x, this.rotation.y, this.rotation.z, this.rotation.w);
            OpenSimRoot.Instance.LocalWorld.localStorage.StorePrim(this.primData);
        }
    }

}
