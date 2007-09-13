using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.ScriptEngine.Common
{
    public interface IScript
    {
        string State();
        Executor Exec { get; }
    }
}
