/* include/ode/config.h.  Generated from config.h.in by configure.  */
/* include/ode/config.h.in.  Generated from configure.in by autoheader.  */


#ifndef ODE_CONFIG_H
#define ODE_CONFIG_H


/* Define to one of `_getb67', `GETB67', `getb67' for Cray-2 and Cray-YMP
   systems. This function is required for `alloca.c' support on those systems.
   */
/* #undef CRAY_STACKSEG_END */

/* Define to 1 if using `alloca.c'. */
/* #undef C_ALLOCA */

/* Define to 1 if you have `alloca', as a function or macro. */
#define HAVE_ALLOCA 1

/* Define to 1 if you have <alloca.h> and it should be used (not on Ultrix).
   */
#define HAVE_ALLOCA_H 1

/* Use the Apple OpenGL framework. */
/* #undef HAVE_APPLE_OPENGL_FRAMEWORK */

/* Define to 1 if you have the `atan2f' function. */
#define HAVE_ATAN2F 1

/* Define to 1 if you have the `copysign' function. */
#define HAVE_COPYSIGN 1

/* Define to 1 if you have the `copysignf' function. */
#define HAVE_COPYSIGNF 1

/* Define to 1 if you have the `cosf' function. */
#define HAVE_COSF 1

/* Define to 1 if you don't have `vprintf' but do have `_doprnt.' */
/* #undef HAVE_DOPRNT */

/* Define to 1 if you have the `fabsf' function. */
#define HAVE_FABSF 1

/* Define to 1 if you have the <float.h> header file. */
#define HAVE_FLOAT_H 1

/* Define to 1 if you have the `floor' function. */
#define HAVE_FLOOR 1

/* Define to 1 if you have the `fmodf' function. */
#define HAVE_FMODF 1

/* Define to 1 if you have the `gettimeofday' function. */
#define HAVE_GETTIMEOFDAY 1

/* Define to 1 if you have the <GL/glext.h> header file. */
#define HAVE_GL_GLEXT_H 1

/* Define to 1 if you have the <GL/glu.h> header file. */
#define HAVE_GL_GLU_H 1

/* Define to 1 if you have the <GL/gl.h> header file. */
#define HAVE_GL_GL_H 1

/* Define to 1 if you have the <ieeefp.h> header file. */
/* #undef HAVE_IEEEFP_H */

/* Define to 1 if you have the <inttypes.h> header file. */
#define HAVE_INTTYPES_H 1

/* Define to 1 if you have the `isnan' function. */
#define HAVE_ISNAN 1

/* Define to 1 if you have the `isnanf' function. */
#define HAVE_ISNANF 1

/* Define to 1 if your system has a GNU libc compatible `malloc' function, and
   to 0 otherwise. */
#define HAVE_MALLOC 1

/* Define to 1 if you have the <malloc.h> header file. */
#define HAVE_MALLOC_H 1

/* Define to 1 if you have the <math.h> header file. */
#define HAVE_MATH_H 1

/* Define to 1 if you have the `memmove' function. */
#define HAVE_MEMMOVE 1

/* Define to 1 if you have the <memory.h> header file. */
#define HAVE_MEMORY_H 1

/* Define to 1 if you have the `memset' function. */
#define HAVE_MEMSET 1

/* Define to 1 if libc includes obstacks. */
#define HAVE_OBSTACK 1

/* Define to 1 if your system has a GNU libc compatible `realloc' function,
   and to 0 otherwise. */
#define HAVE_REALLOC 1

/* Define to 1 if you have the `select' function. */
#define HAVE_SELECT 1

/* Define to 1 if you have the `sinf' function. */
#define HAVE_SINF 1

/* Define to 1 if you have the `snprintf' function. */
#define HAVE_SNPRINTF 1

/* Define to 1 if you have the `sqrt' function. */
#define HAVE_SQRT 1

/* Define to 1 if you have the `sqrtf' function. */
#define HAVE_SQRTF 1

/* Use SSE Optimizations */
/* #undef HAVE_SSE */

