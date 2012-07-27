#!/bin/sh

case "$1" in

  'clean')

    mono bin/Prebuild.exe /clean

  ;;

  *)

    mono bin/Prebuild.exe /target nant
    mono bin/Prebuild.exe /target vs2008
  ;;

esac

