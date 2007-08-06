using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.Scripting.EmbeddedJVM
{
    public enum OpCode : byte
    {
        iconst_m1 = 2,
        iconst_0 = 3,
        iconst_1 = 4,
        iconst_2 = 5,
        iconst_3 = 6,
        iconst_4 = 7,
        iconst_5 = 8,
        fconst_0 = 11,
        fconst_1 = 12,
        fconst_2 = 13,
        bipush = 16,
        sipush = 17,
        fload = 23,
        iload_0 = 26,
        iload_1 = 27,
        fload_0 = 34,
        fload_1 = 35,
        fload_2 = 36,
        fload_3 = 37,
        istore = 54,
        fstore = 56,
        istore_0 = 59,
        istore_1 = 60,
        istore_2 = 61,
        istore_3 = 62,
        fstore_0 = 67,
        fstore_1 = 68,
        fstore_2 = 69,
        fstore_3 = 70,
        pop = 87,
        fadd = 98,
        fsub = 102,
        imul = 104,
        iinc = 132,
        f2i = 139,
        fcmpl = 149,
        fcmpg = 150,
        ifge = 156,
        ifgt = 157,
        ifle = 158,
        if_icmpge = 162,
        if_icmpgt = 163,
        if_icmple = 164,
        _goto = 167,
        getstatic = 178,
        putstatic = 179
    }
}
