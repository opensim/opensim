IMPORTANT NOTES

* Please note that at the moment, the sandbox login server has temporary changed from 8080 to 9000 - this will (probably) change back.

---

BUILD INSTRUCTIONS

=== Microsoft Visual Studio 2005 Sandbox Build ===

* Check out the trunk code

* Build the /OpenSim.sln solution

* open cmd window, go to /bin and launch 
OpenSim.exe -sandbox -loginserver

* open another cmd window, locate the secondlife executable
(In something like C:\Program Files\SecondLife )

* run the viewer with
secondlife.exe -loginuri http://localhost:9000/

* Have fun with your own sandbox!

== Linux/mono sandbox build ==

* check out the trunk code

* ensure you have nant (http://nant.sf.net) installed

* cd to the trunk root directory and type "nant"

* cd to bin/ and run "mono OpenSim.exe -sandbox -loginserver"


RUNNING SANDBOX WITH USER ACCOUNTS

* open cmd window, go to /bin and launch 
OpenSim.exe -sandbox -loginserver -useraccounts

* launch web browser, go to
http://localhost:9000/Admin
enter password 'Admin'

* Select 'Accounts', enter credentials, press 'Create'

* Now, log on thru your viewer (see above) with your newly created credentials.

* Have Fun!



PREBUILD

We use Prebuild to generate vs2005 solutions and nant build scripts.

=== Building Prebuild ===

At the moment, the Prebuild exe is shipped as /bin/Prebuild.exe so you shouldn't really have to build it.

But here's the instructions anyway :

The Prebuild master project is /prebuild.xml

To build it with vs2005 :

* build the solution /Prebuild/Prebuild.sln

To build it with nant :

* cd to /Prebuild/
* type 'nant'

After you've built it, it will land in the root /bin/ directory,

=== Modyfying the OpenSim solution ===

When adding or changing projects, modify the prebuild.xml and then execute

bin/Prebuild.exe /target {target}

where target is either 
vs2005 - to generate new vs2005 solutions and projects
nant - to generate new nant build scripts

Remember to run prebuild whenever you've added or removed files as well.


LOCAL SET-UP OF OGS CONFIGURATION

**NOTE: At the moment OGS is non-functionable, so this WON'T WORK **NOTE

* start up bin/OpenGridServices.GridServer.exe (listens on http://localhost:8001/gridserver)
  * just press enter to keep the defaults

* start up bin/OpenGridServices.UserServer.exe (listens on http://localhost:8002/userserver)
  * just press enter to keep the defaults

* start up bin/OpenSim.exe ( listens for udp on port 9000 )
  * just press enter to keep the defaults

* start the secondlife viewer with -loginuri http://localhost:8080/

