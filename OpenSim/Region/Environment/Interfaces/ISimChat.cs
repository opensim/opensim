using libsecondlife;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface ISimChat
    {
        void SimChat(byte[] message, byte type, int channel, LLVector3 fromPos, string fromName, LLUUID fromAgentID);
    }
}