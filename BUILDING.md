# Building on Windows

Steps:
 * runprebuild.bat
 * Load OpenSim.sln into Visual Studio .NET and build the solution.
 * chdir bin 
 * copy OpenSim.ini.example to OpenSim.ini and other appropriate files in bin/config-include
 * run OpenSim.exe

# Building on Linux

Prereqs:
*	Mono > 5.0
*	On some Linux distributions you may need to install additional packages.
	See http://opensimulator.org/wiki/Dependencies for more information.
	msbuild or xbuild if still supported by mono version

From the distribution type:
 * ./runprebuild.sh
 * type msbuild or xbuild)
 * cd bin 
 * copy OpenSim.ini.example to OpenSim.ini and other appropriate files in bin/config-include
 * windoes: execute opensim.exe or opensim32.exe for small regions
 * linux: run ./opensim.sh
 !* msbuild (xbuild) option switches
 !*          clean:  msbuild /target:clean
 !*          debug: (default) msbuild /property:Configuration=Debug
 !*          release: msbuild /property:Configuration=Release

# References
 
Helpful resources:
* http://opensimulator.org/wiki/Build_Instructions
