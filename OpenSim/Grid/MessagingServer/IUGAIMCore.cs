using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Servers;

namespace OpenSim.Grid.MessagingServer
{
    public interface IUGAIMCore
    {
        T Get<T>();
        void RegisterInterface<T>(T iface);
        bool TryGet<T>(out T iface);
        BaseHttpServer GetHttpServer();
    }
}
