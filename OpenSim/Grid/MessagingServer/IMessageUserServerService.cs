using System;
namespace OpenSim.Grid.MessagingServer
{
    public interface IMessageUserServerService
    {
        bool SendToUserServer(System.Collections.Hashtable request, string method);
    }
}
