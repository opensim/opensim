// Ripped from Paul Bourke

#include "Include\dRay.h"
#include "dxRay.h"

int dCollidePR(dxGeom* RayGeom, dxGeom* PlaneGeom, int Flags, dContactGeom* Contact, int Stride){
	dVector3 Plane;
	dGeomPlaneGetParams(PlaneGeom, Plane);

	dVector3 Origin, Direction;
	dGeomRayGet(RayGeom, Origin, Direction);

	dReal Length = dGeomRayGetLength(RayGeom);

	dReal Denom = Plane[0] * Direction[0] + Plane[1] * Direction[1] + Plane[2] * Direction[2];
	if (dFabs(Denom) < 0.00001f){
		return 0;	// Ray never hits
	}
	
	float T = -(Plane[3] + Plane[0] * Origin[0] + Plane[1] * Origin[1] + Plane[2] * Origin[2]) / Denom;
	
	if (T < 0 || T > Length){
		return 0;	// Ray hits but not within boundaries
	}

	Contact->pos[0] = Origin[0] + T * Direction[0];
	Contact->pos[1] = Origin[1] + T * Direction[1];
	Contact->pos[2] = Origin[2] + T * Direction[2];
	Contact->pos[3] = REAL(0.0);
	//Contact->normal = 0;
	Contact->depth = 0.0f;
	Contact->g1 = RayGeom;
	Contact->g2 = PlaneGeom;
	return 1;
}