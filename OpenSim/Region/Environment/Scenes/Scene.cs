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
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Physics.Manager;
using OpenSim.Region.Caches;
using OpenSim.Region.Interfaces;
using OpenSim.Region.Scripting;
using OpenSim.Region.Terrain;
using Caps = OpenSim.Region.Capabilities.Caps;
using Timer = System.Timers.Timer;

using OpenSim.Region.Environment.LandManagement;

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
        private System.Threading.Mutex _primAllocateMutex = new Mutex(false);
        private int storageCount;
        private int landPrimCheckCount;
        private Mutex updateLock;

        protected AuthenticateSessionsBase authenticateHandler;
        protected RegionCommsListener regionCommsHost;
        protected CommunicationsManager commsManager;
        protected StorageManager storageManager;

        protected Dictionary<LLUUID, Caps> capsHandlers = new Dictionary<LLUUID, Caps>();
        protected BaseHttpServer httpListener;

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

        private EventManager m_eventManager;
        public EventManager EventManager
        {
            get { return m_eventManager; }
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
        public Scene(RegionInfo regInfo, AuthenticateSessionsBase authen, CommunicationsManager commsMan, AssetCache assetCach, StorageManager storeManager, BaseHttpServer httpServer)
        {
            updateLock = new Mutex(false);
            this.authenticateHandler = authen;
            this.commsManager = commsMan;
            this.storageManager = storeManager;
            this.assetCache = assetCach;
            m_regInfo = regInfo;
            m_regionHandle = m_regInfo.RegionHandle;
            m_regionName = m_regInfo.RegionName;
            this.m_datastore = m_regInfo.DataStore;
            this.RegisterRegionWithComms();

            m_LandManager = new LandManager(this, this.m_regInfo);
            m_estateManager = new EstateManager(this, this.m_regInfo);
            m_scriptManager = new ScriptManager(this);
            m_eventManager = new EventManager();

            m_eventManager.OnParcelPrimCountAdd += new EventManager.OnParcelPrimCountAddDelegate(m_LandManager.addPrimToLandPrimCounts);

            MainLog.Instance.Verbose("World.cs - creating new entitities instance");
            Entities = new Dictionary<LLUUID, EntityBase>();
            Avatars = new Dictionary<LLUUID, ScenePresence>();
            Prims = new Dictionary<LLUUID, SceneObject>();

            MainLog.Instance.Verbose("World.cs - loading objects from datastore");
            List<SceneObject> PrimsFromDB = storageManager.DataStore.LoadObjects();
            foreach (SceneObject prim in PrimsFromDB)
            {
                AddEntity(prim);
            }
            MainLog.Instance.Verbose("World.cs - loaded " + PrimsFromDB.Count.ToString() + " object(s)");


            MainLog.Instance.Verbose("World.cs - creating LandMap");
            Terrain = new TerrainEngine();

            ScenePresence.LoadAnims();

            this.httpListener = httpServer;
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
            m_heartbeatTimer.Elapsed += new ElapsedEventHandler(this.Heartbeat);
        }


        #region Update Methods


        /// <summary>
        /// Performs per-frame updates regularly
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Heartbeat(object sender, EventArgs e)
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

                foreach (LLUUID UUID in Entities.Keys)
                {
                    Entities[UUID].updateMovement();
                }

                lock (this.m_syncRoot)
                {
                    this.phyScene.Simulate(timeStep);
                }

                foreach (LLUUID UUID in Entities.Keys)
                {
                    Entities[UUID].update();
                }

                // General purpose event manager
                m_eventManager.TriggerOnFrame();

                //backup world data
                this.storageCount++;
                if (storageCount > 1200) //set to how often you want to backup 
                {
                    this.Backup();
                    storageCount = 0;
                }

                this.landPrimCheckCount++;
                if (this.landPrimCheckCount > 50) //check every 5 seconds for tainted prims
                {
                    if (m_LandManager.landPrimCountTainted)
                    {
                        //Perform land update of prim count
                        performParcelPrimCountUpdate();
                        this.landPrimCheckCount = 0;
                    }
                }

            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("World.cs: Update() - Failed with exception " + e.ToString());
            }
            updateLock.ReleaseMutex();

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool Backup()
        {
            EventManager.TriggerOnBackup(this.storageManager.DataStore);
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
                Terrain.hills();

                lock (this.m_syncRoot)
                {
                    this.phyScene.SetTerrain(Terrain.getHeights1D());
                }

                this.storageManager.DataStore.StoreTerrain(Terrain.getHeights2DD());

                this.ForEachScenePresence(delegate(ScenePresence presence)
                                                  {
                                                      this.SendLayerData(presence.ControllingClient);
                                                  });

                foreach (LLUUID UUID in Entities.Keys)
                {
                    Entities[UUID].LandRenegerated();
                }
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("World.cs: RegenerateTerrain() - Failed with exception " + e.ToString());
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
                lock (this.m_syncRoot)
                {
                    this.phyScene.SetTerrain(this.Terrain.getHeights1D());
                }
                this.storageManager.DataStore.StoreTerrain(Terrain.getHeights2DD());

                this.ForEachScenePresence(delegate(ScenePresence presence)
                                                  {
                                                      this.SendLayerData(presence.ControllingClient);
                                                  });

                foreach (LLUUID UUID in Entities.Keys)
                {
                    Entities[UUID].LandRenegerated();
                }
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("World.cs: RegenerateTerrain() - Failed with exception " + e.ToString());
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

                    this.ForEachScenePresence(delegate(ScenePresence presence)
                                                      {
                                                          this.SendLayerData(pointx, pointy, presence.ControllingClient);
                                                      });
                }
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("World.cs: RegenerateTerrain() - Failed with exception " + e.ToString());
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
                double[,] map = this.storageManager.DataStore.LoadTerrain();
                if (map == null)
                {
                    if (string.IsNullOrEmpty(this.m_regInfo.estateSettings.terrainFile))
                    {
                        Console.WriteLine("No default terrain, procedurally generating...");
                        this.Terrain.hills();

                        this.storageManager.DataStore.StoreTerrain(this.Terrain.getHeights2DD());
                    }
                    else
                    {
                        try
                        {
                            this.Terrain.loadFromFileF32(this.m_regInfo.estateSettings.terrainFile);
                            this.Terrain *= this.m_regInfo.estateSettings.terrainMultiplier;
                        }
                        catch
                        {
                            Console.WriteLine("Unable to load default terrain, procedurally generating instead...");
                            Terrain.hills();
                        }
                        this.storageManager.DataStore.StoreTerrain(this.Terrain.getHeights2DD());
                    }
                }
                else
                {
                    this.Terrain.setHeights2D(map);
                }

                CreateTerrainTexture();

            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("World.cs: LoadWorldMap() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void CreateTerrainTexture()
        {
            //create a texture asset of the terrain 
            byte[] data = this.Terrain.exportJpegImage("defaultstripe.png");
            this.m_regInfo.estateSettings.terrainImageID = LLUUID.Random();
            AssetBase asset = new AssetBase();
            asset.FullID = this.m_regInfo.estateSettings.terrainImageID;
            asset.Data = data;
            asset.Name = "terrainImage";
            asset.Type = 0;
            this.assetCache.AddAsset(asset);
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
                MainLog.Instance.Verbose("World.cs: LoadPrimsFromStorage() - Loading primitives");
                this.localStorage.LoadPrimitives(this);
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("World.cs: LoadPrimsFromStorage() - Failed with exception " + e.ToString());
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

            SceneObject sceneOb = new SceneObject(this, m_eventManager, ownerID, this.PrimIDAllocate(), pos, shape);
            AddEntity(sceneOb);
        }

        public void RemovePrim(uint localID, LLUUID avatar_deleter)
        {
            foreach (EntityBase obj in Entities.Values)
            {
                if (obj is SceneObject)
                {
                    if (((SceneObject)obj).LocalId == localID)
                    {
                        RemoveEntity((SceneObject)obj);
                        return;
                    }
                }
            }

        }

        public void AddEntity(SceneObject sceneObject)
        {
            this.Entities.Add(sceneObject.rootUUID, sceneObject);
        }

        public void RemoveEntity(SceneObject sceneObject)
        {
            if (this.Entities.ContainsKey(sceneObject.rootUUID))
            {
                m_LandManager.removePrimFromLandPrimCounts(sceneObject);
                this.Entities.Remove(sceneObject.rootUUID);
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
            this.m_estateManager.sendRegionHandshake(client);
            CreateAndAddScenePresence(client);
            this.m_LandManager.sendParcelOverlay(client);

        }

        protected virtual void SubscribeToClientEvents(IClientAPI client)
        {
            client.OnRegionHandShakeReply += this.SendLayerData;
            //remoteClient.OnRequestWearables += new GenericCall(this.GetInitialPrims);
            client.OnChatFromViewer += this.SimChat;
            client.OnInstantMessage += this.InstantMessage;
            client.OnRequestWearables += this.InformClientOfNeighbours;
            client.OnAddPrim += this.AddNewPrim;
            client.OnUpdatePrimGroupPosition += this.UpdatePrimPosition;
            client.OnUpdatePrimSinglePosition += this.UpdatePrimSinglePosition;
            client.OnUpdatePrimGroupRotation += this.UpdatePrimRotation;
            client.OnUpdatePrimGroupMouseRotation += this.UpdatePrimRotation;
            client.OnUpdatePrimSingleRotation += this.UpdatePrimSingleRotation;
            client.OnUpdatePrimScale += this.UpdatePrimScale;
            client.OnUpdatePrimShape += this.UpdatePrimShape;
            client.OnRequestMapBlocks += this.RequestMapBlocks;
            client.OnUpdatePrimTexture += this.UpdatePrimTexture;
            client.OnTeleportLocationRequest += this.RequestTeleportLocation;
            client.OnObjectSelect += this.SelectPrim;
            client.OnObjectDeselect += this.DeselectPrim;
            client.OnGrapUpdate += this.MoveObject;
            client.OnNameFromUUIDRequest += this.commsManager.HandleUUIDNameRequest;
            client.OnObjectDescription += this.PrimDescription;
            client.OnObjectName += this.PrimName;
            client.OnLinkObjects += this.LinkObjects;
            client.OnObjectDuplicate += this.DuplicateObject;

            client.OnParcelPropertiesRequest += new ParcelPropertiesRequest(m_LandManager.handleParcelPropertiesRequest);
            client.OnParcelDivideRequest += new ParcelDivideRequest(m_LandManager.handleParcelDivideRequest);
            client.OnParcelJoinRequest += new ParcelJoinRequest(m_LandManager.handleParcelJoinRequest);
            client.OnParcelPropertiesUpdateRequest += new ParcelPropertiesUpdateRequest(m_LandManager.handleParcelPropertiesUpdateRequest);
            client.OnParcelSelectObjects += new ParcelSelectObjects(m_LandManager.handleParcelSelectObjectsRequest);
            client.OnParcelObjectOwnerRequest += new ParcelObjectOwnerRequest(m_LandManager.handleParcelObjectOwnersRequest);

            client.OnEstateOwnerMessage += new EstateOwnerMessageRequest(m_estateManager.handleEstateOwnerMessage);

        }

        protected ScenePresence CreateAndAddScenePresence(IClientAPI client)
        {
            ScenePresence newAvatar = null;

            MainLog.Instance.Verbose("World.cs:AddViewerAgent() - Creating new avatar for remote viewer agent");
            newAvatar = new ScenePresence(client, this, this.m_regInfo);
            MainLog.Instance.Verbose("World.cs:AddViewerAgent() - Adding new avatar to world");
            MainLog.Instance.Verbose("World.cs:AddViewerAgent() - Starting RegionHandshake ");

            PhysicsVector pVec = new PhysicsVector(newAvatar.Pos.X, newAvatar.Pos.Y, newAvatar.Pos.Z);
            lock (this.m_syncRoot)
            {
                newAvatar.PhysActor = this.phyScene.AddAvatar(pVec);
            }

            lock (Entities)
            {
                if (!Entities.ContainsKey(client.AgentId))
                {
                    this.Entities.Add(client.AgentId, newAvatar);
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
                    this.Avatars.Add(client.AgentId, newAvatar);
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

            ScenePresence avatar = this.RequestAvatar(agentID);

            this.ForEachScenePresence(
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
            if (this.Avatars.ContainsKey(avatarID))
            {
                return Avatars[avatarID];
            }
            return null;
        }

        public void ForEachScenePresence(ForEachScenePresenceDelegate whatToDo)
        {
            foreach (ScenePresence presence in this.Avatars.Values)
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
            if (this.Entities.ContainsKey(entID))
            {
                this.Entities.Remove(entID);
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
                    ((SceneObject)ent).SendAllChildPrimsToClient(client);
                }
            }
        }

        #region RegionCommsHost

        /// <summary>
        /// 
        /// </summary>
        public void RegisterRegionWithComms()
        {

            this.regionCommsHost = this.commsManager.GridServer.RegisterRegion(this.m_regInfo);
            if (this.regionCommsHost != null)
            {
                this.regionCommsHost.OnExpectUser += this.NewUserConnection;
                this.regionCommsHost.OnAvatarCrossingIntoRegion += this.AgentCrossing;
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
            if (regionHandle == this.m_regInfo.RegionHandle)
            {
                if (agent.CapsPath != "")
                {
                    //Console.WriteLine("new user, so creating caps handler for it");
                    Caps cap = new Caps(this.assetCache, httpListener, this.m_regInfo.ExternalHostName, this.m_regInfo.ExternalEndPoint.Port, agent.CapsPath, agent.AgentID);
                    cap.RegisterHandlers();
                    if (capsHandlers.ContainsKey(agent.AgentID))
                    {
                        OpenSim.Framework.Console.MainLog.Instance.Warn("Adding duplicate CAPS entry for user " + agent.AgentID.ToStringHyphenated());
                        this.capsHandlers[agent.AgentID] = cap;
                    }
                    else
                    {
                        this.capsHandlers.Add(agent.AgentID, cap);
                    }

                }
                this.authenticateHandler.AddNewCircuit(agent.circuitcode, agent);
            }
        }

        public void AgentCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position)
        {
            if (regionHandle == this.m_regInfo.RegionHandle)
            {
                if (this.Avatars.ContainsKey(agentID))
                {
                    this.Avatars[agentID].MakeAvatar(position);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void InformClientOfNeighbours(IClientAPI remoteClient)
        {
            List<RegionInfo> neighbours = this.commsManager.GridServer.RequestNeighbours(this.m_regInfo);

            if (neighbours != null)
            {
                for (int i = 0; i < neighbours.Count; i++)
                {
                    AgentCircuitData agent = remoteClient.RequestClientInfo();
                    agent.BaseFolder = LLUUID.Zero;
                    agent.InventoryFolder = LLUUID.Zero;
                    agent.startpos = new LLVector3(128, 128, 70);
                    agent.child = true;
                    this.commsManager.InterRegion.InformRegionOfChildAgent(neighbours[i].RegionHandle, agent);
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
            return this.commsManager.GridServer.RequestNeighbourInfo(regionHandle);
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
            mapBlocks = this.commsManager.GridServer.RequestNeighbourMapBlocks(minX, minY, maxX, maxY);
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
        public void RequestTeleportLocation(IClientAPI remoteClient, ulong regionHandle, LLVector3 position, LLVector3 lookAt, uint flags)
        {
            if (regionHandle == this.m_regionHandle)
            {
                if (this.Avatars.ContainsKey(remoteClient.AgentId))
                {
                    remoteClient.SendTeleportLocationStart();
                    remoteClient.SendLocalTeleport(position, lookAt, flags);
                    this.Avatars[remoteClient.AgentId].Teleport(position);
                }
            }
            else
            {
                RegionInfo reg = this.RequestNeighbouringRegionInfo(regionHandle);
                if (reg != null)
                {
                    remoteClient.SendTeleportLocationStart();
                    AgentCircuitData agent = remoteClient.RequestClientInfo();
                    agent.BaseFolder = LLUUID.Zero;
                    agent.InventoryFolder = LLUUID.Zero;
                    agent.startpos = new LLVector3(128, 128, 70);
                    agent.child = true;
                    this.commsManager.InterRegion.InformRegionOfChildAgent(regionHandle, agent);
                    this.commsManager.InterRegion.ExpectAvatarCrossing(regionHandle, remoteClient.AgentId, position);

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
        public bool InformNeighbourOfCrossing(ulong regionhandle, LLUUID agentID, LLVector3 position)
        {
            return this.commsManager.InterRegion.ExpectAvatarCrossing(regionhandle, agentID, position);
        }

        public void performParcelPrimCountUpdate()
        {
            m_LandManager.resetAllLandPrimCounts();
            m_eventManager.TriggerParcelPrimCountUpdate();
            m_LandManager.finalizeLandPrimCountUpdate();
            m_LandManager.landPrimCountTainted = false;
        }
        #endregion

    }
}
