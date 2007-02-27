using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.types;

namespace OpenSim.world
{
    public class Primitive : Entity
    {
        protected float mesh_cutbegin;
        protected float mesh_cutend;

        public Primitive()
        {
            mesh_cutbegin = 0.0f;
            mesh_cutend = 1.0f;
        }

        public override Mesh getMesh()
        {
            Mesh mesh = new Mesh();
            Triangle tri = new Triangle(
                new Axiom.MathLib.Vector3(0.0f, 1.0f, 1.0f), 
                new Axiom.MathLib.Vector3(1.0f, 0.0f, 1.0f), 
                new Axiom.MathLib.Vector3(1.0f, 1.0f, 0.0f));

            mesh.AddTri(tri);
            mesh += base.getMesh();

            return mesh;
        }
    }
}
