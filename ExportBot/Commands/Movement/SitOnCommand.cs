using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class SitOnCommand: Command
    {
        public SitOnCommand(TestClient testClient)
		{
			Name = "siton";
			Description = "Attempt to sit on a particular prim, with specified UUID";
		}
			
        public override string Execute(string[] args, LLUUID fromAgentID)
		{
		    LLObject targetSeat = null;

		    lock (Client.SimPrims)
		    {
                if (Client.SimPrims.ContainsKey(Client.Network.CurrentSim))
                {
                    foreach (LLObject p in Client.SimPrims[Client.Network.CurrentSim].Values)
                   {
                       try
                       {
                           if (p.ID == args[0])
                               targetSeat = p;
                       }
                       catch
                       {
                           // handle exception
                           return "Sorry, I don't think " + args[0] + " is a valid UUID.  I'm unable to sit there.";
                       }
                   }
                }
		    }

            if (targetSeat != null)
            {
                Client.Self.RequestSit(targetSeat.ID, LLVector3.Zero);
                Client.Self.Sit();

                return "Sat on prim " + targetSeat.ID + ".";
            }
            else
            {
                return "Couldn't find specified prim to sit on";
            }
		}
    }
}