//Benoit CHAPEROT 2003-2004 www.jstarlab.com
//some code inspired by Magic Software
#include <ode/common.h>
#include <ode/collision.h>
#include <ode/matrix.h>
#include <ode/rotation.h>
#include <ode/odemath.h>
#include "collision_kernel.h"
#include "collision_std.h"
#include "collision_std_internal.h"
#include "collision_util.h"
#include <drawstuff/drawstuff.h>
#include "windows.h"
#include "ode\ode.h"

#define CONTACT(p,skip) ((dContactGeom*) (((char*)p) + (skip)))
const dReal fEPSILON = 1e-9f;

dxCone::dxCone (dSpaceID space, dReal _radius,dReal _length) :
dxGeom (space,1)
{
	dAASSERT(_radius > 0.f);
	dAASSERT(_length > 0.f);
	type = dConeClass;
	radius = _radius;
	lz = _length;
}

dxCone::~dxCone()
{
}

void dxCone::computeAABB()
{
  const dMatrix3& R = final_posr->R;
  const dVector3& pos = final_posr->pos;

	dReal xrange = dFabs(R[2]  * lz) + radius;
	dReal yrange = dFabs(R[6]  * lz) + radius;
	dReal zrange = dFabs(R[10] * lz) + radius;
	aabb[0] = pos[0] - xrange;
	aabb[1] = pos[0] + xrange;
	aabb[2] = pos[1] - yrange;
	aabb[3] = pos[1] + yrange;
	aabb[4] = pos[2] - zrange;
	aabb[5] = pos[2] + zrange;
}

dGeomID dCreateCone(dSpaceID space, dReal _radius,dReal _length)
{
	return new dxCone(space,_radius,_length);
}

void dGeomConeSetParams (dGeomID g, dReal _radius, dReal _length)
{
	dUASSERT (g && g->type == dConeClass,"argument not a cone");
	dAASSERT (_radius > 0.f);
	dAASSERT (_length > 0.f);
  g->recomputePosr();
	dxCone *c = (dxCone*) g;
	c->radius = _radius;
	c->lz = _length;
	dGeomMoved (g);
}


void dGeomConeGetParams (dGeomID g, dReal *_radius, dReal *_length)
{
	dUASSERT (g && g->type == dConeClass,"argument not a cone");
  g->recomputePosr();
	dxCone *c = (dxCone*) g;
	*_radius = c->radius;
	*_length = c->lz;
}

//positive inside
dReal dGeomConePointDepth(dGeomID g, dReal x, dReal y, dReal z)
{
	dUASSERT (g && g->type == dConeClass,"argument not a cone");

   g->recomputePosr();
	dxCone *cone = (dxCone*) g;

	dVector3 tmp,q;
	tmp[0] = x - cone->final_posr->pos[0];
	tmp[1] = y - cone->final_posr->pos[1];
	tmp[2] = z - cone->final_posr->pos[2];
	dMULTIPLY1_331 (q,cone->final_posr->R,tmp);

	dReal r = cone->radius;
	dReal h = cone->lz;

	dReal d0 = (r - r*q[2]/h) - dSqrt(q[0]*q[0]+q[1]*q[1]);
	dReal d1 = q[2];
	dReal d2 = h-q[2];
	
	if (d0 < d1) {
    if (d0 < d2) return d0; else return d2;
	}
	else {
	if (d1 < d2) return d1; else return d2;
	}
}

