#!/bin/sh

# This script will build LaunchSLClient.app from the .exe, .dll's, and
# other necessary files.
#
# This should be run from the bin directory.

APP_NAME="LaunchSLClient"
SOURCE_PATH="../OpenSim/Tools/${APP_NAME}"

ASSEMBLIES="mscorlib.dll \
    System.Windows.Forms.dll \
    System.Drawing.dll \
    System.Configuration.dll \
    System.Xml.dll \
    System.Security.dll \
    Mono.Security.dll \
    System.Data.dll \
    Mono.Data.Tds.dll \
    System.Transactions.dll \
    System.EnterpriseServices.dll \
    Mono.Mozilla.dll \
    Mono.Posix.dll \
    Accessibility.dll"

if [ ! -e ${APP_NAME}.exe ]; then
    echo "Error: Could not find ${APP_NAME}.exe." >& 2
    echo "Have you built it, and are you currently in the bin directory?" >& 2
    exit 1
fi

mkbundle2 -z -o ${APP_NAME} ${APP_NAME}.exe ${ASSEMBLIES} || exit 1

if [ -d ${APP_NAME}.app ]; then rm -rf ${APP_NAME}.app; fi
cp -r ${SOURCE_PATH}/${APP_NAME}.app.skel ${APP_NAME}.app

# mkbundle doesn't seem to recognize the -L option, so we can't include Nini.dll in the bundling
cp Nini.dll ${APP_NAME}.app/Contents/Resources

cp ${APP_NAME} ${APP_NAME}.ini ${APP_NAME}.app/Contents/Resources
