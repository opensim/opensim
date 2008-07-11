using System;
using libsecondlife;

namespace OpenSim.Framework
{
    public class AvatarPickerReplyAgentDataArgs : EventArgs
    {
        public LLUUID AgentID;
        public LLUUID QueryID;
    }
}