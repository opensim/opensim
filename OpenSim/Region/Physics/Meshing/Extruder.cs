using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.Physics.Meshing
{
    class Extruder
    {
        public float startParameter;
        public float stopParameter;
        public Manager.PhysicsVector size;

        public Mesh Extrude(Mesh m)
        {
            // Currently only works for iSteps=1;
            Mesh result = new Mesh();

            Mesh workingPlus  = m.Clone();
            Mesh workingMinus = m.Clone();

            foreach (Vertex v in workingPlus.vertices)
            {
                if (v == null)
                    continue;

                v.Z = +.5f;
                v.X *= size.X;
                v.Y *= size.Y;
                v.Z *= size.Z;
            }

            foreach (Vertex v in workingMinus.vertices)
            {
                if (v == null)
                    continue;

                v.Z = -.5f;
                v.X *= size.X;
                v.Y *= size.Y;
                v.Z *= size.Z;
            }

            foreach (Triangle t in workingMinus.triangles)
            {
                t.invertNormal();
            }

            result.Append(workingMinus);
            result.Append(workingPlus);

            int iLastNull = 0;
            for (int i = 0; i < workingPlus.vertices.Count; i++)
            {
                int iNext = (i + 1);
                
                if (workingPlus.vertices[i] == null) // Can't make a simplex here
                {
                    iLastNull = i+1;
                    continue;
                }

                if (i == workingPlus.vertices.Count-1) // End of list
                {
                    iNext = iLastNull;
                }

                if (workingPlus.vertices[iNext] == null) // Null means wrap to begin of last segment
                {
                    iNext = iLastNull;
                }

                Triangle tSide;
                tSide = new Triangle(workingPlus.vertices[i], workingMinus.vertices[i], workingPlus.vertices[iNext]);
                result.Add(tSide);

                tSide = new Triangle(workingPlus.vertices[iNext], workingMinus.vertices[i], workingMinus.vertices[iNext]);
                result.Add(tSide);
            }

            return result;
        }
    }
}
