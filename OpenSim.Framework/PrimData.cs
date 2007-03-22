using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Framework.Assets
{
    public class PrimData
    {
        public LLUUID OwnerID;
        public byte PCode;
        public byte PathBegin;
        public byte PathEnd;
        public byte PathScaleX;
        public byte PathScaleY;
        public byte PathShearX;
        public byte PathShearY;
        public sbyte PathSkew;
        public byte ProfileBegin;
        public byte ProfileEnd;
        public LLVector3 Scale;
        public byte PathCurve;
        public byte ProfileCurve;
        public uint ParentID = 0;
        public byte ProfileHollow;
        public sbyte PathRadiusOffset;
        public byte PathRevolutions;
        public sbyte PathTaperX;
        public sbyte PathTaperY;
        public sbyte PathTwist;
        public sbyte PathTwistBegin;
        public byte[] Texture;

        //following only used during prim storage
        public LLVector3 Position;
        public LLQuaternion Rotation;
        public uint LocalID;
        public LLUUID FullID;

        public PrimData()
        {

        }
    }
}
