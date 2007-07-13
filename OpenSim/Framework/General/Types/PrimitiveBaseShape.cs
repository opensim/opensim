using libsecondlife;
using libsecondlife.Packets;

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
        Cylinder,
        Foliage,
        Unknown
    }

    public class PrimitiveBaseShape
    {
        private ShapeType type = ShapeType.Unknown;

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

        public static PrimitiveBaseShape DefaultBox()
        {
            PrimitiveBaseShape primShape = new PrimitiveBaseShape();

            primShape.type = ShapeType.Box;
            primShape.Scale = new LLVector3(0.5f, 0.5f, 0.5f);
            primShape.PCode = 9;
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

        public static PrimitiveBaseShape FromAddPacket(ObjectAddPacket addPacket)
        {
            PrimitiveBaseShape pShape = new PrimitiveBaseShape();

            pShape.PCode = addPacket.ObjectData.PCode;
            pShape.PathBegin = addPacket.ObjectData.PathBegin;
            pShape.PathEnd = addPacket.ObjectData.PathEnd;
            pShape.PathScaleX = addPacket.ObjectData.PathScaleX;
            pShape.PathScaleY = addPacket.ObjectData.PathScaleY;
            pShape.PathShearX = addPacket.ObjectData.PathShearX;
            pShape.PathShearY = addPacket.ObjectData.PathShearY;
            pShape.PathSkew = addPacket.ObjectData.PathSkew;
            pShape.ProfileBegin = addPacket.ObjectData.ProfileBegin;
            pShape.ProfileEnd = addPacket.ObjectData.ProfileEnd;
            pShape.Scale = addPacket.ObjectData.Scale;
            pShape.PathCurve = addPacket.ObjectData.PathCurve;
            pShape.ProfileCurve = addPacket.ObjectData.ProfileCurve;
            pShape.ProfileHollow = addPacket.ObjectData.ProfileHollow;
            pShape.PathRadiusOffset = addPacket.ObjectData.PathRadiusOffset;
            pShape.PathRevolutions = addPacket.ObjectData.PathRevolutions;
            pShape.PathTaperX = addPacket.ObjectData.PathTaperX;
            pShape.PathTaperY = addPacket.ObjectData.PathTaperY;
            pShape.PathTwist = addPacket.ObjectData.PathTwist;
            pShape.PathTwistBegin = addPacket.ObjectData.PathTwistBegin;
            LLObject.TextureEntry ntex = new LLObject.TextureEntry(new LLUUID("00000000-0000-0000-9999-000000000005"));
            pShape.TextureEntry = ntex.ToBytes();
            return pShape;
        }
    }
}
