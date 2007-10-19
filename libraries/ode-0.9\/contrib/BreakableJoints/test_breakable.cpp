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

/*

buggy with suspension.
this also shows you how to use geom groups.

*/


#include <stdlib.h>

#include <ode/ode.h>
#include <drawstuff/drawstuff.h>

#ifdef _MSC_VER
#pragma warning(disable:4244 4305)  // for VC++, no precision loss complaints
#endif

// select correct drawing functions

#ifdef dDOUBLE
#define dsDrawBox dsDrawBoxD
#define dsDrawSphere dsDrawSphereD
#define dsDrawCylinder dsDrawCylinderD
#define dsDrawCappedCylinder dsDrawCappedCylinderD
#endif


// some constants

#define LENGTH 0.7	// chassis length
#define WIDTH 0.4	// chassis width
#define HEIGHT 0.2	// chassis height
#define RADIUS 0.22	// wheel radius
#define STARTZ 0.4	// starting height of chassis
#define CMASS 1		// chassis mass
#define WMASS 0.2	// wheel mass

// dynamics and collision objects (chassis, 4 wheels, environment, obstacles, chain)
static dWorldID world;
static dSpaceID space;

// chain stuff
static const float chain_radius = 0.1;
static const float chain_mass = 0.1;
static const int chain_num = 10;
static dBodyID chain_body[chain_num];
static dGeomID chain_geom[chain_num];
static dJointID chain_joint[chain_num-1]; 

// 1 chasses, 4 wheels
static dBodyID body[5];
// joint[0] is left front wheel, joint[1] is right front wheel
static dJointID joint[4]; 
static int joint_exists[4];
static dJointGroupID contactgroup;
static dGeomID ground;
static dSpaceID car_space;
static dGeomID box[1];
static dGeomID sphere[4];
static dGeomID ground_box;
static const int obstacle_num = 25;
static dGeomID obstacle[obstacle_num];

// things that the user controls

static dReal speed=0,steer=0;	// user commands



// this is called by dSpaceCollide when two objects in space are
// potentially colliding.

static void nearCallback (void *data, dGeomID o1, dGeomID o2)
{
  int i,n;

//   // do not collide objects that are connected
//   dBodyID b1 = dGeomGetBody (o1),
//           b2 = dGeomGetBody (o2);
//   if (b1 && b2 && dAreConnected(b1, b2)) return;
  
  const int N = 10;
  dContact contact[N];
  n = dCollide (o1,o2,N,&contact[0].geom,sizeof(dContact));
  if (n > 0) {
    for (i=0; i<n; i++) {
      contact[i].surface.mode = dContactSlip1 | dContactSlip2 |
	dContactSoftERP | dContactSoftCFM | dContactApprox1;
      contact[i].surface.mu = dInfinity;
      contact[i].surface.slip1 = 0.1;
      contact[i].surface.slip2 = 0.1;
      contact[i].surface.soft_erp = 0.5;
      contact[i].surface.soft_cfm = 0.3;
      dJointID c = dJointCreateContact (world,contactgroup,&contact[i]);
      dJointAttach (c,
		    dGeomGetBody(contact[i].geom.g1),
		    dGeomGetBody(contact[i].geom.g2));
    }
  }
}

// callback function for joints that break
static void jointBreakCallback (dJointID j)
{
       if (j == joint[0]) joint_exists[0] = 0;
  else if (j == joint[1]) joint_exists[1] = 0;
  else if (j == joint[2]) joint_exists[2] = 0;
  else if (j == joint[3]) joint_exists[3] = 0;
  printf ("A joint just broke\n");
}

// start simulation - set viewpoint

static void start()
{
  static float xyz[3] = {0.8317f,-0.9817f,0.8000f};
  static float hpr[3] = {121.0000f,-27.5000f,0.0000f};
  dsSetViewpoint (xyz,hpr);
  printf ("Press:\t'a' to increase speed.\n"
	  "\t'z' to decrease speed.\n"
	  "\t',' to steer left.\n"
	  "\t'.' to steer right.\n"
	  "\t' ' to reset speed and steering.\n");
}


// called when a key pressed

static void command (int cmd)
{
  switch (cmd) {
  case 'a': case 'A':
    speed += 0.3;
    break;
  case 'z': case 'Z':
    speed -= 0.3;
    break;
  case ',':
    steer -= 0.5;
    break;
  case '.':
    steer += 0.5;
    break;
  case ' ':
    speed = 0;
    steer = 0;
    break;
  }
}


// simulation loop

