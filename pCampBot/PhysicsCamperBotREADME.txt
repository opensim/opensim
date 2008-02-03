This is the PhysicsCamperbot libslBot tester.

This is designed to be run in standalone mode with authorize accounts turned off as a way to stress test the simulator.
It creates <N> clients that log in, randomly jump/walk around, and say excuses from the BOFH

**Warning:** Using this bot on a public grid could get you banned perminantly, so just say No! to greifing!

-----Setup -----
Run the prebuilds for it IN THIS FOLDER and then open the created solution and compile it.  It will end up in the regular opensim bin folder.

-----Running the bot----

windows: pCampBot.exe -botcount <N> -loginuri <URI>
*nix: mono pCampBot.exe -botcount <N> -loginuri <URI>

The names it produces are random by default, however, you can specify either a firstname or a lastname in the command line also.
ex: pCampBot.exe -botcount <N> -loginuri <URI> -lastname <lastname>

If you specify both a firstname *and* a lastname, you'll likely run into trouble unless you're only running a single bot.  In that case, there's also a password option.

pCampBot.exe -botcount 1 -loginuri http://somegrid.com:8002 -firstname SomeDude -lastname SomeDude -password GobbleDeGook
