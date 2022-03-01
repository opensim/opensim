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
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using OpenMetaverse;

[StructLayout(LayoutKind.Sequential)]
public class Vertex : IComparable<Vertex>, IEquatable<Vertex>
{
    public float X;
    public float Y;
    public float Z;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vertex(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vertex(Vector3 v)
    {
        X = v.X;
        Y = v.Y;
        Z = v.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vertex Clone()
    {
        return new Vertex(X, Y, Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode()
    {
        int hash = X.GetHashCode();
        hash = Utils.CombineHash(hash, Y.GetHashCode());
        hash = Utils.CombineHash(hash, Z.GetHashCode());
        return hash;
    }

    public override bool Equals(object obj)
    {
        return (obj is Vertex) ? this == (Vertex)obj : false;
    }

    public bool Equals(Vertex other)
    {
        return this == other;
    }

    public bool Equals(Vertex v, float tolerance)
    {
        float x = X - v.X;
        float y = Y - v.Y;
        float z = Z - v.Z;

        double d = x * x + y * y + z * z;
        double t = tolerance * tolerance;
        return d < t;
    }

    public static bool operator ==(Vertex value1, Vertex value2)
    {
        return value1.X == value2.X
            && value1.Y == value2.Y
            && value1.Z == value2.Z;
    }

    public static bool operator !=(Vertex value1, Vertex value2)
    {
        return !(value1 == value2);
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

    // Dumps a triangle in the "raw faces" format, blender can import. This is for visualisation and
    // debugging purposes
    public String ToStringRaw()
    {
        String output = v1.ToRaw() + " " + v2.ToRaw() + " " + v3.ToRaw();
        return output;
    }
}
