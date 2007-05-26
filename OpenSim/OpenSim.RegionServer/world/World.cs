using System;
using libsecondlife;
using libsecondlife.Packets;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using System.Threading;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Terrain;
using OpenSim.Framework.Inventory;
using OpenSim.Assets;
//using OpenSim.world.scripting;
using OpenSim.RegionServer.world.scripting;
using OpenSim.Terrain;

namespace OpenSim.world
{
    public partial class World : WorldBase, ILocalStorageReceiver, IScriptAPI
    {
        public object LockPhysicsEngine = new object();
        public Dictionary<libsecondlife.LLUUID, Avatar> Avatars;
        public Dictionary<libsecondlife.LLUUID, Primitive> Prims;
        //public ScriptEngine Scripts;
        public uint _localNumber = 0;
        private PhysicsScene phyScene;
        private float timeStep = 0.1f;
        public ILocalStorage localStorage;
        private Random Rand = new Random();
        private uint _primCount = 702000;
        private int storageCount;
        private Dictionary<LLUUID, ScriptHandler> m_scriptHandlers;
        private Dictionary<string, ScriptFactory> m_scripts;
        private Mutex updateLock;
        public string m_datastore;

        #region Properties
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
        #endregion

        #region Constructors
        /// <summary>
        /// Creates a new World class, and a region to go with it.
        /// </summary>
        /// <param name="clientThreads">Dictionary to contain client threads</param>
        /// <param name="regionHandle">Region Handle for this region</param>
        /// <param name="regionName">Region Name for this region</param>
        public World(Dictionary<uint, ClientView> clientThreads, RegionInfo regInfo, ulong regionHandle, string regionName)
        {
            try
            {
                updateLock = new Mutex(false);
                m_clientThreads = clientThreads;
                m_regionHandle = regionHandle;
                m_regionName = regionName;
                m_regInfo = regInfo;

                m_scriptHandlers = new Dictionary<LLUUID, ScriptHandler>();
                m_scripts = new Dictionary<string, ScriptFactory>();

                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs - creating new entitities instance");
                Entities = new Dictionary<libsecondlife.LLUUID, Entity>();
                Avatars = new Dictionary<LLUUID, Avatar>();
                Prims = new Dictionary<LLUUID, Primitive>();

                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs - creating LandMap");
                TerrainManager = new TerrainManager(new SecondLife());
                Terrain = new TerrainEngine();
                Avatar.SetupTemplate("avatar-texture.dat");
                //	MainConsole.Instance.WriteLine("World.cs - Creating script engine instance");
                // Initialise this only after the world has loaded
                //	Scripts = new ScriptEngine(this);
                Avatar.LoadAnims();
                this.SetDefaultScripts();
                this.LoadScriptEngines();
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.CRITICAL, "World.cs: Constructor failed with exception " + e.ToString());
            }
        }
        #endregion