//plane plane
bool FindIntersectionPlanePlane(const dReal Plane0[4], const dReal Plane1[4],
	dVector3 LinePos,dVector3 LineDir)
{
    // If Cross(N0,N1) is zero, then either planes are parallel and separated
    // or the same plane.  In both cases, 'false' is returned.  Otherwise,
    // the intersection line is
    //
    //   L(t) = t*Cross(N0,N1) + c0*N0 + c1*N1
    //
    // for some coefficients c0 and c1 and for t any real number (the line
    // parameter).  Taking dot products with the normals,
    //
    //   d0 = Dot(N0,L) = c0*Dot(N0,N0) + c1*Dot(N0,N1)
    //   d1 = Dot(N1,L) = c0*Dot(N0,N1) + c1*Dot(N1,N1)
    //
    // which are two equations in two unknowns.  The solution is
    //
    //   c0 = (Dot(N1,N1)*d0 - Dot(N0,N1)*d1)/det
    //   c1 = (Dot(N0,N0)*d1 - Dot(N0,N1)*d0)/det
    //
    // where det = Dot(N0,N0)*Dot(N1,N1)-Dot(N0,N1)^2.
/*
    Real fN00 = rkPlane0.Normal().SquaredLength();
    Real fN01 = rkPlane0.Normal().Dot(rkPlane1.Normal());
    Real fN11 = rkPlane1.Normal().SquaredLength();
    Real fDet = fN00*fN11 - fN01*fN01;

    if ( Math::FAbs(fDet) < gs_fEpsilon )
        return false;

    Real fInvDet = 1.0f/fDet;
    Real fC0 = (fN11*rkPlane0.Constant() - fN01*rkPlane1.Constant())*fInvDet;
    Real fC1 = (fN00*rkPlane1.Constant() - fN01*rkPlane0.Constant())*fInvDet;

    rkLine.Direction() = rkPlane0.Normal().Cross(rkPlane1.Normal());
    rkLine.Origin() = fC0*rkPlane0.Normal() + fC1*rkPlane1.Normal();
    return true;
*/
	dReal fN00 = dLENGTHSQUARED(Plane0);
    dReal fN01 = dDOT(Plane0,Plane1);
    dReal fN11 = dLENGTHSQUARED(Plane1);
    dReal fDet = fN00*fN11 - fN01*fN01;

    if ( fabs(fDet) < fEPSILON)
        return false;

    dReal fInvDet = 1.0f/fDet;
    dReal fC0 = (fN11*Plane0[3] - fN01*Plane1[3])*fInvDet;
    dReal fC1 = (fN00*Plane1[3] - fN01*Plane0[3])*fInvDet;

    dCROSS(LineDir,=,Plane0,Plane1);
	dNormalize3(LineDir);

	dVector3 Temp0,Temp1;
	dOPC(Temp0,*,Plane0,fC0);
	dOPC(Temp1,*,Plane1,fC1);
	dOP(LinePos,+,Temp0,Temp1);

    return true;
}

//plane ray
bool FindIntersectionPlaneRay(const dReal Plane[4],
					  const dVector3 &LinePos,const dVector3 &LineDir,
					  dReal &u,dVector3 &Pos)
{
/*
	u = (A*X1 + B*Y1 + C*Z1 + D) / (A*(X1-X2) + B*(Y1-Y2)+C*(Z1-Z2))	
*/	
	dReal fDet = -dDot(Plane,LineDir,3);

	if ( fabs(fDet) < fEPSILON)
        return false;

	u = (dDot(Plane,LinePos,3) - Plane[3]) / fDet;
	dOPC(Pos,*,LineDir,u);
	dOPE(Pos,+=,LinePos);

	return true;
}

int SolveQuadraticPolynomial(dReal a,dReal b,dReal c,dReal &x0,dReal &x1)
{
	dReal d = b*b - 4*a*c;
	int NumRoots = 0;
	dReal dr;

	if (d < 0.f)
		return NumRoots;

	if (d == 0.f)
	{
		NumRoots = 1;
		dr = 0.f;
	}
	else
	{
		NumRoots = 2;
		dr = sqrtf(d);
	}

	x0 = (-b -dr) / (2.f * a);
	x1 = (-b +dr) / (2.f * a);

	return NumRoots;
}
/*
const int VALID_INTERSECTION	= 1<<0;
const int POS_TEST_FAILEDT0		= 1<<0;
const int POS_TEST_FAILEDT1		= 1<<1;
*/
int ProcessConeRayIntersectionPoint(	dReal r,dReal h,
										const dVector3 &q,const dVector3 &v,dReal t,
										dVector3 &p,
										dVector3 &n,
										int &f)
{
	dOPC(p,*,v,t);
	dOPE(p,+=,q);
	n[0] = 2*p[0];
	n[1] = 2*p[1];
	n[2] = -2*p[2]*r*r/(h*h);

	f = 0;
	if (p[2] > h)	return 0;
	if (p[2] < 0)	return 0;
	if (t > 1)		return 0;
	if (t < 0)		return 0;

	return 1;
}

