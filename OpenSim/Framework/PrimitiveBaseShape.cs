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

using System;
using System.Xml.Serialization;
using libsecondlife;

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

    [Serializable]
    public class PrimitiveBaseShape
    {
        private static readonly LLObject.TextureEntry m_defaultTexture;
        public byte[] ExtraParams;
        private byte[] m_textureEntry;

        public ushort PathBegin;
        public byte PathCurve;
        public ushort PathEnd;
        public sbyte PathRadiusOffset;
        public byte PathRevolutions;
        public byte PathScaleX;
        public byte PathScaleY;
        public byte PathShearX;
        public byte PathShearY;
        public sbyte PathSkew;
        public sbyte PathTaperX;
        public sbyte PathTaperY;
        public sbyte PathTwist;
        public sbyte PathTwistBegin;
        public byte PCode;
        public ushort ProfileBegin;

        [XmlIgnore] // -- this one is re-constructed from ProfileShape and ProfileHollow
            public byte ProfileCurve;

        public ushort ProfileEnd;
        public ushort ProfileHollow;
        public LLVector3 Scale;
        public byte State;

        static PrimitiveBaseShape()
        {
            m_defaultTexture =
                new LLObject.TextureEntry(new LLUUID("89556747-24cb-43ed-920b-47caed15465f"));
        }

        public PrimitiveBaseShape()
        {
            PCode = (byte) PCodeEnum.Primitive;
            ExtraParams = new byte[1];
            Textures = m_defaultTexture;
        }

        [XmlIgnore]
        public LLObject.TextureEntry Textures
        {
            get { return new LLObject.TextureEntry(m_textureEntry, 0, m_textureEntry.Length); }

            set { m_textureEntry = value.ToBytes(); }
        }

        public byte[] TextureEntry
        {
            get { return m_textureEntry; }

            set { m_textureEntry = value; }
        }

        public ProfileShape ProfileShape
        {
            get { return (ProfileShape) (ProfileCurve & 0xf); }
            set
            {
                byte oldValueMasked = (byte) (ProfileCurve & 0xf0);
                ProfileCurve = (byte) (oldValueMasked | (byte) value);
            }
        }

        public HollowShape HollowShape
        {
            get { return (HollowShape) (ProfileCurve & 0xf0); }
            set
            {
                byte oldValueMasked = (byte) (ProfileCurve & 0x0f);
                ProfileCurve = (byte) (oldValueMasked | (byte) value);
            }
        }

        public LLVector3 PrimScale
        {
            get { return Scale; }
        }

        public static PrimitiveBaseShape Default
        {
            get
            {
                PrimitiveBaseShape boxShape = CreateBox();

                boxShape.SetScale(0.5f);

                return boxShape;
            }
        }


        public static PrimitiveBaseShape Create()
        {
            PrimitiveBaseShape shape = new PrimitiveBaseShape();
            return shape;
        }

        public static PrimitiveBaseShape CreateBox()
        {
            PrimitiveBaseShape shape = Create();

            shape.PathCurve = (byte) Extrusion.Straight;
            shape.ProfileShape = ProfileShape.Square;
            shape.PathScaleX = 100;
            shape.PathScaleY = 100;

            return shape;
        }

        public static PrimitiveBaseShape CreateCylinder()
        {
            PrimitiveBaseShape shape = Create();

            shape.PathCurve = (byte) Extrusion.Curve1;
            shape.ProfileShape = ProfileShape.Square;

            shape.PathScaleX = 100;
            shape.PathScaleY = 100;

            return shape;
        }

        public void SetScale(float side)
        {
            Scale = new LLVector3(side, side, side);
        }

        public void SetHeigth(float heigth)
        {
            Scale.Z = heigth;
        }

        public void SetRadius(float radius)
        {
            Scale.X = Scale.Y = radius*2f;
        }

        //void returns need to change of course
        public virtual void GetMesh()
        {
        }

        public PrimitiveBaseShape Copy()
        {
            return (PrimitiveBaseShape) MemberwiseClone();
        }

        public static PrimitiveBaseShape CreateCylinder(float radius, float heigth)
        {
            PrimitiveBaseShape shape = CreateCylinder();

            shape.SetHeigth(heigth);
            shape.SetRadius(radius);

            return shape;
        }

        public void SetPathRange(LLVector3 pathRange)
        {
            PathBegin = LLObject.PackBeginCut(pathRange.X);
            PathEnd = LLObject.PackEndCut(pathRange.Y);
        }

        public void SetProfileRange(LLVector3 profileRange)
        {
            ProfileBegin = LLObject.PackBeginCut(profileRange.X);
            ProfileEnd = LLObject.PackEndCut(profileRange.Y);
        }
    }
}