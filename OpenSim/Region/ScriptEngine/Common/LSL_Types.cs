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
using System.Text.RegularExpressions;

namespace OpenSim.Region.ScriptEngine.Common
{
    [Serializable]
    public partial class LSL_Types
    {

        // Types are kept is separate .dll to avoid having to add whatever .dll it is in it to script AppDomain

        [Serializable]
        public struct Vector3
        {
            public double x;
            public double y;
            public double z;

            #region Constructors

            public Vector3(Vector3 vector)
            {
                x = (float)vector.x;
                y = (float)vector.y;
                z = (float)vector.z;
            }

            public Vector3(double X, double Y, double Z)
            {
                x = X;
                y = Y;
                z = Z;
            }

            public Vector3(string str)
            {
                str = str.Replace('<', ' ');
                str = str.Replace('>', ' ');
                string[] tmps = str.Split(new Char[] { ',', '<', '>' });
                bool res;
                res = Double.TryParse(tmps[0], out x);
                res = res & Double.TryParse(tmps[1], out y);
                res = res & Double.TryParse(tmps[2], out z);
            }

            #endregion

            #region Overriders

            public override string ToString()
            {
                return "<" + x.ToString() + ", " + y.ToString() + ", " + z.ToString() + ">";
            }

            public static bool operator ==(Vector3 lhs, Vector3 rhs)
            {
                return (lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z);
            }

            public static bool operator !=(Vector3 lhs, Vector3 rhs)
            {
                return !(lhs == rhs);
            }

            public override int GetHashCode()
            {
                return (x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode());
            }

            public override bool Equals(object o)
            {
                if (!(o is Vector3)) return false;

                Vector3 vector = (Vector3)o;

                return (x == vector.x && x == vector.x && z == vector.z);
            }

            #endregion

            #region Vector & Vector Math

            // Vector-Vector Math
            public static Vector3 operator +(Vector3 lhs, Vector3 rhs)
            {
                return new Vector3(lhs.x + rhs.x, lhs.y + rhs.y, lhs.z + rhs.z);
            }

            public static Vector3 operator -(Vector3 lhs, Vector3 rhs)
            {
                return new Vector3(lhs.x - rhs.x, lhs.y - rhs.y, lhs.z - rhs.z);
            }

            public static Vector3 operator *(Vector3 lhs, Vector3 rhs)
            {
                return new Vector3(lhs.x * rhs.x, lhs.y * rhs.y, lhs.z * rhs.z);
            }

            public static Vector3 operator %(Vector3 v1, Vector3 v2)
            {
                //Cross product
                Vector3 tv;
                tv.x = (v1.y * v2.z) - (v1.z * v2.y);
                tv.y = (v1.z * v2.x) - (v1.x * v2.z);
                tv.z = (v1.x * v2.y) - (v1.y * v2.x);
                return tv;
            }

            #endregion

            #region Vector & Float Math

            // Vector-Float and Float-Vector Math
            public static Vector3 operator *(Vector3 vec, float val)
            {
                return new Vector3(vec.x * val, vec.y * val, vec.z * val);
            }

            public static Vector3 operator *(float val, Vector3 vec)
            {
                return new Vector3(vec.x * val, vec.y * val, vec.z * val);
            }

            public static Vector3 operator /(Vector3 v, float f)
            {
                v.x = v.x / f;
                v.y = v.y / f;
                v.z = v.z / f;
                return v;
            }

            #endregion

            #region Vector & Double Math

            public static Vector3 operator *(Vector3 vec, double val)
            {
                return new Vector3(vec.x * val, vec.y * val, vec.z * val);
            }

            public static Vector3 operator *(double val, Vector3 vec)
            {
                return new Vector3(vec.x * val, vec.y * val, vec.z * val);
            }

            public static Vector3 operator /(Vector3 v, double f)
            {
                v.x = v.x / f;
                v.y = v.y / f;
                v.z = v.z / f;
                return v;
            }

            #endregion

            #region Vector & Rotation Math

            // Vector-Rotation Math
            public static Vector3 operator *(Vector3 v, Quaternion r)
            {
                Quaternion vq = new Quaternion(v.x, v.y, v.z, 0);
                Quaternion nq = new Quaternion(-r.x, -r.y, -r.z, r.s);

                Quaternion result = (r * vq) * nq;

                return new Vector3(result.x, result.y, result.z);
            }

