using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.OpenJpeg
{
    public static class fix
    {
        public static int fix_mul(int a, int b)
        {
            long temp = (long)a * (long)b;
            temp += temp & 4096;
            return (int)(temp >> 13);
        }
    }
}
