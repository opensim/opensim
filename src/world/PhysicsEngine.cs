using System;
using System.Threading;
using libsecondlife;
using libsecondlife.Packets;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.world
{
    public class PhysicsEngine
    {

	public PhysicsEngine() {
	}
	
	public void Startup() {
		Console.WriteLine("PhysicsEngine.cs:Startup() - DOING NOTHING, DUMMY FUNCTION!");
	}
 
	public void DoStuff(World simworld) {
         foreach (libsecondlife.LLUUID UUID in simworld.Entities.Keys)
            {
                if( true /* simworld.Entities[UUID].needupdate */) { // FIXME!
                        simworld.Entities[UUID].position += simworld.Entities[UUID].velocity;
			Console.WriteLine("Moving "+UUID.ToString()+ " to "+ simworld.Entities[UUID].position.ToString());
                }

            }
	}
    }
}
