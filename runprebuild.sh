#!/bin/sh

mono bin/Prebuild.exe /target nant
# needed until we break up OpenSim.exe
perl -pi -e 's{OpenSim.dll}{OpenSim.exe}' OpenSim/ApplicationPlugins/LoadRegions/OpenSim.ApplicationPlugins.LoadRegions.dll.build
mono bin/Prebuild.exe /target vs2005
