using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface ISimChat
    {
        void SimChat(byte[] message, byte type, LLVector3 fromPos, string fromName, LLUUID fromAgentID);
    }
}
