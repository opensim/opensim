# Building on Windows

Steps:
 * runprebuild.bat
 * Load OpenSim.sln into Visual Studio .NET and build the solution.
 * chdir bin 
 * copy OpenSim.ini.example to OpenSim.ini and other appropriate files in bin/config-include
 * run OpenSim.exe

# Building on Linux

Prereqs:
*	Mono >= 2.4.3
*	Nant >= 0.85
*	On some Linux distributions you may need to install additional packages.
	See http://opensimulator.org/wiki/Dependencies for more information.
*	May also use xbuild (included in mono distributions)
*	May use Monodevelop, a cross-platform IDE

From the distribution type:
 * ./runprebuild.sh
 * nant (or !* xbuild)
 * cd bin 
 * copy OpenSim.ini.example to OpenSim.ini and other appropriate files in bin/config-include
 * run mono OpenSim.exe
 !* xbuild option switches
 !*          clean:  xbuild /target:clean
 !*          debug: (default) xbuild /property:Configuration=Debug
 !*          release: xbuild /property:Configuration=Release

# Using Monodevelop

From the distribution type:
 * ./runprebuild.sh
 * type monodevelop OpenSim.sln

# References
 
Helpful resources:
* http://opensimulator.org/wiki/Build_Instructions
