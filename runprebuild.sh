#!/bin/sh

case "$1" in

 'clean')
    dotnet bin/prebuild.dll /file prebuild.xml /clean

  ;;


  'autoclean')

    echo y|dotnet bin/prebuild.dll /file prebuild.xml /clean

  ;;



  *)

    dotnet bin/prebuild.dll /target vs2022 /targetframework net6_0 /excludedir = "obj | bin" /file prebuild.xml

  ;;

esac
