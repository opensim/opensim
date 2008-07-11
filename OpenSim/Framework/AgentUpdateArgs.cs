using System;
using libsecondlife;

namespace OpenSim.Framework
{
    public class AgentUpdateArgs : EventArgs
    {
        public LLUUID AgentID;
        public LLQuaternion BodyRotation;
        public LLVector3 CameraAtAxis;
        public LLVector3 CameraCenter;
        public LLVector3 CameraLeftAxis;
        public LLVector3 CameraUpAxis;
        public uint ControlFlags;
        public float Far;
        public byte Flags;
        public LLQuaternion HeadRotation;
        public LLUUID SessionID;
        public byte State;
    }
}