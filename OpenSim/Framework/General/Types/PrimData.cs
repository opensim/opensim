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
        public byte[] TextureEntry; // a LL textureEntry in byte[] format

        public Int32 CreationDate;
        public uint OwnerMask = FULL_MASK_PERMISSIONS;
        public uint NextOwnerMask = FULL_MASK_PERMISSIONS;
        public uint GroupMask = FULL_MASK_PERMISSIONS;
        public uint EveryoneMask = FULL_MASK_PERMISSIONS;
        public uint BaseMask = FULL_MASK_PERMISSIONS;

        //following only used during prim storage
        public LLVector3 Position;
        public LLQuaternion Rotation = new LLQuaternion(0, 1, 0, 0);
        public uint LocalID;
        public LLUUID FullID;

        public PrimData()
        {

        }

        public PrimData(byte[] data)
        {
            int i = 0;

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
            this.PathTaperY = (sbyte)data[i++];
            this.PathTwist = (sbyte)data[i++];
            this.PathTwistBegin = (sbyte)data[i++];
            ushort length = (ushort)(data[i++] + (data[i++] << 8));
            this.TextureEntry = new byte[length];
            Array.Copy(data, i, TextureEntry, 0, length); i += length;
            this.CreationDate = (Int32)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.OwnerMask = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.NextOwnerMask = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.GroupMask = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.EveryoneMask = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.BaseMask = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.Position = new LLVector3(data, i); i += 12;
            this.Rotation = new LLQuaternion(data, i, true); i += 12;
            this.LocalID = (uint)(data[i++] + (data[i++] << 8) + (data[i++] << 16) + (data[i++] << 24));
            this.FullID = new LLUUID(data, i); i += 16;

        }

        public byte[] ToBytes()
        {
            int i = 0;
            byte[] bytes = new byte[126 + TextureEntry.Length];
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
            bytes[i++] = (byte)(this.ProfileHollow % 256);
            bytes[i++] = (byte)((this.ProfileHollow >> 8) % 256);
            bytes[i++] = ((byte)this.PathRadiusOffset);
            bytes[i++] = this.PathRevolutions;
            bytes[i++] = ((byte)this.PathTaperX);
            bytes[i++] = ((byte)this.PathTaperY);
            bytes[i++] = ((byte)this.PathTwist);
            bytes[i++] = ((byte)this.PathTwistBegin);
            bytes[i++] = (byte)(TextureEntry.Length % 256);
            bytes[i++] = (byte)((TextureEntry.Length >> 8) % 256);
            Array.Copy(TextureEntry, 0, bytes, i, TextureEntry.Length); i += TextureEntry.Length;
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
            if (this.Rotation == new LLQuaternion(0, 0, 0, 0))
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

        public static PrimData DefaultCube()
        {
            PrimData primData = new PrimData();
            primData.CreationDate = (Int32)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            primData.FullID = LLUUID.Random();
            primData.Scale = new LLVector3(0.5f, 0.5f, 0.5f);
            primData.Rotation = new LLQuaternion(0, 0, 0, 1);
            primData.PCode = 9;
            primData.ParentID = 0;
            primData.PathBegin = 0;
            primData.PathEnd = 0;
            primData.PathScaleX = 0;
            primData.PathScaleY = 0;
            primData.PathShearX = 0;
            primData.PathShearY = 0;
            primData.PathSkew = 0;
            primData.ProfileBegin = 0;
            primData.ProfileEnd = 0;
            primData.PathCurve = 16;
            primData.ProfileCurve = 1;
            primData.ProfileHollow = 0;
            primData.PathRadiusOffset = 0;
            primData.PathRevolutions = 0;
            primData.PathTaperX = 0;
            primData.PathTaperY = 0;
            primData.PathTwist = 0;
            primData.PathTwistBegin = 0;

            return primData;
        }
    }
}
