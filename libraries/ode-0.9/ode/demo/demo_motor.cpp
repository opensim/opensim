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

#include <ode/ode.h>
#include <drawstuff/drawstuff.h>

#ifdef _MSC_VER
#pragma warning(disable:4244 4305)  // for VC++, no precision loss complaints
#endif

// select correct drawing functions
#ifdef dDOUBLE
#define dsDrawBox dsDrawBoxD
#endif


// some constants
#define SIDE (0.5f)	// side length of a box
#define MASS (1.0)	// mass of a box


// dynamics and collision objects
static dWorldID world;
static dBodyID body[2];
static dGeomID geom[2];
static dJointID lmotor[2];
static dJointID amotor[2];
static dSpaceID space;
static dJointGroupID contactgroup;


// start simulation - set viewpoint

static void start()
{
  static float xyz[3] = {1.0382f,-1.0811f,1.4700f};
  static float hpr[3] = {135.0000f,-19.5000f,0.0000f};
  dsSetViewpoint (xyz,hpr);
  printf ("Press 'q,a,z' to control one axis of lmotor connectiong two bodies. (q is +,a is 0, z is -)\n");
  printf ("Press 'w,e,r' to control one axis of lmotor connectiong first body with world. (w is +,e is 0, r is -)\n");
}


// called when a key pressed

static void command (int cmd)
{
  if (cmd == 'q' || cmd == 'Q') {
  	dJointSetLMotorParam(lmotor[0],dParamVel,0);
  	dJointSetLMotorParam(lmotor[0],dParamVel2,0);
  	dJointSetLMotorParam(lmotor[0],dParamVel3,0.1);
  } else if (cmd == 'a' || cmd == 'A') {
  	dJointSetLMotorParam(lmotor[0],dParamVel,0);
  	dJointSetLMotorParam(lmotor[0],dParamVel2,0);
  	dJointSetLMotorParam(lmotor[0],dParamVel3,0);
  } else if (cmd == 'z' || cmd == 'Z') {
  	dJointSetLMotorParam(lmotor[0],dParamVel,0);
  	dJointSetLMotorParam(lmotor[0],dParamVel2,0);
  	dJointSetLMotorParam(lmotor[0],dParamVel3,-0.1);
  } else if (cmd == 'w' || cmd == 'W') {
  	dJointSetLMotorParam(lmotor[1],dParamVel,0.1);
  	dJointSetLMotorParam(lmotor[1],dParamVel2,0);
  	dJointSetLMotorParam(lmotor[1],dParamVel3,0);
  } else if (cmd == 'e' || cmd == 'E') {
  	dJointSetLMotorParam(lmotor[1],dParamVel,0);
  	dJointSetLMotorParam(lmotor[1],dParamVel2,0);
  	dJointSetLMotorParam(lmotor[1],dParamVel3,0);
  } else if (cmd == 'r' || cmd == 'R') {
  	dJointSetLMotorParam(lmotor[1],dParamVel,-0.1);
  	dJointSetLMotorParam(lmotor[1],dParamVel2,0);
  	dJointSetLMotorParam(lmotor[1],dParamVel3,0);
  }
  
}



static void nearCallback (void *data, dGeomID o1, dGeomID o2)
{
  // exit without doing anything if the two bodies are connected by a joint
  dBodyID b1 = dGeomGetBody(o1);
  dBodyID b2 = dGeomGetBody(o2);

  dContact contact;
  contact.surface.mode = 0;
  contact.surface.mu = dInfinity;
  if (dCollide (o1,o2,1,&contact.geom,sizeof(dContactGeom))) {
    dJointID c = dJointCreateContact (world,contactgroup,&contact);
    dJointAttach (c,b1,b2);
  }
}

// simulation loop

