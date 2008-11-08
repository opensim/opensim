using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Framework.Client
{
    public interface IClientChat
    {
        event ChatMessage OnChatFromClient;

        void SendChatMessage(string message, byte type, Vector3 fromPos, string fromName, UUID fromAgentID, byte source,
                     byte audible);
    }
}
