using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework.Interfaces;

namespace OpenSim.Framework.Types
{
    public enum ShapeType
    {
        Box,
        Sphere,
        Ring,
        Tube,
        Torus,
        Prism,
        Scuplted,
        Cylinder
    }

    public class PrimitiveBaseShape
    {
        private ShapeType type;

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

        public ShapeType PrimType
        {
            get
            {
                return this.type;
            }
        }

        public LLVector3 PrimScale
        {
            get
            {
                return this.Scale;
            }
        }

        public PrimitiveBaseShape()
        {

        }

        //void returns need to change of course
        public void GetMesh()
        {

        }

        public static PrimitiveBaseShape DefaultCube()
        {
            PrimitiveBaseShape primShape = new PrimitiveBaseShape();

            primShape.Scale = new LLVector3(0.5f, 0.5f, 0.5f);
            primShape.PCode = 9;
            primShape.ParentID = 0;
            primShape.PathBegin = 0;
            primShape.PathEnd = 0;
            primShape.PathScaleX = 0;
            primShape.PathScaleY = 0;
            primShape.PathShearX = 0;
            primShape.PathShearY = 0;
            primShape.PathSkew = 0;
            primShape.ProfileBegin = 0;
            primShape.ProfileEnd = 0;
            primShape.PathCurve = 16;
            primShape.ProfileCurve = 1;
            primShape.ProfileHollow = 0;
            primShape.PathRadiusOffset = 0;
            primShape.PathRevolutions = 0;
            primShape.PathTaperX = 0;
            primShape.PathTaperY = 0;
            primShape.PathTwist = 0;
            primShape.PathTwistBegin = 0;

            return primShape;
        }
    }

}
