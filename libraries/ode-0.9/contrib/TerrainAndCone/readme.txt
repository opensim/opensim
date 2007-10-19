Benoit CHAPEROT 2003-2004 www.jstarlab.com
Support for terrain and cones, collision and drawing.

Terrains can be with z up (dTerrainZ) or y up (dTerrainY).
Terrains are defined by a height field.
Terrains are now placeable.
Terrains can now be finite or infinite (repeat themselve in the x and (y or z) directions).

Terrains can potentially collide with everything that collides with planes and rays; 
see the switch statement.

Cones currently collides only with terrain and planes and rays.
Cones, with high radius to height ratios are perfect to simulate vehicle wheels on terrains.



There was an error in the depths returned by dCollideTerrain.
Plus now contacts are not sorted according to their depths.
Contact sorting is now supposed to be done externally. 
Not all dCollide functions seem to sort contacts according to depth.
Requesting a high number of contacts, sorting them and then considering only the most significant contacts
is a good way I think to improve stability.
* Cones Collisions with spheres, boxes, ccylinder and trimesh are now roughly approximated using sphere collisions.

You will need to complete the following operations (with ODE 0.039):

*** add to folder ode\src:

dCone.cpp        
dTerrainY.cpp 
dTerrainZ.cpp 
collision_std_internal.h

On linux => edit each .cpp file and comment out     #include "windows.h"    &    #include "ode\ode.h"


*** add to drawstuff\src\drawstuff.cpp:

static void drawCone(float l, float r)
{
  int i;
  float tmp,ny,nz,a,ca,sa;
  const int n = 24;	// number of sides to the cone (divisible by 4)

  a = float(M_PI*2.0)/float(n);
  sa = (float) sin(a);
  ca = (float) cos(a);

  // draw top
  glShadeModel (GL_FLAT);
  ny=1; nz=0;		  // normal vector = (0,ny,nz)
  glBegin (GL_TRIANGLE_FAN);
  glNormal3d (0,0,1);
  glVertex3d (0,0,l);
  for (i=0; i<=n; i++) {
    if (i==1 || i==n/2+1)
      setColor (color[0]*0.75f,color[1]*0.75f,color[2]*0.75f,color[3]);
    glNormal3d (ny*r,nz*r,0);
    glVertex3d (ny*r,nz*r,0);
    if (i==1 || i==n/2+1)
      setColor (color[0],color[1],color[2],color[3]);

    // rotate ny,nz
    tmp = ca*ny - sa*nz;
    nz = sa*ny + ca*nz;
    ny = tmp;
  }
  glEnd();

  // draw bottom
  ny=1; nz=0;		  // normal vector = (0,ny,nz)
  glBegin (GL_TRIANGLE_FAN);
  glNormal3d (0,0,-1);
  glVertex3d (0,0,0);
  for (i=0; i<=n; i++) {
    if (i==1 || i==n/2+1)
      setColor (color[0]*0.75f,color[1]*0.75f,color[2]*0.75f,color[3]);
    glNormal3d (0,0,-1);
    glVertex3d (ny*r,nz*r,0);
    if (i==1 || i==n/2+1)
      setColor (color[0],color[1],color[2],color[3]);

    // rotate ny,nz
    tmp = ca*ny + sa*nz;
    nz = -sa*ny + ca*nz;
    ny = tmp;
  }
  glEnd();
}

void dsDrawCone (const float pos[3], const float R[12], float length, float radius)
{
	if (current_state != 2) dsError ("drawing function called outside simulation loop");
	setupDrawingMode();
	glShadeModel (GL_SMOOTH);
	setTransform (pos,R);
	drawCone (length,radius);
	glPopMatrix();
	
	if (use_shadows) {
		setShadowDrawingMode();
		setShadowTransform();
		setTransform (pos,R);
		drawCone (length,radius);
		glPopMatrix();
		glPopMatrix();
		glDepthRange (0,1);
	}
}

void dsDrawConeD (const double pos[3], const double R[12], float length, float radius)
{
  int i;
  float pos2[3],R2[12];
  for (i=0; i<3; i++) pos2[i]=(float)pos[i];
  for (i=0; i<12; i++) R2[i]=(float)R[i];
  dsDrawCone(pos2,R2,length,radius);
}

static float GetHeight(int x,int y,int nNumNodesPerSide,float *pHeights)
{
	int nNumNodesPerSideMask = nNumNodesPerSide - 1;
	return pHeights[	(((unsigned int)(y) & nNumNodesPerSideMask) * nNumNodesPerSide)
					+	 ((unsigned int)(x) & nNumNodesPerSideMask)];
}

