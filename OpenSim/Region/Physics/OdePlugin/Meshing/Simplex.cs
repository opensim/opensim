using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.OdePlugin.Meshing
{
    // A simplex is a section of a straight line.
    // It is defined by its endpoints, i.e. by two vertices
    // Operation on vertices are
    public class Simplex : IComparable<Simplex>
    {
        public Vertex v1;
        public Vertex v2;

        public Simplex(Vertex _v1, Vertex _v2)
        {
            v1 = _v1;
            v2 = _v2;
        }

        public int CompareTo(Simplex other)
        {

            Vertex lv1, lv2, ov1, ov2, temp;

            lv1 = v1;
            lv2 = v2;
            ov1 = other.v1;
            ov2 = other.v2;

            if (lv1 > lv2)
            {
                temp = lv1;
                lv1 = lv2;
                lv2 = temp;
            }

            if (ov1 > ov2)
            {
                temp = ov1;
                ov1 = ov2;
                ov2 = temp;
            }

            if (lv1 > ov1)
            {
                return 1;
            }
            if (lv1 < ov1)
            {
                return -1;
            }

            if (lv2 > ov2)
            {
                return 1;
            }
            if (lv2 < ov2)
            {
                return -1;
            }

            return 0;
        }

        private static void intersectParameter(PhysicsVector p1, PhysicsVector r1, PhysicsVector p2, PhysicsVector r2, ref float lambda, ref float mu)
        { 
            // Intersects two straights
            // p1, p2, points on the straight
            // r1, r2, directional vectors of the straight. Not necessarily of length 1!
            // note, that l, m can be scaled such, that the range 0..1 is mapped to the area between two points,
            // thus allowing to decide whether an intersection is between two points

            float r1x = r1.X;
            float r1y = r1.Y;
            float r2x = r2.X;
            float r2y = r2.Y;

            float denom = r1y*r2x - r1x*r2y;

            float p1x = p1.X;
            float p1y = p1.Y;
            float p2x = p2.X;
            float p2y = p2.Y;

            float z1=-p2x * r2y + p1x * r2y + (p2y - p1y) * r2x;
            float z2=-p2x * r1y + p1x * r1y + (p2y - p1y) * r1x;

            if (denom == 0.0f) // Means the straights are parallel. Either no intersection or an infinite number of them
            {
                if (z1==0.0f) {// Means they are identical -> many, many intersections
                    lambda = Single.NaN;
                    mu = Single.NaN;
                } else {
                    lambda = Single.PositiveInfinity;
                    mu = Single.PositiveInfinity;
                }
                return;

            }



            lambda = z1 / denom;
            mu     = z2 / denom;
        
        }


        // Intersects the simplex with another one.
        // the borders are used to deal with float inaccuracies
        // As a rule of thumb, the borders are
        // lowerBorder1 : 0.0
        // lowerBorder2 : 0.0
        // upperBorder1 : 1.0
        // upperBorder2 : 1.0
        // Set these to values near the given parameters (e.g. 0.001 instead of 1 to exclude simplex starts safely, or to -0.001 to include them safely)
        public static PhysicsVector Intersect(
                                        Simplex s1, 
                                        Simplex s2, 
                                        float lowerBorder1, 
                                        float lowerBorder2, 
                                        float upperBorder1, 
                                        float upperBorder2)
        {
            PhysicsVector firstSimplexDirection = s1.v2 - s1.v1;
            PhysicsVector secondSimplexDirection = s2.v2 - s2.v1;

            float lambda = 0.0f;
            float mu = 0.0f;

            // Give us the parameters of an intersection. This subroutine does *not* take the constraints
            // (intersection must be between v1 and v2 and it must be in the positive direction of the ray)
            // into account. We do that afterwards.
            intersectParameter(s1.v1, firstSimplexDirection, s2.v1, secondSimplexDirection, ref lambda, ref mu);

            if (Single.IsInfinity(lambda)) // Special case. No intersection at all. directions parallel.
                return null;

            if (Single.IsNaN(lambda)) // Special case. many, many intersections.
                return null;

            if (lambda > upperBorder1) // We're behind v2
                return null;

            if (lambda < lowerBorder1)
                return null;

            if (mu < lowerBorder2) // outside simplex 2
                return null;

            if (mu > upperBorder2) // outside simplex 2
                return null;

            return s1.v1 + lambda * firstSimplexDirection;

        }

        // Intersects the simplex with a ray. The ray is defined as all p=origin + lambda*direction
        // where lambda >= 0
        public PhysicsVector RayIntersect(Vertex origin, PhysicsVector direction, bool bEndsIncluded)
        {
            PhysicsVector simplexDirection = v2 - v1;

            float lambda = 0.0f;
            float mu = 0.0f;

            // Give us the parameters of an intersection. This subroutine does *not* take the constraints
            // (intersection must be between v1 and v2 and it must be in the positive direction of the ray)
            // into account. We do that afterwards.
            intersectParameter(v1, simplexDirection, origin, direction, ref lambda, ref mu);

            if (Single.IsInfinity(lambda)) // Special case. No intersection at all. directions parallel.
                return null;

            if (Single.IsNaN(lambda)) // Special case. many, many intersections.
                return null;

            if (mu < 0.0) // We're on the wrong side of the ray
                return null;

            if (lambda > 1.0) // We're behind v2
                return null;

            if (lambda == 1.0 && !bEndsIncluded)
                return null;    // The end of the simplices are not included

            if (lambda < 0.0f) // we're before v1;
                return null;

            return this.v1 + lambda * simplexDirection;

        }


    }
}