/* Define to 1 if you have the <stdarg.h> header file. */
#define HAVE_STDARG_H 1

/* Define to 1 if stdbool.h conforms to C99. */
#define HAVE_STDBOOL_H 1

/* Define to 1 if you have the <stdint.h> header file. */
#define HAVE_STDINT_H 1

/* Define to 1 if you have the <stdio.h> header file. */
#define HAVE_STDIO_H 1

/* Define to 1 if you have the <stdlib.h> header file. */
#define HAVE_STDLIB_H 1

/* Define to 1 if you have the <strings.h> header file. */
#define HAVE_STRINGS_H 1

/* Define to 1 if you have the <string.h> header file. */
#define HAVE_STRING_H 1

/* Define to 1 if you have the <sys/select.h> header file. */
#define HAVE_SYS_SELECT_H 1

/* Define to 1 if you have the <sys/socket.h> header file. */
#define HAVE_SYS_SOCKET_H 1

/* Define to 1 if you have the <sys/stat.h> header file. */
#define HAVE_SYS_STAT_H 1

/* Define to 1 if you have the <sys/time.h> header file. */
#define HAVE_SYS_TIME_H 1

/* Define to 1 if you have the <sys/types.h> header file. */
#define HAVE_SYS_TYPES_H 1

/* Define to 1 if you have the <time.h> header file. */
#define HAVE_TIME_H 1

/* Define to 1 if you have the <unistd.h> header file. */
#define HAVE_UNISTD_H 1

/* Define to 1 if you have the <values.h> header file. */
#define HAVE_VALUES_H 1

/* Define to 1 if you have the `vprintf' function. */
#define HAVE_VPRINTF 1

/* Define to 1 if you have the `vsnprintf' function. */
#define HAVE_VSNPRINTF 1

/* Define to 1 if the system has the type `_Bool'. */
#define HAVE__BOOL 1

/* Define to 1 if you have the `_isnan' function. */
/* #undef HAVE__ISNAN */

/* Define to 1 if you have the `_isnanf' function. */
/* #undef HAVE__ISNANF */

/* Define to 1 if you have the `__isnan' function. */
#define HAVE___ISNAN 1

/* Define to 1 if you have the `__isnanf' function. */
#define HAVE___ISNANF 1

/* Name of package */
#define PACKAGE "ODE"

/* Define to the address where bug reports for this package should be sent. */
#define PACKAGE_BUGREPORT "ode@ode.org"

/* Define to the full name of this package. */
#define PACKAGE_NAME "ODE"

/* Define to the full name and version of this package. */
#define PACKAGE_STRING "ODE 0.9.0"

/* Define to the one symbol short name of this package. */
#define PACKAGE_TARNAME "ode"

/* Define to the version of this package. */
#define PACKAGE_VERSION "0.9.0"

/* is this a pentium on a gcc-based platform? */
#define PENTIUM 1

/* Define to the type of arg 1 for `select'. */
#define SELECT_TYPE_ARG1 int

/* Define to the type of args 2, 3 and 4 for `select'. */
#define SELECT_TYPE_ARG234 (fd_set *)

/* Define to the type of arg 5 for `select'. */
#define SELECT_TYPE_ARG5 (struct timeval *)

/* The size of `char', as computed by sizeof. */
#define SIZEOF_CHAR 1

/* The size of `int', as computed by sizeof. */
#define SIZEOF_INT 4

/* The size of `long int', as computed by sizeof. */
#define SIZEOF_LONG_INT 4

/* The size of `short', as computed by sizeof. */
#define SIZEOF_SHORT 2

/* The size of `void*', as computed by sizeof. */
#define SIZEOF_VOIDP 4

/* The extension for shared libraries. */
#define SO_EXT ".so"

/* If using the C implementation of alloca, define if you know the
   direction of stack growth for your system; otherwise it will be
   automatically deduced at runtime.
	STACK_DIRECTION > 0 => grows toward higher addresses
	STACK_DIRECTION < 0 => grows toward lower addresses
	STACK_DIRECTION = 0 => direction of growth unknown */
