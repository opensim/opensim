using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Region.Environment.Modules.ModuleFramework;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface ICommandableModule
    {
        ICommander CommandInterface
        {
            get;
        }
    }
}
