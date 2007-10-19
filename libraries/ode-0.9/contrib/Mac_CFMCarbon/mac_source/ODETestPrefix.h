#ifndef prefix_h
#define prefix_h

#include "CommonPrefix.h"

// Hack to automatically call SIOUX's CLI interface for the test apps
#include <console.h>
#include <SIOUX.h>
int fmain ();
int main (int argc, char **argv) { argc = ccommand(&argv); return fmain(); }
#define main() fmain()

#endif // prefix_h