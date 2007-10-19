Breakable Joints

================================================================================

Description:
This is a small addition to ODE that makes joints breakable. Breakable means
that if a force on a joint is to high it wil break. I have included a modified
version of test_buggy.cpp (test_breakable.cpp) so you can see it for your self.
Just drive your buggy into an obstacle and enjoy!

================================================================================

Installation instructions:
- copy joint.h, joint.cpp, ode.cpp and step.cpp to the ode/src/ directory
- copy common.h and object.h to the include/ directory
- copy test_breakable.cpp to the ode/test/ directory
- add test_breakable.cpp to the ODE_TEST_SRC_CPP object in the makefile.
- make ode-lib
- make ode-test
You can also use the diffs. The above files will quickly go out of sync with the
rest of ODE but the diffs wil remain valid longer.

================================================================================

Functions:
dJointSetBreakable (dJointID joint, int b)
	If b is 1 the joint is made breakable. If b is 0 the joint is made
	unbreakable.

void dJointSetBreakCallback (dJointID joint, dJointBreakCallback *callbackFunc)
	Sets the callback function for this joint. If a funtion is set it will be
	called if the joint is broken but before it is actually detached or deleted.

void dJointSetBreakMode (dJointID joint, int mode)
	Use this functions to set some flags. These flags can be ORred ( | )
	together; ie. dJointSetBreakMode (someJoint,
dJOINT_BREAK_AT_B1_FORCE|dJOINT_DELETE_ON_BREAK)
	dJOINT_DELETE_ON_BREAK    - If the joint breaks it wil be deleted.
	dJOINT_BREAK_AT_B1_FORCE  - If the force on body 1 is to high the joint will
								break
	dJOINT_BREAK_AT_B1_TORQUE - If the torque on body 1 is to high the joint will
								break
	dJOINT_BREAK_AT_B2_FORCE  - If the force on body 2 is to high the joint will
								break
	dJOINT_BREAK_AT_B2_TORQUE - If the torque on body 2 is to high the joint will
								break

void dJointSetBreakForce (dJointID joint, int body, dReal x, dReal y, dReal z)
	With this function you can set the maximum force for a body connected to this
	joint. A value of 0 for body means body 1, 1 means body 2. The force is 
	relative to the	bodies rotation.

void dJointSetBreakTorque (dJointID joint, int body, dReal x, dReal y, dReal z)
	With this function you can set the maximum torque for a body connected to this
	joint. A value of 0 for body means body 1, 1 means body 2. The torque is 
	relative to the	bodies rotation.

int dJointIsBreakable (dJointID joint)
	Returns 1 if this joint is breakable, 0 otherwise.
	
int dJointGetBreakMode (dJointID joint)
	Returns the breakmode flag.

void dJointGetBreakForce (dJointID joint, int body, dReal *force)
	Returns the force at what this joint will break. A value of 0 for body means
	body 1, 1 means body 2. force must have enough space for 3 dReal values.

void dJointGetBreakTorque (dJointID joint, int body, dReal *torque)
	Returns the torque at what this joint will break. A value of 0 for body 
	means body 1, 1 means body 2. force must have enough space for 3 dReal 
	values.

================================================================================

The callback function is defined like this (in common.h):
void dJointBreakCallback (dJointID);

================================================================================

Problems, known bugs & other issues:
- If the timestep is very small then joints get a lot weaker. They can even fall
  apart!
- I have tested all this with the latest checkout from CVS (at the time of 
  writing ofcourse). I haven't tested it with earlier versions of ODE.
- I have modified the code that fills the jointfeedback struct. I haven't tested
  if it still works.
- I'm not sure if the forces are really relative to the connected bodies.
- There are some memory leaks in the test_breakable.cpp example.

================================================================================

Bugfixes and changes:
09/08/2003
- I fixed a bug when there where 0 joints in the simulation

06/12/2003
- dJointGetBreakMode() added, by vadim_mcagon@hotmail.com

11/03/2004
- Updated files to work with latest CVS checkout. 
- Added support for dWorldStepFast1()
- Added separate test_breakable.cpp example.
- Updated the code that breaks and destroys a joint.

================================================================================

Send me an e-mail if you have any suggestions, ideas, bugs, bug-fixes, anything!
e-mail: roelvandijk@home.nl

Roel van Dijk - 11/03/2004
