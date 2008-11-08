using System;
using System.Collections.Generic;
using System.Text;

namespace ScriptAssemblies
{
    public interface IScript
    {
        void ExecuteFunction(string functionName, params object[] args);
    }
}