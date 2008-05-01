#!/bin/sh

# This script will build LaunchSLClient.app from the .exe, .dll's, and
# other necessary files.
#
# This should be run from the bin directory.

APP_NAME="LaunchSLClient"

# Note that proper form is to copy Frameworks to
# *.app/Contents/Frameworks, but because @executable_path resolves to
# [...]/Resources/bin, and the libraries reference
# @executable_path/../Frameworks, we put frameworks in
# Contents/Resources instead.
FRAMEWORKS_PATH="${APP_NAME}.app/Contents/Resources/Frameworks"

if [ ! -e ${APP_NAME}.exe ]; then
    echo "Error: Could not find ${APP_NAME}.exe." >& 2
    echo "Have you built it, and are you currently in the bin directory?" >& 2
    exit 1
fi

CMDFLAGS="-m console -n ${APP_NAME} -a ${APP_NAME}.exe"

REFERENCES="-r /Library/Frameworks/Mono.framework/Versions/Current/lib/ \
    -r Nini.dll \
    -r ${APP_NAME}.ini"

if [ -f ${APP_NAME}.icns ]; then
    CMDFLAGS="${CMDFLAGS} -i ${APP_NAME}.icns"
else
    echo "Warning: no icon file found.  Will use default application icon." >&2
fi

if [ -d ${APP_NAME}.app ]; then rm -rf ${APP_NAME}.app; fi
macpack ${REFERENCES} ${CMDFLAGS}

mkdir -p ${FRAMEWORKS_PATH}
