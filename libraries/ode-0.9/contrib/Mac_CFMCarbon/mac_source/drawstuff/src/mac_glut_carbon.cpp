/*************************************************************************
 *                                                                       *
 * DrawStuff Library, Copyright (C) 2001 Russell L. Smith.               *
 *   Email: russ@q12.org   Web: www.q12.org                              *
 *                                                                       *
 * This library is free software; you can redistribute it and/or         *
 * modify it under the terms of the GNU Lesser General Public            *
 * License as published by the Free Software Foundation; either          *
 * version 2.1 of the License, or (at your option) any later version.    *
 *                                                                       *
 * This library is distributed in the hope that it will be useful,       *
 * but WITHOUT ANY WARRANTY; without even the implied warranty of        *
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU      *
 * Lesser General Public License for more details.                       *
 *                                                                       *
 * You should have received a copy of the GNU Lesser General Public      *
 * License along with this library (see the file LICENSE.TXT); if not,   *
 * write to the Free Software Foundation, Inc., 59 Temple Place,         *
 * Suite 330, Boston, MA 02111-1307 USA.                                 *
 *                                                                       *
 *************************************************************************/

// main window and event handling for Mac CFM Carbon

#include <ode/config.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>
#include <stdio.h>
#include <glut.h>
#include <SIOUX.h>

#include <MacTypes.h>
#include <Timer.h>

#include <drawstuff/drawstuff.h>
#include <drawstuff/version.h>
#include "internal.h"


//***************************************************************************
// error handling for unix (works just fine with SIOUX)

static void printMessage (char *msg1, char *msg2, va_list ap)
{
  fflush (stderr);
  fflush (stdout);
  fprintf (stderr,"\n%s: ",msg1);
  vfprintf (stderr,msg2,ap);
  fprintf (stderr,"\n");
  fflush (stderr);
}

extern "C" void dsError (char *msg, ...)
{
  va_list ap;
  va_start (ap,msg);
  printMessage ("Error",msg,ap);
  exit (1);
}


extern "C" void dsDebug (char *msg, ...)
{
  va_list ap;
  va_start (ap,msg);
  printMessage ("INTERNAL ERROR",msg,ap);
  // *((char *)0) = 0;	 ... commit SEGVicide ?
  abort();
}

extern "C" void dsPrint (char *msg, ...)
{
  va_list ap;
  va_start (ap,msg);
  vprintf (msg,ap);
}

//***************************************************************************
// openGL window

// window and openGL
static int width=0,height=0;		// window size
static int last_key_pressed=0;		// last key pressed in the window
static int pause=0;					// 1 if in `pause' mode
static int singlestep=0;			// 1 if single step key pressed
static int writeframes=0;			// 1 if frame files to be written
static dsFunctions *gfn;
static int frame = 1;

float getTime (void)
{
	UnsignedWide ms;
	
	Microseconds(&ms);
	return ms.lo / 1000000.0;
}


static void captureFrame (int num)
{
// TODO
}

static void reshape(int w, int h)
{
	width = w;
	height = h;
}

static void draw(void)
{
	dsDrawFrame (width,height,gfn,pause && !singlestep);
	singlestep = 0;
	glutSwapBuffers();
	
	if (pause==0 && writeframes) {
      captureFrame (frame);
      frame++;
    }
}

static void idle(void)
{
	static float lasttime=0;
	float t;
	
	// Try to maintain a reasonable rate (good enough for testing anyway)
	t = getTime();
	if (lasttime < t) {
		lasttime = t+0.005;
		draw();
	}
}

