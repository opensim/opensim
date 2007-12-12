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
            public string ToString()
            {
                return "<" + x.ToString() + ", " + y.ToString() + ", " + z.ToString() + ">";
            }
            public static Vector3 operator *(Vector3 v, float f)
            {
                v.x = v.x * f;
                v.y = v.y * f;
                v.z = v.z * f;
                return v;
            }
            public static Vector3 operator /(Vector3 v, float f)
            {
                v.x = v.x / f;
                v.y = v.y / f;
                v.z = v.z / f;
                return v;
            }
            public static Vector3 operator /(float f, Vector3 v)
            {
                v.x = v.x / f;
                v.y = v.y / f;
                v.z = v.z / f;
                return v;
            }
            public static Vector3 operator *(float f, Vector3 v)
            {
                v.x = v.x * f;
                v.y = v.y * f;
                v.z = v.z * f;
                return v;
            }
            public static Vector3 operator *(Vector3 v1, Vector3 v2)
            {
                v1.x = v1.x * v2.x;
                v1.y = v1.y * v2.y;
                v1.z = v1.z * v2.z;
                return v1;
            }
            public static Vector3 operator +(Vector3 v1, Vector3 v2)
            {
                v1.x = v1.x + v2.x;
                v1.y = v1.y + v2.y;
                v1.z = v1.z + v2.z;
                return v1;
            }
            public static Vector3 operator -(Vector3 v1, Vector3 v2)
            {
                v1.x = v1.x - v2.x;
                v1.y = v1.y - v2.y;
                v1.z = v1.z - v2.z;
                return v1;
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
        }

        [Serializable]
        public struct Quaternion
        {
            public double x;
            public double y;
            public double z;
            public double r;

            public Quaternion(Quaternion Quat)
            {
                x = (float) Quat.x;
                y = (float) Quat.y;
                z = (float) Quat.z;
                r = (float) Quat.r;
            }

            public Quaternion(double X, double Y, double Z, double R)
            {
                x = X;
                y = Y;
                z = Z;
                r = R;
            }
            public string ToString()
            {
                return "<" + x.ToString() + ", " + y.ToString() + ", " + z.ToString() + ", " + r.ToString() + ">";
            }
        }
    }
}