            // I *think* this is how it works....
            public static Vector3 operator /(Vector3 vec, Quaternion quat)
            {
                quat.s = -quat.s;
                Quaternion vq = new Quaternion(vec.x, vec.y, vec.z, 0);
                Quaternion nq = new Quaternion(-quat.x, -quat.y, -quat.z, quat.s);

                Quaternion result = (quat * vq) * nq;

                return new Vector3(result.x, result.y, result.z);
            }

            #endregion

            #region Static Helper Functions

            public static double Dot(Vector3 v1, Vector3 v2)
            {
                return (v1.x * v2.x) + (v1.y * v2.y) + (v1.z * v2.z);
            }

            public static Vector3 Cross(Vector3 v1, Vector3 v2)
            {
                return new Vector3
                    (
                    v1.y * v2.z - v1.z * v2.y,
                    v1.z * v2.x - v1.x * v2.z,
                    v1.x * v2.y - v1.y * v2.x
                    );
            }

            public static float Mag(Vector3 v)
            {
                return (float)Math.Sqrt(v.x * v.y + v.y * v.y + v.z * v.z);
            }

            public static Vector3 Norm(Vector3 vector)
            {
                float mag = Mag(vector);
                return new Vector3(vector.x / mag, vector.y / mag, vector.z / mag);
            }

            #endregion
        }

        [Serializable]
        public struct Quaternion
        {
            public double x;
            public double y;
            public double z;
            public double s;

            #region Constructors

            public Quaternion(Quaternion Quat)
            {
                x = (float)Quat.x;
                y = (float)Quat.y;
                z = (float)Quat.z;
                s = (float)Quat.s;
            }

            public Quaternion(double X, double Y, double Z, double S)
            {
                x = X;
                y = Y;
                z = Z;
                s = S;
            }

            public Quaternion(string str)
            {
                str = str.Replace('<', ' ');
                str = str.Replace('>', ' ');
                string[] tmps = str.Split(new Char[] { ',', '<', '>' });
                bool res;
                res = Double.TryParse(tmps[0], out x);
                res = res & Double.TryParse(tmps[1], out y);
                res = res & Double.TryParse(tmps[2], out z);
                res = res & Double.TryParse(tmps[3], out s);
            }

            #endregion

            #region Overriders

            public override int GetHashCode()
            {
                return (x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode() ^ s.GetHashCode());
            }

            public override bool Equals(object o)
            {
                if (!(o is Quaternion)) return false;

                Quaternion quaternion = (Quaternion)o;

                return x == quaternion.x && y == quaternion.y && z == quaternion.z && s == quaternion.s;
            }

            public override string ToString()
            {
                return "<" + x.ToString() + ", " + y.ToString() + ", " + z.ToString() + ", " + s.ToString() + ">";
            }

            public static bool operator ==(Quaternion lhs, Quaternion rhs)
            {
                // Return true if the fields match:
                return lhs.x == rhs.x && lhs.y == rhs.y && lhs.z == rhs.z && lhs.s == rhs.s;
            }

            public static bool operator !=(Quaternion lhs, Quaternion rhs)
            {
                return !(lhs == rhs);
            }

            #endregion

            public static Quaternion operator *(Quaternion a, Quaternion b)
            {
                Quaternion c;
                c.x = a.s * b.x + a.x * b.s + a.y * b.z - a.z * b.y;
                c.y = a.s * b.y + a.y * b.s + a.z * b.x - a.x * b.z;
                c.z = a.s * b.z + a.z * b.s + a.x * b.y - a.y * b.x;
                c.s = a.s * b.s - a.x * b.x - a.y * b.y - a.z * b.z;
                return c;
            }
        }

        [Serializable]
        public class list
        {
            private object[] m_data;

            public list(params object[] args)
            {
                m_data = new object[args.Length];
                m_data = args;
            }

            public int Length
            {
                get { return m_data.Length; }
            }

            public object[] Data
            {
                get { return m_data; }
            }

            public static list operator +(list a, list b)
            {
                object[] tmp;
                tmp = new object[a.Length + b.Length];
                a.Data.CopyTo(tmp, 0);
                b.Data.CopyTo(tmp, a.Length);
                return new list(tmp);
            }

