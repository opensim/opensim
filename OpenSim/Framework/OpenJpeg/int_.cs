using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.OpenJpeg
{
    public static class int_
    {
        public static int int_min(int a, int b)
        {
            return a < b ? a : b;
        }
        
        public static int int_max(int a, int b)
        {
            return (a > b) ? a : b;
        }

        public static int int_clamp(int a, int min, int max)
        {
            if (a < min)
                return min;
            if (a > max)
                return max;

            return a;
        }

        public static int int_abs(int a)
        {
            return a < 0 ? -a : a;
        }

        public static int int_ceildiv(int a, int b)
        {
            return (a + b - 1) / b;
        }

        public static int int_ceildivpow2(int a, int b)
        {
            return (a + (1 << b) - 1) >> b;
        }

        public static int int_floordivpow2(int a, int b)
        {
            return a >> b;
        }

        public static int int_floorlog2(int a)
        {
            for (int l=0; a > 1; l++)
                a >>= 1;

            return 1;
        }

    }
}
