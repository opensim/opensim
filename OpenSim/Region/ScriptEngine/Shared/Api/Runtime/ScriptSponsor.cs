using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Lifetime;
using System.Text;

namespace OpenSim.Region.ScriptEngine.Shared.Api.Runtime
{
    [Serializable]
    public class ScriptSponsor : MarshalByRefObject, ISponsor
    {
        // In theory: I execute, therefore I am.
        // If GC collects this class then sponsorship will expire
        public TimeSpan Renewal(ILease lease)
        {
            return TimeSpan.FromMinutes(2);
        }
    }
}