//cone ray
//line in cone space (position,direction)
//distance from line position (direction normalized)(if any)
//return the number of intersection
int FindIntersectionConeRay(dReal r,dReal h,	
					 const dVector3 &q,const dVector3 &v,dContactGeom *pContact)
{
	dVector3 qp,vp;
	dOPE(qp,=,q);
	dOPE(vp,=,v);
	qp[2] = h-q[2];
	vp[2] = -v[2];
	dReal ts = (r/h);
	ts *= ts;
	dReal a = vp[0]*vp[0] + vp[1]*vp[1] - ts*vp[2]*vp[2];
	dReal b = 2.f*qp[0]*vp[0] + 2.f*qp[1]*vp[1] - 2.f*ts*qp[2]*vp[2];
	dReal c = qp[0]*qp[0] + qp[1]*qp[1] - ts*qp[2]*qp[2];

/*
	dReal a = v[0]*v[0] + v[1]*v[1] - (v[2]*v[2]*r*r) / (h*h);
	dReal b = 2.f*q[0]*v[0] + 2.f*q[1]*v[1] + 2.f*r*r*v[2]/h - 2*r*r*q[0]*v[0]/(h*h);
	dReal c = q[0]*q[0] + q[1]*q[1] + 2*r*r*q[2]/h - r*r*q[2]/(h*h) - r*r;
*/
	int nNumRoots=SolveQuadraticPolynomial(a,b,c,pContact[0].depth,pContact[1].depth);
	int flag = 0;

	dContactGeom ValidContact[2];

	int nNumValidContacts = 0;
	for (int i=0;i<nNumRoots;i++)
	{
		if (ProcessConeRayIntersectionPoint(r,h,q,v,pContact[i].depth,pContact[i].pos,
			pContact[i].normal,flag))
		{
			ValidContact[nNumValidContacts] = pContact[i];
			nNumValidContacts++;
		}
	}

	dOP(qp,+,q,v);

	if ((nNumValidContacts < 2) && (v[2] != 0.f))
	{
		dReal d = (0.f-q[2]) / (v[2]); 
		if ((d>=0) && (d<=1))
		{
			dOPC(vp,*,v,d);
			dOP(qp,+,q,vp);

			if (qp[0]*qp[0]+qp[1]*qp[1] < r*r)
			{
				dOPE(ValidContact[nNumValidContacts].pos,=,qp);
				ValidContact[nNumValidContacts].normal[0] = 0.f;
				ValidContact[nNumValidContacts].normal[1] = 0.f;
				ValidContact[nNumValidContacts].normal[2] = -1.f;
				ValidContact[nNumValidContacts].depth = d;
				nNumValidContacts++;
			}
		}
	}

	if (nNumValidContacts == 2)
	{
		if (ValidContact[0].depth > ValidContact[1].depth)
		{
			pContact[0] = ValidContact[1];
			pContact[1] = ValidContact[0];
		}
		else
		{
			pContact[0] = ValidContact[0];
			pContact[1] = ValidContact[1];
		}
	}
	else if (nNumValidContacts == 1)
	{
		pContact[0] = ValidContact[0];
	}

	return nNumValidContacts;
}

