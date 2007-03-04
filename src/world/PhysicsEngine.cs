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
                        if((simworld.Entities[UUID].position.X>0) & (simworld.Entities[UUID].position.X<256) & (simworld.Entities[UUID].position.Y>1) & (simworld.Entities[UUID].position.Y<256)) {
				simworld.Entities[UUID].position += simworld.Entities[UUID].velocity;
				simworld.Entities[UUID].position.Z = simworld.LandMap[(int)simworld.Entities[UUID].position.Y * 256 + (int)simworld.Entities[UUID].position.X]+1;
			} else {
				simworld.Entities[UUID].velocity = new LLVector3(0f,0f,0f);
			}
            }
	}
    }
}
