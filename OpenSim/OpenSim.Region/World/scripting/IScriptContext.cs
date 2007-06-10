using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;

namespace OpenSim.Region.Scripting
{
    public interface IScriptContext
    {
        IScriptEntity Entity { get; }
        bool TryGetRandomAvatar(out IScriptReadonlyEntity avatar);
    }
}
