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
    public class Primitive2 : Entity
    {
        protected PrimData primData;
        //private ObjectUpdatePacket OurPacket;
        private LLVector3 positionLastFrame = new LLVector3(0, 0, 0);
        private Dictionary<uint, ClientView> m_clientThreads;
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
        public override LLVector3 Pos
        {
            get
            {
                return base.Pos;
            }
            set
            {
                base.Pos = value;
            }
        }
        #endregion

        public Primitive2(Dictionary<uint, ClientView> clientThreads, ulong regionHandle, World world)
        {
            m_clientThreads = clientThreads;
            m_regionHandle = regionHandle;
            m_world = world;
            inventoryItems = new Dictionary<LLUUID, InventoryItem>();
        }

        public Primitive2(Dictionary<uint, ClientView> clientThreads, ulong regionHandle, World world, LLUUID owner)
        {
            m_clientThreads = clientThreads;
            m_regionHandle = regionHandle;
            m_world = world;
            inventoryItems = new Dictionary<LLUUID, InventoryItem>();
            this.primData = new PrimData();
            this.primData.CreationDate = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            this.primData.OwnerID = owner;
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

        public void UpdatePosition(LLVector3 pos)
        {

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
        }

        public void UpdateTexture(byte[] tex)
        {
            this.primData.Texture = tex;
            //this.dirtyFlag = true;
        }

        public void UpdateObjectFlags(ObjectFlagUpdatePacket pack)
        {

        }

        public void AssignToParent(Primitive prim)
        {

        }

        public void GetProperites(ClientView client)
        {
            ObjectPropertiesPacket proper = new ObjectPropertiesPacket();
            proper.ObjectData = new ObjectPropertiesPacket.ObjectDataBlock[1];
            proper.ObjectData[0] = new ObjectPropertiesPacket.ObjectDataBlock();
            proper.ObjectData[0].ItemID = LLUUID.Zero;
            proper.ObjectData[0].CreationDate = (ulong)this.primData.CreationDate;
            proper.ObjectData[0].CreatorID = this.primData.OwnerID;
            proper.ObjectData[0].FolderID = LLUUID.Zero;
            proper.ObjectData[0].FromTaskID = LLUUID.Zero;
            proper.ObjectData[0].GroupID = LLUUID.Zero;
            proper.ObjectData[0].InventorySerial = 0;
            proper.ObjectData[0].LastOwnerID = LLUUID.Zero;
            proper.ObjectData[0].ObjectID = this.uuid;
            proper.ObjectData[0].OwnerID = primData.OwnerID;
            proper.ObjectData[0].TouchName = new byte[0];
            proper.ObjectData[0].TextureID = new byte[0];
            proper.ObjectData[0].SitName = new byte[0];
            proper.ObjectData[0].Name = new byte[0];
            proper.ObjectData[0].Description = new byte[0];
            proper.ObjectData[0].OwnerMask = this.primData.OwnerMask;
            proper.ObjectData[0].NextOwnerMask = this.primData.NextOwnerMask;
            proper.ObjectData[0].GroupMask = this.primData.GroupMask;
            proper.ObjectData[0].EveryoneMask = this.primData.EveryoneMask;
            proper.ObjectData[0].BaseMask = this.primData.BaseMask;

            client.OutPacket(proper);
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

        public void RequestInventoryInfo(ClientView simClient, RequestTaskInventoryPacket packet)
        {

        }

        public void RequestXferInventory(ClientView simClient, ulong xferID)
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

        //should change these mehtods, so that outgoing packets are sent through the avatar class
        public void SendFullUpdateToClient(ClientView remoteClient)
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

            ObjectUpdatePacket outPacket = new ObjectUpdatePacket();
            outPacket.ObjectData = new ObjectUpdatePacket.ObjectDataBlock[1];
            outPacket.ObjectData[0] = this.CreateUpdateBlock();
            byte[] pb = lPos.GetBytes();
            Array.Copy(pb, 0, outPacket.ObjectData[0].ObjectData, 0, pb.Length);

            remoteClient.OutPacket(outPacket);
        }

        public void SendFullUpdateToAllClients()
        {

        }

        public void SendTerseUpdateToClient(ClientView RemoteClient)
        {

        }

        public void SendTerseUpdateToALLClients()
        {

        }

        #endregion

        #region Create Methods

        public void CreateFromPacket(ObjectAddPacket addPacket, LLUUID ownerID, uint localID)
        {
            PrimData PData = new PrimData();
            this.primData = PData;
            this.primData.CreationDate = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            PData.OwnerID = ownerID;
            PData.PCode = addPacket.ObjectData.PCode;
            PData.PathBegin = addPacket.ObjectData.PathBegin;
            PData.PathEnd = addPacket.ObjectData.PathEnd;
            PData.PathScaleX = addPacket.ObjectData.PathScaleX;
            PData.PathScaleY = addPacket.ObjectData.PathScaleY;
            PData.PathShearX = addPacket.ObjectData.PathShearX;
            PData.PathShearY = addPacket.ObjectData.PathShearY;
            PData.PathSkew = addPacket.ObjectData.PathSkew;
            PData.ProfileBegin = addPacket.ObjectData.ProfileBegin;
            PData.ProfileEnd = addPacket.ObjectData.ProfileEnd;
            PData.Scale = addPacket.ObjectData.Scale;
            PData.PathCurve = addPacket.ObjectData.PathCurve;
            PData.ProfileCurve = addPacket.ObjectData.ProfileCurve;
            PData.ParentID = 0;
            PData.ProfileHollow = addPacket.ObjectData.ProfileHollow;
            PData.PathRadiusOffset = addPacket.ObjectData.PathRadiusOffset;
            PData.PathRevolutions = addPacket.ObjectData.PathRevolutions;
            PData.PathTaperX = addPacket.ObjectData.PathTaperX;
            PData.PathTaperY = addPacket.ObjectData.PathTaperY;
            PData.PathTwist = addPacket.ObjectData.PathTwist;
            PData.PathTwistBegin = addPacket.ObjectData.PathTwistBegin;
            LLVector3 pos1 = addPacket.ObjectData.RayEnd;
            this.primData.FullID = this.uuid = LLUUID.Random();
            this.localid = (uint)(localID);
            this.primData.Position = this.Pos = pos1;
        }

        public void CreateFromBytes(byte[] data)
        {

        }

        public void CreateFromPrimData(PrimData primData)
        {
            this.CreateFromPrimData(primData, primData.Position, primData.LocalID, false);
        }

        public void CreateFromPrimData(PrimData primData, LLVector3 posi, uint localID, bool newprim)
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

        protected void SetPacketShapeData(ObjectUpdatePacket.ObjectDataBlock objectData)
        {
            objectData.OwnerID = this.primData.OwnerID;
            objectData.PCode = this.primData.PCode;
            objectData.PathBegin = this.primData.PathBegin;
            objectData.PathEnd = this.primData.PathEnd;
            objectData.PathScaleX = this.primData.PathScaleX;
            objectData.PathScaleY = this.primData.PathScaleY;
            objectData.PathShearX = this.primData.PathShearX;
            objectData.PathShearY = this.primData.PathShearY;
            objectData.PathSkew = this.primData.PathSkew;
            objectData.ProfileBegin = this.primData.ProfileBegin;
            objectData.ProfileEnd = this.primData.ProfileEnd;
            objectData.Scale = this.primData.Scale;
            objectData.PathCurve = this.primData.PathCurve;
            objectData.ProfileCurve = this.primData.ProfileCurve;
            objectData.ParentID = this.primData.ParentID;
            objectData.ProfileHollow = this.primData.ProfileHollow;
            objectData.PathRadiusOffset = this.primData.PathRadiusOffset;
            objectData.PathRevolutions = this.primData.PathRevolutions;
            objectData.PathTaperX = this.primData.PathTaperX;
            objectData.PathTaperY = this.primData.PathTaperY;
            objectData.PathTwist = this.primData.PathTwist;
            objectData.PathTwistBegin = this.primData.PathTwistBegin;
        }

        #endregion
        protected ObjectUpdatePacket.ObjectDataBlock CreateUpdateBlock()
        {
            ObjectUpdatePacket.ObjectDataBlock objupdate = new ObjectUpdatePacket.ObjectDataBlock();
            this.SetDefaultPacketValues(objupdate);
            objupdate.UpdateFlags = 32 + 65536 + 131072 + 256 + 4 + 8 + 2048 + 524288 + 268435456;
            this.SetPacketShapeData(objupdate);
            byte[] pb = this.Pos.GetBytes();
            Array.Copy(pb, 0, objupdate.ObjectData, 0, pb.Length);
            return objupdate;
        }

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
