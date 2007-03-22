using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.types
{
    // TODO: This will need some performance tuning no doubt.
    public class Mesh
    {
        public List<Triangle> mesh;

        public Mesh()
        {
            mesh = new List<Triangle>();
        }

        public void AddTri(Triangle tri)
        {
            mesh.Add(tri);
        }

        public static Mesh operator +(Mesh a, Mesh b)
        {
            a.mesh.AddRange(b.mesh);
            return a;
        }
    }
}
