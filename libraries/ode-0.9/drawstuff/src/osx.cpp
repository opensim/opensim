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

// Platform-specific code for Mac OS X using Carbon+AGL
//
// Created using x11.cpp and the window-initialization -routines from GLFW
// as reference.
// Not thoroughly tested and is certain to contain deficiencies and bugs

#include <ode/config.h>
#include <stdlib.h>
#include <string.h>
#include <stdarg.h>

#ifdef HAVE_SYS_TIME_H
#include <sys/time.h>
#endif

#include <drawstuff/drawstuff.h>
#include <drawstuff/version.h>
#include "internal.h"

#include <Carbon/Carbon.h>
#include <AGL/agl.h>

// Global variables

static bool running = true;			// 1 if simulation running
static bool paused = false;			// 1 if in `pause' mode
static bool singlestep = false;		// 1 if single step key pressed
static bool writeframes = false;	// 1 if frame files to be written

static int					   	windowWidth = -1;
static int					   	windowHeight = -1;
static UInt32 					modifierMask = 0;
static int 						mouseButtonMode = 0;	
static bool						mouseWithOption = false;	// Set if dragging the mouse with alt pressed
static bool						mouseWithControl = false;	// Set if dragging the mouse with ctrl pressed

static dsFunctions*			   	functions = NULL;
static WindowRef               	windowReference;
static AGLContext              	aglContext;

static EventHandlerUPP         	mouseUPP = NULL;
static EventHandlerUPP         	keyboardUPP = NULL;
static EventHandlerUPP         	windowUPP = NULL;

// Describes the window-events we are interested in
EventTypeSpec OSX_WINDOW_EVENT_TYPES[] = {		
	{ kEventClassWindow, kEventWindowBoundsChanged },
	{ kEventClassWindow, kEventWindowClose },
	{ kEventClassWindow, kEventWindowDrawContent }
};

// Describes the mouse-events we are interested in
EventTypeSpec OSX_MOUSE_EVENT_TYPES[] = {		
	{ kEventClassMouse, kEventMouseDown },
	{ kEventClassMouse, kEventMouseUp },
	{ kEventClassMouse, kEventMouseMoved },
	{ kEventClassMouse, kEventMouseDragged }
};

// Describes the key-events we are interested in
EventTypeSpec OSX_KEY_EVENT_TYPES[] = {		
	{ kEventClassKeyboard, kEventRawKeyDown },
//	{ kEventClassKeyboard, kEventRawKeyUp },
	{ kEventClassKeyboard, kEventRawKeyModifiersChanged }
};	

//***************************************************************************
// error handling for unix

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

static void captureFrame( int num ){

  	fprintf( stderr,"\rcapturing frame %04d", num );
	unsigned char buffer[windowWidth*windowHeight][3];
	glReadPixels( 0, 0, windowWidth, windowHeight, GL_RGB, GL_UNSIGNED_BYTE, &buffer );
	char s[100];
	sprintf (s,"frame%04d.ppm",num);
	FILE *f = fopen (s,"wb");
	if( !f ){
		dsError( "can't open \"%s\" for writing", s );
	}
	fprintf( f,"P6\n%d %d\n255\n", windowWidth, windowHeight );
	for( int y=windowHeight-1; y>-1; y-- ){
		fwrite( buffer[y*windowWidth], 3*windowWidth, 1, f );
	}
	fclose (f);	
}

extern "C" void dsStop(){
	
  running = false;
}

extern "C" double dsElapsedTime()
{
#if HAVE_GETTIMEOFDAY
  static double prev=0.0;
  timeval tv ;

  gettimeofday(&tv, 0);
  double curr = tv.tv_sec + (double) tv.tv_usec / 1000000.0 ;
  if (!prev)
    prev=curr;
  double retval = curr-prev;
  prev=curr;
  if (retval>1.0) retval=1.0;
  if (retval<dEpsilon) retval=dEpsilon;
  return retval;
#else
  return 0.01666; // Assume 60 fps
#endif
}