static void simLoop (int pause)
{
  int i;
  if (!pause) {
    for (i=0; i<2; i++) {
  	  if (joint_exists[i]) {
        // motor
        dJointSetHinge2Param (joint[i],dParamVel2,-speed);
        dJointSetHinge2Param (joint[i],dParamFMax2,0.1);

        // steering
        dReal v = steer - dJointGetHinge2Angle1 (joint[i]);
        if (v > 0.1) v = 0.1;
        if (v < -0.1) v = -0.1;
        v *= 10.0;
        dJointSetHinge2Param (joint[i],dParamVel,v);
        dJointSetHinge2Param (joint[i],dParamFMax,0.2);
        dJointSetHinge2Param (joint[i],dParamLoStop,-0.75);
        dJointSetHinge2Param (joint[i],dParamHiStop,0.75);
        dJointSetHinge2Param (joint[i],dParamFudgeFactor,0.1);
	  }
	}
	
    dSpaceCollide (space,0,&nearCallback);
    //dWorldStep (world,0.05);
	dWorldStepFast1 (world,0.05,5);

    // remove all contact joints
    dJointGroupEmpty (contactgroup);
  }

  dsSetColor (0,1,1);
  dsSetTexture (DS_WOOD);
  dReal sides[3] = {LENGTH,WIDTH,HEIGHT};
  dsDrawBox (dBodyGetPosition(body[0]),dBodyGetRotation(body[0]),sides);
  dsSetColor (1,1,1);
  for (i=1; i<=4; i++) 
    dsDrawCylinder (dBodyGetPosition(body[i]),
                    dBodyGetRotation(body[i]),
					0.2,
                    RADIUS);

  dVector3 ss;
  dGeomBoxGetLengths (ground_box,ss);
  dsDrawBox (dGeomGetPosition(ground_box),dGeomGetRotation(ground_box),ss);
  
  dsSetColor (1,0,0);
  for (i=0; i<obstacle_num; i++) {
    dVector3 ss;
	dGeomBoxGetLengths (obstacle[i],ss);
	dsDrawBox (dGeomGetPosition(obstacle[i]),dGeomGetRotation(obstacle[i]),ss);
  }

  dsSetColor (1,1,0);
  for (i=0; i<chain_num; i++) {
    dsDrawSphere (dGeomGetPosition(chain_geom[i]),dGeomGetRotation(chain_geom[i]),chain_radius);
  }
  
  /*
  printf ("%.10f %.10f %.10f %.10f\n",
	  dJointGetHingeAngle (joint[1]),
	  dJointGetHingeAngle (joint[2]),
	  dJointGetHingeAngleRate (joint[1]),
	  dJointGetHingeAngleRate (joint[2]));
  */
}

