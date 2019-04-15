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
using System.Collections.Generic;
using System.IO;
using System.Text;

using LSL_Float = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLFloat;
using LSL_Integer = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;
using LSL_Key = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_List = OpenSim.Region.ScriptEngine.Shared.LSL_Types.list;
using LSL_Rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSL_String = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLString;
using LSL_Vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;

// This class exists in the main app domain
//
namespace OpenSim.Region.ScriptEngine.Yengine
{
    /**
     * @brief Array objects.
     */
    public class XMR_Array
    {
        private const int EMPTYHEAP = 64;
        private const int ENTRYHEAP = 24;

        private bool enumrValid;                              // true: enumr set to return array[arrayValid]
                                                              // false: array[0..arrayValid-1] is all there is
        private SortedDictionary<object, object> dnary;
        private SortedDictionary<object, object>.Enumerator enumr;
        // enumerator used to fill 'array' past arrayValid to end of dictionary
        private int arrayValid;                               // number of elements in 'array' that have been filled in
        private KeyValuePair<object, object>[] array;         // list of kvp's that have been returned by ForEach() since last modification
        private XMRInstAbstract inst;                         // script instance debited with heap use
        private int heapUse;                                  // current heap use debit amount

        public static TokenTypeSDTypeDelegate countDelegate = new TokenTypeSDTypeDelegate(new TokenTypeInt(null), new TokenType[0]);
        public static TokenTypeSDTypeDelegate clearDelegate = new TokenTypeSDTypeDelegate(new TokenTypeVoid(null), new TokenType[0]);
        public static TokenTypeSDTypeDelegate indexDelegate = new TokenTypeSDTypeDelegate(new TokenTypeObject(null), new TokenType[] { new TokenTypeInt(null) });
        public static TokenTypeSDTypeDelegate valueDelegate = new TokenTypeSDTypeDelegate(new TokenTypeObject(null), new TokenType[] { new TokenTypeInt(null) });

        public XMR_Array(XMRInstAbstract inst)
        {
            this.inst = inst;
            dnary = new SortedDictionary<object, object>(XMRArrayKeyComparer.singleton);
            heapUse = inst.UpdateHeapUse(0, EMPTYHEAP);
        }

        ~XMR_Array()
        {
            heapUse = inst.UpdateHeapUse(heapUse, 0);
        }

        public static TokenType GetRValType(TokenName name)
        {
            if(name.val == "count")
                return new TokenTypeInt(name);
            if(name.val == "clear")
                return clearDelegate;
            if(name.val == "index")
                return indexDelegate;
            if(name.val == "value")
                return valueDelegate;
            return new TokenTypeVoid(name);
        }

        /**
         * @brief Handle 'array[index]' syntax to get or set an element of the dictionary.
         * Get returns null if element not defined, script sees type 'undef'.
         * Setting an element to null removes it.
         */
        public object GetByKey(object key)
        {
            object val;
            key = FixKey(key);
            if(!dnary.TryGetValue(key, out val))
                val = null;
            return val;
        }

        public void SetByKey(object key, object value)
        {
            key = FixKey(key);

             // Update heap use throwing an exception on failure
             // before making any changes to the array.
            int keysize = HeapTrackerObject.Size(key);
            int newheapuse = heapUse;
            object oldval;
            if(dnary.TryGetValue(key, out oldval))
            {
                newheapuse -= keysize + HeapTrackerObject.Size(oldval);
            }
            if(value != null)
            {
                newheapuse += keysize + HeapTrackerObject.Size(value);
            }
            heapUse = inst.UpdateHeapUse(heapUse, newheapuse);

             // Save new value in array, replacing one of same key if there.
             // null means remove the value, ie, script did array[key] = undef.
            if (value != null)
            {
                dnary[key] = value;
            }
            else
            {
                dnary.Remove(key);

                 // Shrink the enumeration array, but always leave at least one element.
                if((array != null) && (dnary.Count < array.Length / 2))
                {
                    Array.Resize<KeyValuePair<object, object>>(ref array, array.Length / 2);
                }
            }

             // The enumeration array is invalid because the dictionary has been modified.
             // Next time a ForEach() call happens, it will repopulate 'array' as elements are retrieved.
            arrayValid = 0;
        }

