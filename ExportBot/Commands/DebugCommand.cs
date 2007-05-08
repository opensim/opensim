using System;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class DebugCommand : Command
    {
        public DebugCommand(TestClient testClient)
        {
            Name = "debug";
            Description = "Turn debug messages on or off. Usage: debug [on/off]";
        }

        public override string Execute(string[] args, LLUUID fromAgentID)
        {
            if (args.Length != 1)
                return "Usage: debug [on/off]";

            if (args[0].ToLower() == "on")
            {
                Client.Settings.DEBUG = true;
                return "Debug logging is on";
            }
            else if (args[0].ToLower() == "off")
            {
                Client.Settings.DEBUG = false;
                return "Debug logging is off";
            }
            else
            {
                return "Usage: debug [on/off]";
            }
        }
    }
}
