#!/bin/sh

# this script does a guaranteed clean build from SVN using a URL specified on the command line

rm -rf build/
mkdir build

printf "Getting fresh source tree from SVN..."
svn checkout $1 build

printf "Updating templates..."
./parsetmpl.sh templates/VersionInfo.cs.tmpl >build/OpenSim.RegionServer/VersionInfo.cs

printf "Running prebuild..."
cd build
mono bin/Prebuild.exe /target nant
if [ $? -eq 0 ]
  if [ $? -eq 0 ]
    printf "Doing the build..."
    nant
  else
    exit 1
  fi
else
  exit 1
fi

