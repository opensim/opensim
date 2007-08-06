using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.Framework.Types
{
    //public enum ShapeType
    //{
    //    Box,
    //    Sphere,
    //    Ring,
    //    Tube,
    //    Torus,
    //    Prism,
    //    Scuplted,
    //    Cylinder,
    //    Foliage,
    //    Unknown
    //}

    public class PrimitiveBaseShape
    {
        //protected ShapeType m_type = ShapeType.Unknown;


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

        //public ShapeType PrimType
        //{
        //    get
        //    {
        //        return this.m_type;
        //    }
        //}

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

    public class BoxShape : PrimitiveBaseShape
    {
        public BoxShape() : base()
        {
            //m_type = ShapeType.Box;
            PathCurve = 16;
            ProfileCurve = 1;
            PCode = 9;
            PathScaleX = 100;
            PathScaleY = 100;
        }

        public static BoxShape Default
        {
            get
            {
                BoxShape boxShape = new BoxShape();

                boxShape.Scale = new LLVector3(0.5f, 0.5f, 0.5f);
                
                return boxShape;
            }
        }
    }
}
