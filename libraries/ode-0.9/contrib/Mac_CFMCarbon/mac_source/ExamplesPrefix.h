#ifndef prefix_h
#define prefix_h

#include "CommonPrefix.h"

// Hack to automatically call SIOUX's CLI interface for the test apps
#include <console.h>
#include <SIOUX.h>
int fmain (int argc, char **argv);
int main (int argc, char **argv) { argc = ccommand(&argv); return fmain(argc, argv); }
#define main(argc, argv) fmain(argc, argv)

#endif // prefix_h