/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.ExtensionsScriptModule.JVMEngine.JVM
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
