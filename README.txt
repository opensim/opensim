Build Instructions

=== Microsoft Visual Studio 2005 Sandbox Build ===

* Check out the trunk code

* Build the /OpenSim.sln solution

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

* cd to the trunk root directory and type "nant"

* cd to bin/ and run "mono OpenSim.exe -sandbox -loginserver"

=== Prebuild ===

We use Prebuild to generate vs2005 solutions and nant build scripts.

The Prebuild master project is /prebuild.xml

To build it with vs2005 :

* build the solution /Prebuild/Prebuild.sln

To build it with nant :

* cd to /Prebuild/
* type 'nant'

After you've built it, it will land in the root /bin/ directory,

When adding or changing projects, modify the prebuild.xml and then execute

bin/Prebuild.exe /target {target}

where target is either 
vs2005 - to generate new vs2005 solutions and projects
nant - to generate new nant build scripts

Remember to run prebuild whenever you've added or removed files as well.

