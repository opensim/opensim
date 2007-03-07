using System;
using libsecondlife;
using libsecondlife.Packets;
using System.Collections.Generic;
using System.Text;
using System.IO;
using PhysicsSystem;

namespace OpenSim.world
{
    public class World
    {
    	public Dictionary<libsecondlife.LLUUID, Entity> Entities;
    	public float[] LandMap;
    	public ScriptEngine Scripts;
    	public uint _localNumber=0;
    	private PhysicsScene phyScene;
    	private float timeStep= 0.1f;
    	private libsecondlife.TerrainManager TerrainManager;
    	
    	private Random Rand = new Random();
    	private uint _primCount = 702000;

    	public World()
    	{
    		ServerConsole.MainConsole.Instance.WriteLine("World.cs - creating new entitities instance");
    		Entities = new Dictionary<libsecondlife.LLUUID, Entity>();

    		ServerConsole.MainConsole.Instance.WriteLine("World.cs - creating LandMap");
    		TerrainManager = new TerrainManager(new SecondLife());
    		Avatar.SetupTemplate("avatar-template.dat");
    	//	ServerConsole.MainConsole.Instance.WriteLine("World.cs - Creating script engine instance");
    		// Initialise this only after the world has loaded
    	//	Scripts = new ScriptEngine(this);
    	}
    	
    	public PhysicsScene PhysScene
    	{
    		set
    		{
    			this.phyScene = value;
    		}
    		get
    		{
    			return(this.phyScene);
    		}
    	}
    	
    	public void Update()
    	{

		if(this.phyScene.IsThreaded)
    		{
    			this.phyScene.GetResults();
    			
    		}
    		
    		foreach (libsecondlife.LLUUID UUID in Entities.Keys)
    		{
    			Entities[UUID].addForces();
    		}
    		
    		this.phyScene.Simulate(timeStep);
    		
    		foreach (libsecondlife.LLUUID UUID in Entities.Keys)
    		{
    			Entities[UUID].update();
    		}
    	}

    	public void SendLayerData(OpenSimClient RemoteClient) {
    		int[] patches = new int[4];

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x = x + 4)
                {
                    patches[0] = x + 0 + y * 16;
                    patches[1] = x + 1 + y * 16;
                    patches[2] = x + 2 + y * 16;
                    patches[3] = x + 3 + y * 16;

                    Packet layerpack = TerrainManager.CreateLandPacket(LandMap, patches);
                    RemoteClient.OutPacket(layerpack);
                }
            }
    	}

    	public void AddViewerAgent(OpenSimClient AgentClient) {
    		ServerConsole.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Creating new avatar for remote viewer agent");
    		Avatar NewAvatar = new Avatar(AgentClient);
    		ServerConsole.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Adding new avatar to world");
    		ServerConsole.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Starting RegionHandshake ");
    		NewAvatar.SendRegionHandshake(this);
    		
    		NewAvatar.PhysActor = this.phyScene.AddAvatar(new PhysicsVector(NewAvatar.position.X, NewAvatar.position.Y, NewAvatar.position.Z));
    		//this.Update();		// will work for now, but needs to be optimised so we don't update everything in the sim for each new user
    		this.Entities.Add(AgentClient.AgentID, NewAvatar);
	}
    	
    	public void AddNewPrim(ObjectAddPacket addPacket, OpenSimClient AgentClient)
    	{
    		ServerConsole.MainConsole.Instance.WriteLine("World.cs: AddNewPrim() - Creating new prim");
    		Primitive prim = new Primitive();
    		prim.CreateFromPacket(addPacket, AgentClient.AgentID, this._primCount);
    		this.Entities.Add(prim.uuid, prim);
    		this._primCount++;
    	}

    	public bool Backup() {
    		/* TODO: Save the current world entities state. */

    		return false;
    	}
	
    }
}
