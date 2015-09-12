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
using System.Diagnostics;
using System.Globalization;
using OpenMetaverse;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Region.PhysicsModule.ubODEMeshing;

public class Vertex : IComparable<Vertex>
{
    Vector3 vector;

    public float X
    {
        get { return vector.X; }
        set { vector.X = value; }
    }

    public float Y
    {
        get { return vector.Y; }
        set { vector.Y = value; }
    }

    public float Z
    {
        get { return vector.Z; }
        set { vector.Z = value; }
    }

    public Vertex(float x, float y, float z)
    {
        vector.X = x;
        vector.Y = y;
        vector.Z = z;
    }

    public Vertex normalize()
    {
        float tlength = vector.Length();
        if (tlength != 0f)
        {
            float mul = 1.0f / tlength;
            return new Vertex(vector.X * mul, vector.Y * mul, vector.Z * mul);
        }
        else
        {
            return new Vertex(0f, 0f, 0f);
        }
    }

    public Vertex cross(Vertex v)
    {
        return new Vertex(vector.Y * v.Z - vector.Z * v.Y, vector.Z * v.X - vector.X * v.Z, vector.X * v.Y - vector.Y * v.X);
    }

    // disable warning: mono compiler moans about overloading
    // operators hiding base operator but should not according to C#
    // language spec
#pragma warning disable 0108
    public static Vertex operator *(Vertex v, Quaternion q)
    {
        // From http://www.euclideanspace.com/maths/algebra/realNormedAlgebra/quaternions/transforms/

        Vertex v2 = new Vertex(0f, 0f, 0f);

        v2.X =   q.W * q.W * v.X +
            2f * q.Y * q.W * v.Z -
            2f * q.Z * q.W * v.Y +
                 q.X * q.X * v.X +
            2f * q.Y * q.X * v.Y +
            2f * q.Z * q.X * v.Z -
                 q.Z * q.Z * v.X -
                 q.Y * q.Y * v.X;

        v2.Y =
            2f * q.X * q.Y * v.X +
                 q.Y * q.Y * v.Y +
            2f * q.Z * q.Y * v.Z +
            2f * q.W * q.Z * v.X -
                 q.Z * q.Z * v.Y +
                 q.W * q.W * v.Y -
            2f * q.X * q.W * v.Z -
                 q.X * q.X * v.Y;

        v2.Z =
            2f * q.X * q.Z * v.X +
            2f * q.Y * q.Z * v.Y +
                 q.Z * q.Z * v.Z -
            2f * q.W * q.Y * v.X -
                 q.Y * q.Y * v.Z +
            2f * q.W * q.X * v.Y -
                 q.X * q.X * v.Z +
                 q.W * q.W * v.Z;

        return v2;
    }

    public static Vertex operator +(Vertex v1, Vertex v2)
    {
        return new Vertex(v1.X + v2.X, v1.Y + v2.Y, v1.Z + v2.Z);
    }