        /**
         * @brief Converts an 'object' type to array, key, list, string, but disallows null,
         *        as our language doesn't allow types other than 'object' to be null.
         *        Value types (float, rotation, etc) don't need explicit check for null as
         *        the C# runtime can't convert a null to a value type, and throws an exception.
         *        But for any reference type (array, key, etc) we must manually check for null.
         */
        public static XMR_Array Obj2Array(object obj)
        {
            if(obj == null)
                throw new NullReferenceException();
            return (XMR_Array)obj;
        }
        public static LSL_Key Obj2Key(object obj)
        {
            if(obj == null)
                throw new NullReferenceException();
            return (LSL_Key)obj;
        }
        public static LSL_List Obj2List(object obj)
        {
            if(obj == null)
                throw new NullReferenceException();
            return (LSL_List)obj;
        }
        public static LSL_String Obj2String(object obj)
        {
            if(obj == null)
                throw new NullReferenceException();
            return obj.ToString();
        }

        /**
         * @brief remove all elements from the array.
         *        sets everything to its 'just constructed' state.
         */
        public void __pub_clear()
        {
            heapUse = inst.UpdateHeapUse(heapUse, EMPTYHEAP);
            dnary.Clear();
            enumrValid = false;
            arrayValid = 0;
            array = null;
        }

        /**
         * @brief return number of elements in the array.
         */
        public int __pub_count()
        {
            return dnary.Count;
        }

        /**
         * @brief Retrieve index (key) of an arbitrary element.
         * @param number = number of the element (0 based)
         * @returns null: array doesn't have that many elements
         *          else: index (key) for that element
         */
        public object __pub_index(int number)
        {
            return ForEach(number) ? UnfixKey(array[number].Key) : null;
        }

        /**
         * @brief Retrieve value of an arbitrary element.
         * @param number = number of the element (0 based)
         * @returns null: array doesn't have that many elements
         *          else: value for that element
         */
        public object __pub_value(int number)
        {
            return ForEach(number) ? array[number].Value : null;
        }

        /**
         * @brief Called in each iteration of a 'foreach' statement.
         * @param number = index of element to retrieve (0 = first one)
         * @returns false: element does not exist
         *           true: element exists
         */
        private bool ForEach(int number)
        {
             // If we don't have any array, we can't have ever done
             // any calls here before, so allocate an array big enough
             // and set everything else to the beginning.
            if(array == null)
            {
                array = new KeyValuePair<object, object>[dnary.Count];
                arrayValid = 0;
            }

             // If dictionary modified since last enumeration, get a new enumerator.
            if(arrayValid == 0)
            {
                enumr = dnary.GetEnumerator();
                enumrValid = true;
            }

             // Make sure we have filled the array up enough for requested element.
            while((arrayValid <= number) && enumrValid && enumr.MoveNext())
            {
                if(arrayValid >= array.Length)
                {
                    Array.Resize<KeyValuePair<object, object>>(ref array, dnary.Count);
                }
                array[arrayValid++] = enumr.Current;
            }

             // If we don't have that many elements, return end-of-array status.
            return number < arrayValid;
        }

        /**
         * @brief Transmit array out in such a way that it can be reconstructed,
         *        including any in-progress ForEach() enumerations.
         */
        public delegate void SendArrayObjDelegate(object graph);
        public void SendArrayObj(SendArrayObjDelegate sendObj)
        {
             // Set the count then the elements themselves.
             // UnfixKey() because sendObj doesn't handle XMRArrayListKeys.
            sendObj(dnary.Count);
            foreach(KeyValuePair<object, object> kvp in dnary)
            {
                sendObj(UnfixKey(kvp.Key));
                sendObj(kvp.Value);
            }
        }

