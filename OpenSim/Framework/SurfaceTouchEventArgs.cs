using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class SurfaceTouchEventArgs
    {
        public Vector3 Binormal;
        public int FaceIndex;
        public Vector3 Normal;
        public Vector3 Position;
        public Vector3 STCoord;
        public Vector3 UVCoord;
    }
}
