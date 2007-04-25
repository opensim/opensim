using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Interfaces
{
    public interface IScriptEngine
    {
        bool Init(IScriptAPI api);
        string GetName();
        void LoadScript(string script, string scriptName, uint entityID);
        void OnFrame();
    }
}
