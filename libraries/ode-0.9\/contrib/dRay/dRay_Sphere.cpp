// Ripped from Magic Software

#include "Include\dRay.h"
#include "dxRay.h"

int dCollideSR(dxGeom* RayGeom, dxGeom* SphereGeom, int Flags, dContactGeom* Contacts, int Stride){
	const dVector3& Position = *(const dVector3*)dGeomGetPosition(SphereGeom);
	dReal Radius = dGeomSphereGetRadius(SphereGeom);

	dVector3 Origin, Direction;
	dGeomRayGet(RayGeom, Origin, Direction);
	dReal Length = dGeomRayGetLength(RayGeom);

	dVector3 Diff;
	Diff[0] = Origin[0] - Position[0];
	Diff[1] = Origin[1] - Position[1];
	Diff[2] = Origin[2] - Position[2];
	Diff[3] = Origin[3] - Position[3];

	Direction[0] *= Length;
	Direction[1] *= Length;
	Direction[2] *= Length;
	Direction[3] *= Length;

	dReal A = Length * Length;
	dReal B = dDOT(Diff, Direction);
	dReal C = dDOT(Diff, Diff) - (Radius * Radius);

	dReal Discr = B * B - A * C;
	if (Discr < REAL(0.0)){
		return 0;
	}
	else if (Discr > REAL(0.0)){
		dReal T[2];
		dReal Root = dSqrt(Discr);
		dReal InvA = REAL(1.0) / A;
		T[0] = (-B - Root) * InvA;
		T[1] = (-B + Root) * InvA;

		if (T[0] >= REAL(0.0)){
			dContactGeom* Contact0 = CONTACT(Flags, Contacts, 0, Stride);
			Contact0->pos[0] = Origin[0] + T[0] * Direction[0];
			Contact0->pos[1] = Origin[1] + T[0] * Direction[1];
			Contact0->pos[2] = Origin[2] + T[0] * Direction[2];
			Contact0->pos[3] = Origin[3] + T[0] * Direction[3];
			//Contact0->normal = 0;
			Contact0->depth = 0.0f;
			Contact0->g1 = RayGeom;
			Contact0->g2 = SphereGeom;

			dContactGeom* Contact1 = CONTACT(Flags, Contacts, 1, Stride);
			Contact1->pos[0] = Origin[0] + T[1] * Direction[0];
			Contact1->pos[1] = Origin[1] + T[1] * Direction[1];
			Contact1->pos[2] = Origin[2] + T[1] * Direction[2];
			Contact1->pos[3] = Origin[3] + T[1] * Direction[3];
			//Contact1->normal = 0;
			Contact1->depth = 0.0f;
			Contact1->g1 = RayGeom;
			Contact1->g2 = SphereGeom;

			return 2;
		}
		else if (T[1] >= REAL(0.0)){
			dContactGeom* Contact = CONTACT(Flags, Contacts, 1, Stride);
			Contact->pos[0] = Origin[0] + T[1] * Direction[0];
			Contact->pos[1] = Origin[1] + T[1] * Direction[1];
			Contact->pos[2] = Origin[2] + T[1] * Direction[2];
			Contact->pos[3] = Origin[3] + T[1] * Direction[3];
			//Contact->normal = 0;
			Contact->depth = 0.0f;
			Contact->g1 = RayGeom;
			Contact->g2 = SphereGeom;

            return 1;
		}
		else return 0;
	}
	else{
		dReal T;
		T = -B / A;
		if (T >= REAL(0.0)){
			dContactGeom* Contact = CONTACT(Flags, Contacts, 0, Stride);
			Contact->pos[0] = Origin[0] + T * Direction[0];
			Contact->pos[1] = Origin[1] + T * Direction[1];
			Contact->pos[2] = Origin[2] + T * Direction[2];
			Contact->pos[3] = Origin[3] + T * Direction[3];
			//Contact->normal = 0;
			Contact->depth = 0.0f;
			Contact->g1 = RayGeom;
			Contact->g2 = SphereGeom;
            return 1;
		}
		else return 0;
	}
}