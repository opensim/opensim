# OpenSimCurrencyServer-2020
For the new OpeSimulator 0.9.2.0.200+ DEV

DTL/NSL Money Server by Fumi.Iseki and NSL http://www.nsl.tuis.ac.jp , here is my test revision.

I have fixed or switched off all errors, warnings and messages.

Now you can experiment with new things without having old messages.

    This is currently being tested with:
    opensim-0.9.2.0 Dev - 579
    Status works.

## copy:

copy addon-modules to addon-modules

copy bin to bin

copy helper to web (www/html/helper) - OSGrid Version - mysqli is not yet working

## Building:

### Linux: (Ubuntu 18.04 test server)

    ./runprebuild.sh
    msbuild /p:Configuration=Release

### Windows: (Windows 10, Visual Studio 2019 Community)

    runprebuild.bat
    start Visual studio with OpenSim.sln 
    or run compile.bat
    
Config: Robust, MoneyServer and OpenSim.

Start: 1. Robust, 2. MoneyServer, 3. OpenSim regions.

INFO: On Windows and Visual Studio, the Money Server only starts when mysql is running and config is set.

## Todo:
If a message comes to the Console, the prompt is gone.

No color is displayed in the MoneyServer Console.

BuyLand, buyCurrency (Multi-currency support to OpenSim viewers)
