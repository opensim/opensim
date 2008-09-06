using System;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class ViewerEffectEventHandlerArg : EventArgs
    {
        public UUID AgentID;
        public byte[] Color;
        public float Duration;
        public UUID ID;
        public byte Type;
        public byte[] TypeData;
    }
}
