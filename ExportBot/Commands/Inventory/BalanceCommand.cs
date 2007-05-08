using System;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class BalanceCommand: Command
    {
        public BalanceCommand(TestClient testClient)
		{
			Name = "balance";
			Description = "Shows the amount of L$.";
		}

        public override string Execute(string[] args, LLUUID fromAgentID)
		{
			return Client.ToString() + " has L$: " + Client.Self.Balance;
		}
    }
}
