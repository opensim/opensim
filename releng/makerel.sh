#!/bin/sh

# This is the one!

export OPENSIMMAJOR=0
export OPENSIMMINOR=1
export BUILD=`date +%s`
export BRANCH=PRESTABLE
export SVNURL=svn://openmetaverse.org/opensim/branches/0.1-prestable





# shouldn't have to change anything below here

./dobuild.sh $SVNURL
./createreldir.sh
rm -rf build

tar cvf opensim-$OPENSIMMAJOR.$OPENSIMMINOR-$BUILD-$BRANCH.tar opensim-$OPENSIMMAJOR.$OPENSIMMINOR/*
gzip opensim-$OPENSIMMAJOR.$OPENSIMMINOR-$BUILD-$BRANCH.tar

echo "Produced binary tarball ready for distribution."