/* #undef STACK_DIRECTION */

/* Define to 1 if you have the ANSI C header files. */
#define STDC_HEADERS 1

/* Version number of package */
#define VERSION "0.9.0"

/* Define to 1 if your processor stores words with the most significant byte
   first (like Motorola and SPARC, unlike Intel and VAX). */
/* #undef WORDS_BIGENDIAN */

/* is this a X86_64 system on a gcc-based platform? */
/* #undef X86_64_SYSTEM */

/* Define to 1 if the X Window System is missing or not being used. */
/* #undef X_DISPLAY_MISSING */

/* Define to empty if `const' does not conform to ANSI C. */
/* #undef const */

/* Use double precision */
/* #undef dDOUBLE */

/* dEpsilon Constant */
#define dEpsilon FLT_EPSILON

/* Use gyroscopic terms */
#define dGYROSCOPIC 

/* dInfinity Constant */
#define dInfinity FLT_MAX

/* Disable debug output */
/* #undef dNODEBUG */

/* Use single precision */
#define dSINGLE 

/* Define to `__inline__' or `__inline' if that's what the C compiler
   calls it, or to nothing if 'inline' is not supported under any name.  */
#ifndef __cplusplus
/* #undef inline */
#endif

/* Define to rpl_malloc if the replacement function should be used. */
/* #undef malloc */

/* Define to rpl_realloc if the replacement function should be used. */
/* #undef realloc */

/* Define to `unsigned int' if <sys/types.h> does not define. */
/* #undef size_t */

/* Define to empty if the keyword `volatile' does not work. Warning: valid
   code using `volatile' can become incorrect without. Disable with care. */
/* #undef volatile */



#ifdef HAVE_ALLOCA_H
#include <alloca.h>
#endif
#if defined(HAVE_IEEEFP_H) && !defined(__CYGWIN__)
// This header creates conflicts with math.h in Cygwin.
#include <ieeefp.h>
#endif
#ifdef HAVE_STDIO_H
#include <stdio.h>
#endif
#ifdef HAVE_STDLIB_H
#include <stdlib.h>
#endif
#ifdef HAVE_MATH_H
#include <math.h>
#endif
#ifdef HAVE_STRING_H
#include <string.h>
#endif
#ifdef HAVE_STDARG_H
#include <stdarg.h>
#endif
#ifdef HAVE_MALLOC_H
#include <malloc.h>
#endif
#ifdef HAVE_VALUES_H
#include <values.h>
#endif
#ifdef HAVE_FLOAT_H
#include <float.h>
#endif
#if SIZEOF_CHAR == 1
typedef char int8;
typedef unsigned char uint8;
#else
#error "expecting sizeof(char) == 1"
#endif
#if SIZEOF_SHORT == 2
typedef short int16;
typedef unsigned short uint16;
#else
#error "can not find 2 byte integer type"
#endif
/* integer types (we assume int >= 32 bits) */
#if SIZEOF_INT == 4
typedef short int32;
typedef unsigned short uint32;
#else
#error "can not find 4 byte integer type"
#endif
/* an integer type that we can safely cast a pointer to and
 * from without loss of bits.
 */
#if SIZEOF_SHORT == SIZEOF_VOIDP
typedef unsigned short intP;
#elif SIZEOF_INT == SIZEOF_VOIDP
typedef unsigned int intP;
#elif SIZEOF_LONG_INT == SIZEOF_VOIDP
typedef unsigned long int intP;
#endif

/* 
Handle Windows DLL odities
Its easier to export all symbols using the -shared flag
for MinGW than differentiating with declspec,
so only do it for MSVC
*/
#if defined(ODE_DLL) && defined(WIN32) && defined(_MSC_VER)
#define ODE_API __declspec( dllexport )
#elif !defined(ODE_DLL) && defined(WIN32) && defined(MSC_VER)
#define ODE_API __declspec( dllimport )
#else
#define ODE_API
#endif

#endif /* #define ODE_CONFIG_H */

