#!/bin/sh

./prebuild /target makefile /file ../prebuild.xml /pause

if [ -f ../Makefile ]
then
    rm -rf ../Makefile
fi

mv ../Prebuild.make ../Makefile
