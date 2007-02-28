using System;
using libsecondlife;
using libsecondlife.Packets;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.world
{
    public class World
    {
        public Dictionary<libsecondlife.LLUUID, Entity> Entities;
        public float[] LandMap;
        public ScriptEngine Scripts;
	public TerrainDecode terrainengine = new TerrainDecode();

	
        public World()
        {
		Console.WriteLine("World.cs - creating new entitities instance");				
		Entities = new Dictionary<libsecondlife.LLUUID, Entity>();

		// We need a 16x16 array of 16m2 surface patches for a 256m2 sim
		Console.WriteLine("World.cs - creating LandMap");
		terrainengine = new TerrainDecode();
                LandMap = new float[65536];
                for(int i =0; i < 65536; i++) {
                       LandMap[i] =  30.4989f;
                }
  
 
		Console.WriteLine("World.cs - Creating script engine instance");
		// Initialise this only after the world has loaded
		Scripts = new ScriptEngine(this);
        }

        public void Update()
        {
            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
                Entities[UUID].update();
            }
        }

	public void SendLayerData(OpenSimClient RemoteClient) {
		for(int i=1; i<16; i++) {
			Packet layerpack=this.terrainengine.CreateLayerPacket(LandMap, i,1,1,16);
			RemoteClient.OutPacket(layerpack);
		}
	}

	public void AddViewerAgent(OpenSimClient AgentClient) {
		Console.WriteLine("World.cs:AddViewerAgent() - Creating new avatar for remote viewer agent");
		Avatar NewAvatar = new Avatar(AgentClient);
		Console.WriteLine("World.cs:AddViewerAgent() - Adding new avatar to world");
		this.Entities.Add(AgentClient.AgentID, NewAvatar);
		Console.WriteLine("World.cs:AddViewerAgent() - Starting RegionHandshake ");
		NewAvatar.SendRegionHandshake(this);
		this.Update();		// will work for now, but needs to be optimised so we don't update everything in the sim for each new user
	}

        public bool Backup() {
            /* TODO: Save the current world entities state. */

            return false;
        }
    }
}
