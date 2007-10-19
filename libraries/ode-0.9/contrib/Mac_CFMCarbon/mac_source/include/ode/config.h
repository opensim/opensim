/* This file has been manually hacked together for the Mac CFM Carbon build - Frank. */

#ifndef _ODE_CONFIG_H_
#define _ODE_CONFIG_H_

/* standard system headers */
#include <stdio.h>
#include <stdlib.h>
#include <math.h>
#include <string.h>
#include <stdarg.h>
#include <malloc.h>
#include <alloca.h>
#include <float.h>

#ifdef __cplusplus
extern "C" {
#endif

/* #define PENTIUM 1 -- not a pentium */

/* integer types (we assume int >= 32 bits) */
typedef char int8;
typedef unsigned char uint8;
typedef int int32;
typedef unsigned int uint32;

/* an integer type that we can safely cast a pointer to and from without loss of bits. */
typedef unsigned int intP;

#ifdef PRECISION_DOUBLE

 /*select the base floating point type*/
 #define dDOUBLE 1

 /* the floating point infinity */
 #define dInfinity DBL_MAX

#else

 /* select the base floating point type */
 #define dSINGLE 1

 /* the floating point infinity */
 #define dInfinity FLT_MAX

#endif

#ifdef __cplusplus
}
#endif
#endif