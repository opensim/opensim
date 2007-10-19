#include <stdio.h>
#include <glut.h>
#include "ode.h"

#define NUMBODIES 80

#define USE_SPHERE 0
#define USE_HELIX 1
#define USE_TORQUE 1
#define USE_WEIRD_MATRIX_OPS 0

#define CONTACTS 1

dWorldID aWorld;
dSpaceID aSpace;
float cycle = 0, fade;
dJointGroupID aContactGroup;
dBodyID bodies[NUMBODIES];
dGeomID geoms[NUMBODIES];
GLfloat colors[NUMBODIES][4];
unsigned int contactsThisFrame;

void kglTransformByODEGeom(dGeomID geom) {
  const dReal *p = dGeomGetPosition(geom);
  const dReal *R = dGeomGetRotation(geom);
  GLdouble glm[16];

  glm[0]  = R[0]; glm[1]  = R[4]; glm[2]  = R[8]; glm[3]  = 0;
  glm[4]  = R[1]; glm[5]  = R[5]; glm[6]  = R[9]; glm[7]  = 0;
  glm[8]  = R[2]; glm[9]  = R[6]; glm[10] = R[10];glm[11] = 0;
  glm[12] = p[0]; glm[13] = p[1]; glm[14] = p[2]; glm[15] = 1;
   
  glMultMatrixd(glm);
}

static void odeNearCallback(void *data, dGeomID g1, dGeomID g2) {
  dBodyID b1 = dGeomGetBody(g1), 
          b2 = dGeomGetBody(g2);
  dContact contact[CONTACTS];
  int contactsUsed, i;

  if (b1 && b2 && dAreConnected(b1, b2)) return;

  contactsUsed = dCollide(g1, g2, CONTACTS, &contact[0].geom,
    sizeof(dContact));
  if (contactsUsed > CONTACTS) contactsUsed = CONTACTS;

  for (i = 0; i < contactsUsed; i++) {
    contact[i].surface.mode = 0;
    contact[i].surface.mu = 20.0;
    
    dJointAttach(dJointCreateContact(aWorld, aContactGroup,
      &(contact[i])), b1, b2);
    contactsThisFrame++;
  }
}

void myGlutResize(int w, int h) {
  glViewport(0, 0, w, h);
  glMatrixMode(GL_PROJECTION);
  glLoadIdentity();
  gluPerspective(45.0, (GLfloat)w / h, 1.0, 120.0);
  glMatrixMode(GL_MODELVIEW);
  glLoadIdentity();
  glTranslatef(0, -6, -20);
}

void myGlutIdle(void) {
  const float step = 1.0/120;
  int i;

  cycle = fmod(cycle + step / 4, 1);
  fade = fabs(cycle * 2 - 1);

  contactsThisFrame = 0;
  dSpaceCollide(aSpace, NULL, &odeNearCallback);
  //printf("%u\n", contactsThisFrame);
  dWorldStep(aWorld, step);
  dJointGroupEmpty(aContactGroup);

  for (i = 0; i < NUMBODIES; i++) {
    const dReal *cvel = dBodyGetLinearVel(bodies[i]);
    dBodyAddForce(bodies[i],
      -cvel[0] * 0.5,
      -cvel[1] * 0.5,
      -cvel[2] * 0.5
    );
  }

  glutPostRedisplay();
}

void myGlutDisplay(void) {
  int i;

  glClearColor(fade * 0.15, 0, 0, 1);
  glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
  
  if (USE_WEIRD_MATRIX_OPS) glPushMatrix();
  for (i = 0; i < NUMBODIES; i++) {
    if (!USE_WEIRD_MATRIX_OPS) glPushMatrix();
    kglTransformByODEGeom(geoms[i]);
    glMaterialfv(GL_FRONT, GL_AMBIENT_AND_DIFFUSE, colors[i]);
    glColor3f(fade * 1.5, 0, 0);
#if USE_SPHERE
    glRotatef(90, 1, 0, 0);
    glutSolidSphere(0.5, 9, 6);
    glDisable(GL_LIGHTING);
    glutWireSphere(0.5, 9, 6);
#else
    glutSolidCube(1);
    glDisable(GL_LIGHTING);
    glutWireCube(1);
#endif
    glEnable(GL_LIGHTING);
    if (!USE_WEIRD_MATRIX_OPS) glPopMatrix();
  }
  if (USE_WEIRD_MATRIX_OPS) glPopMatrix();
  
  glutSwapBuffers();
}

int main(int argc, char **argv) {
	printf("Initializing GLUT\n");

  glutInit(&argc, argv);                
  glutInitDisplayMode(GLUT_DOUBLE | GLUT_RGB | GLUT_DEPTH);
  glutInitWindowSize(400, 300);
  glutInitWindowPosition(100, 100);
  glutCreateWindow("ODE Crash Test");
  
  glutDisplayFunc(myGlutDisplay);
  glutReshapeFunc(myGlutResize);
  glutIdleFunc(myGlutIdle);

  glPolygonOffset(1, 1);
  glDepthFunc(GL_LEQUAL);
  glEnable(GL_POLYGON_OFFSET_FILL);
  glEnable(GL_DEPTH_TEST);
  glEnable(GL_CULL_FACE);
  glEnable(GL_LIGHTING);
  glEnable(GL_LIGHT0);
  myGlutResize(400, 300);

  printf("Creating ODE world\n");
  aWorld = dWorldCreate();
  aSpace = dHashSpaceCreate();
  aContactGroup = dJointGroupCreate(0);
  dCreatePlane(aSpace, 0, 1, 0, 0);
  dWorldSetGravity(aWorld, 0, -9.81, 0);
  dWorldSetERP(aWorld, 0.4);
  dWorldSetCFM(aWorld, 1e-10);
  
  printf("Creating objects\n");
  {
    int i;
    dMass mass;
    
    dMassSetBox(&mass, 1.0, 1, 1, 1);

    for (i = 0; i < NUMBODIES; i++) {
      float fraction = (float)i / NUMBODIES;
    
      bodies[i] = dBodyCreate(aWorld);
      dBodySetMass(bodies[i], &mass);
#if USE_SPHERE
      geoms[i] = dCreateSphere(aSpace, 0.5);
#else
      geoms[i] = dCreateBox(aSpace, 1, 1, 1);
#endif
      dGeomSetBody(geoms[i], bodies[i]);
    
      if (USE_HELIX) {
        float r     = (i % 3 - 1) * (1.5+4*(1 - fraction)),
              theta = (float)i / 4;
        dBodySetPosition(bodies[i],
          sin(theta) * r, 
          (float)i + 1,
          cos(theta) * r
        );
      } else {
        dBodySetPosition(bodies[i], 0, (float)i * 2 + 1, 0);
      }
      if (USE_TORQUE) dBodyAddTorque(bodies[i], fraction*10, fraction*20, fraction*30);
      
      colors[i][0] = fraction;
      colors[i][1] = 1 - fraction;
      colors[i][2] = 1 - fabs(fraction * 2 - 1);
      colors[i][3] = 1;
    }
  }

  printf("Starting simulation\n");
  glutMainLoop();
	
  return 0;
}