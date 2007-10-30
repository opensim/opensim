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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using OpenSim.Region.Physics.Manager;

public class Vertex : IComparable<Vertex>
{
    public String name;
    public PhysicsVector point;

    public Vertex(String name, float x, float y, float z)
    {
        this.name = name;
        point = new PhysicsVector(x, y, z);
    }

    public int CompareTo(Vertex other)
    {
        if (point.X < other.point.X)
            return -1;

        if (point.X > other.point.X)
            return 1;

        if (point.Y < other.point.Y)
            return -1;

        if (point.Y > other.point.Y)
            return 1;

        if (point.Z < other.point.Z)
            return -1;

        if (point.Z > other.point.Z)
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
}

public class Simplex : IComparable<Simplex>
{
    public Vertex v1;
    public Vertex v2;

    public Simplex(Vertex _v1, Vertex _v2)
    {
        // Presort indices to make sorting (comparing) easier
        if (_v1 > _v2)
        {
            v1 = _v1;
            v2 = _v2;
        }
        else
        {
            v1 = _v2;
            v2 = _v1;
        }
    }

    public int CompareTo(Simplex other)
    {
        if (v1 > other.v1)
        {
            return 1;
        }
        if (v1 < other.v1)
        {
            return -1;
        }

        if (v2 > other.v2)
        {
            return 1;
        }
        if (v2 < other.v2)
        {
            return -1;
        }

        return 0;
    }
} ;

public class Triangle
{
    public Vertex v1;
    public Vertex v2;
    public Vertex v3;

    private float radius_square;
    private float cx;
    private float cy;

    public Triangle(Vertex _v1, Vertex _v2, Vertex _v3)
    {
        v1 = _v1;
        v2 = _v2;
        v3 = _v3;

        CalcCircle();
    }

    public bool isInCircle(float x, float y)
    {
        float dx, dy;
        float dd;

        dx = x - cx;
        dy = y - cy;

        dd = dx*dx + dy*dy;
        if (dd < radius_square)
            return true;
        else
            return false;
    }


    private void CalcCircle()
    {
        // Calculate the center and the radius of a circle given by three points p1, p2, p3
        // It is assumed, that the triangles vertices are already set correctly
        double p1x, p2x, p1y, p2y, p3x, p3y;

        // Deviation of this routine: 
        // A circle has the general equation (M-p)^2=r^2, where M and p are vectors
        // this gives us three equations f(p)=r^2, each for one point p1, p2, p3
        // putting respectively two equations together gives two equations
        // f(p1)=f(p2) and f(p1)=f(p3)
        // bringing all constant terms to one side brings them to the form
        // M*v1=c1 resp.M*v2=c2 where v1=(p1-p2) and v2=(p1-p3) (still vectors)
        // and c1, c2 are scalars (Naming conventions like the variables below)
        // Now using the equations that are formed by the components of the vectors
        // and isolate Mx lets you make one equation that only holds My
        // The rest is straight forward and eaasy :-)
        // 

        /* helping variables for temporary results */
        double c1, c2;
        double v1x, v1y, v2x, v2y;

        double z, n;

        double rx, ry;

        // Readout the three points, the triangle consists of
        p1x = v1.point.X;
        p1y = v1.point.Y;

        p2x = v2.point.X;
        p2y = v2.point.Y;

        p3x = v3.point.X;
        p3y = v3.point.Y;

        /* calc helping values first */
        c1 = (p1x*p1x + p1y*p1y - p2x*p2x - p2y*p2y)/2;
        c2 = (p1x*p1x + p1y*p1y - p3x*p3x - p3y*p3y)/2;

        v1x = p1x - p2x;
        v1y = p1y - p2y;

        v2x = p1x - p3x;
        v2y = p1y - p3y;

        z = (c1*v2x - c2*v1x);
        n = (v1y*v2x - v2y*v1x);

        if (n == 0.0) // This is no triangle, i.e there are (at least) two points at the same location
        {
            radius_square = 0.0f;
            return;
        }

        cy = (float) (z/n);

        if (v2x != 0.0)
        {
            cx = (float) ((c2 - v2y*cy)/v2x);
        }
        else if (v1x != 0.0)
        {
            cx = (float) ((c1 - v1y*cy)/v1x);
        }
        else
        {
            Debug.Assert(false, "Malformed triangle"); /* Both terms zero means nothing good */
        }

        rx = (p1x - cx);
        ry = (p1y - cy);

        radius_square = (float) (rx*rx + ry*ry);
    }

    public List<Simplex> GetSimplices()
    {
        List<Simplex> result = new List<Simplex>();
        Simplex s1 = new Simplex(v1, v2);
        Simplex s2 = new Simplex(v2, v3);
        Simplex s3 = new Simplex(v3, v1);

        result.Add(s1);
        result.Add(s2);
        result.Add(s3);

        return result;
    }

    public override String ToString()
    {
        NumberFormatInfo nfi = new NumberFormatInfo();
        nfi.CurrencyDecimalDigits = 2;
        nfi.CurrencyDecimalSeparator = ".";

        String s1 = "<" + v1.point.X.ToString(nfi) + "," + v1.point.Y.ToString(nfi) + "," + v1.point.Z.ToString(nfi) +
                    ">";
        String s2 = "<" + v2.point.X.ToString(nfi) + "," + v2.point.Y.ToString(nfi) + "," + v2.point.Z.ToString(nfi) +
                    ">";
        String s3 = "<" + v3.point.X.ToString(nfi) + "," + v3.point.Y.ToString(nfi) + "," + v3.point.Z.ToString(nfi) +
                    ">";

        return s1 + ";" + s2 + ";" + s3;
    }

    public PhysicsVector getNormal()
    {
        // Vertices

        // Vectors for edges
        PhysicsVector e1;
        PhysicsVector e2;

        e1 = new PhysicsVector(v1.point.X - v2.point.X, v1.point.Y - v2.point.Y, v1.point.Z - v2.point.Z);
        e2 = new PhysicsVector(v1.point.X - v3.point.X, v1.point.Y - v3.point.Y, v1.point.Z - v3.point.Z);

        // Cross product for normal
        PhysicsVector n = new PhysicsVector();
        float nx, ny, nz;
        n.X = e1.Y*e2.Z - e1.Z*e2.Y;
        n.Y = e1.Z*e2.X - e1.X*e2.Z;
        n.Z = e1.X*e2.Y - e1.Y*e2.X;

        // Length
        float l = (float) Math.Sqrt(n.X*n.X + n.Y*n.Y + n.Z*n.Z);

        // Normalized "normal"
        n.X /= l;
        n.Y /= l;
        n.Z /= l;

        return n;
    }

    public void invertNormal()
    {
        Vertex vt;
        vt = v1;
        v1 = v2;
        v2 = vt;
    }
}