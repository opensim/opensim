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
using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OpenSim.Framework;

using OpenMetaverse;
using OMV_Vector3 = OpenMetaverse.Vector3;
using OMV_Vector3d = OpenMetaverse.Vector3d;
using OMV_Quaternion = OpenMetaverse.Quaternion;

namespace OpenSim.Region.ScriptEngine.Shared
{
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

            public Vector3(OMV_Vector3 vector)
            {
                x = vector.X;
                y = vector.Y;
                z = vector.Z;
            }

            public Vector3(OMV_Vector3d vector)
            {
                x = vector.X;
                y = vector.Y;
                z = vector.Z;
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
                string[] tmps = str.Split(new Char[] {','});
                if (tmps.Length < 3)
                {
                    z = y = x = 0;
                    return;
                }
                if (!Double.TryParse(tmps[0], NumberStyles.Float, Culture.NumberFormatInfo, out x))
                {
                    z = y = 0;
                    return;
                }
                if (!Double.TryParse(tmps[1], NumberStyles.Float, Culture.NumberFormatInfo, out y))
                {
                    z = x = 0;
                    return;
                }
                if (!Double.TryParse(tmps[2], NumberStyles.Float, Culture.NumberFormatInfo, out z))
                {
                    y = x = 0;
                }
            }

            #endregion

            #region Overriders

            public static implicit operator Boolean(Vector3 vec)
            {
                if (vec.x != 0)
                    return true;
                if (vec.y != 0)
                    return true;
                if (vec.z != 0)
                    return true;
                return false;
            }

            public override string ToString()
            {
                string s = String.Format(Culture.FormatProvider, "<{0:0.000000}, {1:0.000000}, {2:0.000000}>", x, y, z);
                return s;
            }

            public static explicit operator LSLString(Vector3 vec)
            {
                string s = String.Format(Culture.FormatProvider, "<{0:0.000000}, {1:0.000000}, {2:0.000000}>", vec.x, vec.y, vec.z);
                return new LSLString(s);
            }

            public static explicit operator string(Vector3 vec)
            {
                string s = String.Format(Culture.FormatProvider, "<{0:0.000000}, {1:0.000000}, {2:0.000000}>", vec.x, vec.y, vec.z);
                return s;
            }

            public static explicit operator Vector3(string s)
            {
                return new Vector3(s);
            }

            public static implicit operator list(Vector3 vec)
            {
                return new list(new object[] { vec });
            }

            public static implicit operator OMV_Vector3(Vector3 vec)
            {
                return new OMV_Vector3((float)vec.x, (float)vec.y, (float)vec.z);
            }

            public static implicit operator Vector3(OMV_Vector3 vec)
            {
                return new Vector3(vec);
            }

            public static implicit operator OMV_Vector3d(Vector3 vec)
            {
                return new OMV_Vector3d(vec.x, vec.y, vec.z);
            }

            public static implicit operator Vector3(OMV_Vector3d vec)
            {
                return new Vector3(vec);
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

                return (x == vector.x && y == vector.y && z == vector.z);
            }

