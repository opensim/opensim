using System.Net.Sockets;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.ClientStack
{
    public interface IClientNetworkServer
    {
        Socket Server { get; }
        bool HandlesRegion(Location x);
        void AddScene(Scene x);

        void Start();
        void Stop();
    }
}