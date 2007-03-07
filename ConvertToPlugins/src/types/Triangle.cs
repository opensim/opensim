using System;
using System.Collections.Generic;
using System.Text;
using Axiom.MathLib;

namespace OpenSim.types
{
    public class Triangle
    {
        Vector3 a;
        Vector3 b;
        Vector3 c;

        public Triangle()
        {
            a = new Vector3();
            b = new Vector3();
            c = new Vector3();
        }

        public Triangle(Vector3 A, Vector3 B, Vector3 C)
        {
            a = A;
            b = B;
            c = C;
        }
    }
}
