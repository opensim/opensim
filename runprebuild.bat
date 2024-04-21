@echo OFF

copy bin\System.Drawing.Common.dll.win bin\System.Drawing.Common.dll

dotnet bin\prebuild.dll /target vs2022 /targetframework net8_0 /excludedir = "obj | bin" /file prebuild.xml

    @echo Creating compile.bat
rem To compile in release mode
    @echo dotnet build --configuration Release OpenSim.sln > compile.bat
rem To compile in debug mode comment line (add rem to start) above and uncomment next (remove rem)
rem    @echo dotnet build --configuration Debug OpenSim.sln > compile.bat
:done


if exist "bin\addin-db-002" (
	del /F/Q/S bin\addin-db-002 > NUL
	rmdir /Q/S bin\addin-db-002
	)
if exist "bin\addin-db-004" (
	del /F/Q/S bin\addin-db-004 > NUL
	rmdir /Q/S bin\addin-db-004
	)	