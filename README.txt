Welcome to OpenSim!

Version 0.6.x
 
== OVERVIEW ==

OpenSim is a BSD Licensed Open Source project to develop a functioning
virtual worlds server platform capable of supporting multiple clients
and servers in a heterogeneous grid structure. OpenSim is written in
C#, and can run under Mono or the Microsoft .NET runtimes.

This is considered an alpha release.  Some stuff works, a lot doesn't.
If it breaks, you get to keep *both* pieces.

== Installation on Windows ==

Prereqs:

 * Load OpenSim.sln into Visual Studio .NET and build the solution.
 * chdir bin
 * OpenSim.exe

See configuring OpenSim

== Installation on Linux ==

Prereqs:
 * Mono >= 2.4.2
 * Nant >= 0.86beta
 * sqlite3 or mysql 5.x (you'll need a back end database)

From the distribution type:
 * ./runprebuild.sh
 * nant
 * cd bin
 * mono ./OpenSim.exe

See configuring OpenSim

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

== Connecting to your OpenSim ==

By default your sim will be running on http://127.0.0.1:9000.  To use
your OpenSim add -loginuri http://127.0.0.1:9000 to your second life
client (running on the same machine as your OpenSim).

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

