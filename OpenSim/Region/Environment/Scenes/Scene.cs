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
using System.Net;
using System.Threading;
using System.Timers;
using System.Xml;
using Axiom.Math;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.LandManagement;
using OpenSim.Framework.Servers;
using OpenSim.Region.Capabilities;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Environment.Types;
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Terrain;
using Timer = System.Timers.Timer;

namespace OpenSim.Region.Environment.Scenes
{
    public delegate bool FilterAvatarList(ScenePresence avatar);

    public partial class Scene : SceneBase
    {
        #region Fields
        protected Timer m_heartbeatTimer = new Timer();

        public InnerScene m_innerScene;

        private Random Rand = new Random();
        private uint _primCount = 702000;
        private readonly Mutex _primAllocateMutex = new Mutex(false);

        private int m_timePhase = 24;
        private int m_timeUpdateCount;

        private readonly Mutex updateLock;
        public bool m_physicalPrim;
        protected ModuleLoader m_moduleLoader;
        protected StorageManager m_storageManager;
        protected AgentCircuitManager m_authenticateHandler;
        public CommunicationsManager CommsManager;
        // protected XferManager xferManager;
        protected SceneCommunicationService m_sceneGridService;
        protected SceneXmlLoader m_sceneXmlLoader;

        protected Dictionary<LLUUID, Caps> m_capsHandlers = new Dictionary<LLUUID, Caps>();
        protected BaseHttpServer httpListener;

        protected Dictionary<string, IRegionModule> Modules = new Dictionary<string, IRegionModule>();
        public Dictionary<Type, object> ModuleInterfaces = new Dictionary<Type, object>();
        protected Dictionary<string, object> ModuleAPIMethods = new Dictionary<string, object>();

        //API module interfaces

        public IXfer XferManager;

        private IHttpRequests m_httpRequestModule;
        private ISimChat m_simChatModule;
        private IXMLRPC m_xmlrpcModule;
        private IWorldComm m_worldCommModule;
        private IAvatarFactory m_AvatarFactory;

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
        #endregion

        #region Properties

        public AgentCircuitManager AuthenticateHandler
        {
            get { return m_authenticateHandler; }
        }

        private readonly LandManager m_LandManager;

        public LandManager LandManager
        {
            get { return m_LandManager; }
        }

        private readonly EstateManager m_estateManager;

        private PhysicsScene phyScene
        {
            set { m_innerScene.PhyScene = value; }
            get { return (m_innerScene.PhyScene); }
        }

        public PhysicsScene PhysScene
        {
            set { m_innerScene.PhyScene = value; }
            get { return (m_innerScene.PhyScene); }
        }

        public object SyncRoot
        {
            get { return m_innerScene.m_syncRoot; }
        }

        public EstateManager EstateManager
        {
            get { return m_estateManager; }
        }

        public float TimeDilation
        {
            get { return m_timedilation; }
        }

        private readonly PermissionManager m_permissionManager;

        public PermissionManager PermissionsMngr
        {
            get { return m_permissionManager; }
        }

        public int TimePhase
        {
            get { return m_timePhase; }
        }

        public Dictionary<LLUUID, SceneObjectGroup> Objects
        {
            get { return m_innerScene.SceneObjects; }
        }

        protected Dictionary<LLUUID, ScenePresence> m_scenePresences
        {
            get { return m_innerScene.ScenePresences; }
            set { m_innerScene.ScenePresences = value; }
        }

        protected Dictionary<LLUUID, SceneObjectGroup> m_sceneObjects
        {
            get { return m_innerScene.SceneObjects; }
            set { m_innerScene.SceneObjects = value; }
        }

        public Dictionary<LLUUID, EntityBase> Entities
        {
            get { return m_innerScene.Entities; }
            set { m_innerScene.Entities = value; }
        }

        #endregion

        #region Constructors

