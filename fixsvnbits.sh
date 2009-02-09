#!/bin/sh

# This can be used to fix up the svn bits to keep line endings sane
# between platforms.

find OpenSim -type f | grep -v '.svn' | xargs perl -pi -e 's/\r//g'
find OpenSim -type f | grep -v '.svn' | xargs svn propset svn:eol-style native 

