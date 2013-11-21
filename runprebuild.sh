#!/bin/sh

case "$1" in

  'clean')

    mono bin/Prebuild.exe /clean

  ;;


  'autoclean')

    echo y|mono bin/Prebuild.exe /clean

  ;;


  'vs2010')
  
    mono bin/Prebuild.exe /target vs2010
  
  ;;

  *)

    mono bin/Prebuild.exe /target nant
    mono bin/Prebuild.exe /target vs2010

  ;;

esac
