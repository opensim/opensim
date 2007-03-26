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
        private static uint nextXferID = 5000;
        private static object XferLock = new object();

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

        public static uint GetNextXferID()
        {
            uint id = 0;
            lock(XferLock)
            {
                id = nextXferID;
                nextXferID++;
            }
            return id;
        }

        public Util()
        {

        }
    }

}
