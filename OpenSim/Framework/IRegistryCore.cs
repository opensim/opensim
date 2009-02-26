using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    public interface IRegistryCore
    {
        T Get<T>();
        void RegisterInterface<T>(T iface);
        bool TryGet<T>(out T iface);
    }
}
