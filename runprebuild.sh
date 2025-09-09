#!/bin/sh

case "$1" in

 'clean')
    dotnet bin/prebuild.dll /file prebuild.xml /clean

  ;;


  'autoclean')

    echo y|dotnet bin/prebuild.dll /file prebuild.xml /clean

  ;;



  *)

    cp bin/System.Drawing.Common.dll.linux bin/System.Drawing.Common.dll
    dotnet bin/prebuild.dll /target vs2022 /targetframework net8_0 /excludedir = "obj | bin" /file prebuild.xml
    sed -i 's|<TargetFramework>net8.0</TargetFramework>|<TargetFramework>net8.0-windows</TargetFramework>|' OpenSim/Tools/GuiControlPanel/OpenSim.Tools.GuiControlPanel.csproj
    sed -i '/<PropertyGroup>/a \    <UseWindowsForms>true</UseWindowsForms>' OpenSim/Tools/GuiControlPanel/OpenSim.Tools.GuiControlPanel.csproj
    sed -i '/<PropertyGroup>/a \    <EnableWindowsTargeting>true</EnableWindowsTargeting>' OpenSim/Tools/GuiControlPanel/OpenSim.Tools.GuiControlPanel.csproj
    echo "dotnet build -c Release OpenSim.sln" > compile.sh
    chmod +x compile.sh

  ;;

esac