static void key(unsigned char key, int x, int y)
{
	if (!glutGetModifiers()) {
		
		if (key >= ' ' && key <= 126 && gfn->command) gfn->command (key);
	
	// GLUT_ACTIVE_CTRL doesn't seem to be working, so we use Alt
	} else if (glutGetModifiers()&GLUT_ACTIVE_ALT) {
		
		switch (key) {
		case 't': case 'T':
			dsSetTextures (dsGetTextures() ^ 1);
			break;
		case 's': case 'S':
			dsSetShadows (dsGetShadows() ^ 1);
			break;
		case 'p': case 'P':
			pause ^= 1;
			singlestep = 0;
			break;
		case 'o': case 'O':
			if (pause) singlestep = 1;
			break;
		case 'v': case 'V': {
			float xyz[3],hpr[3];
			dsGetViewpoint (xyz,hpr);
			printf ("Viewpoint = (%.4f,%.4f,%.4f,%.4f,%.4f,%.4f)\n",
					xyz[0],xyz[1],xyz[2],hpr[0],hpr[1],hpr[2]);
			break;
	      }
	    // No case 'X' - Quit works through the Mac system menu, or cmd-q
		case 'w': case 'W':
			writeframes ^= 1;
			if (writeframes) printf ("Write frames not done yet!\n");// TODO
				break;
		}
	}
		
    last_key_pressed = key;
}

static int mx=0,my=0; 	// mouse position
static int mode = 0;	// mouse button bits
  
static void MouseDown(int button, int state, int x, int y)
{
	if(button == GLUT_LEFT_BUTTON)
	{
		if(state == GLUT_DOWN)
			mode |= 1;
		else if(state == GLUT_UP)
			mode &= (~1);
	}
	else if (button == GLUT_MIDDLE_BUTTON)
	{
		if(state == GLUT_DOWN)
			mode |= 3;
		else if(state == GLUT_UP)
			mode &= (~3);
	}
	else if (button == GLUT_RIGHT_BUTTON)
	{
		if(state == GLUT_DOWN)
			mode |= 2;
		else if(state == GLUT_UP)
			mode &= (~2);
	}
	
	mx = x;
	my = y;
}

static void MouseMove(int x, int y)
{
	dsMotion (mode, x - mx, y - my);
	mx = x;
	my = y;	
}

static void createMainWindow (int _width, int _height)
{
	// So GLUT doesn't complain
	int argc = 0;
	char **argv = NULL;
	
	// initialize variables
	width = _width;
	height = _height;
	last_key_pressed = 0;

	if (width < 1 || height < 1) dsDebug (0,"bad window width or height");
	
	glutInit(&argc, argv);
	glutInitDisplayMode(GLUT_DOUBLE | GLUT_RGB | GLUT_DEPTH);
	glutInitWindowSize(_width, _height);
	glutInitWindowPosition(100, 100);
	glutCreateWindow("ODE Simulation");
	
	glutKeyboardFunc(key);
	glutMotionFunc(MouseMove);
	glutMouseFunc(MouseDown);
	glutReshapeFunc(reshape);
	glutDisplayFunc(idle);
	glutIdleFunc(idle);
}

void dsPlatformSimLoop (int window_width, int window_height, dsFunctions *fn,
			int initial_pause)
{	
	SIOUXSettings.initializeTB     = false;
	SIOUXSettings.standalone       = false;
	SIOUXSettings.setupmenus       = false;
	SIOUXSettings.autocloseonquit  = true;
	SIOUXSettings.asktosaveonclose = false;
		
	gfn = fn;
	pause = initial_pause;

	printf (
			"\n"
			"Simulation test environment v%d.%02d\n"
			"   Option-P : pause / unpause (or say `-pause' on command line).\n"
			"   Option-O : single step when paused.\n"
			"   Option-T : toggle textures (or say `-notex' on command line).\n"
			"   Option-S : toggle shadows (or say `-noshadow' on command line).\n"
			"   Option-V : print current viewpoint coordinates (x,y,z,h,p,r).\n"
			"   Option-W : write frames to ppm files: frame/frameNNN.ppm\n"
			"\n"
			"Change the camera position by clicking + dragging in the window.\n"
			"   Left button - pan and tilt.\n"
			"   Right button - forward and sideways.\n"
			"   Left + Right button (or middle button) - sideways and up.\n"
			"\n",DS_VERSION >> 8,DS_VERSION & 0xff);
	
	createMainWindow (window_width, window_height);
	dsStartGraphics (window_width,window_height,fn);
	
	if (fn->start) fn->start();
	
	glutMainLoop();
	
	if (fn->stop) fn->stop();
	dsStopGraphics();
}

extern "C" void dsStop(){ }// GLUT/MSL hooks into the system to exit
