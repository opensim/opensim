

rem Prebuild.exe /target VS2010 

@ECHO OFF

echo ==========================================
echo ==== WhiteCore Prebuild Configuration ====
echo ==========================================
echo.
echo If you wish to customize the configuration, re-run with the switch '-p'
echo   e.g.   runprebuild -p
echo.

rem ## Default "configuration" choice ((r)elease, (d)ebug)
set configuration=d

rem ## Default Visual Studio edition
set vstudio=2010

rem ## Default Framework
set framework=4_0

rem ## Default architecture (86 (for 32bit), 64)
:CheckArch
set bits=AnyCPU
rem if exist "%PROGRAMFILES(X86)%" (set bits=x64)
rem if %bits% == x64 (
rem 	echo Found 64bit architecture
rem )
rem if %bits% == x86 (
rem 	echo Found 32 bit architecture
rem )

rem ## Determine native framework
:CheckOS
set framework=4_5
for /f "tokens=4-5 delims=. " %%i in ('ver') do set VERSION=%%i.%%j
if %version% == 10.0 (
	set framework=4_5
	echo Windows 10
)
if %version% == 6.3 (
	set framework=4_5
	echo Windows 8.1 or Server 2012 R2
)
if %version% == 6.2 (
	set framework=4_5
	echo Windows 8 or Server 2012
)
if %version% == 6.1 (
	set framework=4_0
	echo Windows 7 or Server 2008 R2
)
if %version% == 6.0 (
	set framework=3_5
	echo hmmm... Windows Vista or Server 2008
)
if %version% == 5.2 (
	set framework=3_5
	echo hmmm... Windows XP x64 or  Server 2003
)
if %version% == 5.1 (
	set framework=3_5
	echo hmmm... Windows XP
)


rem ## If not requested, skip the prompting
if "%1" =="" goto final
if %1 == -p goto prompt
if %1 == --prompt goto prompt
goto final

:prompt
echo I will now ask you four questions regarding your build.
echo However, if you wish to build for:
echo		%bits% Architecture
echo		.NET %framework%
echo		Visual Studio %vstudio%

echo.
echo Simply tap [ENTER] three times.
echo.
echo Note that you can change these defaults by opening this
echo batch file in a text editor.
echo.

:bits
set /p bits="Choose your architecture (x86, x64, AnyCPU) [%bits%]: "
if %bits%==86 goto configuration
if %bits%==x86 goto configuration
if %bits%==64 goto configuration
if %bits%==x64 goto configuration
if %bits%==AnyCPU goto configuration
if %bits%==anycpu goto configuration
echo "%bits%" isn't a valid choice!
goto bits

:configuration
set /p configuration="Choose your configuration ((r)elease or (d)ebug)? [%configuration%]: "
if %configuration%==r goto framework
if %configuration%==d goto framework
if %configuration%==release goto framework
if %configuration%==debug goto framework
echo "%configuration%" isn't a valid choice!
goto configuration

:framework
set /p framework="Choose your .NET framework (4_0 or 4_5)? [%framework%]: "
if %framework%==4_0 goto final
if %framework%==4_5 goto final
echo "%framework%" isn't a valid choice!
goto framework

:final
echo.
echo Configuring for %bits% architecture using %framework% .NET framework
echo.
echo.


if %framework%==4_5 set %vstudio%=2012

echo Calling Prebuild for target %vstudio% with framework %framework%...
Prebuild.exe /target vs%vstudio% /targetframework v%framework% /conditionals ISWIN;NET_%framework%

