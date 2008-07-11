using System;
using libsecondlife;

namespace OpenSim.Framework
{
    public class ViewerEffectEventHandlerArg : EventArgs
    {
        public LLUUID AgentID;
        public byte[] Color;
        public float Duration;
        public LLUUID ID;
        public byte Type;
        public byte[] TypeData;
    }
}