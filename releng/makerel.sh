#!/bin/sh

# This is the one!

export OPENSIMMAJOR=0
export OPENSIMMINOR=1
export BRANCH=DEVEL
export SVNURL=svn://openmetaverse.org/opensim/trunk





# shouldn't have to change anything below here

./dobuild.sh $SVNURL
./createreldir.sh

tar cvf opensim-$OPENSIMMAJOR.$OPENSIMMINOR-$BRANCH.tar opensim-$OPENSIMMAJOR.$OPENSIMMINOR/*
gzip opensim-$OPENSIMMAJOR.$OPENSIMMINOR-$BRANCH.tar

echo "Produced binary tarball ready for distribution."

