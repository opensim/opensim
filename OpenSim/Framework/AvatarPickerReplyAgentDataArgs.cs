using System;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public class AvatarPickerReplyAgentDataArgs : EventArgs
    {
        public UUID AgentID;
        public UUID QueryID;
    }
}