        public Scene(RegionInfo regInfo, AgentCircuitManager authen, PermissionManager permissionManager, CommunicationsManager commsMan, SceneCommunicationService sceneGridService,
                     AssetCache assetCach, StorageManager storeManager, BaseHttpServer httpServer,
                     ModuleLoader moduleLoader, bool dumpAssetsToFile, bool physicalPrim)
        {
            updateLock = new Mutex(false);

            m_moduleLoader = moduleLoader;
            m_authenticateHandler = authen;
            CommsManager = commsMan;
            m_sceneGridService = sceneGridService;
            m_storageManager = storeManager;
            AssetCache = assetCach;
            m_regInfo = regInfo;
            m_regionHandle = m_regInfo.RegionHandle;
            m_regionName = m_regInfo.RegionName;
            m_datastore = m_regInfo.DataStore;
            RegisterRegionWithComms();
            m_physicalPrim = physicalPrim;

            m_LandManager = new LandManager(this, m_regInfo);
            m_estateManager = new EstateManager(this, m_regInfo);
            m_eventManager = new EventManager();

            m_permissionManager = permissionManager;
            m_permissionManager.Initialise(this);

            m_innerScene = new InnerScene(this, m_regInfo, m_permissionManager);
            m_sceneXmlLoader = new SceneXmlLoader(this, m_innerScene, m_regInfo);

            RegisterDefaultSceneEvents();

            MainLog.Instance.Verbose("Creating new entitities instance");
            Entities = new Dictionary<LLUUID, EntityBase>();
            m_scenePresences = new Dictionary<LLUUID, ScenePresence>();
            m_sceneObjects = new Dictionary<LLUUID, SceneObjectGroup>();

            MainLog.Instance.Verbose("Creating LandMap");
            Terrain = new TerrainEngine((int)RegionInfo.RegionLocX, (int)RegionInfo.RegionLocY);

            ScenePresence.LoadAnims();

            httpListener = httpServer;
            m_dumpAssetsToFile = dumpAssetsToFile;
        }

        #endregion

        #region Startup / Close Methods

        protected virtual void RegisterDefaultSceneEvents()
        {
            m_eventManager.OnParcelPrimCountAdd += m_LandManager.addPrimToLandPrimCounts;
            m_eventManager.OnPermissionError += SendPermissionAlert;
        }

        public override void Close()
        {
            ForEachScenePresence(delegate(ScenePresence avatar)
                                {
                                    avatar.ControllingClient.Stop();
                                });

            m_heartbeatTimer.Close();
            m_innerScene.Close();
            m_sceneGridService.Close();

            foreach (IRegionModule module in this.Modules.Values)
            {
                if (!module.IsSharedModule)
                {
                    module.Close();
                }
            }
            Modules.Clear();

            base.Close();

        }

        /// <summary>
        /// 
        /// </summary>
        public void StartTimer()
        {
            m_heartbeatTimer.Enabled = true;
            m_heartbeatTimer.Interval = (int)(m_timespan * 1000);
            m_heartbeatTimer.Elapsed += new ElapsedEventHandler(Heartbeat);
        }

        public void SetModuleInterfaces()
        {
            m_simChatModule = RequestModuleInterface<ISimChat>();
            m_httpRequestModule = RequestModuleInterface<IHttpRequests>();
            m_xmlrpcModule = RequestModuleInterface<IXMLRPC>();
            m_worldCommModule = RequestModuleInterface<IWorldComm>();
            XferManager = RequestModuleInterface<IXfer>();
        }

        #endregion

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
                    m_innerScene.UpdatePreparePhysics();

                if (m_frame % m_update_entitymovement == 0)
                    m_innerScene.UpdateEntityMovement();

                if (m_frame % m_update_physics == 0)
                    m_innerScene.UpdatePhysics(
                        Math.Max(SinceLastFrame.TotalSeconds, m_timespan)
                        );

                if (m_frame % m_update_entities == 0)
                    m_innerScene.UpdateEntities();

                if (m_frame % m_update_events == 0)
                    UpdateEvents();

                if (m_frame % m_update_backup == 0)
                    UpdateStorageBackup();

                if (m_frame % m_update_terrain == 0)
                    UpdateTerrain();

                if (m_frame % m_update_land == 0)
                    UpdateLand();

                // if (m_frame%m_update_avatars == 0)
                //   UpdateInWorldTime();
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

        private void UpdateInWorldTime()
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
                    lock (SyncRoot)
                    {
                        phyScene.SetTerrain(Terrain.GetHeights1D());
                    }

                    m_storageManager.DataStore.StoreTerrain(Terrain.GetHeights2DD(), RegionInfo.RegionID);

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

