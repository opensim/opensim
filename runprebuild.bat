@echo OFF

bin\Prebuild.exe /target vs2015

setlocal ENABLEEXTENSIONS
set VALUE_NAME=MSBuildToolsPath

rem Try to find VS2019
for %%e in (Enterprise Professional Community) do (
    if exist "%PROGRAMS%\Microsoft Visual Studio\2019\%%e\MSBuild\Current\Bin\MSBuild.exe" (

        set ValueValue="%PROGRAMS%\Microsoft Visual Studio\2019\%%e\MSBuild\Current\Bin\"
		goto :found
    )
)

rem try find vs2017
if "%PROCESSOR_ARCHITECTURE%"=="x86" set PROGRAMS=%ProgramFiles%
if defined ProgramFiles(x86) set PROGRAMS=%ProgramFiles(x86)%

for %%e in (Enterprise Professional Community) do (
    if exist "%PROGRAMS%\Microsoft Visual Studio\2017\%%e\MSBuild\15.0\Bin\MSBuild.exe" (

        set ValueValue="%PROGRAMS%\Microsoft Visual Studio\2017\%%e\MSBuild\15.0\Bin\"
		goto :found
    )
)

rem We have to use grep or find to locate the correct line, because reg query spits
rem out 4 lines before Windows 7 but 2 lines after Windows 7.
rem We use grep if it's on the path; otherwise we use the built-in find command
rem from Windows. (We must use grep on Cygwin because it overrides the "find" command.)

for %%X in (grep.exe) do (set FOUNDGREP=%%~$PATH:X)
if defined FOUNDGREP (
  set FINDCMD=grep
) else (
  set FINDCMD=find
)

rem try vs2015
FOR /F "usebackq tokens=1-3" %%A IN (`REG QUERY "HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\MSBuild\ToolsVersions\14.0" /v %VALUE_NAME% 2^>nul ^| %FINDCMD% "%VALUE_NAME%"`) DO (
	set ValueValue=%%C
	goto :found
)

@echo msbuild for at least VS2015 not found, please install a (Community) edition of VS2017 or VS2015
@echo Not creating compile.bat
if exist "compile.bat" (
	del compile.bat
	)
goto :done

:found
    @echo Found msbuild at %ValueValue%
    @echo Creating compile.bat
    @echo %ValueValue%\msbuild opensim.sln > compile.bat

:done