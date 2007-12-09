using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    [Serializable]
    public class ChildAgentDataUpdate
    {
        public ChildAgentDataUpdate()
        {

        }
        public sLLVector3 Position;
        public ulong regionHandle;
        public float drawdistance;
        public sLLVector3 cameraPosition;
        public sLLVector3 Velocity;
        public float AVHeight;
        public Guid AgentID;
        public float godlevel;
        public byte[] throttles; 
    }
}
