@echo OFF

bin\Prebuild.exe /target nant
bin\Prebuild.exe /target vs2010

setlocal ENABLEEXTENSIONS
set KEY_NAME="HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MSBuild\ToolsVersions\4.0"
set VALUE_NAME=MSBuildToolsPath

rem We have to use find here as req query spits out 4 lines before Windows 7
rem But 2 lines after Windows 7.  Unfortunately, this screws up cygwin
rem as it uses its own find command.  This could be fixed but it could be
rem complex to find the location of find on all windows systems
FOR /F "usebackq tokens=1-3" %%A IN (`REG QUERY %KEY_NAME% /v %VALUE_NAME% 2^>nul ^| FIND "%VALUE_NAME%"`) DO (
    set ValueName=%%A
    set ValueType=%%B
    set ValueValue=%%C
)

if defined ValueName (
    @echo Found msbuild path registry entry
    @echo Value Name = %ValueName%
    @echo Value Type = %ValueType%
    @echo Value Value = %ValueValue%
    @echo Creating compile.bat
    @echo %ValueValue%\msbuild opensim.sln > compile.bat
) else (
    @echo %KEY_NAME%\%VALUE_NAME% not found.
    @echo Not creating compile.bat
)
