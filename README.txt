Welcome to OpenSim!

== OVERVIEW ==

OpenSim is a BSD Licensed Open Source project to develop a functioning
virtual worlds server platform capable of supporting multiple clients
and servers in a heterogeneous grid structure. OpenSim is written in
C#, and can run under Mono or the Microsoft .NET runtimes.

This is considered an alpha release.  Some stuff works, a lot doesn't.
If it breaks, you get to keep *both* pieces.

== Compiling OpenSim ==

Please see BUILDING.txt if you downloaded a source distribution and 
need to build OpenSim before running it.

== Running OpenSim on Windows ==

We recommend that you run OpenSim from a command prompt on Windows in order
to capture any errors, though you can also run it by double-clicking
bin/OpenSim.exe

To run OpenSim from a command prompt

 * cd to the bin/ directory where you unpacked OpenSim
 * run OpenSim.exe

Now see the "Configuring OpenSim" section

== Running OpenSim on Linux ==

You will need Mono >= 2.4.2 to run OpenSim.  On some Linux distributions you
may need to install additional packages.  See http://opensimulator.org/wiki/Dependencies
for more information.

To run OpenSim, from the unpacked distribution type:

 * cd bin
 * mono ./OpenSim.exe

Now see the "Configuring OpenSim" section

== Configuring OpenSim ==

When OpenSim starts for the first time, you will be prompted with a
series of questions that look something like:

[09-17 03:54:40] DEFAULT REGION CONFIG: Simulator Name [OpenSim Test]:

At each of these you must provide you own value or just hit enter to
take the default (in this case "OpenSim Test").

YOUR SIM WILL NOT BE STARTED UNTIL YOU ANSWER ALL QUESTIONS

Once you are presented with a prompt that looks like:

  Region# :

You have successfully started OpenSim.

Before you can log in you will need to create a user account if you didn't already create
your user as the "Master Avatar" during the region configuration stage.  You can do
this by running the "create user" command on the OpenSim console.  This will
ask you a series of questions such as first name, last name and password.

Helpful resources:
 * http://opensimulator.org/wiki/Configuration
 * http://opensimulator.org/wiki/Configuring_Regions
 * http://opensimulator.org/wiki/Mysql-config

== Connecting to your OpenSim ==

By default your sim will be running on http://127.0.0.1:9000.  To use
your OpenSim add -loginuri http://127.0.0.1:9000 to your second life
client (running on the same machine as your OpenSim).  To login, use the
same avatar details that you gave to the "create user" console command.

== Bug reports ==

In the likely event of bugs biting you (err, your OpenSim) we
encourage you to see whether the problem has already been reported on
the OpenSim mantis system. You can find the OpenSim mantis system at

    http://opensimulator.org/mantis/main_page.php

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

== More Information on OpenSim ==

More extensive information on building, running, and configuring
OpenSim, as well as how to report bugs, and participate in the OpenSim
project can always be found at http://opensimulator.org.

Thanks for trying OpenSim, we hope it is a pleasant experience.