            public static Vector3 operator -(Vector3 vector)
            {
                return new Vector3(-vector.x, -vector.y, -vector.z);
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

            public static LSLFloat operator *(Vector3 lhs, Vector3 rhs)
            {
                return Dot(lhs, rhs);
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
                double rx = r.s * v.x + r.y * v.z - r.z * v.y;
                double ry = r.s * v.y + r.z * v.x - r.x * v.z;
                double rz = r.s * v.z + r.x * v.y - r.y * v.x;

                v.x += 2.0f * (rz * r.y - ry * r.z);
                v.y += 2.0f * (rx * r.z - rz * r.x);
                v.z += 2.0f * (ry * r.x - rx * r.y);

                return v;
            }

            public static Vector3 operator /(Vector3 v, Quaternion r)
            {
                r.s = -r.s;
                return v * r;
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

            public static double MagSquare(Vector3 v)
            {
                return v.x * v.x + v.y * v.y + v.z * v.z;
            }

            public static double Mag(Vector3 v)
            {
                return Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);
            }

            public static Vector3 Norm(Vector3 vector)
            {
                double mag = Mag(vector);
                if (mag > 0.0)
                {
                    double invMag = 1.0 / mag;
                    return vector * invMag;
                }
                return new Vector3(0, 0, 0);
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
                if (s == 0 && x == 0 && y == 0 && z == 0)
                    s = 1;
            }

            public Quaternion(double X, double Y, double Z, double S)
            {
                x = X;
                y = Y;
                z = Z;
                s = S;
                if (s == 0 && x == 0 && y == 0 && z == 0)
                    s = 1;
            }

            public Quaternion(string str)
            {
                str = str.Replace('<', ' ');
                str = str.Replace('>', ' ');
                string[] tmps = str.Split(new Char[] {','});
                if (tmps.Length < 4 ||
                    !Double.TryParse(tmps[3], NumberStyles.Float, Culture.NumberFormatInfo, out s))
                {
                    z = y = x = 0;
                    s = 1;
                    return;
                }
                if (!Double.TryParse(tmps[0], NumberStyles.Float, Culture.NumberFormatInfo, out x))
                {
                    z = y = 0;
                    s = 1;
                    return;
                }
                if (!Double.TryParse(tmps[1], NumberStyles.Float, Culture.NumberFormatInfo, out y))
                {
                    z = x = 0;
                    s = 1;
                    return;
                }
                if (!Double.TryParse(tmps[2], NumberStyles.Float, Culture.NumberFormatInfo, out z))
                {
                    y = x = 0;
                    s = 1;
                }
            }

            public Quaternion(OMV_Quaternion rot)
            {
                x = rot.X;
                y = rot.Y;
                z = rot.Z;
                s = rot.W;
            }

            #endregion

            #region Methods
            public Quaternion Normalize()
            {
                double lengthsq = x * x + y * y + z * z + s * s;
                if (lengthsq < float.Epsilon)
                {
                    x = 0;
                    y = 0;
                    z = 0;
                    s = 1;
                }
                else
                {

                    double invLength = 1.0 / Math.Sqrt(lengthsq);
                    x *= invLength;
                    y *= invLength;
                    z *= invLength;
                    s *= invLength;
                }

                return this;
            }

            public static Quaternion Slerp(Quaternion q1, Quaternion q2, double amount)
            {
                double angle = (q1.x * q2.x) + (q1.y * q2.y) + (q1.z * q2.z) + (q1.s * q2.s);

                if (angle < 0f)
                {
                    q1.x = -q1.x;
                    q1.y = -q1.y;
                    q1.z = -q1.z;
                    q1.s = -q1.s;
                    angle *= -1.0;
                }

                double scale;
                double invscale;

                if ((angle + 1f) > 0.05f)
                {
                    if ((1f - angle) >= 0.05f)
                    {
                        // slerp
                        double theta = Math.Acos(angle);
                        double invsintheta = 1.0 / Math.Sin(theta);
                        scale = Math.Sin(theta * (1.0 - amount)) * invsintheta;
                        invscale = Math.Sin(theta * amount) * invsintheta;
                    }
                    else
                    {
                        // lerp
                        scale = 1.0 - amount;
                        invscale = amount;
                    }
                }
                else
                {
                    q2.x = -q1.y;
                    q2.y = q1.x;
                    q2.z = -q1.s;
                    q2.s = q1.z;

                    scale = Math.Sin(Math.PI * (0.5 - amount));
                    invscale = Math.Sin(Math.PI * amount);
                }

                return new Quaternion(
                    q1.x * scale + q2.x * invscale,
                    q1.y * scale + q2.y * invscale,
                    q1.z * scale + q2.z * invscale,
                    q1.s * scale + q2.s * invscale
                    );
            }
            #endregion

            #region Overriders
            public static implicit operator Boolean(Quaternion q)
            {
                if (q.x != 0)
                    return true;
                if (q.y != 0)
                    return true;
                if (q.z != 0)
                    return true;
                if (q.s != 1.0f)
                    return true;
                return false;
            }

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
                string st=String.Format(Culture.FormatProvider, "<{0:0.000000}, {1:0.000000}, {2:0.000000}, {3:0.000000}>", x, y, z, s);
                return st;
            }

            public static explicit operator string(Quaternion r)
            {
                string st=String.Format(Culture.FormatProvider,"<{0:0.000000}, {1:0.000000}, {2:0.000000}, {3:0.000000}>", r.x, r.y, r.z, r.s);
                return st;
            }

            public static explicit operator LSLString(Quaternion r)
            {
                string st=String.Format(Culture.FormatProvider,"<{0:0.000000}, {1:0.000000}, {2:0.000000}, {3:0.000000}>", r.x, r.y, r.z, r.s);
                return new LSLString(st);
            }

            public static explicit operator Quaternion(string s)
            {
                return new Quaternion(s);
            }

            public static implicit operator list(Quaternion r)
            {
                return new list(new object[] { r });
            }

            public static implicit operator OMV_Quaternion(Quaternion rot)
            {
                // LSL quaternions can normalize to 0, normal Quaternions can't.
                if (rot.s == 0 && rot.x == 0 && rot.y == 0 && rot.z == 0)
                    return OMV_Quaternion.Identity; // ZERO_ROTATION = 0,0,0,1

                OMV_Quaternion omvrot = new OMV_Quaternion((float)rot.x, (float)rot.y, (float)rot.z, (float)rot.s);
                omvrot.Normalize();
                return omvrot;
            }

            public static implicit operator Quaternion(OMV_Quaternion rot)
            {
                return new Quaternion(rot);
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

            public static double Mag(Quaternion q)
            {
                return Math.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.s * q.s);
            }

            #endregion

            public static Quaternion operator +(Quaternion a, Quaternion b)
            {
                return new Quaternion(a.x + b.x, a.y + b.y, a.z + b.z, a.s + b.s);
            }

            public static Quaternion operator /(Quaternion a, Quaternion b)
            {
                // assume normalized
                // if not, sl seems to not normalize either
                b.x = -b.x;
                b.y = -b.y;
                b.z = -b.z;

                return a * b;
            }

            public static Quaternion operator -(Quaternion a, Quaternion b)
            {
                return new Quaternion(a.x - b.x, a.y - b.y, a.z - b.z, a.s - b.s);
            }

