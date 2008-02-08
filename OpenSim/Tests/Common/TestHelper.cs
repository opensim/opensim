using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Tests.Common
{
    public delegate void TestDelegate();

    public class TestHelper
    {
        public static bool AssertThisDelegateCausesArgumentException(TestDelegate d)
        {
            try
            {
                d();
            }
            catch(ArgumentException e)
            {
                return true;
            }

            return false;
        }
    }
}
