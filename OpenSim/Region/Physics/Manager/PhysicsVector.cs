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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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


namespace OpenSim.Region.Physics.Manager
{
    public class PhysicsVector
    {
        public float X;
        public float Y;
        public float Z;

        public PhysicsVector()
        {
        }

        public PhysicsVector(float x, float y, float z)
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

        // Operations
        public static PhysicsVector operator +(PhysicsVector a, PhysicsVector b)
        {
            return new PhysicsVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static PhysicsVector operator -(PhysicsVector a, PhysicsVector b)
        {
            return new PhysicsVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static PhysicsVector cross(PhysicsVector a, PhysicsVector b)
        {
            return new PhysicsVector(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }

        public float length()
        {
            return (float)Math.Sqrt(X*X + Y*Y + Z*Z);
        }

        public static PhysicsVector operator / (PhysicsVector v, float f)
        {
            return new PhysicsVector(v.X / f, v.Y / f, v.Z / f);
        }

        public static PhysicsVector operator *(PhysicsVector v, float f)
        {
            return new PhysicsVector(v.X * f, v.Y * f, v.Z * f);
        }

        public static PhysicsVector operator *(float f, PhysicsVector v)
        {
            return v * f;
        }

        public virtual bool IsIdentical(PhysicsVector v, float tolerance)
        {
            PhysicsVector diff = this - v;
            float d = diff.length();
            if (d < tolerance)
                return true;

            return false;
        }
    }
}