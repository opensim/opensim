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

using System;
using System.Reflection;
using System.Reflection.Emit;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

namespace OpenSim.Region.ScriptEngine.Yengine
{
    /**
     * One instance of this class for lsl base objects that take a variable
     * amount of memory.  They are what the script-visible list,object,string
     * variables are declared as at the CIL level.  Generally, temp vars used
     * by the compiler get their basic type (list,object,string).
     *
     * Note that the xmr arrays and script-defined objects have their own
     * heap tracking built in so do not need any of this stuff.
     */
    public class HeapTrackerBase
    {
        protected int usage;                    // num bytes used by object
        protected XMRInstAbstract instance;     // what script it is in

        public HeapTrackerBase(XMRInstAbstract inst)
        {
            if(inst == null)
                throw new ArgumentNullException("inst");
            instance = inst;
            usage = 0;
        }
    }

    /**
     * Wrapper around lists to keep track of how much memory they use.
     */
    public class HeapTrackerList: HeapTrackerBase
    {
        private static FieldInfo listValueField = typeof(HeapTrackerList).GetField("value");
        private static MethodInfo listSaveMethod = typeof(HeapTrackerList).GetMethod("Save");
        private static MethodInfo listRestoreMethod = typeof(HeapTrackerList).GetMethod("Restore");
        private static MethodInfo listFreeMethod = typeof(HeapTrackerList).GetMethod("Free");

        public LSL_List value;

        public HeapTrackerList(XMRInstAbstract inst) : base(inst) {}

        // generate CIL code to pop the value ie store in value
        //  input:
        //   'this' pointer already pushed on CIL stack
        //   new value
        //  output:
        public static void GenPop(Token errorAt, ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, OpCodes.Call, listSaveMethod);
        }

