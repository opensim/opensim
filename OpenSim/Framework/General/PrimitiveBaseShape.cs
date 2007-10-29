/*
* Copyright (c) Contributors, http://opensimulator.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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

using System.Xml.Serialization;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.Framework
{
    public enum ProfileShape : byte
    {
        Circle = 0,
        Square = 1,
        IsometricTriangle = 2,
        EquilateralTriangle = 3,
        RightTriangle = 4,
        HalfCircle = 5
    }

    public enum HollowShape : byte
    {
        Same = 0,
        Circle = 16,
        Square = 32,
        Triangle = 48
    }

    public enum PCodeEnum : byte
    {
        Primitive = 9,
        Avatar = 47
    }

    public enum Extrusion : byte
    {
        Straight = 16,
        Curve1 = 32,
        Curve2 = 48,
        Flexible = 128
    }

    public class PrimitiveBaseShape
    {
        private static byte[] m_defaultTextureEntry;

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
        public ushort ProfileHollow;
        public sbyte PathRadiusOffset;
        public byte PathRevolutions;
        public sbyte PathTaperX;
        public sbyte PathTaperY;
        public sbyte PathTwist;
        public sbyte PathTwistBegin;
        public byte[] TextureEntry; // a LL textureEntry in byte[] format
        public byte[] ExtraParams;

        public ProfileShape ProfileShape
        {
            get
            {
                return (ProfileShape)(ProfileCurve & 0xf);
            }
            set
            {
                byte oldValueMasked = (byte)(ProfileCurve & 0xf0);
                ProfileCurve = (byte)(oldValueMasked | (byte)value);
            }
        }

        [XmlIgnore]
        public HollowShape HollowShape
        {
            get
            {
                return (HollowShape)(ProfileHollow & 0xf0);
            }
            set
            {
                byte oldValueMasked = (byte)(ProfileHollow & 0xf0);
                ProfileHollow = (byte)(oldValueMasked | (byte)value);
            }
        }

        public LLVector3 PrimScale
        {
            get
            {
                return this.Scale;
            }
        }

        static PrimitiveBaseShape()
        {
            m_defaultTextureEntry = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-9999-000000000005")).ToBytes();
        }

        public PrimitiveBaseShape()
        {
            PCode = (byte)PCodeEnum.Primitive;
            ExtraParams = new byte[1];
            TextureEntry = m_defaultTextureEntry;
        }

        //void returns need to change of course
        public virtual void GetMesh()
        {

        }

        public PrimitiveBaseShape Copy()
        {
            return (PrimitiveBaseShape)this.MemberwiseClone();
        }
    }

    public class GenericShape : PrimitiveBaseShape
    {
        public GenericShape()
            : base()
        {

        }
    }

    public class BoxShape : PrimitiveBaseShape
    {
        public BoxShape()
            : base()
        {
            PathCurve = (byte)Extrusion.Straight;
            ProfileShape = ProfileShape.Square;
            PathScaleX = 100;
            PathScaleY = 100;
        }

        public BoxShape(float side)
            : this()
        {
            SetSide(side);
        }

        public void SetSide(float side)
        {
            Scale = new LLVector3(side, side, side);
        }

        public static BoxShape Default
        {
            get
            {
                BoxShape boxShape = new BoxShape();

                boxShape.SetSide(0.5f);

                return boxShape;
            }
        }
    }
    public class CylinderShape : PrimitiveBaseShape
    {
        public CylinderShape()
            : base()
        {
            PathCurve = (byte)Extrusion.Straight;
            ProfileShape = ProfileShape.Circle;
            PathScaleX = 100;
            PathScaleY = 100;
        }

        public CylinderShape(float radius, float heigth)
            : this()
        {
            SetRadius(radius);
            SetHeigth(heigth);
        }

        private void SetHeigth(float heigth)
        {
            Scale.Z = heigth;
        }

        private void SetRadius(float radius)
        {
            Scale.X = Scale.Y = radius * 2f;
        }
    }
}
