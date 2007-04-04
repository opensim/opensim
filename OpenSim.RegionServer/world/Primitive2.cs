using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.types;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Assets;
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
        private string inventoryFileName = "";

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

        public override void update()
        {
            LLVector3 pos2 = new LLVector3(0, 0, 0);
        }

        public void UpdateShape(ObjectShapePacket.ObjectDataBlock addPacket)
        {

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

        public override void BackUp()
        {

        }

        public void SendFullUpdateToClient(SimClient RemoteClient)
        {

        }

        public ImprovedTerseObjectUpdatePacket.ObjectDataBlock CreateImprovedBlock()
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
