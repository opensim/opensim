-----------------------------
ODE - Mac CFM Carbon Port
(contact Frank Condello <pox@planetquake.com> with questions regarding this port)

Although ODE contains a MacOSX makefile, and some individuals have implemented ODE in
Cocoa, I opted to use (and prefer) CodeWarrior. This also opens up ODE to MacOS8 & 9
users, without scarfing functionality in MacOSX (same binaries run on both platforms).

The 'ode_CW7.mcp' project contains release and debug targets to create static ODE and
DrawStuff libraries.

'examples_CW7.mcp' contains targets for the entire ODE test suite, plus a couple other
test programs which were posted to the ODE mailing list.


-----------------------------
Compiling Notes:

You'll need to extract the CodeWarrior projects from the 'CW7_projects.sit.bin' archive
(They're nearly a meg uncompressed so this was done to be bandwith friendly on the CVS).

Projects require CodeWarrior 7 or above (recreating them with earlier versions shouldn't
be too difficult). The projects use relative paths and are meant to be compiled from
'contrib/Mac_CFMCarbon/'. Don't move them!

All the libraries build into the 'lib/' directory, all test applications build into
'contrib/Mac_CFMCarbon/mac_testbin/' (and must be run from that directory since the
texture path is hard-coded).

You'll need to compile the release ODE library, and the DrawStuff library before
compiling the examples.

The ODE 'configurator' has not been ported, but a Mac-friendly 'config.h' header has been
manually hacked together (all PPC Macs should be fine with this header). Single or double
precision can be defined in the 'CommonPrefix.h' header found in
'contrib/Mac_CFMCarbon/mac_source/'.

'contrib/Mac_CFMCarbon/mac_source/' also contains any mac specific additions to the main source.
The directory structure here matches the main source tree, and I would recommend that this
format is maintained when making additions, since the access paths are touchy (more below...)

Some issues were encountered with duplicate header names. CodeWarrior tends to be
unforgiving about this sort of thing but fudging with the access paths eventually
cleared up the problem. If ODE fails to compile, make sure the <ode/objects.h> and
"objects.h" or <timer.h> and <Timer.h> are actually pointing to the correct header.

You'll need Apple's OpenGL SDK (with GLUT) in your compiler path to build DrawStuff. I've
added redirection headers in 'contrib/Mac_CFMCarbon/mac_source/include/GL/' to properly
link with the Apple headers (since the projects are set to follow DOS paths).

The examples link against a crapload of static libraries, but my initial builds using
ODE, MSL, GLUT, and DrawStuff shared/merged DLL's proved unstable (mostly problems with
SIOUX spawning multiple sessions, and crashes in Classic). Static libs just worked better
in the end, but the test apps are a little bloated as a result, and need to be re-linked
whenever a change to a library is made.

IMPORTANT: You must use the same 'CommonPrefix.h' settings for libraries, and test apps
(i.e. double or single precision).


-----------------------------
Running the test apps:

The test apps will show the SIOUX CLI prompt when run. Just hit OK to ignore it, or add any
DrawStuff arguments. You'll want to log output to a file for 'test_ode'.

There are two extra test programs in the 'mac_source' directory. Both were posted to the ODE
mailing list by OSX users. 'test_stability1' visualizes some internal issues with ODE, and
'test_stacktest' is a standalone GLUT program (doesn't use DrawStuff) that can be useful
to stress test the library, and give you an idea of just how much stack memory you're
going to need for large systems.

ISSUES:

The carbon DrawStuff lib uses GLUT to make life easy, but GLUT isn't exactly bug-free
or stable on the Mac... Try moving the mouse around if a simulation is running slowly
on OS9 (it's not ODE's fault, but rather a poor carbon GLUT implementation - seems GLUT stalls
when it's not getting system events  - I haven't seen this problem on OSX).

The 3D view may not update if typing in the SIOUX console window.

You cannot pass startup args to GLUT due to the way the DrawStuff library initializes.

'Write Frames' doesn't actually do anything at the moment.

The 'test_joints' app seems broken (though I don't know what the intended effect should be)


-----------------------------
TODO:

- Re-add shared library targets (if stability issues are resolved).
- Implement 'Write Frames' in DrawStuff.
- Write a Carbon compatible configurator
- Create CodeWarrior 8 projects (once I scrounge up enough dough for the update).