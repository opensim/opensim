using System;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class AgentUpdateArgs : EventArgs
    {
        public UUID AgentID;
        public Quaternion BodyRotation;
        public Vector3 CameraAtAxis;
        public Vector3 CameraCenter;
        public Vector3 CameraLeftAxis;
        public Vector3 CameraUpAxis;
        public uint ControlFlags;
        public float Far;
        public byte Flags;
        public Quaternion HeadRotation;
        public UUID SessionID;
        public byte State;
    }
}
