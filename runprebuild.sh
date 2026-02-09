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
    grep -q '<PackageReference Include=\"System.Runtime.Caching\"' OpenSim/Tests/Common/OpenSim.Tests.Common.csproj || sed -i '/<ItemGroup>/a \    <PackageReference Include="System.Runtime.Caching" Version="8.0.0" />' OpenSim/Tests/Common/OpenSim.Tests.Common.csproj
    echo "dotnet build -c Release OpenSim.sln" > compile.sh
    chmod +x compile.sh

  ;;

esac
