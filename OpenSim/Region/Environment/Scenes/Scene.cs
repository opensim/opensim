/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Physics.Manager;
using OpenSim.Framework.Communications.Caches;
using OpenSim.Region.Environment.LandManagement;
using OpenSim.Region.Scripting;
using OpenSim.Region.Terrain;
using Caps=OpenSim.Region.Capabilities.Caps;
using Timer=System.Timers.Timer;

namespace OpenSim.Region.Environment.Scenes
{
    public delegate bool FilterAvatarList(ScenePresence avatar);

    public delegate void ForEachScenePresenceDelegate(ScenePresence presence);

    public partial class Scene : SceneBase, ILocalStorageReceiver
    {
        protected Timer m_heartbeatTimer = new Timer();
        protected Dictionary<LLUUID, ScenePresence> Avatars;
        protected Dictionary<LLUUID, SceneObject> Prims;
        protected PhysicsScene phyScene;
        protected float timeStep = 0.1f;
        private Random Rand = new Random();
        private uint _primCount = 702000;
        private Mutex _primAllocateMutex = new Mutex(false);
        private int storageCount;
        private int terrainCheckCount;
        private int landPrimCheckCount;
        private Mutex updateLock;

        protected StorageManager storageManager;
        protected AgentCircuitManager authenticateHandler;
        protected RegionCommsListener regionCommsHost;
        protected CommunicationsManager commsManager;

        protected Dictionary<LLUUID, Caps> capsHandlers = new Dictionary<LLUUID, Caps>();
        protected BaseHttpServer httpListener;

        #region Properties

        /// <summary>
        /// 
        /// </summary>
        public PhysicsScene PhysScene
        {
            set { phyScene = value; }
            get { return (phyScene); }
        }

        private LandManager m_LandManager;

        public LandManager LandManager
        {
            get { return m_LandManager; }
        }

        private EstateManager m_estateManager;

        public EstateManager EstateManager
        {
            get { return m_estateManager; }
        }

        private ScriptManager m_scriptManager;

        public ScriptManager ScriptManager
        {
            get { return m_scriptManager; }
        }

