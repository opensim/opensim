#!/bin/sh

case "$1" in

  'clean')

    mono bin/Prebuild.exe /clean
  ;;


  'autoclean')

    echo y|mono bin/Prebuild.exe /clean

  ;;


  *)

    mono bin/Prebuild.exe /target nant
    mono bin/Prebuild.exe /target vs2015

  ;;

esac
    rm -fr bin/addin-db-002

