using libsecondlife;
using OpenSim.Region.Environment.Modules;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface IXMLRPC
    {
        LLUUID OpenXMLRPCChannel(uint localID, LLUUID itemID);
        void CloseXMLRPCChannel(LLUUID channelKey);
        bool hasRequests();
        RPCRequestInfo GetNextRequest();
        void RemoteDataReply(string channel, string message_id, string sdata, int idata);
    }
}