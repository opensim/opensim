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
using OpenSim.Framework.Inventory;
using OpenSim.Assets;
using OpenSim.world.scripting;
using OpenSim.RegionServer.world.scripting;
using OpenSim.RegionServer.world.scripting.Scripts;

namespace OpenSim.world
{
    public partial class World : ILocalStorageReceiver
    {
        public object LockPhysicsEngine = new object();
        public Dictionary<libsecondlife.LLUUID, Entity> Entities;
        public Dictionary<libsecondlife.LLUUID, Avatar> Avatars;
        public Dictionary<libsecondlife.LLUUID, Primitive> Prims;
        public float[] LandMap;
        public ScriptEngine Scripts;
        public uint _localNumber = 0;
        private PhysicsScene phyScene;
        private float timeStep = 0.1f;
        private libsecondlife.TerrainManager TerrainManager;
        public ILocalStorage localStorage;
        private Random Rand = new Random();
        private uint _primCount = 702000;
        private int storageCount;
        private Dictionary<uint, SimClient> m_clientThreads;
        private Dictionary<LLUUID, ScriptHandler> m_scriptHandlers;
        private Dictionary<string, ScriptFactory> m_scripts;
        private ulong m_regionHandle;
        private string m_regionName;
        private InventoryCache _inventoryCache;
        private AssetCache _assetCache;

        public World(Dictionary<uint, SimClient> clientThreads, ulong regionHandle, string regionName)
        {
            m_clientThreads = clientThreads;
            m_regionHandle = regionHandle;
            m_regionName = regionName;

            m_scriptHandlers = new Dictionary<LLUUID, ScriptHandler>();
            m_scripts = new Dictionary<string, ScriptFactory>();

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs - creating new entitities instance");
            Entities = new Dictionary<libsecondlife.LLUUID, Entity>();
            Avatars = new Dictionary<LLUUID, Avatar>();
            Prims = new Dictionary<LLUUID, Primitive>();

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs - creating LandMap");
            TerrainManager = new TerrainManager(new SecondLife());
            Avatar.SetupTemplate("avatar-template.dat");
            //	MainConsole.Instance.WriteLine("World.cs - Creating script engine instance");
            // Initialise this only after the world has loaded
            //	Scripts = new ScriptEngine(this);
            Avatar.LoadAnims();
            this.SetDefaultScripts();
        }

        public void AddScript(Entity entity, Script script)
        {
            ScriptHandler scriptHandler = new ScriptHandler(script, entity, this);
            m_scriptHandlers.Add(scriptHandler.ScriptId, scriptHandler);
        }

        public void AddScript(Entity entity, string scriptData)
        {
            int scriptstart = 0;
            int scriptend = 0;
            string substring;
            scriptstart = scriptData.LastIndexOf("<Script>");
            scriptend = scriptData.LastIndexOf("</Script>");
            substring = scriptData.Substring(scriptstart + 8, scriptend - scriptstart - 8);
            substring = substring.Trim();
            //Console.WriteLine("searching for script to add: " + substring);

            ScriptFactory scriptFactory;

            if (this.m_scripts.TryGetValue(substring, out scriptFactory))
            {
                //Console.WriteLine("added script");
                this.AddScript(entity, scriptFactory());
            }

        }

        public InventoryCache InventoryCache
        {
            set
            {
                this._inventoryCache = value;
            }
        }

        public AssetCache AssetCache
        {
            set
            {
                this._assetCache = value;
            }
        }
        public PhysicsScene PhysScene
        {
            set
            {
                this.phyScene = value;
            }
            get
            {
                return (this.phyScene);
            }
        }

        public void Update()
        {
            if (this.phyScene.IsThreaded)
            {
                this.phyScene.GetResults();

            }

            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
                Entities[UUID].addForces();
            }

            lock (this.LockPhysicsEngine)
            {
                this.phyScene.Simulate(timeStep);
            }

            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
                Entities[UUID].update();
            }

            foreach (ScriptHandler scriptHandler in m_scriptHandlers.Values)
            {
                scriptHandler.OnFrame();
            }

            //backup world data
            this.storageCount++;
            if (storageCount > 1200) //set to how often you want to backup 
            {
                this.Backup();
                storageCount = 0;
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
            return (store == null);
        }

