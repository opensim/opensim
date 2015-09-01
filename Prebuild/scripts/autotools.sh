#!/bin/sh
RUNTIME=`which mono`

SCRIPTDIR=`dirname $0`
${RUNTIME} ${SCRIPTDIR}/../Prebuild.exe /target autotools /file ${SCRIPTDIR}/../prebuild.xml /build NET_2_0
