using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim.Framework.Utilities
{
    public class Util
    {
        private static Random randomClass = new Random();

        public static ulong UIntsToLong(uint X, uint Y)
        {
            return Helpers.UIntsToLong(X, Y);
        }

        public static Random RandomClass
        {
            get
            {
                return randomClass;
            }
        }

        public Util()
        {

        }
    }

}
