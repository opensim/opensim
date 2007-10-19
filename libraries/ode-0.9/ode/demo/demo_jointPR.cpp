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

This file try to demonstrate how the PR joint is working.

The axisP is draw in red and the axisR is in green

*/


#include <ode/ode.h>
#include <drawstuff/drawstuff.h>
#include <iostream>
#include <math.h>


#define DRAWSTUFF_TEXTURE_PATH "../../drawstuff/textures"


#ifdef _MSC_VER
#pragma warning(disable:4244 4305)  // for VC++, no precision loss complaints
#endif
// select correct drawing functions
#ifdef dDOUBLE
#define dsDrawBox dsDrawBoxD
#endif

// physics parameters
#define BOX1_LENGTH 2    // Size along the X axis 
#define BOX1_WIDTH 1     // Size along the Y axis
#define BOX1_HEIGHT 0.4  // Size along the Z axis (up) since gravity is (0,0,-10)
#define BOX2_LENGTH 0.2
#define BOX2_WIDTH 0.1
#define BOX2_HEIGHT 0.4
#define Mass1 10
#define Mass2 0.1


#define PRISMATIC_ONLY 1
#define ROTOIDE_ONLY   2
int flag = 0;


//camera view
static float xyz[3] = {2.0f,-3.5f,2.0000f};
static float hpr[3] = {90.000f,-25.5000f,0.0000f};
//world,space,body & geom
static dWorldID world;
static dSpaceID space;
static dSpaceID box1_space;
static dSpaceID box2_space;
static dBodyID box1_body[1];
static dBodyID box2_body[1];
static dJointID joint[1];
static dJointGroupID contactgroup;
static dGeomID ground;
static dGeomID box1[1];
static dGeomID box2[1];


//collision detection
static void nearCallback (void *data, dGeomID o1, dGeomID o2)
{
	  int i,n;

		  dBodyID b1 = dGeomGetBody(o1);
		  dBodyID b2 = dGeomGetBody(o2);
		  if (b1 && b2 && dAreConnectedExcluding (b1,b2,dJointTypeContact)) return;
		  const int N = 10;
		  dContact contact[N];
		  n = dCollide (o1,o2,N,&contact[0].geom,sizeof(dContact));
		  if (n > 0)
		  {
			for (i=0; i<n; i++)
			{
				 contact[i].surface.mode = dContactSlip1 | dContactSlip2 |
				 dContactSoftERP | dContactSoftCFM | dContactApprox1;
				 contact[i].surface.mu = 0.1;
				 contact[i].surface.slip1 = 0.02;
				 contact[i].surface.slip2 = 0.02;
				 contact[i].surface.soft_erp = 0.1;
				 contact[i].surface.soft_cfm = 0.0001;
				 dJointID c = dJointCreateContact (world,contactgroup,&contact[i]);
				 dJointAttach (c,dGeomGetBody(contact[i].geom.g1),dGeomGetBody(contact[i].geom.g2));
			}
		  }
}


// start simulation - set viewpoint
static void start()
{
		dsSetViewpoint (xyz,hpr);
		printf ("Press 'd' to add force along positive x direction.\nPress 'a' to add force along negative x direction.\n");
		printf ("Press 'w' to add force along positive y direction.\nPress 's' to add force along negative y direction.\n");
		printf ("Press 'e' to add torque around positive z direction.\nPress 'q' to add torque around negative z direction.\n");
		printf ("Press 'o' to add force around positive x direction \n");
}

// function to update camera position at each step.
void update()
{
// 		const dReal *a =(dBodyGetPosition (box1_body[0]));
// 		float dx=a[0];
// 		float dy=a[1];
// 		float dz=a[2];
// 		xyz[0]=dx;
// 		xyz[1]=dy-5;
// 		xyz[2]=dz+2;
// 		hpr[1]=-22.5000f;
// 		dsSetViewpoint (xyz,hpr);
}


