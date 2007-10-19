/*************************************************************************
 *                                                                       *
 * Open Dynamics Engine, Copyright (C) 2001-2003 Russell L. Smith.       *
 * All rights reserved.  Email: russ@q12.org   Web: www.q12.org          *
 *                                                                       *
 * This library is free software; you can redistribute it and/or         *
 * modify it under the terms of EITHER:                                  *
 *   (1) The GNU Lesser General Public License as published by the Free  *
 *       Software Foundation; either version 2.1 of the License, or (at  *
 *       your option) any later version. The text of the GNU Lesser      *
 *       General Public License is included with this library in the     *
 *       file LICENSE.TXT.                                               *
 *   (2) The BSD-style license that is included with this library in     *
 *       the file LICENSE-BSD.TXT.                                       *
 *                                                                       *
 * This library is distributed in the hope that it will be useful,       *
 * but WITHOUT ANY WARRANTY; without even the implied warranty of        *
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the files    *
 * LICENSE.TXT and LICENSE-BSD.TXT for more details.                     *
 *                                                                       *
 *************************************************************************/

/*
Code for Convex Collision Detection
By Rodrigo Hernandez
*/
//#include <algorithm>
#include <ode/common.h>
#include <ode/collision.h>
#include <ode/matrix.h>
#include <ode/rotation.h>
#include <ode/odemath.h>
#include "collision_kernel.h"
#include "collision_std.h"
#include "collision_util.h"

#ifdef _MSC_VER
#pragma warning(disable:4291)  // for VC++, no complaints about "no matching operator delete found"
#endif

#if _MSC_VER <= 1200
#define dMIN(A,B)  ((A)>(B) ? B : A)
#define dMAX(A,B)  ((A)>(B) ? A : B)
#else
#define dMIN(A,B)  std::min(A,B)
#define dMAX(A,B)  std::max(A,B)
#endif

//****************************************************************************
// Convex public API
dxConvex::dxConvex (dSpaceID space,
		    dReal *_planes,
		    unsigned int _planecount,
		    dReal *_points,
		    unsigned int _pointcount,
		    unsigned int *_polygons) : 
  dxGeom (space,1)
{
  dAASSERT (_planes != NULL);
  dAASSERT (_points != NULL);
  dAASSERT (_polygons != NULL);
  //fprintf(stdout,"dxConvex Constructor planes %X\n",_planes);
  type = dConvexClass;
  planes = _planes;
  planecount = _planecount;
  // we need points as well
  points = _points;
  pointcount = _pointcount;
  polygons=_polygons;
  FillEdges();
}


void dxConvex::computeAABB()
{
  // this can, and should be optimized
  dVector3 point;
  dMULTIPLY0_331 (point,final_posr->R,points);
  aabb[0] = point[0]+final_posr->pos[0];
  aabb[1] = point[0]+final_posr->pos[0];
  aabb[2] = point[1]+final_posr->pos[1];
  aabb[3] = point[1]+final_posr->pos[1];
  aabb[4] = point[2]+final_posr->pos[2];
  aabb[5] = point[2]+final_posr->pos[2];
  for(unsigned int i=3;i<(pointcount*3);i+=3)
    {
      dMULTIPLY0_331 (point,final_posr->R,&points[i]);
      aabb[0] = dMIN(aabb[0],point[0]+final_posr->pos[0]);
      aabb[1] = dMAX(aabb[1],point[0]+final_posr->pos[0]);
      aabb[2] = dMIN(aabb[2],point[1]+final_posr->pos[1]);
      aabb[3] = dMAX(aabb[3],point[1]+final_posr->pos[1]);
      aabb[4] = dMIN(aabb[4],point[2]+final_posr->pos[2]);
      aabb[5] = dMAX(aabb[5],point[2]+final_posr->pos[2]);
    }
}

/*! \brief Populates the edges set, should be called only once whenever
  the polygon array gets updated */
void dxConvex::FillEdges()
{
	unsigned int *points_in_poly=polygons;
	unsigned int *index=polygons+1;
	for(unsigned int i=0;i<planecount;++i)
	{
		//fprintf(stdout,"Points in Poly: %d\n",*points_in_poly);
		for(unsigned int j=0;j<*points_in_poly;++j)
		{
			edges.insert(edge(dMIN(index[j],index[(j+1)%*points_in_poly]),
				dMAX(index[j],index[(j+1)%*points_in_poly])));
			//fprintf(stdout,"Insert %d-%d\n",index[j],index[(j+1)%*points_in_poly]);
		}
		points_in_poly+=(*points_in_poly+1);
		index=points_in_poly+1;
	}
	/*
	fprintf(stdout,"Edge Count: %d\n",edges.size());
	for(std::set<edge>::iterator it=edges.begin();
	it!=edges.end();
	++it)
	{
	fprintf(stdout,"Edge: %d-%d\n",it->first,it->second);
	}
	*/
}

dGeomID dCreateConvex (dSpaceID space,dReal *_planes,unsigned int _planecount,
		    dReal *_points,
		       unsigned int _pointcount,
		       unsigned int *_polygons)
{
  //fprintf(stdout,"dxConvex dCreateConvex\n");
  return new dxConvex(space,_planes, _planecount,
		      _points,
		      _pointcount,
		      _polygons);
}

void dGeomSetConvex (dGeomID g,dReal *_planes,unsigned int _planecount,
		     dReal *_points,
		     unsigned int _pointcount,
		     unsigned int *_polygons)
{
  //fprintf(stdout,"dxConvex dGeomSetConvex\n");
  dUASSERT (g && g->type == dConvexClass,"argument not a convex shape");
  dxConvex *s = (dxConvex*) g;
  s->planes = _planes;
  s->planecount = _planecount;
  s->points = _points;
  s->pointcount = _pointcount;
  s->polygons=_polygons;
}

//****************************************************************************
// Helper Inlines
//