            // using the equations below, we need to do "b * a" to be compatible with LSL
            public static Quaternion operator *(Quaternion b, Quaternion a)
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
                m_data = args;
            }

            public int Length
            {
                get
                {
                    if (m_data == null)
                        m_data=new Object[0];
                    return m_data.Length;
                }
            }

            public int Size
            {
                get
                {
                    if (m_data == null)
                        m_data=new Object[0];

                    int size = 0;

                    foreach (Object o in m_data)
                    {
                        if (o is LSL_Types.LSLInteger)
                            size += 4;
                        else if (o is LSL_Types.LSLFloat)
                            size += 8;
                        else if (o is LSL_Types.LSLString)
                            size += ((LSL_Types.LSLString)o).m_string == null ? 0 : ((LSL_Types.LSLString)o).m_string.Length;
                        else if (o is LSL_Types.key)
                            size += ((LSL_Types.key)o).value.Length;
                        else if (o is LSL_Types.Vector3)
                            size += 32;
                        else if (o is LSL_Types.Quaternion)
                            size += 64;
                        else if (o is int)
                            size += 4;
                        else if (o is uint)
                            size += 4;
                        else if (o is string)
                            size += ((string)o).Length;
                        else if (o is float)
                            size += 8;
                        else if (o is double)
                            size += 16;
                        else if (o is list)
                            size += ((list)o).Size;
                        else
                            throw new Exception("Unknown type in List.Size: " + o.GetType().ToString());
                    }
                    return size;
                }
            }

            public object[] Data
            {
                get {
                    if (m_data == null)
                        m_data=new Object[0];
                    return m_data;
                }

                set {m_data = value; }
            }

            /// <summary>
            /// Obtain LSL type from an index.
            /// </summary>
            /// <remarks>
            /// This is needed because LSL lists allow for multiple types, and safely
            /// iterating in them requires a type check.
            /// </remarks>
            /// <returns></returns>
            /// <param name='itemIndex'></param>
            public Type GetLSLListItemType(int itemIndex)
            {
                return Data[itemIndex].GetType();
            }

            /// <summary>
            /// Obtain float from an index.
            /// </summary>
            /// <remarks>
            /// For cases where implicit conversions would apply if items
            /// were not in a list (e.g. integer to float, but not float
            /// to integer) functions check for alternate types so as to
            /// down-cast from Object to the correct type.
            /// Note: no checks for item index being valid are performed
            /// </remarks>
            /// <returns></returns>
            /// <param name='itemIndex'></param>
            public LSL_Types.LSLFloat GetLSLFloatItem(int itemIndex)
            {
                if (Data[itemIndex] is LSL_Types.LSLInteger)
                {
                    return (LSL_Types.LSLInteger)Data[itemIndex];
                }
                else if (Data[itemIndex] is Int32)
                {
                    return new LSL_Types.LSLFloat((int)Data[itemIndex]);
                }
                else if (Data[itemIndex] is float)
                {
                    return new LSL_Types.LSLFloat((float)Data[itemIndex]);
                }
                else if (Data[itemIndex] is Double)
                {
                    return new LSL_Types.LSLFloat((Double)Data[itemIndex]);
                }
                else if (Data[itemIndex] is LSL_Types.LSLString)
                {
                    return new LSL_Types.LSLFloat(Data[itemIndex].ToString());
                }
                else
                {
                    return (LSL_Types.LSLFloat)Data[itemIndex];
                }
            }

            public LSL_Types.LSLString GetLSLStringItem(int itemIndex)
            {
                if (Data[itemIndex] is LSL_Types.key)
                {
                    return (LSL_Types.key)Data[itemIndex];
                }
                else
                {
                    return new LSL_Types.LSLString(Data[itemIndex].ToString());
                }
            }

            public LSL_Types.LSLInteger GetLSLIntegerItem(int itemIndex)
            {
                if (Data[itemIndex] is LSL_Types.LSLInteger)
                    return (LSL_Types.LSLInteger)Data[itemIndex];
                if (Data[itemIndex] is LSL_Types.LSLFloat)
                    return new LSLInteger((int)Data[itemIndex]);
                else if (Data[itemIndex] is Int32)
                    return new LSLInteger((int)Data[itemIndex]);
                else if (Data[itemIndex] is LSL_Types.LSLString)
                    return new LSLInteger(Data[itemIndex].ToString());
                else
                    throw new InvalidCastException(string.Format(
                        "{0} expected but {1} given",
                        typeof(LSL_Types.LSLInteger).Name,
                        Data[itemIndex] != null ?
                        Data[itemIndex].GetType().Name : "null"));
            }

            public LSL_Types.Vector3 GetVector3Item(int itemIndex)
            {
                if (Data[itemIndex] is LSL_Types.Vector3)
                {
                    return (LSL_Types.Vector3)Data[itemIndex];
                }
                else if(Data[itemIndex] is OpenMetaverse.Vector3)
                {
                    return new LSL_Types.Vector3(
                            (OpenMetaverse.Vector3)Data[itemIndex]);
                }
                else
                {
                    throw new InvalidCastException(string.Format(
                        "{0} expected but {1} given",
                        typeof(LSL_Types.Vector3).Name,
                        Data[itemIndex] != null ?
                        Data[itemIndex].GetType().Name : "null"));
                }
            }

            // use LSL_Types.Quaternion to parse and store a vector4 for lightShare
            public LSL_Types.Quaternion GetVector4Item(int itemIndex)
            {
                if (Data[itemIndex] is LSL_Types.Quaternion)
                {
                    LSL_Types.Quaternion q = (LSL_Types.Quaternion)Data[itemIndex];
                    return q;
                }
                else if(Data[itemIndex] is OpenMetaverse.Quaternion)
                {
                    LSL_Types.Quaternion q = new LSL_Types.Quaternion(
                            (OpenMetaverse.Quaternion)Data[itemIndex]);
                    q.Normalize();
                    return q;
                }
                else
                {
                    throw new InvalidCastException(string.Format(
                        "{0} expected but {1} given",
                        typeof(LSL_Types.Quaternion).Name,
                        Data[itemIndex] != null ?
                        Data[itemIndex].GetType().Name : "null"));
                }
            }

            public LSL_Types.Quaternion GetQuaternionItem(int itemIndex)
            {
                if (Data[itemIndex] is LSL_Types.Quaternion)
                {
                    LSL_Types.Quaternion q = (LSL_Types.Quaternion)Data[itemIndex];
                    q.Normalize();
                    return q;
                }
                else if(Data[itemIndex] is OpenMetaverse.Quaternion)
                {
                    LSL_Types.Quaternion q = new LSL_Types.Quaternion(
                            (OpenMetaverse.Quaternion)Data[itemIndex]);
                    q.Normalize();
                    return q;
                }
                else
                {
                    throw new InvalidCastException(string.Format(
                        "{0} expected but {1} given",
                        typeof(LSL_Types.Quaternion).Name,
                        Data[itemIndex] != null ?
                        Data[itemIndex].GetType().Name : "null"));
                }
            }

            public LSL_Types.key GetKeyItem(int itemIndex)
            {
              return (LSL_Types.key)Data[itemIndex];
            }

            public static list operator +(list a, list b)
            {
                object[] tmp;
                tmp = new object[a.Length + b.Length];
                a.Data.CopyTo(tmp, 0);
                b.Data.CopyTo(tmp, a.Length);
                return new list(tmp);
            }

            private void ExtendAndAdd(object o)
            {
                object[] tmp;
                tmp = new object[Data.Length + 1];
                Data.CopyTo(tmp, 0);
                tmp.SetValue(o, tmp.Length - 1);
                Data = tmp;
            }

            public static implicit operator Boolean(list l)
            {
                return l.Length != 0;
            }

            public static list operator +(list a, LSLString s)
            {
                a.ExtendAndAdd(s);
                return a;
            }

            public static list operator +(list a, LSLInteger i)
            {
                a.ExtendAndAdd(i);
                return a;
            }

            public static list operator +(list a, LSLFloat d)
            {
                a.ExtendAndAdd(d);
                return a;
            }

            public static bool operator ==(list a, list b)
            {
                int la = -1;
                int lb = -1;
                try { la = a.Length; }
                catch (NullReferenceException) { }
                try { lb = b.Length; }
                catch (NullReferenceException) { }

                return la == lb;
            }

            public static bool operator !=(list a, list b)
            {
                int la = -1;
                int lb = -1;
                try { la = a.Length; }
                catch (NullReferenceException) { }
                try { lb = b.Length; }
                catch (NullReferenceException) { }

                return la != lb;
            }

            public void Add(object o)
            {
                object[] tmp;
                tmp = new object[Data.Length + 1];
                Data.CopyTo(tmp, 0);
                tmp[Data.Length] = o; // Since this is tmp.Length - 1
                Data = tmp;
            }

            public bool Contains(object o)
            {
                bool ret = false;
                foreach (object i in Data)
                {
                    if (i == o)
                    {
                        ret = true;
                        break;
                    }
                }
                return ret;
            }

            public list DeleteSublist(int start, int end)
            {
                // Not an easy one
                // If start <= end, remove that part
                // if either is negative, count from the end of the array
                // if the resulting start > end, remove all BUT that part

                Object[] ret;

                if (start < 0)
                    start=Data.Length+start;

                if (start < 0)
                    start=0;

                if (end < 0)
                    end=Data.Length+end;
                if (end < 0)
                    end=0;

                if (start > end)
                {
                    if (end >= Data.Length)
                        return new list(new Object[0]);

                    if (start >= Data.Length)
                        start=Data.Length-1;

                    return GetSublist(end, start);
                }

                // start >= 0 && end >= 0 here
                if (start >= Data.Length)
                {
                    ret=new Object[Data.Length];
                    Array.Copy(Data, 0, ret, 0, Data.Length);

                    return new list(ret);
                }

                if (end >= Data.Length)
                    end=Data.Length-1;

                // now, this makes the math easier
                int remove=end+1-start;

                ret=new Object[Data.Length-remove];
                if (ret.Length == 0)
                    return new list(ret);

                int src;
                int dest=0;

                for (src = 0; src < Data.Length; src++)
                {
                    if (src < start || src > end)
                        ret[dest++]=Data[src];
                }

                return new list(ret);
            }

            public list GetSublist(int start, int end)
            {

                object[] ret;

                // Take care of neg start or end's
                // NOTE that either index may still be negative after
                // adding the length, so we must take additional
                // measures to protect against this. Note also that
                // after normalisation the negative indices are no
                // longer relative to the end of the list.

                if (start < 0)
                {
                    start = Data.Length + start;
                }

                if (end < 0)
                {
                    end = Data.Length + end;
                }

                // The conventional case is start <= end
                // NOTE that the case of an empty list is
                // dealt with by the initial test. Start
                // less than end is taken to be the most
                // common case.

                if (start <= end)
                {

                    // Start sublist beyond length
                    // Also deals with start AND end still negative
                    if (start >= Data.Length || end < 0)
                    {
                        return new list();
                    }

                    // Sublist extends beyond the end of the supplied list
                    if (end >= Data.Length)
                    {
                        end = Data.Length - 1;
                    }

                    // Sublist still starts before the beginning of the list
                    if (start < 0)
                    {
                        start = 0;
                    }

                    ret = new object[end - start + 1];

                    Array.Copy(Data, start, ret, 0, end - start + 1);

                    return new list(ret);

                }

                // Deal with the segmented case: 0->end + start->EOL

                else
                {

                    list result;

                    // If end is negative, then prefix list is empty
                    if (end < 0)
                    {
                        result = new list();
                        // If start is still negative, then the whole of
                        // the existing list is returned. This case is
                        // only admitted if end is also still negative.
                        if (start < 0)
                        {
                            return this;
                        }

                    }
                    else
                    {
                        result = GetSublist(0,end);
                    }

                    // If start is outside of list, then just return
                    // the prefix, whatever it is.
                    if (start >= Data.Length)
                    {
                        return result;
                    }

                    return result + GetSublist(start, Data.Length);

                }
            }

            private static int compare(object left, object right, int ascending)
            {
                if (!left.GetType().Equals(right.GetType()))
                {
                    // unequal types are always "equal" for comparison purposes.
                    // this way, the bubble sort will never swap them, and we'll
                    // get that feathered effect we're looking for
                    return 0;
                }

                int ret = 0;

                if (left is key)
                {
                    key l = (key)left;
                    key r = (key)right;
                    ret = String.CompareOrdinal(l.value, r.value);
                }
                else if (left is LSLString)
                {
                    LSLString l = (LSLString)left;
                    LSLString r = (LSLString)right;
                    ret = String.CompareOrdinal(l.m_string, r.m_string);
                }
                else if (left is LSLInteger)
                {
                    LSLInteger l = (LSLInteger)left;
                    LSLInteger r = (LSLInteger)right;
                    ret = Math.Sign(l.value - r.value);
                }
                else if (left is LSLFloat)
                {
                    LSLFloat l = (LSLFloat)left;
                    LSLFloat r = (LSLFloat)right;
                    ret = Math.Sign(l.value - r.value);
                }
                else if (left is Vector3)
                {
                    Vector3 l = (Vector3)left;
                    Vector3 r = (Vector3)right;
                    ret = Math.Sign(Vector3.Mag(l) - Vector3.Mag(r));
                }
                else if (left is Quaternion)
                {
                    Quaternion l = (Quaternion)left;
                    Quaternion r = (Quaternion)right;
                    ret = Math.Sign(Quaternion.Mag(l) - Quaternion.Mag(r));
                }

                if (ascending == 0)
                {
                    ret = 0 - ret;
                }

                return ret;
            }

            class HomogeneousComparer : IComparer
            {
                public HomogeneousComparer()
                {
                }

                public int Compare(object lhs, object rhs)
                {
                    return compare(lhs, rhs, 1);
                }
            }

            public list Sort(int stride, int ascending)
            {
                if (Data.Length == 0)
                    return new list(); // Don't even bother

                object[] ret = new object[Data.Length];
                Array.Copy(Data, 0, ret, 0, Data.Length);

                if (stride <= 0)
                {
                    stride = 1;
                }

                if ((Data.Length % stride) != 0)
                    return new list(ret);

                // we can optimize here in the case where stride == 1 and the list
                // consists of homogeneous types

                if (stride == 1)
                {
                    bool homogeneous = true;
                    int index;
                    for (index = 1; index < Data.Length; index++)
                    {
                        if (!Data[0].GetType().Equals(Data[index].GetType()))
                        {
                            homogeneous = false;
                            break;
                        }
                    }

                    if (homogeneous)
                    {
                        Array.Sort(ret, new HomogeneousComparer());
                        if (ascending == 0)
                        {
                            Array.Reverse(ret);
                        }
                        return new list(ret);
                    }
                }

                // Because of the desired type specific feathered sorting behavior
                // requried by the spec, we MUST use a non-optimized bubble sort here.
                // Anything else will give you the incorrect behavior.

                // begin bubble sort...
                int i;
                int j;
                int k;
                int n = Data.Length;

                for (i = 0; i < (n-stride); i += stride)
                {
                    for (j = i + stride; j < n; j += stride)
                    {
                        if (compare(ret[i], ret[j], ascending) > 0)
                        {
                            for (k = 0; k < stride; k++)
                            {
                                object tmp = ret[i + k];
                                ret[i + k] = ret[j + k];
                                ret[j + k] = tmp;
                            }
                        }
                    }
                }

                // end bubble sort

                return new list(ret);
            }

            #region CSV Methods

            public static list FromCSV(string csv)
            {
                return new list(csv.Split(','));
            }

            public string ToCSV()
            {
                if(m_data == null || m_data.Length == 0)
                    return String.Empty;

                Object o = m_data[0];
                int len = m_data.Length;
                if(len == 1)
                    return o.ToString();

                StringBuilder sb = new StringBuilder(1024);
                sb.Append(o.ToString());
                for(int i = 1 ; i < len; i++)
                {
                    sb.Append(",");
                    sb.Append(o.ToString());
                }
                return sb.ToString();
            }

            private string ToSoup()
            {
                if(m_data == null || m_data.Length == 0)
                    return String.Empty;

                StringBuilder sb = new StringBuilder(1024);
                foreach (object o in m_data)
                {
                    sb.Append(o.ToString());
                }
                return sb.ToString();
            }

            public static explicit operator String(list l)
            {
                return l.ToSoup();
            }

            public static explicit operator LSLString(list l)
            {
                return new LSLString(l.ToSoup());
            }

            public override string ToString()
            {
                return ToSoup();
            }

            #endregion

            #region Statistic Methods

            public double Min()
            {
                double minimum = double.PositiveInfinity;
                double entry;
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(Data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out entry))
                    {
                        if (entry < minimum) minimum = entry;
                    }
                }
                return minimum;
            }

            public double Max()
            {
                double maximum = double.NegativeInfinity;
                double entry;
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(Data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out entry))
                    {
                        if (entry > maximum) maximum = entry;
                    }
                }
                return maximum;
            }

            public double Range()
            {
                return (this.Max() / this.Min());
            }

            public int NumericLength()
            {
                int count = 0;
                double entry;
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(Data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out entry))
                    {
                        count++;
                    }
                }
                return count;
            }

            public static list ToDoubleList(list src)
            {
                list ret = new list();
                double entry;
                for (int i = 0; i < src.Data.Length; i++)
                {
                    if (double.TryParse(src.Data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out entry))
                    {
                        ret.Add(entry);
                    }
                }
                return ret;
            }

            public double Sum()
            {
                double sum = 0;
                double entry;
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(Data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out entry))
                    {
                        sum = sum + entry;
                    }
                }
                return sum;
            }

            public double SumSqrs()
            {
                double sum = 0;
                double entry;
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(Data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out entry))
                    {
                        sum = sum + Math.Pow(entry, 2);
                    }
                }
                return sum;
            }

            public double Mean()
            {
                return (this.Sum() / this.NumericLength());
            }

            public void NumericSort()
            {
                IComparer Numeric = new NumericComparer();
                Array.Sort(Data, Numeric);
            }

            public void AlphaSort()
            {
                IComparer Alpha = new AlphaCompare();
                Array.Sort(Data, Alpha);
            }

            public double Median()
            {
                return Qi(0.5);
            }

            public double GeometricMean()
            {
                double ret = 1.0;
                list nums = ToDoubleList(this);
                for (int i = 0; i < nums.Data.Length; i++)
                {
                    ret *= (double)nums.Data[i];
                }
                return Math.Exp(Math.Log(ret) / (double)nums.Data.Length);
            }

            public double HarmonicMean()
            {
                double ret = 0.0;
                list nums = ToDoubleList(this);
                for (int i = 0; i < nums.Data.Length; i++)
                {
                    ret += 1.0 / (double)nums.Data[i];
                }
                return ((double)nums.Data.Length / ret);
            }

            public double Variance()
            {
                double s = 0;
                list num = ToDoubleList(this);
                for (int i = 0; i < num.Data.Length; i++)
                {
                    s += Math.Pow((double)num.Data[i], 2);
                }
                return (s - num.Data.Length * Math.Pow(num.Mean(), 2)) / (num.Data.Length - 1);
            }

            public double StdDev()
            {
                return Math.Sqrt(this.Variance());
            }

            public double Qi(double i)
            {
                list j = this;
                j.NumericSort();

                if (Math.Ceiling(this.Length * i) == this.Length * i)
                {
                    return (double)((double)j.Data[(int)(this.Length * i - 1)] + (double)j.Data[(int)(this.Length * i)]) / 2;
                }
                else
                {
                    return (double)j.Data[((int)(Math.Ceiling(this.Length * i))) - 1];
                }
            }

            #endregion

            public string ToPrettyString()
            {
                if(m_data == null || m_data.Length == 0)
                    return "[]";

                StringBuilder sb = new StringBuilder(1024);
                int len = m_data.Length;
                int last = len - 1;
                object o;

                sb.Append("[");
                for(int i = 0; i < len; i++ )
                {
                    o = m_data[i];
                    if (o is String)
                    {
                        sb.Append("\"");
                        sb.Append((String)o);
                        sb.Append("\"");
                    }
                    else
                    {
                        sb.Append(o.ToString());
                    }
                    if(i < last)
                        sb.Append(",");
                }
                sb.Append("]");
                return sb.ToString();
            }

            public class AlphaCompare : IComparer
            {
                int IComparer.Compare(object x, object y)
                {
                    return string.Compare(x.ToString(), y.ToString());
                }
            }

            public class NumericComparer : IComparer
            {
                int IComparer.Compare(object x, object y)
                {
                    double a;
                    double b;
                    if (!double.TryParse(x.ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out a))
                    {
                        a = 0.0;
                    }
                    if (!double.TryParse(y.ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out b))
                    {
                        b = 0.0;
                    }
                    if (a < b)
                    {
                        return -1;
                    }
                    else if (a == b)
                    {
                        return 0;
                    }
                    else
                    {
                        return 1;
                    }
                }
            }

            public override bool Equals(object o)
            {
                if (!(o is list))
                    return false;

                return Data.Length == ((list)o).Data.Length;
            }

            public override int GetHashCode()
            {
                return Data.GetHashCode();
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

            static public implicit operator Boolean(key k)
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

            public static bool operator true(key k)
            {
                return (Boolean)k;
            }

            public static bool operator false(key k)
            {
                return !(Boolean)k;
            }

            static public implicit operator key(string s)
            {
                return new key(s);
            }

            static public implicit operator String(key k)
            {
                return k.value;
            }

            static public implicit operator LSLString(key k)
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
                return o.ToString() == value;
            }

            public override int GetHashCode()
            {
                return value.GetHashCode();
            }

            public override string ToString()
            {
                return value;
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

            public LSLString(double d)
            {
                string s = String.Format(Culture.FormatProvider, "{0:0.000000}", d);
                m_string = s;
            }

            public LSLString(LSLFloat f)
            {
                string s = String.Format(Culture.FormatProvider, "{0:0.000000}", f.value);
                m_string = s;
            }

            public LSLString(int i)
            {
                string s = String.Format("{0}", i);
                m_string = s;
            }

            public LSLString(LSLInteger i) : this(i.value) {}

            #endregion

            #region Operators
            static public implicit operator Boolean(LSLString s)
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

            static public implicit operator String(LSLString s)
            {
                return s.m_string;
            }

            static public implicit operator LSLString(string s)
            {
                return new LSLString(s);
            }

            public static string ToString(LSLString s)
            {
                return s.m_string;
            }

            public override string ToString()
            {
                return m_string;
            }

            public static bool operator ==(LSLString s1, string s2)
            {
                return s1.m_string == s2;
            }

            public static bool operator !=(LSLString s1, string s2)
            {
                return s1.m_string != s2;
            }

            public static LSLString operator +(LSLString s1, LSLString s2)
            {
                return new LSLString(s1.m_string + s2.m_string);
            }

            public static explicit operator double(LSLString s)
            {
                return new LSLFloat(s).value;
            }

            public static explicit operator LSLInteger(LSLString s)
            {
                return new LSLInteger(s.m_string);
            }

            public static explicit operator LSLString(double d)
            {
                return new LSLString(d);
            }

            static public explicit operator LSLString(int i)
            {
                return new LSLString(i);
            }

            public static explicit operator LSLString(LSLFloat f)
            {
                return new LSLString(f);
            }

            static public explicit operator LSLString(bool b)
            {
                if (b)
                    return new LSLString("1");
                else
                    return new LSLString("0");
            }

            public static implicit operator Vector3(LSLString s)
            {
                return new Vector3(s.m_string);
            }

            public static implicit operator Quaternion(LSLString s)
            {
                return new Quaternion(s.m_string);
            }

            public static implicit operator LSLFloat(LSLString s)
            {
                return new LSLFloat(s);
            }

            public static implicit operator list(LSLString s)
            {
                return new list(new object[]{s});
            }

            #endregion

            #region Overriders
            public override bool Equals(object o)
            {
                return m_string == o.ToString();
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
            private static readonly Regex castRegex = new Regex(@"(^[ ]*0[xX][0-9A-Fa-f][0-9A-Fa-f]*)|(^[ ]*(-?|\+?)[0-9][0-9]*)");

            #region Constructors
            public LSLInteger(int i)
            {
                value = i;
            }

            public LSLInteger(uint i)
            {
                value = (int)i;
            }

            public LSLInteger(double d)
            {
                value = (int)d;
            }

            public LSLInteger(string s)
            {
                Match m = castRegex.Match(s);
                string v = m.Groups[0].Value;
                // Leading plus sign is allowed, but ignored
                v = v.Replace("+", "");

                if (v == String.Empty)
                {
                    value = 0;
                }
                else
                {
                    try
                    {
                        if (v.Contains("x") || v.Contains("X"))
                        {
                            value = int.Parse(v.Substring(2), System.Globalization.NumberStyles.HexNumber);
                        }
                        else
                        {
                            value = int.Parse(v, System.Globalization.NumberStyles.Integer);
                        }
                    }
                    catch (OverflowException)
                    {
                        value = -1;
                    }
                }
            }

            #endregion

            #region Operators

            static public implicit operator int(LSLInteger i)
            {
                return i.value;
            }

            static public explicit operator uint(LSLInteger i)
            {
                return (uint)i.value;
            }

            static public explicit operator LSLString(LSLInteger i)
            {
                return new LSLString(i.ToString());
            }

            public static implicit operator list(LSLInteger i)
            {
                return new list(new object[] { i });
            }

            static public implicit operator Boolean(LSLInteger i)
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

            static public explicit operator LSLInteger(string s)
            {
                return new LSLInteger(s);
            }

            static public implicit operator LSLInteger(uint u)
            {
                return new LSLInteger(u);
            }

            static public explicit operator LSLInteger(double d)
            {
                return new LSLInteger(d);
            }

            static public explicit operator LSLInteger(LSLFloat f)
            {
                return new LSLInteger(f.value);
            }

            static public implicit operator LSLInteger(bool b)
            {
                if (b)
                    return new LSLInteger(1);
                else
                    return new LSLInteger(0);
            }

            static public LSLInteger operator ==(LSLInteger i1, LSLInteger i2)
            {
                bool ret = i1.value == i2.value;
                return new LSLInteger((ret ? 1 : 0));
            }

            static public LSLInteger operator !=(LSLInteger i1, LSLInteger i2)
            {
                bool ret = i1.value != i2.value;
                return new LSLInteger((ret ? 1 : 0));
            }

            static public LSLInteger operator <(LSLInteger i1, LSLInteger i2)
            {
                bool ret = i1.value < i2.value;
                return new LSLInteger((ret ? 1 : 0));
            }
            static public LSLInteger operator <=(LSLInteger i1, LSLInteger i2)
            {
                bool ret = i1.value <= i2.value;
                return new LSLInteger((ret ? 1 : 0));
            }

            static public LSLInteger operator >(LSLInteger i1, LSLInteger i2)
            {
                bool ret = i1.value > i2.value;
                return new LSLInteger((ret ? 1 : 0));
            }

            static public LSLInteger operator >=(LSLInteger i1, LSLInteger i2)
            {
                bool ret = i1.value >= i2.value;
                return new LSLInteger((ret ? 1 : 0));
            }

            static public LSLInteger operator +(LSLInteger i1, int i2)
            {
                return new LSLInteger(i1.value + i2);
            }

            static public LSLInteger operator -(LSLInteger i1, int i2)
            {
                return new LSLInteger(i1.value - i2);
            }

            static public LSLInteger operator *(LSLInteger i1, int i2)
            {
                return new LSLInteger(i1.value * i2);
            }

            static public LSLInteger operator /(LSLInteger i1, int i2)
            {
                return new LSLInteger(i1.value / i2);
            }

//            static public LSLFloat operator +(LSLInteger i1, double f)
//            {
//                return new LSLFloat((double)i1.value + f);
//            }
//
//            static public LSLFloat operator -(LSLInteger i1, double f)
//            {
//                return new LSLFloat((double)i1.value - f);
//            }
//
//            static public LSLFloat operator *(LSLInteger i1, double f)
//            {
//                return new LSLFloat((double)i1.value * f);
//            }
//
//            static public LSLFloat operator /(LSLInteger i1, double f)
//            {
//                return new LSLFloat((double)i1.value / f);
//            }

            static public LSLInteger operator -(LSLInteger i)
            {
                return new LSLInteger(-i.value);
            }

            static public LSLInteger operator ~(LSLInteger i)
            {
                return new LSLInteger(~i.value);
            }

            public override bool Equals(Object o)
            {
                if (!(o is LSLInteger))
                {
                    if (o is int)
                    {
                        return value == (int)o;
                    }
                    else
                    {
                        return false;
                    }
                }

                return value == ((LSLInteger)o).value;
            }

            public override int GetHashCode()
            {
                return value;
            }

            static public LSLInteger operator &(LSLInteger i1, LSLInteger i2)
            {
                int ret = i1.value & i2.value;
                return ret;
            }

            static public LSLInteger operator %(LSLInteger i1, LSLInteger i2)
            {
                int ret = i1.value % i2.value;
                return ret;
            }

            static public LSLInteger operator |(LSLInteger i1, LSLInteger i2)
            {
                int ret = i1.value | i2.value;
                return ret;
            }

            static public LSLInteger operator ^(LSLInteger i1, LSLInteger i2)
            {
                int ret = i1.value ^ i2.value;
                return ret;
            }

            static public LSLInteger operator !(LSLInteger i1)
            {
                return i1.value == 0 ? 1 : 0;
            }

            public static LSLInteger operator ++(LSLInteger i)
            {
                i.value++;
                return i;
            }


            public static LSLInteger operator --(LSLInteger i)
            {
                i.value--;
                return i;
            }

            public static LSLInteger operator << (LSLInteger i, int s)
            {
                return i.value << s;
            }

            public static LSLInteger operator >> (LSLInteger i, int s)
            {
                return i.value >> s;
            }

            static public implicit operator System.Double(LSLInteger i)
            {
                return (double)i.value;
            }

            public static bool operator true(LSLInteger i)
            {
                return i.value != 0;
            }

            public static bool operator false(LSLInteger i)
            {
                return i.value == 0;
            }

            #endregion

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

            public LSLFloat(string s)
            {
                Regex r = new Regex("^ *(\\+|-)?([0-9]+\\.?[0-9]*|\\.[0-9]+)([eE](\\+|-)?[0-9]+)?");
                Match m = r.Match(s);
                string v = m.Groups[0].Value;

                v = v.Trim();

                if (v == String.Empty || v == null)
                    v = "0.0";
                else
                    if (!v.Contains(".") && !v.ToLower().Contains("e"))
                        v = v + ".0";
                    else
                        if (v.EndsWith("."))
                            v = v + "0";
                this.value = double.Parse(v, System.Globalization.NumberStyles.Float, Culture.NumberFormatInfo);
            }

            #endregion

            #region Operators

            static public explicit operator float(LSLFloat f)
            {
                return (float)f.value;
            }

            static public explicit operator int(LSLFloat f)
            {
                return (int)f.value;
            }

            static public explicit operator uint(LSLFloat f)
            {
                return (uint) Math.Abs(f.value);
            }

            static public implicit operator Boolean(LSLFloat f)
            {
                if (f.value == 0.0)
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

            static public implicit operator LSLFloat(LSLInteger i)
            {
                return new LSLFloat(i.value);
            }

            static public explicit operator LSLFloat(string s)
            {
                return new LSLFloat(s);
            }

            public static implicit operator list(LSLFloat f)
            {
                return new list(new object[] { f });
            }

            static public implicit operator LSLFloat(double d)
            {
                return new LSLFloat(d);
            }

            static public implicit operator LSLFloat(bool b)
            {
                if (b)
                    return new LSLFloat(1.0);
                else
                    return new LSLFloat(0.0);
            }

            static public bool operator ==(LSLFloat f1, LSLFloat f2)
            {
                return f1.value == f2.value;
            }

            static public bool operator !=(LSLFloat f1, LSLFloat f2)
            {
                return f1.value != f2.value;
            }

            static public LSLFloat operator ++(LSLFloat f)
            {
                f.value++;
                return f;
            }

            static public LSLFloat operator --(LSLFloat f)
            {
                f.value--;
                return f;
            }

            static public LSLFloat operator +(LSLFloat f, int i)
            {
                return new LSLFloat(f.value + (double)i);
            }

            static public LSLFloat operator -(LSLFloat f, int i)
            {
                return new LSLFloat(f.value - (double)i);
            }

            static public LSLFloat operator *(LSLFloat f, int i)
            {
                return new LSLFloat(f.value * (double)i);
            }

            static public LSLFloat operator /(LSLFloat f, int i)
            {
                return new LSLFloat(f.value / (double)i);
            }

            static public LSLFloat operator +(LSLFloat lhs, LSLFloat rhs)
            {
                return new LSLFloat(lhs.value + rhs.value);
            }

            static public LSLFloat operator -(LSLFloat lhs, LSLFloat rhs)
            {
                return new LSLFloat(lhs.value - rhs.value);
            }

            static public LSLFloat operator *(LSLFloat lhs, LSLFloat rhs)
            {
                return new LSLFloat(lhs.value * rhs.value);
            }

            static public LSLFloat operator /(LSLFloat lhs, LSLFloat rhs)
            {
                return new LSLFloat(lhs.value / rhs.value);
            }

            static public LSLFloat operator -(LSLFloat f)
            {
                return new LSLFloat(-f.value);
            }

            static public implicit operator System.Double(LSLFloat f)
            {
                return f.value;
            }

            #endregion

            #region Overriders

            public override string ToString()
            {
                return String.Format(Culture.FormatProvider, "{0:0.000000}", this.value);
            }

            public override bool Equals(Object o)
            {
                if (!(o is LSLFloat))
                    return false;
                return value == ((LSLFloat)o).value;
            }

            public override int GetHashCode()
            {
                return value.GetHashCode();
            }


            #endregion
        }
    }
}
