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
				simworld.Entities[UUID].position += simworld.Entities[UUID].velocity;
				simworld.Entities[UUID].position.Z = simworld.LandMap[(int)simworld.Entities[UUID].position.Y * 256 + (int)simworld.Entities[UUID].position.X]+1;
				if(simworld.Entities[UUID].position.X<0) simworld.Entities[UUID].position.X=0;
				if(simworld.Entities[UUID].position.Y<0) simworld.Entities[UUID].position.Y=0;
				if(simworld.Entities[UUID].position.X>255) simworld.Entities[UUID].position.X=255;
				if(simworld.Entities[UUID].position.Y>255) simworld.Entities[UUID].position.Y=255;
            }
	}
    }
}
