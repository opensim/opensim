using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Framework.Types
{
    public class PrimData
    {
        private const uint FULL_MASK_PERMISSIONS = 2147483647;

        public LLUUID OwnerID;
        public byte PCode;
        public ushort PathBegin;
        public ushort PathEnd;
        public byte PathScaleX;
        public byte PathScaleY;
        public byte PathShearX;
        public byte PathShearY;
        public sbyte PathSkew;
        public ushort ProfileBegin;
        public ushort ProfileEnd;
        public LLVector3 Scale;
        public byte PathCurve;
        public byte ProfileCurve;
        public uint ParentID = 0;
        public ushort ProfileHollow;
        public sbyte PathRadiusOffset;
        public byte PathRevolutions;
        public sbyte PathTaperX;
        public sbyte PathTaperY;
        public sbyte PathTwist;
        public sbyte PathTwistBegin;
        public byte[] Texture;
       

        public Int32 CreationDate;
        public uint OwnerMask = FULL_MASK_PERMISSIONS;
        public uint NextOwnerMask = FULL_MASK_PERMISSIONS;
        public uint GroupMask = FULL_MASK_PERMISSIONS;
        public uint EveryoneMask = FULL_MASK_PERMISSIONS;
        public uint BaseMask = FULL_MASK_PERMISSIONS;

        //following only used during prim storage
        public LLVector3 Position;
        public LLQuaternion Rotation = new LLQuaternion(0,1,0,0);
        public uint LocalID;
        public LLUUID FullID;

        public PrimData()
        {

        }

        public PrimData(byte[] data)
        {
            int i =0;

            this.OwnerID = new LLUUID(data, i); i += 16;
            this.PCode = data[i++];
            this.PathBegin = (ushort)(data[i++] + (data[i++] << 8));
            this.PathEnd = (ushort)(data[i++] + (data[i++] << 8));
            this.PathScaleX = data[i++];
            this.PathScaleY = data[i++];
            this.PathShearX = data[i++];
            this.PathShearY = data[i++];
            this.PathSkew = (sbyte)data[i++];
            this.ProfileBegin = (ushort)(data[i++] + (data[i++] << 8));
            this.ProfileEnd = (ushort)(data[i++] + (data[i++] << 8));
            this.Scale = new LLVector3(data, i); i += 12;
            this.PathCurve = data[i++];
            this.ProfileCurve = data[i++];
            this.ParentID = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.ProfileHollow = (ushort)(data[i++] + (data[i++] << 8));
            this.PathRadiusOffset = (sbyte)data[i++];
            this.PathRevolutions = data[i++];
            this.PathTaperX = (sbyte)data[i++];
            this.PathTaperY =(sbyte) data[i++];
            this.PathTwist = (sbyte) data[i++];
            this.PathTwistBegin = (sbyte) data[i++];
            ushort length = (ushort)(data[i++] + (data[i++] << 8));
            this.Texture = new byte[length];
            Array.Copy(data, i, Texture, 0, length); i += length;
            this.CreationDate = (Int32)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.OwnerMask = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.NextOwnerMask = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.GroupMask = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.EveryoneMask = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.BaseMask = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.Position = new LLVector3(data, i); i += 12;
            this.Rotation = new LLQuaternion(data,i, true); i += 12;
            this.LocalID = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.FullID = new LLUUID(data, i); i += 16;

        }

        public byte[] ToBytes()
        {
            int i = 0;
            byte[] bytes = new byte[121 + Texture.Length];
            Array.Copy(OwnerID.GetBytes(), 0, bytes, i, 16); i += 16;
            bytes[i++] = this.PCode;
            bytes[i++] = (byte)(this.PathBegin % 256);
            bytes[i++] = (byte)((this.PathBegin >> 8) % 256);
            bytes[i++] = (byte)(this.PathEnd % 256);
            bytes[i++] = (byte)((this.PathEnd >> 8) % 256);
            bytes[i++] = this.PathScaleX;
            bytes[i++] = this.PathScaleY;
            bytes[i++] = this.PathShearX;
            bytes[i++] = this.PathShearY;
            bytes[i++] = (byte)this.PathSkew;
            bytes[i++] = (byte)(this.ProfileBegin % 256);
            bytes[i++] = (byte)((this.ProfileBegin >> 8) % 256);
            bytes[i++] = (byte)(this.ProfileEnd % 256);
            bytes[i++] = (byte)((this.ProfileEnd >> 8) % 256);
            Array.Copy(Scale.GetBytes(), 0, bytes, i, 12); i += 12;
            bytes[i++] = this.PathCurve;
            bytes[i++] = this.ProfileCurve;
            bytes[i++] = (byte)(ParentID % 256);
            bytes[i++] = (byte)((ParentID >> 8) % 256);
            bytes[i++] = (byte)((ParentID >> 16) % 256);
            bytes[i++] = (byte)((ParentID >> 24) % 256);
            bytes[i++] = (byte)(this.ProfileHollow %256);
            bytes[i++] = (byte)((this.ProfileHollow >> 8)% 256);
            bytes[i++] = ((byte)this.PathRadiusOffset);
            bytes[i++] = this.PathRevolutions;
            bytes[i++] = ((byte) this.PathTaperX);
            bytes[i++] = ((byte) this.PathTaperY);
            bytes[i++] = ((byte) this.PathTwist);
            bytes[i++] = ((byte) this.PathTwistBegin);
            bytes[i++] = (byte)(Texture.Length % 256);
            bytes[i++] = (byte)((Texture.Length >> 8) % 256);
            Array.Copy(Texture, 0, bytes, i, Texture.Length); i += Texture.Length;
            bytes[i++] = (byte)(this.CreationDate % 256);
            bytes[i++] = (byte)((this.CreationDate >> 8) % 256);
            bytes[i++] = (byte)((this.CreationDate >> 16) % 256);
            bytes[i++] = (byte)((this.CreationDate >> 24) % 256);
            bytes[i++] = (byte)(this.OwnerMask % 256);
            bytes[i++] = (byte)((this.OwnerMask >> 8) % 256);
            bytes[i++] = (byte)((this.OwnerMask >> 16) % 256);
            bytes[i++] = (byte)((this.OwnerMask >> 24) % 256);
            bytes[i++] = (byte)(this.NextOwnerMask % 256);
            bytes[i++] = (byte)((this.NextOwnerMask >> 8) % 256);
            bytes[i++] = (byte)((this.NextOwnerMask >> 16) % 256);
            bytes[i++] = (byte)((this.NextOwnerMask >> 24) % 256);
            bytes[i++] = (byte)(this.GroupMask % 256);
            bytes[i++] = (byte)((this.GroupMask >> 8) % 256);
            bytes[i++] = (byte)((this.GroupMask >> 16) % 256);
            bytes[i++] = (byte)((this.GroupMask >> 24) % 256);
            bytes[i++] = (byte)(this.EveryoneMask % 256);
            bytes[i++] = (byte)((this.EveryoneMask >> 8) % 256);
            bytes[i++] = (byte)((this.EveryoneMask >> 16) % 256);
            bytes[i++] = (byte)((this.EveryoneMask >> 24) % 256);
            bytes[i++] = (byte)(this.BaseMask % 256);
            bytes[i++] = (byte)((this.BaseMask >> 8) % 256);
            bytes[i++] = (byte)((this.BaseMask >> 16) % 256);
            bytes[i++] = (byte)((this.BaseMask >> 24) % 256);
            Array.Copy(this.Position.GetBytes(), 0, bytes, i, 12); i += 12;
            if (this.Rotation == new LLQuaternion(0,0,0,0))
            {
                this.Rotation = new LLQuaternion(0, 1, 0, 0);
            }
            Array.Copy(this.Rotation.GetBytes(), 0, bytes, i, 12); i += 12;
            bytes[i++] = (byte)(this.LocalID % 256);
            bytes[i++] = (byte)((this.LocalID >> 8) % 256);
            bytes[i++] = (byte)((this.LocalID >> 16) % 256);
            bytes[i++] = (byte)((this.LocalID >> 24) % 256);
            Array.Copy(FullID.GetBytes(), 0, bytes, i, 16); i += 16;

            return bytes;
        }
    }
}