// called when a key pressed
static void command (int cmd)
{
    switch(cmd)
	{
		case 'w': case 'W':
			dBodyAddForce(box2_body[0],0,500,0);
			std::cout<<(dBodyGetPosition(box2_body[0])[1]-dBodyGetPosition(box1_body[0])[1])<<'\n';
			break;
		case 's': case 'S':
			dBodyAddForce(box2_body[0],0,-500,0);
			std::cout<<(dBodyGetPosition(box2_body[0])[1]-dBodyGetPosition(box1_body[0])[1])<<'\n';
			break;
		case 'd': case 'D':
			dBodyAddForce(box2_body[0],500,0,0);
			std::cout<<(dBodyGetPosition(box2_body[0])[0]-dBodyGetPosition(box1_body[0])[0])<<'\n';
			break;
		case 'a': case 'A':
			dBodyAddForce(box2_body[0],-500,0,0);
			std::cout<<(dBodyGetPosition(box2_body[0])[0]-dBodyGetPosition(box1_body[0])[0])<<'\n';
			break;
		case 'e': case 'E':
			dBodyAddRelTorque(box2_body[0],0,0,200);
			break;
		case 'q': case 'Q':
			dBodyAddRelTorque(box2_body[0],0,0,-200);
			break;
		case 'o': case 'O':
			dBodyAddForce(box1_body[0],10000,0,0);
			break;
	}
}


// simulation loop
static void simLoop (int pause)
{
	if (!pause)
	{
		//draw 2 boxes
		dVector3 ss;
		dsSetTexture (DS_WOOD);

		const dReal *posBox2 = dGeomGetPosition(box2[0]);
		const dReal *rotBox2 = dGeomGetRotation(box2[0]);
		dsSetColor (1,1,0);
		dGeomBoxGetLengths (box2[0],ss);
		dsDrawBox (posBox2, rotBox2, ss);

		const dReal *posBox1 = dGeomGetPosition(box1[0]);
		const dReal *rotBox1 = dGeomGetRotation(box1[0]);
		dsSetColor (1,1,2);
		dGeomBoxGetLengths (box1[0], ss);
		dsDrawBox (posBox1, rotBox1, ss);

		dVector3 anchorPos;
		dJointGetPRAnchor (joint[0], anchorPos);

		// Draw the axisP 
		if (ROTOIDE_ONLY != flag )
		{
		  dsSetColor (1,0,0);
		  dVector3 sizeP = {0, 0.1, 0.1};
		  for (int i=0; i<3; ++i)
		    sizeP[0] += (anchorPos[i] - posBox1[i])*(anchorPos[i] - posBox1[i]);
		  sizeP[0] = sqrt(sizeP[0]);
		  dVector3 posAxisP;
		  for (int i=0; i<3; ++i)
		    posAxisP[i] = posBox1[i] + (anchorPos[i] - posBox1[i])/2.0;
		  dsDrawBox (posAxisP, rotBox1, sizeP);
		}
		

		// Draw the axisR 
		if (PRISMATIC_ONLY != flag )
		{
		  dsSetColor (0,1,0);
		  dVector3 sizeR = {0, 0.1, 0.1};
		  for (int i=0; i<3; ++i)
		    sizeR[0] += (anchorPos[i] - posBox2[i])*(anchorPos[i] - posBox2[i]);
		  sizeR[0] = sqrt(sizeR[0]);
		  dVector3 posAxisR;
		  for (int i=0; i<3; ++i)
		    posAxisR[i] = posBox2[i] + (anchorPos[i] - posBox2[i])/2.0;
		  dsDrawBox (posAxisR, rotBox2, sizeR);
		}

		dSpaceCollide (space,0,&nearCallback);
		dWorldQuickStep (world,0.0001);
		update();
		dJointGroupEmpty (contactgroup);
	  }
}


