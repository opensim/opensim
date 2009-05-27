using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Tests.Common
{
    [AttributeUsage(AttributeTargets.All,
            AllowMultiple = false,
            Inherited = true)]
    public class IntegrationTestAttribute : LongRunningAttribute
    {
        public IntegrationTestAttribute()
            : base("Integration")
        {
        }
    }
}
