using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule.Interfaces
{
    public interface IMicrothreader
    {
        void Run(IEnumerable microthread);
    }
}
