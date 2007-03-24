using System;
using libsecondlife;
using libsecondlife.Packets;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Assets;
using OpenSim.Framework.Terrain;

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
    		OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs - creating new entitities instance");
    		Entities = new Dictionary<libsecondlife.LLUUID, Entity>();

    		OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs - creating LandMap");
    		TerrainManager = new TerrainManager(new SecondLife());
    		Avatar.SetupTemplate("avatar-template.dat");
    	//	ServerConsole.MainConsole.Instance.WriteLine("World.cs - Creating script engine instance");
    		// Initialise this only after the world has loaded
    	//	Scripts = new ScriptEngine(this);
            Avatar.LoadAnims();
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
			if(storageCount> 1200) //set to how often you want to backup 
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
    		OpenSimRoot.Instance.Cfg.SaveMap(this.LandMap);
    		
    		foreach(SimClient client in OpenSimRoot.Instance.ClientThreads.Values) {
    			this.SendLayerData(client);
    		}
    	}
    	public void LoadPrimsFromStorage()
    	{
    		OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: LoadPrimsFromStorage() - Loading primitives");
    		this.localStorage.LoadPrimitives(this);
    	}
    	
    	public void PrimFromStorage(PrimData prim)
    	{
    		if(prim.LocalID >= this._primCount)
    		{
    			_primCount = prim.LocalID + 1;
    		}
    		OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: PrimFromStorage() - Reloading prim (localId "+ prim.LocalID+ " ) from storage");
    		Primitive nPrim = new Primitive();
    		nPrim.CreateFromStorage(prim);
    		this.Entities.Add(nPrim.uuid, nPrim);
    	}
    	
    	public void Close()
    	{
    		this.localStorage.ShutDown();
    	}
    	
    	public void SendLayerData(SimClient RemoteClient) {
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
    	
    	public void GetInitialPrims(SimClient RemoteClient)
    	{
    		foreach (libsecondlife.LLUUID UUID in Entities.Keys)
    		{
    			if(Entities[UUID].ToString()== "OpenSim.world.Primitive")
    			{
    				((OpenSim.world.Primitive)Entities[UUID]).UpdateClient(RemoteClient);
    			}
    		}
    	}

    	public void AddViewerAgent(SimClient AgentClient) {
    		OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Creating new avatar for remote viewer agent");
    		Avatar NewAvatar = new Avatar(AgentClient);
    		OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Adding new avatar to world");
    		OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Starting RegionHandshake ");
    		NewAvatar.SendRegionHandshake(this);
    		PhysicsVector pVec = new PhysicsVector(NewAvatar.position.X, NewAvatar.position.Y, NewAvatar.position.Z);
    		NewAvatar.PhysActor = this.phyScene.AddAvatar(pVec);
    		this.Entities.Add(AgentClient.AgentID, NewAvatar);
    	}
    	
    	public void AddNewPrim(ObjectAddPacket addPacket, SimClient AgentClient)
    	{
    		OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: AddNewPrim() - Creating new prim");
    		Primitive prim = new Primitive();
    		prim.CreateFromPacket(addPacket, AgentClient.AgentID, this._primCount);
    		PhysicsVector pVec = new PhysicsVector(prim.position.X, prim.position.Y, prim.position.Z);
    		PhysicsVector pSize = new PhysicsVector( 0.255f, 0.255f, 0.255f);
    		if(OpenSim.world.Avatar.PhysicsEngineFlying)
    		{
    			prim.PhysActor = this.phyScene.AddPrim(pVec, pSize );
    		}
    		//prim.PhysicsEnabled = true;
    		this.Entities.Add(prim.uuid, prim);
    		this._primCount++;
	}

        public void DeRezObject(DeRezObjectPacket DeRezPacket, SimClient AgentClient)
        {
		//Needs to delete object from physics at a later date
		
		libsecondlife.LLUUID [] DeRezEnts;
		DeRezEnts = new libsecondlife.LLUUID[ DeRezPacket.ObjectData.Length ];
		int i = 0;
		foreach( DeRezObjectPacket.ObjectDataBlock Data in DeRezPacket.ObjectData )
		{
			//OpenSim.Framework.Console.MainConsole.Instance.WriteLine("LocalID:" + Data.ObjectLocalID.ToString());
			foreach (Entity ent in OpenSimRoot.Instance.LocalWorld.Entities.Values)
			{
				if (ent.localid == Data.ObjectLocalID)
				{
					DeRezEnts[i++] = ent.uuid;
					this.localStorage.RemovePrim(ent.uuid);
					KillObjectPacket kill = new KillObjectPacket();
					kill.ObjectData = new KillObjectPacket.ObjectDataBlock[1];
					kill.ObjectData[0] = new KillObjectPacket.ObjectDataBlock();
					kill.ObjectData[0].ID = ent.localid;
					foreach (SimClient client in OpenSimRoot.Instance.ClientThreads.Values)
					{
						client.OutPacket(kill);
					}
					//Uncommenting this means an old UUID will be re-used, thus crashing the asset server
					//Uncomment when prim/object UUIDs are random or such
					//2007-03-22 - Randomskk
					//this._primCount--;
					OpenSim.Framework.Console.MainConsole.Instance.WriteLine("Deleted UUID " + ent.uuid);
				}
			}
		}
		foreach( libsecondlife.LLUUID uuid in DeRezEnts )
		{
			lock (OpenSimRoot.Instance.LocalWorld.Entities)
			{
				OpenSimRoot.Instance.LocalWorld.Entities.Remove(uuid);
			}
		}
		
        }

    	public bool Backup() {
    	
    		OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: Backup() - Backing up Primitives");
    		foreach (libsecondlife.LLUUID UUID in Entities.Keys)
    		{
    			Entities[UUID].BackUp();
    		}
    		return true;
    	}
	
    }
}
