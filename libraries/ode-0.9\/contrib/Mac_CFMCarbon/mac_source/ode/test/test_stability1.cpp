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
#define dsDrawSphere dsDrawSphereD
#define dsDrawCylinder dsDrawCylinderD
#define dsDrawCappedCylinder dsDrawCappedCylinderD
#endif


// some constants

#define DENSITY (5.0)		// density of all objects

// dynamics and collision objects

struct MyObject {
  dBodyID body;		// the body
  dGeomID geom;		// geometry representing this body
};

static dWorldID world;
static dSpaceID space;
static MyObject fallingObject;
static dGeomID  box1, box2;
static dJointGroupID contactgroup;


// this is called by dSpaceCollide when two objects in space are
// potentially colliding.

static void nearCallback (void *data, dGeomID o1, dGeomID o2)
{
  int i;
  // if (o1->body && o2->body) return;

  // exit without doing anything if the two bodies are connected by a joint
  dBodyID b1 = dGeomGetBody(o1);
  dBodyID b2 = dGeomGetBody(o2);
  if (b1 && b2 && dAreConnected (b1,b2)) return;

  dContact contact[4];			// up to 3 contacts per box
  for (i=0; i<4; i++) {
    contact[i].surface.mode = dContactBounce; //dContactMu2;
    contact[i].surface.mu = dInfinity;
    contact[i].surface.mu2 = 0;
    contact[i].surface.bounce = 0.5;
    contact[i].surface.bounce_vel = 0.1;
  }
  if (int numc = dCollide (o1,o2,4,&contact[0].geom,sizeof(dContact))) {
    // dMatrix3 RI;
    // dRSetIdentity (RI);
    // const dReal ss[3] = {0.02,0.02,0.02};
    for (i=0; i<numc; i++) {
      dJointID c = dJointCreateContact (world,contactgroup,contact+i);
      dJointAttach (c,b1,b2);
      // dsDrawBox (contact[i].geom.pos,RI,ss);
    }
  }
}


// start simulation - set viewpoint

static void start()
{
  static float xyz[3] = {-4.0f, 0.0f, 3.0f};
  static float hpr[3] = {0.0f,-15.0f,0.0f};
  dsSetViewpoint (xyz,hpr);
  printf ("To drop another object, press:\n");
  printf ("   b for box.\n");
  printf ("   s for sphere.\n");
  printf ("   c for cylinder.\n");
  printf ("To select an object, press space.\n");
}


char locase (char c)
{
  if (c >= 'A' && c <= 'Z') return c - ('a'-'A');
  else return c;
}


// called when a key pressed

static void command (int cmd)
{
    int i,k;
    dReal sides[3];
    dMass m;

    cmd = locase (cmd);
    if (cmd == 'b' || cmd == 's' || cmd == 'c') {
        // Destroy the currently falling object and replace it by an instance of the requested type
        if (fallingObject.body) {
            dBodyDestroy (fallingObject.body);
            dGeomDestroy (fallingObject.geom);
            memset (&fallingObject, 0, sizeof(fallingObject));
        }

        fallingObject.body = dBodyCreate (world);
        for (k=0; k<3; k++) sides[k] = dRandReal()*0.5+0.1;

        // Start out centered above the V-gap
        dBodySetPosition (fallingObject.body, 0,0,5);

#if 0
        dMatrix3 R;
        dRFromAxisAndAngle (R,dRandReal()*2.0-1.0,dRandReal()*2.0-1.0,
                            dRandReal()*2.0-1.0,dRandReal()*10.0-5.0);
        dBodySetRotation (fallingObject.body,R);
        dBodySetData (fallingObject.body,(void*) i);
#endif
        
        if (cmd == 'b') {
            dMassSetBox (&m,DENSITY,sides[0],sides[1],sides[2]);
            fallingObject.geom = dCreateBox (space,sides[0],sides[1],sides[2]);
        }
        else if (cmd == 'c') {
            sides[0] *= 0.5;
            dMassSetCappedCylinder (&m,DENSITY,3,sides[0],sides[1]);
            fallingObject.geom = dCreateCCylinder (space,sides[0],sides[1]);
        }
        else if (cmd == 's') {
            sides[0] *= 0.5;
            dMassSetSphere (&m,DENSITY,sides[0]);
            fallingObject.geom = dCreateSphere (space,sides[0]);
        }

        dGeomSetBody (fallingObject.geom,fallingObject.body);

        dBodySetMass (fallingObject.body,&m);
    }
}