int dCollideConePlane (dxGeom *o1, dxGeom *o2, int flags,
						 dContactGeom *contact, int skip)
{
	dIASSERT (skip >= (int)sizeof(dContactGeom));
	dIASSERT (o1->type == dConeClass);
	dIASSERT (o2->type == dPlaneClass);
	dxCone *cone = (dxCone*) o1;
	dxPlane *plane = (dxPlane*) o2;

	contact->g1 = o1;
	contact->g2 = o2;

	dVector3 p0,p1,pp0,pp1;
	dOPE(p0,=,cone->final_posr->pos);
	p1[0] = cone->final_posr->R[0*4+2] * cone->lz + p0[0];
	p1[1] = cone->final_posr->R[1*4+2] * cone->lz + p0[1];
	p1[2] = cone->final_posr->R[2*4+2] * cone->lz + p0[2];

	dReal u;
	FindIntersectionPlaneRay(plane->p,p0,plane->p,u,pp0);
	FindIntersectionPlaneRay(plane->p,p1,plane->p,u,pp1);

	if (dDISTANCE(pp0,pp1) < fEPSILON)
	{
		p1[0] = cone->final_posr->R[0*4+0] * cone->lz + p0[0];
		p1[1] = cone->final_posr->R[1*4+0] * cone->lz + p0[1];
		p1[2] = cone->final_posr->R[2*4+0] * cone->lz + p0[2];
		FindIntersectionPlaneRay(plane->p,p1,plane->p,u,pp1);
		dIASSERT(dDISTANCE(pp0,pp1) >= fEPSILON);
	}
	dVector3 h,r0,r1;
	h[0] = cone->final_posr->R[0*4+2];
	h[1] = cone->final_posr->R[1*4+2];
	h[2] = cone->final_posr->R[2*4+2];
	
	dOP(r0,-,pp0,pp1);
	dCROSS(r1,=,h,r0);
	dCROSS(r0,=,r1,h);
	dNormalize3(r0);
	dOPEC(h,*=,cone->lz);
	dOPEC(r0,*=,cone->radius);

	dVector3 p[3];
	dOP(p[0],+,cone->final_posr->pos,h);
	dOP(p[1],+,cone->final_posr->pos,r0);
	dOP(p[2],-,cone->final_posr->pos,r0);
	
	int numMaxContacts = flags & 0xffff;
	if (numMaxContacts == 0) 
		numMaxContacts = 1;

	int n=0;
	for (int i=0;i<3;i++)
	{
		dReal d = dGeomPlanePointDepth(o2, p[i][0], p[i][1], p[i][2]);

		if (d>0.f)
		{
			CONTACT(contact,n*skip)->g1 = o1;
			CONTACT(contact,n*skip)->g2 = o2;
			dOPE(CONTACT(contact,n*skip)->normal,=,plane->p); 
			dOPE(CONTACT(contact,n*skip)->pos,=,p[i]); 
			CONTACT(contact,n*skip)->depth = d;
			n++;

			if (n == numMaxContacts)
				return n;
		}
	}
	
	return n;
}

int dCollideRayCone (dxGeom *o1, dxGeom *o2, int flags,
						 dContactGeom *contact, int skip)
{
	dIASSERT (skip >= (int)sizeof(dContactGeom));
	dIASSERT (o1->type == dRayClass);
	dIASSERT (o2->type == dConeClass);
	dxRay *ray = (dxRay*) o1;
	dxCone *cone = (dxCone*) o2;

	contact->g1 = o1;
	contact->g2 = o2;

	dVector3 tmp,q,v;
	tmp[0] = ray->final_posr->pos[0] - cone->final_posr->pos[0];
	tmp[1] = ray->final_posr->pos[1] - cone->final_posr->pos[1];
	tmp[2] = ray->final_posr->pos[2] - cone->final_posr->pos[2];
	dMULTIPLY1_331 (q,cone->final_posr->R,tmp);
	tmp[0] = ray->final_posr->R[0*4+2] * ray->length;
	tmp[1] = ray->final_posr->R[1*4+2] * ray->length;
	tmp[2] = ray->final_posr->R[2*4+2] * ray->length;
	dMULTIPLY1_331 (v,cone->final_posr->R,tmp);

	dReal r = cone->radius;
	dReal h = cone->lz;

	dContactGeom Contact[2];

	if (FindIntersectionConeRay(r,h,q,v,Contact))
	{
		dMULTIPLY0_331(contact->normal,cone->final_posr->R,Contact[0].normal);
		dMULTIPLY0_331(contact->pos,cone->final_posr->R,Contact[0].pos);
		dOPE(contact->pos,+=,cone->final_posr->pos);
		contact->depth = Contact[0].depth * dLENGTH(v);
/*
		dMatrix3 RI;
		dRSetIdentity (RI);
		dVector3 ss;
		ss[0] = 0.01f;
		ss[1] = 0.01f;
		ss[2] = 0.01f;

		dsSetColorAlpha (1,0,0,0.8f);
		dsDrawBox(contact->pos,RI,ss);
*/		
		return 1;
	}

	return 0;
}