        public static void GenRestore(Token errorAt, ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, OpCodes.Call, listRestoreMethod);
        }

        public static void GenFree(Token errorAt, ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, OpCodes.Call, listFreeMethod);
        }

        // generate CIL code to push the value on the CIL stack
        //  input:
        //   'this' pointer already pushed on CIL stack
        //  output:
        //   'this' pointer popped from stack
        //   value pushed on CIL stack replacing 'this' pointer
        //   returns typeof value pushed on stack
        public static Type GenPush(Token errorAt, ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, OpCodes.Ldfld, listValueField);
            return typeof(LSL_List);
        }

        public void Save(LSL_List lis)
        {
            if (lis == null)
                usage = instance.UpdateHeapUse(usage, 0);
            else
                usage = instance.UpdateHeapUse(usage, Size(lis));
            value = lis;
        }

        public void Restore(LSL_List lis)
        {
            value = lis;
            if (lis != null)
                usage = Size(lis);
            else
                usage = 0;
        }

        public void Free()
        {
            usage = instance.UpdateHeapUse(usage, 0);
            value = null;
            instance = null;
        }

        //private static int counter = 5;
        public static int Size(LSL_List lis)
        {
            try
            {
                return lis.Size;
            }
            catch
            {
                return 0;
            }
        }
    }

    /**
     * Wrapper around objects to keep track of how much memory they use.
     */
    public class HeapTrackerObject: HeapTrackerBase
    {
        private static FieldInfo objectValueField = typeof(HeapTrackerObject).GetField("value");
        private static MethodInfo objectSaveMethod = typeof(HeapTrackerObject).GetMethod("Save");
        private static MethodInfo objectRestoreMethod = typeof(HeapTrackerObject).GetMethod("Restore");
        private static MethodInfo objectFreeMethod = typeof(HeapTrackerObject).GetMethod("Free");

        public const int HT_CHAR = 2;
        public const int HT_DELE = 8;
        public const int HT_DOUB = 8;
        public const int HT_SING = 4;
        public const int HT_SFLT = 4;
        public const int HT_INT = 4;
        public const int HT_VEC = HT_DOUB * 3;
        public const int HT_ROT = HT_DOUB * 4;

        public object value;

        public HeapTrackerObject(XMRInstAbstract inst) : base(inst) { }

        // generate CIL code to pop the value from the CIL stack
        //  input:
        //   'this' pointer already pushed on CIL stack
        //   new value pushed on CIL stack
        //  output:
        //   'this' pointer popped from stack
        //   new value popped from CIL stack
        //   heap usage updated
        public static void GenPop(Token errorAt, ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, OpCodes.Call, objectSaveMethod);
        }

        public static void GenRestore(Token errorAt, ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, OpCodes.Call, objectRestoreMethod);
        }

        public static void GenFree(Token errorAt, ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, OpCodes.Call, objectFreeMethod);
        }

        // generate CIL code to push the value on the CIL stack
        //  input:
        //   'this' pointer already pushed on CIL stack
        //  output:
        //   'this' pointer popped from stack
        //   value pushed on CIL stack replacing 'this' pointer
        //   returns typeof value pushed on stack
        public static Type GenPush(Token errorAt, ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, OpCodes.Ldfld, objectValueField);
            return typeof(object);
        }

        public void Save(object obj)
        {
            int newuse = Size(obj);
            usage = instance.UpdateHeapUse(usage, newuse);
            value = obj;
        }

        public void Restore(object obj)
        {
            value = obj;
            usage = Size(obj);
        }

        public void Free()
        {
            usage = instance.UpdateHeapUse(usage, 0);
            value = null;
            instance = null;
        }

        // public so it can be used by XMRArray
        public static int Size(object obj)
        {
            if(obj == null)
                return 0;

            if(obj is char)
                return HT_CHAR;
            if(obj is Delegate)
                return HT_DELE;
            if(obj is double)
                return HT_DOUB;
            if(obj is float)
                return HT_SING;
            if(obj is int)
                return HT_INT;
            if(obj is LSL_Float) // lsl floats are stupid doubles
                return HT_DOUB;
            if(obj is LSL_Integer)
                return HT_INT;
            if(obj is LSL_List)
                return ((LSL_List)obj).Size;
            if(obj is LSL_Rotation)
                return HT_ROT;
            if(obj is LSL_String)
                return ((LSL_String)obj).m_string.Length * HT_CHAR;
            if(obj is LSL_Vector)
                return HT_VEC;
            if(obj is string)
                return ((string)obj).Length * HT_CHAR;
            if(obj is XMR_Array)
                return 0;
            if(obj is XMRArrayListKey)
                return ((XMRArrayListKey)obj).Size;
            if(obj is XMRSDTypeClObj)
                return 0;

            if(obj is Array)
            {
                Array ar = (Array)obj;
                int len = ar.Length;
                if(len == 0)
                    return 0;
                Type et = ar.GetType().GetElementType();
                if(et.IsValueType)
                    return Size(ar.GetValue(0)) * len;
                int size = 0;
                for(int i = 0; i < len; i++)
                {
                    size += Size(ar.GetValue(i));
                }
                return size;
            }

            throw new Exception("unknown size of type " + obj.GetType().Name);
        }
    }

    /**
     * Wrapper around strings to keep track of how much memory they use.
     */
    public class HeapTrackerString: HeapTrackerBase
    {
        private static FieldInfo stringValueField = typeof(HeapTrackerString).GetField("value");
        private static MethodInfo stringRestoreMethod = typeof(HeapTrackerString).GetMethod("Restore");
        private static MethodInfo stringSaveMethod = typeof(HeapTrackerString).GetMethod("Save");
        private static MethodInfo stringFreeMethod = typeof(HeapTrackerString).GetMethod("Free");

        public string value;

        public HeapTrackerString(XMRInstAbstract inst) : base(inst) { }

        // generate CIL code to pop the value from the CIL stack
        //  input:
        //   'this' pointer already pushed on CIL stack
        //   new value pushed on CIL stack
        //  output:
        //   'this' pointer popped from stack
        //   new value popped from CIL stack
        //   heap usage updated
        public static void GenPop(Token errorAt, ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, OpCodes.Call, stringSaveMethod);
        }

        public static void GenRestore(Token errorAt, ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, OpCodes.Call, stringRestoreMethod);
        }
        
        public static void GenFree(Token errorAt, ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, OpCodes.Call, stringFreeMethod);
        }

        // generate CIL code to push the value on the CIL stack
        //  input:
        //   'this' pointer already pushed on CIL stack
        //  output:
        //   'this' pointer popped from stack
        //   value pushed on CIL stack replacing 'this' pointer
        //   returns typeof value pushed on stack
        public static Type GenPush(Token errorAt, ScriptMyILGen ilGen)
        {
            ilGen.Emit(errorAt, OpCodes.Ldfld, stringValueField);
            return typeof(string);
        }

        public void Save(string str)
        {
            int newuse = Size(str);
            usage = instance.UpdateHeapUse(usage, newuse);
            value = str;
        }

        public void Restore(string str)
        {
            value = str;
            usage = Size(str);
        }

        public void Free()
        {
            usage = instance.UpdateHeapUse(usage, 0);
            value = null;
            instance = null;
        }

        public static int Size(string str)
        {
            return (str == null) ? 0 : str.Length * HeapTrackerObject.HT_CHAR;
        }
    }
}
