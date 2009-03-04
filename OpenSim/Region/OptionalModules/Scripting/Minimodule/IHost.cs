using System;
using System.Collections.Generic;
using System.Text;
using log4net;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public interface IHost
    {
        IObject Object { get; }
        ILog Console { get; }
    }
}
