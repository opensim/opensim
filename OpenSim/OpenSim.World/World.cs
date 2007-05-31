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
using OpenSim.Framework;
using OpenSim.RegionServer.world.scripting;
using OpenSim.Terrain;
using OpenGrid.Framework.Communications;

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
        protected AuthenticateSessionsBase authenticateHandler;
        protected RegionCommsHostBase regionCommsHost;
        protected RegionServerCommsManager commsManager;

        #region Properties
        /// <summary>
        /// 
        /// </summary>
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
        public World(Dictionary<uint, IClientAPI> clientThreads, RegionInfo regInfo, AuthenticateSessionsBase authen, RegionServerCommsManager commsMan)
        {
            try
            {
                updateLock = new Mutex(false);
                this.authenticateHandler = authen;
                this.commsManager = commsMan;
                m_clientThreads = clientThreads;
                m_regInfo = regInfo;
                m_regionHandle = m_regInfo.RegionHandle;
                m_regionName = m_regInfo.RegionName;
                this.m_datastore = m_regInfo.DataStore;
                this.RegisterRegionWithComms();

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

                Avatar.LoadAnims();
                
                //this.SetDefaultScripts();
                //this.LoadScriptEngines();


            }
            catch (Exception e)
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(OpenSim.Framework.Console.LogPriority.CRITICAL, "World.cs: Constructor failed with exception " + e.ToString());
            }
        }
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void StartTimer()
        {
            m_heartbeatTimer.Enabled = true;
            m_heartbeatTimer.Interval = 100;
            m_heartbeatTimer.Elapsed += new ElapsedEventHandler(this.Heartbeat);
        }

      
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

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addPacket"></param>
        /// <param name="agentClient"></param>
        public void AddNewPrim(Packet addPacket, IClientAPI agentClient)
        {
            AddNewPrim((ObjectAddPacket)addPacket, agentClient.AgentId);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addPacket"></param>
        /// <param name="ownerID"></param>
        public void AddNewPrim(ObjectAddPacket addPacket, LLUUID ownerID)
        {

        }

        #endregion

        #region Add/Remove Avatar Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="agentID"></param>
        /// <param name="child"></param>
        public override void AddNewAvatar(IClientAPI remoteClient, LLUUID agentID, bool child)
        {
            remoteClient.OnRegionHandShakeReply += new GenericCall(this.SendLayerData);
            //remoteClient.OnRequestWearables += new GenericCall(this.GetInitialPrims);
            remoteClient.OnChatFromViewer += new ChatFromViewer(this.SimChat);

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="agentID"></param>
        public override void RemoveAvatar(LLUUID agentID)
        {
            return;
        }
        #endregion

        #region Request Avatars List Methods
        //The idea is to have a group of method that return a list of avatars meeting some requirement
        // ie it could be all Avatars within a certain range of the calling prim/avatar. 

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
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

        #region RegionCommsHost

        public void RegisterRegionWithComms()
        {
            this.regionCommsHost = this.commsManager.RegisterRegion(this.m_regInfo);
            if (this.regionCommsHost != null)
            {
                this.regionCommsHost.OnExpectUser += new ExpectUserDelegate(this.NewUserConnection);
            }
        }

        public void NewUserConnection(ulong regionHandle,AgentCircuitData agent)
        {
            Console.WriteLine("World.cs - add new user connection"); 
            //should just check that its meant for this region 
            if (regionHandle == this.m_regInfo.RegionHandle)
            {
                this.authenticateHandler.AddNewCircuit(agent.circuitcode, agent);
            }
        }

        #endregion
    }
}
