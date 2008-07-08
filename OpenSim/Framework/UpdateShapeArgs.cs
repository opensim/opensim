using System;

namespace OpenSim.Framework
{
    public class UpdateShapeArgs : EventArgs
    {
        public uint ObjectLocalID;
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
        public ushort ProfileBegin;
        public byte ProfileCurve;
        public ushort ProfileEnd;
        public ushort ProfileHollow;
    }
}