#!/bin/sh
ulimit -s 1048576
# next option may improve SGen gc (for opensim only)
#export MONO_GC_PARAMS="minor=split,promotion-age=14"
mono --desktop OpenSim32.exe
