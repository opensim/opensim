using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.types;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Types;
using OpenSim.Framework.Inventory;

namespace OpenSim.world
{
    public class Primitive2 :Entity
    {
        protected PrimData primData;
        private ObjectUpdatePacket OurPacket;
        private LLVector3 positionLastFrame = new LLVector3(0, 0, 0);
        private Dictionary<uint, SimClient> m_clientThreads;
        private ulong m_regionHandle;
        private const uint FULL_MASK_PERMISSIONS = 2147483647;
        private bool physicsEnabled = false;

        private Dictionary<LLUUID, InventoryItem> inventoryItems;

        #region Properties

        public LLVector3 Scale
        {
            set
            {
                this.primData.Scale = value;
                //this.dirtyFlag = true;
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

        #endregion

        public Primitive2(Dictionary<uint, SimClient> clientThreads, ulong regionHandle, World world)
        {
            m_clientThreads = clientThreads;
            m_regionHandle = regionHandle;
            m_world = world;
            inventoryItems = new Dictionary<LLUUID, InventoryItem>();
        }

        public byte[] GetByteArray()
        {
            byte[] result = null;
            List<byte[]> dataArrays = new List<byte[]>();
            dataArrays.Add(primData.ToBytes());
            foreach (Entity child in children)
            {
                if (child is OpenSim.world.Primitive2)
                {
                    dataArrays.Add(((OpenSim.world.Primitive2)child).GetByteArray());
                }
            }
            byte[] primstart = Helpers.StringToField("<Prim>");
            byte[] primend = Helpers.StringToField("</Prim>");
            int totalLength = primstart.Length + primend.Length;
            for (int i = 0; i < dataArrays.Count; i++)
            {
                totalLength += dataArrays[i].Length;
            }

            result = new byte[totalLength];
            int arraypos = 0;
            Array.Copy(primstart, 0, result, 0, primstart.Length);
            arraypos += primstart.Length;
            for (int i = 0; i < dataArrays.Count; i++)
            {
                Array.Copy(dataArrays[i], 0, result, arraypos, dataArrays[i].Length);
                arraypos += dataArrays[i].Length;
            }
            Array.Copy(primend, 0, result, arraypos, primend.Length);

            return result;
        }

        #region Overridden Methods

        public override void update()
        {
            LLVector3 pos2 = new LLVector3(0, 0, 0);
        }

        public override void BackUp()
        {

        }

        #endregion

        #region Packet handlers

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
        }

        public void UpdateTexture(byte[] tex)
        {
            this.OurPacket.ObjectData[0].TextureEntry = tex;
            this.primData.Texture = tex;
            //this.dirtyFlag = true;
        }

        public void UpdateObjectFlags(ObjectFlagUpdatePacket pack)
        {

        }

        public void AssignToParent(Primitive prim)
        {

        }

        #endregion

        # region Inventory Methods

        public bool AddToInventory(InventoryItem item)
        {
            return false;
        }

        public InventoryItem RemoveFromInventory(LLUUID itemID)
        {
            return null;
        }

        public void RequestInventoryInfo(SimClient simClient, RequestTaskInventoryPacket packet)
        {

        }

        public void RequestXferInventory(SimClient simClient, ulong xferID)
        {
            //will only currently work if the total size of the inventory data array is under about 1000 bytes
            SendXferPacketPacket send = new SendXferPacketPacket();

            send.XferID.ID = xferID;
            send.XferID.Packet = 1 + 2147483648;
            send.DataPacket.Data = this.ConvertInventoryToBytes();

            simClient.OutPacket(send);
        }

        public byte[] ConvertInventoryToBytes()
        {
            System.Text.Encoding enc = System.Text.Encoding.ASCII;
            byte[] result = new byte[0];
            List<byte[]> inventoryData = new List<byte[]>();
            int totallength = 0;
            foreach (InventoryItem invItem in inventoryItems.Values)
            {
                byte[] data = enc.GetBytes(invItem.ExportString());
                inventoryData.Add(data);
                totallength += data.Length;
            }
            //TODO: copy arrays into the single result array

            return result;
        }

        public void CreateInventoryFromBytes(byte[] data)
        {

        }

        #endregion

        #region Update viewers Methods

        public void SendFullUpdateToClient(SimClient remoteClient)
        {
            LLVector3 lPos;
            if (this._physActor != null && this.physicsEnabled)
            {
                PhysicsVector pPos = this._physActor.Position;
                lPos = new LLVector3(pPos.X, pPos.Y, pPos.Z);
            }
            else
            {
                lPos = this.Pos;
            }
            byte[] pb = lPos.GetBytes();
            Array.Copy(pb, 0, OurPacket.ObjectData[0].ObjectData, 0, pb.Length);

            this.UpdatePacketShapeData();
            remoteClient.OutPacket(OurPacket);
        }

        public void SendTerseUpdateToClient(SimClient RemoteClient)
        {

        }

        public void SendTerseUpdateToALLClients()
        {

        }

        #endregion

        #region Create Methods

        public void CreateFromPacket(ObjectAddPacket addPacket, LLUUID agentID, uint localID)
        {
            ObjectUpdatePacket objupdate = new ObjectUpdatePacket();
            objupdate.RegionData.RegionHandle = m_regionHandle;
            objupdate.RegionData.TimeDilation = 64096;
            objupdate.ObjectData = new libsecondlife.Packets.ObjectUpdatePacket.ObjectDataBlock[1];
            PrimData PData = new PrimData();
            this.primData = PData;
            this.primData.CreationDate = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            objupdate.ObjectData[0] = new ObjectUpdatePacket.ObjectDataBlock();
            this.SetDefaultPacketValues(objupdate.ObjectData[0]);

            objupdate.ObjectData[0].UpdateFlags = 32 + 65536 + 131072 + 256 + 4 + 8 + 2048 + 524288 + 268435456;
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
            objupdate.ObjectData[0].FullID = LLUUID.Random();
            LLVector3 pos1 = addPacket.ObjectData.RayEnd;
            //update position
            byte[] pb = pos1.GetBytes();
            Array.Copy(pb, 0, objupdate.ObjectData[0].ObjectData, 0, pb.Length);
            //this.newPrimFlag = true;
            this.primData.FullID = this.uuid = objupdate.ObjectData[0].FullID;
            this.localid = objupdate.ObjectData[0].ID;
            this.primData.Position = this.Pos = pos1;
            this.OurPacket = objupdate;
        }

        public void CreateFromBytes(byte[] data)
        {

        }

        public void CreateFromPrimData(PrimData primData)
        {

        }

        #endregion

        #region Packet Update Methods
        protected void SetDefaultPacketValues(ObjectUpdatePacket.ObjectDataBlock objdata)
        {
            objdata.PSBlock = new byte[0];
            objdata.ExtraParams = new byte[1];
            objdata.MediaURL = new byte[0];
            objdata.NameValue = new byte[0];
            objdata.Text = new byte[0];
            objdata.TextColor = new byte[4];
            objdata.JointAxisOrAnchor = new LLVector3(0, 0, 0);
            objdata.JointPivot = new LLVector3(0, 0, 0);
            objdata.Material = 3;
            objdata.TextureAnim = new byte[0];
            objdata.Sound = LLUUID.Zero;
            LLObject.TextureEntry ntex = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-5005-000000000005"));
            this.primData.Texture = objdata.TextureEntry = ntex.ToBytes();
            objdata.State = 0;
            objdata.Data = new byte[0];

            objdata.ObjectData = new byte[60];
            objdata.ObjectData[46] = 128;
            objdata.ObjectData[47] = 63;
        }

        protected void UpdatePacketShapeData()
        {
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
            OurPacket.ObjectData[0].ParentID = this.primData.ParentID;
            OurPacket.ObjectData[0].ProfileHollow = this.primData.ProfileHollow;
            OurPacket.ObjectData[0].PathRadiusOffset = this.primData.PathRadiusOffset;
            OurPacket.ObjectData[0].PathRevolutions = this.primData.PathRevolutions;
            OurPacket.ObjectData[0].PathTaperX = this.primData.PathTaperX;
            OurPacket.ObjectData[0].PathTaperY = this.primData.PathTaperY;
            OurPacket.ObjectData[0].PathTwist = this.primData.PathTwist;
            OurPacket.ObjectData[0].PathTwistBegin = this.primData.PathTwistBegin;
        }

        #endregion

        protected ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreateImprovedBlock()
        {
            uint ID = this.localid;
            byte[] bytes = new byte[60];

            int i = 0;
            ImprovedTerseObjectUpdatePacket.ObjectDataBlock dat = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock();
            dat.TextureEntry = new byte[0];
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
                lPos = this.Pos;
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
    }
}