    public static Vertex operator -(Vertex v1, Vertex v2)
    {
        return new Vertex(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
    }

    public static Vertex operator *(Vertex v1, Vertex v2)
    {
        return new Vertex(v1.X * v2.X, v1.Y * v2.Y, v1.Z * v2.Z);
    }

    public static Vertex operator +(Vertex v1, float am)
    {
        v1.X += am;
        v1.Y += am;
        v1.Z += am;
        return v1;
    }

    public static Vertex operator -(Vertex v1, float am)
    {
        v1.X -= am;
        v1.Y -= am;
        v1.Z -= am;
        return v1;
    }

    public static Vertex operator *(Vertex v1, float am)
    {
        v1.X *= am;
        v1.Y *= am;
        v1.Z *= am;
        return v1;
    }

    public static Vertex operator /(Vertex v1, float am)
    {
        if (am == 0f)
        {
            return new Vertex(0f,0f,0f);
        }
        float mul = 1.0f / am;
        v1.X *= mul;
        v1.Y *= mul;
        v1.Z *= mul;
        return v1;
    }
#pragma warning restore 0108


    public float dot(Vertex v)
    {
        return X * v.X + Y * v.Y + Z * v.Z;
    }

    public Vertex(Vector3 v)
    {
        vector = v;
    }

    public Vertex Clone()
    {
        return new Vertex(X, Y, Z);
    }

    public static Vertex FromAngle(double angle)
    {
        return new Vertex((float) Math.Cos(angle), (float) Math.Sin(angle), 0.0f);
    }

    public float Length()
    {
        return vector.Length();
    }

    public virtual bool Equals(Vertex v, float tolerance)
    {
        Vertex diff = this - v;
        float d = diff.Length();
        if (d < tolerance)
            return true;

        return false;
    }


    public int CompareTo(Vertex other)
    {
        if (X < other.X)
            return -1;

        if (X > other.X)
            return 1;

        if (Y < other.Y)
            return -1;

        if (Y > other.Y)
            return 1;

        if (Z < other.Z)
            return -1;

        if (Z > other.Z)
            return 1;

        return 0;
    }

    public static bool operator >(Vertex me, Vertex other)
    {
        return me.CompareTo(other) > 0;
    }

    public static bool operator <(Vertex me, Vertex other)
    {
        return me.CompareTo(other) < 0;
    }

    public String ToRaw()
    {
        // Why this stuff with the number formatter?
        // Well, the raw format uses the english/US notation of numbers
        // where the "," separates groups of 1000 while the "." marks the border between 1 and 10E-1.
        // The german notation uses these characters exactly vice versa!
        // The Float.ToString() routine is a localized one, giving different results depending on the country
        // settings your machine works with. Unusable for a machine readable file format :-(
        NumberFormatInfo nfi = new NumberFormatInfo();
        nfi.NumberDecimalSeparator = ".";
        nfi.NumberDecimalDigits = 6;

        String s1 = X.ToString(nfi) + " " + Y.ToString(nfi) + " " + Z.ToString(nfi);

        return s1;
    }
}

public class Triangle
{
    public Vertex v1;
    public Vertex v2;
    public Vertex v3;

    public Triangle(Vertex _v1, Vertex _v2, Vertex _v3)
    {
        v1 = _v1;
        v2 = _v2;
        v3 = _v3;
    }

    public Triangle(float _v1x,float _v1y,float _v1z,
                    float _v2x,float _v2y,float _v2z,
                    float _v3x,float _v3y,float _v3z)
    {
        v1 = new Vertex(_v1x, _v1y, _v1z);
        v2 = new Vertex(_v2x, _v2y, _v2z);
        v3 = new Vertex(_v3x, _v3y, _v3z);
    }

    public override String ToString()
    {
        NumberFormatInfo nfi = new NumberFormatInfo();
        nfi.CurrencyDecimalDigits = 2;
        nfi.CurrencyDecimalSeparator = ".";

        String s1 = "<" + v1.X.ToString(nfi) + "," + v1.Y.ToString(nfi) + "," + v1.Z.ToString(nfi) + ">";
        String s2 = "<" + v2.X.ToString(nfi) + "," + v2.Y.ToString(nfi) + "," + v2.Z.ToString(nfi) + ">";
        String s3 = "<" + v3.X.ToString(nfi) + "," + v3.Y.ToString(nfi) + "," + v3.Z.ToString(nfi) + ">";

        return s1 + ";" + s2 + ";" + s3;
    }

    public Vector3 getNormal()
    {
        // Vertices

        // Vectors for edges
        Vector3 e1;
        Vector3 e2;

        e1 = new Vector3(v1.X - v2.X, v1.Y - v2.Y, v1.Z - v2.Z);
        e2 = new Vector3(v1.X - v3.X, v1.Y - v3.Y, v1.Z - v3.Z);

        // Cross product for normal
        Vector3 n = Vector3.Cross(e1, e2);

        // Length
        float l = n.Length();

        // Normalized "normal"
        n = n/l;

        return n;
    }

    public void invertNormal()
    {
        Vertex vt;
        vt = v1;
        v1 = v2;
        v2 = vt;
    }

    // Dumps a triangle in the "raw faces" format, blender can import. This is for visualisation and
    // debugging purposes
    public String ToStringRaw()
    {
        String output = v1.ToRaw() + " " + v2.ToRaw() + " " + v3.ToRaw();
        return output;
    }
}
