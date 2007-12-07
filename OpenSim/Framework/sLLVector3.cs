using System;
using libsecondlife;


namespace OpenSim.Framework
{
    [Serializable]
    public class sLLVector3
    {
        public sLLVector3()
        {

        }
        public sLLVector3(LLVector3 v)
        {
            x = v.X;
            y = v.Y;
            z = v.Z;
        }
        public float x;
        public float y;
        public float z;
    }

}
