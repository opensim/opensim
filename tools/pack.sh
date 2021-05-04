#!/bin/bash

# Build a tarball for the build, excluding various development directories.  This command should be run in the top 
# level of the source tree and passed a name to be used for the release. This name is used to generate the tar file
# and is currently placed in the directory above where the command is run.

# saner programming env: these switches turn some bugs into errors
set -o errexit -o pipefail -o noclobber -o nounset

! getopt --test > /dev/null
if [[ ${PIPESTATUS[0]} -ne 4 ]]; then
    echo "I’m sorry, `getopt --test` failed in this environment."
    exit 1
fi

OPTIONS=rvd
LONGOPTS=release,debug,verbose

# -use ! and PIPESTATUS to get exit code with errexit set
# -temporarily store output to be able to check for errors
# -activate quoting/enhanced mode (e.g. by writing out “--options”)
# -pass arguments only via   -- "$@"   to separate them correctly
! PARSED=$(getopt --options=$OPTIONS --longoptions=$LONGOPTS --name "$0" -- "$@")
if [[ ${PIPESTATUS[0]} -ne 0 ]]; then
    # e.g. return value is 1
    #  then getopt has complained about wrong arguments to stdout
    exit 2
fi

# read getopt’s output this way to handle the quoting right:
eval set -- "$PARSED"

r=y
v=n
d=n

# now enjoy the options in order and nicely split until we see --
while true; do
    case "$1" in
        -v|--verbose)
            v=y
            shift
            ;;
        -d|--debug)
            d=y
            shift
            ;;
        -r|--release)
            r=y
            shift
            ;;
        --)
            shift
            break
            ;;
        *)
            echo "Programming error"
            exit 3
            ;;
    esac
done

# handle non-option arguments
if [[ $# -ne 1 ]]; then
    echo "$0: A single release tag name is required."
    exit 4
fi

TAGNAME=$1

CURDIR=`pwd`

#
# Options handled.  Start things up.
if [ "$d" = "y" ]; then
    RELEASE=Debug
    TYPETAG=dbg
else
    RELEASE=Release
    TYPETAG=rel
fi

BUILDDIR="$CURDIR/build/${RELEASE}"

if [ ! -d $BUILDDIR ]; then
    echo "Build directory $BUILDDIR does not exist!"
    exit 1
fi

TARGET="$CURDIR/../opensim-${TYPETAG}-${TAGNAME}"
EXCLUDES="--exclude='./.git' --exclude='./.nant' --exclude='./.vs' --exclude='./obj' --exclude='./config' --exclude='./logs' --exclude='./ScriptEngines'"

(cd $BUILDDIR && tar ${EXCLUDES} -czvf "${TARGET}.tar.gz" .)

exit 0
