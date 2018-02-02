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
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using OpenSim.Region.ScriptEngine.Shared.ScriptBase;
using OpenSim.Region.ScriptEngine.XMREngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.XMREngine
{
    public class HeapTrackerBase {
        private int usage;
        private XMRInstAbstract instance;

        public HeapTrackerBase (XMRInstAbstract inst)
        {
            if (inst == null) throw new ArgumentNullException ("inst");
            instance = inst;
        }

        ~HeapTrackerBase ()
        {
            usage = instance.UpdateHeapUse (usage, 0);
        }

        protected void NewUse (int newuse)
        {
            usage = instance.UpdateHeapUse (usage, newuse);
        }
    }

    public class HeapTrackerList : HeapTrackerBase {
        private LSL_List value;

        public HeapTrackerList (XMRInstAbstract inst) : base (inst) { }

        public void Pop (LSL_List lis)
        {
            NewUse (Size (lis));
            value = lis;
        }

        public LSL_List Push ()
        {
            return value;
        }

        public static int Size (LSL_List lis)
        {
            return (!typeof (LSL_List).IsValueType && (lis == null)) ? 0 : lis.Size;
        }
    }

    public class HeapTrackerObject : HeapTrackerBase {
        public const int HT_CHAR = 2;
        public const int HT_DELE = 8;
        public const int HT_DOUB = 8;
        public const int HT_SING = 4;
        public const int HT_SFLT = 4;
        public const int HT_INT  = 4;
        public const int HT_VEC  = HT_DOUB * 3;
        public const int HT_ROT  = HT_DOUB * 4;

        private object value;

        public HeapTrackerObject (XMRInstAbstract inst) : base (inst) { }

        public void Pop (object obj)
        {
            NewUse (Size (obj));
            value = obj;
        }

        public object Push ()
        {
            return value;
        }

        public static int Size (object obj)
        {
            if (obj == null) return 0;

            if (obj is char)            return HT_CHAR;
            if (obj is Delegate)        return HT_DELE;
            if (obj is double)          return HT_DOUB;
            if (obj is float)           return HT_SING;
            if (obj is int)             return HT_INT;
            if (obj is LSL_Float)       return HT_SFLT;
            if (obj is LSL_Integer)     return HT_INT;
            if (obj is LSL_List)        return ((LSL_List)obj).Size;
            if (obj is LSL_Rotation)    return HT_ROT;
            if (obj is LSL_String)      return ((LSL_String)obj).m_string.Length * HT_CHAR;
            if (obj is LSL_Vector)      return HT_VEC;
            if (obj is string)          return ((string)obj).Length * HT_CHAR;
            if (obj is XMR_Array)       return 0;
            if (obj is XMRArrayListKey) return ((XMRArrayListKey)obj).Size;
            if (obj is XMRSDTypeClObj)  return 0;

            if (obj is Array) {
                Array ar = (Array)obj;
                int len = ar.Length;
                if (len == 0) return 0;
                Type et = ar.GetType ().GetElementType ();
                if (et.IsValueType) return Size (ar.GetValue (0)) * len;
                int size = 0;
                for (int i = 0; i < len; i ++) {
                    size += Size (ar.GetValue (i));
                }
                return size;
            }

            throw new Exception ("unknown size of type " + obj.GetType ().Name);
        }
    }

    public class HeapTrackerString : HeapTrackerBase {
        private string value;

        public HeapTrackerString (XMRInstAbstract inst) : base (inst) { }

        public void Pop (string str)
        {
            NewUse (Size (str));
            value = str;
        }

        public string Push ()
        {
            return value;
        }

        public static int Size (string str)
        {
            return (str == null) ? 0 : str.Length * HeapTrackerObject.HT_CHAR;
        }
    }
}
