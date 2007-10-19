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

// Test for cylinder vs sphere, by Bram Stolk

#include <ode/config.h>
#include <assert.h>
#ifdef HAVE_UNISTD_H
#include <unistd.h>
#endif
#include <ode/ode.h>
#include <drawstuff/drawstuff.h>

#ifdef _MSC_VER
#pragma warning(disable:4244 4305)  // for VC++, no precision loss complaints
#endif


// dynamics and collision objects (chassis, 3 wheels, environment)

static dWorldID world;
static dSpaceID space;

static dBodyID cylbody;
static dGeomID cylgeom;

static dBodyID sphbody;
static dGeomID sphgeom;

static dJointGroupID contactgroup;
static dGeomID world_mesh;

static bool show_contacts = true;

#define CYLRADIUS    0.6
#define CYLLENGTH    2.0
#define SPHERERADIUS 0.5

    
#ifdef dDOUBLE
#define dsDrawBox dsDrawBoxD
#define dsDrawLine dsDrawLineD
#endif



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

  const int N = 32;
  dContact contact[N];
  int n = dCollide (o1,o2,N,&(contact[0].geom),sizeof(dContact));
  if (n > 0) 
  {
    for (int i=0; i<n; i++) 
    {
      contact[i].surface.mode = 0;
      contact[i].surface.mu = 50.0; // was: dInfinity
      dJointID c = dJointCreateContact (world,contactgroup,&contact[i]);
      dJointAttach (c, dGeomGetBody(contact[i].geom.g1), dGeomGetBody(contact[i].geom.g2));
      if (show_contacts) 
      {
        dMatrix3 RI;
        dRSetIdentity (RI);
        const dReal ss[3] = {0.12,0.12,0.12};
        dsSetColorAlpha (0,0,1,0.5);
        dsDrawBox (contact[i].geom.pos,RI,ss);
        dReal *pos  = contact[i].geom.pos;
        dReal depth = contact[i].geom.depth;
        dReal *norm = contact[i].geom.normal;
        dReal endp[3] = {pos[0]+depth*norm[0], pos[1]+depth*norm[1], pos[2]+depth*norm[2]};
        dsSetColorAlpha (1,1,1,1);
        dsDrawLine (contact[i].geom.pos, endp);
      }
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


// called when a key pressed

static void command (int cmd)
{
  switch (cmd) 
  {
    case ' ':
      break;
  }
}



// simulation loop

static void simLoop (int pause)
{
  dSpaceCollide (space,0,&nearCallback);
  if (!pause)
  {
    dWorldQuickStep (world, 0.01); // 100 Hz
  }
  dJointGroupEmpty (contactgroup);

  dsSetColorAlpha (1,1,0,0.5);

  const dReal *CPos = dBodyGetPosition(cylbody);
  const dReal *CRot = dBodyGetRotation(cylbody);
  float cpos[3] = {CPos[0], CPos[1], CPos[2]};
  float crot[12] = { CRot[0], CRot[1], CRot[2], CRot[3], CRot[4], CRot[5], CRot[6], CRot[7], CRot[8], CRot[9], CRot[10], CRot[11] };
  dsDrawCylinder
  (
    cpos,
    crot,
    CYLLENGTH,
    CYLRADIUS
  ); // single precision

  const dReal *SPos = dBodyGetPosition(sphbody);
  const dReal *SRot = dBodyGetRotation(sphbody);
  float spos[3] = {SPos[0], SPos[1], SPos[2]};
  float srot[12] = { SRot[0], SRot[1], SRot[2], SRot[3], SRot[4], SRot[5], SRot[6], SRot[7], SRot[8], SRot[9], SRot[10], SRot[11] };
  dsDrawSphere
  (
    spos,
    srot,
    SPHERERADIUS
  ); // single precision
}


int main (int argc, char **argv)
{
  dMass m;

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
  dWorldSetQuickStepNumIterations (world, 32);

  dCreatePlane (space,0,0,1, 0.0);

  cylbody = dBodyCreate (world);
  dQuaternion q;
#if 0
  dQFromAxisAndAngle (q,1,0,0,M_PI*0.5);
#else
//  dQFromAxisAndAngle (q,1,0,0, M_PI * 1.0);
  dQFromAxisAndAngle (q,1,0,0, M_PI * -0.77);
#endif
  dBodySetQuaternion (cylbody,q);
  dMassSetCylinder (&m,1.0,3,CYLRADIUS,CYLLENGTH);
  dBodySetMass (cylbody,&m);
  cylgeom = dCreateCylinder(0, CYLRADIUS, CYLLENGTH);
  dGeomSetBody (cylgeom,cylbody);
  dBodySetPosition (cylbody, 0, 0, 3);
  dSpaceAdd (space, cylgeom);

  sphbody = dBodyCreate (world);
  dMassSetSphere (&m,1,SPHERERADIUS);
  dBodySetMass (sphbody,&m);
  sphgeom = dCreateSphere(0, SPHERERADIUS);
  dGeomSetBody (sphgeom,sphbody);
  dBodySetPosition (sphbody, 0, 0, 5.5);
  dSpaceAdd (space, sphgeom);

  // run simulation
  dsSimulationLoop (argc,argv,352,288,&fn);

  dJointGroupEmpty (contactgroup);
  dJointGroupDestroy (contactgroup);

  dGeomDestroy(sphgeom);
  dGeomDestroy (cylgeom);

  dSpaceDestroy (space);
  dWorldDestroy (world);
  dCloseODE();
  return 0;
}

