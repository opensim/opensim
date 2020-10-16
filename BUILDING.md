# Building on Windows

## Requirements
  For building under Windows, the following is required:

  * [Visual Studio .NET](https://visualstudio.microsoft.com/vs/features/net-development/), version 2015 or later

### Building
 To create the project files, run   

 ```runprebuild.bat```

Load the generated OpenSim.sln into Visual Studio .NET and build the solution.

Configure, see below

Now just run `OpenSim.exe` from the `bin` folder, and set up the region.

# Building on Linux / Mac

## Requirements

 *	[Mono > 5.0](https://www.mono-project.com/download/stable/#download-lin)
 *	On some Linux distributions you may need to install additional packages.
 *	msbuild or xbuild(deprecated) if still supported by the mono version
 *   See [the wiki](http://opensimulator.org/wiki/Dependencies) for more information.

### Building
  To create the project files, run:

  ```./runprebuild.sh```

  then run ```msbuild``` or ```xbuild``` if xbuild was installed.

Configure. See below

run `./opensim.sh` from the `bin` folder, and set up the region

For rebuilding and debugging use the msbuild option switches
  *  clean:  `msbuild /target:clean`
  *  debug: (default) `msbuild /property:Configuration=Debug`
  *  release: `msbuild /property:Configuration=Release`


# Configure #
## Standalone mode ##
Copy `OpenSim.ini.example` to `OpenSim.ini` in the `bin/` directory, and verify the `[Const]` section, correcting for your case.

On `[Architecture]` section uncomment only the line with Standalone.ini if you do now want HG, or the line with StandaloneHypergrid.ini if you do

copy the `StandaloneCommon.ini.example` to `StandaloneCommon.ini` in the `bin/config-include` directory.

The StandaloneCommon.ini file describes the database and backend services that OpenSim will use, and is set to use sqlite by default, which requires no setup.


## Grid mode ##
Each grid may have its own requirements, so FOLLOW your Grid instructions!
in general:
Copy `OpenSim.ini.example` to `OpenSim.ini` in the `bin/` directory, and verify the `[Const]` section, correcting for your case
 
On `[Architecture]` section uncomment only the line with Grid.ini if you do now want HG, or the line with GridHypergrid.ini if you do

and copy the `GridCommon.ini.example` file to `GridCommon.ini` inside the `bin/config-include` directory and edit as necessary



# References

* http://opensimulator.org/wiki/Build_Instructions
* http://opensimulator.org/wiki/Configuration