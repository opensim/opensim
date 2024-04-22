# git clone

get or update source from git

 `git clone git://opensimulator.org/git/opensim`
	


# Building on Windows

## Requirements
  For building under Windows, the following is required:
  * [Microsoft DotNet 6.0](https://dotnet.microsoft.com/en-us/download), version 6.0 or later. 

  dotnet 8.0 is the LTS version and is recommended.
  To building under Windows, the following is required:

  * [dotnet 6.0 SDK, Runtime and Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)

optionally also

  * [Visual Studio .NET](https://visualstudio.microsoft.com/vs/features/net-development/), version 2022 or later
  

### Building
 Prebuild is no longer used.  There is a top level Solution (sln) and csproj files for each
 of the projects in the solution.  To run a build use either Visual Studio Community (recommended on Windows)
 or from a CLI run:`
 
 dotnet build --configuration Debug
 dotnet build --configuration Release

Either command will do a NuGet restore (dotnet restore) to restore any required NuGet package references prior to
kicking off a build using a current version of msbuild.  The Csproj and SLN files are all designed to use the new
format for Msbuild which is simplified and really directly replaces what prebuild provided.

run 
  `compile.bat`

Or load the generated OpenSim.sln into Visual Studio or Visual Studio Code and build the solution.

Configure, see below

The resulting build will be generated to ./build/{Debug|Release}/

# Building on Linux / Mac

## Requirements

 * [Microsoft DotNet 6.0 or later](https://dotnet.microsoft.com/en-us/download). 
    dotnet 8.0 is the LTS version and is recommended.
 * [dotnet 6.0 SDK and Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
 * libgdiplus 
 
 if you have mono 6.x complete, you already have libgdiplus, otherwise you need to install it
 using a package manager for your operating system, like apt, brew, macports, etc
 for example on debian:
 
 `apt-get update && apt-get install -y apt-utils libgdiplus libc6-dev`

### Building
 Prebuild is no longer used.  There is a top level Solution (sln) and csproj files for each
 of the projects in the solution.  To run a build from a CLI run:
 
 dotnet build --configuration Debug
 dotnet build --configuration Release

Either command will do a NuGet restore (dotnet restore) to restore any required NuGet package references prior to
kicking off a build using a current version of msbuild.  The Csproj and SLN files are all designed to use the new
format for Msbuild which is simplified and really directly replaces what prebuild provided.

Configure. See below

The resulting build will be generated to ./build/{Debug|Release}/

For rebuilding and debugging use the dotnet command options
  *  clean:  `dotnet clean
  *  restore: dotnet restore
  *  debug:   dotnet build --configuration Debug
  *  release: dotnet build --configuration Release

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

* http://opensimulator.org/wiki/Configuration
