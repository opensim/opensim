using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.types
{
    // TODO: This will need some performance tuning no doubt.
    public class Mesh
    {
        public List<Triangle> mesh;

        /// <summary>
        /// 
        /// </summary>
        public Mesh()
        {
            mesh = new List<Triangle>();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tri"></param>
        public void AddTri(Triangle tri)
        {
            mesh.Add(tri);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static Mesh operator +(Mesh a, Mesh b)
        {
            a.mesh.AddRange(b.mesh);
            return a;
        }
    }
}