void dsDrawTerrainY(int x,int z,float vLength,float vNodeLength,int nNumNodesPerSide,float *pHeights,const float *pR,const float *ppos)
{
	float A[3],B[3],C[3],D[3];
	float R[12];
	float pos[3];
	if (pR)
		memcpy(R,pR,sizeof(R));
	else
	{
		memset(R,0,sizeof(R));
		R[0] = 1.f;
		R[5] = 1.f;
		R[10] = 1.f;
	}
	
	if (ppos)
		memcpy(pos,ppos,sizeof(pos));
	else
		memset(pos,0,sizeof(pos));
	
	float vx,vz;
	vx = vLength * x;
	vz = vLength * z;
	
	int i;
	for (i=0;i<nNumNodesPerSide;i++)
	{
		for (int j=0;j<nNumNodesPerSide;j++)
		{
			A[0] = i * vNodeLength + vx;
			A[2] = j * vNodeLength + vz;
			A[1] = GetHeight(i,j,nNumNodesPerSide,pHeights);
			B[0] = (i+1) * vNodeLength + vx;
			B[2] = j * vNodeLength + vz;
			B[1] = GetHeight(i+1,j,nNumNodesPerSide,pHeights);
			C[0] = i * vNodeLength + vx;
			C[2] = (j+1) * vNodeLength + vz;
			C[1] = GetHeight(i,j+1,nNumNodesPerSide,pHeights);
			D[0] = (i+1) * vNodeLength + vx;
			D[2] = (j+1) * vNodeLength + vz;
			D[1] = GetHeight(i+1,j+1,nNumNodesPerSide,pHeights);
			dsDrawTriangle(pos,R,C,B,A,1);
			dsDrawTriangle(pos,R,D,B,C,1);
		}
	}
}

void dsDrawTerrainZ(int x,int z,float vLength,float vNodeLength,int nNumNodesPerSide,float *pHeights,const float *pR,const float *ppos)
{
	float A[3],B[3],C[3],D[3];
	float R[12];
	float pos[3];
	if (pR)
		memcpy(R,pR,sizeof(R));
	else
	{
		memset(R,0,sizeof(R));
		R[0] = 1.f;
		R[5] = 1.f;
		R[10] = 1.f;
	}
	
	if (ppos)
		memcpy(pos,ppos,sizeof(pos));
	else
		memset(pos,0,sizeof(pos));
	
	float vx,vz;
	vx = vLength * x;
	vz = vLength * z;
	
	int i;
	for (i=0;i<nNumNodesPerSide;i++)
	{
		for (int j=0;j<nNumNodesPerSide;j++)
		{
			A[0] = i * vNodeLength + vx;
			A[1] = j * vNodeLength + vz;
			A[2] = GetHeight(i,j,nNumNodesPerSide,pHeights);
			B[0] = (i+1) * vNodeLength + vx;
			B[1] = j * vNodeLength + vz;
			B[2] = GetHeight(i+1,j,nNumNodesPerSide,pHeights);
			C[0] = i * vNodeLength + vx;
			C[1] = (j+1) * vNodeLength + vz;
			C[2] = GetHeight(i,j+1,nNumNodesPerSide,pHeights);
			D[0] = (i+1) * vNodeLength + vx;
			D[1] = (j+1) * vNodeLength + vz;
			D[2] = GetHeight(i+1,j+1,nNumNodesPerSide,pHeights);
			dsDrawTriangle(pos,R,C,A,B,1);
			dsDrawTriangle(pos,R,D,C,B,1);
		}
	}
}

*** add to include\drawstuff\drawstuff.h:
void dsDrawCone (const float pos[3], const float R[12],	float length, float radius);
void dsDrawConeD (const double pos[3], const double R[12], float length, float radius);
void dsDrawTerrainY(int x,int y,float vLength,float vNodeLength,int nNumNodesPerSide,float *pHeights,const float *pR,const float *ppos);
void dsDrawTerrainZ(int x,int y,float vLength,float vNodeLength,int nNumNodesPerSide,float *pHeights,const float *pR,const float *ppos);

*** add in include\ode\collision.h line 77:
/* class numbers - each geometry object needs a unique number */
enum {
  dSphereClass = 0,
  dBoxClass,
  dCCylinderClass,
  dCylinderClass,
  dPlaneClass,
  dRayClass,
  dGeomTransformClass,
  dTriMeshClass,

  dTerrainYClass,	//here
  dTerrainZClass,	//here
  dConeClass,		//here

  dFirstSpaceClass,
  dSimpleSpaceClass = dFirstSpaceClass,
  dHashSpaceClass,
  dQuadTreeSpaceClass,

  dLastSpaceClass = dQuadTreeSpaceClass,

  dFirstUserClass,
  dLastUserClass = dFirstUserClass + dMaxUserClasses - 1,
  dGeomNumClasses
};

