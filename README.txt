Build Instructions

=== Microsoft Visual Studio 2005 Sandbox Build ===

* Check out the trunk code

* Build the /src/opensim.sln solution

* open cmd window, go to /bin and launch 
OpenSim.exe -sandbox -loginserver

* open another cmd window, locate the secondlife executable
(In something like C:\Program Files\SecondLife )

* run the viewer with
secondlife.exe -loginuri http://localhost:8080/

* Have fun with your own sandbox!

== Linux/mono sandbox build ==

* check out the trunk code

* ensure you have nant (http://nant.sf.net) installed

* cd to the right directory and type "nant"

* cd to bin/ and run "mono OpenSim.exe -sandbox -loginserver"

=== Windows Nant Build ===

* same as Linux/mono build, but use
nant nogenvers
to cicumvent bash invokation