static void simLoop (int pause)
{
  if (!pause) {
    dSpaceCollide(space,0,&nearCallback);
    dWorldQuickStep (world,0.05);
	dJointGroupEmpty(contactgroup);
  }

  dReal sides1[3];
  dGeomBoxGetLengths(geom[0], sides1);
  dReal sides2[3];
  dGeomBoxGetLengths(geom[1], sides2);
  dsSetTexture (DS_WOOD);
  dsSetColor (1,1,0);
  dsDrawBox (dBodyGetPosition(body[0]),dBodyGetRotation(body[0]),sides1);
  dsSetColor (0,1,1);
  dsDrawBox (dBodyGetPosition(body[1]),dBodyGetRotation(body[1]),sides2);
}


int main (int argc, char **argv)
{
  // setup pointers to drawstuff callback functions
  dsFunctions fn;
  fn.version = DS_VERSION;
  fn.start = &start;
  fn.step = &simLoop;
  fn.command = &command;
  fn.stop = 0;
  fn.path_to_textures = "../../drawstuff/textures";
  if(argc>=2)
  {
    fn.path_to_textures = argv[1];
  }

  // create world
  dInitODE();
  contactgroup = dJointGroupCreate(0);
  world = dWorldCreate();
  space = dSimpleSpaceCreate(0);
  dMass m;
  dMassSetBox (&m,1,SIDE,SIDE,SIDE);
  dMassAdjust (&m,MASS);

  body[0] = dBodyCreate (world);
  dBodySetMass (body[0],&m);
  dBodySetPosition (body[0],0,0,1);
  geom[0] = dCreateBox(space,SIDE,SIDE,SIDE);
  body[1] = dBodyCreate (world);
  dBodySetMass (body[1],&m);
  dBodySetPosition (body[1],0,0,2);
  geom[1] = dCreateBox(space,SIDE,SIDE,SIDE); 

  dGeomSetBody(geom[0],body[0]);
  dGeomSetBody(geom[1],body[1]);

  lmotor[0] = dJointCreateLMotor (world,0);
  dJointAttach (lmotor[0],body[0],body[1]);
  lmotor[1] = dJointCreateLMotor (world,0);
  dJointAttach (lmotor[1],body[0],0);
  amotor[0] = dJointCreateAMotor(world,0);
  dJointAttach(amotor[0], body[0],body[1]);
  amotor[1] = dJointCreateAMotor(world,0);
  dJointAttach(amotor[1], body[0], 0);
  
  for (int i=0; i<2; i++) {
	  dJointSetAMotorNumAxes(amotor[i], 3);
	  dJointSetAMotorAxis(amotor[i],0,1,1,0,0);
	  dJointSetAMotorAxis(amotor[i],1,1,0,1,0);
	  dJointSetAMotorAxis(amotor[i],2,1,0,0,1);
	  dJointSetAMotorParam(amotor[i],dParamFMax,0.00001);
	  dJointSetAMotorParam(amotor[i],dParamFMax2,0.00001);
	  dJointSetAMotorParam(amotor[i],dParamFMax3,0.00001);

	  dJointSetAMotorParam(amotor[i],dParamVel,0);
	  dJointSetAMotorParam(amotor[i],dParamVel2,0);
	  dJointSetAMotorParam(amotor[i],dParamVel3,0);

	  dJointSetLMotorNumAxes(lmotor[i],3);
	  dJointSetLMotorAxis(lmotor[i],0,1,1,0,0);
	  dJointSetLMotorAxis(lmotor[i],1,1,0,1,0);
	  dJointSetLMotorAxis(lmotor[i],2,1,0,0,1);
	 
	  dJointSetLMotorParam(lmotor[i],dParamFMax,0.0001);
	  dJointSetLMotorParam(lmotor[i],dParamFMax2,0.0001);
	  dJointSetLMotorParam(lmotor[i],dParamFMax3,0.0001);
  }

  // run simulation
  dsSimulationLoop (argc,argv,352,288,&fn);

  dJointGroupDestroy(contactgroup);
  dSpaceDestroy (space);
  dWorldDestroy (world);
  dCloseODE();
  return 0;
}