            public list GetSublist(int start, int end)
            {
                Console.WriteLine("GetSublist(" + start.ToString() + "," + end.ToString() + ")");
                object[] ret;
                // Take care of neg start or end's
                if (start < 0)
                {
                    start = m_data.Length + start;
                }
                if (end < 0)
                {
                    end = m_data.Length + end;
                }

                // Case start <= end
                if (start <= end)
                {
                    if (start >= m_data.Length)
                    {
                        return new list();
                    }
                    if (end >= m_data.Length)
                    {
                        end = m_data.Length - 1;
                    }
                    ret = new object[end - start + 1];
                    Array.Copy(m_data, start, ret, 0, end - start + 1);
                    return new list(ret);
                }
                else
                {
                    if (start >= m_data.Length)
                    {
                        return GetSublist(0, end);
                    }
                    if (end >= m_data.Length)
                    {
                        return new list();
                    }
                    // end < start
                    //ret = new object[m_data.Length - Math.Abs(end - start + 1)];
                    //Array.Copy(m_data, 0, ret, m_data.Length - start, end + 1);
                    //Array.Copy(m_data, start, ret, 0, m_data.Length - start);
                    return GetSublist(0, end) + GetSublist(start, Data.Length - 1);
                    //return new list(ret);
                }
            }

            public string ToPrettyString()
            {
                string output;
                if (m_data.Length == 0)
                {
                    return "[]";
                }
                output = "[";
                foreach (object o in m_data)
                {
                    if (o is System.String)
                    {
                        output = output + "\"" + o + "\", ";
                    }
                    else
                    {
                        output = output + o.ToString() + ", ";
                    }
                }
                output = output.Substring(0, output.Length - 2);
                output = output + "]";
                return output;
            }

            public override string ToString()
            {
                string output;
                output = String.Empty;
                if (m_data.Length == 0)
                {
                    return String.Empty;
                }
                foreach (object o in m_data)
                {
                    output = output + o.ToString();
                }
                return output;

            }

        }

        //
        // BELOW IS WORK IN PROGRESS... IT WILL CHANGE, SO DON'T USE YET! :) 
        //

        public struct StringTest
        {
            // Our own little string
            internal string actualString;
            public static implicit operator bool(StringTest mString)
            {
                if (mString.actualString.Length == 0)
                    return true;
                return false;
            }
            public override string ToString()
            {
                return actualString;
            }

        }

        [Serializable]
        public struct key
        {
            public string value;

            #region Constructors
            public key(string s)
            {
                value = s;
            }

            #endregion

            #region Methods

