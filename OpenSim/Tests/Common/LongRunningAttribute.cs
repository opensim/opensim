using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace OpenSim.Tests.Common
{
    [AttributeUsage(AttributeTargets.All,
        AllowMultiple = false,
        Inherited = true)]
    public class LongRunningAttribute :  CategoryAttribute 
    {
        public LongRunningAttribute() : this("Long Running Test")
        {
            
        }

        protected LongRunningAttribute(string category) : base(category)
        {            
        }
    }
}
