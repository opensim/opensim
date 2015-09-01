This is the PhysicsCamperbot libslBot tester.

This is designed to stress test the simulator.  It creates <N>
clients that log in, randomly jump/walk around, and can say excuses from
the BOFH.

Bots must have accounts already created.  Each bot will have the same firstname and password
but their lastname will be appended with _<bot-number> starting from 0.  So if you have two bots called ima bot, their
first names will be ima_bot_0 and ima_bot_1.

*** WARNING ***
Using this bot on a public grid could get you banned permanently, so
just say No! to griefing!

----- Setup -----
Linux: To build, in the main opensim directory, run:
  ./runprebuild.sh
  nant

Windows: Run the prebuild.bat in the main opensim directory and then
open the created solution and compile it.

pCampBot.exe will end up in the regular opensim/bin folder

----- Running the bot -----

windows: pCampBot.exe -botcount <N> -loginuri <URI> -firstname <bot-first-name> -lastname <bot-last-name-stem> -password <bot-password>
*nix: mono pCampBot.exe -botcount <N> -loginuri <URI> -firstname <bot-first-name> -lastname <bot-last-name-stem> -password <bot-password>

----- Commands -----

The bot has console commands:
  help       - lists the console commands and what they do
  shutdown   - gracefully shuts down the bots
  quit       - forcefully shuts things down leaving stuff unclean
