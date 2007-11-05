using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework.Console;

namespace OpenSim.Region.Physics.OdePlugin.Meshing
{
    // A simple hull is a set of vertices building up to simplices that border a region
    // The word simple referes to the fact, that this class assumes, that all simplices
    // do not intersect
    // Simple hulls can be added and subtracted.
    // Vertices can be checked to lie inside a hull
    // Also note, that the sequence of the vertices is important and defines if the region that
    // is defined by the hull lies inside or outside the simplex chain
    public class SimpleHull
    {
        List<Vertex> vertices = new List<Vertex>();
        List<Vertex> holeVertices = new List<Vertex>(); // Only used, when the hull is hollow

        // Adds a vertex to the end of the list
        public void AddVertex(Vertex v) {
            vertices.Add(v);
        }

        override public String ToString()
        {
            String result="";
            foreach (Vertex v in vertices)
            {
                result += "b:" + v.ToString() + "\n";
            }

            return result;
        }


        public List<Vertex> getVertices() {
            List<Vertex> newVertices = new List<Vertex>();

            newVertices.AddRange(vertices);
            newVertices.Add(null);
            newVertices.AddRange(holeVertices);

            return newVertices;
        }

        public SimpleHull Clone()
        {
            SimpleHull result = new SimpleHull();
            foreach (Vertex v in vertices)
            {
                result.AddVertex(v.Clone());
            }

            foreach (Vertex v in this.holeVertices)
            {
                result.holeVertices.Add(v.Clone());
            }
            
            return result;
        }

        public bool IsPointIn(Vertex v1)
        {
            int iCounter=0;
            List<Simplex> simplices=buildSimplexList();
            foreach (Simplex s in simplices)
            {
                // Send a ray along the positive X-Direction
                // Note, that this direction must correlate with the "below" interpretation
                // of handling for the special cases below
                Manager.PhysicsVector intersection = s.RayIntersect(v1, new Manager.PhysicsVector(1.0f, 0.0f, 0.0f), true);

                if (intersection == null)
                    continue;   // No intersection. Done. More tests to follow otherwise

                // Did we hit the end of a simplex?
                // Then this can be one of two special cases:
                // 1. we go through a border exactly at a joint
                // 2. we have just marginally touched a corner
                // 3. we can slide along a border
                // Solution: If the other vertex is "below" the ray, we don't count it
                // Thus corners pointing down are counted twice, corners pointing up are not counted
                // borders are counted once
                if (intersection.IsIdentical(s.v1, 0.001f)) {
                    if (s.v2.Y < v1.Y)
                        continue;
                }
                // Do this for the other vertex two
                if (intersection.IsIdentical(s.v2, 0.001f)) {
                    if (s.v1.Y<v1.Y)
                        continue;
                }
                iCounter++;
            }

            return iCounter % 2 == 1; // Point is inside if the number of intersections is odd
        }

        public bool containsPointsFrom(SimpleHull otherHull)
        {
            foreach (Vertex v in otherHull.vertices)
            {
                if (IsPointIn(v))
                    return true;
            }

            return false;
        }


        List<Simplex> buildSimplexList() {

            List<Simplex> result = new List<Simplex>();

            // Not asserted but assumed: at least three vertices
            for (int i=0; i<vertices.Count-1; i++) {
                Simplex s=new Simplex(vertices[i], vertices[i+1]);
                result.Add(s);
            }
            Simplex s1=new Simplex(vertices[vertices.Count-1], vertices[0]);
            result.Add(s1);

            if (holeVertices.Count==0)
                return result;

            // Same here. At least three vertices in hole assumed
            for (int i = 0; i < holeVertices.Count - 1; i++)
            {
                Simplex s = new Simplex(holeVertices[i], holeVertices[i + 1]);
                result.Add(s);
            }
            
            s1 = new Simplex(holeVertices[holeVertices.Count - 1], holeVertices[0]);
            result.Add(s1);
            return result;
        }

