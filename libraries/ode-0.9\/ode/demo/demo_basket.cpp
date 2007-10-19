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

// Basket ball demo.
// Serves as a test for the sphere vs trimesh collider
// By Bram Stolk.
// Press the spacebar to reset the position of the ball.

#include <ode/config.h>
#include <assert.h>
#ifdef HAVE_UNISTD_H
#include <unistd.h>
#endif
#include <ode/ode.h>
#include <drawstuff/drawstuff.h>

#include "basket_geom.h" // this is our world mesh

#ifdef _MSC_VER
#pragma warning(disable:4244 4305)  // for VC++, no precision loss complaints
#endif

// some constants

#define RADIUS 0.14

// dynamics and collision objects (chassis, 3 wheels, environment)

static dWorldID world;
static dSpaceID space;

static dBodyID sphbody;
static dGeomID sphgeom;

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
	  // Paranoia  <-- not working for some people, temporarily removed for 0.6
      //dIASSERT(dVALIDVEC3(contact[i].geom.pos));
      //dIASSERT(dVALIDVEC3(contact[i].geom.normal));
      //dIASSERT(!dIsNan(contact[i].geom.depth));
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
  static float xyz[3] = {-8,0,5};
  static float hpr[3] = {0.0f,-29.5000f,0.0000f};
  dsSetViewpoint (xyz,hpr);
}



static void reset_ball(void)
{
  float sx=0.0f, sy=3.40f, sz=7.05;

#if defined(_MSC_VER) && defined(dDOUBLE)
  sy -= 0.01; // Cheat, to make it score under win32/double
#endif

  dQuaternion q;
  dQSetIdentity(q);
  dBodySetPosition (sphbody, sx, sy, sz);
  dBodySetQuaternion(sphbody, q);
  dBodySetLinearVel (sphbody, 0,0,0);
  dBodySetAngularVel (sphbody, 0,0,0);
}


// called when a key pressed

static void command (int cmd)
{
  switch (cmd) 
  {
    case ' ':
	  reset_ball();
      break;
  }
}


// simulation loop

static void simLoop (int pause)
{
  double simstep = 0.001; // 1ms simulation steps
  double dt = dsElapsedTime();

  int nrofsteps = (int) ceilf(dt/simstep);
//  fprintf(stderr, "dt=%f, nr of steps = %d\n", dt, nrofsteps);

  for (int i=0; i<nrofsteps && !pause; i++)
  {
    dSpaceCollide (space,0,&nearCallback);
    dWorldQuickStep (world, simstep);
    dJointGroupEmpty (contactgroup);
  }

  dsSetColor (1,1,1);
  const dReal *SPos = dBodyGetPosition(sphbody);
  const dReal *SRot = dBodyGetRotation(sphbody);
  float spos[3] = {SPos[0], SPos[1], SPos[2]};
  float srot[12] = { SRot[0], SRot[1], SRot[2], SRot[3], SRot[4], SRot[5], SRot[6], SRot[7], SRot[8], SRot[9], SRot[10], SRot[11] };
  dsDrawSphere
  (
    spos, 
    srot, 
    RADIUS
  );

  // draw world trimesh
  dsSetColor(0.4,0.7,0.9);
  dsSetTexture (DS_NONE);

  const dReal* Pos = dGeomGetPosition(world_mesh);
  //dIASSERT(dVALIDVEC3(Pos));
  float pos[3] = { Pos[0], Pos[1], Pos[2] };

  const dReal* Rot = dGeomGetRotation(world_mesh);
  //dIASSERT(dVALIDMAT3(Rot));
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
    fn.path_to_textures = argv[1];

  // create world
  dInitODE();
  world = dWorldCreate();
  space = dHashSpaceCreate (0);

  contactgroup = dJointGroupCreate (0);
  dWorldSetGravity (world,0,0,-9.8);
  dWorldSetQuickStepNumIterations (world, 64);

  // Create a static world using a triangle mesh that we can collide with.
  int numv = sizeof(world_vertices)/(3*sizeof(float));
  int numi = sizeof(world_indices)/ sizeof(int);
  printf("numv=%d, numi=%d\n", numv, numi);
  dTriMeshDataID Data = dGeomTriMeshDataCreate();

//  fprintf(stderr,"Building Single Precision Mesh\n");

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
  dGeomTriMeshEnableTC(world_mesh, dSphereClass, false);
  dGeomTriMeshEnableTC(world_mesh, dBoxClass, false);
  dGeomSetPosition(world_mesh, 0, 0, 0.5);
  dRSetIdentity(R);
  //dIASSERT(dVALIDMAT3(R));

  dGeomSetRotation (world_mesh, R);

  float sx=0.0, sy=3.40, sz=6.80;
  sphbody = dBodyCreate (world);
  dMassSetSphere (&m,1,RADIUS);
  dBodySetMass (sphbody,&m);
  sphgeom = dCreateSphere(0, RADIUS);
  dGeomSetBody (sphgeom,sphbody);
  reset_ball();
  dSpaceAdd (space, sphgeom);

  // run simulation
  dsSimulationLoop (argc,argv,352,288,&fn);

  // Causes segm violation? Why?
  // (because dWorldDestroy() destroys body connected to geom; must call first!)
  dGeomDestroy(sphgeom);
  dGeomDestroy (world_mesh);

  dJointGroupEmpty (contactgroup);
  dJointGroupDestroy (contactgroup);
  dSpaceDestroy (space);
  dWorldDestroy (world);
  dCloseODE();
  return 0;
}

