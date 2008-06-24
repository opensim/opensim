using System;
using System.Collections;
using System.Collections.Generic;
using OpenSim.Region.ScriptEngine.Interfaces;

namespace OpenSim.Region.ScriptEngine.Shared.ScriptBase
{
    public interface IScript
    {
        string[] GetApis();
        void InitApi(string name, IScriptApi data);

        Dictionary<string,Object> GetVars();
        void SetVars(Dictionary<string,Object> vars);
        void ResetVars();
    }
}