/*! \brief Returns Whether or not the segment ab intersects plane p
  \param a origin of the segment
  \param b segment destination
  \param p plane to test for intersection
  \param t returns the time "t" in the segment ray that gives us the intersecting 
  point
  \param q returns the intersection point
  \return true if there is an intersection, otherwise false.
*/
bool IntersectSegmentPlane(dVector3 a, 
			   dVector3 b, 
			   dVector4 p, 
			   dReal &t, 
			   dVector3 q)
{
  // Compute the t value for the directed line ab intersecting the plane
  dVector3 ab;
  ab[0]= b[0] - a[0];
  ab[1]= b[1] - a[1];
  ab[2]= b[2] - a[2];
  
  t = (p[3] - dDOT(p,a)) / dDOT(p,ab);
  
  // If t in [0..1] compute and return intersection point
  if (t >= 0.0 && t <= 1.0) 
    {
      q[0] = a[0] + t * ab[0];
      q[1] = a[1] + t * ab[1];
      q[2] = a[2] + t * ab[2];
      return true;
    }
  // Else no intersection
  return false;
}

/*! \brief Returns the Closest Point in Ray 1 to Ray 2
  \param Origin1 The origin of Ray 1
  \param Direction1 The direction of Ray 1
  \param Origin1 The origin of Ray 2
  \param Direction1 The direction of Ray 3
  \param t the time "t" in Ray 1 that gives us the closest point 
  (closest_point=Origin1+(Direction*t).
  \return true if there is a closest point, false if the rays are paralell.
*/
inline bool ClosestPointInRay(const dVector3 Origin1,
			      const dVector3 Direction1,
			      const dVector3 Origin2,
			      const dVector3 Direction2,
			      dReal& t)
{
  dVector3 w = {Origin1[0]-Origin2[0],
		Origin1[1]-Origin2[1],
		Origin1[2]-Origin2[2]};
  dReal a = dDOT(Direction1 , Direction1);
  dReal b = dDOT(Direction1 , Direction2);
  dReal c = dDOT(Direction2 , Direction2);
  dReal d = dDOT(Direction1 , w);
  dReal e = dDOT(Direction2 , w);
  dReal denominator = (a*c)-(b*b);
  if(denominator==0.0f)
    {
      return false;
    }
  t = ((a*e)-(b*d))/denominator;
  return true;
}

/*! \brief Returns the Ray on which 2 planes intersect if they do.
  \param p1 Plane 1
  \param p2 Plane 2
  \param p Contains the origin of the ray upon returning if planes intersect
  \param d Contains the direction of the ray upon returning if planes intersect
  \return true if the planes intersect, false if paralell.
*/
inline bool IntersectPlanes(const dVector4 p1, const dVector4 p2, dVector3 p, dVector3 d)
{
  // Compute direction of intersection line
  //Cross(p1, p2,d);
  dCROSS(d,=,p1,p2);
  
  // If d is (near) zero, the planes are parallel (and separated)
  // or coincident, so they're not considered intersecting
  dReal denom = dDOT(d, d);
  if (denom < dEpsilon) return false;

  dVector3 n;
  n[0]=p1[3]*p2[0] - p2[3]*p1[0];
  n[1]=p1[3]*p2[1] - p2[3]*p1[1];
  n[2]=p1[3]*p2[2] - p2[3]*p1[2];
  // Compute point on intersection line
  //Cross(n, d,p);
  dCROSS(p,=,n,d);
  p[0]/=denom;
  p[1]/=denom;
  p[2]/=denom;
  return true;
}

/*! \brief Finds out if a point lies inside a 2D polygon
  \param p Point to test
  \param polygon a pointer to the start of the convex polygon index buffer
  \param out the closest point in the polygon if the point is not inside
  \return true if the point lies inside of the polygon, false if not.
*/
inline bool IsPointInPolygon(dVector3 p,
			     unsigned int *polygon,
			     dxConvex *convex,
			     dVector3 out)
{
  // p is the point we want to check,
  // polygon is a pointer to the polygon we
  // are checking against, remember it goes
  // number of vertices then that many indexes
  // out returns the closest point on the border of the
  // polygon if the point is not inside it.
  size_t pointcount=polygon[0];
  dVector3 a;
  dVector3 b;
  dVector3 c;
  dVector3 ab;
  dVector3 ac;
  dVector3 ap;
  dVector3 bp;
  dReal d1;
  dReal d2;
  dReal d3;
  dReal d4;
  dReal vc;
  polygon++; // skip past pointcount
  for(size_t i=0;i<pointcount;++i)
    {
      dMULTIPLY0_331 (a,convex->final_posr->R,&convex->points[(polygon[i]*3)]);
      a[0]=convex->final_posr->pos[0]+a[0];
      a[1]=convex->final_posr->pos[1]+a[1];
      a[2]=convex->final_posr->pos[2]+a[2];

      dMULTIPLY0_331 (b,convex->final_posr->R,
		      &convex->points[(polygon[(i+1)%pointcount]*3)]);
      b[0]=convex->final_posr->pos[0]+b[0];
      b[1]=convex->final_posr->pos[1]+b[1];
      b[2]=convex->final_posr->pos[2]+b[2];

      dMULTIPLY0_331 (c,convex->final_posr->R,
		      &convex->points[(polygon[(i+2)%pointcount]*3)]);
      c[0]=convex->final_posr->pos[0]+c[0];
      c[1]=convex->final_posr->pos[1]+c[1];
      c[2]=convex->final_posr->pos[2]+c[2];

      ab[0] = b[0] - a[0];
      ab[1] = b[1] - a[1];
      ab[2] = b[2] - a[2];
      ac[0] = c[0] - a[0];
      ac[1] = c[1] - a[1];
      ac[2] = c[2] - a[2];
      ap[0] = p[0] - a[0];
      ap[1] = p[1] - a[1];
      ap[2] = p[2] - a[2];
      d1 = dDOT(ab,ap);
      d2 = dDOT(ac,ap);
      if (d1 <= 0.0f && d2 <= 0.0f)
	{
	  out[0]=a[0];
	  out[1]=a[1];
	  out[2]=a[2];
	  return false;
	}
      bp[0] = p[0] - b[0];
      bp[1] = p[1] - b[1];
      bp[2] = p[2] - b[2];
      d3 = dDOT(ab,bp);
      d4 = dDOT(ac,bp);
      if (d3 >= 0.0f && d4 <= d3)
	{
	  out[0]=b[0];
	  out[1]=b[1];
	  out[2]=b[2];
	  return false;
	}      
      vc = d1*d4 - d3*d2;
      if (vc <= 0.0f && d1 >= 0.0f && d3 <= 0.0f) 
	{
	  dReal v = d1 / (d1 - d3);
	  out[0] = a[0] + (ab[0]*v);
	  out[1] = a[1] + (ab[1]*v);
	  out[2] = a[2] + (ab[2]*v);
	  return false;
	}
    }
  return true;
}

