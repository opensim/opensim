readme.txt

WARNING: THIS IS NOT VERY RELIABLE CODE.  IT HAS BUGS.  YOUR
         SUCCESS MAY VARY.  CONTRIBUTIONS OF FIXES/REWRITES ARE
         WELCOME.

///////////////////////////////////////////////////////////////////////

Cylinder geometry class.

New in this version:

Cylinder class implemented as User Geometry Class so it now can be
used with old and new ODE collision detection.

Cylinder - Ray has been contributed by Olivier Michel.

THE IDENTIFIER dCylinderClass HAS BEEN REPLACED BY dCylinderClassUser

to avoid conflict with dCylinderClass in the enum definite in collision.h

///////////////////////////////////////////////////////////////////////
The dCylinder class includes the following collisions:

Cylinder - Box
Cylinder - Cylinder
Cylinder - Sphere
Cylinder - Plane
Cylinder - Ray (contributed by Olivier Michel)

Cylinder aligned along axis - Y when created. (Not like Capped
Cylinder which aligned along axis - Z).

Interface is just the same as  Capped Cylinder has.

Use functions which have one "C" instead of double "C".

to create:
dGeomID dCreateCylinder (dSpaceID space, dReal radius, dReal length);

to set params:
void dGeomCylinderSetParams (dGeomID cylinder,
                              dReal radius, dReal length);


to get params:
void dGeomCylinderGetParams (dGeomID cylinder,
                              dReal *radius, dReal *length);

Return in radius and length the parameters of the given cylinder.

Identification number of the class:
 dCylinderClassUser

 I do not include a function that sets inertia tensor for cylinder.
 One may use existing ODE functions dMassSetCappedCylinder or dMassSetBox.
 To set exact tensor for cylinder use dMassSetParameters.
 Remember cylinder aligned along axis - Y.
 
 ///////////////////////////////////////////////////////////////////////////
 Konstantin Slipchenko
 February 5, 2002
