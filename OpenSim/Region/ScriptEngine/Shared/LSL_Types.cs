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
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using OpenSim.Framework;

using OpenMetaverse;
using OMV_Vector3 = OpenMetaverse.Vector3;
using OMV_Vector3d = OpenMetaverse.Vector3d;
using OMV_Quaternion = OpenMetaverse.Quaternion;
using System.Runtime.InteropServices;

namespace OpenSim.Region.ScriptEngine.Shared
{
    public partial class LSL_Types
    {
        // Types are kept is separate .dll to avoid having to add whatever .dll it is in it to script AppDomain
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static bool IsBadNumber(double d)
        {
            return (*(long*)(&d) & 0x7FFFFFFFFFFFFFFF) >= 0x7FF0000000000000;
        }

        [Serializable]
        public struct Vector3
        {
            public double x;
            public double y;
            public double z;

            #region Constructors

            public Vector3(Vector3 vector)
            {
                x = vector.x;
                y = vector.y;
                z = vector.z;
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector3(string str) : this(MemoryExtensions.AsSpan(str)) { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Vector3(LSLString str) : this(MemoryExtensions.AsSpan(str.m_string)) { }

            public Vector3(ReadOnlySpan<char> str)
            {
                if (str.Length < 5)
                {
                    z = y = x = 0;
                    return;
                }

                int start = 0;
                int comma = 0;
                char c;
                do
                {
                    c = Unsafe.Add(ref MemoryMarshal.GetReference(str), comma);
                    if (c == ',' || c == '<')
                        break;
                }
                while (++comma < str.Length);

                if (c == '<')
                {
                    start = ++comma;
                    while (++comma < str.Length)
                    {
                        if (Unsafe.Add(ref MemoryMarshal.GetReference(str), comma) == ',')
                            break;
                    }
                }
                if (comma > str.Length - 3)
                {
                    z = y = x = 0;
                    return;
                }

                if (!double.TryParse(str[start..comma], NumberStyles.Float, Utils.EnUsCulture, out x))
                {
                    z = y = 0;
                    return;
                }

                start = ++comma;
                while (++comma < str.Length)
                {
                    if (Unsafe.Add(ref MemoryMarshal.GetReference(str), comma) == ',')
                        break;
                }
                if (comma > str.Length - 1)
                {
                    z = y = x = 0;
                    return;
                }
                if (!double.TryParse(str[start..comma], NumberStyles.Float, Utils.EnUsCulture, out y))
                {
                    z = x = 0;
                    return;
                }

                start = ++comma;
                while (++comma < str.Length)
                {
                    c = Unsafe.Add(ref MemoryMarshal.GetReference(str), comma);
                    if (c == '>')
                        break;
                }

                if (!double.TryParse(str[start..comma], NumberStyles.Float, Utils.EnUsCulture, out z))
                {
                    y = x = 0;
                    return;
                }
            }

            #endregion

            #region Overriders

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static implicit operator Boolean(Vector3 vec)
            {
                return vec.x != 0 || vec.y != 0 || vec.z != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override string ToString()
            {
                string s = String.Format(Culture.FormatProvider, "<{0:0.000000}, {1:0.000000}, {2:0.000000}>", x, y, z);
                return s;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                return x.GetHashCode() + y.GetHashCode() + z.GetHashCode();
            }

            public override bool Equals(object o)
            {
                if (o is Vector3 vector)
                    return (x == vector.x && y == vector.y && z == vector.z);
                return false;
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
                double r = v.x / f;
                if (IsBadNumber(r))
                    throw new ScriptException("Vector division by zero");
                v.x = r;

                r = v.y / f;
                if (IsBadNumber(r))
                    throw new ScriptException("Vector division by zero");
                v.y = r;

                r = v.z / f;
                if (IsBadNumber(r))
                    throw new ScriptException("Vector division by zero");
                v.z = r;

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
                double r = v.x / f;
                if (IsBadNumber(r))
                    throw new ScriptException("Vector division by zero");
                v.x = r;

                r = v.y / f;
                if (IsBadNumber(r))
                    throw new ScriptException("Vector division by zero");
                v.y = r;

                r = v.z / f;
                if (IsBadNumber(r))
                    throw new ScriptException("Vector division by zero");
                v.z = r;

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
                double mag = MagSquare(vector);
                if (mag > float.Epsilon)
                {
                    double invMag = 1.0 / Math.Sqrt(mag);
                    return vector * invMag;
                }
                return Vector3.Zero;
            }

            public static Vector3 Slerp(Vector3 v1, Vector3 v2, double amount)
            {
                double angle = (v1.x * v2.x) + (v1.y * v2.y) + (v1.z * v2.z);
                double scale;
                double invscale;

                if (angle < 0.999f)
                {
                    angle = Math.Acos(angle);
                    invscale = 1.0f / Math.Sin(angle);
                    scale = Math.Sin((1.0f - amount) * angle) * invscale;
                    invscale *= Math.Sin((amount * angle));
                }
                else
                {
                    scale = 1.0f - amount;
                    invscale = amount;
                }
                return new Vector3(
                    v1.x * scale + v2.x * invscale,
                    v1.y * scale + v2.y * invscale,
                    v1.z * scale + v2.z * invscale
                );
            }

            public static readonly Vector3 Zero = new(0, 0, 0);
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
                x = Quat.x;
                y = Quat.y;
                z = Quat.z;
                s = Quat.s;
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

            public Quaternion(OMV_Quaternion rot)
            {
                x = rot.X;
                y = rot.Y;
                z = rot.Z;
                s = rot.W;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Quaternion(string str) : this(MemoryExtensions.AsSpan(str)) { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Quaternion(LSLString str) : this(MemoryExtensions.AsSpan(str.m_string)) { }

            public Quaternion(ReadOnlySpan<char> str)
            {
                if (str.Length < 7)
                {
                    z = y = x = 0;
                    s = 1;
                    return;
                }

                int start = 0;
                int comma = 0;
                char c;

                do
                {
                    c = Unsafe.Add(ref MemoryMarshal.GetReference(str), comma);
                    if (c == ',' || c == '<')
                        break;
                }
                while (++comma < str.Length);

                if (c == '<')
                {
                    start = ++comma;
                    while (++comma < str.Length)
                    {
                        if (Unsafe.Add(ref MemoryMarshal.GetReference(str), comma) == ',')
                            break;
                    }
                }
                if (comma > str.Length - 5)
                {
                    z = y = x = 0;
                    s = 1;
                    return;
                }

                if (!double.TryParse(str[start..comma], NumberStyles.Float, Utils.EnUsCulture, out x))
                {
                    z = y = 0;
                    s = 1;
                    return;
                }

                start = ++comma;
                while (++comma < str.Length)
                {
                    if (Unsafe.Add(ref MemoryMarshal.GetReference(str), comma) == ',')
                        break;
                }
                if (comma > str.Length - 3)
                {
                    z = y = x = 0;
                    s = 1;
                    return;
                }

                if (!double.TryParse(str[start..comma], NumberStyles.Float, Utils.EnUsCulture, out y))
                {
                    z = x = 0;
                    s = 1;
                    return;
                }
                start = ++comma;
                while (++comma < str.Length)
                {
                    if (Unsafe.Add(ref MemoryMarshal.GetReference(str), comma) == ',')
                        break;
                }
                if (comma > str.Length - 1)
                {
                    z = y = x = 0;
                    s = 1;
                    return;
                }

                if (!double.TryParse(str[start..comma], NumberStyles.Float, Utils.EnUsCulture, out z))
                {
                    y = x = 0;
                    s = 1;
                    return;
                }

                start = ++comma;
                while (++comma < str.Length)
                {
                    c = Unsafe.Add(ref MemoryMarshal.GetReference(str), comma);
                    if (c == '>')
                        break;
                }

                if (!double.TryParse(str[start..comma], NumberStyles.Float, Utils.EnUsCulture, out s))
                {
                    z = y = x = 0;
                    s = 1;
                    return;
                }
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

                if ((angle + 1.0) > 0.0005)
                {
                    if ((1f - angle) >= 0.0005)
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
                if (o is Quaternion qo)
                    return x == qo.x && y == qo.y && z == qo.z && s == qo.s;

                return false;
            }

            public override string ToString()
            {
                string st = String.Format(Culture.FormatProvider, "<{0:0.000000}, {1:0.000000}, {2:0.000000}, {3:0.000000}>", x, y, z, s);
                return st;
            }

            public static explicit operator string(Quaternion r)
            {
                string st = String.Format(Culture.FormatProvider, "<{0:0.000000}, {1:0.000000}, {2:0.000000}, {3:0.000000}>", r.x, r.y, r.z, r.s);
                return st;
            }

            public static explicit operator LSLString(Quaternion r)
            {
                string st = String.Format(Culture.FormatProvider, "<{0:0.000000}, {1:0.000000}, {2:0.000000}, {3:0.000000}>", r.x, r.y, r.z, r.s);
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

                OMV_Quaternion omvrot = new((float)rot.x, (float)rot.y, (float)rot.z, (float)rot.s);
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

            public static readonly Quaternion Identity = new(0, 0, 0, 1);
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
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    if (m_data is null)
                    {
                        m_data = new object[0];
                        return 0;
                    }
                    return m_data.Length;
                }
            }

            public int Size
            {
                get
                {
                    if (m_data is null)
                        return 0;

                    int size = IntPtr.Size * m_data.Length;

                    foreach (object o in m_data)
                    {
                       // here modern sugar alternatives with switch or switch statement generate crap code on dotnet6
                       if (o is null) // this explicit test does improve release jit code, otherwise it is present on ALL its
                           throw new Exception("null entry in List.Size");
                       else if (o is LSL_Types.LSLInteger)
                           size += sizeof(int);
                       else if (o is LSL_Types.LSLFloat)
                           size += sizeof(double);
                       else if (o is LSL_Types.LSLString lso)
                           size += lso.m_string is null ? 0 : lso.m_string.Length * sizeof(char);
                       else if (o is LSL_Types.key ko)
                           size += ko.value.Length;
                       else if (o is LSL_Types.Vector3)
                           size += 3 * sizeof(double);
                       else if (o is LSL_Types.Quaternion)
                           size += 4 * sizeof(double);
                       else if (o is int)
                           size += sizeof(int);
                       else if (o is uint)
                           size += sizeof(uint);
                       else if (o is string so)
                           size += so.Length * sizeof(char);
                       else if (o is float)
                           size += sizeof(float);
                       else if (o is double)
                           size += sizeof(double);
                       else if (o is list lo)
                           size += lo.Size;
                       else
                           throw new Exception("Unknown type in List.Size: " + o.GetType().ToString());
                    }
                    return size;
                }
            }

            public object[] Data
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    m_data ??= new object[0];
                    return m_data;
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set { m_data = value; }
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                object o = Data[itemIndex];
                if (o is LSL_Types.LSLFloat lfo)
                    return lfo;
                if (o is LSL_Types.LSLInteger lio)
                    return new LSL_Types.LSLFloat(lio.value);
                if (o is int io)
                    return new LSL_Types.LSLFloat(io);
                if (o is float fo)
                    return new LSL_Types.LSLFloat(fo);
                if (o is double dov)
                    return new LSL_Types.LSLFloat(dov);
                if (o is LSL_Types.LSLString lso)
                    return new LSL_Types.LSLFloat(lso.m_string);
                return (LSL_Types.LSLFloat)o;
            }

            public float GetFloatItem(int itemIndex)
            {
                object o = Data[itemIndex];
                if (o is LSL_Types.LSLFloat lfo)
                    return (float)lfo.value;
                if (o is LSL_Types.LSLInteger lio)
                    return lio.value;
                if (o is int io)
                    return io;
                if (o is float fo)
                    return fo;
                if (o is Double dov)
                    return (float)dov;
                if (o is LSL_Types.LSLString lso)
                    return Convert.ToSingle(lso.m_string);
                return Convert.ToSingle(o.ToString());
            }

            public float GetStrictFloatItem(int itemIndex)
            {
                object o = Data[itemIndex];
                if (o is LSL_Types.LSLFloat lfo)
                    return (float)lfo.value;
                if (o is LSL_Types.LSLInteger lio)
                    return lio.value;
                if (o is int io)
                    return io;
                if (o is float fo)
                    return fo;
                if (o is double dov)
                    return (float)dov;
                if (o is LSL_Types.LSLString lso)
                    return Convert.ToSingle(lso.m_string);
                throw new InvalidCastException(string.Format($"LSL float expected but {0} given", o is not null ? o.GetType().Name : "null"));
            }

            public LSL_Types.LSLString GetLSLStringItem(int itemIndex)
            {
                object o = Data[itemIndex];
                if (o is LSL_Types.key ko)
                    return ko;
                if (o is LSL_Types.LSLString lso)
                    return lso;
                if (o is string s)
                    return new LSL_Types.LSLString(s);
                return new LSL_Types.LSLString(o.ToString());
            }

            public string GetStringItem(int itemIndex)
            {
                object o = Data[itemIndex];
                if (o is LSL_Types.LSLString lso)
                    return lso.m_string;
                if (o is LSL_Types.key lk)
                    return (lk.value);
                if (o is string s)
                    return s;
                return o.ToString();
            }

            public LSL_Types.LSLString GetStrictLSLStringItem(int itemIndex)
            {
                object o = Data[itemIndex];
                if (o is LSL_Types.LSLString lso)
                    return lso;
                if (o is string so)
                    return new LSL_Types.LSLString(so);
                if (o is LSL_Types.key lko)
                    return new LSL_Types.LSLString(lko.value);

                throw new InvalidCastException(string.Format(
                    "{0} expected but {1} given",
                    typeof(LSL_Types.LSLString).Name,
                    o is not null ? o.GetType().Name : "null"));
            }

            public string GetStrictStringItem(int itemIndex)
            {
                object o = Data[itemIndex];
                if (o is LSL_Types.LSLString lso)
                    return lso.m_string;
                if (o is string s)
                    return s;
                if (o is LSL_Types.key lko)
                    return lko.value;

                throw new InvalidCastException(string.Format(
                    "{0} expected but {1} given",
                    typeof(LSL_Types.LSLString).Name,
                    o is not null ? o.GetType().Name : "null"));
            }

            public LSL_Types.LSLInteger GetLSLIntegerItem(int itemIndex)
            {
                object o = Data[itemIndex];
                if (o is LSL_Types.LSLInteger lio)
                    return lio;
                if (o is int io)
                    return new LSLInteger(io);
                if (o is LSL_Types.LSLFloat lfo)
                    return new LSLInteger((int)lfo.value);
                if (o is float fo)
                    return new LSLInteger((int)fo);
                if (o is double duo)
                    return new LSLInteger((int)duo);
                if (o is LSL_Types.LSLString lso)
                    return new LSLInteger(lso.m_string);

                throw new InvalidCastException(string.Format(
                    "{0} expected but {1} given",
                    typeof(LSL_Types.LSLInteger).Name,
                    o is not null ? o.GetType().Name : "null"));
            }

            public int GetIntegerItem(int itemIndex)
            {
                object o = Data[itemIndex];
                if (o is LSL_Types.LSLInteger lio)
                    return lio.value;
                if (o is int io)
                    return io;
                if (o is LSL_Types.LSLFloat lfo)
                    return (int)lfo.value;
                if (o is float fo)
                    return (int)fo;
                if (o is double duo)
                    return (int)duo;
                if (o is LSL_Types.LSLString lso)
                    return Convert.ToInt32(lso.m_string);

                throw new InvalidCastException(string.Format(
                    "{0} expected but {1} given",
                    typeof(LSL_Types.LSLInteger).Name,
                    o is not null ? o.GetType().Name : "null"));
            }

            public LSL_Types.Vector3 GetVector3Item(int itemIndex)
            {
                object o = Data[itemIndex];
                if (o is LSL_Types.Vector3 vo)
                    return vo;
                if (o is OMV_Vector3 ov)
                    return new LSL_Types.Vector3(ov);

                throw new InvalidCastException(string.Format(
                    "{0} expected but {1} given",
                    typeof(LSL_Types.Vector3).Name,
                    o != null ? o.GetType().Name : "null"));
            }

            // use LSL_Types.Quaternion to parse and store a vector4 for lightShare
            public LSL_Types.Quaternion GetVector4Item(int itemIndex)
            {
                object o = Data[itemIndex];
                if (o is LSL_Types.Quaternion q)
                    return q;

                if (o is OMV_Quaternion qo)
                {
                    qo.Normalize();
                    return new LSL_Types.Quaternion(qo); ;
                }

                throw new InvalidCastException(string.Format(
                    "{0} expected but {1} given",
                    typeof(LSL_Types.Quaternion).Name,
                    o != null ?
                    o.GetType().Name : "null"));
            }

            public LSL_Types.Quaternion GetQuaternionItem(int itemIndex)
            {
                object o = Data[itemIndex];
                if (o is LSL_Types.Quaternion q)
                {
                    q.Normalize();
                    return q;
                }
                if (o is OMV_Quaternion oq)
                {
                    oq.Normalize();
                    return new LSL_Types.Quaternion(oq);
                }

                throw new InvalidCastException(string.Format(
                        "{0} expected but {1} given",
                        typeof(LSL_Types.Quaternion).Name,
                        Data[itemIndex] != null ?
                        Data[itemIndex].GetType().Name : "null"));
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                if (m_data is null || m_data.Length == 0)
                {
                    tmp = new object[] {o};
                }
                else
                {
                    tmp = new object[m_data.Length + 1];
                    m_data.CopyTo(tmp, 0);
                    tmp.SetValue(o, tmp.Length - 1);
                }
                m_data = tmp;
            }

            public object this[int i]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    if(m_data is null || i >= m_data.Length)
                        return null;
                    return m_data[i];
                }
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                set
                {
                    if (m_data is null || i >= m_data.Length)
                        return;
                    m_data[i] = value;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator ==(list a, list b)
            {
                if (b is null)
                    return (a is null);
                return a is not null && a.Length == b.Length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool operator !=(list a, list b)
            {
                if (b is null)
                    return a is not null;
                return (a is null) || a.Length != b.Length;
            }

            public void Add(object o)
            {
                object[] tmp;
                if (m_data is null || m_data.Length == 0)
                {
                    tmp = new object[] { o };
                }
                else
                {
                    tmp = new object[m_data.Length + 1];
                    m_data.CopyTo(tmp, 0);
                    tmp[m_data.Length] = o; // Since this is tmp.Length - 1
                }
                m_data = tmp;
            }

            public bool Contains(object o)
            {
                if (m_data is null)
                    return false;
                foreach (object i in m_data)
                {
                    if (i == o)
                        return true;
                }
                return false;
            }

            public list DeleteSublist(int start, int end)
            {
                // Not an easy one
                // If start <= end, remove that part
                // if either is negative, count from the end of the array
                // if the resulting start > end, keep [end + 1, start - 1]
                if (m_data is null || m_data.Length == 0)
                    return new list(new object[0]);

                int len = m_data.Length;
                object[] ret;

                if (start < 0)
                    start = len + start;
                if (start < 0)
                    start = 0;

                if (end < 0)
                    end = len + end;
                if (end < 0)
                    end = 0;

                if (start > end)
                {
                    end++;
                    if (end >= len)
                        return new list(new object[0]);

                    start--;
                    if (start >= len)
                        start = len - 1;

                    int num = start - end + 1;
                    if (num <= 0)
                        return new list(new object[0]);

                    ret = new object[num];
                    Array.Copy(m_data, end, ret, 0, num);
                    return new list(ret);
                }

                // start >= 0 && end >= 0 here
                if (start >= len)
                {
                    ret = new object[len];
                    Array.Copy(m_data, 0, ret, 0, len);
                    return new list(ret);
                }

                if (end >= len)
                    end = len - 1;

                end++;

                // now, this makes the math easier
                int remove = end - start;

                if (remove >= len)
                    return new list(new object[0]);

                ret = new object[len - remove];

                if (start > 0)
                    Array.Copy(m_data, 0, ret, 0, start);

                if (end >= len)
                    return new list(ret);

                Array.Copy(m_data, end, ret, start, len - end);

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
                    start += Data.Length;

                if (end < 0)
                    end += Data.Length;

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
                        return new list();

                    // Sublist extends beyond the end of the supplied list
                    if (end >= Data.Length)
                        end = Data.Length - 1;

                    // Sublist still starts before the beginning of the list
                    if (start < 0)
                        start = 0;

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
                        result = GetSublist(0, end);
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

            // compare for ??ListFindList* functions
            public static bool ListFind_areEqual(object l, object r)
            {
                if (l is null || r is null)
                    return false;

                if (l is LSLInteger lli)
                {
                    if (r is LSLInteger rli)
                        return lli.value == rli.value;
                    if (r is int ri)
                        return lli.value == ri;
                    return false;
                    }

                if (l is int li)
                {
                    if (r is LSLInteger rli)
                        return li == rli.value;
                    if (r is int ri)
                        return li == ri;
                    return false;
                    }

                if (l is LSLFloat llf)
                {
                    if (r is LSLFloat rlf)
                        return llf.value == rlf.value;
                    if (r is float rf)
                        return llf.value == (double)rf;
                    if (r is double rd)
                        return llf.value == rd;
                    return false;
                    }
                if (l is double ld)
                {
                    if (r is LSL_Types.LSLFloat rlf)
                        return ld == rlf.value;
                    if (r is float rf)
                        return ld == (double)rf;
                    if (r is double rd)
                        return ld == rd;
                    return false;
                    }
                if (l is float lf)
                {
                    if (r is LSLFloat rlf)
                        return lf == (float)rlf.value;
                    if (r is float rf)
                        return lf == rf;
                    if (r is double rd)
                            return lf == (float)rd;
                    return false;
                    }

                if (l is LSLString lls)
                {
                    if (r is LSLString rls)
                        return lls.m_string.Equals(rls.m_string, StringComparison.Ordinal);
                    if (r is string rs)
                        return lls.m_string.Equals(rs, StringComparison.Ordinal);
                        return false;
                    }

                if (l is string ls)
                {
                    if (r is LSLString rls)
                        return ls.Equals(rls.m_string, StringComparison.Ordinal);
                    if (r is string rs)
                        return ls.Equals(rs, StringComparison.Ordinal);
                    if (r is LSL_Types.key rlk)
                        return ls.Equals(rlk.value, StringComparison.OrdinalIgnoreCase);
                    return false;
                }

                if(l is key llk)
                {
                    if (r is key rlk)
                        return llk.value.Equals(rlk.value, StringComparison.OrdinalIgnoreCase);
                    if (r is string rk)
                        return llk.value.Equals(rk, StringComparison.OrdinalIgnoreCase);
                }

                if (l is Vector3 llv)
                {
                    if(r is Vector3 rlv)
                        return llv.Equals(rlv);
                    return false;
                }

                if (l is Quaternion llr)
                {
                    if(r is Quaternion rlr)
                        return llr.Equals(rlr);
                    return false;
                }

                return false;
            }

            private static int compare(object left, object right, bool ascending)
            {
                if(left is null)
                    return 0;

                if (!left.GetType().Equals(right.GetType()))
                {
                    // unequal types are always "equal" for comparison purposes.
                    // this way, the bubble sort will never swap them, and we'll
                    // get that feathered effect we're looking for
                    return 0;
                }

                int ret;

                if (left is LSLInteger l)
                {
                    LSLInteger r = (LSLInteger)right;
                    ret = Math.Sign(l.value - r.value);
                }
                else if (left is LSLString lsl)
                {
                    LSLString r = (LSLString)right;
                    ret = string.CompareOrdinal(lsl.m_string, r.m_string);
                }
                else if (left is string ssl)
                {
                    ret =  string.CompareOrdinal(ssl, right as string);
                }
                else if (left is LSLFloat fl)
                {
                    LSLFloat r = (LSLFloat)right;
                    ret = Math.Sign(fl.value - r.value);
                }
                else if (left is Vector3 vl)
                {
                    Vector3 r = (Vector3)right;
                    ret = Math.Sign(Vector3.Mag(vl) - Vector3.Mag(r));
                }
                else if (left is key kl)
                {
                    key r = (key)right;
                    ret = string.CompareOrdinal(kl.value, r.value);
                }
                else //if (left is Quaternion) and unknown types
                {
                    return 0;
                }

                return ascending ? ret : -ret;
            }

            class HomogeneousComparer : IComparer
            {
                //both sides known to be of same type
                private readonly bool ascending;
                public HomogeneousComparer(bool ascend)
                {
                    ascending = ascend;
                }

                public int Compare(object left, object right)
                {
                    if (left is null)
                        return 0;

                    int ret;
                    if (left is LSLInteger il)
                    {
                        LSLInteger r = (LSLInteger)right;
                        ret = Math.Sign(il.value - r.value);
                    }
                    else if (left is LSLString lsl)
                    {
                        LSLString r = (LSLString)right;
                        ret = string.CompareOrdinal(lsl.m_string, r.m_string);
                    }
                    else if (left is string ssl)
                    {
                        ret = string.CompareOrdinal(ssl, right as string);
                    }
                    else if (left is LSLFloat fl)
                    {
                        LSLFloat r = (LSLFloat)right;
                        ret = Math.Sign(fl.value - r.value);
                    }
                    else if (left is Vector3 vl)
                    {
                        Vector3 r = (Vector3)right;
                        ret = Math.Sign(Vector3.MagSquare(vl) - Vector3.MagSquare(r));
                    }
                    else if (left is key kl)
                    {
                        key r = (key)right;
                        ret = string.CompareOrdinal(kl.value, r.value);
                    }
                    else //if (left is Quaternion) and unknown types
                    {
                        return 0;
                    }

                    if (ascending)
                        return ret;

                    return -ret;
                }
            }

            private static bool needSwapAscending(object left, object right)
            {
                if (left is null)
                    return false;

                if (left is LSLInteger li)
                {
                    LSLInteger r = (LSLInteger)right;
                    return li.value > r.value;
                }
                if (left is LSLString lsl)
                {
                    LSLString r = (LSLString)right;
                    return string.CompareOrdinal(lsl.m_string, r.m_string) > 0;
                }
                else if (left is string ssl)
                {
                    return string.CompareOrdinal(ssl, right as string) > 0;
                }
                if (left is LSLFloat lf)
                {
                    LSLFloat r = (LSLFloat)right;
                    return lf.value > r.value;
                }
                if (left is Vector3 lv)
                {
                    Vector3 r = (Vector3)right;
                    return Vector3.MagSquare(lv) > Vector3.MagSquare(r);
                }
                if (left is key lk)
                {
                    key r = (key)right;
                    return string.CompareOrdinal(lk.value, r.value) > 0;
                }
                return false;
            }

            private static bool needSwapDescending(object left, object right)
            {
                if (left is null)
                    return false;

                if (left is LSLInteger li)
                {
                    LSLInteger r = (LSLInteger)right;
                    return li.value < r.value;
                }
                if (left is LSLString lsl)
                {
                    LSLString r = (LSLString)right;
                    return string.CompareOrdinal(lsl.m_string, r.m_string) < 0;
                }
                else if (left is string ssl)
                {
                    string sr = right as string;
                    return string.CompareOrdinal(ssl, sr) < 0;
                }
                if (left is LSLFloat lf)
                {
                    LSLFloat r = (LSLFloat)right;
                    return lf.value < r.value;
                }
                if (left is Vector3 lv)
                {
                    Vector3 r = (Vector3)right;
                    return Vector3.MagSquare(lv) < Vector3.MagSquare(r);
                }
                if (left is key lk)
                {
                    key r = (key)right;
                    return string.CompareOrdinal(lk.value, r.value) < 0;
                }
                return false;
            }

            public list Sort(int stride, bool ascending)
            {
                if (m_data is null)
                    return new list(); // Don't even bother

                int len = m_data.Length;
                if (len == 0)
                    return new list(); // Don't even bother

                object[] ret = new object[len];
                m_data.CopyTo(ret, 0);

                if (stride < 1)
                    stride = 1;

                if ((len <= stride) || (len % stride) != 0)
                    return new list(ret);

                Sort(ret, stride, ascending);
                return new list(ret);
            }

            public list Sort(int stride, int stride_index, bool ascending)
            {
                if (m_data is null)
                    return new list(); // Don't even bother

                int len = m_data.Length;
                if (len == 0)
                    return new list(); // Don't even bother

                if (stride < 1)
                    stride = 1;

                if (stride_index < 0)
                {
                    stride_index += stride;
                    if (stride_index < 0)
                        return new list();
                }
                else if (stride_index >= stride)
                    return new list();

                object[] ret = new object[len];
                m_data.CopyTo(ret, 0);

                if (len <= stride)
                    return new list(ret);

                if (stride == 1)
                {
                    Sort(ret, stride, ascending);
                    return new list(ret);
                }

                if (len % stride == 0)
                {
                    if (stride_index == 0)
                        Sort(ret, stride, ascending);
                    else
                        Sort(ret, stride, stride_index, ascending);
                }
                return new list(ret);
            }

            public void SortInPlace(int stride, bool ascending)
            {
                if (m_data is null)
                    return; // Don't even bother

                if (stride < 1)
                    stride = 1;

                int len = m_data.Length;
                if ((len <= stride) || (len % stride) != 0)
                    return;

                Sort(m_data, stride, ascending);
            }

            public void SortInPlace(int stride, int stride_index, bool ascending)
            {
                if (m_data is null)
                    return; // Don't even bother

                if (stride < 1)
                    stride = 1;

                if (stride_index < 0)
                {
                    stride_index += stride;
                    if (stride_index < 0)
                        return;
                }
                else if (stride_index >= stride)
                    return;

                int len = m_data.Length;
                if ((len <= stride) || (len % stride) != 0)
                    return;

                if(stride_index == 0)
                    Sort(m_data, stride, ascending);
                else
                    Sort(m_data, stride,stride_index, ascending);
            }

            public void Sort(object[] ret, int stride, bool ascending)
            {
                // if list does not consists of homogeneous types
                // and because of the desired type specific feathered sorting behavior
                // requeried by the spec, we MUST use a non-optimized bubble sort
                // Anything else will give you the incorrect behavior.

                // we can optimize the case where stride == 1
                if (stride == 1)
                {
                    bool homogeneous = true;
                    Type firstType = ret[0].GetType();
                    for (int i = 1; i < ret.Length; ++i)
                    {
                        if (!firstType.Equals(ret[i].GetType()))
                        {
                            homogeneous = false;
                            break;
                        }
                    }

                    if (homogeneous)
                        // big boost using native Sort with its faster sorting methods.
                        Array.Sort(ret, new HomogeneousComparer(ascending));
                    else
                    {
                        if (ascending)
                        {
                            for (int i = 0; i < ret.Length - 1; ++i)
                            {
                                object pivot = ret[i];
                                Type pivotType = pivot.GetType();
                                for (int j = i + 1; j < ret.Length; ++j)
                                {
                                    object tmp = ret[j];
                                    if (pivotType.Equals(tmp.GetType()) && needSwapAscending(pivot, tmp))
                                    {
                                        ret[j] = pivot;
                                        pivot = tmp;
                                    }
                                }
                                ret[i] = pivot;
                            }
                        }
                        else
                        {
                            for (int i = 0; i < ret.Length - 1; ++i)
                            {
                                object pivot = ret[i];
                                Type pivotType = pivot.GetType();
                                for (int j = i + 1; j < ret.Length; ++j)
                                {
                                    object tmp = ret[j];
                                    if (pivotType.Equals(tmp.GetType()) && needSwapDescending(pivot, tmp))
                                    {
                                        ret[j] = pivot;
                                        pivot = tmp;
                                    }
                                }
                                ret[i] = pivot;
                            }
                        }
                    }
                    return;
                }

                if (ascending)
                {
                    for (int i = 0; i < ret.Length - stride; i += stride)
                    {
                        object pivot = ret[i];
                        Type pivotType = pivot.GetType();
                        for (int j = i + stride; j < ret.Length; j += stride)
                        {
                            object tmp = ret[j];
                            if (tmp.GetType() == pivotType && needSwapAscending(pivot, tmp))
                            {
                                ret[j] = pivot;
                                pivot = tmp;

                                int ik = i;
                                int end = ik + stride - 1;
                                int jk = j;
                                while (ik < end)
                                {
                                    ++ik;
                                    ++jk;
                                    tmp = ret[ik];
                                    ret[ik] = ret[jk];
                                    ret[jk] = tmp;
                                }
                            }
                        }
                        ret[i] = pivot;
                    }
                }
                else
                {
                    for (int i = 0; i < ret.Length - stride; i += stride)
                    {
                        object pivot = ret[i];
                        Type pivotType = pivot.GetType();
                        for (int j = i + stride; j < ret.Length; j += stride)
                        {
                            object tmp = ret[j];
                            if (tmp.GetType() == pivotType && needSwapDescending(pivot, tmp))
                            {
                                ret[j] = pivot;
                                pivot = tmp;

                                int ik = i;
                                int end = ik + stride - 1;
                                int jk = j;
                                while (ik < end)
                                {
                                    ++ik;
                                    ++jk;
                                    tmp = ret[ik];
                                    ret[ik] = ret[jk];
                                    ret[jk] = tmp;
                                }
                            }
                        }
                        ret[i] = pivot;
                    }
                }
            }

            public void Sort(object[] ret, int stride, int stride_index, bool ascending)
            {
                if (ascending)
                {
                    for (int i = stride_index; i < ret.Length - stride; i += stride)
                    {
                        object pivot = ret[i];
                        Type pivotType = pivot.GetType();
                        for (int j = i + stride; j < ret.Length; j += stride)
                        {
                            object tmp = ret[j];
                            if (pivotType.Equals(tmp.GetType()) && needSwapAscending(pivot, tmp))
                            {
                                pivot = tmp;
                                int ik = i - stride_index;
                                int end = ik + stride;
                                int jk = j - stride_index;
                                while (ik < end)
                                {
                                    tmp = ret[ik];
                                    ret[ik] = ret[jk];
                                    ret[jk] = tmp;
                                    ++ik;
                                    ++jk;
                                }
                            }
                        }
                    }
                }
                else
                {
                    for (int i = stride_index; i < ret.Length - stride; i += stride)
                    {
                        object pivot = ret[i];
                        Type pivotType = pivot.GetType();
                        for (int j = i + stride; j < ret.Length; j += stride)
                        {
                            object tmp = ret[j];
                            if (pivotType.Equals(tmp.GetType()) && needSwapDescending(pivot, tmp))
                            {
                                pivot = tmp;
                                int ik = i - stride_index;
                                int end = ik + stride;
                                int jk = j - stride_index;
                                while (ik < end)
                                {
                                    tmp = ret[ik];
                                    ret[ik] = ret[jk];
                                    ret[jk] = tmp;
                                    ++ik;
                                    ++jk;
                                }
                            }
                        }
                    }
                }
            }

            #region CSV Methods

            public static list FromCSV(string csv)
            {
                return new list(csv.Split(','));
            }

            public string ToCSV()
            {
                if (m_data is null || m_data.Length == 0)
                    return string.Empty;

                object o = m_data[0];
                int len = m_data.Length;
                if (len == 1)
                    return o.ToString();

                StringBuilder sb = osStringBuilderCache.Acquire();
                sb.Append(o.ToString());
                for (int i = 1; i < len; i++)
                {
                    sb.Append(',');
                    sb.Append(o.ToString());
                }
                return osStringBuilderCache.GetStringAndRelease(sb);
            }

            private string ToSoup()
            {
                if (m_data is null || m_data.Length == 0)
                    return string.Empty;

                StringBuilder sb = osStringBuilderCache.Acquire();
                foreach (object o in m_data)
                {
                    sb.Append(o.ToString());
                }
                return osStringBuilderCache.GetStringAndRelease(sb);
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
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(m_data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out double entry))
                    {
                        if (entry < minimum) minimum = entry;
                    }
                }
                return minimum;
            }

            public double Max()
            {
                double maximum = double.NegativeInfinity;
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(m_data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out double entry))
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
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(m_data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out _))
                    {
                        count++;
                    }
                }
                return count;
            }

            public static list ToDoubleList(list src)
            {
                list ret = new();
                for (int i = 0; i < src.Data.Length; i++)
                {
                    if (double.TryParse(src.m_data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out double entry))
                    {
                        ret.Add(entry);
                    }
                }
                return ret;
            }

            public double Sum()
            {
                double sum = 0;
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(m_data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out double entry))
                    {
                        sum += entry;
                    }
                }
                return sum;
            }