int dCollideConvexPlane (dxGeom *o1, dxGeom *o2, int flags,
						 dContactGeom *contact, int skip)
{
	dIASSERT (skip >= (int)sizeof(dContactGeom));
	dIASSERT (o1->type == dConvexClass);
	dIASSERT (o2->type == dPlaneClass);
	dIASSERT ((flags & NUMC_MASK) >= 1);
	
	dxConvex *Convex = (dxConvex*) o1;
	dxPlane *Plane = (dxPlane*) o2;
	unsigned int contacts=0;
	unsigned int maxc = flags & NUMC_MASK;
	dVector3 v2;

#define LTEQ_ZERO	0x10000000
#define GTEQ_ZERO	0x20000000
#define BOTH_SIGNS	(LTEQ_ZERO | GTEQ_ZERO)
	dIASSERT((BOTH_SIGNS & NUMC_MASK) == 0); // used in conditional operator later

	unsigned int totalsign = 0;
	for(unsigned int i=0;i<Convex->pointcount;++i)
	{
		dMULTIPLY0_331 (v2,Convex->final_posr->R,&Convex->points[(i*3)]);
		dVector3Add(Convex->final_posr->pos, v2, v2);
		
		unsigned int distance2sign = GTEQ_ZERO;
		dReal distance2 = dVector3Dot(Plane->p, v2) - Plane->p[3]; // Ax + By + Cz - D
		if((distance2 <= REAL(0.0)))
		{
			distance2sign = distance2 != REAL(0.0) ? LTEQ_ZERO : BOTH_SIGNS;

			if (contacts != maxc)
			{
				dContactGeom *target = SAFECONTACT(flags, contact, contacts, skip);
				dVector3Copy(Plane->p, target->normal);
				dVector3Copy(v2, target->pos);
				target->depth = -distance2;
				target->g1 = Convex;
				target->g2 = Plane;
				contacts++;
			}
		}

		// Take new sign into account
		totalsign |= distance2sign;
		// Check if contacts are full and both signs have been already found
		if ((contacts ^ maxc | totalsign) == BOTH_SIGNS) // harder to comprehend but requires one register less
		{
			break; // Nothing can be changed any more
		}
	}
	if (totalsign == BOTH_SIGNS) return contacts;
	return 0;
#undef BOTH_SIGNS
#undef GTEQ_ZERO
#undef LTEQ_ZERO
}

int dCollideSphereConvex (dxGeom *o1, dxGeom *o2, int flags,
			  dContactGeom *contact, int skip)
{
  dIASSERT (skip >= (int)sizeof(dContactGeom));
  dIASSERT (o1->type == dSphereClass);
  dIASSERT (o2->type == dConvexClass);
  dIASSERT ((flags & NUMC_MASK) >= 1);

  dxSphere *Sphere = (dxSphere*) o1;
  dxConvex *Convex = (dxConvex*) o2;
  dReal dist,closestdist=dInfinity;
  dVector4 plane;
  // dVector3 contactpoint;
  dVector3 offsetpos,out,temp;
  unsigned int *pPoly=Convex->polygons;
  int closestplane;
  bool sphereinside=true;
  /* 
     Do a good old sphere vs plane check first,
     if a collision is found then check if the contact point
     is within the polygon
  */
  // offset the sphere final_posr->position into the convex space
  offsetpos[0]=Sphere->final_posr->pos[0]-Convex->final_posr->pos[0];
  offsetpos[1]=Sphere->final_posr->pos[1]-Convex->final_posr->pos[1];
  offsetpos[2]=Sphere->final_posr->pos[2]-Convex->final_posr->pos[2];
  //fprintf(stdout,"Begin Check\n");  
  for(unsigned int i=0;i<Convex->planecount;++i)
    {
      // apply rotation to the plane
      dMULTIPLY0_331(plane,Convex->final_posr->R,&Convex->planes[(i*4)]);
      plane[3]=(&Convex->planes[(i*4)])[3];
      // Get the distance from the sphere origin to the plane
      dist = dVector3Dot(plane, offsetpos) - plane[3]; // Ax + By + Cz - D
      if(dist>0)
	{
	  // if we get here, we know the center of the sphere is
	  // outside of the convex hull.
	  if(dist<Sphere->radius)
	    {
	      // if we get here we know the sphere surface penetrates
	      // the plane
	      if(IsPointInPolygon(Sphere->final_posr->pos,pPoly,Convex,out))
		{
		  // finally if we get here we know that the
		  // sphere is directly touching the inside of the polyhedron
		  //fprintf(stdout,"Contact! distance=%f\n",dist);
		  contact->normal[0] = plane[0];
		  contact->normal[1] = plane[1];
		  contact->normal[2] = plane[2];
		  contact->pos[0] = Sphere->final_posr->pos[0]+
		    (-contact->normal[0]*Sphere->radius);
		  contact->pos[1] = Sphere->final_posr->pos[1]+
		    (-contact->normal[1]*Sphere->radius);
		  contact->pos[2] = Sphere->final_posr->pos[2]+
		    (-contact->normal[2]*Sphere->radius);
		  contact->depth = Sphere->radius-dist;
		  contact->g1 = Sphere;
		  contact->g2 = Convex;
		  return 1;
		}
	      else
		{
		  // the sphere may not be directly touching
		  // the polyhedron, but it may be touching
		  // a point or an edge, if the distance between
		  // the closest point on the poly (out) and the 
		  // center of the sphere is less than the sphere 
		  // radius we have a hit.
		  temp[0] = (Sphere->final_posr->pos[0]-out[0]);
		  temp[1] = (Sphere->final_posr->pos[1]-out[1]);
		  temp[2] = (Sphere->final_posr->pos[2]-out[2]);
		  dist=(temp[0]*temp[0])+(temp[1]*temp[1])+(temp[2]*temp[2]);
		  // avoid the sqrt unless really necesary
		  if(dist<(Sphere->radius*Sphere->radius))
		    {
		      // We got an indirect hit
		      dist=dSqrt(dist);
		      contact->normal[0] = temp[0]/dist;
		      contact->normal[1] = temp[1]/dist;
		      contact->normal[2] = temp[2]/dist;
		      contact->pos[0] = Sphere->final_posr->pos[0]+
			(-contact->normal[0]*Sphere->radius);
		      contact->pos[1] = Sphere->final_posr->pos[1]+
			(-contact->normal[1]*Sphere->radius);
		      contact->pos[2] = Sphere->final_posr->pos[2]+
			(-contact->normal[2]*Sphere->radius);
		      contact->depth = Sphere->radius-dist;
		      contact->g1 = Sphere;
		      contact->g2 = Convex;
		      return 1;
		    }
		}
	    }
	  sphereinside=false;
	}
      if(sphereinside)
	{
	  if(closestdist>dFabs(dist))
	    {
	      closestdist=dFabs(dist);
	      closestplane=i;
	    }
	}
      pPoly+=pPoly[0]+1;
    }
    if(sphereinside)
      {
	// if the center of the sphere is inside 
	// the Convex, we need to pop it out
	dMULTIPLY0_331(contact->normal,
		       Convex->final_posr->R,
		       &Convex->planes[(closestplane*4)]);
	contact->pos[0] = Sphere->final_posr->pos[0];
	contact->pos[1] = Sphere->final_posr->pos[1];
	contact->pos[2] = Sphere->final_posr->pos[2];
	contact->depth = closestdist+Sphere->radius;
	contact->g1 = Sphere;
	contact->g2 = Convex;
	return 1;
      }
  return 0;
}