int main (int argc, char **argv)
{
  int i;
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
    {
        fn.path_to_textures = argv[1];
    }
  // create world

  world = dWorldCreate();
  space = dHashSpaceCreate (0);
  contactgroup = dJointGroupCreate (0);
  dWorldSetGravity (world,0,0,-0.5);
  ground = dCreatePlane (space,0,0,1,0);

  // chassis body
  body[0] = dBodyCreate (world);
  dBodySetPosition (body[0],0,0,STARTZ);
  dMassSetBox (&m,1,LENGTH,WIDTH,HEIGHT);
  dMassAdjust (&m,CMASS);
  dBodySetMass (body[0],&m);
  box[0] = dCreateBox (0,LENGTH,WIDTH,HEIGHT);
  dGeomSetBody (box[0],body[0]);
  
  // a chain
  for (i=0; i<chain_num; i++) {
    chain_body[i] = dBodyCreate (world);
	dBodySetPosition (chain_body[i],-LENGTH-(i*2*chain_radius),0,STARTZ-HEIGHT*0.5);
    dMassSetSphere (&m,1,chain_radius);
    dMassAdjust (&m,chain_mass);
    dBodySetMass (chain_body[i],&m);
    chain_geom[i] = dCreateSphere (space,chain_radius);
    dGeomSetBody (chain_geom[i],chain_body[i]);
  }
  
  // wheel bodies
  for (i=1; i<=4; i++) {
    body[i] = dBodyCreate (world);
    dQuaternion q;
    dQFromAxisAndAngle (q,1,0,0,M_PI*0.5);
    dBodySetQuaternion (body[i],q);
    dMassSetSphere (&m,1,RADIUS);
    dMassAdjust (&m,WMASS);
    dBodySetMass (body[i],&m);
    sphere[i-1] = dCreateSphere (0,RADIUS);
    dGeomSetBody (sphere[i-1],body[i]);
  }
  dBodySetPosition (body[1],  0.5*LENGTH,  WIDTH*1.0, STARTZ-HEIGHT*0.5);
  dBodySetPosition (body[2],  0.5*LENGTH, -WIDTH*1.0, STARTZ-HEIGHT*0.5);
  dBodySetPosition (body[3], -0.5*LENGTH,  WIDTH*1.0, STARTZ-HEIGHT*0.5);
  dBodySetPosition (body[4], -0.5*LENGTH, -WIDTH*1.0, STARTZ-HEIGHT*0.5);

  // front wheel hinge
  /*
  joint[0] = dJointCreateHinge2 (world,0);
  dJointAttach (joint[0],body[0],body[1]);
  const dReal *a = dBodyGetPosition (body[1]);
  dJointSetHinge2Anchor (joint[0],a[0],a[1],a[2]);
  dJointSetHinge2Axis1 (joint[0],0,0,1);
  dJointSetHinge2Axis2 (joint[0],0,1,0);
  */

  // front and back wheel hinges
  for (i=0; i<4; i++) {
    joint[i] = dJointCreateHinge2 (world,0);
	joint_exists[i] = 1;
    dJointAttach (joint[i],body[0],body[i+1]);
    const dReal *a = dBodyGetPosition (body[i+1]);
    dJointSetHinge2Anchor (joint[i],a[0],a[1],a[2]);
    dJointSetHinge2Axis1 (joint[i],0,0,1);
    dJointSetHinge2Axis2 (joint[i],0,1,0);

    // the wheels can break
    dJointSetBreakable (joint[i], 1);
    // the wheels wil break at a specific force
    dJointSetBreakMode (joint[i], 
                        dJOINT_BREAK_AT_B1_FORCE |
                        dJOINT_BREAK_AT_B2_FORCE |
                        dJOINT_DELETE_ON_BREAK);
    // specify the force for the first body connected to the joint ...
    dJointSetBreakForce (joint[i], 0, 2.5, 2.5, 2.5);
    // and for the second body
    dJointSetBreakForce (joint[i], 1, 2.5, 2.5, 2.5);
	// set the callback function
	dJointSetBreakCallback (joint[i], &jointBreakCallback);
  }
  
  // joints for the chain
  for (i=0; i<chain_num-1; i++) {
    chain_joint[i] = dJointCreateFixed (world,0);
    dJointAttach (chain_joint[i],chain_body[i+1],chain_body[i]);
	dJointSetFixed (chain_joint[i]);
	// the chain can break
    dJointSetBreakable (chain_joint[i], 1);
    // the chain wil break at a specific force
    dJointSetBreakMode (chain_joint[i], 
      dJOINT_BREAK_AT_B1_FORCE |
      dJOINT_BREAK_AT_B2_FORCE |
      dJOINT_DELETE_ON_BREAK);
    // specify the force for the first body connected to the joint ...
    dJointSetBreakForce (chain_joint[i], 0, 0.5, 0.5, 0.5);
    // and for the second body
    dJointSetBreakForce (chain_joint[i], 1, 0.5, 0.5, 0.5);
	// set the callback function
	dJointSetBreakCallback (chain_joint[i], &jointBreakCallback);
  }
  
  // set joint suspension
  for (i=0; i<4; i++) {
    dJointSetHinge2Param (joint[i],dParamSuspensionERP,0.4);
    dJointSetHinge2Param (joint[i],dParamSuspensionCFM,0.1);
  }

  // lock back wheels along the steering axis
  for (i=1; i<4; i++) {
    // set stops to make sure wheels always stay in alignment
    dJointSetHinge2Param (joint[i],dParamLoStop,0);
    dJointSetHinge2Param (joint[i],dParamHiStop,0);
    // the following alternative method is no good as the wheels may get out
    // of alignment:
    //   dJointSetHinge2Param (joint[i],dParamVel,0);
    //   dJointSetHinge2Param (joint[i],dParamFMax,dInfinity);
  }
  
  // create car space and add it to the top level space
  car_space = dSimpleSpaceCreate (space);
  dSpaceSetCleanup (car_space,0);
  dSpaceAdd (car_space,box[0]);
  dSpaceAdd (car_space,sphere[0]);
  dSpaceAdd (car_space,sphere[1]);
  dSpaceAdd (car_space,sphere[2]);

  // environment
  ground_box = dCreateBox (space,2,1.5,1);
  dMatrix3 R;
  dRFromAxisAndAngle (R,0,1,0,-0.15);
  dGeomSetPosition (ground_box,2,0,-0.34);
  dGeomSetRotation (ground_box,R);
  
  // obstacles
  for (i=0; i<obstacle_num; i++) {
    dReal height = 0.1+(dReal(rand()%10)/10.0);
    obstacle[i] = dCreateBox (space,0.2,0.2,height);
	dGeomSetPosition (
	  obstacle[i],
	  (rand()%20)-10,
	  (rand()%20)-10,
	  height/2.0);
  }

  // run simulation
  dsSimulationLoop (argc,argv,352,288,&fn);

  dJointGroupDestroy (contactgroup);
  dSpaceDestroy (space);
  dWorldDestroy (world);
  dGeomDestroy (box[0]);
  for (i=0; i<4; i++)
    dGeomDestroy (sphere[i]);
	
  dCloseODE ();
  
  return 0;
}
