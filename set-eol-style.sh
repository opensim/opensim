#!/bin/sh

find OpenSim -name \*\.cs | xargs perl -pi -e 's/\r//' 
find OpenSim -name \*\.cs | xargs svn propset svn:eol-style native