int dCollideConvexBox (dxGeom *o1, dxGeom *o2, int flags,
		       dContactGeom *contact, int skip)
{
  dIASSERT (skip >= (int)sizeof(dContactGeom));
  dIASSERT (o1->type == dConvexClass);
  dIASSERT (o2->type == dBoxClass);
  dIASSERT ((flags & NUMC_MASK) >= 1);
  
  dxConvex *Convex = (dxConvex*) o1;
  dxBox *Box = (dxBox*) o2;
  
  return 0;
}

int dCollideConvexCapsule (dxGeom *o1, dxGeom *o2,
			     int flags, dContactGeom *contact, int skip)
{
  dIASSERT (skip >= (int)sizeof(dContactGeom));
  dIASSERT (o1->type == dConvexClass);
  dIASSERT (o2->type == dCapsuleClass);
  dIASSERT ((flags & NUMC_MASK) >= 1);

  dxConvex *Convex = (dxConvex*) o1;
  dxCapsule *Capsule = (dxCapsule*) o2;
  
  return 0;
}

/*!
  \brief Retrieves the proper convex and plane index between 2 convex objects.

  Seidel's Algorithm does not discriminate between 2 different Convex Hulls,
  it only cares about planes, so we feed it the planes of Convex 1 followed
  by the planes of Convex 2 as a single collection of planes, 
  given an index into the single collection, 
  this function determines the correct convex object and index to retrieve
  the current plane.
  
  \param c1 Convex 1
  \param c2 Convex 2
  \param i Plane Index to retrieve
  \param index contains the translated index uppon return
  \return a pointer to the convex object containing plane index "i"
*/
inline dxConvex* GetPlaneIndex(dxConvex& c1, 
			       dxConvex& c2,
			       unsigned int i,unsigned int& index)
{
  if(i>=c1.planecount)
    {
      index = i-c1.planecount;
      return &c2;
    }
  index=i;
  return &c1;
}

inline void dumpplanes(dxConvex& cvx)
{
	// This is just a dummy debug function
	dVector4 plane;
	fprintf(stdout,"DUMP PLANES\n");
	for (unsigned int i=0;i<cvx.planecount;++i)
	{
		dMULTIPLY0_331(plane,cvx.final_posr->R,&cvx.planes[(i*4)]);      
		// Translate
		plane[3]=
			(cvx.planes[(i*4)+3])+
			((plane[0] * cvx.final_posr->pos[0]) + 
			(plane[1] * cvx.final_posr->pos[1]) + 
			(plane[2] * cvx.final_posr->pos[2]));
		fprintf(stdout,"%f %f %f %f\n",plane[0],plane[1],plane[2],plane[3]);
	}
}

// this variable is for debuggin purpuses only, should go once everything works
static bool hit=false;

