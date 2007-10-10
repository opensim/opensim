/*
* Copyright (c) Contributors, http://opensimulator.org/
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
using System.IO;
using System.Threading;
using System.Timers;
using System.Xml;
using Axiom.Math;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Framework.Utilities;
using OpenSim.Region.Capabilities;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.LandManagement;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Environment.Types;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Terrain;
using Timer = System.Timers.Timer;

namespace OpenSim.Region.Environment.Scenes
{
    public partial class Scene : SceneBase
    {
        public delegate bool FilterAvatarList(ScenePresence avatar);

        protected Timer m_heartbeatTimer = new Timer();
        protected Dictionary<LLUUID, ScenePresence> m_scenePresences;
        protected Dictionary<LLUUID, SceneObjectGroup> m_sceneObjects;

        private Random Rand = new Random();
        private uint _primCount = 702000;
        private readonly Mutex _primAllocateMutex = new Mutex(false);

        private int m_timePhase = 24;
        private int m_timeUpdateCount;

        public BasicQuadTreeNode QuadTree;

        private readonly Mutex updateLock;

        protected ModuleLoader m_moduleLoader;
        protected StorageManager storageManager;
        protected AgentCircuitManager authenticateHandler;
        protected RegionCommsListener regionCommsHost;
        public CommunicationsManager commsManager;
        // protected XferManager xferManager;

        protected Dictionary<LLUUID, Caps> capsHandlers = new Dictionary<LLUUID, Caps>();
        protected BaseHttpServer httpListener;

        protected Dictionary<string, IRegionModule> Modules = new Dictionary<string, IRegionModule>();
        public Dictionary<Type, object> ModuleInterfaces = new Dictionary<Type, object>();
        protected Dictionary<string, object> ModuleAPIMethods = new Dictionary<string, object>();

        //API module interfaces

        public IXfer XferManager;

        private IHttpRequests m_httpRequestModule = null;
        private ISimChat m_simChatModule = null;
        private IXMLRPC m_xmlrpcModule = null;
        private IWorldComm m_worldCommModule = null;


        // Central Update Loop

        protected int m_fps = 10;
        protected int m_frame = 0;
        protected float m_timespan = 0.1f;
        protected DateTime m_lastupdate = DateTime.Now;

        protected float m_timedilation = 1.0f;

        private int m_update_physics = 1;
        private int m_update_entitymovement = 1;
        private int m_update_entities = 1;
        private int m_update_events = 1;
        private int m_update_backup = 200;
        private int m_update_terrain = 50;
        private int m_update_land = 1;
        private int m_update_avatars = 1;

        #region Properties

        public AgentCircuitManager AuthenticateHandler
        {
            get { return authenticateHandler; }
        }

        private readonly LandManager m_LandManager;

        public LandManager LandManager
        {
            get { return m_LandManager; }
        }

        private readonly EstateManager m_estateManager;

        private PhysicsScene phyScene;
        public PhysicsScene PhysScene
        {
            set { phyScene = value; }
            get { return (phyScene); }
        }

        public EstateManager EstateManager
        {
            get { return m_estateManager; }
        }

        private readonly PermissionManager m_permissionManager;

        public PermissionManager PermissionsMngr
        {
            get { return m_permissionManager; }
        }

        public Dictionary<LLUUID, SceneObjectGroup> Objects
        {
            get { return m_sceneObjects; }
        }

        public int TimePhase
        {
            get { return m_timePhase; }
        }

        #endregion

        #region Constructors

        public Scene(RegionInfo regInfo, AgentCircuitManager authen, CommunicationsManager commsMan,
                     AssetCache assetCach, StorageManager storeManager, BaseHttpServer httpServer,
                     ModuleLoader moduleLoader)
        {
            updateLock = new Mutex(false);

            m_moduleLoader = moduleLoader;
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
            m_eventManager = new EventManager();
            m_permissionManager = new PermissionManager(this);

            m_eventManager.OnParcelPrimCountAdd +=
                m_LandManager.addPrimToLandPrimCounts;

            m_eventManager.OnPermissionError += SendPermissionAlert;

            QuadTree = new BasicQuadTreeNode(null, 0, 0, 256, 256);
            QuadTree.Subdivide();
            QuadTree.Subdivide();

            MainLog.Instance.Verbose("Creating new entitities instance");
            Entities = new Dictionary<LLUUID, EntityBase>();
            m_scenePresences = new Dictionary<LLUUID, ScenePresence>();
            m_sceneObjects = new Dictionary<LLUUID, SceneObjectGroup>();

            MainLog.Instance.Verbose("Creating LandMap");
            Terrain = new TerrainEngine((int)RegionInfo.RegionLocX, (int)RegionInfo.RegionLocY);

            ScenePresence.LoadAnims();

            httpListener = httpServer;
        }

        #endregion

        public void SetModuleInterfaces()
        {
            m_simChatModule = RequestModuleInterface<ISimChat>();
            m_httpRequestModule = RequestModuleInterface<IHttpRequests>();
            m_xmlrpcModule = RequestModuleInterface<IXMLRPC>();
            m_worldCommModule = RequestModuleInterface<IWorldComm>();

            XferManager = RequestModuleInterface<IXfer>();
        }

        #region Script Handling Methods

        public void SendCommandToPlugins(string[] args)
        {
            m_eventManager.TriggerOnPluginConsole(args);
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void StartTimer()
        {
            m_heartbeatTimer.Enabled = true;
            m_heartbeatTimer.Interval = (int)(m_timespan * 1000);
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
        /// Performs per-frame updates on the scene, this should be the central scene loop
        /// </summary>
        public override void Update()
        {
            TimeSpan SinceLastFrame = DateTime.Now - m_lastupdate;
            // Aquire a lock so only one update call happens at once
            updateLock.WaitOne();

            try
            {
                // Increment the frame counter
                m_frame++;

                // Loop it
                if (m_frame == Int32.MaxValue)
                    m_frame = 0;

                if (m_frame % m_update_physics == 0)
                    UpdatePreparePhysics();

                if (m_frame % m_update_entitymovement == 0)
                    UpdateEntityMovement();

                if (m_frame % m_update_physics == 0)
                    UpdatePhysics(
                        Math.Max(SinceLastFrame.TotalSeconds, m_timespan)
                        );

                if (m_frame % m_update_entities == 0)
                    UpdateEntities();

                if (m_frame % m_update_events == 0)
                    UpdateEvents();

                if (m_frame % m_update_backup == 0)
                    UpdateStorageBackup();

                if (m_frame % m_update_terrain == 0)
                    UpdateTerrain();

                if (m_frame % m_update_land == 0)
                    UpdateLand();

                if (m_frame % m_update_avatars == 0)
                    UpdateAvatars();
            }
            catch (NotImplementedException)
            {
                throw;
            }
            catch (Exception e)
            {
                MainLog.Instance.Error("Scene", "Failed with exception " + e.ToString());
            }
            finally
            {
                updateLock.ReleaseMutex();

                m_timedilation = m_timespan / (float)SinceLastFrame.TotalSeconds;
                m_lastupdate = DateTime.Now;
            }
        }

        private void UpdatePreparePhysics()
        {
            // If we are using a threaded physics engine
            // grab the latest scene from the engine before
            // trying to process it.

            // PhysX does this (runs in the background).

            if (phyScene.IsThreaded)
            {
                phyScene.GetResults();
            }
        }

        private void UpdateAvatars()
        {
            m_timeUpdateCount++;
            if (m_timeUpdateCount > 600)
            {
                List<ScenePresence> avatars = GetAvatars();
                foreach (ScenePresence avatar in avatars)
                {
                    avatar.ControllingClient.SendViewerTime(m_timePhase);
                }

                m_timeUpdateCount = 0;
                m_timePhase++;
                if (m_timePhase > 94)
                {
                    m_timePhase = 0;
                }
            }
        }

        private void UpdateLand()
        {
            if (m_LandManager.landPrimCountTainted)
            {
                //Perform land update of prim count
                performParcelPrimCountUpdate();
            }
        }

        private void UpdateTerrain()
        {
            if (Terrain.Tainted() && !Terrain.StillEditing())
            {
                CreateTerrainTexture();

                lock (Terrain.heightmap)
                {
                    lock (m_syncRoot)
                    {
                        phyScene.SetTerrain(Terrain.GetHeights1D());
                    }

                    storageManager.DataStore.StoreTerrain(Terrain.GetHeights2DD());

                    float[] terData = Terrain.GetHeights1D();

                    Broadcast(delegate(IClientAPI client)
                                             {
                                                 for (int x = 0; x < 16; x++)
                                                 {
                                                     for (int y = 0; y < 16; y++)
                                                     {
                                                         if (Terrain.Tainted(x * 16, y * 16))
                                                         {
                                                             client.SendLayerData(x, y, terData);
                                                         }
                                                     }
                                                 }
                                             });



                    Terrain.ResetTaint();
                }
            }
        }

        private void UpdateStorageBackup()
        {
            Backup();
        }

        private void UpdateEvents()
        {
            m_eventManager.TriggerOnFrame();
        }

        private void UpdateEntities()
        {
            List<EntityBase> updateEntities = new List<EntityBase>(Entities.Values);

            foreach (EntityBase entity in updateEntities)
            {
                entity.Update();
            }
        }

        private void UpdatePhysics(double elapsed)
        {
            lock (m_syncRoot)
            {
                phyScene.Simulate((float)elapsed);
            }
        }

        private void UpdateEntityMovement()
        {
            List<EntityBase> moveEntities = new List<EntityBase>(Entities.Values);

            foreach (EntityBase entity in moveEntities)
            {
                entity.UpdateMovement();
            }
        }

        /// <summary>
        /// Perform delegate action on all clients subscribing to updates from this region.
        /// </summary>
        /// <returns></returns>
        internal void Broadcast(Action<IClientAPI> whatToDo)
        {
            ForEachScenePresence( delegate( ScenePresence presence )
                                      {
                                          whatToDo(presence.ControllingClient);
                                      });
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
                        MainLog.Instance.Verbose("TERRAIN", "No default terrain. Generating a new terrain.");
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
                            MainLog.Instance.Verbose("TERRAIN","No terrain found in database or default. Generating a new terrain.");
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
            commsManager.AssetCache.AddAsset(asset);
        }

        #endregion

        #region Primitives Methods

        /// <summary>
        /// Loads the World's objects
        /// </summary>
        public virtual void LoadPrimsFromStorage()
        {
            MainLog.Instance.Verbose("Loading objects from datastore");
            List<SceneObjectGroup> PrimsFromDB = storageManager.DataStore.LoadObjects(m_regInfo.SimUUID);
            foreach (SceneObjectGroup prim in PrimsFromDB)
            {
                AddEntityFromStorage(prim);
                SceneObjectPart rootPart = prim.GetChildPart(prim.UUID);
                if ((rootPart.ObjectFlags & (uint)LLObject.ObjectFlags.Phantom) == 0)
                    rootPart.PhysActor = phyScene.AddPrimShape(
                        rootPart.Name,
                        rootPart.Shape,
                        new PhysicsVector(rootPart.AbsolutePosition.X, rootPart.AbsolutePosition.Y,
                                          rootPart.AbsolutePosition.Z),
                        new PhysicsVector(rootPart.Scale.X, rootPart.Scale.Y, rootPart.Scale.Z),
                        new Quaternion(rootPart.RotationOffset.W, rootPart.RotationOffset.X,
                                       rootPart.RotationOffset.Y, rootPart.RotationOffset.Z));
            }
            MainLog.Instance.Verbose("Loaded " + PrimsFromDB.Count.ToString() + " SceneObject(s)");
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
            if (PermissionsMngr.CanRezObject(ownerID, pos))
            {
                SceneObjectGroup sceneOb =
                    new SceneObjectGroup(this, m_regionHandle, ownerID, PrimIDAllocate(), pos, shape);
                AddEntity(sceneOb);
                SceneObjectPart rootPart = sceneOb.GetChildPart(sceneOb.UUID);
                // if grass or tree, make phantom
                if ((rootPart.Shape.PCode == 95) || (rootPart.Shape.PCode == 255))
                {
                    rootPart.ObjectFlags += (uint)LLObject.ObjectFlags.Phantom;
                }
                // if not phantom, add to physics
                if ((rootPart.ObjectFlags & (uint)LLObject.ObjectFlags.Phantom) == 0)
                    rootPart.PhysActor =
                        phyScene.AddPrimShape(
                                         rootPart.Name,
                                         rootPart.Shape,
                                         new PhysicsVector(pos.X, pos.Y, pos.Z),
                                         new PhysicsVector(shape.Scale.X, shape.Scale.Y, shape.Scale.Z),
                                         new Quaternion());
            }
        }

        public void RemovePrim(uint localID, LLUUID avatar_deleter)
        {
            foreach (EntityBase obj in Entities.Values)
            {
                if (obj is SceneObjectGroup)
                {
                    if (((SceneObjectGroup)obj).LocalId == localID)
                    {
                        RemoveEntity((SceneObjectGroup)obj);
                        return;
                    }
                }
            }
        }

        public void AddEntityFromStorage(SceneObjectGroup sceneObject)
        {
            sceneObject.RegionHandle = m_regionHandle;
            sceneObject.SetScene(this);
            foreach (SceneObjectPart part in sceneObject.Children.Values)
            {
                part.LocalID = PrimIDAllocate();
            }
            sceneObject.UpdateParentIDs();
            AddEntity(sceneObject);
        }

        public void AddEntity(SceneObjectGroup sceneObject)
        {
            if (!Entities.ContainsKey(sceneObject.UUID))
            {
                //  QuadTree.AddObject(sceneObject);
                Entities.Add(sceneObject.UUID, sceneObject);
            }
        }

        public void RemoveEntity(SceneObjectGroup sceneObject)
        {
            if (Entities.ContainsKey(sceneObject.UUID))
            {
                m_LandManager.removePrimFromLandPrimCounts(sceneObject);
                Entities.Remove(sceneObject.UUID);
                m_LandManager.setPrimsTainted();
            }
        }

        /// <summary>
        /// Called by a prim when it has been created/cloned, so that its events can be subscribed to
        /// </summary>
        /// <param name="prim"></param>
        public void AcknowledgeNewPrim(SceneObjectGroup prim)
        {
            prim.OnPrimCountTainted += m_LandManager.setPrimsTainted;
        }

        public void LoadPrimsFromXml(string fileName)
        {
            XmlDocument doc = new XmlDocument();
            XmlNode rootNode;
            int primCount = 0;
            if ((fileName.StartsWith("http:")) | (File.Exists(fileName)))
            {
                
                XmlTextReader reader = new XmlTextReader(fileName);
                reader.WhitespaceHandling = WhitespaceHandling.None;
                doc.Load(reader);
                reader.Close();
                rootNode = doc.FirstChild;
                foreach (XmlNode aPrimNode in rootNode.ChildNodes)
                {
                    SceneObjectGroup obj = new SceneObjectGroup(this,
                                                                m_regionHandle, aPrimNode.OuterXml);
                    //if we want this to be a import method then we need new uuids for the object to avoid any clashes
                    //obj.RegenerateFullIDs(); 
                    AddEntity(obj);

                    SceneObjectPart rootPart = obj.GetChildPart(obj.UUID);
                    if ((rootPart.ObjectFlags & (uint)LLObject.ObjectFlags.Phantom) == 0)
                        rootPart.PhysActor = phyScene.AddPrimShape(
                            rootPart.Name,
                            rootPart.Shape,
                            new PhysicsVector(rootPart.AbsolutePosition.X, rootPart.AbsolutePosition.Y,
                                              rootPart.AbsolutePosition.Z),
                            new PhysicsVector(rootPart.Scale.X, rootPart.Scale.Y, rootPart.Scale.Z),
                            new Quaternion(rootPart.RotationOffset.W, rootPart.RotationOffset.X,
                                           rootPart.RotationOffset.Y, rootPart.RotationOffset.Z));
                    primCount++;
                }
            }
            else
            {
                throw new Exception("Could not open file " + fileName + " for reading");
            }
        }

        public void SavePrimsToXml(string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Create);
            StreamWriter stream = new StreamWriter(file);
            int primCount = 0;
            stream.WriteLine("<scene>\n");
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    stream.WriteLine(((SceneObjectGroup)ent).ToXmlString());
                    primCount++;
                }
            }
            stream.WriteLine("</scene>\n");
            stream.Close();
            file.Close();
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

            CreateAndAddScenePresence(client, child);

            m_LandManager.sendParcelOverlay(client);
            commsManager.UserProfileCache.AddNewUser(client.AgentId);
            commsManager.TransactionsManager.AddUser(client.AgentId);
        }

        protected virtual void SubscribeToClientEvents(IClientAPI client)
        {
            // client.OnStartAnim += StartAnimation;
            client.OnRegionHandShakeReply += SendLayerData;
            //remoteClient.OnRequestWearables += new GenericCall(this.GetInitialPrims);
            client.OnModifyTerrain += ModifyTerrain;
            //client.OnChatFromViewer += SimChat;
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
            client.OnGrabUpdate += MoveObject;
            client.OnDeRezObject += DeRezObject;
            client.OnRezObject += RezObject;
            client.OnNameFromUUIDRequest += commsManager.HandleUUIDNameRequest;
            client.OnObjectDescription += PrimDescription;
            client.OnObjectName += PrimName;
            client.OnLinkObjects += LinkObjects;
            client.OnObjectDuplicate += DuplicateObject;

            client.OnParcelPropertiesRequest += new ParcelPropertiesRequest(m_LandManager.handleParcelPropertiesRequest);
            client.OnParcelDivideRequest += new ParcelDivideRequest(m_LandManager.handleParcelDivideRequest);
            client.OnParcelJoinRequest += new ParcelJoinRequest(m_LandManager.handleParcelJoinRequest);
            client.OnParcelPropertiesUpdateRequest +=
                new ParcelPropertiesUpdateRequest(m_LandManager.handleParcelPropertiesUpdateRequest);
            client.OnParcelSelectObjects += new ParcelSelectObjects(m_LandManager.handleParcelSelectObjectsRequest);
            client.OnParcelObjectOwnerRequest +=
                new ParcelObjectOwnerRequest(m_LandManager.handleParcelObjectOwnersRequest);

            client.OnEstateOwnerMessage += new EstateOwnerMessageRequest(m_estateManager.handleEstateOwnerMessage);

            client.OnCreateNewInventoryItem += CreateNewInventoryItem;
            client.OnCreateNewInventoryFolder += commsManager.UserProfileCache.HandleCreateInventoryFolder;
            client.OnFetchInventoryDescendents += commsManager.UserProfileCache.HandleFecthInventoryDescendents;
            client.OnRequestTaskInventory += RequestTaskInventory;
            client.OnFetchInventory += commsManager.UserProfileCache.HandleFetchInventory;
            client.OnUpdateInventoryItem += UDPUpdateInventoryItemAsset;
            client.OnAssetUploadRequest += commsManager.TransactionsManager.HandleUDPUploadRequest;
            client.OnXferReceive += commsManager.TransactionsManager.HandleXfer;
            client.OnRezScript += RezScript;
            client.OnRemoveTaskItem += RemoveTaskInventory;

            // client.OnRequestAvatarProperties += RequestAvatarProperty;

            client.OnGrabObject += ProcessObjectGrab;

            EventManager.TriggerOnNewClient(client);
        }

        protected ScenePresence CreateAndAddScenePresence(IClientAPI client, bool child)
        {
            ScenePresence newAvatar = null;

            newAvatar = new ScenePresence(client, this, m_regInfo);
            newAvatar.IsChildAgent = child;

            if (child)
            {
                MainLog.Instance.Verbose(RegionInfo.RegionName + ": Creating new child agent.");
            }
            else
            {
                newAvatar.OnSignificantClientMovement += m_LandManager.handleSignificantClientMovement;

                MainLog.Instance.Verbose(RegionInfo.RegionName + ": Creating new root agent.");
                MainLog.Instance.Verbose(RegionInfo.RegionName + ": Adding Physical agent.");

                newAvatar.AddToPhysicalScene();
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
            lock (m_scenePresences)
            {
                if (m_scenePresences.ContainsKey(client.AgentId))
                {
                    m_scenePresences[client.AgentId] = newAvatar;
                }
                else
                {
                    m_scenePresences.Add(client.AgentId, newAvatar);
                }
            }

            return newAvatar;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="agentID"></param>
        public override void RemoveClient(LLUUID agentID)
        {
            m_eventManager.TriggerOnRemovePresence(agentID);

            ScenePresence avatar = GetScenePresence(agentID);

            Broadcast(delegate(IClientAPI client)
                           {
                               client.SendKillObject(avatar.RegionHandle, avatar.LocalId);
                           });

            ForEachScenePresence(
                delegate(ScenePresence presence)
                {
                    presence.CoarseLocationChange();
                });

            lock (m_scenePresences)
            {
                m_scenePresences.Remove(agentID);
            }

            lock (Entities)
            {
                Entities.Remove(agentID);
            }

            avatar.Close();

            // Remove client agent from profile, so new logins will work
            commsManager.UserService.clearUserAgent(agentID);

            return;
        }

        #endregion

        #region Request m_scenePresences List Methods

        //The idea is to have a group of method that return a list of avatars meeting some requirement
        // ie it could be all m_scenePresences within a certain range of the calling prim/avatar. 

        /// <summary>
        /// Request a List of all m_scenePresences in this World
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences()
        {
            List<ScenePresence> result = new List<ScenePresence>(m_scenePresences.Values);

            return result;
        }

        public List<ScenePresence> GetAvatars()
        {
            List<ScenePresence> result = GetScenePresences(delegate(ScenePresence scenePresence)
                                                                {
                                                                    return !scenePresence.IsChildAgent;
                                                                });

            return result;
        }

        /// <summary>
        /// Request a filtered list of m_scenePresences in this World
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences(FilterAvatarList filter)
        {
            List<ScenePresence> result = new List<ScenePresence>();

            foreach (ScenePresence avatar in m_scenePresences.Values)
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
        public ScenePresence GetScenePresence(LLUUID avatarID)
        {
            if (m_scenePresences.ContainsKey(avatarID))
            {
                return m_scenePresences[avatarID];
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="whatToDo"></param>
        public void ForEachScenePresence(Action<ScenePresence> whatToDo)
        {
            foreach (ScenePresence presence in m_scenePresences.Values)
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
                storageManager.DataStore.RemoveObject(entID, m_regInfo.SimUUID);
                return true;
            }
            return false;
        }

        public void SendKillObject(uint localID)
        {
            Broadcast(delegate(IClientAPI client)
                                     {
                                         client.SendKillObject(m_regionHandle, localID);
                                     });
        }

        public void NotifyMyCoarseLocationChange()
        {
            ForEachScenePresence(delegate(ScenePresence presence)
                                             {
                                                 presence.CoarseLocationChange();
                                             });
        }

        public void SendAllSceneObjectsToClient(ScenePresence presence)
        {
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    // ((SceneObjectGroup)ent).SendFullUpdateToClient(client);
                    ((SceneObjectGroup)ent).ScheduleFullUpdateToAvatar(presence);
                }
            }
        }

        #region RegionCommsHost

        /// <summary>
        /// 
        /// </summary>
        public void RegisterRegionWithComms()
        {
            regionCommsHost = commsManager.GridService.RegisterRegion(m_regInfo);
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
                        new Caps(commsManager.AssetCache, httpListener, m_regInfo.ExternalHostName, httpListener.Port,
                                 agent.CapsPath, agent.AgentID);
                    Util.SetCapsURL(agent.AgentID,
                                    "http://" + m_regInfo.ExternalHostName + ":" + httpListener.Port.ToString() +
                                    "/CAPS/" + agent.CapsPath + "0000/");
                    cap.RegisterHandlers();
                    cap.AddNewInventoryItem = AddInventoryItem;
                    cap.ItemUpdatedCall = CapsUpdateInventoryItemAsset;
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
                if (m_scenePresences.ContainsKey(agentID))
                {
                    m_scenePresences[agentID].MakeAvatarPhysical(position, isFlying);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void InformClientOfNeighbours(IClientAPI remoteClient)
        {
            List<RegionInfo> neighbours = commsManager.GridService.RequestNeighbours(m_regInfo);

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
            return commsManager.GridService.RequestNeighbourInfo(regionHandle);
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
            mapBlocks = commsManager.GridService.RequestNeighbourMapBlocks(minX, minY, maxX, maxY);
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
                if (m_scenePresences.ContainsKey(remoteClient.AgentId))
                {
                    remoteClient.SendTeleportLocationStart();
                    remoteClient.SendLocalTeleport(position, lookAt, flags);
                    m_scenePresences[remoteClient.AgentId].Teleport(position);
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
                    // agent.startpos = new LLVector3(128, 128, 70);
                    agent.startpos = position;
                    agent.child = true;
                    m_scenePresences[remoteClient.AgentId].Close();
                    commsManager.InterRegion.InformRegionOfChildAgent(regionHandle, agent);
                    commsManager.InterRegion.ExpectAvatarCrossing(regionHandle, remoteClient.AgentId, position, false);
                    AgentCircuitData circuitdata = remoteClient.RequestClientInfo();
                    string capsPath = Util.GetCapsURL(remoteClient.AgentId);
                    remoteClient.SendRegionTeleport(regionHandle, 13, reg.ExternalEndPoint, 4, (1 << 4), capsPath);
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

        public void AddModule(string name, IRegionModule module)
        {
            if (!Modules.ContainsKey(name))
            {
                Modules.Add(name, module);
            }
        }

        public void RegisterModuleInterface<M>(M mod)
        {
            if (!ModuleInterfaces.ContainsKey(typeof(M)))
            {
                ModuleInterfaces.Add(typeof(M), mod);
            }
        }

        public T RequestModuleInterface<T>()
        {
            if (ModuleInterfaces.ContainsKey(typeof(T)))
            {
                return (T)ModuleInterfaces[typeof(T)];
            }
            else
            {
                return default(T);
            }
        }

        public void SetTimePhase(int phase)
        {
            m_timePhase = phase;
        }

        public void SendUrlToUser(LLUUID avatarID, string objectname, LLUUID objectID, LLUUID ownerID, bool groupOwned,
                                  string message, string url)        
        {
            if (m_scenePresences.ContainsKey(avatarID))
            {
                m_scenePresences[avatarID].ControllingClient.SendLoadURL(objectname, objectID, ownerID, groupOwned, message, url);
            }
        }

        #region Alert Methods

        private void SendPermissionAlert(LLUUID user, string reason)
        {
            SendAlertToUser(user, reason, false);
        }


        public void SendGeneralAlert(string message)
        {
            foreach (ScenePresence presence in m_scenePresences.Values)
            {
                presence.ControllingClient.SendAlertMessage(message);
            }
        }

        public void SendAlertToUser(LLUUID agentID, string message, bool modal)
        {
            if (m_scenePresences.ContainsKey(agentID))
            {
                m_scenePresences[agentID].ControllingClient.SendAgentAlertMessage(message, modal);
            }
        }

        public void SendAlertToUser(string firstName, string lastName, string message, bool modal)
        {
            foreach (ScenePresence presence in m_scenePresences.Values)
            {
                if ((presence.Firstname == firstName) && (presence.Lastname == lastName))
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
                string message = CombineParams(commandParams, 1);
                SendGeneralAlert(message);
            }
            else
            {
                string message = CombineParams(commandParams, 2);
                SendAlertToUser(commandParams[0], commandParams[1], message, false);
            }
        }

        private string CombineParams(string[] commandParams, int pos)
        {
            string result = "";
            for (int i = pos; i < commandParams.Length; i++)
            {
                result += commandParams[i] + " ";
            }
            return result;
        }

        #endregion

        public void ForceClientUpdate()
        {
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    ((SceneObjectGroup)ent).ScheduleGroupForFullUpdate();
                }
            }
        }

        public void HandleEditCommand(string[] cmmdparams)
        {
            Console.WriteLine("Searching for Primitive: '" + cmmdparams[0] + "'");
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    SceneObjectPart part = ((SceneObjectGroup)ent).GetChildPart(((SceneObjectGroup)ent).UUID);
                    if (part != null)
                    {
                        if (part.Name == cmmdparams[0])
                        {
                            part.Resize(
                                new LLVector3(Convert.ToSingle(cmmdparams[1]), Convert.ToSingle(cmmdparams[2]),
                                              Convert.ToSingle(cmmdparams[3])));

                            Console.WriteLine("Edited scale of Primitive: " + part.Name);
                        }
                    }
                }
            }
        }

        public void Show(string ShowWhat)
        {
            switch (ShowWhat)
            {
                case "users":
                    MainLog.Instance.Error("Current Region: " + RegionInfo.RegionName);
                    MainLog.Instance.Error(
                        String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16}{5,-16}{6,-16}", "Firstname", "Lastname",
                                      "Agent ID", "Session ID", "Circuit", "IP", "World"));

                    foreach (ScenePresence scenePrescence in GetAvatars())
                    {
                        MainLog.Instance.Error(
                                    String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}{6,-16}",
                                                  scenePrescence.Firstname,
                                                  scenePrescence.Lastname,
                                                  scenePrescence.UUID,
                                                  scenePrescence.ControllingClient.AgentId,
                                                  "Unknown",
                                                  "Unknown",
                                                  RegionInfo.RegionName));
                    }
                    break;
                case "modules":
                    MainLog.Instance.Error("The currently loaded modules in " + RegionInfo.RegionName + " are:");
                    foreach (IRegionModule module in Modules.Values)
                    {
                        if (!module.IsSharedModule())
                        {
                            MainLog.Instance.Error("Region Module: " + module.GetName());
                        }
                    }
                    break;
            }
        }

        public LLUUID MakeHttpRequest(string url, string type, string body)
        {
            if (m_httpRequestModule != null)
            {
                return m_httpRequestModule.MakeHttpRequest(url, type, body);
            }
            return LLUUID.Zero;
        }

        #region Script Engine

        private List<ScriptEngineInterface> ScriptEngines = new List<ScriptEngineInterface>();

        public void AddScriptEngine(ScriptEngineInterface ScriptEngine, LogBase m_logger)
        {
            ScriptEngines.Add(ScriptEngine);

            ScriptEngine.InitializeEngine(this, m_logger);
        }

        #endregion

        public LLUUID ConvertLocalIDToFullID(uint localID)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        return ((SceneObjectGroup)ent).GetPartsFullID(localID);
                    }
                }
            }
            return LLUUID.Zero;
        }

        public SceneObjectPart GetSceneObjectPart(uint localID)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(localID);
                    if (hasPrim != false)
                    {
                        return ((SceneObjectGroup)ent).GetChildPart(localID);
                    }
                }
            }
            return null;
        }

        public SceneObjectPart GetSceneObjectPart(LLUUID fullID)
        {
            bool hasPrim = false;
            foreach (EntityBase ent in Entities.Values)
            {
                if (ent is SceneObjectGroup)
                {
                    hasPrim = ((SceneObjectGroup)ent).HasChildPrim(fullID);
                    if (hasPrim != false)
                    {
                        return ((SceneObjectGroup)ent).GetChildPart(fullID);
                    }
                }
            }
            return null;
        }

        internal bool TryGetAvatar(LLUUID avatarId, out ScenePresence avatar)
        {
            ScenePresence presence;
            if (m_scenePresences.TryGetValue(avatarId, out presence))
            {
                if (!presence.IsChildAgent)
                {
                    avatar = presence;
                    return true;
                }
            }

            avatar = null;
            return false;
        }

        public override void Close()
        {
            m_heartbeatTimer.Close();

            base.Close();
        }

        internal bool TryGetAvatarByName(string avatarName, out ScenePresence avatar)
        {
            foreach( ScenePresence presence in m_scenePresences.Values )
            {
                if( !presence.IsChildAgent )
                {
                    string name = presence.ControllingClient.FirstName + " " + presence.ControllingClient.LastName;

                    if( String.Compare( avatarName, name, true ) == 0 )
                    {
                        avatar = presence;
                        return true;                        
                    }
                }
            }

            avatar = null;
            return false;           
        }
    }
}