        /**
         * @brief Receive array in.  Any previous contents are erased.
         *        Set up such that any enumeration in progress will resume
         *        at the exact spot and in the exact same order as they
         *        were in on the sending side.
         */
        public delegate object RecvArrayObjDelegate();
        public void RecvArrayObj(RecvArrayObjDelegate recvObj)
        {
            heapUse = inst.UpdateHeapUse(heapUse, EMPTYHEAP);
            // Cause any enumeration to refill the array from the sorted dictionary.
            // Since it is a sorted dictionary, any enumerations will be in the same 
            // order as on the sending side.
            arrayValid = 0;
            enumrValid = false;

             // Fill dictionary.
            dnary.Clear();
            int count = (int)recvObj();
            while(--count >= 0)
            {
                object key = FixKey(recvObj());
                object val = recvObj();
                int htuse = HeapTrackerObject.Size(key) + HeapTrackerObject.Size(val);
                heapUse = inst.UpdateHeapUse(heapUse, heapUse + htuse);
                dnary.Add(key, val);
            }
        }

        /**
         * We want our index values to be of consistent type, otherwise we get things like (LSL_Integer)1 != (int)1.
         * So strip off any LSL-ness from the types.
         * We also deep-strip any given lists used as keys (multi-dimensional arrays).
         */
        public static object FixKey(object key)
        {
            if(key is LSL_Integer)
                return (int)(LSL_Integer)key;
            if(key is LSL_Float)
                return (double)(LSL_Float)key;
            if(key is LSL_Key)
                return (string)(LSL_Key)key;
            if(key is LSL_String)
                return (string)(LSL_String)key;
            if(key is LSL_List)
            {
                object[] data = ((LSL_List)key).Data;
                if(data.Length == 1)
                    return FixKey(data[0]);
                return new XMRArrayListKey((LSL_List)key);
            }
            return key;  // int, double, string, LSL_Vector, LSL_Rotation, etc are ok as is
        }

        /**
         * @brief When returning a key, such as for array.index(), we want to return the original
         *        LSL_List, not the sanitized one, as the script compiler expects an LSL_List.
         *        Any other sanitized types can remain as is (int, string, etc).
         */
        private static object UnfixKey(object key)
        {
            if(key is XMRArrayListKey)
                key = ((XMRArrayListKey)key).GetOriginal();
            return key;
        }
    }

    public class XMRArrayKeyComparer: IComparer<object>
    {

        public static XMRArrayKeyComparer singleton = new XMRArrayKeyComparer();

        /**
         * @brief Compare two keys
         */
        public int Compare(object x, object y)  // IComparer<object>
        {
             // Use short type name (eg, String, Int32, XMRArrayListKey) as most significant part of key.
            string xtn = x.GetType().Name;
            string ytn = y.GetType().Name;
            int ctn = String.CompareOrdinal(xtn, ytn);
            if(ctn != 0)
                return ctn;

            ComparerDelegate cd;
            if(!comparers.TryGetValue(xtn, out cd))
            {
                throw new Exception("unsupported key type " + xtn);
            }
            return cd(x, y);
        }

        private delegate int ComparerDelegate(object a, object b);

        private static Dictionary<string, ComparerDelegate> comparers = BuildComparers();

        private static Dictionary<string, ComparerDelegate> BuildComparers()
        {
            Dictionary<string, ComparerDelegate> cmps = new Dictionary<string, ComparerDelegate>();
            cmps.Add(typeof(double).Name, MyFloatComparer);
            cmps.Add(typeof(int).Name, MyIntComparer);
            cmps.Add(typeof(XMRArrayListKey).Name, MyListKeyComparer);
            cmps.Add(typeof(LSL_Rotation).Name, MyRotationComparer);
            cmps.Add(typeof(string).Name, MyStringComparer);
            cmps.Add(typeof(LSL_Vector).Name, MyVectorComparer);
            return cmps;
        }