/*
  \brief Tests whether 2 convex objects collide

  Seidel's algorithm is a method to solve linear programs, 
  it finds the optimum vertex "v" of a set of functions, 
  in our case, the set of functions are the plane functions
  for the 2 convex objects being tested.
  We don't really care about the value optimum vertex though,
  but the 2 convex objects only collide if this vertex exists,
  otherwise, the set of functions is said to be "empty" or "void".

  Seidel's Original algorithm is recursive and not bound to any number
  of dimensions, the one I present here is Iterative rather than recursive
  and bound to 3 dimensions, which is what we care about.
  
  If you're interested (and you should be!) on the algorithm, the paper
  by Raimund Seidel himself should explain a lot better than I did, you can
  find it here: http://www.cs.berkeley.edu/~jrs/meshpapers/SeidelLP.pdf

  If posible, read the section about Linear Programming in 
  Christer Ericson's RealTime Collision Detection book.
  
  \note currently there seem to be some issues with this function since
  it doesn't detect collisions except for the most simple tests :(. 
*/
bool SeidelLP(dxConvex& cvx1,dxConvex& cvx2)
{
	dVector3 c={1,0,0}; // The Objective vector can be anything
	dVector3 solution;    // We dont really need the solution so its local
	dxConvex *cvx;
	unsigned int index;
	unsigned int planecount=cvx1.planecount+cvx2.planecount;
	dReal sum,min,max,med;
	dVector3 c1; // ,c2;
	dVector4 aoveral,aoveram; // these will contain cached computations
	unsigned int l,m,n; // l and m are the axes to the zerod dimensions, n is the axe for the last dimension
	unsigned int i,j,k;
	dVector4 eq1,eq2,eq3; // cached equations for 3d,2d and 1d respectivelly
	// Get the support mapping for a HUGE bounding box in direction c
	solution[0]= (c[0]>0) ? dInfinity : -dInfinity;
	solution[1]= (c[1]>0) ? dInfinity : -dInfinity;
	solution[2]= (c[2]>0) ? dInfinity : -dInfinity;
	for( i=0;i<planecount;++i)
	{
		// Isolate plane equation
		cvx=GetPlaneIndex(cvx1,cvx2,i,index);
		// Rotate
		dMULTIPLY0_331(eq1,cvx->final_posr->R,&cvx->planes[(index*4)]);

		// Translate
		eq1[3]=(cvx->planes[(index*4)+3])+
			((eq1[0] * cvx->final_posr->pos[0]) + 
			(eq1[1] * cvx->final_posr->pos[1]) + 
			(eq1[2] * cvx->final_posr->pos[2]));
		//       if(!hit)
		// 	{
		// 	  fprintf(stdout,"Plane I %d: %f %f %f %f\n",i,
		// 		  cvx->planes[(index*4)+0],
		// 		  cvx->planes[(index*4)+1],
		// 		  cvx->planes[(index*4)+2],
		// 		  cvx->planes[(index*4)+3]);
		// 	  fprintf(stdout,"Transformed Plane %d: %f %f %f %f\n",i,
		// 		  eq1[0],
		// 		  eq1[1],eq1[2],eq1[3]);
		// 	  fprintf(stdout,"POS %f,%f,%f\n",
		// 		  cvx->final_posr->pos[0],
		// 		  cvx->final_posr->pos[1],
		// 		  cvx->final_posr->pos[2]);
		// 	}
		// find if the solution is behind the current plane
		sum=
			((eq1[0]*solution[0])+
			(eq1[1]*solution[1])+
			(eq1[2]*solution[2]))-eq1[3];
		// if not we need to discard the current solution
		if(sum>0)
		{
			// go down a dimension by eliminating a variable
			// first find l
			l=0;
			for( j=0;j<3;++j)
			{
				if(fabs(eq1[j])>fabs(eq1[l]))
				{
					l=j;
				}
			}
			if(eq1[l]==0)
			{
				if(!hit) 
				{
					fprintf(stdout,"Plane %d: %f %f %f %f is invalid\n",i,
						eq1[0],eq1[1],eq1[2],eq1[3]);
				}
				return false; 
			}
			// then compute a/a[l] c1 and solution
			aoveral[0]=eq1[0]/eq1[l];
			aoveral[1]=eq1[1]/eq1[l];
			aoveral[2]=eq1[2]/eq1[l];
			aoveral[3]=eq1[3]/eq1[l];
			c1[0]=c[0]-((c[l]/eq1[l])*eq1[0]);
			c1[1]=c[1]-((c[l]/eq1[l])*eq1[1]);
			c1[2]=c[2]-((c[l]/eq1[l])*eq1[2]);
			solution[0]=solution[0]-((solution[l]/eq1[l])*eq1[0]);
			solution[1]=solution[1]-((solution[l]/eq1[l])*eq1[1]);
			solution[2]=solution[2]-((solution[l]/eq1[l])*eq1[2]);
			// iterate a to get the new equations with the help of a/a[l]
			for( j=0;j<planecount;++j)
			{
				if(i!=j)
				{
					cvx=GetPlaneIndex(cvx1,cvx2,j,index);
					// Rotate
					dMULTIPLY0_331(eq2,cvx->final_posr->R,&cvx->planes[(index*4)]);
					// Translate
					eq2[3]=(cvx->planes[(index*4)+3])+
						((eq2[0] * cvx->final_posr->pos[0]) + 
						(eq2[1] * cvx->final_posr->pos[1]) + 
						(eq2[2] * cvx->final_posr->pos[2]));

					//       if(!hit)
					// 	{
					// 	  fprintf(stdout,"Plane J %d: %f %f %f %f\n",j,
					// 		  cvx->planes[(index*4)+0],
					// 		  cvx->planes[(index*4)+1],
					// 		  cvx->planes[(index*4)+2],
					// 		  cvx->planes[(index*4)+3]);
					// 	  fprintf(stdout,"Transformed Plane %d: %f %f %f %f\n",j,
					// 		  eq2[0],
					// 		  eq2[1],
					// 		  eq2[2],
					// 		  eq2[3]);
					// 	  fprintf(stdout,"POS %f,%f,%f\n",
					// 		  cvx->final_posr->pos[0],
					// 		  cvx->final_posr->pos[1],
					// 		  cvx->final_posr->pos[2]);
					// 	}

					// Take The equation down a dimension
					eq2[0]-=(cvx->planes[(index*4)+l]*aoveral[0]);
					eq2[1]-=(cvx->planes[(index*4)+l]*aoveral[1]);
					eq2[2]-=(cvx->planes[(index*4)+l]*aoveral[2]);
					eq2[3]-=(cvx->planes[(index*4)+l]*aoveral[3]);
					sum=
						((eq2[0]*solution[0])+
						(eq2[1]*solution[1])+
						(eq2[2]*solution[2]))-eq2[3];
					if(sum>0)
					{
						m=0;
						for( k=0;k<3;++k)
						{
							if(fabs(eq2[k])>fabs(eq2[m]))
							{
								m=k;
							}
						}
						if(eq2[m]==0)
						{
							/* 
							if(!hit) fprintf(stdout,
							"Plane %d: %f %f %f %f is invalid\n",j,
							eq2[0],eq2[1],eq2[2],eq2[3]);
							*/
							return false; 
						}
						// then compute a/a[m] c1 and solution
						aoveram[0]=eq2[0]/eq2[m];
						aoveram[1]=eq2[1]/eq2[m];
						aoveram[2]=eq2[2]/eq2[m];
						aoveram[3]=eq2[3]/eq2[m];
						c1[0]=c[0]-((c[m]/eq2[m])*eq2[0]);
						c1[1]=c[1]-((c[m]/eq2[m])*eq2[1]);
						c1[2]=c[2]-((c[m]/eq2[m])*eq2[2]);
						solution[0]=solution[0]-((solution[m]/eq2[m])*eq2[0]);
						solution[1]=solution[1]-((solution[m]/eq2[m])*eq2[1]);
						solution[2]=solution[2]-((solution[m]/eq2[m])*eq2[2]);
						// figure out the value for n by elimination
						n = (l==0) ? ((m==1)? 2:1):((m==0)?((l==1)?2:1):0);
						// iterate a to get the new equations with the help of a/a[l]
						min=-dInfinity;
						max=med=dInfinity;
						for(k=0;k<planecount;++k)
						{
							if((i!=k)&&(j!=k))
							{
								cvx=GetPlaneIndex(cvx1,cvx2,k,index);
								// Rotate
								dMULTIPLY0_331(eq3,cvx->final_posr->R,&cvx->planes[(index*4)]);
								// Translate
								eq3[3]=(cvx->planes[(index*4)+3])+
									((eq3[0] * cvx->final_posr->pos[0]) + 
									(eq3[1] * cvx->final_posr->pos[1]) + 
									(eq3[2] * cvx->final_posr->pos[2]));
								//       if(!hit)
								// 	{
								// 	  fprintf(stdout,"Plane K %d: %f %f %f %f\n",k,
								// 		  cvx->planes[(index*4)+0],
								// 		  cvx->planes[(index*4)+1],
								// 		  cvx->planes[(index*4)+2],
								// 		  cvx->planes[(index*4)+3]);
								// 	  fprintf(stdout,"Transformed Plane %d: %f %f %f %f\n",k,
								// 		  eq3[0],
								// 		  eq3[1],
								// 		  eq3[2],
								// 		  eq3[3]);
								// 	  fprintf(stdout,"POS %f,%f,%f\n",
								// 		  cvx->final_posr->pos[0],
								// 		  cvx->final_posr->pos[1],
								// 		  cvx->final_posr->pos[2]);
								// 	}

								// Take the equation Down to 1D
								eq3[0]-=(cvx->planes[(index*4)+m]*aoveram[0]);
								eq3[1]-=(cvx->planes[(index*4)+m]*aoveram[1]);
								eq3[2]-=(cvx->planes[(index*4)+m]*aoveram[2]);
								eq3[3]-=(cvx->planes[(index*4)+m]*aoveram[3]);
								if(eq3[n]>0)
								{
									max=dMIN(max,eq3[3]/eq3[n]);
								}
								else if(eq3[n]<0)
								{
									min=dMAX(min,eq3[3]/eq3[n]);
								}
								else
								{
									med=dMIN(med,eq3[3]);
								}
							}
						}
						if ((max<min)||(med<0)) 
						{
							// 			  if(!hit) fprintf(stdout,"((max<min)||(med<0)) ((%f<%f)||(%f<0))",max,min,med);
							if(!hit)
							{
								dumpplanes(cvx1);
								dumpplanes(cvx2);
							}
							//fprintf(stdout,"Planes %d %d\n",i,j);
							return false;

						}
						// find the value for the solution in one dimension
						solution[n] = (c1[n]>=0) ? max:min;
						// lift to 2D
						solution[m] = (eq2[3]-(eq2[n]*solution[n]))/eq2[m];
						// lift to 3D
						solution[l] = (eq1[3]-(eq1[m]*solution[m]+
							eq1[n]*solution[n]))/eq1[l];
					}
				}
			}
		}
	}
	return true;
}

