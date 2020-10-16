# Building on Windows

## Requirements
  For building under Windows, the following is required:

  * [Visual Studio .NET](https://visualstudio.microsoft.com/vs/features/net-development/), version 2015 or later

### Building Standalone
 To create the project files, run   

 ```runprebuild.bat```

Load the generated OpenSim.sln into Visual Studio .NET and build the solution.

Copy `OpenSim.ini.exmple` to `OpenSim.ini` in the `bin/config-include` directory, and verify that the `[Const]` and `[Architecture]` sections are what you want.

copy the `StandaloneCommon.ini.example` to `StandaloneCommon.ini` in the `bin/config-include` directory.

The StandaloneCommon.ini file describes the database and backend services that OpenSim will use, and is set to use sqlite by default, which requires no setup.

Now just run `OpenSim.exe` from the `bin` folder, and set up the region.

# Building on Linux / Mac

## Requirements

 *	[Mono > 5.0](https://www.mono-project.com/download/stable/#download-lin)
 *	On some Linux distributions you may need to install additional packages.
 *	msbuild or xbuild(deprecated) if still supported by the mono version
 *   See [the wiki](http://opensimulator.org/wiki/Dependencies) for more information.

### Building Standalone
  To create the project files, run:

  ```./runprebuild.sh```

  then run ```msbuild``` or ```xbuild``` if xbuild was installed.

 Copy `OpenSim.ini.exmple` to `OpenSim.ini` in the `bin/config-include` directory, and verify that the `[Const]` and `[Architecture]` sections are what you want.

copy the `StandaloneCommon.ini.example` to `StandaloneCommon.ini` in the `bin/config-include` directory.

 The StandaloneCommon.ini file describes the database and backend services that OpenSim will use, and is set to use sqlite by default, which requires no setup.

run `./opensim.sh` from the `bin` folder, and set up the region

For rebuilding and debugging use the msbuild option switches
  *  clean:  `msbuild /target:clean`
  *  debug: (default) `msbuild /property:Configuration=Debug`
  *  release: `msbuild /property:Configuration=Release`


# Building other versions
Other versions of OpenSim can be built by changing the .ini files in bin/config-include

For example, if you wanted to create an instance of OpenSim running in grid mode, comment the version that you currently have, (by default it is Standalone.ini) and uncomment
```Include-Architecture = "config-include/Grid.ini"```
and copy the `GridCommon.ini.example` file to `GridCommon.ini` inside the `bin/config-include` directory.

The same can be done for `StandaloneHypergrid.ini`, and `GridHyperGrid.ini`.


# References

Helpful resources:
* http://opensimulator.org/wiki/Build_Instructions