        private static int MyFloatComparer(object a, object b)
        {
            double af = (double)a;
            double bf = (double)b;
            if(af < bf)
                return -1;
            if(af > bf)
                return 1;
            return 0;
        }
        private static int MyIntComparer(object a, object b)
        {
            return (int)a - (int)b;
        }
        private static int MyListKeyComparer(object a, object b)
        {
            XMRArrayListKey alk = (XMRArrayListKey)a;
            XMRArrayListKey blk = (XMRArrayListKey)b;
            return XMRArrayListKey.Compare(alk, blk);
        }
        private static int MyRotationComparer(object a, object b)
        {
            LSL_Rotation ar = (LSL_Rotation)a;
            LSL_Rotation br = (LSL_Rotation)b;
            if(ar.x < br.x)
                return -1;
            if(ar.x > br.x)
                return 1;
            if(ar.y < br.y)
                return -1;
            if(ar.y > br.y)
                return 1;
            if(ar.z < br.z)
                return -1;
            if(ar.z > br.z)
                return 1;
            if(ar.s < br.s)
                return -1;
            if(ar.s > br.s)
                return 1;
            return 0;
        }
        private static int MyStringComparer(object a, object b)
        {
            return String.CompareOrdinal((string)a, (string)b);
        }
        private static int MyVectorComparer(object a, object b)
        {
            LSL_Vector av = (LSL_Vector)a;
            LSL_Vector bv = (LSL_Vector)b;
            if(av.x < bv.x)
                return -1;
            if(av.x > bv.x)
                return 1;
            if(av.y < bv.y)
                return -1;
            if(av.y > bv.y)
                return 1;
            if(av.z < bv.z)
                return -1;
            if(av.z > bv.z)
                return 1;
            return 0;
        }
    }

    /**
     * @brief Lists used as keys must be sanitized first.
     *        List gets converted to an object[] and each element is converted from LSL_ types to system types where possible.
     *        And we also need an equality operator that compares the values of all elements of the list, not just the lengths.
     *        Note that just like LSL_Lists, we consider these objects to be immutable, so they can be directly used as keys in
     *        the dictionary as they don't ever change.
     */
    public class XMRArrayListKey
    {
        private LSL_List original;
        private object[] cleaned;
        private int length;
        private int hashCode;

        /**
         * @brief Construct a sanitized object[] from a list.
         *        Also save the original list in case we need it later.
         */
        public XMRArrayListKey(LSL_List key)
        {
            original = key;
            object[] given = key.Data;
            int len = given.Length;
            length = len;
            cleaned = new object[len];
            int hc = len;
            for(int i = 0; i < len; i++)
            {
                object v = XMR_Array.FixKey(given[i]);
                hc += hc + ((hc < 0) ? 1 : 0);
                hc ^= v.GetHashCode();
                cleaned[i] = v;
            }
            hashCode = hc;
        }

        /**
         * @brief Get heap tracking size.
         */
        public int Size
        {
            get
            {
                return original.Size;
            }
        }

        /**
         * @brief See if the given object is an XMRArrayListKey and every value is equal to our own.
         */
        public override bool Equals(object o)
        {
            if(!(o is XMRArrayListKey))
                return false;
            XMRArrayListKey a = (XMRArrayListKey)o;
            int len = a.length;
            if(len != length)
                return false;
            if(a.hashCode != hashCode)
                return false;
            for(int i = 0; i < len; i++)
            {
                if(!cleaned[i].Equals(a.cleaned[i]))
                    return false;
            }
            return true;
        }

        /**
         * @brief Get an hash code.
         */
        public override int GetHashCode()
        {
            return hashCode;
        }

        /**
         * @brief Compare for key sorting.
         */
        public static int Compare(XMRArrayListKey x, XMRArrayListKey y)
        {
            int j = x.length - y.length;
            if(j == 0)
            {
                for(int i = 0; i < x.length; i++)
                {
                    object xo = x.cleaned[i];
                    object yo = y.cleaned[i];
                    j = XMRArrayKeyComparer.singleton.Compare(xo, yo);
                    if(j != 0)
                        break;
                }
            }
            return j;
        }

        /**
         * @brief Get the original LSL_List we were built from.
         */
        public LSL_List GetOriginal()
        {
            return original;
        }

        /**
         * @brief Debugging
         */
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            for(int i = 0; i < length; i++)
            {
                if(i > 0)
                    sb.Append(',');
                sb.Append(cleaned[i].ToString());
            }
            return sb.ToString();
        }
    }
}