            public double SumSqrs()
            {
                double sum = 0;
                for (int i = 0; i < Data.Length; i++)
                {
                    if (double.TryParse(m_data[i].ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out double entry))
                    {
                        sum += (entry * entry);
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
                if (m_data is null || m_data.Length == 0)
                    return "[]";

                StringBuilder sb = osStringBuilderCache.Acquire();
                int len = m_data.Length;
                int last = len - 1;
                object o;

                sb.Append('[');
                for (int i = 0; i < len; i++)
                {
                    o = m_data[i];
                    if (o is string so)
                    {
                        sb.Append('\"');
                        sb.Append(so);
                        sb.Append('\"');
                    }
                    else
                    {
                        sb.Append(o.ToString());
                    }
                    if (i < last)
                        sb.Append(',');
                }
                sb.Append(']');
                return osStringBuilderCache.GetStringAndRelease(sb);
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
                    if (!double.TryParse(x.ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out double a))
                    {
                        a = 0.0;
                    }
                    if (!double.TryParse(y.ToString(), NumberStyles.Float, Culture.NumberFormatInfo, out double b))
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

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public override bool Equals(object o)
            {
                if (o is list lo)
                    return Data.Length == lo.Data.Length;
                return false;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
                Regex isuuid = new(@"^[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}$", RegexOptions.Compiled);
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
                Regex isuuid = new(@"^[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}$", RegexOptions.Compiled);
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
                return k1.value.Equals(k2.value);
            }
            public static bool operator !=(key k1, key k2)
            {
                return !k1.value.Equals(k2.value);
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
            public static readonly key NullKey = new("00000000-0000-0000-0000-000000000000");
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

            public LSLString(ReadOnlySpan<char> s)
            {
                m_string = s.ToString();
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

            public LSLString(LSLInteger i) : this(i.value) { }

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

            static public implicit operator string(LSLString s)
            {
                return s.m_string;
            }

            static public implicit operator LSLString(string s)
            {
                return new LSLString(s);
            }

            static public implicit operator LSLString(ReadOnlySpan<char> s)
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
                return new LSLString( b ? "1" : "0");
            }

            public static implicit operator Vector3(LSLString s)
            {
                return new Vector3(s);
            }

            public static implicit operator Quaternion(LSLString s)
            {
                return new Quaternion(s);
            }

            public static implicit operator LSLFloat(LSLString s)
            {
                return new LSLFloat(s);
            }

            public static implicit operator list(LSLString s)
            {
                return new list(new object[] { s });
            }

            public static implicit operator ReadOnlySpan<char>(LSLString s)
            {
                return s.m_string.AsSpan();
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
            public static readonly LSLString Empty = new(string.Empty);
            public static readonly LSLString NullKey = new("00000000-0000-0000-0000-000000000000");

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

            public LSLInteger(uint i)
            {
                value = (int)i;
            }

            public LSLInteger(double d)
            {
                value = (int)d;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LSLInteger(string s) : this(MemoryExtensions.AsSpan(s)) { }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public LSLInteger(LSLString s) : this(MemoryExtensions.AsSpan(s.m_string)) { }
            public LSLInteger(ReadOnlySpan<char> s)
            {
                value = 0;
                if (s.Length == 0)
                    return;

                int indx = 0;
                char c;
                bool neg = false;
                int rc;
                try
                {
                    do
                    {
                        c = Unsafe.Add(ref MemoryMarshal.GetReference(s), indx);
                        if (c != ' ')
                            break;
                    }
                    while (++indx < s.Length);

                    if (c == '0')
                    {
                        if (++indx >= s.Length)
                            return;
                        c = Unsafe.Add(ref MemoryMarshal.GetReference(s), indx);
                        if (c == 'x' || c == 'X')
                        {
                            uint uvalue = 0;
                            while (++indx < s.Length)
                            {
                                c = Unsafe.Add(ref MemoryMarshal.GetReference(s), indx);
                                rc = Utils.HexNibbleWithChk(c);
                                if (rc < 0)
                                    break;
                                checked
                                {
                                    uvalue *= 16;
                                    uvalue += (uint)rc;
                                }
                            }
                            value = (int)uvalue;
                            return;
                        }
                    }
                    else if (c == '+')
                    {
                        if (++indx >= s.Length)
                            return;
                        c = Unsafe.Add(ref MemoryMarshal.GetReference(s), indx);
                    }
                    else if (c == '-')
                    {
                        if (++indx >= s.Length)
                            return;

                        neg = true;
                        c = Unsafe.Add(ref MemoryMarshal.GetReference(s), indx);
                    }

                    while (c >= '0' && c <= '9')
                    {
                        rc = c - '0';
                        checked
                        {
                            value *= 10;
                            value += rc;
                        }
                        if(++indx >= s.Length)
                            break;
                        c = Unsafe.Add(ref MemoryMarshal.GetReference(s), indx);
                    }

                    if(neg)
                        value = -value;
                    return;
                }
                catch (OverflowException)
                {
                    value = -1;
                }
                catch
                {
                    value = 0;
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
                try
                {
                    return new LSLInteger(i1.value / i2);
                }
                catch (DivideByZeroException)
                {
                    throw new ScriptException("Integer division by Zero");
                }
            }

            static public LSLInteger operator %(LSLInteger i1, int i2)
            {
                try
                {
                    return new LSLInteger(i1.value % i2);
                }
                catch (DivideByZeroException)
                {
                    throw new ScriptException("Integer division by Zero");
                }
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
                if (o is LSLInteger lio)
                    return value == lio.value;

                if (o is int io)
                    return value == io;

                return false;
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

            static public LSLInteger operator /(LSLInteger i1, LSLInteger i2)
            {
                try
                {
                    int ret = i1.value / i2.value;
                    return ret;
                }
                catch (DivideByZeroException)
                {
                    throw new ScriptException("Integer division by Zero");
                }
            }

            static public LSLInteger operator %(LSLInteger i1, LSLInteger i2)
            {
                try
                {
                    int ret = i1.value % i2.value;
                    return ret;
                }
                catch (DivideByZeroException)
                {
                    throw new ScriptException("Integer division by Zero");
                }
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

            public static LSLInteger operator <<(LSLInteger i, int s)
            {
                return i.value << s;
            }

            public static LSLInteger operator >>(LSLInteger i, int s)
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

            public static readonly LSLInteger Zero = new(0);
            #endregion
        }

        [Serializable]
        public struct LSLFloat
        {
            public double value;

            #region Constructors

            public LSLFloat(int i)
            {
                value = i;
            }

            public LSLFloat(double d)
            {
                value = d;
            }

            public LSLFloat(string s) : this(MemoryExtensions.AsSpan(s)) { }
            public LSLFloat(LSLString s) : this(MemoryExtensions.AsSpan(s.m_string)) { }

            public LSLFloat(ReadOnlySpan<char> s)
            {
                if (!double.TryParse(s, NumberStyles.Float, Culture.NumberFormatInfo, out value))
                    value = 0;
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
                return (uint)Math.Abs(f.value);
            }

            static public explicit operator string(LSLFloat f)
            {
                return string.Format(Culture.FormatProvider, "{0:0.000000}", f.value);
            }

            static public explicit operator LSLString(LSLFloat f)
            {
                return new LSLString(string.Format(Culture.FormatProvider, "{0:0.000000}", f.value));
            }

            static public implicit operator Boolean(LSLFloat f)
            {
                return f.value != 0.0;
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
                return  b ? new LSLFloat(1.0) :  new LSLFloat(0.0);
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
                double r = f.value / (double)i;
                if (IsBadNumber(r))
                    throw new ScriptException("Float division by zero");
                return new LSLFloat(r);
            }

            static public LSLFloat operator %(LSLFloat f, int i)
            {
                double r = f.value % (double)i;
                if (IsBadNumber(r))
                    throw new ScriptException("Float division by zero");
                return new LSLFloat(r);
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
                double r = lhs.value / rhs.value;
                if (IsBadNumber(r))
                    throw new ScriptException("Float division by zero");
                return new LSLFloat(r);
            }

            static public LSLFloat operator %(LSLFloat lhs, LSLFloat rhs)
            {
                double r = lhs.value % rhs.value;
                if (IsBadNumber(r))
                    throw new ScriptException("Float division by zero");
                return new LSLFloat(r);
            }

            static public LSLFloat operator -(LSLFloat f)
            {
                return new LSLFloat(-f.value);
            }

            static public implicit operator double(LSLFloat f)
            {
                return f.value;
            }

            public static readonly LSLFloat Zero = new(0);

            #endregion

            #region Overriders

            public override string ToString()
            {
                return string.Format(Culture.FormatProvider, "{0:0.000000}", this.value);
            }

            public override bool Equals(Object o)
            {
                if (o is LSLFloat fo)
                    return value == fo.value;
                return false;
            }

            public override int GetHashCode()
            {
                return value.GetHashCode();
            }

            #endregion
        }
    }
}