            static public bool Parse2Key(string s)
            {
                Regex isuuid = new Regex(@"^[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}$", RegexOptions.Compiled);
                if (isuuid.IsMatch(s))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            #endregion

            #region Operators

            static public implicit operator System.Boolean(key k)
            {
                if (k.value.Length == 0)
                {
                    return false;
                }

                if (k.value == "00000000-0000-0000-0000-000000000000")
                {
                    return false;
                }
                Regex isuuid = new Regex(@"^[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}$", RegexOptions.Compiled);
                if (isuuid.IsMatch(k.value))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            static public implicit operator key(string s)
            {
                return new key(s);
            }

            static public implicit operator System.String(key k)
            {
                return k.value;
            }

            public static bool operator ==(key k1, key k2)
            {
                return k1.value == k2.value;
            }
            public static bool operator !=(key k1, key k2)
            {
                return k1.value != k2.value;
            }

            #endregion

            #region Overriders

            public override bool Equals(object o)
            {
                if (o is String)
                {
                    string s = (string)o;
                    return s == this.value;
                }
                if (o is key)
                {
                    key k = (key)o;
                    return this.value == k.value;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return value.GetHashCode();
            }

            #endregion
        }

        [Serializable]
        public struct LSLString
        {
            public string m_string;
            #region Constructors
            public LSLString(string s)
            {
                m_string = s;
            }
            #endregion

            #region Operators
            static public implicit operator System.Boolean(LSLString s)
            {
                if (s.m_string.Length == 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            static public implicit operator System.String(LSLString s)
            {
                return s.m_string;
            }

            static public implicit operator LSLString(string s)
            {
                return new LSLString(s);
            }

            // Commented out:
            /*
                 [echo] Build Directory is /home/tedd/opensim/trunk/OpenSim/Region/ScriptEngine/Common/bin/Debug
                  [csc] Compiling 5 files to '/home/tedd/opensim/trunk/OpenSim/Region/ScriptEngine/Common/bin/Debug/OpenSim.Region.ScriptEngine.Common.dll'.
                  [csc] error CS0121: The call is ambiguous between the following methods or properties: `OpenSim.Region.ScriptEngine.Common.LSL_Types.LSLString.operator /(OpenSim.Region.ScriptEngine.Common.LSL_Types.LSLString, OpenSim.Region.ScriptEngine.Common.LSL_Types.LSLString)' and `string.operator /(string, string)'
                  [csc] /home/tedd/opensim/trunk/OpenSim/Region/ScriptEngine/Common/LSL_Types.cs(602,32): (Location of the symbol related to previous error)
                  [csc] /usr/lib/mono/2.0/mscorlib.dll (Location of the symbol related to previous error)
                  [csc] Compilation failed: 1 error(s), 0 warnings
             */
            //public static bool operator ==(LSLString s1, LSLString s2)
            //{
            //    return s1.m_string == s2.m_string;
            //}
            //public static bool operator !=(LSLString s1, LSLString s2)
            //{
            //    return s1.m_string != s2.m_string;
            //}
            #endregion

            #region Overriders
            public override bool Equals(object o)
            {
                if (o is String)
                {
                    string s = (string)o;
                    return s == this.m_string;
                }
                if (o is key)
                {
                    key k = (key)o;
                    return this.m_string == k.value;
                }
                if (o is LSLString)
                {
                    LSLString s = (string)o;
                    return this.m_string == s;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return m_string.GetHashCode();
            }

            #endregion

            #region " Standard string functions "
            //Clone,CompareTo,Contains
            //CopyTo,EndsWith,Equals,GetEnumerator,GetHashCode,GetType,GetTypeCode
            //IndexOf,IndexOfAny,Insert,IsNormalized,LastIndexOf,LastIndexOfAny
            //Length,Normalize,PadLeft,PadRight,Remove,Replace,Split,StartsWith,Substring,ToCharArray,ToLowerInvariant
            //ToString,ToUpper,ToUpperInvariant,Trim,TrimEnd,TrimStart
            public bool Contains(string value) { return m_string.Contains(value); }
            public int IndexOf(string value) { return m_string.IndexOf(value); }
            public int Length { get { return m_string.Length; } }


            #endregion
        }

        [Serializable]
        public struct LSLInteger
        {
            public int value;

            #region Constructors
            public LSLInteger(int i)
            {
                value = i;
            }

            public LSLInteger(double d)
            {
                value = (int)d;
            }

            #endregion
            static public implicit operator System.Int32(LSLInteger i)
            {
                return i.value;
            }

            static public implicit operator System.Boolean(LSLInteger i)
            {
                if (i.value == 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            static public implicit operator LSLInteger(int i)
            {
                return new LSLInteger(i);
            }

            static public implicit operator LSLInteger(double d)
            {
                return new LSLInteger(d);
            }

            static public LSLInteger operator &(LSLInteger i1, LSLInteger i2)
            {
                int ret = i1.value & i2.value;
                return ret;
            }


            //static public implicit operator System.Double(LSLInteger i)
            //{
            //    return (double)i.value;
            //}

            #region Overriders

            public override string ToString()
            {
                return this.value.ToString();
            }

            #endregion
        }

        [Serializable]
        public struct LSLFloat
        {
            public double value;

            #region Constructors
            public LSLFloat(int i)
            {
                this.value = (double)i;
            }

            public LSLFloat(double d)
            {
                this.value = d;
            }

            #endregion

            #region Operators

            static public implicit operator System.Double(LSLFloat f)
            {
                return f.value;
            }

            //static public implicit operator System.Int32(LSLFloat f)
            //{
            //    return (int)f.value;
            //}


            static public implicit operator System.Boolean(LSLFloat f)
            {
                if (f.value == 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            static public implicit operator LSLFloat(int i)
            {
                return new LSLFloat(i);
            }

            static public implicit operator LSLFloat(double d)
            {
                return new LSLFloat(d);
            }
            #endregion

            #region Overriders
            public override string ToString()
            {
                return this.value.ToString();
            }
            #endregion
        }

    }
}
