@rem Generates a solution (.sln) and a set of project files (.csproj, .vbproj, etc.)
@rem for Microsoft Visual Studio .NET 2010
cd ..
Prebuild.exe /target vs2010 /file prebuild.xml /pause
