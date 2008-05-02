using System.Net.Sockets;
using OpenSim.Framework;

namespace OpenSim.Region.ClientStack
{
    public interface IClientNetworkServer
    {
        Socket Server { get; }
        bool HandlesRegion(Location x);
    }
}