// draw a geom

void drawGeom (dGeomID g, const dReal *pos, const dReal *R)
{
  if (!g) return;
  if (!pos) pos = dGeomGetPosition (g);
  if (!R) R = dGeomGetRotation (g);

  int type = dGeomGetClass (g);
  if (type == dBoxClass) {
    dVector3 sides;
    dGeomBoxGetLengths (g,sides);
    dsDrawBox (pos,R,sides);
  }
  else if (type == dSphereClass) {
    dsDrawSphere (pos,R,dGeomSphereGetRadius (g));
  }
  else if (type == dCCylinderClass) {
    dReal radius,length;
    dGeomCCylinderGetParams (g,&radius,&length);
    dsDrawCappedCylinder (pos,R,length,radius);
  }
  /*
  else if (type == dGeomTransformClass) {
    dGeomID g2 = dGeomTransformGetGeom (g);
    const dReal *pos2 = dGeomGetPosition (g2);
    const dReal *R2 = dGeomGetRotation (g2);
    dVector3 actual_pos;
    dMatrix3 actual_R;
    dMULTIPLY0_331 (actual_pos,R,pos2);
    actual_pos[0] += pos[0];
    actual_pos[1] += pos[1];
    actual_pos[2] += pos[2];
    dMULTIPLY0_333 (actual_R,R,R2);
    drawGeom (g2,actual_pos,actual_R);
  }
   */
}


// simulation loop

static void simLoop (int pause)
{
  dsSetColor (0,0,2);
  dSpaceCollide (space,0,&nearCallback);
  if (!pause) dWorldStep (world,0.0005);

  // remove all contact joints
  dJointGroupEmpty (contactgroup);

  dsSetColor (1,1,0);
  dsSetTexture (DS_WOOD);

  // draw the falling object
  dsSetColor (1,0,0);
  drawGeom (fallingObject.geom,0,0);

  // draw the constraining boxes
  dsSetColor(0.8, 1, 0.8);
  drawGeom (box1,0,0);
  drawGeom (box2,0,0);
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
  if(argc==2)
    {
        fn.path_to_textures = argv[1];
    }

  // create world

  world = dWorldCreate();
  space = dHashSpaceCreate();
  contactgroup = dJointGroupCreate (0);
  dWorldSetGravity (world,0,0,-0.5);
  dWorldSetCFM (world,1e-5);
  dCreatePlane (space,0,0,1,0);
  memset (&fallingObject,0,sizeof(fallingObject));

  // Create two flat boxes, just slightly off vertical and a bit apart for stuff to fall in between.
  // Don't create bodies for these boxes -- they'll be immovable instead.
  {
      dReal sides[3];
      dMatrix3 R;
      
      sides[0] = 4;
      sides[1] = 0.2;
      sides[2] = 3;

      box1 = dCreateBox (space,sides[0],sides[1],sides[2]);
      dGeomSetPosition (box1, 0, sides[1], sides[2]/2);
      dRFromAxisAndAngle (R, 1, 0, 0, -0.1);
      dGeomSetRotation (box1, R);
      
      box2 = dCreateBox (space,sides[0],sides[1],sides[2]);
      dGeomSetPosition (box2, 0, -sides[1], sides[2]/2);
      dRFromAxisAndAngle (R, 1, 0, 0, 0.1);
      dGeomSetRotation (box2, R);
  }
  
  // Pretend to drop a box to start
  command('b');
  
  // run simulation
  dsSimulationLoop (argc,argv,640,480,&fn);

  dJointGroupDestroy (contactgroup);
  dSpaceDestroy (space);
  dWorldDestroy (world);

  return 0;
}
