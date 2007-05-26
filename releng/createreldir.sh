#!/bin/sh

# this script creates a new opensim-major.minor directory and copies all the relevant files into it
# not designed for direct invocation from the command line

mkdir opensim-$OPENSIMMAJOR.$OPENSIMMINOR

cp -R dist/* opensim-$OPENSIMMAJOR.$OPENSIMMINOR
cp -R build/bin/* opensim-$OPENSIMMAJOR.$OPENSIMMINOR/bin
