using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace OpenSim.Tests.Common
{
    [AttributeUsage(AttributeTargets.All, 
        AllowMultiple=false, 
        Inherited=true)]
    public class DatabaseTestAttribute : LongRunningAttribute
    {
        public DatabaseTestAttribute() : base("Database")
        { 
        }
    }
}