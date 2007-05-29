using System;
using libsecondlife;
using libsecondlife.Packets;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
using System.Threading;
using System.Timers;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using OpenSim.Framework.Inventory;
using OpenSim.RegionServer.world.scripting;
using OpenSim.Terrain;

namespace OpenSim.world
{
    public partial class World : WorldBase, ILocalStorageReceiver, IScriptAPI
    {
        protected System.Timers.Timer m_heartbeatTimer = new System.Timers.Timer();
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
        public World(Dictionary<uint, IClientAPI> clientThreads, RegionInfo regInfo)
        {
            try
            {
                updateLock = new Mutex(false);
                m_clientThreads = clientThreads;
                m_regInfo = regInfo;
                m_regionHandle = m_regInfo.RegionHandle;
                m_regionName = m_regInfo.RegionName;
                this.m_datastore = m_regInfo.DataStore;

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

        public void StartTimer()
        {
            m_heartbeatTimer.Enabled = true;
            m_heartbeatTimer.Interval = 100;
            m_heartbeatTimer.Elapsed += new ElapsedEventHandler(this.Heartbeat);
        }

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
        /// Performs per-frame updates regularly
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Heartbeat(object sender, System.EventArgs e)
        {
            this.Update();
        }

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

                foreach (IClientAPI client in m_clientThreads.Values)
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

                foreach (IClientAPI client in m_clientThreads.Values)
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

                    foreach (IClientAPI client in m_clientThreads.Values)
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
        public void GetInitialPrims(IClientAPI RemoteClient)
        {

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

        }

        public void AddNewPrim(Packet addPacket, IClientAPI agentClient)
        {
            AddNewPrim((ObjectAddPacket)addPacket, agentClient.AgentId);
        }

        public void AddNewPrim(ObjectAddPacket addPacket, LLUUID ownerID)
        {

        }

        #endregion

        #region Add/Remove Avatar Methods

        public override void AddNewAvatar(IClientAPI remoteClient, LLUUID agentID, bool child)
        {
            remoteClient.OnRegionHandShakeReply += new GenericCall(this.SendLayerData);
            //remoteClient.OnRequestWearables += new GenericCall(this.GetInitialPrims);

            Avatar newAvatar = null;
            try
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs:AddViewerAgent() - Creating new avatar for remote viewer agent");
                newAvatar = new Avatar(remoteClient, this, m_clientThreads, this.m_regInfo);
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs:AddViewerAgent() - Adding new avatar to world");
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.LOW, "World.cs:AddViewerAgent() - Starting RegionHandshake ");
                newAvatar.SendRegionHandshake();
               
                PhysicsVector pVec = new PhysicsVector(newAvatar.Pos.X, newAvatar.Pos.Y, newAvatar.Pos.Z);
                lock (this.LockPhysicsEngine)
                {
                    newAvatar.PhysActor = this.phyScene.AddAvatar(pVec);
                }
               
                lock (Entities)
                {
                    if (!Entities.ContainsKey(agentID))
                    {
                        this.Entities.Add(agentID, newAvatar);
                    }
                    else
                    {
                        Entities[agentID] = newAvatar;
                    }
                }
                lock (Avatars)
                {
                    if (Avatars.ContainsKey(agentID))
                    {
                        Avatars[agentID] = newAvatar;
                    }
                    else
                    {
                        this.Avatars.Add(agentID, newAvatar);
                    }
                }
            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.MEDIUM, "World.cs: AddViewerAgent() - Failed with exception " + e.ToString());
            }
            return;
        }

        public override void RemoveAvatar(LLUUID agentID)
        {
            return;
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
