OpenSim Test Suite
------------------------------------------------------------

The eventual goal of the OpenSim Test Suite is to provide a framework
and a set of tests to do system level regression testing of OpenSim.
In short:

OpenSim Test Suite will have Test Modules (Mono Addins?) that will
verify certain paths in the code.  Some early modules may be (subject
to change):

 * Login Tests
    - Attempt to Log in 1, 5, 20 bots.  
 * Basic Walk Tests
    - Attempt to Log in and move about in well known tracks
    - Repeat with 5, 20 bots
 * Basic Construct Tests
    - Construct Simple Objects in World
    - Ensure bots can see other objects constructed
 * Basic Asset Tests
    - Construct Simple Objects in World with Textures
    - Pull Objects and Textures


    