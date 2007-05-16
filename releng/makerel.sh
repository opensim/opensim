#!/bin/sh

# This is the one!

export OPENSIMMAJOR=0
export OPENSIMMINOR=1
export BUILD=`date +%s`
export BRANCH=DEVEL
export SVNURL=svn://openmetaverse.org/opensim/trunk





# shouldn't have to change anything below here

script dobuild.log -c ./dobuild.sh $SVNURL
if [ ! $? -eq 0 ]
then
  echo "Build failed!"
else
  script createrel.log -c ./createreldir.sh
  rm -rf build
  tar cvf opensim-$OPENSIMMAJOR.$OPENSIMMINOR-$BUILD-$BRANCH.tar opensim-$OPENSIMMAJOR.$OPENSIMMINOR/*
  gzip opensim-$OPENSIMMAJOR.$OPENSIMMINOR-$BUILD-$BRANCH.tar
fi

rm -rf opensim-$OPENSIMMAJOR.$OPENSIMMINOR
echo "Produced binary tarball ready for distribution."