/*! \brief A Support mapping function for convex shapes
  \param dir direction to find the Support Point for
  \param cvx convex object to find the support point for
  \param out the support mapping in dir.
 */
inline void Support(dVector3 dir,dxConvex& cvx,dVector3 out)
{
	unsigned int index = 0;
	dVector3 point;
	dMULTIPLY0_331 (point,cvx.final_posr->R,cvx.points);
	point[0]+=cvx.final_posr->pos[0];
	point[1]+=cvx.final_posr->pos[1];
	point[2]+=cvx.final_posr->pos[2];

	dReal max = dDOT(point,dir);
	dReal tmp;
	for (unsigned int i = 1; i < cvx.pointcount; ++i) 
	{
		dMULTIPLY0_331 (point,cvx.final_posr->R,cvx.points+(i*3));
		point[0]+=cvx.final_posr->pos[0];
		point[1]+=cvx.final_posr->pos[1];
		point[2]+=cvx.final_posr->pos[2];      
		tmp = dDOT(point, dir);
		if (tmp > max) 
		{ 
			out[0]=point[0];
			out[1]=point[1];
			out[2]=point[2];
			max = tmp; 
		}
	}
}

inline void ComputeInterval(dxConvex& cvx,dVector4 axis,dReal& min,dReal& max)
{
	dVector3 point;
	dReal value;
	//fprintf(stdout,"Compute Interval Axis %f,%f,%f\n",axis[0],axis[1],axis[2]);
	dMULTIPLY0_331 (point,cvx.final_posr->R,cvx.points);
	//fprintf(stdout,"initial point %f,%f,%f\n",point[0],point[1],point[2]);
	point[0]+=cvx.final_posr->pos[0];
	point[1]+=cvx.final_posr->pos[1];
	point[2]+=cvx.final_posr->pos[2];
	max = min = dDOT(axis,cvx.points);
	for (unsigned int i = 1; i < cvx.pointcount; ++i) 
	{
		dMULTIPLY0_331 (point,cvx.final_posr->R,cvx.points+(i*3));
		point[0]+=cvx.final_posr->pos[0];
		point[1]+=cvx.final_posr->pos[1];
		point[2]+=cvx.final_posr->pos[2];
		value=dDOT(axis,point);
		if(value<min)
		{
			min=value;
		}
		else if(value>max)
		{
			max=value;
		}
	}
	//fprintf(stdout,"Compute Interval Min Max %f,%f\n",min,max);
}