int dCollideConeSphere(dxGeom *o1, dxGeom *o2, int flags, dContactGeom *contact, int skip)
{
	dIASSERT (skip >= (int)sizeof(dContactGeom));
	dIASSERT (o1->type == dConeClass);
	dIASSERT (o2->type == dSphereClass);
	dxCone		*cone = (dxCone*) o1;
	
	dxSphere ASphere(0,cone->radius);
	dGeomSetRotation(&ASphere,cone->final_posr->R);
	dGeomSetPosition(&ASphere,cone->final_posr->pos[0],cone->final_posr->pos[1],cone->final_posr->pos[2]);

	return dCollideSphereSphere(&ASphere, o2, flags, contact, skip);
}

int dCollideConeBox(dxGeom *o1, dxGeom *o2, int flags, dContactGeom *contact, int skip)
{
	dIASSERT (skip >= (int)sizeof(dContactGeom));
	dIASSERT (o1->type == dConeClass);
	dIASSERT (o2->type == dBoxClass);
	dxCone		*cone = (dxCone*) o1;
	
	dxSphere ASphere(0,cone->radius);
	dGeomSetRotation(&ASphere,cone->final_posr->R);
	dGeomSetPosition(&ASphere,cone->final_posr->pos[0],cone->final_posr->pos[1],cone->final_posr->pos[2]);

	return dCollideSphereBox(&ASphere, o2, flags, contact, skip);
}

int dCollideCCylinderCone(dxGeom *o1, dxGeom *o2, int flags, dContactGeom *contact, int skip)
{
	dIASSERT (skip >= (int)sizeof(dContactGeom));
	dIASSERT (o1->type == dCCylinderClass);
	dIASSERT (o2->type == dConeClass);
	dxCone		*cone = (dxCone*) o2;
	
	dxSphere ASphere(0,cone->radius);
	dGeomSetRotation(&ASphere,cone->final_posr->R);
	dGeomSetPosition(&ASphere,cone->final_posr->pos[0],cone->final_posr->pos[1],cone->final_posr->pos[2]);

	return dCollideCCylinderSphere(o1, &ASphere, flags, contact, skip);
}

extern int dCollideSTL(dxGeom *o1, dxGeom *o2, int flags, dContactGeom *contact, int skip);

int dCollideTriMeshCone(dxGeom *o1, dxGeom *o2, int flags, dContactGeom *contact, int skip)
{
	dIASSERT (skip >= (int)sizeof(dContactGeom));
	dIASSERT (o1->type == dTriMeshClass);
	dIASSERT (o2->type == dConeClass);
	dxCone		*cone = (dxCone*) o2;

	dxSphere ASphere(0,cone->radius);
	dGeomSetRotation(&ASphere,cone->final_posr->R);
	dGeomSetPosition(&ASphere,cone->final_posr->pos[0],cone->final_posr->pos[1],cone->final_posr->pos[2]);

	return dCollideSTL(o1, &ASphere, flags, contact, skip);
}


	


