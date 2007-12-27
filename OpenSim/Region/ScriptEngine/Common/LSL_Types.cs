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

namespace OpenSim.Region.ScriptEngine.Common
{
    [Serializable]
    public class LSL_Types
    {
        [Serializable]
        public struct Vector3
        {
            public double x;
            public double y;
            public double z;

            public Vector3(Vector3 vector)
            {
                x = (float) vector.x;
                y = (float) vector.y;
                z = (float) vector.z;
            }

            public Vector3(double X, double Y, double Z)
            {
                x = X;
                y = Y;
                z = Z;
            }

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

                Vector3 vector = (Vector3) o;

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
                return new Vector3(lhs.x*rhs.x, lhs.y*rhs.y, lhs.z*rhs.z);
            }

            public static Vector3 operator %(Vector3 v1, Vector3 v2)
            {
                //Cross product
                Vector3 tv;
                tv.x = (v1.y*v2.z) - (v1.z*v2.y);
                tv.y = (v1.z*v2.x) - (v1.x*v2.z);
                tv.z = (v1.x*v2.y) - (v1.y*v2.x);
                return tv;
            }

            #endregion

            #region Vector & Float Math

            // Vector-Float and Float-Vector Math
            public static Vector3 operator *(Vector3 vec, float val)
            {
                return new Vector3(vec.x*val, vec.y*val, vec.z*val);
            }

            public static Vector3 operator *(float val, Vector3 vec)
            {
                return new Vector3(vec.x*val, vec.y*val, vec.z*val);
            }

            public static Vector3 operator /(Vector3 v, float f)
            {
                v.x = v.x/f;
                v.y = v.y/f;
                v.z = v.z/f;
                return v;
            }

            #endregion

            #region Vector & Rotation Math

            // Vector-Rotation Math
            public static Vector3 operator *(Vector3 v, Quaternion r)
            {
                Quaternion vq = new Quaternion(v.x, v.y, v.z, 0);
                Quaternion nq = new Quaternion(-r.x, -r.y, -r.z, r.s);

                Quaternion result = (r*vq)*nq;

                return new Vector3(result.x, result.y, result.z);
            }

            // I *think* this is how it works....
            public static Vector3 operator /(Vector3 vec, Quaternion quat)
            {
                quat.s = -quat.s;
                Quaternion vq = new Quaternion(vec.x, vec.y, vec.z, 0);
                Quaternion nq = new Quaternion(-quat.x, -quat.y, -quat.z, quat.s);

                Quaternion result = (quat*vq)*nq;

                return new Vector3(result.x, result.y, result.z);
            }

            #endregion

            #region Static Helper Functions

            public static double Dot(Vector3 v1, Vector3 v2)
            {
                return (v1.x*v2.x) + (v1.y*v2.y) + (v1.z*v2.z);
            }

            public static Vector3 Cross(Vector3 v1, Vector3 v2)
            {
                return new Vector3
                    (
                    v1.y*v2.z - v1.z*v2.y,
                    v1.z*v2.x - v1.x*v2.z,
                    v1.x*v2.y - v1.y*v2.x
                    );
            }

            public static float Mag(Vector3 v)
            {
                return (float) Math.Sqrt(v.x*v.y + v.y*v.y + v.z*v.z);
            }

            public static Vector3 Norm(Vector3 vector)
            {
                float mag = Mag(vector);
                return new Vector3(vector.x/mag, vector.y/mag, vector.z/mag);
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

            public Quaternion(Quaternion Quat)
            {
                x = (float) Quat.x;
                y = (float) Quat.y;
                z = (float) Quat.z;
                s = (float) Quat.s;
            }

            public Quaternion(double X, double Y, double Z, double S)
            {
                x = X;
                y = Y;
                z = Z;
                s = S;
            }

            #region Overriders

            public override int GetHashCode()
            {
                return (x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode() ^ s.GetHashCode());
            }

            public override bool Equals(object o)
            {
                if (!(o is Quaternion)) return false;

                Quaternion quaternion = (Quaternion) o;

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
                c.x = a.s*b.x + a.x*b.s + a.y*b.z - a.z*b.y;
                c.y = a.s*b.y + a.y*b.s + a.z*b.x - a.x*b.z;
                c.z = a.s*b.z + a.z*b.s + a.x*b.y - a.y*b.x;
                c.s = a.s*b.s - a.x*b.x - a.y*b.y - a.z*b.z;
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
                    if (o.GetType().ToString() == "System.String")
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
                output = "";
                if (m_data.Length == 0)
                {
                    return "";
                }
                foreach (object o in m_data)
                {
                    output = output + o.ToString();
                }
                return output;
            }
        }
    }
}