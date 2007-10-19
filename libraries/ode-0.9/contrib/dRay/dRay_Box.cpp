// Ripped from Magic Software

#include "Include\dRay.h"
#include "dxRay.h"

bool Clip(dReal Denom, dReal Numer, dReal& T0, dReal& T1){
    // Return value is 'true' if line segment intersects the current test
    // plane.  Otherwise 'false' is returned in which case the line segment
    // is entirely clipped.
	
    if (Denom > REAL(0.0)){
		if (Numer > Denom * T1){
            return false;
		}

        if (Numer > Denom * T0){
            T0 = Numer / Denom;
		}
        return true;
    }
    else if (Denom < REAL(0.0)){
        if (Numer > Denom * T0){
            return false;
		}

        if (Numer > Denom * T1){
            T1 = Numer / Denom;
		}
        return true;
    }
    else return Numer <= REAL(0.0);
}

bool FindIntersection(const dVector3 Origin, const dVector3 Direction, const dVector3 Extents, dReal& T0, dReal& T1){
    dReal SaveT0 = T0;
	dReal SaveT1 = T1;

    bool NotEntirelyClipped =
        Clip(+Direction[0], -Origin[0] - Extents[0], T0, T1) &&
        Clip(-Direction[0], +Origin[0] - Extents[0], T0, T1) &&
        Clip(+Direction[1], -Origin[1] - Extents[1], T0, T1) &&
        Clip(-Direction[1], +Origin[1] - Extents[1], T0, T1) &&
        Clip(+Direction[2], -Origin[2] - Extents[2], T0, T1) &&
        Clip(-Direction[2], +Origin[2] - Extents[2], T0, T1);

    return NotEntirelyClipped && (T0 != SaveT0 || T1 != SaveT1);
}

int dCollideBR(dxGeom* RayGeom, dxGeom* BoxGeom, int Flags, dContactGeom* Contacts, int Stride){
	const dVector3& Position = *(const dVector3*)dGeomGetPosition(BoxGeom);
	const dMatrix3& Rotation = *(const dMatrix3*)dGeomGetRotation(BoxGeom);
	dVector3 Extents;
	dGeomBoxGetLengths(BoxGeom, Extents);
	Extents[0] /= 2;
	Extents[1] /= 2;
	Extents[2] /= 2;
	Extents[3] /= 2;

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

	dVector3 Rot[3];
	Decompose(Rotation, Rot);

	dVector3 TransOrigin;
	TransOrigin[0] = dDOT(Diff, Rot[0]);
	TransOrigin[1] = dDOT(Diff, Rot[1]);
	TransOrigin[2] = dDOT(Diff, Rot[2]);
	TransOrigin[3] = REAL(0.0);

	dVector3 TransDirection;
	TransDirection[0] = dDOT(Direction, Rot[0]);
	TransDirection[1] = dDOT(Direction, Rot[1]);
	TransDirection[2] = dDOT(Direction, Rot[2]);
	TransDirection[3] = REAL(0.0);

	dReal T[2];
	T[0] = 0.0f;
	T[1] = dInfinity;

	bool Intersect = FindIntersection(TransOrigin, TransDirection, Extents, T[0], T[1]);

	if (Intersect){
		if (T[0] > REAL(0.0)){
			dContactGeom* Contact0 = CONTACT(Flags, Contacts, 0, Stride);
			Contact0->pos[0] = Origin[0] + T[0] * Direction[0];
			Contact0->pos[1] = Origin[1] + T[0] * Direction[1];
			Contact0->pos[2] = Origin[2] + T[0] * Direction[2];
			Contact0->pos[3] = Origin[3] + T[0] * Direction[3];
			//Contact0->normal = 0;
			Contact0->depth = 0.0f;
			Contact0->g1 = RayGeom;
			Contact0->g2 = BoxGeom;

			dContactGeom* Contact1 = CONTACT(Flags, Contacts, 1, Stride);
			Contact1->pos[0] = Origin[0] + T[1] * Direction[0];
			Contact1->pos[1] = Origin[1] + T[1] * Direction[1];
			Contact1->pos[2] = Origin[2] + T[1] * Direction[2];
			Contact1->pos[3] = Origin[3] + T[1] * Direction[3];
			//Contact1->normal = 0;
			Contact1->depth = 0.0f;
			Contact1->g1 = RayGeom;
			Contact1->g2 = BoxGeom;

			return 2;
		}
		else{
			dContactGeom* Contact = CONTACT(Flags, Contacts, 0, Stride);
			Contact->pos[0] = Origin[0] + T[1] * Direction[0];
			Contact->pos[1] = Origin[1] + T[1] * Direction[1];
			Contact->pos[2] = Origin[2] + T[1] * Direction[2];
			Contact->pos[3] = Origin[3] + T[1] * Direction[3];
			//Contact->normal = 0;
			Contact->depth = 0.0f;
			Contact->g1 = RayGeom;
			Contact->g2 = BoxGeom;

			return 1;
		}
	}
	else return 0;
}