using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class HelpCommand: Command
    {
        public HelpCommand(TestClient testClient)
		{
			Name = "help";
			Description = "Lists available commands.";
		}

        public override string Execute(string[] args, LLUUID fromAgentID)
		{
			StringBuilder result = new StringBuilder();
			result.AppendFormat("\n\nHELP\nClient accept teleport lures from master and group members.\n");
			foreach (Command c in Client.Commands.Values)
			{
				result.AppendFormat(" * {0} - {1}\n", c.Name, c.Description);
			}

            return result.ToString();
		}
    }
}
