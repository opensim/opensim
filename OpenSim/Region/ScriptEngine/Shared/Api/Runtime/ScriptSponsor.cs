using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;
using System.Text;

namespace OpenSim.Region.ScriptEngine.Shared.Api.Runtime
{
    public class ScriptSponsor: ISponsor
    {
        // In theory: I execute, therefore I am.
        // If GC collects this class then sponsorship will expire
        public TimeSpan Renewal(ILease lease)
        {
            return TimeSpan.FromMinutes(2);
        }
    }
}
