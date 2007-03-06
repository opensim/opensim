using System;
using System.Threading;
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
	public uint _localNumber=0;
	public PhysicsEngine physics;
	
        private Random Rand = new Random();

        public World()
        {
		OpenSim_Main.localcons.WriteLine("World.cs - creating new entitities instance");				
		Entities = new Dictionary<libsecondlife.LLUUID, Entity>();

		OpenSim_Main.localcons.WriteLine("World.cs - creating LandMap");
		terrainengine = new TerrainDecode();
                LandMap = new float[65536];
		for(int i =0; i < 65536; i++) {
			LandMap[i] =  21.4989f;
		}
 
        }

	public void InitLoop() {
		OpenSim_Main.localcons.WriteLine("World.cs:StartLoop() - Initialising physics");
		this.physics = new PhysicsEngine();
		physics.Startup();
	}
	
	public void DoStuff() {
		lock(this) {
			physics.DoStuff(this);
			this.Update();
		}
	}

	public void Update() {
            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
		if(Entities[UUID].needupdate) {
			Entities[UUID].update();
		
			if(Entities[UUID] is Avatar) {
			Avatar avatar=(Avatar)Entities[UUID];
			if((avatar.oldpos!=avatar.position) || (avatar.oldvel!=avatar.velocity) || avatar.walking) {
				ImprovedTerseObjectUpdatePacket.ObjectDataBlock terseBlock = Entities[UUID].CreateTerseBlock();
				foreach(OpenSimClient client in OpenSim_Main.sim.ClientThreads.Values) {
					ImprovedTerseObjectUpdatePacket terse = new ImprovedTerseObjectUpdatePacket();
					terse.RegionData.RegionHandle = OpenSim_Main.cfg.RegionHandle; // FIXME
					terse.RegionData.TimeDilation = 0;
					terse.ObjectData = new ImprovedTerseObjectUpdatePacket.ObjectDataBlock[1];
					terse.ObjectData[0] = terseBlock;
					client.OutPacket(terse);
				}
			}}
		}
            }
        }

	public void SendLayerData(OpenSimClient RemoteClient) {
		for(int x=0; x<16; x=x+4) for(int y=0; y<16; y++){
			Packet layerpack=this.terrainengine.CreateLayerPacket(LandMap, x,y,x+4,y+1);
			RemoteClient.OutPacket(layerpack);
		}
	}

	public void AddViewerAgent(OpenSimClient AgentClient) {
		OpenSim_Main.localcons.WriteLine("World.cs:AddViewerAgent() - Creating new avatar for remote viewer agent");
		Avatar NewAvatar = new Avatar(AgentClient);
		OpenSim_Main.localcons.WriteLine("World.cs:AddViewerAgent() - Adding new avatar to world");
		this.Entities.Add(AgentClient.AgentID, NewAvatar);
		OpenSim_Main.localcons.WriteLine("World.cs:AddViewerAgent() - Starting RegionHandshake ");
		NewAvatar.SendRegionHandshake(this);
		this.Update();		// will work for now, but needs to be optimised so we don't update everything in the sim for each new user
	}

        public bool Backup() {
            /* TODO: Save the current world entities state. */

            return false;
        }
    }
}
