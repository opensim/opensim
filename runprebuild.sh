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
    echo "dotnet build -c Release OpenSim.sln" > compile.sh
    chmod +x compile.sh
	cp bin/System.Drawing.Common.dll.linux bin/System.Drawing.Common.dll

  ;;

esac