        #region Script Methods
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
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: AddScript() - Failed with exception " + e.ToString());
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
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: AddScript() - Failed with exception " + e.ToString());
            }
        }

        #endregion

        #region Update Methods
        /// <summary>
        /// Performs per-frame updates on the world, this should be the central world loop
        /// </summary>
        public override void Update()
        {
            updateLock.WaitOne();
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
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: Update() - Failed with exception " + e.ToString());
            }
            updateLock.ReleaseMutex();
        }

        public bool Backup()
        {
            try
            {
                // Terrain backup routines
                if (Terrain.tainted > 0)
                {
                    Terrain.tainted = 0;
                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs: Backup() - Terrain tainted, saving.");
                    localStorage.SaveMap(Terrain.getHeights1D());
                    OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs: Backup() - Terrain saved, informing Physics.");
                    lock (this.LockPhysicsEngine)
                    {
                        phyScene.SetTerrain(Terrain.getHeights1D());
                    }
                }

                // Primitive backup routines
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs: Backup() - Backing up Primitives");
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
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, "World.cs: Backup() - Backup Failed with exception " + e.ToString());
                return false;
            }
        }
        #endregion

        #region Setup Methods
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

                                store.Initialise(this.m_datastore);
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
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: LoadStorageDLL() - Failed with exception " + e.ToString());
                return false;
            }
        }

        public void SetDefaultScripts()
        {
            this.m_scripts.Add("FollowRandomAvatar", delegate()
                                                             {
                                                                 return new OpenSim.RegionServer.world.scripting.FollowRandomAvatar();
                                                             });
        }

        #endregion

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

                foreach (ClientView client in m_clientThreads.Values)
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
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: RegenerateTerrain() - Failed with exception " + e.ToString());
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

                foreach (ClientView client in m_clientThreads.Values)
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
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: RegenerateTerrain() - Failed with exception " + e.ToString());
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
                    /* Dont save here, rely on tainting system instead */

                    foreach (ClientView client in m_clientThreads.Values)
                    {
                        this.SendLayerData(pointx, pointy, client);
                    }
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: RegenerateTerrain() - Failed with exception " + e.ToString());
            }
        }

        #endregion

        #region Load Terrain
        /// <summary>
        /// Loads the World heightmap
        /// </summary>
        public override void LoadWorldMap()
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
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: LoadWorldMap() - Failed with exception " + e.ToString());
            }
        }
        #endregion

        #region Primitives Methods

        /// <summary>
        /// Sends prims to a client
        /// </summary>
        /// <param name="RemoteClient">Client to send to</param>
        public void GetInitialPrims(ClientView RemoteClient)
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
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: GetInitialPrims() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// Loads the World's objects
        /// </summary>
        public void LoadPrimsFromStorage()
        {
            try
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs: LoadPrimsFromStorage() - Loading primitives");
                this.localStorage.LoadPrimitives(this);
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: LoadPrimsFromStorage() - Failed with exception " + e.ToString());
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
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs: PrimFromStorage() - Reloading prim (localId " + prim.LocalID + " ) from storage");
                Primitive nPrim = new Primitive(m_clientThreads, m_regionHandle, this);
                nPrim.CreateFromStorage(prim);
                this.Entities.Add(nPrim.uuid, nPrim);
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: PrimFromStorage() - Failed with exception " + e.ToString());
            }
        }

        public void AddNewPrim(Packet addPacket, ClientView agentClient)
        {
            AddNewPrim((ObjectAddPacket)addPacket, agentClient.AgentID);
        }

        public void AddNewPrim(ObjectAddPacket addPacket, LLUUID ownerID)
        {
            try
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs: AddNewPrim() - Creating new prim");
                Primitive prim = new Primitive(m_clientThreads, m_regionHandle, this);
                prim.CreateFromPacket(addPacket, ownerID, this._primCount);
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
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: AddNewPrim() - Failed with exception " + e.ToString());
            }
        }

        #endregion

        #region Add/Remove Avatar Methods

        public override Avatar AddViewerAgent(ClientView agentClient)
        {
            //register for events
            agentClient.OnChatFromViewer += new ChatFromViewer(this.SimChat);
            agentClient.OnRezObject += new RezObject(this.RezObject);
            agentClient.OnModifyTerrain += new ModifyTerrain(this.ModifyTerrain);
            agentClient.OnRegionHandShakeReply += new ClientView.GenericCall(this.SendLayerData);
            agentClient.OnRequestWearables += new ClientView.GenericCall(this.GetInitialPrims);
            agentClient.OnRequestAvatarsData += new ClientView.GenericCall(this.SendAvatarsToClient);
            agentClient.OnLinkObjects += new LinkObjects(this.LinkObjects);
            agentClient.OnAddPrim += new ClientView.GenericCall4(this.AddNewPrim);
            agentClient.OnUpdatePrimShape += new ClientView.UpdateShape(this.UpdatePrimShape);
            agentClient.OnObjectSelect += new ClientView.ObjectSelect(this.SelectPrim);
            agentClient.OnUpdatePrimFlags += new ClientView.UpdatePrimFlags(this.UpdatePrimFlags);
            agentClient.OnUpdatePrimTexture += new ClientView.UpdatePrimTexture(this.UpdatePrimTexture);
            agentClient.OnUpdatePrimPosition += new ClientView.UpdatePrimVector(this.UpdatePrimPosition);
            agentClient.OnUpdatePrimRotation += new ClientView.UpdatePrimRotation(this.UpdatePrimRotation);
            agentClient.OnUpdatePrimScale += new ClientView.UpdatePrimVector(this.UpdatePrimScale);
            agentClient.OnDeRezObject += new ClientView.GenericCall4(this.DeRezObject);
            Avatar newAvatar = null;
            try
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs:AddViewerAgent() - Creating new avatar for remote viewer agent");
                newAvatar = new Avatar(agentClient, this, m_regionName, m_clientThreads, m_regionHandle, true, 20);
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs:AddViewerAgent() - Adding new avatar to world");
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs:AddViewerAgent() - Starting RegionHandshake ");
                newAvatar.SendRegionHandshake(this);
                //if (!agentClient.m_child)
                //{
                    
                    PhysicsVector pVec = new PhysicsVector(newAvatar.Pos.X, newAvatar.Pos.Y, newAvatar.Pos.Z);
                    lock (this.LockPhysicsEngine)
                    {
                        newAvatar.PhysActor = this.phyScene.AddAvatar(pVec);
                    }
              //  }
                lock (Entities)
                {
                    if (!Entities.ContainsKey(agentClient.AgentID))
                    {
                        this.Entities.Add(agentClient.AgentID, newAvatar);
                    }
                    else
                    {
                        Entities[agentClient.AgentID] = newAvatar;
                    }
                }
                lock (Avatars)
                {
                    if (Avatars.ContainsKey(agentClient.AgentID))
                    {
                        Avatars[agentClient.AgentID] = newAvatar;
                    }
                    else
                    {
                        this.Avatars.Add(agentClient.AgentID, newAvatar);
                    }
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: AddViewerAgent() - Failed with exception " + e.ToString());
            }
            return newAvatar;
        }

        public override void RemoveViewerAgent(ClientView agentClient)
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
                if (agentClient.ClientAvatar.PhysActor != null)
                {
                    this.phyScene.RemoveAvatar(agentClient.ClientAvatar.PhysActor);
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: RemoveViewerAgent() - Failed with exception " + e.ToString());
            }
        }
        #endregion

        #region Request Avatars List Methods
        //The idea is to have a group of method that return a list of avatars meeting some requirement
        // ie it could be all Avatars within a certain range of the calling prim/avatar. 

        public List<Avatar> RequestAvatarList()
        {
            List<Avatar> result = new List<Avatar>();

            foreach (Avatar avatar in Avatars.Values)
            {
                result.Add(avatar);
            }

            return result;
        }
        #endregion

        #region ShutDown
        /// <summary>
        /// Tidy before shutdown
        /// </summary>
        public override void Close()
        {
            try
            {
                this.localStorage.ShutDown();
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.HIGH, "World.cs: Close() - Failed with exception " + e.ToString());
            }
        }
        #endregion

    }
}
