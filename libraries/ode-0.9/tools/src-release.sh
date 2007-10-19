#!/bin/sh
###################################################################
# ODE Source Code Release Script
# Originally written by Jason Perkins (starkos@gmail.com)
#
# Prerequisites:
#  svn, zip
###################################################################

# Check arguments
if [ $# -ne 2 ]; then
  echo 1>&2 "Usage: $0 version_number branch_name"
  exit 1
fi


###################################################################
# Pre-build checklist
###################################################################

echo "" 
echo "STARTING PREBUILD CHECKLIST, PRESS ^^C TO ABORT."
echo ""
echo "Is the version number '$1' correct?"
read line
echo ""
echo "Have you created a release branch named '$2' in SVN?"
read line
echo ""
echo "Have you run all of the tests?"
read line
echo ""
echo "Is the Changelog up to date?"
read line
echo ""
echo "Okay, ready to build the source code package for version $1!"
read line


###################################################################
# Retrieve source code
###################################################################

echo ""
echo "RETRIEVING SOURCE CODE FROM REPOSITORY..."
echo ""
f
svn export https://opende.svn.sourceforge.net/svnroot/opende/branches/$2 ode-$1


###################################################################
# Prepare source code
###################################################################

echo ""
echo "PREPARING SOURCE TREE..."
echo ""

cd ode-$1
chmod 755 autogen.sh
./autogen.sh
rm -rf autom4te.cache

cp build/config-default.h include/ode/config.h

cd ode/doc
doxygen

cd ../../..


###################################################################
# Package source code
###################################################################

echo ""
echo "PACKAGING SOURCE CODE..."
echo ""

zip -r9 ode-src-$1.zip ode-$1/*


###################################################################
# Clean up
###################################################################

echo ""
echo "CLEANING UP..."
echo ""

rm -rf ode-$1


#####################################################################
# Send the files to SourceForge
#####################################################################

echo ""
echo "Upload packages to SourceForge?"
read line
if [ $line = "y" ]; then
	echo "Uploading to SourceForge..."
	ftp -n upload.sourceforge.net < ftp_src_script
fi