OSStatus osxKeyEventHandler( EventHandlerCallRef handlerCallRef, EventRef event, void *userData ){
	
	UInt32 keyCode;
	UInt32 state = 0;
	void* KCHR = NULL;
	char charCode = 0;
	char uppercase = 0;
	
    switch( GetEventKind( event ) ){
        case kEventRawKeyDown:
			if( GetEventParameter( event, kEventParamKeyCode, typeUInt32, NULL, sizeof( UInt32 ), NULL, &keyCode ) != noErr ){
				break;														
			}
			KCHR = (void *)GetScriptVariable( smCurrentScript, smKCHRCache );
			charCode = (char)KeyTranslate( KCHR, keyCode, &state );
			uppercase = charCode;			
			UppercaseText( &uppercase, 1, smSystemScript );
			//printf( "Character %d [%c] [%c] modifiers [%d]\n", charCode, charCode, uppercase, modifierMask );
			
			if( modifierMask == 0 ){
				if( charCode >= ' ' && charCode <= 126 && functions -> command ){
					functions -> command( charCode );	
				}
			}
			else if( ( modifierMask & controlKey ) ){
				// ctrl+key was pressed
				switch(uppercase ){
					case 'T':
						dsSetTextures( !dsGetTextures() );
					break;
					case 'S':
						dsSetShadows( !dsGetShadows() );
					break;
					case 'X':
						running = false;
					break;
					case 'P':
						paused = !paused;
						singlestep = false;
					break;
					case 'O':
						if( paused ){
							singlestep = true;
						}
					break;
					case 'V': {
						float xyz[3],hpr[3];
						dsGetViewpoint( xyz,hpr );
						printf( "Viewpoint = (%.4f,%.4f,%.4f,%.4f,%.4f,%.4f)\n", xyz[0], xyz[1], xyz[2], hpr[0], hpr[1], hpr[2] );
					break;
					}
					case 'W':						
						writeframes = !writeframes;
						if( writeframes ){
							printf( "Now writing frames to PPM files\n" );
						}						 
					break;
				}
				
			}			
		return noErr;
        case kEventRawKeyModifiersChanged:
			if( GetEventParameter( event, kEventParamKeyModifiers, typeUInt32, NULL, sizeof( UInt32 ), NULL, &modifierMask ) == noErr ){
				if( ( mouseWithOption && !( modifierMask & optionKey ) ) || ( mouseWithControl && !( modifierMask & controlKey ) ) ){
					// The mouse was being dragged using either the command-key or the option-key modifier to emulate 
					// the right button or both left + right.
					// Now the modifier-key has been released so the mouseButtonMode must be changed accordingly
					// The following releases the right-button.
					mouseButtonMode &= (~4);
					mouseWithOption = false;
					mouseWithControl = false;
				}
				return noErr;
			}
		break;		
    }	
    return eventNotHandledErr;
}

OSStatus osxMouseEventHandler( EventHandlerCallRef handlerCallRef, EventRef event, void *userData ){
	
	bool buttonDown = false;	
	HIPoint mouseLocation;

    switch( GetEventKind( event ) ){
		
        case kEventMouseDown:
			buttonDown = true;
        case kEventMouseUp:
			if( GetEventParameter( event, kEventParamWindowMouseLocation, typeHIPoint, NULL, sizeof( HIPoint ), NULL, &mouseLocation ) != noErr ){
				break;			
			}				
			EventMouseButton button;
			if( GetEventParameter( event, kEventParamMouseButton, typeMouseButton, NULL, sizeof( EventMouseButton ), NULL, &button ) == noErr ){
				
				if( button == kEventMouseButtonPrimary ){					
					if( modifierMask & controlKey ){
						// Ctrl+button == right
						button = kEventMouseButtonSecondary;
						mouseWithControl = true;
					}	
					else if( modifierMask & optionKey ){
						// Alt+button == left+right
						mouseButtonMode = 5;
						mouseWithOption = true;
						return noErr;
					}
				}
				if( buttonDown ){
					if( button == kEventMouseButtonPrimary ) mouseButtonMode |= 1;		// Left
					if( button == kEventMouseButtonTertiary ) mouseButtonMode |= 2;	// Middle				
					if( button == kEventMouseButtonSecondary ) mouseButtonMode |= 4;	// Right
				}
				else{
					if( button == kEventMouseButtonPrimary ) mouseButtonMode &= (~1);	// Left
					if( button == kEventMouseButtonTertiary ) mouseButtonMode &= (~2);	// Middle									
					if( button == kEventMouseButtonSecondary ) mouseButtonMode &= (~4);// Right
				}		
				return noErr;
			}
		break;
        case kEventMouseMoved:
			// NO-OP
			return noErr;
        case kEventMouseDragged:
			// Carbon provides mouse-position deltas, so we don't have to store the old state ourselves
			if( GetEventParameter( event, kEventParamMouseDelta, typeHIPoint, NULL, sizeof( HIPoint ), NULL, &mouseLocation ) == noErr ){
				//printf( "Mode %d\n", mouseButtonMode );
				dsMotion( mouseButtonMode, (int)mouseLocation.x, (int)mouseLocation.y );
				return noErr;
			}
        break;
        case kEventMouseWheelMoved:
			// NO-OP
		break;
    }	
    return eventNotHandledErr;
}

