#!/bin/sh
mono bin/Prebuild.exe /target nant
perl -pi -e 's{OpenSim.dll}{OpenSim.exe}' OpenSim/ApplicationPlugins/LoadRegions/OpenSim.ApplicationPlugins.LoadRegions.dll.build
nant

