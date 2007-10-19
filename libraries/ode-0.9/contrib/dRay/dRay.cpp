#include "Include\dRay.h"
#include "dxRay.h"

int dRayClass = -1;

void dAABBRay(dxGeom* Ray, dReal AABB[6]){
	dVector3 Start, End;
	dGeomRayGet(Ray, Start, End);
	dReal Length = dGeomRayGetLength(Ray);

	End[0] = Start[0] + End[0] * Length;
	End[1] = Start[1] + End[1] * Length;
	End[2] = Start[2] + End[2] * Length;
	End[3] = Start[3] + End[3] * Length;

	if (Start[0] < End[0]){
		AABB[0] = Start[0];
		AABB[1] = End[0];
	}
	else{
		AABB[0] = End[0];
		AABB[1] = Start[0];
	}

	if (Start[1] < End[1]){
		AABB[2] = Start[1];
		AABB[3] = End[1];
	}
	else{
		AABB[2] = End[1];
		AABB[3] = Start[1];
	}

	if (Start[2] < End[2]){
		AABB[4] = Start[2];
		AABB[5] = End[2];
	}
	else{
		AABB[4] = End[2];
		AABB[5] = Start[2];
	}
	// Should we tweak the box to have a minimum size for axis aligned lines? How small should it be?
}

dColliderFn* dRayColliderFn(int num){
	if (num == dPlaneClass) return (dColliderFn*)&dCollidePR;
	if (num == dSphereClass) return (dColliderFn*)&dCollideSR;
	if (num == dBoxClass) return (dColliderFn*)&dCollideBR;
	if (num == dCCylinderClass) return (dColliderFn*)&dCollideCCR;
	return 0;
}

dxGeom* dGeomCreateRay(dSpaceID space, dReal Length){
	if (dRayClass == -1){
		dGeomClass c;
		c.bytes = sizeof(dxRay);
		c.collider = &dRayColliderFn;
		c.aabb = &dAABBRay;
		c.aabb_test = 0;
		c.dtor = 0;

		dRayClass = dCreateGeomClass(&c);
	}

	dxGeom* g = dCreateGeom(dRayClass);
	if (space) dSpaceAdd(space, g);

	dGeomRaySetLength(g, Length);
	return g;
}

void dGeomRaySetLength(dxGeom* g, dReal Length){
	((dxRay*)dGeomGetClassData(g))->Length = Length;
}

dReal dGeomRayGetLength(dxGeom* g){
	return ((dxRay*)dGeomGetClassData(g))->Length;
}

void dGeomRaySet(dxGeom* g, dVector3 Origin, dVector3 Direction){
	dGeomSetPosition(g, Origin[0], Origin[1], Origin[2]);

	dVector3 Up, Right;
	dPlaneSpace(Direction, Up, Right);

	Origin[3] = Up[3] = Right[3] = REAL(0.0);

	dMatrix3 Rotation;
	Rotation[0 * 4 + 0] = Right[0];
	Rotation[1 * 4 + 0] = Right[1];
	Rotation[2 * 4 + 0] = Right[2];
	Rotation[3 * 4 + 0] = Right[3];

	Rotation[0 * 4 + 1] = Up[0];
	Rotation[1 * 4 + 1] = Up[1];
	Rotation[2 * 4 + 1] = Up[2];
	Rotation[3 * 4 + 1] = Up[3];

	Rotation[0 * 4 + 2] = Direction[0];
	Rotation[1 * 4 + 2] = Direction[1];
	Rotation[2 * 4 + 2] = Direction[2];
	Rotation[3 * 4 + 2] = Direction[3];

	dGeomSetRotation(g, Rotation);
}

void dGeomRayGet(dxGeom* g, dVector3 Origin, dVector3 Direction){
	const dReal* Position = dGeomGetPosition(g);
	Origin[0] = Position[0];
	Origin[1] = Position[1];
	Origin[2] = Position[2];
	Origin[3] = Position[3];

	const dReal* Rotation = dGeomGetRotation(g);
	Direction[0] = Rotation[0 * 4 + 2];
	Direction[1] = Rotation[1 * 4 + 2];
	Direction[2] = Rotation[2 * 4 + 2];
	Direction[3] = Rotation[3 * 4 + 2];
}