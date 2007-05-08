using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using libsecondlife.Packets;

namespace libsecondlife.TestClient
{
    public class TouchCommand: Command
    {
        public TouchCommand(TestClient testClient)
		{
			Name = "touch";
			Description = "Attempt to touch a prim with specified UUID";
		}
		
        public override string Execute(string[] args, LLUUID fromAgentID)
		{
		    Primitive target = null;

		    lock (Client.SimPrims)
		    {
                if (Client.SimPrims.ContainsKey(Client.Network.CurrentSim))
                {
                    foreach (Primitive p in Client.SimPrims[Client.Network.CurrentSim].Values)
                    {
                        if (args.Length == 0)
                            return "You must specify a UUID of the prim.";

                        try
                        {
                            if (p.ID == args[0])
                                target = p;
                        }
                        catch
                        {
                            // handle exception
                            return "Sorry, I don't think " + args[0] + " is a valid UUID.  I'm unable to touch it.";
                        }
                    }
                }
		    }

            if (target != null)
            {
                Client.Self.Touch(target.LocalID);
                return "Touched prim " + target.ID + ".";
            }
            else
            {
                return "Couldn't find that prim.";
            }
		}
    }
}