static void osxCloseMainWindow(){
	
	if( windowUPP != NULL ){
		DisposeEventHandlerUPP( windowUPP );
		windowUPP = NULL;
	}
	
	if( aglContext != NULL ){
		aglSetCurrentContext( NULL );
		aglSetDrawable( aglContext, NULL );
		aglDestroyContext( aglContext );
		aglContext = NULL;
	}
	
	if( windowReference != NULL ){
		ReleaseWindow( windowReference );
		windowReference = NULL;
	}
}

OSStatus osxWindowEventHandler( EventHandlerCallRef handlerCallRef, EventRef event, void *userData ){
	
	//printf( "WindowEvent\n" );
	switch( GetEventKind(event) ){
    	case kEventWindowBoundsChanged:
      		WindowRef window;
      		GetEventParameter( event, kEventParamDirectObject, typeWindowRef, NULL, sizeof(WindowRef), NULL, &window );
      		Rect rect;
      		GetWindowPortBounds( window, &rect );			
			windowWidth = rect.right;
			windowHeight = rect.bottom;
			aglUpdateContext( aglContext );
		break;			
    	case kEventWindowClose:
			osxCloseMainWindow();
			exit( 0 );
		return noErr;			
    	case kEventWindowDrawContent:
			// NO-OP
		break;
  	}
	
  	return eventNotHandledErr;
}

static void osxCreateMainWindow( int width, int height ){
	
	int redbits = 4;
	int greenbits = 4;
	int bluebits = 4;
	int alphabits = 4;
	int depthbits = 16;
	
    OSStatus error;
		
    // create pixel format attribute list
	
    GLint pixelFormatAttributes[256];
    int numAttrs = 0;
	
    pixelFormatAttributes[numAttrs++] = AGL_RGBA;
    pixelFormatAttributes[numAttrs++] = AGL_DOUBLEBUFFER;

    pixelFormatAttributes[numAttrs++] = AGL_RED_SIZE;
	pixelFormatAttributes[numAttrs++] = redbits;
    pixelFormatAttributes[numAttrs++] = AGL_GREEN_SIZE;
	pixelFormatAttributes[numAttrs++] = greenbits;
    pixelFormatAttributes[numAttrs++] = AGL_BLUE_SIZE;        
	pixelFormatAttributes[numAttrs++] = bluebits;
	pixelFormatAttributes[numAttrs++] = AGL_ALPHA_SIZE;       
	pixelFormatAttributes[numAttrs++] = alphabits;
	pixelFormatAttributes[numAttrs++] = AGL_DEPTH_SIZE;       
	pixelFormatAttributes[numAttrs++] = depthbits;

    pixelFormatAttributes[numAttrs++] = AGL_NONE;
	
    // create pixel format.
	
    AGLDevice mainMonitor = GetMainDevice();
    AGLPixelFormat pixelFormat = aglChoosePixelFormat( &mainMonitor, 1, pixelFormatAttributes );
    if( pixelFormat == NULL ){
        return;
    }
		
    aglContext = aglCreateContext( pixelFormat, NULL );
	
    aglDestroyPixelFormat( pixelFormat );
	
    if( aglContext == NULL ){
        osxCloseMainWindow();
		return;
    }
	
    Rect windowContentBounds;
    windowContentBounds.left = 0;
    windowContentBounds.top = 0;
    windowContentBounds.right = width;
    windowContentBounds.bottom = height;
	
	int windowAttributes = kWindowCloseBoxAttribute  
		| kWindowFullZoomAttribute
		| kWindowCollapseBoxAttribute 
	 	| kWindowResizableAttribute 
	 	| kWindowStandardHandlerAttribute
		| kWindowLiveResizeAttribute;
	
    error = CreateNewWindow( kDocumentWindowClass, windowAttributes, &windowContentBounds, &windowReference );
    if( ( error != noErr ) || ( windowReference == NULL ) ){
        osxCloseMainWindow();
		return;
    }
	
	windowUPP = NewEventHandlerUPP( osxWindowEventHandler );
		
	error = InstallWindowEventHandler( windowReference, windowUPP,GetEventTypeCount( OSX_WINDOW_EVENT_TYPES ), OSX_WINDOW_EVENT_TYPES, NULL, NULL );
	if( error != noErr ){
		osxCloseMainWindow();
		return;
	}
	
	// The process-type must be changed for a ForegroundApplication
	// Unless it is a foreground-process, the application will not show in the dock or expose and the window
	// will not behave properly.
	ProcessSerialNumber currentProcess;
	GetCurrentProcess( &currentProcess );
	TransformProcessType( &currentProcess, kProcessTransformToForegroundApplication );
	SetFrontProcess( &currentProcess );
	
    SetWindowTitleWithCFString( windowReference, CFSTR( "ODE - Drawstuff" ) );
    RepositionWindow( windowReference, NULL, kWindowCenterOnMainScreen );
	
    ShowWindow( windowReference );
	
	if( !aglSetDrawable( aglContext, GetWindowPort( windowReference ) ) ){
		osxCloseMainWindow();
		return;
	}
	
    if( !aglSetCurrentContext( aglContext ) ){
        osxCloseMainWindow();
    }	
	
	windowWidth = width;
	windowHeight = height;
}