        bool InsertVertex(Vertex v, int iAfter)
        {
            vertices.Insert(iAfter + 1, v);
            return true;
        }

        Vertex getNextVertex(Vertex currentVertex)
        {
            int iCurrentIndex;
            iCurrentIndex = vertices.IndexOf(currentVertex);

            // Error handling for iCurrentIndex==-1 should go here (and probably never will)

            iCurrentIndex++;
            if (iCurrentIndex == vertices.Count)
                iCurrentIndex = 0;

            return vertices[iCurrentIndex];
        }

        public Vertex FindVertex(Vertex vBase, float tolerance) {
            foreach (Vertex v in vertices) {
                if (v.IsIdentical(vBase, tolerance))
                    return v;
            }

            return null;
        }

        public void FindIntersection(Simplex s, ref Vertex Intersection, ref Vertex nextVertex)
        {
            Vertex bestIntersection=null;
            float distToV1=Single.PositiveInfinity;
            Simplex bestIntersectingSimplex=null;

            List<Simplex> simple = buildSimplexList();
            foreach (Simplex sTest in simple)
            {
                Manager.PhysicsVector vvTemp = Simplex.Intersect(sTest, s, -.001f, -.001f, 0.999f, .999f);
                
                Vertex vTemp=null;
                if (vvTemp != null)
                    vTemp = new Vertex(vvTemp);

                if (vTemp!=null) {

                    Manager.PhysicsVector diff=(s.v1-vTemp);
                    float distTemp=diff.length();

                    if (bestIntersection==null || distTemp<distToV1) {
                        bestIntersection=vTemp;
                        distToV1=distTemp;
                        bestIntersectingSimplex = sTest;
                    }

                } // end if vTemp

            } // end foreach

            Intersection = bestIntersection;
            if (bestIntersectingSimplex != null)
                nextVertex = bestIntersectingSimplex.v2;
            else
                nextVertex = null;
        }


