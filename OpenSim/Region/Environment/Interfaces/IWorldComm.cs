using libsecondlife;
using OpenSim.Region.Environment.Modules;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface IWorldComm
    {
        int Listen(uint LocalID, LLUUID itemID, LLUUID hostID, int channel, string name, string id, string msg);
        void DeliverMessage(string sourceItemID, int type, int channel, string name, string msg);
        bool HasMessages();
        ListenerInfo GetNextMessage();
        void ListenControl(int handle, int active);
        void ListenRemove(int handle);
    }
}