        public void RegenerateTerrain()
        {
            HeightmapGenHills hills = new HeightmapGenHills();
            this.LandMap = hills.GenerateHeightmap(200, 4.0f, 80.0f, false);
            lock (this.LockPhysicsEngine)
            {
                this.phyScene.SetTerrain(this.LandMap);
            }
            this.localStorage.SaveMap(this.LandMap);

            foreach (SimClient client in m_clientThreads.Values)
            {
                this.SendLayerData(client);
            }

            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
                Entities[UUID].LandRenegerated();
            }
        }

        public void RegenerateTerrain(float[] newMap)
        {
            this.LandMap = newMap;
            lock (this.LockPhysicsEngine)
            {
                this.phyScene.SetTerrain(this.LandMap);
            }
            this.localStorage.SaveMap(this.LandMap);

            foreach (SimClient client in m_clientThreads.Values)
            {
                this.SendLayerData(client);
            }

            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
                Entities[UUID].LandRenegerated();
            }
        }

        public void RegenerateTerrain(bool changes, int pointx, int pointy)
        {
            if (changes)
            {
                lock (this.LockPhysicsEngine)
                {
                    this.phyScene.SetTerrain(this.LandMap);
                }
                this.localStorage.SaveMap(this.LandMap);

                foreach (SimClient client in m_clientThreads.Values)
                {
                    this.SendLayerData(pointx, pointy, client);
                }
            }
        }

        public void LoadWorldMap()
        {
            LandMap = this.localStorage.LoadWorld();
        }

        public void LoadPrimsFromStorage()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: LoadPrimsFromStorage() - Loading primitives");
            this.localStorage.LoadPrimitives(this);
        }

        public void PrimFromStorage(PrimData prim)
        {
            if (prim.LocalID >= this._primCount)
            {
                _primCount = prim.LocalID + 1;
            }
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: PrimFromStorage() - Reloading prim (localId " + prim.LocalID + " ) from storage");
            Primitive nPrim = new Primitive(m_clientThreads, m_regionHandle, this);
            nPrim.CreateFromStorage(prim);
            this.Entities.Add(nPrim.uuid, nPrim);
        }

        public void Close()
        {
            this.localStorage.ShutDown();
        }

        public void SendLayerData(SimClient RemoteClient)
        {
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

        public void SendLayerData(int px, int py, SimClient RemoteClient)
        {
            int[] patches = new int[1];
            int patchx, patchy;
            patchx = px / 16;
            /* if (patchx > 12)
             {
                 patchx = 12;
             }*/
            patchy = py / 16;

            patches[0] = patchx + 0 + patchy * 16;
            //patches[1] = patchx + 1 + patchy * 16;
            //patches[2] = patchx + 2 + patchy * 16;
            //patches[3] = patchx + 3 + patchy * 16;

            Packet layerpack = TerrainManager.CreateLandPacket(LandMap, patches);
            RemoteClient.OutPacket(layerpack);
        }

        public void GetInitialPrims(SimClient RemoteClient)
        {
            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
                if (Entities[UUID] is Primitive)
                {
                    Primitive primitive = Entities[UUID] as Primitive;
                    primitive.UpdateClient(RemoteClient);
                }
            }
        }

        public void AddViewerAgent(SimClient agentClient)
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Creating new avatar for remote viewer agent");
            Avatar newAvatar = new Avatar(agentClient, this, m_regionName, m_clientThreads, m_regionHandle);
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Adding new avatar to world");
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Starting RegionHandshake ");
            newAvatar.SendRegionHandshake(this);
            PhysicsVector pVec = new PhysicsVector(newAvatar.Pos.X, newAvatar.Pos.Y, newAvatar.Pos.Z);
            lock (this.LockPhysicsEngine)
            {
                newAvatar.PhysActor = this.phyScene.AddAvatar(pVec);
            }
            lock (Entities)
            {
                this.Entities.Add(agentClient.AgentID, newAvatar);
            }
            lock (Avatars)
            {
                this.Avatars.Add(agentClient.AgentID, newAvatar);
            }
        }

        public void RemoveViewerAgent(SimClient agentClient)
        {
            lock (Entities)
            {
                Entities.Remove(agentClient.AgentID);
            }
            lock (Avatars)
            {
                Avatars.Remove(agentClient.AgentID);
            }
        }

        public void AddNewPrim(ObjectAddPacket addPacket, SimClient AgentClient)
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: AddNewPrim() - Creating new prim");
            Primitive prim = new Primitive(m_clientThreads, m_regionHandle, this);
            prim.CreateFromPacket(addPacket, AgentClient.AgentID, this._primCount);
            PhysicsVector pVec = new PhysicsVector(prim.Pos.X, prim.Pos.Y, prim.Pos.Z);
            PhysicsVector pSize = new PhysicsVector(0.255f, 0.255f, 0.255f);
            if (OpenSim.world.Avatar.PhysicsEngineFlying)
            {
                lock (this.LockPhysicsEngine)
                {
                    prim.PhysActor = this.phyScene.AddPrim(pVec, pSize);
                }
            }

            this.Entities.Add(prim.uuid, prim);
            this._primCount++;
        }

        public bool Backup()
        {

            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: Backup() - Backing up Primitives");
            foreach (libsecondlife.LLUUID UUID in Entities.Keys)
            {
                Entities[UUID].BackUp();
            }
            return true;
        }

        public void SetDefaultScripts()
        {
            this.m_scripts.Add("FollowRandomAvatar", delegate()
                                                             {
                                                                 return new FollowRandomAvatar();
                                                             });
        }

    }
}