        public static SimpleHull SubtractHull(SimpleHull baseHull, SimpleHull otherHull)
        {

            SimpleHull baseHullClone = baseHull.Clone();
            SimpleHull otherHullClone = otherHull.Clone();
            bool intersects = false;

            MainLog.Instance.Debug("State before intersection detection");
            MainLog.Instance.Debug("The baseHull is:\n{1}", 0, baseHullClone.ToString());
            MainLog.Instance.Debug("The otherHull is:\n{1}", 0, otherHullClone.ToString());

            {
                int iBase, iOther;

                // Insert into baseHull
                for (iBase = 0; iBase < baseHullClone.vertices.Count; iBase++)
                {
                    int iBaseNext = (iBase + 1) % baseHullClone.vertices.Count;
                    Simplex sBase = new Simplex(baseHullClone.vertices[iBase], baseHullClone.vertices[iBaseNext]);

                    for (iOther = 0; iOther < otherHullClone.vertices.Count; iOther++)
                    {
                        int iOtherNext = (iOther + 1) % otherHullClone.vertices.Count;
                        Simplex sOther = new Simplex(otherHullClone.vertices[iOther], otherHullClone.vertices[iOtherNext]);

                        Manager.PhysicsVector intersect = Simplex.Intersect(sBase, sOther, 0.001f, -.001f, 0.999f, 1.001f);
                        if (intersect != null)
                        {
                            Vertex vIntersect = new Vertex(intersect);
                            baseHullClone.vertices.Insert(iBase + 1, vIntersect);
                            sBase.v2 = vIntersect;
                            intersects = true;
                        }
                    }
                }
            }

            MainLog.Instance.Debug("State after intersection detection for the base hull");
            MainLog.Instance.Debug("The baseHull is:\n{1}", 0, baseHullClone.ToString());

            {
                int iOther, iBase;

                // Insert into otherHull
                for (iOther = 0; iOther < otherHullClone.vertices.Count; iOther++)
                {
                    int iOtherNext = (iOther + 1) % otherHullClone.vertices.Count;
                    Simplex sOther = new Simplex(otherHullClone.vertices[iOther], otherHullClone.vertices[iOtherNext]);

                    for (iBase = 0; iBase < baseHullClone.vertices.Count; iBase++)
                    {
                        int iBaseNext = (iBase + 1) % baseHullClone.vertices.Count;
                        Simplex sBase = new Simplex(baseHullClone.vertices[iBase], baseHullClone.vertices[iBaseNext]);

                        Manager.PhysicsVector intersect = Simplex.Intersect(sBase, sOther, -.001f, 0.001f, 1.001f, 0.999f);
                        if (intersect != null)
                        {
                            Vertex vIntersect = new Vertex(intersect);
                            otherHullClone.vertices.Insert(iOther + 1, vIntersect);
                            sOther.v2 = vIntersect;
                            intersects = true;
                        }
                    }
                }
            }

            MainLog.Instance.Debug("State after intersection detection for the base hull");
            MainLog.Instance.Debug("The otherHull is:\n{1}", 0, otherHullClone.ToString());


            bool otherIsInBase = baseHullClone.containsPointsFrom(otherHullClone);
            if (!intersects && otherIsInBase)
            {
                // We have a hole here
                baseHullClone.holeVertices = otherHullClone.vertices;
                return baseHullClone;
            }


            SimpleHull result = new SimpleHull();

            // Find a good starting Simplex from baseHull
            // A good starting simplex is one that is outside otherHull
            // Such a simplex must exist, otherwise the result will be empty
            Vertex baseStartVertex = null;
            {
                int iBase;
                for (iBase = 0; iBase < baseHullClone.vertices.Count; iBase++)
                {
                    int iBaseNext = (iBase + 1) % baseHullClone.vertices.Count;
                    Vertex center = new Vertex((baseHullClone.vertices[iBase] + baseHullClone.vertices[iBaseNext]) / 2.0f);
                    bool isOutside = !otherHullClone.IsPointIn(center);
                    if (isOutside)
                    {
                        baseStartVertex = baseHullClone.vertices[iBaseNext];
                        break;
                    }
                }
            }


            if (baseStartVertex == null) // i.e. no simplex fulfilled the "outside" condition. 
            // In otherwords, subtractHull completely embraces baseHull
            {
                return result;
            }

            // The simplex that *starts* with baseStartVertex is outside the cutting hull,
            // so we can start our walk with the next vertex without loosing a branch
            Vertex V1 = baseStartVertex;
            bool onBase = true;

            // And here is how we do the magic :-)
            // Start on the base hull.
            // Walk the vertices in the positive direction
            // For each vertex check, whether it is a vertex shared with the other hull
            // if this is the case, switch over to walking the other vertex list.
            // Note: The other hull *must* go backwards to our starting point (via several orther vertices)
            // Thus it is important that the cutting hull has the inverse directional sense than the
            // base hull!!!!!!!!! (means if base goes CW around it's center cutting hull must go CCW)

            bool done = false;
            while (!done)
            {
                result.AddVertex(V1);
                Vertex nextVertex = null;
                if (onBase)
                {
                    nextVertex = otherHullClone.FindVertex(V1, 0.001f);
                }
                else
                {
                    nextVertex = baseHullClone.FindVertex(V1, 0.001f);
                }

                if (nextVertex != null) // A node that represents an intersection
                {
                    V1 = nextVertex;    // Needed to find the next vertex on the other hull
                    onBase = !onBase;
                }

                if (onBase)
                    V1 = baseHullClone.getNextVertex(V1);
                else
                    V1 = otherHullClone.getNextVertex(V1);

                if (V1 == baseStartVertex)
                    done = true;
            }

            MainLog.Instance.Debug("The resulting Hull is:\n{1}", 0, result.ToString());

            return result;

        }
    }
}
