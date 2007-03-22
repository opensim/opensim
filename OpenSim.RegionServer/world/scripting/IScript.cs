using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.world.scripting
{
    public interface IScriptHost {
        bool Register(IScript iscript);
    }
    public interface IScript
    {
        string Name{get;set;}
        IScriptHost Host{get;set;}
        void Show();
    }
}