/*! \brief Does an axis separation test between the 2 convex shapes
using faces and edges */
int TestConvexIntersection(dxConvex& cvx1,dxConvex& cvx2, int flags,
						   dContactGeom *contact, int skip)
{
	dVector4 plane,savedplane;
	dReal min1,max1,min2,max2,min_depth=-dInfinity;
	dVector3 e1,e2,t;
	int maxc = flags & NUMC_MASK; // this is causing a segfault
	dIASSERT(maxc != 0);
	dxConvex *g1,*g2;
	unsigned int *pPoly;
	dVector3 v;
	// Test faces of cvx1 for separation
	pPoly=cvx1.polygons;
	for(unsigned int i=0;i<cvx1.planecount;++i)
	{
		// -- Apply Transforms --
		// Rotate
		dMULTIPLY0_331(plane,cvx1.final_posr->R,cvx1.planes+(i*4));
		dNormalize3(plane);
		// Translate
		plane[3]=
			(cvx1.planes[(i*4)+3])+
			((plane[0] * cvx1.final_posr->pos[0]) + 
			(plane[1] * cvx1.final_posr->pos[1]) + 
			(plane[2] * cvx1.final_posr->pos[2]));
		ComputeInterval(cvx1,plane,min1,max1);
		ComputeInterval(cvx2,plane,min2,max2);
		//fprintf(stdout,"width %f\n",max1-min1);
		if(max2<min1 || max1<min2) return 0;
#if 1
		// this one ON works
		else if ((max1>min2)&&(max1<max2))
		{
			min_depth=max1-min2;
			savedplane[0]=plane[0];
			savedplane[1]=plane[1];
			savedplane[2]=plane[2];
			savedplane[3]=plane[3];
			g1=&cvx2; // cvx2 moves
			g2=&cvx1;
		}
#endif
		pPoly+=pPoly[0]+1;
	}
	// Test faces of cvx2 for separation
	pPoly=cvx2.polygons;
	for(unsigned int i=0;i<cvx2.planecount;++i)
	{
		//       fprintf(stdout,"Poly verts %d\n",pPoly[0]);
		// -- Apply Transforms --
		// Rotate
		dMULTIPLY0_331(plane,
			cvx2.final_posr->R,
			cvx2.planes+(i*4));
		dNormalize3(plane);
		// Translate
		plane[3]=
			(cvx2.planes[(i*4)+3])+
			((plane[0] * cvx2.final_posr->pos[0]) + 
			(plane[1] * cvx2.final_posr->pos[1]) + 
			(plane[2] * cvx2.final_posr->pos[2]));
		ComputeInterval(cvx2,plane,min1,max1);
		ComputeInterval(cvx1,plane,min2,max2);
		//fprintf(stdout,"width %f\n",max1-min1);
		if(max2<min1 || max1<min2) return 0;
#if 1
		// this one ON does not work
		else if ((max1>min2)&&(max1<max2))
		{
			min_depth=max1-min2;
			savedplane[0]=plane[0];
			savedplane[1]=plane[1];
			savedplane[2]=plane[2];
			savedplane[3]=plane[3];
			g1=&cvx1; // cvx1 moves
			g2=&cvx2;
		}
#endif
		pPoly+=pPoly[0]+1;
	}
	// Test cross products of pairs of edges
	for(std::set<edge>::iterator i = cvx1.edges.begin();
		i!= cvx1.edges.end();
		++i)
	{
		// we only need to apply rotation here
		dMULTIPLY0_331 (t,cvx1.final_posr->R,cvx1.points+(i->first*3));
		dMULTIPLY0_331 (e1,cvx1.final_posr->R,cvx1.points+(i->second*3));
		e1[0]-=t[0];
		e1[1]-=t[1];
		e1[2]-=t[2];
		for(std::set<edge>::iterator j = cvx2.edges.begin();
			j!= cvx2.edges.end();
			++j)
		{
			// we only need to apply rotation here
			dMULTIPLY0_331 (t,cvx2.final_posr->R,cvx2.points+(j->first*3));
			dMULTIPLY0_331 (e2,cvx2.final_posr->R,cvx2.points+(j->second*3));
			e2[0]-=t[0];
			e2[1]-=t[1];
			e2[2]-=t[2];
			dCROSS(plane,=,e1,e2);
			plane[3]=0;
			ComputeInterval(cvx1,plane,min1,max1);
			ComputeInterval(cvx2,plane,min2,max2);
			if(max2<min1 || max1 < min2) return 0;
		}      
	}
/* -- uncomment if you are debugging
	static int  cvxhit=0;
	if(cvxhit<2)
		fprintf(stdout,"Plane: %f,%f,%f,%f\n",
		(double)savedplane[0],
		(double)savedplane[1],
		(double)savedplane[2],
		(double)savedplane[3]);
*/
	// If we get here, there was a collision
	int contacts=0;
	for(unsigned int i=0;i<g1->pointcount;++i)
	{
		dMULTIPLY0_331 (v,g1->final_posr->R,&g1->points[(i*3)]);
		dVector3Add(g1->final_posr->pos, v, v);

		dReal distance = dVector3Dot(savedplane, v) - savedplane[3]; // Ax + By + Cz - D
		if(distance<0)
		{
			dContactGeom *target = SAFECONTACT(flags, contact, contacts, skip);
			dVector3Copy(savedplane, target->normal);
			dVector3Copy(v, target->pos);
			target->depth = -distance;
			target->g1 = g1;
			target->g2 = g2;
/* -- uncomment if you are debugging
			if(cvxhit<2)
				fprintf(stdout,"Contact: %f,%f,%f depth %f\n",
				(double)target->pos[0],
				(double)target->pos[1],
				(double)target->pos[2],
				(double)target->depth);
*/
			contacts++;
			if (contacts==maxc) break;
		}
	}
/* -- uncomment if you are debugging
	cvxhit++;
*/

	return contacts;
}

int dCollideConvexConvex (dxGeom *o1, dxGeom *o2, int flags,
			  dContactGeom *contact, int skip)
{
  dIASSERT (skip >= (int)sizeof(dContactGeom));
  dIASSERT (o1->type == dConvexClass);
  dIASSERT (o2->type == dConvexClass);
  dIASSERT ((flags & NUMC_MASK) >= 1);

//   if(!hit) fprintf(stdout,"dCollideConvexConvex\n");
  dxConvex *Convex1 = (dxConvex*) o1;
  dxConvex *Convex2 = (dxConvex*) o2;
  int contacts;
  if(contacts=TestConvexIntersection(*Convex1,*Convex2,flags,
				     contact,skip))
    {
      //fprintf(stdout,"We have a Hit!\n");
    }
  return contacts;
}