void Help(char **argv)
{
  printf("%s ", argv[0]);
  printf(" -h | --help : print this help\n");
  printf(" -b | --both : Display how the complete joint works\n");
  printf("               Default behavior\n");
  printf(" -p | --prismatic-only : Display how the prismatic part works\n");
  printf("                         The anchor pts is set at the center of body 2\n");
  printf(" -r | --rotoide-only   : Display how the rotoide part works\n");
  printf("                         The anchor pts is set at the center of body 1\n");
  printf(" -t | --texture-path path  : Path to the texture.\n");
  printf("                             Default = %s\n", DRAWSTUFF_TEXTURE_PATH);
  printf("--------------------------------------------------\n");
  printf("Hit any key to continue:");
  getchar();

  exit(0);
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
  fn.path_to_textures = DRAWSTUFF_TEXTURE_PATH;
  
  if (argc >= 2 )
  {
    for (int i=1; i < argc; ++i)
    {
      if(  0 == strcmp("-h", argv[i]) || 0 == strcmp("--help", argv[i]) )
	Help(argv);
      
      if(!flag && (0 == strcmp("-p", argv[i]) ||0 == strcmp("--prismatic-only", argv[i])) )
	flag = PRISMATIC_ONLY;
      
      if(!flag && (0 == strcmp("-r", argv[i]) || 0 == strcmp("--rotoide-only", argv[i])) )
	flag = ROTOIDE_ONLY;
      
      if(0 == strcmp("-t", argv[i]) || 0 == strcmp("--texture-path", argv[i]))
      {
	int j = i+1;
	if ( j+1 > argc      ||  // Check if we have enough arguments
	     argv[j] == '\0' ||  // We should have a path here
	     argv[j][0] == '-' ) // We should have a path not a command line
	  Help(argv);
	else
	  fn.path_to_textures = argv[++i]; // Increase i since we use this argument
      }
    }
  }
  
  // create world
  world = dWorldCreate();
  space = dHashSpaceCreate (0);
  contactgroup = dJointGroupCreate (0);
  dWorldSetGravity (world,0,0,-10);
  ground = dCreatePlane (space,0,0,1,0);
  
  //create two boxes
  dMass m;
  box1_body[0] = dBodyCreate (world);
  dMassSetBox (&m,1,BOX1_LENGTH,BOX1_WIDTH,BOX1_HEIGHT);
  dMassAdjust (&m,Mass1);
  dBodySetMass (box1_body[0],&m);
  box1[0] = dCreateBox (0,BOX1_LENGTH,BOX1_WIDTH,BOX1_HEIGHT);
  dGeomSetBody (box1[0],box1_body[0]);
  
  box2_body[0] = dBodyCreate (world);
  dMassSetBox (&m,10,BOX2_LENGTH,BOX2_WIDTH,BOX2_HEIGHT);
  dMassAdjust (&m,Mass2);
  dBodySetMass (box2_body[0],&m);
  box2[0] = dCreateBox (0,BOX2_LENGTH,BOX2_WIDTH,BOX2_HEIGHT);
  dGeomSetBody (box2[0],box2_body[0]);
  
  //set the initial positions of body1 and body2
  dMatrix3 R;
  dRSetIdentity(R);
  dBodySetPosition (box1_body[0],0,0,BOX1_HEIGHT/2.0);
  dBodySetRotation (box1_body[0], R);
  
  dBodySetPosition (box2_body[0],
		    2.1, 
		    0.0,
		    BOX2_HEIGHT/2.0);
  dBodySetRotation (box2_body[0], R);
  
  
  //set PR joint
  joint[0] = dJointCreatePR(world,0);
  dJointAttach (joint[0],box1_body[0],box2_body[0]);  
  switch (flag)
  {
    case PRISMATIC_ONLY:
      dJointSetPRAnchor (joint[0], 
			 2.1, 
			 0.0,
			 BOX2_HEIGHT/2.0);
      dJointSetPRParam (joint[0],dParamLoStop, -0.5);
      dJointSetPRParam (joint[0],dParamHiStop, 1.5);
      break;
      
    case ROTOIDE_ONLY:
      dJointSetPRAnchor (joint[0], 
			 0.0, 
			 0.0,
			 BOX2_HEIGHT/2.0);
      dJointSetPRParam (joint[0],dParamLoStop, 0.0);
      dJointSetPRParam (joint[0],dParamHiStop, 0.0);
      break;
      
    default:
      dJointSetPRAnchor (joint[0], 
			 1.1, 
			 0.0,
			 BOX2_HEIGHT/2.0);
      dJointSetPRParam (joint[0],dParamLoStop, -0.5);
      dJointSetPRParam (joint[0],dParamHiStop, 1.5);
      break;
  }
  
  dJointSetPRAxis1(joint[0],1,0,0);
  dJointSetPRAxis2(joint[0],0,0,1);
// We position  the 2 body
// The position of the rotoide joint is on the second body so it can rotate on itself
// and move along the X axis.
// With this anchor 
// - A force in X will move only the body 2 inside the low and hi limit
//   of the prismatic
// - A force in Y will make the 2 bodies to rotate around on the plane
  
  box1_space = dSimpleSpaceCreate (space);
  dSpaceSetCleanup (box1_space,0);
  dSpaceAdd(box1_space,box1[0]);
  
  // run simulation
  dsSimulationLoop (argc,argv,400,300,&fn);
  dJointGroupDestroy (contactgroup);
  dSpaceDestroy (space);
  dWorldDestroy (world);
  return 0;
}