dGeomID dCreateTerrainY (dSpaceID space, dReal *pHeights,dReal vLength,int nNumNodesPerSide, int bFinite, int bPlaceable);
dReal dGeomTerrainYPointDepth (dGeomID g, dReal x, dReal y, dReal z);
dGeomID dCreateTerrainZ (dSpaceID space, dReal *pHeights,dReal vLength,int nNumNodesPerSide, int bFinite, int bPlaceable);
dReal dGeomTerrainZPointDepth (dGeomID g, dReal x, dReal y, dReal z);

dGeomID dCreateCone(dSpaceID space, dReal radius, dReal length);
void dGeomConeSetParams (dGeomID cone, dReal radius, dReal length);
void dGeomConeGetParams (dGeomID cone, dReal *radius, dReal *length);
dReal dGeomConePointDepth(dGeomID g, dReal x, dReal y, dReal z);

*** add in include\ode\odemath.h:
#define dOP(a,op,b,c) \
  (a)[0] = ((b)[0]) op ((c)[0]); \
  (a)[1] = ((b)[1]) op ((c)[1]); \
  (a)[2] = ((b)[2]) op ((c)[2]);
#define dOPC(a,op,b,c) \
  (a)[0] = ((b)[0]) op (c); \
  (a)[1] = ((b)[1]) op (c); \
  (a)[2] = ((b)[2]) op (c);
#define dOPE(a,op,b) \
  (a)[0] op ((b)[0]); \
  (a)[1] op ((b)[1]); \
  (a)[2] op ((b)[2]);
#define dOPEC(a,op,c) \
  (a)[0] op (c); \
  (a)[1] op (c); \
  (a)[2] op (c);
#define dLENGTH(a) \
	(dSqrt( ((a)[0])*((a)[0]) + ((a)[1])*((a)[1]) + ((a)[2])*((a)[2]) ));
#define dLENGTHSQUARED(a) \
	(((a)[0])*((a)[0]) + ((a)[1])*((a)[1]) + ((a)[2])*((a)[2]));

*** add in ode\src\collision_kernel.cpp function  'static void initColliders()'  next to other 'setCollider' calls:
  setCollider (dTerrainYClass,dSphereClass,&dCollideTerrainY);
  setCollider (dTerrainYClass,dBoxClass,&dCollideTerrainY);
  setCollider (dTerrainYClass,dCCylinderClass,&dCollideTerrainY);
  setCollider (dTerrainYClass,dRayClass,&dCollideTerrainY);
  setCollider (dTerrainYClass,dConeClass,&dCollideTerrainY);

  setCollider (dTerrainZClass,dSphereClass,&dCollideTerrainZ);
  setCollider (dTerrainZClass,dBoxClass,&dCollideTerrainZ);
  setCollider (dTerrainZClass,dCCylinderClass,&dCollideTerrainZ);
  setCollider (dTerrainZClass,dRayClass,&dCollideTerrainZ);
  setCollider (dTerrainZClass,dConeClass,&dCollideTerrainZ);

  setCollider (dRayClass,dConeClass,&dCollideRayCone);
  setCollider (dConeClass,dPlaneClass,&dCollideConePlane);
  setCollider (dConeClass,dSphereClass,&dCollideConeSphere);
  setCollider (dConeClass,dBoxClass,&dCollideConeBox);
  setCollider (dCCylinderClass,dConeClass,&dCollideCCylinderCone);
  setCollider (dTriMeshClass,dConeClass,&dCollideTriMeshCone);

*** add in ode\src\collision_std.h:
int dCollideTerrainY(dxGeom *o1, dxGeom *o2, int flags,dContactGeom *contact, int skip);
int dCollideTerrainZ(dxGeom *o1, dxGeom *o2, int flags,dContactGeom *contact, int skip);

int dCollideConePlane (dxGeom *o1, dxGeom *o2, int flags,dContactGeom *contact, int skip);
int dCollideRayCone (dxGeom *o1, dxGeom *o2, int flags,dContactGeom *contact, int skip);
int dCollideConeSphere(dxGeom *o1, dxGeom *o2, int flags, dContactGeom *contact, int skip);
int dCollideConeBox(dxGeom *o1, dxGeom *o2, int flags, dContactGeom *contact, int skip);
int dCollideCCylinderCone(dxGeom *o1, dxGeom *o2, int flags, dContactGeom *contact, int skip);
int dCollideTriMeshCone(dxGeom *o1, dxGeom *o2, int flags, dContactGeom *contact, int skip);

*** add dCone.cpp, dTerrainY.cpp and dTerrainZ.cpp to the the ODE_SRC variable in the makefile
On Linux => add dCone.cpp, dTerrainY.cpp and dTerrainZ.cpp to the the libode_a_SOURCES variable in the ode/src/Makefile.am file.

*** now you can now test using file test_boxstackb.cpp (to add in folder ode\test).