#if 0

int dCollideRayConvex (dxGeom *o1, dxGeom *o2, int flags, 
		       dContactGeom *contact, int skip)
{
  dIASSERT (skip >= (int)sizeof(dContactGeom));
  dIASSERT( o1->type == dRayClass );
  dIASSERT( o2->type == dConvexClass );
  dIASSERT ((flags & NUMC_MASK) >= 1);

  dxRay* ray = (dxRay*) o1;
  dxConvex* convex = (dxConvex*) o2;
  dVector3 origin,destination,contactpoint,out;
  dReal depth;
  dVector4 plane;
  unsigned int *pPoly=convex->polygons;
  // Calculate ray origin and destination
  destination[0]=0;
  destination[1]=0;
  destination[2]= ray->length;
  // -- Rotate --
  dMULTIPLY0_331(destination,ray->final_posr->R,destination);
  origin[0]=ray->final_posr->pos[0];
  origin[1]=ray->final_posr->pos[1];
  origin[2]=ray->final_posr->pos[2];
  destination[0]+=origin[0];
  destination[1]+=origin[1];
  destination[2]+=origin[2];
  for(int i=0;i<convex->planecount;++i)
    {
      // Rotate
      dMULTIPLY0_331(plane,convex->final_posr->R,convex->planes+(i*4));
      // Translate
      plane[3]=
	(convex->planes[(i*4)+3])+
	((plane[0] * convex->final_posr->pos[0]) + 
	 (plane[1] * convex->final_posr->pos[1]) + 
	 (plane[2] * convex->final_posr->pos[2]));
      if(IntersectSegmentPlane(origin, 
			       destination, 
			       plane, 
			       depth, 
			       contactpoint))
	{
	  if(IsPointInPolygon(contactpoint,pPoly,convex,out))
	    {
	      contact->pos[0]=contactpoint[0];
	      contact->pos[1]=contactpoint[1];
	      contact->pos[2]=contactpoint[2];
	      contact->normal[0]=plane[0];
	      contact->normal[1]=plane[1];
	      contact->normal[2]=plane[2];
	      contact->depth=depth;
	      contact->g1 = ray;
	      contact->g2 = convex;
	      return 1;
	    }
	}
      pPoly+=pPoly[0]+1;
    }
  return 0;
}

#else

// Ray - Convex collider by David Walters, June 2006
int dCollideRayConvex( dxGeom *o1, dxGeom *o2,
					   int flags, dContactGeom *contact, int skip )
{
	dIASSERT( skip >= (int)sizeof(dContactGeom) );
	dIASSERT( o1->type == dRayClass );
	dIASSERT( o2->type == dConvexClass );
	dIASSERT ((flags & NUMC_MASK) >= 1);

	dxRay* ray = (dxRay*) o1;
	dxConvex* convex = (dxConvex*) o2;

	contact->g1 = ray;
	contact->g2 = convex;

	dReal alpha, beta, nsign;
	int flag;

	//
	// Compute some useful info
	//

	flag = 0;	// Assume start point is behind all planes.

	for ( unsigned int i = 0; i < convex->planecount; ++i )
	{
		// Alias this plane.
		dReal* plane = convex->planes + ( i * 4 );

		// If alpha >= 0 then start point is outside of plane.
		alpha = dDOT( plane, ray->final_posr->pos ) - plane[3];

		// If any alpha is positive, then
		// the ray start is _outside_ of the hull
		if ( alpha >= 0 )
		{
			flag = 1;
			break;
		}
	}

	// If the ray starts inside the convex hull, then everything is flipped.
	nsign = ( flag ) ? REAL( 1.0 ) : REAL( -1.0 );


	//
	// Find closest contact point
	//

	// Assume no contacts.
	contact->depth = dInfinity;

	for ( unsigned int i = 0; i < convex->planecount; ++i )
	{
		// Alias this plane.
		dReal* plane = convex->planes + ( i * 4 );

		// If alpha >= 0 then point is outside of plane.
		alpha = nsign * ( dDOT( plane, ray->final_posr->pos ) - plane[3] );

		// Compute [ plane-normal DOT ray-normal ], (/flip)
		beta = dDOT13( plane, ray->final_posr->R+2 ) * nsign;

		// Ray is pointing at the plane? ( beta < 0 )
		// Ray start to plane is within maximum ray length?
		// Ray start to plane is closer than the current best distance?
		if ( beta < -dEpsilon &&
			 alpha >= 0 && alpha <= ray->length &&
			 alpha < contact->depth )
		{
			// Compute contact point on convex hull surface.
			contact->pos[0] = ray->final_posr->pos[0] + alpha * ray->final_posr->R[0*4+2];
			contact->pos[1] = ray->final_posr->pos[1] + alpha * ray->final_posr->R[1*4+2];
			contact->pos[2] = ray->final_posr->pos[2] + alpha * ray->final_posr->R[2*4+2];

			flag = 0;

			// For all _other_ planes.
			for ( unsigned int j = 0; j < convex->planecount; ++j )
			{
				if ( i == j )
					continue;	// Skip self.

				// Alias this plane.
				dReal* planej = convex->planes + ( j * 4 );

				// If beta >= 0 then start is outside of plane.
				beta = dDOT( planej, contact->pos ) - plane[3];

				// If any beta is positive, then the contact point
				// is not on the surface of the convex hull - it's just
				// intersecting some part of its infinite extent.
				if ( beta > dEpsilon )
				{
					flag = 1;
					break;
				}
			}

			// Contact point isn't outside hull's surface? then it's a good contact!
			if ( flag == 0 )
			{
				// Store the contact normal, possibly flipped.
				contact->normal[0] = nsign * plane[0];
				contact->normal[1] = nsign * plane[1];
				contact->normal[2] = nsign * plane[2];

				// Store depth
				contact->depth = alpha;
				
				if ((flags & CONTACTS_UNIMPORTANT) && contact->depth <= ray->length )
				{
					// Break on any contact if contacts are not important
					break; 
				}
			}
		}
	}

	// Contact?
	return ( contact->depth <= ray->length );
}

#endif

//<-- Convex Collision
