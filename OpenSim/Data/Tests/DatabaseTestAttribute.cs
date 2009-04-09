using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace OpenSim.Data.Tests
{   
    [AttributeUsage(AttributeTargets.All, 
                   AllowMultiple=false, 
                   Inherited=true)]
    public class DatabaseTestAttribute : CategoryAttribute
    {
        public DatabaseTestAttribute() : base("Database")
        { 
        }
    }
}
