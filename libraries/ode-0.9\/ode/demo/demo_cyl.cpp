/*************************************************************************
 *                                                                       *
 * Open Dynamics Engine, Copyright (C) 2001,2002 Russell L. Smith.       *
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

// Test for non-capped cylinder, by Bram Stolk
#include <ode/config.h>
#include <assert.h>
#ifdef HAVE_UNISTD_H
#include <unistd.h>
#endif
#include <ode/ode.h>
#include <drawstuff/drawstuff.h>

#include "world_geom3.h" // this is our world mesh

#ifdef _MSC_VER
#pragma warning(disable:4244 4305)  // for VC++, no precision loss complaints
#endif

#define BOX
#define CYL

// some constants

#define RADIUS 0.22	// wheel radius
#define WMASS 0.2	// wheel mass
#define WHEELW 0.2	// wheel width
#define BOXSZ 0.4	// box size
//#define CYL_GEOM_OFFSET // rotate cylinder using geom offset

// dynamics and collision objects (chassis, 3 wheels, environment)

static dWorldID world;
static dSpaceID space;
#ifdef BOX
static dBodyID boxbody;
static dGeomID boxgeom;
#endif
#ifdef CYL
static dBodyID cylbody;
static dGeomID cylgeom;
#endif
static dJointGroupID contactgroup;
static dGeomID world_mesh;


// this is called by dSpaceCollide when two objects in space are
// potentially colliding.

static void nearCallback (void *data, dGeomID o1, dGeomID o2)
{
  assert(o1);
  assert(o2);

  if (dGeomIsSpace(o1) || dGeomIsSpace(o2))
  {
    fprintf(stderr,"testing space %p %p\n", o1,o2);
    // colliding a space with something
    dSpaceCollide2(o1,o2,data,&nearCallback);
    // Note we do not want to test intersections within a space,
    // only between spaces.
    return;
  }

//  fprintf(stderr,"testing geoms %p %p\n", o1, o2);

  const int N = 32;
  dContact contact[N];
  int n = dCollide (o1,o2,N,&(contact[0].geom),sizeof(dContact));
  if (n > 0) 
  {
    for (int i=0; i<n; i++) 
    {
      contact[i].surface.slip1 = 0.7;
      contact[i].surface.slip2 = 0.7;
      contact[i].surface.mode = dContactSoftERP | dContactSoftCFM | dContactApprox1 | dContactSlip1 | dContactSlip2;
      contact[i].surface.mu = 50.0; // was: dInfinity
      contact[i].surface.soft_erp = 0.96;
      contact[i].surface.soft_cfm = 0.04;
      dJointID c = dJointCreateContact (world,contactgroup,&contact[i]);
      dJointAttach (c,
		    dGeomGetBody(contact[i].geom.g1),
		    dGeomGetBody(contact[i].geom.g2));
    }
  }
}


// start simulation - set viewpoint

static void start()
{
  static float xyz[3] = {-8,-9,3};
  static float hpr[3] = {45.0000f,-27.5000f,0.0000f};
  dsSetViewpoint (xyz,hpr);
}



static void reset_state(void)
{
  float sx=-4, sy=-4, sz=2;
  dQuaternion q;
  dQFromAxisAndAngle (q,1,0,0,M_PI*0.5);
#ifdef BOX
  dBodySetPosition (boxbody, sx, sy+1, sz);
  dBodySetLinearVel (boxbody, 0,0,0);
  dBodySetAngularVel (boxbody, 0,0,0);
  dBodySetQuaternion (boxbody, q);
#endif
#ifdef CYL
  dBodySetPosition (cylbody, sx, sy, sz);
  dBodySetLinearVel (cylbody, 0,0,0);
  dBodySetAngularVel (cylbody, 0,0,0);
  dBodySetQuaternion (cylbody, q);
#endif
}


// called when a key pressed

static void command (int cmd)
{
  switch (cmd) 
  {
    case ' ':
	  reset_state();
      break;
  }
}



// simulation loop

static void simLoop (int pause)
{
  double simstep = 0.005; // 5ms simulation steps
  double dt = dsElapsedTime();
  int nrofsteps = (int) ceilf(dt/simstep);
  for (int i=0; i<nrofsteps && !pause; i++)
  {
    dSpaceCollide (space,0,&nearCallback);
    dWorldQuickStep (world, simstep);
    dJointGroupEmpty (contactgroup);
  }

  dsSetColor (1,1,1);
#ifdef BOX
  const dReal *BPos = dBodyGetPosition(boxbody);
  const dReal *BRot = dBodyGetRotation(boxbody);
  float bpos[3] = {BPos[0], BPos[1], BPos[2]};
  float brot[12] = { BRot[0], BRot[1], BRot[2], BRot[3], BRot[4], BRot[5], BRot[6], BRot[7], BRot[8], BRot[9], BRot[10], BRot[11] };
  float sides[3] = {BOXSZ, BOXSZ, BOXSZ};
  dsDrawBox
  (
    bpos, 
    brot, 
    sides
  ); // single precision
#endif
#ifdef CYL
  const dReal *CPos = dGeomGetPosition(cylgeom);
  const dReal *CRot = dGeomGetRotation(cylgeom);
  float cpos[3] = {CPos[0], CPos[1], CPos[2]};
  float crot[12] = { CRot[0], CRot[1], CRot[2], CRot[3], CRot[4], CRot[5], CRot[6], CRot[7], CRot[8], CRot[9], CRot[10], CRot[11] };
  dsDrawCylinder
  ( 
//    dBodyGetPosition(cylbody),
//    dBodyGetRotation(cylbody),
    cpos,
    crot,
    WHEELW,
    RADIUS
  ); // single precision
#endif

  // draw world trimesh
  dsSetColor(0.7,0.7,0.4);
  dsSetTexture (DS_NONE);

  const dReal* Pos = dGeomGetPosition(world_mesh);
  float pos[3] = { Pos[0], Pos[1], Pos[2] };

  const dReal* Rot = dGeomGetRotation(world_mesh);
  float rot[12] = { Rot[0], Rot[1], Rot[2], Rot[3], Rot[4], Rot[5], Rot[6], Rot[7], Rot[8], Rot[9], Rot[10], Rot[11] };

  int numi = sizeof(world_indices)  / sizeof(int);

  for (int i=0; i<numi/3; i++)
  {
    int i0 = world_indices[i*3+0];
    int i1 = world_indices[i*3+1];
    int i2 = world_indices[i*3+2];
    float *v0 = world_vertices+i0*3;
    float *v1 = world_vertices+i1*3;
    float *v2 = world_vertices+i2*3;
    dsDrawTriangle(pos, rot, v0,v1,v2, true); // single precision draw
  }
}


int main (int argc, char **argv)
{
  dMass m;
  dMatrix3 R;

  // setup pointers to drawstuff callback functions
  dsFunctions fn;
  fn.version = DS_VERSION;
  fn.start = &start;
  fn.step = &simLoop;
  fn.command = &command;
  fn.stop = 0;
  fn.path_to_textures = "../../drawstuff/textures";
  if(argc==2)
    {
        fn.path_to_textures = argv[1];
    }

  // create world
  dInitODE();
  world = dWorldCreate();
  space = dHashSpaceCreate (0);
  contactgroup = dJointGroupCreate (0);
  dWorldSetGravity (world,0,0,-9.8);
  dWorldSetQuickStepNumIterations (world, 12);


  // Create a static world using a triangle mesh that we can collide with.
  int numv = sizeof(world_vertices)/(3*sizeof(float));
  int numi = sizeof(world_indices)/ sizeof(int);
  printf("numv=%d, numi=%d\n", numv, numi);
  dTriMeshDataID Data = dGeomTriMeshDataCreate();

  dGeomTriMeshDataBuildSingle
  (
    Data, 
    world_vertices, 
    3 * sizeof(float), 
    numv, 
    world_indices, 
    numi, 
    3 * sizeof(int)
  );

  world_mesh = dCreateTriMesh(space, Data, 0, 0, 0);
  dGeomSetPosition(world_mesh, 0, 0, 0.5);
  dRFromAxisAndAngle (R, 0,1,0, 0.0);
  dGeomSetRotation (world_mesh, R);


#ifdef BOX
  boxbody = dBodyCreate (world);
  dMassSetBox (&m,1, BOXSZ, BOXSZ, BOXSZ);
  dMassAdjust (&m, 1);
  dBodySetMass (boxbody,&m);
  boxgeom = dCreateBox (0, BOXSZ, BOXSZ, BOXSZ);
  dGeomSetBody (boxgeom,boxbody);
  dSpaceAdd (space, boxgeom);
#endif
#ifdef CYL
  cylbody = dBodyCreate (world);
  dMassSetSphere (&m,1,RADIUS);
  dMassAdjust (&m,WMASS);
  dBodySetMass (cylbody,&m);
  cylgeom = dCreateCylinder(0, RADIUS, WHEELW);
  dGeomSetBody (cylgeom,cylbody);
  
  #if defined(CYL_GEOM_OFFSET)
  dMatrix3 mat;
  dRFromAxisAndAngle(mat,1.0f,0.0f,0.0f,M_PI/2.0);
  dGeomSetOffsetRotation(cylgeom,mat);
  #endif
    
  dSpaceAdd (space, cylgeom);
#endif
  reset_state();

  // run simulation
  dsSimulationLoop (argc,argv,352,288,&fn);

  dJointGroupEmpty (contactgroup);
  dJointGroupDestroy (contactgroup);

  // First destroy geoms, then space, then the world.
#ifdef CYL
  dGeomDestroy (cylgeom);
#endif
#ifdef BOX
  dGeomDestroy (boxgeom);
#endif
  dGeomDestroy (world_mesh);

  dSpaceDestroy (space);
  dWorldDestroy (world);
  dCloseODE();
  return 0;
}

