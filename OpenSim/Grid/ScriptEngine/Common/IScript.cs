using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Grid.ScriptEngine.Common
{
    public interface IScript
    {
        string State();
        Executor Exec { get; }
    }
}