int  osxInstallEventHandlers(){

    OSStatus error;
	
    mouseUPP = NewEventHandlerUPP( osxMouseEventHandler );
	
    error = InstallEventHandler( GetApplicationEventTarget(), mouseUPP, GetEventTypeCount( OSX_MOUSE_EVENT_TYPES ), OSX_MOUSE_EVENT_TYPES, NULL, NULL );
    if( error != noErr ){
        return GL_FALSE;
    }

    keyboardUPP = NewEventHandlerUPP( osxKeyEventHandler );
	
    error = InstallEventHandler( GetApplicationEventTarget(), keyboardUPP, GetEventTypeCount( OSX_KEY_EVENT_TYPES ), OSX_KEY_EVENT_TYPES, NULL, NULL );
    if( error != noErr ){
        return GL_FALSE;
    }
	
    return GL_TRUE;
}

extern void dsPlatformSimLoop( int givenWindowWidth, int givenWindowHeight, dsFunctions *fn, int givenPause ){
	
	functions = fn;
	
	paused = givenPause;
	
	osxCreateMainWindow( givenWindowWidth, givenWindowHeight );
	osxInstallEventHandlers();
	
	dsStartGraphics( windowWidth, windowHeight, fn );
	
	static bool firsttime=true;
	if( firsttime )
	{
		fprintf
		(
		 stderr,
		 "\n"
		 "Simulation test environment v%d.%02d\n"
		 "   Ctrl-P : pause / unpause (or say `-pause' on command line).\n"
		 "   Ctrl-O : single step when paused.\n"
		 "   Ctrl-T : toggle textures (or say `-notex' on command line).\n"
		 "   Ctrl-S : toggle shadows (or say `-noshadow' on command line).\n"
		 "   Ctrl-V : print current viewpoint coordinates (x,y,z,h,p,r).\n"
		 "   Ctrl-W : write frames to ppm files: frame/frameNNN.ppm\n"
		 "   Ctrl-X : exit.\n"
		 "\n"
		 "Change the camera position by clicking + dragging in the window.\n"
		 "   Left button - pan and tilt.\n"
		 "   Right button (or Ctrl + button) - forward and sideways.\n"
		 "   Left + Right button (or middle button, or Alt + button) - sideways and up.\n"
		 "\n",DS_VERSION >> 8,DS_VERSION & 0xff
		 );
		firsttime = false;
	}
	
	if( fn -> start ) fn->start();
	
	int frame = 1;
	running = true;
	while( running ){
		// read in and process all pending events for the main window
		EventRef event;
		EventTargetRef eventDispatcher = GetEventDispatcherTarget();		
		while( ReceiveNextEvent( 0, NULL, 0.0, TRUE, &event ) == noErr ){
			SendEventToEventTarget( event, eventDispatcher );
			ReleaseEvent( event );
		}
				
		dsDrawFrame( windowWidth, windowHeight, fn, paused && !singlestep );
		singlestep = false;
		
		glFlush();
		aglSwapBuffers( aglContext );

		// capture frames if necessary
		if( !paused && writeframes ){
			captureFrame( frame );
			frame++;
		}
	}
	
	if( fn->stop ) fn->stop();
	dsStopGraphics();
	
	osxCloseMainWindow();
}
