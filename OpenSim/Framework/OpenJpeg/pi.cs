using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.OpenJpeg
{
    public static class pi
    {
    }

    public struct opj_pi_resolution
    {
        public int pdx, pdy;
        public int pw, ph;
    }

    public struct opj_pi_comp
    {
        public int dx, dy;
        public int numresolutions;
        public opj_pi_resolution resolutions;
    }

    public struct obj_pi_iterator
    {
        public sbyte tp_on;
        public short include;
        public int step_l;
        public int step_r;
        public int step_c;
        public int step_p;
        public int compno;
        public int resno;
        public int precno;
        public int layno;
        public int first;
        public opj_poc poc;
        public int numcomps;
        public opj_pi_comp comps;

        public int tx0, ty0, tx1, ty1;
        public int x, y, dx, dy;
    }




}
