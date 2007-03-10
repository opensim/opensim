using System;
using libsecondlife;
using libsecondlife.Packets;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using PhysicsSystem;
using GridInterfaces;

namespace OpenSim.world
{
	public class World : ILocalStorageReceiver
    {
    	public Dictionary<libsecondlife.LLUUID, Entity> Entities;
    	public float[] LandMap;
    	public ScriptEngine Scripts;
    	public uint _localNumber=0;
    	private PhysicsScene phyScene;
    	private float timeStep= 0.1f;
    	private libsecondlife.TerrainManager TerrainManager;
    	public ILocalStorage localStorage;
    	private Random Rand = new Random();
    	private uint _primCount = 702000;
    	private int storageCount;

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
    		
    		//backup world data
    		this.storageCount++;
			if(storageCount> 300) //set to how often you want to backup 
			{
				this.Backup();
				storageCount =0;
			}
    	}

    	public bool LoadStorageDLL(string dllName)
    	{
    		Assembly pluginAssembly = Assembly.LoadFrom(dllName);
			ILocalStorage store = null;
			
			foreach (Type pluginType in pluginAssembly.GetTypes())
			{
				if (pluginType.IsPublic)
				{
					if (!pluginType.IsAbstract)
					{
						Type typeInterface = pluginType.GetInterface("ILocalStorage", true);
						
						if (typeInterface != null)
						{
							ILocalStorage plug = (ILocalStorage)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
							store = plug;
							break;
						}
						
						typeInterface = null;
					}
				}
			}
			pluginAssembly = null;
			this.localStorage = store;
			return(store == null);
    	}
    	
    	public void RegenerateTerrain()
    	{
    		HeightmapGenHills hills = new HeightmapGenHills();
    		this.LandMap = hills.GenerateHeightmap(200, 4.0f, 80.0f, false);
    		this.phyScene.SetTerrain(this.LandMap);
    		OpenSim_Main.cfg.SaveMap();
    		
    		foreach(OpenSimClient client in OpenSim_Main.sim.ClientThreads.Values) {
    			this.SendLayerData(client);
    		}
    	}
    	public void LoadPrimsFromStorage()
    	{
    		ServerConsole.MainConsole.Instance.WriteLine("World.cs: LoadPrimsFromStorage() - Loading primitives");
    		this.localStorage.LoadPrimitives(this);
    	}
    	
    	public void PrimFromStorage(PrimData prim)
    	{
    		if(prim.LocalID >= this._primCount)
    		{
    			_primCount = prim.LocalID + 1;
    		}
    		ServerConsole.MainConsole.Instance.WriteLine("World.cs: PrimFromStorage() - Reloading prim (localId "+ prim.LocalID+ " ) from storage");
    		Primitive nPrim = new Primitive();
    		nPrim.CreateFromStorage(prim);
    		this.Entities.Add(nPrim.uuid, nPrim);
    	}
    	
    	public void Close()
    	{
    		this.localStorage.ShutDown();
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
    	
    	public void GetInitialPrims(OpenSimClient RemoteClient)
    	{
    		foreach (libsecondlife.LLUUID UUID in Entities.Keys)
    		{
    			if(Entities[UUID].ToString()== "OpenSim.world.Primitive")
    			{
    				((OpenSim.world.Primitive)Entities[UUID]).UpdateClient(RemoteClient);
    			}
    		}
    	}

    	public void AddViewerAgent(OpenSimClient AgentClient) {
    		ServerConsole.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Creating new avatar for remote viewer agent");
    		Avatar NewAvatar = new Avatar(AgentClient);
    		ServerConsole.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Adding new avatar to world");
    		ServerConsole.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Starting RegionHandshake ");
    		NewAvatar.SendRegionHandshake(this);
    		PhysicsVector pVec = new PhysicsVector(NewAvatar.position.X, NewAvatar.position.Y, NewAvatar.position.Z);
    		NewAvatar.PhysActor = this.phyScene.AddAvatar(pVec);
    		this.Entities.Add(AgentClient.AgentID, NewAvatar);
    	}
    	
    	public void AddNewPrim(ObjectAddPacket addPacket, OpenSimClient AgentClient)
    	{
    		ServerConsole.MainConsole.Instance.WriteLine("World.cs: AddNewPrim() - Creating new prim");
    		Primitive prim = new Primitive();
    		prim.CreateFromPacket(addPacket, AgentClient.AgentID, this._primCount);
    		PhysicsVector pVec = new PhysicsVector(prim.position.X, prim.position.Y, prim.position.Z);
    		PhysicsVector pSize = new PhysicsVector( 0.25f, 0.25f, 0.25f);
    		//prim.PhysActor = this.phyScene.AddPrim(pVec, pSize );
    		//prim.PhysicsEnabled = true;
    		this.Entities.Add(prim.uuid, prim);
    		this._primCount++;
    	}

    	public bool Backup() {
    		/* TODO: Save the current world entities state. */
    		ServerConsole.MainConsole.Instance.WriteLine("World.cs: Backup() - Backing up Primitives");
    		foreach (libsecondlife.LLUUID UUID in Entities.Keys)
    		{
    			Entities[UUID].BackUp();
    		}
    		return true;
    	}
	
    }
}
