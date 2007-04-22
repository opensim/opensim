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
using OpenSim.Terrain;

namespace OpenSim.world
{
    public partial class World : ILocalStorageReceiver, IScriptAPI
    {
        public object LockPhysicsEngine = new object();
        public Dictionary<libsecondlife.LLUUID, Entity> Entities;
        public Dictionary<libsecondlife.LLUUID, Avatar> Avatars;
        public Dictionary<libsecondlife.LLUUID, Primitive> Prims;
        public ScriptEngine Scripts;
        public TerrainEngine Terrain; //TODO: Replace TerrainManager with this.
        public uint _localNumber = 0;
        private PhysicsScene phyScene;
        private float timeStep = 0.1f;
        private libsecondlife.TerrainManager TerrainManager; // To be referenced via TerrainEngine
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
        private Object updateLock;

        /// <summary>
        /// Creates a new World class, and a region to go with it.
        /// </summary>
        /// <param name="clientThreads">Dictionary to contain client threads</param>
        /// <param name="regionHandle">Region Handle for this region</param>
        /// <param name="regionName">Region Name for this region</param>
        public World(Dictionary<uint, SimClient> clientThreads, ulong regionHandle, string regionName)
        {
            try
            {
                updateLock = null;
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
                Terrain = new TerrainEngine();
                Avatar.SetupTemplate("avatar-template.dat");
                //	MainConsole.Instance.WriteLine("World.cs - Creating script engine instance");
                // Initialise this only after the world has loaded
                //	Scripts = new ScriptEngine(this);
                Avatar.LoadAnims();
                this.SetDefaultScripts();
                this.LoadScriptEngines();
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: Constructor failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Loads a new script into the specified entity
        /// </summary>
        /// <param name="entity">Entity to be scripted</param>
        /// <param name="script">The script to load</param>
        public void AddScript(Entity entity, Script script)
        {
            try
            {
                ScriptHandler scriptHandler = new ScriptHandler(script, entity, this);
                m_scriptHandlers.Add(scriptHandler.ScriptId, scriptHandler);
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: AddScript() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Loads a new script into the specified entity, using a script loaded from a string.
        /// </summary>
        /// <param name="entity">The entity to be scripted</param>
        /// <param name="scriptData">The string containing the script</param>
        public void AddScript(Entity entity, string scriptData)
        {
            try
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
                //Console.WriteLine("script string is " + substring);
                if (substring.StartsWith("<ScriptEngine:"))
                {
                    string substring1 = "";
                    string script = "";
                    // Console.WriteLine("searching for script engine");
                    substring1 = substring.Remove(0, 14);
                    int dev = substring1.IndexOf(',');
                    string sEngine = substring1.Substring(0, dev);
                    substring1 = substring1.Remove(0, dev + 1);
                    int end = substring1.IndexOf('>');
                    string sName = substring1.Substring(0, end);
                    //Console.WriteLine(" script info : " + sEngine + " , " + sName);
                    int startscript = substring.IndexOf('>');
                    script = substring.Remove(0, startscript + 1);
                    // Console.WriteLine("script data is " + script);
                    if (this.scriptEngines.ContainsKey(sEngine))
                    {
                        this.scriptEngines[sEngine].LoadScript(script, sName, entity.localid);
                    }
                }
                else if (this.m_scripts.TryGetValue(substring, out scriptFactory))
                {
                    //Console.WriteLine("added script");
                    this.AddScript(entity, scriptFactory());
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: AddScript() - Failed with exception " + e.ToString());
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

        /// <summary>
        /// Performs per-frame updates on the world, this should be the central world loop
        /// </summary>
        public void Update()
        {
            lock (updateLock)
            {
                try
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
                    foreach (IScriptEngine scripteng in this.scriptEngines.Values)
                    {
                        scripteng.OnFrame();
                    }
                    //backup world data
                    this.storageCount++;
                    if (storageCount > 1200) //set to how often you want to backup 
                    {
                        this.Backup();
                        storageCount = 0;
                    }
                }
                catch (Exception e)
                {
                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: Update() - Failed with exception " + e.ToString());
                }
            }
        }

        /// <summary>
        /// Loads a new storage subsystem from a named library
        /// </summary>
        /// <param name="dllName">Storage Library</param>
        /// <returns>Successful or not</returns>
        public bool LoadStorageDLL(string dllName)
        {
            try
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
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: LoadStorageDLL() - Failed with exception " + e.ToString());
                return false;
            }
        }

        #region Regenerate Terrain

        /// <summary>
        /// Rebuilds the terrain using a procedural algorithm
        /// </summary>
        public void RegenerateTerrain()
        {
            try
            {
                Terrain.hills();

                lock (this.LockPhysicsEngine)
                {
                    this.phyScene.SetTerrain(Terrain.getHeights1D());
                }
                this.localStorage.SaveMap(this.Terrain.getHeights1D());

                foreach (SimClient client in m_clientThreads.Values)
                {
                    this.SendLayerData(client);
                }

                foreach (libsecondlife.LLUUID UUID in Entities.Keys)
                {
                    Entities[UUID].LandRenegerated();
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: RegenerateTerrain() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Rebuilds the terrain using a 2D float array
        /// </summary>
        /// <param name="newMap">256,256 float array containing heights</param>
        public void RegenerateTerrain(float[,] newMap)
        {
            try
            {
                this.Terrain.setHeights2D(newMap);
                lock (this.LockPhysicsEngine)
                {
                    this.phyScene.SetTerrain(this.Terrain.getHeights1D());
                }
                this.localStorage.SaveMap(this.Terrain.getHeights1D());

                foreach (SimClient client in m_clientThreads.Values)
                {
                    this.SendLayerData(client);
                }

                foreach (libsecondlife.LLUUID UUID in Entities.Keys)
                {
                    Entities[UUID].LandRenegerated();
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: RegenerateTerrain() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Rebuilds the terrain assuming changes occured at a specified point[?]
        /// </summary>
        /// <param name="changes">???</param>
        /// <param name="pointx">???</param>
        /// <param name="pointy">???</param>
        public void RegenerateTerrain(bool changes, int pointx, int pointy)
        {
            try
            {
                if (changes)
                {
                    lock (this.LockPhysicsEngine)
                    {
                        this.phyScene.SetTerrain(this.Terrain.getHeights1D());
                    }
                    this.localStorage.SaveMap(this.Terrain.getHeights1D());

                    foreach (SimClient client in m_clientThreads.Values)
                    {
                        this.SendLayerData(pointx, pointy, client);
                    }
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: RegenerateTerrain() - Failed with exception " + e.ToString());
            }
        }

        #endregion

        /// <summary>
        /// Loads the World heightmap
        /// </summary>
        public void LoadWorldMap()
        {
            try
            {
                float[] map = this.localStorage.LoadWorld();
                if (map == null)
                {
                    Console.WriteLine("creating new terrain");
                    this.Terrain.hills();

                    this.localStorage.SaveMap(this.Terrain.getHeights1D());
                }
                else
                {
                    this.Terrain.setHeights1D(map);
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: LoadWorldMap() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Loads the World's objects
        /// </summary>
        public void LoadPrimsFromStorage()
        {
            try
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: LoadPrimsFromStorage() - Loading primitives");
                this.localStorage.LoadPrimitives(this);
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: LoadPrimsFromStorage() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Loads a specific object from storage
        /// </summary>
        /// <param name="prim">The object to load</param>
        public void PrimFromStorage(PrimData prim)
        {
            try
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
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: PrimFromStorage() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Tidy before shutdown
        /// </summary>
        public void Close()
        {
            try
            {
                this.localStorage.ShutDown();
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: Close() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Send the region heightmap to the client
        /// </summary>
        /// <param name="RemoteClient">Client to send to</param>
        public void SendLayerData(SimClient RemoteClient)
        {
            try
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

                        Packet layerpack = TerrainManager.CreateLandPacket(Terrain.getHeights1D(), patches);
                        RemoteClient.OutPacket(layerpack);
                    }
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: SendLayerData() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Sends a specified patch to a client
        /// </summary>
        /// <param name="px">Patch coordinate (x) 0..16</param>
        /// <param name="py">Patch coordinate (y) 0..16</param>
        /// <param name="RemoteClient">The client to send to</param>
        public void SendLayerData(int px, int py, SimClient RemoteClient)
        {
            try
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

                Packet layerpack = TerrainManager.CreateLandPacket(Terrain.getHeights1D(), patches);
                RemoteClient.OutPacket(layerpack);
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: SendLayerData() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Sends prims to a client
        /// </summary>
        /// <param name="RemoteClient">Client to send to</param>
        public void GetInitialPrims(SimClient RemoteClient)
        {
            try
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
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: GetInitialPrims() - Failed with exception " + e.ToString());
            }
        }

        public void AddViewerAgent(SimClient agentClient)
        {
            try
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Creating new avatar for remote viewer agent");
                Avatar newAvatar = new Avatar(agentClient, this, m_regionName, m_clientThreads, m_regionHandle);
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Adding new avatar to world");
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs:AddViewerAgent() - Starting RegionHandshake ");
                newAvatar.SendRegionHandshake(this);
                if (!agentClient.m_child)
                {
                    PhysicsVector pVec = new PhysicsVector(newAvatar.Pos.X, newAvatar.Pos.Y, newAvatar.Pos.Z);
                    lock (this.LockPhysicsEngine)
                    {
                        newAvatar.PhysActor = this.phyScene.AddAvatar(pVec);
                    }
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
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: AddViewerAgent() - Failed with exception " + e.ToString());
            }
        }

        public void RemoveViewerAgent(SimClient agentClient)
        {
            try
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
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: RemoveViewerAgent() - Failed with exception " + e.ToString());
            }
        }

        public void AddNewPrim(ObjectAddPacket addPacket, SimClient AgentClient)
        {
            try
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
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: AddNewPrim() - Failed with exception " + e.ToString());
            }
        }

        public bool Backup()
        {
            try
            {
                // Terrain backup routines
                if (Terrain.tainted > 0)
                {
                    Terrain.tainted = 0;
                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: Backup() - Terrain tainted, saving.");
                    localStorage.SaveMap(Terrain.getHeights1D());
                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: Backup() - Terrain saved, informing Physics.");
                    phyScene.SetTerrain(Terrain.getHeights1D());
                }

                // Primitive backup routines
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: Backup() - Backing up Primitives");
                foreach (libsecondlife.LLUUID UUID in Entities.Keys)
                {
                    Entities[UUID].BackUp();
                }

                // Backup successful
                return true;
            }
            catch (Exception e)
            {
                // Backup failed
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: Backup() - Backup Failed with exception " + e.ToString());
                return false;
            }
        }

        public void SetDefaultScripts()
        {
            try
            {
                this.m_scripts.Add("FollowRandomAvatar", delegate()
                                                                 {
                                                                     return new FollowRandomAvatar();
                                                                 });
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine("World.cs: SetDefaultScripts() - Failed with exception " + e.ToString());
            }
        }

    }
}