        /// <summary>
        /// Perform delegate action on all clients subscribing to updates from this region.
        /// </summary>
        /// <returns></returns>
        internal void Broadcast(Action<IClientAPI> whatToDo)
        {
            ForEachScenePresence(delegate(ScenePresence presence) { whatToDo(presence.ControllingClient); });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool Backup()
        {
            EventManager.TriggerOnBackup(m_storageManager.DataStore);
            return true;
        }

        #endregion

        #region Load Terrain

        public void ExportWorldMap(string fileName)
        {
            List<MapBlockData> mapBlocks = this.CommsManager.GridService.RequestNeighbourMapBlocks((int)(this.RegionInfo.RegionLocX - 9), (int)(this.RegionInfo.RegionLocY - 9), (int)(this.RegionInfo.RegionLocX + 9), (int)(this.RegionInfo.RegionLocY + 9));
            List<AssetBase> textures = new List<AssetBase>();
            List<System.Drawing.Image> bitImages = new List<System.Drawing.Image>();

            foreach (MapBlockData mapBlock in mapBlocks)
            {
                AssetBase texAsset = this.AssetCache.GetAsset(mapBlock.MapImageId, true);

                if (texAsset != null)
                {
                    textures.Add(texAsset);
                }
                else
                {
                    texAsset = this.AssetCache.GetAsset(mapBlock.MapImageId, true);
                    if (texAsset != null)
                    {
                        textures.Add(texAsset);
                    }
                }
            }

            foreach(AssetBase asset in textures)
            {
               System.Drawing.Image image= OpenJPEGNet.OpenJPEG.DecodeToImage(asset.Data);
               bitImages.Add(image);
            }

            System.Drawing.Bitmap mapTexture = new System.Drawing.Bitmap(2560, 2560);
            System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(mapTexture);
            System.Drawing.SolidBrush sea = new System.Drawing.SolidBrush(System.Drawing.Color.DarkBlue);
            g.FillRectangle(sea, 0, 0, 2560, 2560);

            for(int i =0; i<mapBlocks.Count; i++)
            {
                ushort x = (ushort) ((mapBlocks[i].X - this.RegionInfo.RegionLocX) + 10);
                ushort y = (ushort) ((mapBlocks[i].Y - this.RegionInfo.RegionLocY) + 10);
                g.DrawImage(bitImages[i], (x*128), (y*128), 128, 128);
            }
            mapTexture.Save(fileName, System.Drawing.Imaging.ImageFormat.Jpeg);
        }

        /// <summary>
        /// Loads the World heightmap
        /// </summary>
        /// 
        public override void LoadWorldMap()
        {
            try
            {
                double[,] map = m_storageManager.DataStore.LoadTerrain(RegionInfo.RegionID);
                if (map == null)
                {
                    if (string.IsNullOrEmpty(m_regInfo.EstateSettings.terrainFile))
                    {
                        MainLog.Instance.Verbose("TERRAIN", "No default terrain. Generating a new terrain.");
                        Terrain.HillsGenerator();

                        m_storageManager.DataStore.StoreTerrain(Terrain.GetHeights2DD(), RegionInfo.RegionID);
                    }
                    else
                    {
                        try
                        {
                            Terrain.LoadFromFileF32(m_regInfo.EstateSettings.terrainFile);
                            Terrain *= m_regInfo.EstateSettings.terrainMultiplier;
                        }
                        catch
                        {
                            MainLog.Instance.Verbose("TERRAIN",
                                                     "No terrain found in database or default. Generating a new terrain.");
                            Terrain.HillsGenerator();
                        }
                        m_storageManager.DataStore.StoreTerrain(Terrain.GetHeights2DD(), RegionInfo.RegionID);
                    }
                }
                else
                {
                    Terrain.SetHeights2D(map);
                }

                CreateTerrainTexture();
                CommsManager.GridService.RegisterRegion(RegionInfo); //hack to update the terrain texture in grid mode so it shows on world map
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("terrain", "Scene.cs: LoadWorldMap() - Failed with exception " + e.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void CreateTerrainTexture()
        {
            //create a texture asset of the terrain 
            byte[] data = Terrain.ExportJpegImage("defaultstripe.png");
            m_regInfo.EstateSettings.terrainImageID = LLUUID.Random();
            AssetBase asset = new AssetBase();
            asset.FullID = m_regInfo.EstateSettings.terrainImageID;
            asset.Data = data;
            asset.Name = "terrainImage";
            asset.Type = 0;
            asset.Temporary = true;
            AssetCache.AddAsset(asset);
        }

        #endregion

        #region Primitives Methods

        /// <summary>
        /// Loads the World's objects
        /// </summary>
        public virtual void LoadPrimsFromStorage()
        {
            MainLog.Instance.Verbose("Loading objects from datastore");
            List<SceneObjectGroup> PrimsFromDB = m_storageManager.DataStore.LoadObjects(m_regInfo.RegionID);
            foreach (SceneObjectGroup prim in PrimsFromDB)
            {
                AddEntityFromStorage(prim);
                SceneObjectPart rootPart = prim.GetChildPart(prim.UUID);
                bool UsePhysics = (((rootPart.ObjectFlags & (uint)LLObject.ObjectFlags.Physics) > 0) && m_physicalPrim);
                if ((rootPart.ObjectFlags & (uint)LLObject.ObjectFlags.Phantom) == 0)
                    rootPart.PhysActor = phyScene.AddPrimShape(
                        rootPart.Name,
                        rootPart.Shape,
                        new PhysicsVector(rootPart.AbsolutePosition.X, rootPart.AbsolutePosition.Y,
                                          rootPart.AbsolutePosition.Z),
                        new PhysicsVector(rootPart.Scale.X, rootPart.Scale.Y, rootPart.Scale.Z),
                        new Quaternion(rootPart.RotationOffset.W, rootPart.RotationOffset.X,
                                       rootPart.RotationOffset.Y, rootPart.RotationOffset.Z), UsePhysics);
                rootPart.doPhysicsPropertyUpdate(UsePhysics);
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
        public void AddNewPrim(LLUUID ownerID, LLVector3 pos, LLQuaternion rot, PrimitiveBaseShape shape)
        {
            if (PermissionsMngr.CanRezObject(ownerID, pos))
            {
                SceneObjectGroup sceneOb =
                    new SceneObjectGroup(this, m_regionHandle, ownerID, PrimIDAllocate(), pos, rot, shape);
                AddEntity(sceneOb);
                SceneObjectPart rootPart = sceneOb.GetChildPart(sceneOb.UUID);
                // if grass or tree, make phantom
                if ((rootPart.Shape.PCode == 95) || (rootPart.Shape.PCode == 255))
                {
                    rootPart.ObjectFlags += (uint)LLObject.ObjectFlags.Phantom;
                }
                // if not phantom, add to physics
                bool UsePhysics = (((rootPart.ObjectFlags & (uint)LLObject.ObjectFlags.Physics) > 0) && m_physicalPrim);
                if ((rootPart.ObjectFlags & (uint)LLObject.ObjectFlags.Phantom) == 0)
                {

                    rootPart.PhysActor =
                        phyScene.AddPrimShape(
                            rootPart.Name,
                            rootPart.Shape,
                            new PhysicsVector(pos.X, pos.Y, pos.Z),
                            new PhysicsVector(shape.Scale.X, shape.Scale.Y, shape.Scale.Z),
                            new Quaternion(), UsePhysics);
                    // subscribe to physics events.
                    rootPart.doPhysicsPropertyUpdate(UsePhysics);
                    
                }
            }
        }

        public void RemovePrim(uint localID, LLUUID avatar_deleter)
        {
            m_innerScene.RemovePrim(localID, avatar_deleter);
        }

        public void AddEntityFromStorage(SceneObjectGroup sceneObject)
        {
            m_innerScene.AddEntityFromStorage(sceneObject);
        }

        public void AddEntity(SceneObjectGroup sceneObject)
        {
            m_innerScene.AddEntity(sceneObject);
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
            m_sceneXmlLoader.LoadPrimsFromXml(fileName);
        }

        public void SavePrimsToXml(string fileName)
        {
            m_sceneXmlLoader.SavePrimsToXml(fileName);
        }

        public void LoadPrimsFromXml2(string fileName)
        {
            m_sceneXmlLoader.LoadPrimsFromXml2(fileName);
        }

        public void SavePrimsToXml2(string fileName)
        {
            m_sceneXmlLoader.SavePrimsToXml2(fileName);
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
            CommsManager.UserProfileCache.AddNewUser(client.AgentId);
            CommsManager.TransactionsManager.AddUser(client.AgentId);
        }

        protected virtual void SubscribeToClientEvents(IClientAPI client)
        {
            // client.OnStartAnim += StartAnimation;
            client.OnRegionHandShakeReply += SendLayerData;
            //remoteClient.OnRequestWearables += new GenericCall(this.GetInitialPrims);
            client.OnModifyTerrain += ModifyTerrain;
            // client.OnRequestWearables += InformClientOfNeighbours;
            client.OnAddPrim += AddNewPrim;
            client.OnUpdatePrimGroupPosition += m_innerScene.UpdatePrimPosition;
            client.OnUpdatePrimSinglePosition += m_innerScene.UpdatePrimSinglePosition;
            client.OnUpdatePrimGroupRotation += m_innerScene.UpdatePrimRotation;
            client.OnUpdatePrimGroupMouseRotation += m_innerScene.UpdatePrimRotation;
            client.OnUpdatePrimSingleRotation += m_innerScene.UpdatePrimSingleRotation;
            client.OnUpdatePrimScale += m_innerScene.UpdatePrimScale;
            client.OnUpdateExtraParams += m_innerScene.UpdateExtraParam;
            client.OnUpdatePrimShape += m_innerScene.UpdatePrimShape;
            client.OnRequestMapBlocks += RequestMapBlocks;
            client.OnUpdatePrimTexture += m_innerScene.UpdatePrimTexture;
            client.OnTeleportLocationRequest += RequestTeleportLocation;
            client.OnObjectSelect += SelectPrim;
            client.OnObjectDeselect += DeselectPrim;
            client.OnGrabUpdate += m_innerScene.MoveObject;
            client.OnDeRezObject += DeRezObject;
            client.OnRezObject += RezObject;
            client.OnNameFromUUIDRequest += CommsManager.HandleUUIDNameRequest;
            client.OnObjectDescription += m_innerScene.PrimDescription;
            client.OnObjectName += m_innerScene.PrimName;
            client.OnLinkObjects += m_innerScene.LinkObjects;
            client.OnDelinkObjects += m_innerScene.DelinkObjects;
            client.OnObjectDuplicate += m_innerScene.DuplicateObject;
            client.OnUpdatePrimFlags += m_innerScene.UpdatePrimFlags;
            client.OnRequestObjectPropertiesFamily += m_innerScene.RequestObjectPropertiesFamily;
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
            client.OnCreateNewInventoryFolder += CommsManager.UserProfileCache.HandleCreateInventoryFolder;
            client.OnFetchInventoryDescendents += CommsManager.UserProfileCache.HandleFecthInventoryDescendents;
            client.OnRequestTaskInventory += RequestTaskInventory;
            client.OnFetchInventory += CommsManager.UserProfileCache.HandleFetchInventory;
            client.OnUpdateInventoryItem += UDPUpdateInventoryItemAsset;
            client.OnAssetUploadRequest += CommsManager.TransactionsManager.HandleUDPUploadRequest;
            client.OnXferReceive += CommsManager.TransactionsManager.HandleXfer;
            client.OnRezScript += RezScript;
            client.OnRemoveTaskItem += RemoveTaskInventory;

            client.OnGrabObject += ProcessObjectGrab;
            client.OnAvatarPickerRequest += ProcessAvatarPickerRequest;
            
            EventManager.TriggerOnNewClient(client);
        }

        protected ScenePresence CreateAndAddScenePresence(IClientAPI client, bool child)
        {
            ScenePresence newAvatar = null;

            byte[] visualParams;
            AvatarWearable[] wearables;

            if (m_AvatarFactory == null ||
                !m_AvatarFactory.TryGetIntialAvatarAppearance(client.AgentId, out wearables, out visualParams))
            {
                AvatarFactoryModule.GetDefaultAvatarAppearance(out wearables, out visualParams);
            }

            newAvatar = m_innerScene.CreateAndAddScenePresence(client, child, wearables, visualParams);

            if (!newAvatar.IsChildAgent)
            {
                newAvatar.OnSignificantClientMovement += m_LandManager.handleSignificantClientMovement;
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

            Broadcast(delegate(IClientAPI client) { client.SendKillObject(avatar.RegionHandle, avatar.LocalId); });

            ForEachScenePresence(
                delegate(ScenePresence presence) { presence.CoarseLocationChange(); });

            lock (m_scenePresences)
            {
                m_scenePresences.Remove(agentID);
            }

            lock (Entities)
            {
                Entities.Remove(agentID);
            }

            try
            {
                avatar.Close();
            }
            catch (Exception e)
            {
                MainLog.Instance.Error("Scene.cs:RemoveClient exception: " + e.ToString());
            }

            // Remove client agent from profile, so new logins will work
            CommsManager.UserService.clearUserAgent(agentID);

            return;
        }

        public void NotifyMyCoarseLocationChange()
        {
            ForEachScenePresence(delegate(ScenePresence presence) { presence.CoarseLocationChange(); });
        }
        #endregion

        #region Entities
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
                m_storageManager.DataStore.RemoveObject(entID, m_regInfo.RegionID);
                return true;
            }
            return false;
        }

        public void SendKillObject(uint localID)
        {
            Broadcast(delegate(IClientAPI client) { client.SendKillObject(m_regionHandle, localID); });
        }

        #endregion

        #region RegionComms

        /// <summary>
        /// 
        /// </summary>
        public void RegisterRegionWithComms()
        {
            m_sceneGridService.RegisterRegion(m_regInfo);
            m_sceneGridService.OnExpectUser += NewUserConnection;
            m_sceneGridService.OnAvatarCrossingIntoRegion += AgentCrossing;
            m_sceneGridService.OnCloseAgentConnection += CloseConnection;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <param name="agent"></param>
        public void NewUserConnection(ulong regionHandle, AgentCircuitData agent)
        {
            if (regionHandle == m_regInfo.RegionHandle)
            {
                if (agent.CapsPath != "")
                {
                    Caps cap =
                        new Caps(AssetCache, httpListener, m_regInfo.ExternalHostName, httpListener.Port,
                                 agent.CapsPath, agent.AgentID, m_dumpAssetsToFile);

                    Util.SetCapsURL(agent.AgentID, "http://" + m_regInfo.ExternalHostName + ":" + httpListener.Port.ToString() +
                                    "/CAPS/" + agent.CapsPath + "0000/");
                    cap.RegisterHandlers();
                    cap.AddNewInventoryItem = AddInventoryItem;
                    cap.ItemUpdatedCall = CapsUpdateInventoryItemAsset;
                    if (m_capsHandlers.ContainsKey(agent.AgentID))
                    {
                        //MainLog.Instance.Warn("client", "Adding duplicate CAPS entry for user " +
                        //    agent.AgentID.ToStringHyphenated());
                        m_capsHandlers[agent.AgentID] = cap;
                    }
                    else
                    {
                        m_capsHandlers.Add(agent.AgentID, cap);
                    }
                }
                m_authenticateHandler.AddNewCircuit(agent.circuitcode, agent);
            }
        }

        public void AgentCrossing(ulong regionHandle, LLUUID agentID, LLVector3 position, bool isFlying)
        {
            if (regionHandle == m_regInfo.RegionHandle)
            {
                if (m_scenePresences.ContainsKey(agentID))
                {
                    m_scenePresences[agentID].MakeRootAgent(position, isFlying);
                }
            }
        }

        public void CloseConnection(ulong regionHandle, LLUUID agentID)
        {
            if (regionHandle == m_regionHandle)
            {
                ScenePresence presence = m_innerScene.GetScenePresence(agentID);
                if (presence != null)
                {
                    libsecondlife.Packets.DisableSimulatorPacket disable = new libsecondlife.Packets.DisableSimulatorPacket();
                    presence.ControllingClient.OutPacket(disable);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        public void InformClientOfNeighbours(ScenePresence presence)
        {
            m_sceneGridService.EnableNeighbourChildAgents(presence);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="regionHandle"></param>
        /// <returns></returns>
        public RegionInfo RequestNeighbouringRegionInfo(ulong regionHandle)
        {
            return m_sceneGridService.RequestNeighbouringRegionInfo(regionHandle);
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
            m_sceneGridService.RequestMapBlocks(remoteClient, minX, minY, maxX, maxX);
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
            if (m_scenePresences.ContainsKey(remoteClient.AgentId))
            {
                m_sceneGridService.RequestTeleportToLocation(m_scenePresences[remoteClient.AgentId], regionHandle, position, lookAt, flags);
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
            return m_sceneGridService.CrossToNeighbouringRegion(regionhandle, agentID, position, isFlying);
        }

        #endregion

        #region Module Methods
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
        #endregion

        #region Other Methods
        public void SetTimePhase(int phase)
        {
            m_timePhase = phase;
        }

        public void SendUrlToUser(LLUUID avatarID, string objectname, LLUUID objectID, LLUUID ownerID, bool groupOwned,
                                  string message, string url)
        {
            if (m_scenePresences.ContainsKey(avatarID))
            {
                m_scenePresences[avatarID].ControllingClient.SendLoadURL(objectname, objectID, ownerID, groupOwned,
                                                                         message, url);
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

        public void performParcelPrimCountUpdate()
        {
            m_LandManager.resetAllLandPrimCounts();
            m_eventManager.TriggerParcelPrimCountUpdate();
            m_LandManager.finalizeLandPrimCountUpdate();
            m_LandManager.landPrimCountTainted = false;
        }

        #endregion

        #region Console Commands
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
                        if (!module.IsSharedModule)
                        {
                            MainLog.Instance.Error("Region Module: " + module.Name);
                        }
                    }
                    break;
            }
        }

        #endregion

        #region Script Handling Methods

        public void SendCommandToPlugins(string[] args)
        {
            m_eventManager.TriggerOnPluginConsole(args);
        }

        #endregion

        #region Script Engine

        private List<ScriptEngineInterface> ScriptEngines = new List<ScriptEngineInterface>();
        private bool m_dumpAssetsToFile;

        public void AddScriptEngine(ScriptEngineInterface ScriptEngine, LogBase m_logger)
        {
            ScriptEngines.Add(ScriptEngine);

            ScriptEngine.InitializeEngine(this, m_logger);
        }

        #endregion

        #region InnerScene wrapper methods

        public LLUUID ConvertLocalIDToFullID(uint localID)
        {
            return m_innerScene.ConvertLocalIDToFullID(localID);
        }

        public void SendAllSceneObjectsToClient(ScenePresence presence)
        {
            m_innerScene.SendAllSceneObjectsToClient(presence);
        }

        //The idea is to have a group of method that return a list of avatars meeting some requirement
        // ie it could be all m_scenePresences within a certain range of the calling prim/avatar. 

        public List<ScenePresence> GetAvatars()
        {
            return m_innerScene.GetAvatars();
        }

        /// <summary>
        /// Request a List of all m_scenePresences in this World
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences()
        {
            return m_innerScene.GetScenePresences();
        }

        /// <summary>
        /// Request a filtered list of m_scenePresences in this World
        /// </summary>
        /// <returns></returns>
        public List<ScenePresence> GetScenePresences(FilterAvatarList filter)
        {
            return m_innerScene.GetScenePresences(filter);
        }

        /// <summary>
        /// Request a Avatar by UUID
        /// </summary>
        /// <param name="avatarID"></param>
        /// <returns></returns>
        public ScenePresence GetScenePresence(LLUUID avatarID)
        {
            return m_innerScene.GetScenePresence(avatarID);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="action"></param>
        public void ForEachScenePresence(Action<ScenePresence> action)
        {
            foreach (ScenePresence presence in m_scenePresences.Values)
            {
                action(presence);
            }
        }

        public void ForEachObject(Action<SceneObjectGroup> action)
        {
            foreach (SceneObjectGroup presence in m_sceneObjects.Values)
            {
                action(presence);
            }
        }

        public SceneObjectPart GetSceneObjectPart(uint localID)
        {
            return m_innerScene.GetSceneObjectPart(localID);
        }

        public SceneObjectPart GetSceneObjectPart(LLUUID fullID)
        {
            return m_innerScene.GetSceneObjectPart(fullID);
        }

        internal bool TryGetAvatar(LLUUID avatarId, out ScenePresence avatar)
        {
            return m_innerScene.TryGetAvatar(avatarId, out avatar);
        }


        internal bool TryGetAvatarByName(string avatarName, out ScenePresence avatar)
        {
            return m_innerScene.TryGetAvatarByName(avatarName, out avatar);
        }

        internal void ForEachClient(Action<IClientAPI> action)
        {
            m_innerScene.ForEachClient(action);
        }

        #endregion
    }
}
