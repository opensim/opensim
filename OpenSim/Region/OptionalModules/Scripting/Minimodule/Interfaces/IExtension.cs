using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule.Interfaces
{
    public interface IExtension
    {
        T Get<T>();
        bool TryGet<T>(out T extension);
        bool Has<T>();
    }
}
