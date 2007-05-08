using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class PrimCountCommand: Command
    {
        public PrimCountCommand(TestClient testClient)
		{
			Name = "primcount";
			Description = "Shows the number of prims that have been received.";
		}

        public override string Execute(string[] args, LLUUID fromAgentID)
		{
            int count = 0;

            lock (Client.SimPrims)
            {
                foreach (Dictionary<uint, Primitive> prims in Client.SimPrims.Values)
                {
                    count += prims.Count;
                }
            }

			return count.ToString();
		}
    }
}
