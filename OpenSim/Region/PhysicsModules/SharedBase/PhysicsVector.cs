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

namespace OpenSim.Region.PhysicsModules.SharedBase
{
    /*public class PhysicsVector
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3()
        {
        }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3(Vector3 pv) : this(pv.X, pv.Y, pv.Z)
        {
        }
        
        public void setValues(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static readonly PhysicsVector Zero = new PhysicsVector(0f, 0f, 0f);

        public override string ToString()
        {
            return "<" + X + "," + Y + "," + Z + ">";
        }

        /// <summary>
        /// These routines are the easiest way to store XYZ values in an Vector3 without requiring 3 calls.
        /// </summary>
        /// <returns></returns>
        public byte[] GetBytes()
        {
            byte[] byteArray = new byte[12];

            Buffer.BlockCopy(BitConverter.GetBytes(X), 0, byteArray, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(Y), 0, byteArray, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(Z), 0, byteArray, 8, 4);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(byteArray, 0, 4);
                Array.Reverse(byteArray, 4, 4);
                Array.Reverse(byteArray, 8, 4);
            }

            return byteArray;
        }

        public void FromBytes(byte[] byteArray, int pos)
        {
            byte[] conversionBuffer = null;
            if (!BitConverter.IsLittleEndian)
            {
                // Big endian architecture
                if (conversionBuffer == null)
                    conversionBuffer = new byte[12];

                Buffer.BlockCopy(byteArray, pos, conversionBuffer, 0, 12);

                Array.Reverse(conversionBuffer, 0, 4);
                Array.Reverse(conversionBuffer, 4, 4);
                Array.Reverse(conversionBuffer, 8, 4);

                X = BitConverter.ToSingle(conversionBuffer, 0);
                Y = BitConverter.ToSingle(conversionBuffer, 4);
                Z = BitConverter.ToSingle(conversionBuffer, 8);
            }
            else
            {
                // Little endian architecture
                X = BitConverter.ToSingle(byteArray, pos);
                Y = BitConverter.ToSingle(byteArray, pos + 4);
                Z = BitConverter.ToSingle(byteArray, pos + 8);
            }
        }

        // Operations
        public static PhysicsVector operator +(Vector3 a, Vector3 b)
        {
            return new PhysicsVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static PhysicsVector operator -(Vector3 a, Vector3 b)
        {
            return new PhysicsVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static PhysicsVector cross(Vector3 a, Vector3 b)
        {
            return new PhysicsVector(a.Y*b.Z - a.Z*b.Y, a.Z*b.X - a.X*b.Z, a.X*b.Y - a.Y*b.X);
        }

        public float length()
        {
            return (float) Math.Sqrt(X*X + Y*Y + Z*Z);
        }

        public static float GetDistanceTo(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            float dz = a.Z - b.Z;
            return (float) Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public static PhysicsVector operator /(Vector3 v, float f)
        {
            return new PhysicsVector(v.X/f, v.Y/f, v.Z/f);
        }

        public static PhysicsVector operator *(Vector3 v, float f)
        {
            return new PhysicsVector(v.X*f, v.Y*f, v.Z*f);
        }

        public static PhysicsVector operator *(float f, Vector3 v)
        {
            return v*f;
        }

        public static bool isFinite(Vector3 v)
        {
            if (v == null)
                return false;
            if (Single.IsInfinity(v.X) || Single.IsNaN(v.X))
                return false;
            if (Single.IsInfinity(v.Y) || Single.IsNaN(v.Y))
                return false;
            if (Single.IsInfinity(v.Z) || Single.IsNaN(v.Z))
                return false;

            return true;
        }

        public virtual bool IsIdentical(Vector3 v, float tolerance)
        {
            PhysicsVector diff = this - v;
            float d = diff.length();
            if (d <= tolerance)
                return true;

            return false;
        }

    }*/
}
