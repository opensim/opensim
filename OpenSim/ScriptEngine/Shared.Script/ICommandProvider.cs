using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptAssemblies
{
    public interface ICommandProvider
    {
        void ExecuteCommand(string functionName, params object[] args);
        string Name { get; }
    }
}
