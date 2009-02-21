using System;
using OpenSim.Framework.Servers;

namespace OpenSim.Grid.GridServer
{
    public interface IGridCore
    {
        T Get<T>();
        void RegisterInterface<T>(T iface);
        bool TryGet<T>(out T iface);
        BaseHttpServer GetHttpServer();
    }
}