        public Dictionary<LLUUID, SceneObject> Objects
        {
            get { return Prims; }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Creates a new World class, and a region to go with it.
        /// </summary>
        /// <param name="clientThreads">Dictionary to contain client threads</param>
        /// <param name="regionHandle">Region Handle for this region</param>
        /// <param name="regionName">Region Name for this region</param>
        public Scene(RegionInfo regInfo, AgentCircuitManager authen, CommunicationsManager commsMan,
                     AssetCache assetCach, StorageManager storeManager, BaseHttpServer httpServer)
        {
            updateLock = new Mutex(false);
            authenticateHandler = authen;
            commsManager = commsMan;
            storageManager = storeManager;
            assetCache = assetCach;
            m_regInfo = regInfo;
            m_regionHandle = m_regInfo.RegionHandle;
            m_regionName = m_regInfo.RegionName;
            m_datastore = m_regInfo.DataStore;
            RegisterRegionWithComms();

            m_LandManager = new LandManager(this, m_regInfo);
            m_estateManager = new EstateManager(this, m_regInfo);
            m_scriptManager = new ScriptManager(this);
            m_eventManager = new EventManager();

            m_eventManager.OnParcelPrimCountAdd +=
                m_LandManager.addPrimToLandPrimCounts;

            MainLog.Instance.Verbose("Creating new entitities instance");
            Entities = new Dictionary<LLUUID, EntityBase>();
            Avatars = new Dictionary<LLUUID, ScenePresence>();
            Prims = new Dictionary<LLUUID, SceneObject>();

            MainLog.Instance.Verbose("Loading objects from datastore");
            List<SceneObject> PrimsFromDB = storageManager.DataStore.LoadObjects();
            foreach (SceneObject prim in PrimsFromDB)
            {
                AddEntity(prim);
            }
            MainLog.Instance.Verbose("Loaded " + PrimsFromDB.Count.ToString() + " object(s)");


            MainLog.Instance.Verbose("Creating LandMap");
            Terrain = new TerrainEngine();

            ScenePresence.LoadAnims();

            httpListener = httpServer;
        }

        #endregion

        #region Script Handling Methods

        public void SendCommandToScripts(string[] args)
        {
            m_eventManager.TriggerOnScriptConsole(args);
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void StartTimer()
        {
            m_heartbeatTimer.Enabled = true;
            m_heartbeatTimer.Interval = 100;
            m_heartbeatTimer.Elapsed += new ElapsedEventHandler(Heartbeat);
        }

        #region Update Methods

        /// <summary>
        /// Performs per-frame updates regularly
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Heartbeat(object sender, EventArgs e)
        {
            Update();
        }

        /// <summary>
        /// Performs per-frame updates on the scene, this should be the central world loop
        /// </summary>
        public override void Update()
        {
            updateLock.WaitOne();
            try
            {
                if (phyScene.IsThreaded)
                {
                    phyScene.GetResults();
                }

                List<EntityBase> moveEntities = new List<EntityBase>( Entities.Values );

                foreach (EntityBase entity in moveEntities)
                {
                    entity.UpdateMovement();
                }

                lock (m_syncRoot)
                {
                    phyScene.Simulate(timeStep);
                }

                List<EntityBase> updateEntities = new List<EntityBase>(Entities.Values);

                foreach (EntityBase entity in updateEntities)
                {
                    entity.Update();
                }

                // General purpose event manager
                m_eventManager.TriggerOnFrame();

                //backup world data
                storageCount++;
                if (storageCount > 1200) //set to how often you want to backup 
                {
                    Backup();
                    storageCount = 0;
                }

                terrainCheckCount++;
                if (terrainCheckCount >= 50)
                {
                    terrainCheckCount = 0;

                    if (Terrain.Tainted())
                    {
                        lock (Terrain.heightmap)
                        {
                            lock (m_syncRoot)
                            {
                                phyScene.SetTerrain(Terrain.GetHeights1D());
                            }

                            storageManager.DataStore.StoreTerrain(Terrain.GetHeights2DD());

                            float[] terData = Terrain.GetHeights1D();

                            ForEachScenePresence(delegate(ScenePresence presence)
                                                     {
                                                         for (int x = 0; x < 16; x++)
                                                         {
                                                             for (int y = 0; y < 16; y++)
                                                             {
                                                                 if (Terrain.Tainted(x * 16, y * 16))
                                                                 {
                                                                     SendLayerData(x, y, presence.ControllingClient, terData);
                                                                 }
                                                             }
                                                         }
                                                     });

                            foreach (LLUUID UUID in Entities.Keys)
                            {
                                Entities[UUID].LandRenegerated();
                            }

                            Terrain.ResetTaint();
                        }
                    }
                }

                landPrimCheckCount++;
                if (landPrimCheckCount > 50) //check every 5 seconds for tainted prims
                {
                    if (m_LandManager.landPrimCountTainted)
                    {
                        //Perform land update of prim count
                        performParcelPrimCountUpdate();
                        landPrimCheckCount = 0;
                    }
                }
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("scene", "World.cs: Update() - Failed with exception " + e.ToString());
            }
            updateLock.ReleaseMutex();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool Backup()
        {
            EventManager.TriggerOnBackup(storageManager.DataStore);
            return true;
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
                Terrain.HillsGenerator();

                lock (m_syncRoot)
                {
                    phyScene.SetTerrain(Terrain.GetHeights1D());
                }

                storageManager.DataStore.StoreTerrain(Terrain.GetHeights2DD());

                ForEachScenePresence(delegate(ScenePresence presence)
                                         {
                                             SendLayerData(presence.ControllingClient);
                                         });

                foreach (LLUUID UUID in Entities.Keys)
                {
                    Entities[UUID].LandRenegerated();
                }
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("terrain", "World.cs: RegenerateTerrain() - Failed with exception " + e.ToString());
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
                Terrain.SetHeights2D(newMap);
                lock (m_syncRoot)
                {
                    phyScene.SetTerrain(Terrain.GetHeights1D());
                }
                storageManager.DataStore.StoreTerrain(Terrain.GetHeights2DD());

                ForEachScenePresence(delegate(ScenePresence presence)
                                         {
                                             SendLayerData(presence.ControllingClient);
                                         });

                foreach (LLUUID UUID in Entities.Keys)
                {
                    Entities[UUID].LandRenegerated();
                }
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("terrain", "World.cs: RegenerateTerrain() - Failed with exception " + e.ToString());
            }
        }

        #endregion

        #region Load Terrain

        /// <summary>
        /// Loads the World heightmap
        /// </summary>
        /// 
        public override void LoadWorldMap()
        {
            try
            {
                double[,] map = storageManager.DataStore.LoadTerrain();
                if (map == null)
                {
                    if (string.IsNullOrEmpty(m_regInfo.estateSettings.terrainFile))
                    {
                        Console.WriteLine("No default terrain, procedurally generating...");
                        Terrain.HillsGenerator();

                        storageManager.DataStore.StoreTerrain(Terrain.GetHeights2DD());
                    }
                    else
                    {
                        try
                        {
                            Terrain.LoadFromFileF32(m_regInfo.estateSettings.terrainFile);
                            Terrain *= m_regInfo.estateSettings.terrainMultiplier;
                        }
                        catch
                        {
                            Console.WriteLine("Unable to load default terrain, procedurally generating instead...");
                            Terrain.HillsGenerator();
                        }
                        storageManager.DataStore.StoreTerrain(Terrain.GetHeights2DD());
                    }
                }
                else
                {
                    Terrain.SetHeights2D(map);
                }

                CreateTerrainTexture();
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("terrain", "World.cs: LoadWorldMap() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void CreateTerrainTexture()
        {
            //create a texture asset of the terrain 
            byte[] data = Terrain.ExportJpegImage("defaultstripe.png");
            m_regInfo.estateSettings.terrainImageID = LLUUID.Random();
            AssetBase asset = new AssetBase();
            asset.FullID = m_regInfo.estateSettings.terrainImageID;
            asset.Data = data;
            asset.Name = "terrainImage";
            asset.Type = 0;
            assetCache.AddAsset(asset);
        }

        #endregion

        #region Primitives Methods

        /// <summary>
        /// Loads the World's objects
        /// </summary>
        public void LoadPrimsFromStorage()
        {
            MainLog.Instance.Verbose("World.cs: LoadPrimsFromStorage() - Loading primitives");
            List<SceneObject> NewObjectsList = storageManager.DataStore.LoadObjects();
            foreach (SceneObject obj in NewObjectsList)
            {
                this.Objects.Add(obj.rootUUID, obj);
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
        /// Returns a new unallocated primitive ID
        /// </summary>
        /// <returns>A brand new primitive ID</returns>
        public uint PrimIDAllocate()
        {
            uint myID;

            _primAllocateMutex.WaitOne();
            ++_primCount;
            myID = _primCount;
            _primAllocateMutex.ReleaseMutex();

            return myID;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="addPacket"></param>
        /// <param name="ownerID"></param>
        public void AddNewPrim(LLUUID ownerID, LLVector3 pos, PrimitiveBaseShape shape)
        {
            SceneObject sceneOb = new SceneObject(this, m_eventManager, ownerID, PrimIDAllocate(), pos, shape);
            AddEntity(sceneOb);
        }

        public void RemovePrim(uint localID, LLUUID avatar_deleter)
        {
            foreach (EntityBase obj in Entities.Values)
            {
                if (obj is SceneObject)
                {
                    if (((SceneObject) obj).LocalId == localID)
                    {
                        RemoveEntity((SceneObject) obj);
                        return;
                    }
                }
            }
        }

        public void AddEntity(SceneObject sceneObject)
        {
            Entities.Add(sceneObject.rootUUID, sceneObject);
        }

        public void RemoveEntity(SceneObject sceneObject)
        {
            if (Entities.ContainsKey(sceneObject.rootUUID))
            {
                m_LandManager.removePrimFromLandPrimCounts(sceneObject);
                Entities.Remove(sceneObject.rootUUID);
                m_LandManager.setPrimsTainted();
            }
        }

        /// <summary>
        /// Called by a prim when it has been created/cloned, so that its events can be subscribed to
        /// </summary>
        /// <param name="prim"></param>
        public void AcknowledgeNewPrim(Primitive prim)
        {
            prim.OnPrimCountTainted += m_LandManager.setPrimsTainted;
        }

        #endregion

        #region Add/Remove Avatar Methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param          
        /// <param name="agentID"></param>
        /// <param name="child"></param>
        public override void AddNewClient(IClientAPI client, bool child)
        {
            SubscribeToClientEvents(client);
            m_estateManager.sendRegionHandshake(client);
            CreateAndAddScenePresence(client);
            m_LandManager.sendParcelOverlay(client);
            //commsManager.UserProfiles.AddNewUser(client.AgentId);
        }

        protected virtual void SubscribeToClientEvents(IClientAPI client)
        {
            client.OnRegionHandShakeReply += SendLayerData;
            //remoteClient.OnRequestWearables += new GenericCall(this.GetInitialPrims);
            client.OnModifyTerrain += ModifyTerrain;
            client.OnChatFromViewer += SimChat;
            client.OnInstantMessage += InstantMessage;
            client.OnRequestWearables += InformClientOfNeighbours;
            client.OnAddPrim += AddNewPrim;
            client.OnUpdatePrimGroupPosition += UpdatePrimPosition;
            client.OnUpdatePrimSinglePosition += UpdatePrimSinglePosition;
            client.OnUpdatePrimGroupRotation += UpdatePrimRotation;
            client.OnUpdatePrimGroupMouseRotation += UpdatePrimRotation;
            client.OnUpdatePrimSingleRotation += UpdatePrimSingleRotation;
            client.OnUpdatePrimScale += UpdatePrimScale;
            client.OnUpdateExtraParams += UpdateExtraParam;
            client.OnUpdatePrimShape += UpdatePrimShape;
            client.OnRequestMapBlocks += RequestMapBlocks;
            client.OnUpdatePrimTexture += UpdatePrimTexture;
            client.OnTeleportLocationRequest += RequestTeleportLocation;
            client.OnObjectSelect += SelectPrim;
            client.OnObjectDeselect += DeselectPrim;
            client.OnGrapUpdate += MoveObject;
            client.OnNameFromUUIDRequest += commsManager.HandleUUIDNameRequest;
            client.OnObjectDescription += PrimDescription;
            client.OnObjectName += PrimName;
            client.OnLinkObjects += LinkObjects;
            client.OnObjectDuplicate += DuplicateObject;
            client.OnModifyTerrain += ModifyTerrain;

            client.OnParcelPropertiesRequest += new ParcelPropertiesRequest(m_LandManager.handleParcelPropertiesRequest);
            client.OnParcelDivideRequest += new ParcelDivideRequest(m_LandManager.handleParcelDivideRequest);
            client.OnParcelJoinRequest += new ParcelJoinRequest(m_LandManager.handleParcelJoinRequest);
            client.OnParcelPropertiesUpdateRequest +=
                new ParcelPropertiesUpdateRequest(m_LandManager.handleParcelPropertiesUpdateRequest);
            client.OnParcelSelectObjects += new ParcelSelectObjects(m_LandManager.handleParcelSelectObjectsRequest);
            client.OnParcelObjectOwnerRequest +=
                new ParcelObjectOwnerRequest(m_LandManager.handleParcelObjectOwnersRequest);

            client.OnEstateOwnerMessage += new EstateOwnerMessageRequest(m_estateManager.handleEstateOwnerMessage);
            
           // client.OnCreateNewInventoryItem += CreateNewInventoryItem;
            //client.OnCreateNewInventoryFolder += commsManager.UserProfiles.HandleCreateInventoryFolder;
            client.OnFetchInventoryDescendents += commsManager.UserProfiles.HandleFecthInventoryDescendents;
            client.OnRequestTaskInventory += RequestTaskInventory;
        }

        protected ScenePresence CreateAndAddScenePresence(IClientAPI client)
        {
            ScenePresence newAvatar = null;

            MainLog.Instance.Verbose("World.cs:AddViewerAgent() - Creating new avatar for remote viewer agent");
            newAvatar = new ScenePresence(client, this, m_regInfo);
            MainLog.Instance.Verbose("World.cs:AddViewerAgent() - Adding new avatar to world");
            MainLog.Instance.Verbose("World.cs:AddViewerAgent() - Starting RegionHandshake ");

            PhysicsVector pVec = new PhysicsVector(newAvatar.Pos.X, newAvatar.Pos.Y, newAvatar.Pos.Z);
            lock (m_syncRoot)
            {
                newAvatar.PhysActor = phyScene.AddAvatar(pVec);
            }

            lock (Entities)
            {
                if (!Entities.ContainsKey(client.AgentId))
                {
                    Entities.Add(client.AgentId, newAvatar);
                }
                else
                {
                    Entities[client.AgentId] = newAvatar;
                }
            }
            lock (Avatars)
            {
                if (Avatars.ContainsKey(client.AgentId))
                {
                    Avatars[client.AgentId] = newAvatar;
                }
                else
                {
                    Avatars.Add(client.AgentId, newAvatar);
                }
            }
            newAvatar.OnSignificantClientMovement += m_LandManager.handleSignificantClientMovement;
            return newAvatar;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="agentID"></param>
        public override void RemoveClient(LLUUID agentID)
        {
            m_eventManager.TriggerOnRemovePresence(agentID);

            ScenePresence avatar = RequestAvatar(agentID);

            ForEachScenePresence(
                delegate(ScenePresence presence)
                    {
                        presence.ControllingClient.SendKillObject(avatar.RegionHandle, avatar.LocalId);
                    });

            lock (Avatars)
            {
                if (Avatars.ContainsKey(agentID))
                {
                    Avatars.Remove(agentID);
                }
            }
            lock (Entities)
            {
                if (Entities.ContainsKey(agentID))
                {
                    Entities.Remove(agentID);
                }
            }
            // TODO: Add the removal from physics ?


            return;
        }

        #endregion

        #region Request Avatars List Methods

        //The idea is to have a group of method that return a list of avatars meeting some requirement
        // ie it could be all Avatars within a certain range of the calling prim/avatar. 

        /// <summary>
        /// Request a List of all Avatars in this World
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> RequestAvatarList()
        {
            List<ScenePresence> result = new List<ScenePresence>();

            foreach (ScenePresence avatar in Avatars.Values)
            {
                result.Add(avatar);
            }

            return result;
        }

        /// <summary>
        /// Request a filtered list of Avatars in this World
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> RequestAvatarList(FilterAvatarList filter)
        {
            List<ScenePresence> result = new List<ScenePresence>();

            foreach (ScenePresence avatar in Avatars.Values)
            {
                if (filter(avatar))
                {
                    result.Add(avatar);
                }
            }

            return result;
        }

        /// <summary>
        /// Request a Avatar by UUID
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns></returns>
        public ScenePresence RequestAvatar(LLUUID avatarID)
        {
            if (Avatars.ContainsKey(avatarID))
            {
                return Avatars[avatarID];
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="whatToDo"></param>
        public void ForEachScenePresence(ForEachScenePresenceDelegate whatToDo)
        {
            foreach (ScenePresence presence in Avatars.Values)
            {
                whatToDo(presence);
            }
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entID"></param>
        /// <returns></returns>
        public bool DeleteEntity(LLUUID entID)
        {
            if (Entities.ContainsKey(entID))
            {
                Entities.Remove(entID);
                return true;
            }
            return false;
        }

        public void SendAllSceneObjectsToClient(IClientAPI client)
        {
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObject)
                {
                    ((SceneObject) ent).SendAllChildPrimsToClient(client);
                }
            }
        }

        #region RegionCommsHost

        /// <summary>
        /// 
        /// </summary>
        public void RegisterRegionWithComms()
        {
            regionCommsHost = commsManager.GridServer.RegisterRegion(m_regInfo);
            if (regionCommsHost != null)
            {
                regionCommsHost.OnExpectUser += NewUserConnection;
                regionCommsHost.OnAvatarCrossingIntoRegion += AgentCrossing;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agent"></param>
        public void NewUserConnection(ulong regionHandle, AgentCircuitData agent)
        {
            // Console.WriteLine("World.cs - add new user connection");
            //should just check that its meant for this region 
            if (regionHandle == m_regInfo.RegionHandle)
            {
                if (agent.CapsPath != "")
                {
                    //Console.WriteLine("new user, so creating caps handler for it");
                    Caps cap =
                        new Caps(assetCache, httpListener, m_regInfo.ExternalHostName, m_regInfo.ExternalEndPoint.Port,
                                 agent.CapsPath, agent.AgentID);
                    cap.RegisterHandlers();
                    if (capsHandlers.ContainsKey(agent.AgentID))
                    {
                        MainLog.Instance.Warn("client", "Adding duplicate CAPS entry for user " +
                                              agent.AgentID.ToStringHyphenated());
                        capsHandlers[agent.AgentID] = cap;
                    }
                    else
                    {
                        capsHandlers.Add(agent.AgentID, cap);
                    }
                }
                authenticateHandler.AddNewCircuit(agent.circuitcode, agent);
            }
        }

        public void AgentCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position, bool isFlying)
        {
            if (regionHandle == m_regInfo.RegionHandle)
            {
                if (Avatars.ContainsKey(agentID))
                {
                    Avatars[agentID].MakeAvatar(position, isFlying);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void InformClientOfNeighbours(IClientAPI remoteClient)
        {
            List<RegionInfo> neighbours = commsManager.GridServer.RequestNeighbours(m_regInfo);

            if (neighbours != null)
            {
                for (int i = 0; i < neighbours.Count; i++)
                {
                    AgentCircuitData agent = remoteClient.RequestClientInfo();
                    agent.BaseFolder = LLUUID.Zero;
                    agent.InventoryFolder = LLUUID.Zero;
                    agent.startpos = new LLVector3(128, 128, 70);
                    agent.child = true;
                    commsManager.InterRegion.InformRegionOfChildAgent(neighbours[i].RegionHandle, agent);
                    remoteClient.InformClientOfNeighbour(neighbours[i].RegionHandle, neighbours[i].ExternalEndPoint);
                    //this.capsHandlers[remoteClient.AgentId].CreateEstablishAgentComms("", System.Net.IPAddress.Parse(neighbours[i].CommsIPListenAddr) + ":" + neighbours[i].CommsIPListenPort);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionInfo RequestNeighbouringRegionInfo(ulong regionHandle)
        {
            return commsManager.GridServer.RequestNeighbourInfo(regionHandle);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        public void RequestMapBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY)
        {
            List<MapBlockData> mapBlocks;
            mapBlocks = commsManager.GridServer.RequestNeighbourMapBlocks(minX, minY, maxX, maxY);
            remoteClient.SendMapBlock(mapBlocks);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="RegionHandle"></param>
        /// <param name="position"></param>
        /// <param name="lookAt"></param>
        /// <param name="flags"></param>
        public void RequestTeleportLocation(IClientAPI remoteClient, ulong regionHandle, LLVector3 position,
                                            LLVector3 lookAt, uint flags)
        {
            if (regionHandle == m_regionHandle)
            {
                if (Avatars.ContainsKey(remoteClient.AgentId))
                {
                    remoteClient.SendTeleportLocationStart();
                    remoteClient.SendLocalTeleport(position, lookAt, flags);
                    Avatars[remoteClient.AgentId].Teleport(position);
                }
            }
            else
            {
                RegionInfo reg = RequestNeighbouringRegionInfo(regionHandle);
                if (reg != null)
                {
                    remoteClient.SendTeleportLocationStart();
                    AgentCircuitData agent = remoteClient.RequestClientInfo();
                    agent.BaseFolder = LLUUID.Zero;
                    agent.InventoryFolder = LLUUID.Zero;
                    agent.startpos = new LLVector3(128, 128, 70);
                    agent.child = true;
                    commsManager.InterRegion.InformRegionOfChildAgent(regionHandle, agent);
                    commsManager.InterRegion.ExpectAvatarCrossing(regionHandle, remoteClient.AgentId, position, false);

                    remoteClient.SendRegionTeleport(regionHandle, 13, reg.ExternalEndPoint, 4, (1 << 4));
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionhandle"></param>
        /// <param name="agentID"></param>
        /// <param name="position"></param>
        public bool InformNeighbourOfCrossing(ulong regionhandle, LLUUID agentID, LLVector3 position, bool isFlying)
        {
            return commsManager.InterRegion.ExpectAvatarCrossing(regionhandle, agentID, position, isFlying);
        }

        public void performParcelPrimCountUpdate()
        {
            m_LandManager.resetAllLandPrimCounts();
            m_eventManager.TriggerParcelPrimCountUpdate();
            m_LandManager.finalizeLandPrimCountUpdate();
            m_LandManager.landPrimCountTainted = false;
        }

        #endregion

        #region Alert Methods
        public void SendGeneralAlert(string message)
        {
            foreach (ScenePresence presence in this.Avatars.Values)
            {
                presence.ControllingClient.SendAlertMessage(message);
            }
        }

        public void SendAlertToUser(LLUUID agentID, string message, bool modal)
        {
            if (this.Avatars.ContainsKey(agentID))
            {
                this.Avatars[agentID].ControllingClient.SendAgentAlertMessage(message, modal);
            }
        }

        public void SendAlertToUser(string firstName, string lastName, string message, bool modal)
        {
            foreach (ScenePresence presence in this.Avatars.Values)
            {
                if ((presence.firstname == firstName) && (presence.lastname == lastName))
                {
                    presence.ControllingClient.SendAgentAlertMessage(message, modal);
                    break;
                }
            }
        }

        public void HandleAlertCommand(string[] commandParams)
        {
            if (commandParams[0] == "general")
            {
                string message = this.CombineParams(commandParams, 1);
                this.SendGeneralAlert(message);
            }
            else
            {
                string message = this.CombineParams(commandParams, 2);
                this.SendAlertToUser(commandParams[0], commandParams[1], message, false);
            }
        }

        private string CombineParams(string[] commandParams, int pos)
        {
            string result = "";
            for (int i = pos; i < commandParams.Length; i++)
            {
                result += commandParams[i]+ " ";
            }
            return result;
        }
        #endregion
    }
}
