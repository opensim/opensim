Welcome to OpenSim!

# LibLSLCC Integration

This distribution of OpenSim integrates the LibLSLCC Compiler and moves
OpenSim over to using .NET Framework 4.5 instead of 4.0

See:

	https://github.com/EriHoss/LibLSLCC

	Or
	
	https://gitlab.com/erihoss/LibLSLCC


Settings that determine the compiler Assembly (dll) and class have been added under the
[XEngine] configuration section in OpenSim.ini.  See OpenSim.ini in the 'bin' folder where the OpenSim
executable resides.

The included OpenSim.ini.example and OpenSim.ini have the default compiler assembly
set to the "LibLSLCCCompiler.dll" so that LibLSLCC is used as the default compiler.


Under [XEngine] in OpenSim.ini you will find these new settings:


	;==========================================
	;LibLSLCC Patch Settings
	;==========================================

	;The name of the class that implements the compiler
	;CompilerClass = "OpenSim.Region.ScriptEngine.Shared.CodeTools.Compiler"
	CompilerClass = "OpenSim.Region.ScriptEngine.Shared.LibLSLCCCompiler.Compiler"

	;The assembly to load the compiler implementation from
	;CompilerAssembly = "OpenSim.Region.ScriptEngine.Shared.CodeTools.dll"
	CompilerAssembly = "OpenSim.Region.ScriptEngine.Shared.LibLSLCCCompiler.dll"



When you clone this repository OpenSim is pre-configured in standalone mode using the SQLite storage backend.


OpenSim.ini and bin/config-include/StandaloneCommon.ini have been left in the repository
for this distribution to make running a test server on your PC easier.  If you just want
to run a test server on localhost, you should not have to modify any configuration after building,
just run OpenSim.exe and set up your region and region owner by answering the questions OpenSim
asks you in the command prompt.



# Overview

OpenSim is a BSD Licensed Open Source project to develop a functioning
virtual worlds server platform capable of supporting multiple clients
and servers in a heterogeneous grid structure. OpenSim is written in
C#, and can run under Mono or the Microsoft .NET runtimes.

This is considered an alpha release.  Some stuff works, a lot doesn't.
If it breaks, you get to keep *both* pieces.

# Compiling OpenSim

Please see BUILDING.md if you downloaded a source distribution and 
need to build OpenSim before running it.

# Running OpenSim on Windows

You will need .NET 4.5 installed to run this distribution of OpenSimulator.

We recommend that you run OpenSim from a command prompt on Windows in order
to capture any errors.

To run OpenSim from a command prompt

 * cd to the bin/ directory where you unpacked OpenSim
 * run OpenSim.exe

Now see the "Configuring OpenSim" section

# Running OpenSim on Linux

You will need Mono >= 2.10.8.1 to run OpenSimulator.  On some Linux distributions you
may need to install additional packages.  See http://opensimulator.org/wiki/Dependencies
for more information.

To run OpenSim, from the unpacked distribution type:

 * cd bin
 * mono OpenSim.exe

Now see the "Configuring OpenSim" section

# Configuring OpenSim

When OpenSim starts for the first time, you will be prompted with a
series of questions that look something like:

	[09-17 03:54:40] DEFAULT REGION CONFIG: Simulator Name [OpenSim Test]:

For all the options except simulator name, you can safely hit enter to accept
the default if you want to connect using a client on the same machine or over
your local network.

You will then be asked "Do you wish to join an existing estate?".  If you're
starting OpenSim for the first time then answer no (which is the default) and
provide an estate name.

Shortly afterwards, you will then be asked to enter an estate owner first name,
last name, password and e-mail (which can be left blank).  Do not forget these
details, since initially only this account will be able to manage your region
in-world.  You can also use these details to perform your first login.

Once you are presented with a prompt that looks like:

	Region (My region name) #

You have successfully started OpenSim.

If you want to create another user account to login rather than the estate
account, then type "create user" on the OpenSim console and follow the prompts.

Helpful resources:
 * http://opensimulator.org/wiki/Configuration
 * http://opensimulator.org/wiki/Configuring_Regions

# Connecting to your OpenSim

By default your sim will be available for login on port 9000.  You can login by
adding -loginuri http://127.0.0.1:9000 to the command that starts Second Life
(e.g. in the Target: box of the client icon properties on Windows).  You can
also login using the network IP address of the machine running OpenSim (e.g.
http://192.168.1.2:9000)

To login, use the avatar details that you gave for your estate ownership or the
one you set up using the "create user" command.

# Bug reports

In the very likely event of bugs biting you (err, your OpenSim) we
encourage you to see whether the problem has already been reported on
the [OpenSim mantis system](http://opensimulator.org/mantis/main_page.php).

If your bug has already been reported, you might want to add to the
bug description and supply additional information.

If your bug has not been reported yet, file a bug report ("opening a
mantis"). Useful information to include:
 * description of what went wrong
 * stack trace
 * OpenSim.log (attach as file)
 * OpenSim.ini (attach as file)
 * if running under mono: run OpenSim.exe with the "--debug" flag:

       mono --debug OpenSim.exe

# More Information on OpenSim

More extensive information on building, running, and configuring
OpenSim, as well as how to report bugs, and participate in the OpenSim
project can always be found at http://opensimulator.org.

Thanks for trying OpenSim, we hope it is a pleasant experience.


