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
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;

using System.Threading;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.StructuredData;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Monitoring;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.World.Land;
using Caps=OpenSim.Framework.Capabilities.Caps;
using OSDArray=OpenMetaverse.StructuredData.OSDArray;
using OSDMap=OpenMetaverse.StructuredData.OSDMap;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.World.WorldMap
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "WorldMapModule")]
    public class WorldMapModule : INonSharedRegionModule, IWorldMapModule, IDisposable
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private const string LogHeader = "[WORLD MAP]";

        private static readonly string DEFAULT_WORLD_MAP_EXPORT_PATH = "exportmap.jpg";

        private IMapImageGenerator m_mapImageGenerator;
        private IMapImageUploadModule m_mapImageServiceModule;

        protected Scene m_scene;
        private ulong m_regionHandle;
        private uint m_regionGlobalX;
        private uint m_regionGlobalY;
        private uint m_regionSizeX;
        private uint m_regionSizeY;
        private string m_regionName;

        private byte[] myMapImageJPEG;
        protected volatile bool m_Enabled = false;

        private ManualResetEvent m_mapBlockRequestEvent = new ManualResetEvent(false);
        private ObjectJobEngine m_mapItemsRequests;
        private readonly Dictionary<UUID, Queue<MapBlockRequestData>> m_mapBlockRequests = new Dictionary<UUID, Queue<MapBlockRequestData>>();

        private readonly List<MapBlockData> cachedMapBlocks = new List<MapBlockData>();
        private ExpiringKey<string> m_blacklistedurls = new ExpiringKey<string>(60000);
        private ExpiringKey<ulong> m_blacklistedregions = new ExpiringKey<ulong>(60000);
        private ExpiringCacheOS<ulong, OSDMap> m_cachedRegionMapItemsResponses = new ExpiringCacheOS<ulong, OSDMap>(1000);
        private readonly HashSet<UUID> m_rootAgents = new HashSet<UUID>();

        private volatile bool m_threadsRunning = false;

        // expire time for the blacklists in seconds
        protected int expireBlackListTime = 300; // 5 minutes
        // expire mapItems responses time in seconds. Throttles requests to regions that do answer
        private const double expireResponsesTime = 120.0; // 2 minutes ?
        //private int CacheRegionsDistance = 256;

        protected bool m_exportPrintScale = false; // prints the scale of map in meters on exported map
        protected bool m_exportPrintRegionName = false; // prints the region name exported map
        protected bool m_localV1MapAssets = false; // keep V1 map assets only on  local cache

        private readonly object m_sceneLock = new object();
        public WorldMapModule()
        {
        }

        ~WorldMapModule()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        bool disposed;
        public virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                disposed = true;

                m_mapBlockRequestEvent?.Dispose();
                m_blacklistedurls?.Dispose();
                m_blacklistedregions?.Dispose();
                m_mapItemsRequests?.Dispose();
                m_cachedRegionMapItemsResponses?.Dispose();

                m_mapBlockRequestEvent = null;
                m_blacklistedurls = null;
                m_blacklistedregions = null;
                m_mapItemsRequests = null;
                m_cachedRegionMapItemsResponses = null;
            }
        }

        #region INonSharedRegionModule Members
        public virtual void Initialise(IConfigSource config)
        {
            string[] configSections = new string[] { "Map", "Startup" };

            if (Util.GetConfigVarFromSections<string>(
                    config, "WorldMapModule", configSections, "WorldMap") == "WorldMap")
                m_Enabled = true;

            expireBlackListTime = (int)Util.GetConfigVarFromSections<int>(config, "BlacklistTimeout", configSections, 10 * 60);
            expireBlackListTime *= 1000;
            m_exportPrintScale =
                Util.GetConfigVarFromSections<bool>(config, "ExportMapAddScale", configSections, m_exportPrintScale);
            m_exportPrintRegionName =
                Util.GetConfigVarFromSections<bool>(config, "ExportMapAddRegionName", configSections, m_exportPrintRegionName);
            m_localV1MapAssets =
                Util.GetConfigVarFromSections<bool>(config, "LocalV1MapAssets", configSections, m_localV1MapAssets);
        }

        public virtual void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (m_sceneLock)
            {
                m_scene = scene;
                m_regionHandle = scene.RegionInfo.RegionHandle;
                m_regionGlobalX = scene.RegionInfo.WorldLocX;
                m_regionGlobalY = scene.RegionInfo.WorldLocY;
                m_regionSizeX = scene.RegionInfo.RegionSizeX;
                m_regionSizeY = scene.RegionInfo.RegionSizeX;
                m_regionName = scene.RegionInfo.RegionName;

                m_scene.RegisterModuleInterface<IWorldMapModule>(this);

                m_scene.AddCommand(
                    "Regions", this, "export-map",
                    "export-map [<path>]",
                    "Save an image of the world map", HandleExportWorldMapConsoleCommand);

                m_scene.AddCommand(
                    "Regions", this, "generate map",
                    "generate map",
                    "Generates and stores a new maptile.", HandleGenerateMapConsoleCommand);

                AddHandlers();
            }
        }

        public virtual void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (m_sceneLock)
            {
                m_Enabled = false;
                RemoveHandlers();
                m_scene = null;
            }
        }

        public virtual void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_mapImageGenerator = m_scene.RequestModuleInterface<IMapImageGenerator>();
            m_mapImageServiceModule = m_scene.RequestModuleInterface<IMapImageUploadModule>();
        }

        public virtual void Close()
        {
            Dispose();
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public virtual string Name
        {
            get { return "WorldMapModule"; }
        }

        #endregion

        // this has to be called with a lock on m_scene
        protected virtual void AddHandlers()
        {
            myMapImageJPEG = [];

            string regionimage = "regionImage" + m_scene.RegionInfo.RegionID.ToString();
            regionimage = regionimage.Replace("-", "");
            m_log.Info("[WORLD MAP]: JPEG Map location: " + m_scene.RegionInfo.ServerURI + "index.php?method=" + regionimage);

            MainServer.Instance.AddIndexPHPMethodHandler(regionimage, OnHTTPGetMapImage);
            MainServer.Instance.AddSimpleStreamHandler(new SimpleStreamHandler(
                "/MAP/MapItems/" + m_regionHandle.ToString(), HandleRemoteMapItemRequest));

            m_scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            m_scene.EventManager.OnNewClient += OnNewClient;
            m_scene.EventManager.OnClientClosed += ClientLoggedOut;
            m_scene.EventManager.OnMakeChildAgent += MakeChildAgent;
            m_scene.EventManager.OnMakeRootAgent += MakeRootAgent;
            m_scene.EventManager.OnRegionUp += OnRegionUp;

            StartThreads();
        }

        // this has to be called with a lock on m_scene
        protected virtual void RemoveHandlers()
        {
            StopThreads();

            m_scene.EventManager.OnRegionUp -= OnRegionUp;
            m_scene.EventManager.OnMakeRootAgent -= MakeRootAgent;
            m_scene.EventManager.OnMakeChildAgent -= MakeChildAgent;
            m_scene.EventManager.OnClientClosed -= ClientLoggedOut;
            m_scene.EventManager.OnNewClient -= OnNewClient;
            m_scene.EventManager.OnRegisterCaps -= OnRegisterCaps;

            m_scene.UnregisterModuleInterface<IWorldMapModule>(this);

            MainServer.Instance.RemoveSimpleStreamHandler("/MAP/MapItems/" + m_scene.RegionInfo.RegionHandle.ToString());
            string regionimage = "regionImage" + m_scene.RegionInfo.RegionID.ToString();
            regionimage = regionimage.Replace("-", "");
            MainServer.Instance.RemoveIndexPHPMethodHandler(regionimage);
        }

        public void OnRegisterCaps(UUID agentID, Caps caps)
        {
            //m_log.DebugFormat("[WORLD MAP]: OnRegisterCaps: agentID {0} caps {1}", agentID, caps);
            caps.RegisterSimpleHandler("MapLayer", new SimpleStreamHandler("/" + UUID.Random(), MapLayerRequest));
        }

        /// <summary>
        /// Callback for a map layer request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="agentID"></param>
        /// <param name="caps"></param>
        /// <returns></returns>
        public void MapLayerRequest(IOSHttpRequest request, IOSHttpResponse response)
        {
            if(request.HttpMethod != "POST")
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            LLSDMapLayerResponse mapResponse = new LLSDMapLayerResponse();
            mapResponse.LayerData.Array.Add(GetOSDMapLayerResponse());
            response.RawBuffer = System.Text.Encoding.UTF8.GetBytes(LLSDHelpers.SerialiseLLSDReply(mapResponse));
            response.StatusCode = (int)HttpStatusCode.OK;
        }

         /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        protected static OSDMapLayer GetOSDMapLayerResponse()
        {
            // not sure about this.... 2048 or master 5000 and hack above?

            OSDMapLayer mapLayer = new OSDMapLayer();
            mapLayer.Right = 30000;
            mapLayer.Top = 30000;
            mapLayer.ImageID = new UUID("00000000-0000-1111-9999-000000000006");

            return mapLayer;
        }
        #region EventHandlers

        /// <summary>
        /// Registered for event
        /// </summary>
        /// <param name="client"></param>
        private void OnNewClient(IClientAPI client)
        {
            client.OnRequestMapBlocks += RequestMapBlocks;
            client.OnMapItemRequest += HandleMapItemRequest;
        }

        /// <summary>
        /// Client logged out, check to see if there are any more root agents in the simulator
        /// If not, stop the mapItemRequest Thread
        /// Event handler
        /// </summary>
        /// <param name="AgentId">AgentID that logged out</param>
        private void ClientLoggedOut(UUID AgentId, Scene scene)
        {
            lock (m_rootAgents)
            {
                m_rootAgents.Remove(AgentId);
            }
            lock (m_mapBlockRequestEvent)
            {
                m_mapBlockRequests.Remove(AgentId);
            }
        }
        #endregion

        /// <summary>
        /// Starts the MapItemRequest Thread
        /// Note that this only gets started when there are actually agents in the region
        /// Additionally, it gets stopped when there are none.
        /// </summary>
        /// <param name="o"></param>
        private void StartThreads()
        {
            if (!m_threadsRunning)
            {
                m_threadsRunning = true;
                m_mapItemsRequests = new ObjectJobEngine(MapItemsprocess,string.Format("MapItems ({0})", m_regionName));
                WorkManager.StartThread(MapBlocksProcess, string.Format("MapBlocks ({0})", m_regionName));
            }
        }

        /// <summary>
        /// Enqueues a 'stop thread' MapRequestState.  Causes the MapItemRequest thread to end
        /// </summary>
        private void StopThreads()
        {
            m_threadsRunning = false;
            m_mapBlockRequestEvent.Set();
            m_mapItemsRequests.Dispose();
        }

        public virtual void HandleMapItemRequest(IClientAPI remoteClient, uint flags,
            uint EstateID, bool godlike, uint itemtype, ulong regionhandle)
        {
            // m_log.DebugFormat("[WORLD MAP]: Handle MapItem request {0} {1}", regionhandle, itemtype);

            lock (m_rootAgents)
            {
                if (!m_rootAgents.Contains(remoteClient.AgentId))
                    return;
            }

            // local or remote request?
            if (regionhandle != 0 && regionhandle != m_regionHandle)
            {
                Util.RegionHandleToWorldLoc(regionhandle, out uint x, out uint y);
                if( x < m_regionGlobalX || y < m_regionGlobalY ||
                    x >= (m_regionGlobalX + m_regionSizeX) || y >= (m_regionGlobalY + m_regionSizeY))
                {
                    RequestMapItems(remoteClient.AgentId, flags, EstateID, godlike, itemtype, regionhandle);
                    return;
                }
            }

            // its about this region...

            List<mapItemReply> mapitems = new List<mapItemReply>();
            mapItemReply mapitem = new mapItemReply();

            // viewers only ask for green dots to each region now
            // except at login with regionhandle 0
            // possible on some other rare ocasions
            // use previous hack of sending all items with the green dots

            bool adultRegion;

            int tc = Environment.TickCount;
            string hash = Util.Md5Hash(m_regionName + tc.ToString());

            if (regionhandle == 0)
            {
                switch (itemtype)
                {
                    case (int)GridItemType.AgentLocations:
                        // Service 6 right now (MAP_ITEM_AGENTS_LOCATION; green dots)

                        if (m_scene.GetRootAgentCount() <= 1) //own position is not sent
                        {
                            mapitem = new mapItemReply(
                                        m_regionGlobalX + 1,
                                        m_regionGlobalY + 1,
                                        UUID.Zero,
                                        hash,
                                        0, 0);
                            mapitems.Add(mapitem);
                        }
                        else
                        {
                            m_scene.ForEachRootScenePresence(delegate (ScenePresence sp)
                            {
                                // Don't send a green dot for yourself
                                if (sp.UUID != remoteClient.AgentId)
                                {
                                    if (sp.IsNPC || sp.IsDeleted || sp.IsInTransit)
                                        return;

                                    mapitem = new mapItemReply(
                                        m_regionGlobalX + (uint)sp.AbsolutePosition.X,
                                        m_regionGlobalY + (uint)sp.AbsolutePosition.Y,
                                        UUID.Zero,
                                        hash,
                                        1, 0);
                                    mapitems.Add(mapitem);
                                }
                            });
                        }
                        remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                        break;

                    case (int)GridItemType.Telehub:
                        // Service 1 (MAP_ITEM_TELEHUB)

                        SceneObjectGroup sog = m_scene.GetSceneObjectGroup(m_scene.RegionInfo.RegionSettings.TelehubObject);
                        if (sog != null)
                        {
                            mapitem = new mapItemReply(
                                            m_regionGlobalX + (uint)sog.AbsolutePosition.X,
                                            m_regionGlobalY + (uint)sog.AbsolutePosition.Y,
                                            UUID.Zero,
                                            sog.Name,
                                            0,  // color (not used)
                                            0   // 0 = telehub / 1 = infohub
                                            );
                            mapitems.Add(mapitem);
                            remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                        }
                        break;

                    case (int)GridItemType.AdultLandForSale:
                    case (int)GridItemType.LandForSale:

                        // Service 7 (MAP_ITEM_LAND_FOR_SALE)
                        adultRegion = m_scene.RegionInfo.RegionSettings.Maturity == 2;
                        if (adultRegion)
                        {
                            if (itemtype == (int)GridItemType.LandForSale)
                                break;
                        }
                        else
                        {
                            if (itemtype == (int)GridItemType.AdultLandForSale)
                                break;
                        }

                        // Parcels
                        ILandChannel landChannel = m_scene.LandChannel;
                        List<ILandObject> parcels = landChannel.AllParcels();

                        if ((parcels != null) && (parcels.Count >= 1))
                        {
                            foreach (ILandObject parcel_interface in parcels)
                            {
                                // Play it safe
                                if (!(parcel_interface is LandObject))
                                    continue;

                                LandObject land = (LandObject)parcel_interface;
                                LandData parcel = land.LandData;

                                // Show land for sale
                                if ((parcel.Flags & (uint)ParcelFlags.ForSale) == (uint)ParcelFlags.ForSale)
                                {
                                    float x = land.CenterPoint.X + m_regionGlobalX;
                                    float y = land.CenterPoint.Y + m_regionGlobalY;
                                    mapitem = new mapItemReply(
                                                (uint)x, (uint)y,
                                                parcel.GlobalID,
                                                parcel.Name,
                                                parcel.Area,
                                                parcel.SalePrice
                                    );
                                    mapitems.Add(mapitem);
                                }
                            }
                        }
                        remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                        break;

                    case (uint)GridItemType.PgEvent:
                    case (uint)GridItemType.MatureEvent:
                    case (uint)GridItemType.AdultEvent:
                    case (uint)GridItemType.Classified:
                    case (uint)GridItemType.Popular:
                        // TODO
                        // just dont not cry about them
                        break;

                    default:
                        // unkown map item type
                        m_log.DebugFormat("[WORLD MAP]: Unknown MapItem type {0}", itemtype);
                        break;
                }
            }
            else
            {
                // send all items till we get a better fix

                // Service 6 right now (MAP_ITEM_AGENTS_LOCATION; green dots)

                if (m_scene.GetRootAgentCount() <= 1) // own is not sent
                {
                    mapitem = new mapItemReply(
                                m_regionGlobalX + 1,
                                m_regionGlobalY + 1,
                                UUID.Zero,
                                hash,
                                0, 0);
                    mapitems.Add(mapitem);
                }
                else
                {
                    m_scene.ForEachRootScenePresence(delegate (ScenePresence sp)
                    {
                        // Don't send a green dot for yourself
                        if (sp.UUID != remoteClient.AgentId)
                        {
                            if (sp.IsNPC || sp.IsDeleted || sp.IsInTransit)
                                return;

                            mapitem = new mapItemReply(
                                m_regionGlobalX + (uint)sp.AbsolutePosition.X,
                                m_regionGlobalY + (uint)sp.AbsolutePosition.Y,
                                UUID.Zero,
                                hash,
                                1, 0);
                            mapitems.Add(mapitem);
                        }
                    });
                }
                remoteClient.SendMapItemReply(mapitems.ToArray(), 6, flags);
                mapitems.Clear();

                // Service 1 (MAP_ITEM_TELEHUB)

                SceneObjectGroup sog = m_scene.GetSceneObjectGroup(m_scene.RegionInfo.RegionSettings.TelehubObject);
                if (sog != null)
                {
                    mapitem = new mapItemReply(
                                    m_regionGlobalX + (uint)sog.AbsolutePosition.X,
                                    m_regionGlobalY + (uint)sog.AbsolutePosition.Y,
                                    UUID.Zero,
                                    sog.Name,
                                    0,  // color (not used)
                                    0   // 0 = telehub / 1 = infohub
                                    );
                    mapitems.Add(mapitem);
                    remoteClient.SendMapItemReply(mapitems.ToArray(), 1, flags);
                    mapitems.Clear();
                }

                // Service 7 (MAP_ITEM_LAND_FOR_SALE)

                uint its = 7;
                if (m_scene.RegionInfo.RegionSettings.Maturity == 2)
                    its = 10;

                // Parcels
                ILandChannel landChannel = m_scene.LandChannel;
                List<ILandObject> parcels = landChannel.AllParcels();

                if ((parcels != null) && (parcels.Count >= 1))
                {
                    foreach (ILandObject parcel_interface in parcels)
                    {
                        // Play it safe
                        if (!(parcel_interface is LandObject))
                            continue;

                        LandObject land = (LandObject)parcel_interface;
                        LandData parcel = land.LandData;

                        // Show land for sale
                        if ((parcel.Flags & (uint)ParcelFlags.ForSale) == (uint)ParcelFlags.ForSale)
                        {
                            float x = land.CenterPoint.X + m_regionGlobalX;
                            float y = land.CenterPoint.Y + m_regionGlobalY;
                            mapitem = new mapItemReply(
                                        (uint)x, (uint)y,
                                        parcel.GlobalID,
                                        parcel.Name,
                                        parcel.Area,
                                        parcel.SalePrice
                            );
                            mapitems.Add(mapitem);
                        }
                    }
                    if(mapitems.Count >0)
                        remoteClient.SendMapItemReply(mapitems.ToArray(), its, flags);
                    mapitems.Clear();
                }
            }
        }

        private int nAsyncRequests = 0;
        /// <summary>
        /// Processing thread main() loop for doing remote mapitem requests
        /// </summary>
        public void MapItemsprocess(object o)
        {
            if (m_scene == null || !m_threadsRunning)
                return;

            const int MAX_ASYNC_REQUESTS = 5;
            ScenePresence av = null;
            MapRequestState st = o as MapRequestState;

            if (st == null || st.agentID.IsZero())
                return;

            if (m_blacklistedregions.ContainsKey(st.regionhandle))
                return;
            if (!m_scene.TryGetScenePresence(st.agentID, out av))
                return;
            if (av == null || av.IsChildAgent || av.IsDeleted || av.IsInTransit)
                return;

            try
            {
                if (m_cachedRegionMapItemsResponses.TryGetValue(st.regionhandle, out OSDMap responseMap))
                {
                    if (responseMap != null)
                    {
                        if (responseMap.ContainsKey(st.itemtype.ToString()))
                        {
                            List<mapItemReply> returnitems = new List<mapItemReply>();
                            OSDArray itemarray = (OSDArray)responseMap[st.itemtype.ToString()];
                            for (int i = 0; i < itemarray.Count; i++)
                            {
                                OSDMap mapitem = (OSDMap)itemarray[i];
                                mapItemReply mi = new mapItemReply();
                                mi.x = (uint)mapitem["X"].AsInteger();
                                mi.y = (uint)mapitem["Y"].AsInteger();
                                mi.id = mapitem["ID"].AsUUID();
                                mi.Extra = mapitem["Extra"].AsInteger();
                                mi.Extra2 = mapitem["Extra2"].AsInteger();
                                mi.name = mapitem["Name"].AsString();
                                returnitems.Add(mi);
                            }
                            av.ControllingClient.SendMapItemReply(returnitems.ToArray(), st.itemtype, st.flags & 0xffff);
                        }
                    }
                    else
                    {
                        m_mapItemsRequests.Enqueue(st);
                        if (m_mapItemsRequests.Count < 3)
                            Thread.Sleep(100);
                    }
                }
                else
                {
                    m_cachedRegionMapItemsResponses.AddOrUpdate(st.regionhandle, null, expireResponsesTime); //  a bit more time for the access

                    // nothig for region, fire a request
                    Interlocked.Increment(ref nAsyncRequests);
                    MapRequestState rst = st;
                    Util.FireAndForget(x =>
                    {
                        RequestMapItemsAsync(rst);
                    });
                }

                while (nAsyncRequests >= MAX_ASYNC_REQUESTS) // hit the break
                {
                    Thread.Sleep(100);
                    if (m_scene == null || !m_threadsRunning)
                        break;
                }
            }
            catch { }
        }

        /// <summary>
        /// Enqueue the MapItem request for remote processing
        /// </summary>
        /// <param name="id">Agent ID that we are making this request on behalf</param>
        /// <param name="flags">passed in from packet</param>
        /// <param name="EstateID">passed in from packet</param>
        /// <param name="godlike">passed in from packet</param>
        /// <param name="itemtype">passed in from packet</param>
        /// <param name="regionhandle">Region we're looking up</param>
        public void RequestMapItems(UUID id, uint flags, uint EstateID, bool godlike, uint itemtype, ulong regionhandle)
        {
            if(!m_threadsRunning)
                return;

            MapRequestState st = new MapRequestState();
            st.agentID = id;
            st.flags = flags;
            st.EstateID = EstateID;
            st.godlike = godlike;
            st.itemtype = itemtype;
            st.regionhandle = regionhandle;
            m_mapItemsRequests.Enqueue(st);
        }

        private static readonly uint[] itemTypesForcedSend = new uint[] { 6, 1, 7, 10 }; // green dots, infohub, land sells

        /// <summary>
        /// Does the actual remote mapitem request
        /// This should be called from an asynchronous thread
        /// Request failures get blacklisted until region restart so we don't
        /// continue to spend resources trying to contact regions that are down.
        /// </summary>
        /// <param name="httpserver">blank string, we discover this in the process</param>
        /// <param name="id">Agent ID that we are making this request on behalf</param>
        /// <param name="flags">passed in from packet</param>
        /// <param name="EstateID">passed in from packet</param>
        /// <param name="godlike">passed in from packet</param>
        /// <param name="itemtype">passed in from packet</param>
        /// <param name="regionhandle">Region we're looking up</param>
        /// <returns></returns>
        private void RequestMapItemsAsync(MapRequestState requestState)
        {
            // m_log.DebugFormat("[WORLDMAP]: RequestMapItemsAsync; region handle: {0} {1}", regionhandle, itemtype);

            ulong regionhandle = requestState.regionhandle;
            if (m_blacklistedregions.ContainsKey(regionhandle))
            {
                m_cachedRegionMapItemsResponses.Remove(regionhandle);
                Interlocked.Decrement(ref nAsyncRequests);
                return;
            }

            UUID agentID = requestState.agentID;
            if (agentID.IsZero() || !m_scene.TryGetScenePresence(agentID, out ScenePresence sp))
            {
                m_cachedRegionMapItemsResponses.Remove(regionhandle);
                Interlocked.Decrement(ref nAsyncRequests);
                return;
            }

            GridRegion mreg = m_scene.GridService.GetRegionByHandle(m_scene.RegionInfo.ScopeID, regionhandle);
            if (mreg == null)
            {
                // Can't find the http server or its blocked
                m_blacklistedregions.Add(regionhandle, expireBlackListTime);
                m_cachedRegionMapItemsResponses.Remove(regionhandle);
                Interlocked.Decrement(ref nAsyncRequests);
                return;
            }

            if (!m_threadsRunning)
                return;

            string serverURI = mreg.ServerURI;
            if(WebUtil.GlobalExpiringBadURLs.ContainsKey(serverURI))
            {
                m_blacklistedregions.Add(regionhandle, expireBlackListTime);
                m_cachedRegionMapItemsResponses.Remove(regionhandle);
                Interlocked.Decrement(ref nAsyncRequests);
                return;
            }

            string httpserver = serverURI + "MAP/MapItems/" + regionhandle.ToString();
            if (m_blacklistedurls.ContainsKey(httpserver))
            {
                m_blacklistedregions.Add(regionhandle, expireBlackListTime);
                m_cachedRegionMapItemsResponses.Remove(regionhandle);
                Interlocked.Decrement(ref nAsyncRequests);
                return;
            }

            if (!m_threadsRunning)
                return;

            WebRequest mapitemsrequest = null;
            try
            {
                mapitemsrequest = WebRequest.Create(httpserver);
            }
            catch (Exception e)
            {
                WebUtil.GlobalExpiringBadURLs.Add(serverURI, 120000);
                m_blacklistedregions.Add(regionhandle, expireBlackListTime);
                m_cachedRegionMapItemsResponses.Remove(regionhandle);
                m_log.DebugFormat("[WORLD MAP]: Access to {0} failed with {1}", httpserver, e);
                Interlocked.Decrement(ref nAsyncRequests);
                return;
            }

            UUID requestID = UUID.Random();

            mapitemsrequest.Method = "GET";
            mapitemsrequest.ContentType = "application/xml+llsd";

            string response_mapItems_reply = null;

            // get the response
            try
            {
                using (WebResponse webResponse = mapitemsrequest.GetResponse())
                {
                    using (StreamReader sr = new StreamReader(webResponse.GetResponseStream()))
                        response_mapItems_reply = sr.ReadToEnd().Trim();
                }
            }
            catch (WebException)
            {
                WebUtil.GlobalExpiringBadURLs.Add(serverURI, 60000);
                m_blacklistedurls.Add(httpserver, expireBlackListTime);
                m_blacklistedregions.Add(regionhandle, expireBlackListTime);
                m_cachedRegionMapItemsResponses.Remove(regionhandle);

                m_log.WarnFormat("[WORLD MAP]: Blacklisted url {0}", httpserver);
                Interlocked.Decrement(ref nAsyncRequests);
                return;
            }
            catch
            {
                m_log.DebugFormat("[WORLD MAP]: RequestMapItems failed for {0}", httpserver);
                m_blacklistedregions.Add(regionhandle, expireBlackListTime);
                m_cachedRegionMapItemsResponses.Remove(regionhandle);
                Interlocked.Decrement(ref nAsyncRequests);
                return;
            }

            if (!m_threadsRunning)
                return;

            OSDMap responseMap = null;
            try
            {
                responseMap = (OSDMap)OSDParser.DeserializeLLSDXml(response_mapItems_reply);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[WORLD MAP]: exception on parse of RequestMapItems reply from {0}: {1}", httpserver, ex.Message);
                m_blacklistedregions.Add(regionhandle, expireBlackListTime);
                m_cachedRegionMapItemsResponses.Remove(regionhandle);

                Interlocked.Decrement(ref nAsyncRequests);
                return;
            }

            if (!m_threadsRunning)
                return;

            m_cachedRegionMapItemsResponses.AddOrUpdate(regionhandle, responseMap, expireResponsesTime);

            uint flags = requestState.flags & 0xffff;
            if(m_scene.TryGetScenePresence(agentID, out ScenePresence av) &&
                    av != null && !av.IsChildAgent && !av.IsDeleted && !av.IsInTransit)
            {
                // send all the items or viewers will never ask for them, except green dots
                foreach (uint itfs in itemTypesForcedSend)
                {
                    if (responseMap.ContainsKey(itfs.ToString()))
                    {
                        List<mapItemReply> returnitems = new List<mapItemReply>();
                        OSDArray itemarray = (OSDArray)responseMap[itfs.ToString()];
                        for (int i = 0; i < itemarray.Count; i++)
                        {
                            if (!m_threadsRunning)
                                return;

                            OSDMap mapitem = (OSDMap)itemarray[i];
                            mapItemReply mi = new mapItemReply();
                            mi.x = (uint)mapitem["X"].AsInteger();
                            mi.y = (uint)mapitem["Y"].AsInteger();
                            mi.id = mapitem["ID"].AsUUID();
                            mi.Extra = mapitem["Extra"].AsInteger();
                            mi.Extra2 = mapitem["Extra2"].AsInteger();
                            mi.name = mapitem["Name"].AsString();
                            returnitems.Add(mi);
                        }
                        av.ControllingClient.SendMapItemReply(returnitems.ToArray(), itfs, flags);
                    }
                }
            }

            Interlocked.Decrement(ref nAsyncRequests);
        }


        private const double SPAMBLOCKTIMEms = 30000;
        private Dictionary<UUID,double> spamBlocked = new Dictionary<UUID,double>();

        /// <summary>
        /// Requests map blocks in area of minX, maxX, minY, MaxY in world cordinates
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        public void RequestMapBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag)
        {
            // anti spam because of FireStorm 4.7.7 absurd request repeat rates
            // possible others

            double now = Util.GetTimeStampMS();
            UUID agentID = remoteClient.AgentId;

            lock (m_mapBlockRequestEvent)
            {
                if(spamBlocked.ContainsKey(agentID))
                {
                    if(spamBlocked[agentID] < now &&
                            (!m_mapBlockRequests.ContainsKey(agentID) ||
                            m_mapBlockRequests[agentID].Count == 0 ))
                    {
                        spamBlocked.Remove(agentID);
                        m_log.DebugFormat("[WoldMapModule] RequestMapBlocks release spammer {0}", agentID);
                    }
                    else
                        return;
                }
                else
                {
                // ugly slow expire spammers
                    if(spamBlocked.Count > 0)
                    {
                        UUID k = UUID.Zero;
                        bool expireone = false;
                        foreach(UUID k2 in spamBlocked.Keys)
                        {
                            if(spamBlocked[k2] < now &&
                                (!m_mapBlockRequests.ContainsKey(k2) ||
                                m_mapBlockRequests[k2].Count == 0 ))
                            {
                                m_log.DebugFormat("[WoldMapModule] RequestMapBlocks release spammer {0}", k2);
                                k = k2;
                                expireone = true;
                            }
                        break; // doing one at a time
                        }
                    if(expireone)
                        spamBlocked.Remove(k);
                    }
                }

//                m_log.DebugFormat("[WoldMapModule] RequestMapBlocks {0}={1}={2}={3} {4}", minX, minY, maxX, maxY, flag);

                MapBlockRequestData req = new MapBlockRequestData()
                {
                    client = remoteClient,
                    minX = minX,
                    maxX = maxX,
                    minY = minY,
                    maxY = maxY,
                    flags = flag
                };

                Queue<MapBlockRequestData> agentq; 
                if(!m_mapBlockRequests.TryGetValue(agentID, out agentq))
                {
                    agentq = new Queue<MapBlockRequestData>();
                    m_mapBlockRequests[agentID] = agentq;
                }
                if(agentq.Count < 150 )
                    agentq.Enqueue(req);
                else
                {
                    spamBlocked[agentID] = now + SPAMBLOCKTIMEms;
                    m_log.DebugFormat("[WoldMapModule] RequestMapBlocks blocking spammer {0} for {1} s",agentID, SPAMBLOCKTIMEms/1000.0);
                }
                m_mapBlockRequestEvent.Set();
            }
        }

        protected void MapBlocksProcess()
        {
            List<MapBlockRequestData> thisRunData = new List<MapBlockRequestData>();
            List<UUID> toRemove = new List<UUID>();
            try
            {
                while (true)
                {
                    while(!m_mapBlockRequestEvent.WaitOne(4900))
                    {
                        Watchdog.UpdateThread();
                        if (m_scene == null || !m_threadsRunning)
                        {
                            Watchdog.RemoveThread();
                            return;
                        }
                    }
                    Watchdog.UpdateThread();
                    if (m_scene == null || !m_threadsRunning)
                        break;

                    lock (m_mapBlockRequestEvent)
                    {
                        int total = 0;
                        foreach (KeyValuePair<UUID, Queue<MapBlockRequestData>> kvp in m_mapBlockRequests)
                        {
                            if (kvp.Value.Count > 0)
                            {
                                thisRunData.Add(kvp.Value.Dequeue());
                                total += kvp.Value.Count;
                            }
                            else
                                toRemove.Add(kvp.Key);
                        }

                        if (m_scene == null || !m_threadsRunning)
                            break;

                        if (total == 0)
                            m_mapBlockRequestEvent.Reset();
                    }

                    if (toRemove.Count > 0)
                    {
                        foreach (UUID u in toRemove)
                            m_mapBlockRequests.Remove(u);
                        toRemove.Clear();
                    }

                    if (thisRunData.Count > 0)
                    {
                        foreach (MapBlockRequestData req in thisRunData)
                        {
                            GetAndSendBlocksInternal(req.client, req.minX, req.minY, req.maxX, req.maxY, req.flags);
                            if (m_scene == null || !m_threadsRunning)
                                break;
                            Watchdog.UpdateThread();
                        }
                        thisRunData.Clear();
                    }

                    if (m_scene == null || !m_threadsRunning)
                        break;
                    Thread.Sleep(50);
                }
            }
            catch { }
            Watchdog.RemoveThread();
        }

        protected virtual List<MapBlockData> GetAndSendBlocksInternal(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag)
        {
            List<MapBlockData> mapBlocks = new List<MapBlockData>();
            List<GridRegion> regions = m_scene.GridService.GetRegionRange(m_scene.RegionInfo.ScopeID,
                minX * (int)Constants.RegionSize,
                maxX * (int)Constants.RegionSize,
                minY * (int)Constants.RegionSize,
                maxY * (int)Constants.RegionSize);

            // only send a negative answer for a single region request
            // corresponding to a click on the map. Current viewers
            // keep displaying "loading.." without this
            if (regions.Count == 0)
            {
                if((flag & 0x10000) != 0 && minX == maxX && minY == maxY)
                {
                    MapBlockData block = new MapBlockData();
                    block.X = (ushort)minX;
                    block.Y = (ushort)minY;
                    block.MapImageId = UUID.Zero;
                    block.Access = (byte)SimAccess.NonExistent;
                    mapBlocks.Add(block);
                    remoteClient.SendMapBlock(mapBlocks, flag & 0xffff);
                }
                return mapBlocks;
            }

            List<MapBlockData> allBlocks = new List<MapBlockData>();
            flag &= 0xffff;

            foreach (GridRegion r in regions)
            {
                if (r == null)
                    continue;
                MapBlockData block = new MapBlockData();
                MapBlockFromGridRegion(block, r, flag);
                mapBlocks.Add(block);
                allBlocks.Add(block);

                if (mapBlocks.Count >= 10)
                {
                    remoteClient.SendMapBlock(mapBlocks, flag);
                    mapBlocks.Clear();
                    Thread.Sleep(50);
                }
                if (m_scene == null || !m_threadsRunning)
                    return allBlocks;
            }
            if (mapBlocks.Count > 0)
                remoteClient.SendMapBlock(mapBlocks, flag);

            return allBlocks;
        }

        public void MapBlockFromGridRegion(MapBlockData block, GridRegion r, uint flag)
        {
            if (r == null)
            {
                // we should not get here ??
//                block.Access = (byte)SimAccess.Down; this is for a grid reply on r
                block.Access = (byte)SimAccess.NonExistent;
                block.MapImageId = UUID.Zero;
                return;
            }

            block.Access = r.Access;
            switch (flag)
            {
                case 0:
                    block.MapImageId = r.TerrainImage;
                    break;
                case 2:
                    block.MapImageId = r.ParcelImage;
                    break;
                default:
                    block.MapImageId = UUID.Zero;
                    break;
            }
            block.Name = r.RegionName;
            block.X = (ushort)(r.RegionLocX / Constants.RegionSize);
            block.Y = (ushort)(r.RegionLocY / Constants.RegionSize);
            block.SizeX = (ushort)r.RegionSizeX;
            block.SizeY = (ushort)r.RegionSizeY;

        }

        public Hashtable OnHTTPThrottled(Hashtable keysvals)
        {
            Hashtable reply = new Hashtable();
            int statuscode = 500;
            reply["str_response_string"] = "";
            reply["int_response_code"] = statuscode;
            reply["content_type"] = "text/plain";
            return reply;
        }

        public void OnHTTPGetMapImage(IOSHttpRequest request, IOSHttpResponse response)
        {
            response.KeepAlive = false;
            if (request.HttpMethod != "GET" || m_scene.RegionInfo.RegionSettings.TerrainImageID.IsZero())
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            byte[] jpeg = null;
            m_log.Debug("[WORLD MAP]: Sending map image jpeg");

            if (myMapImageJPEG.Length == 0)
            {
                MemoryStream imgstream = null;
                Bitmap mapTexture = new Bitmap(1, 1);
                ManagedImage managedImage;
                Image image = (Image)mapTexture;

                try
                {
                    // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data

                    imgstream = new MemoryStream();

                    // non-async because we know we have the asset immediately.
                    AssetBase mapasset = m_scene.AssetService.Get(m_scene.RegionInfo.RegionSettings.TerrainImageID.ToString());
                    if(mapasset == null || mapasset.Data == null || mapasset.Data.Length == 0)
                    {
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    // Decode image to System.Drawing.Image
                    if (OpenJPEG.DecodeToImage(mapasset.Data, out managedImage, out image))
                    {
                        // Save to bitmap
                        mapTexture = new Bitmap(image);

                        EncoderParameters myEncoderParameters = new EncoderParameters();
                        myEncoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 95L);

                        // Save bitmap to stream
                        mapTexture.Save(imgstream, GetEncoderInfo("image/jpeg"), myEncoderParameters);

                        // Write the stream to a byte array for output
                        jpeg = imgstream.ToArray();
                        myMapImageJPEG = jpeg;
                    }
                }
                catch (Exception e)
                {
                    // Dummy!
                    m_log.Warn("[WORLD MAP]: Unable to generate Map image" + e.Message);
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }
                finally
                {
                    // Reclaim memory, these are unmanaged resources
                    // If we encountered an exception, one or more of these will be null
                    if (mapTexture != null)
                        mapTexture.Dispose();

                    if (image != null)
                        image.Dispose();

                    if (imgstream != null)
                        imgstream.Dispose();
                }
            }
            else
            {
                // Use cached version so we don't have to loose our mind
                jpeg = myMapImageJPEG;
            }
            if(jpeg == null)
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            response.RawBuffer = jpeg;
            //response.RawBuffer = Convert.ToBase64String(jpeg);
            response.ContentType = "image/jpeg";
            response.StatusCode = (int)HttpStatusCode.OK;
        }

        // From msdn
        private static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (int j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }

        /// <summary>
        /// Export the world map
        /// </summary>
        /// <param name="fileName"></param>
        public void HandleExportWorldMapConsoleCommand(string module, string[] cmdparams)
        {
            if (m_scene.ConsoleScene() == null)
            {
                // FIXME: If console region is root then this will be printed by every module.  Currently, there is no
                // way to prevent this, short of making the entire module shared (which is complete overkill).
                // One possibility is to return a bool to signal whether the module has completely handled the command
                m_log.InfoFormat("[WORLD MAP]: Please change to a specific region in order to export its world map");
                return;
            }

            if (m_scene.ConsoleScene() != m_scene)
                return;

            string exportPath;

            if (cmdparams.Length > 1)
                exportPath = cmdparams[1];
            else
                exportPath = DEFAULT_WORLD_MAP_EXPORT_PATH;

            m_log.InfoFormat(
                "[WORLD MAP]: Exporting world map for {0} to {1}", m_regionName, exportPath);

            // assumed this is 1m less than next grid line
            int regionsView = (int)m_scene.MaxRegionViewDistance;

            int regionSizeX = (int)m_regionSizeX;
            int regionSizeY = (int)m_regionSizeY;

            int regionX = (int)m_regionGlobalX;
            int regionY = (int)m_regionGlobalY;

            int startX = regionX - regionsView;
            int startY = regionY - regionsView;

            int endX = regionX + regionSizeX + regionsView;
            int endY = regionY + regionSizeY + regionsView;

            int spanX = endX - startX + 2;
            int spanY = endY - startY + 2;

            Bitmap mapTexture = new Bitmap(spanX, spanY);
            ImageAttributes gatrib = new ImageAttributes();
            gatrib.SetWrapMode(System.Drawing.Drawing2D.WrapMode.TileFlipXY);

            Graphics g = Graphics.FromImage(mapTexture);           
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

            SolidBrush sea = new SolidBrush(Color.DarkBlue);
            g.FillRectangle(sea, 0, 0, spanX, spanY);
            sea.Dispose();

            Font drawFont = new Font("Arial", 32);
            SolidBrush drawBrush = new SolidBrush(Color.White);

            List<GridRegion> regions = m_scene.GridService.GetRegionRange(m_scene.RegionInfo.ScopeID,
                    startX, startY, endX, endY);

            startX--;
            startY--;

            bool doneLocal = false;
            string filename = "MAP-" + m_scene.RegionInfo.RegionID.ToString() + ".png";
            try
            {
                using(Image localMap = Bitmap.FromFile(filename))
                {
                    int x = regionX - startX;
                    int y = regionY - startY;
                    int sx = regionSizeX;
                    int sy = regionSizeY;
                    // y origin is top
                    g.DrawImage(localMap,new Rectangle(x, spanY - y - sy, sx, sy),
                                0, 0, localMap.Width, localMap.Height, GraphicsUnit.Pixel, gatrib);

                    if(m_exportPrintRegionName)
                    {
                        SizeF stringSize = g.MeasureString(m_regionName, drawFont);
                        g.DrawString(m_regionName, drawFont, drawBrush, x + 30, spanY - y - 30 - stringSize.Height);
                    }
                }
                doneLocal = true;
            }
            catch {}

            if(regions.Count > 0)
            {
                ManagedImage managedImage = null;
                Image image = null;

                foreach(GridRegion r in regions)
                {
                    if(r.TerrainImage.IsZero())
                        continue;

                    if(doneLocal && r.RegionHandle == m_regionHandle)
                        continue;

                    AssetBase texAsset = m_scene.AssetService.Get(r.TerrainImage.ToString());
                    if(texAsset == null)
                        continue;

                    if(OpenJPEG.DecodeToImage(texAsset.Data, out managedImage, out image))
                    {
                        int x = r.RegionLocX - startX;
                        int y = r.RegionLocY - startY;
                        int sx = r.RegionSizeX;
                        int sy = r.RegionSizeY;
                        // y origin is top
                        g.DrawImage(image,new Rectangle(x, spanY - y - sy, sx, sy),
                                0, 0, image.Width, image.Height, GraphicsUnit.Pixel, gatrib);

                        if(m_exportPrintRegionName && r.RegionHandle == m_regionHandle)
                        {
                            SizeF stringSize = g.MeasureString(r.RegionName, drawFont);
                            g.DrawString(r.RegionName, drawFont, drawBrush, x + 30, spanY - y - 30 - stringSize.Height);
                        }
                    }
                }

                if(image != null)
                    image.Dispose();

            }

            if(m_exportPrintScale)
            {
                String drawString = string.Format("{0}m x {1}m", spanX, spanY);
                g.DrawString(drawString, drawFont, drawBrush, 30, 30);
            }

            drawBrush.Dispose();
            drawFont.Dispose();
            gatrib.Dispose();
            g.Dispose();

            mapTexture.Save(exportPath, ImageFormat.Jpeg);
            mapTexture.Dispose();

            m_log.InfoFormat(
                "[WORLD MAP]: Successfully exported world map for {0} to {1}",
                m_regionName, exportPath);
        }

        public void HandleGenerateMapConsoleCommand(string module, string[] cmdparams)
        {
            if(m_scene == null)
                return;

            Scene consoleScene = m_scene.ConsoleScene();
            if (consoleScene != null && consoleScene != m_scene)
                return;

            m_scene.RegenerateMaptileAndReregister(this, null);
        }

        public void HandleRemoteMapItemRequest(IOSHttpRequest request, IOSHttpResponse response)
        {
            // Service 6 (MAP_ITEM_AGENTS_LOCATION; green dots)

            OSDMap responsemap = new OSDMap();
            int tc = Environment.TickCount;
            OSD osdhash = OSD.FromString(Util.Md5Hash(m_regionName + tc.ToString()));

            if (m_scene.GetRootAgentCount() == 0)
            {
                OSDMap responsemapdata = new OSDMap();
                responsemapdata["X"] = OSD.FromInteger((int)(m_regionGlobalX + 1));
                responsemapdata["Y"] = OSD.FromInteger((int)(m_regionGlobalY + 1));
                responsemapdata["ID"] = OSD.FromUUID(UUID.Zero);
                responsemapdata["Name"] = osdhash;
                responsemapdata["Extra"] = OSD.FromInteger(0);
                responsemapdata["Extra2"] = OSD.FromInteger(0);
                OSDArray responsearr = new OSDArray();
                responsearr.Add(responsemapdata);

                responsemap["6"] = responsearr;
            }
            else
            {
                OSDArray responsearr = new OSDArray(); // Don't preallocate. MT (m_scene.GetRootAgentCount());
                m_scene.ForEachRootScenePresence(delegate (ScenePresence sp)
                {
                    if (sp.IsNPC || sp.IsDeleted || sp.IsInTransit)
                        return;
                    OSDMap responsemapdata = new OSDMap();
                    responsemapdata["X"] = OSD.FromInteger((int)(m_regionGlobalX + sp.AbsolutePosition.X));
                    responsemapdata["Y"] = OSD.FromInteger((int)(m_regionGlobalY + sp.AbsolutePosition.Y));
                    responsemapdata["ID"] = OSD.FromUUID(UUID.Zero);
                    responsemapdata["Name"] = osdhash;
                    responsemapdata["Extra"] = OSD.FromInteger(1);
                    responsemapdata["Extra2"] = OSD.FromInteger(0);
                    responsearr.Add(responsemapdata);
                });
                responsemap["6"] = responsearr;
            }

            // Service 7/10 (MAP_ITEM_LAND_FOR_SALE/ADULT)

            ILandChannel landChannel = m_scene.LandChannel;
            List<ILandObject> parcels = landChannel.AllParcels();

            if ((parcels != null) && (parcels.Count >= 0))
            {
                OSDArray responsearr = new OSDArray(parcels.Count);
                foreach (ILandObject parcel_interface in parcels)
                {
                    // Play it safe
                    if (!(parcel_interface is LandObject))
                        continue;

                    LandObject land = (LandObject)parcel_interface;
                    LandData parcel = land.LandData;

                    // Show land for sale
                    if ((parcel.Flags & (uint)ParcelFlags.ForSale) == (uint)ParcelFlags.ForSale)
                    {
                        float x = m_regionGlobalX + land.CenterPoint.X;
                        float y = m_regionGlobalY + land.CenterPoint.Y;

                        OSDMap responsemapdata = new OSDMap();
                        responsemapdata["X"] = OSD.FromInteger((int)x);
                        responsemapdata["Y"] = OSD.FromInteger((int)y);
                        // responsemapdata["Z"] = OSD.FromInteger((int)m_scene.GetGroundHeight(x,y));
                        responsemapdata["ID"] = OSD.FromUUID(land.FakeID);
                        responsemapdata["Name"] = OSD.FromString(parcel.Name);
                        responsemapdata["Extra"] = OSD.FromInteger(parcel.Area);
                        responsemapdata["Extra2"] = OSD.FromInteger(parcel.SalePrice);
                        responsearr.Add(responsemapdata);
                    }
                }

                if(responsearr.Count > 0)
                {
                    if(m_scene.RegionInfo.RegionSettings.Maturity == 2)
                        responsemap["10"] = responsearr;
                    else
                        responsemap["7"] = responsearr;
                }
            }

            if (!m_scene.RegionInfo.RegionSettings.TelehubObject.IsZero())
            {
                SceneObjectGroup sog = m_scene.GetSceneObjectGroup(m_scene.RegionInfo.RegionSettings.TelehubObject);
                if (sog != null)
                {
                    OSDArray responsearr = new OSDArray();
                    OSDMap responsemapdata = new OSDMap();
                    responsemapdata["X"] = OSD.FromInteger((int)(m_regionGlobalX + sog.AbsolutePosition.X));
                    responsemapdata["Y"] = OSD.FromInteger((int)(m_regionGlobalY + sog.AbsolutePosition.Y));
                    // responsemapdata["Z"] = OSD.FromInteger((int)m_scene.GetGroundHeight(x,y));
                    responsemapdata["ID"] = OSD.FromUUID(sog.UUID);
                    responsemapdata["Name"] = OSD.FromString(sog.Name);
                    responsemapdata["Extra"] = OSD.FromInteger(0); // color (unused)
                    responsemapdata["Extra2"] = OSD.FromInteger(0); // 0 = telehub / 1 = infohub
                    responsearr.Add(responsemapdata);

                    responsemap["1"] = responsearr;
                }
            }

            response.RawBuffer = OSDParser.SerializeLLSDXmlBytes(responsemap);
            response.StatusCode = (int)HttpStatusCode.OK;
        }

        public void GenerateMaptile()
        {
            // Cannot create a map for a nonexistent heightmap
            if (m_scene.Heightmap == null)
                return;

            if (m_mapImageGenerator == null)
            {
                Console.WriteLine("No map image generator available for {0}", m_scene.Name);
                return;
            }
            m_log.DebugFormat("[WORLD MAP]: Generating map image for {0}", m_scene.Name);

            using (Bitmap mapbmp = m_mapImageGenerator.CreateMapTile())
            {
                GenerateMaptile(mapbmp);

                if (m_mapImageServiceModule != null)
                    m_mapImageServiceModule.UploadMapTile(m_scene, mapbmp);
            }
        }

        public void DeregisterMap()
        {
            //if (m_mapImageServiceModule != null)
            //    m_mapImageServiceModule.RemoveMapTiles(m_scene);
        }

        private void GenerateMaptile(Bitmap mapbmp)
        {
            bool needRegionSave = false;

            // remove old assets
            if (m_scene.RegionInfo.RegionSettings.TerrainImageID.IsNotZero())
            {
                m_scene.AssetService.Delete(m_scene.RegionInfo.RegionSettings.TerrainImageID.ToString());
                m_scene.RegionInfo.RegionSettings.TerrainImageID = UUID.Zero;
                myMapImageJPEG = [];
                needRegionSave = true;
            }

            if (m_scene.RegionInfo.RegionSettings.ParcelImageID.IsNotZero())
            {
                m_scene.AssetService.Delete(m_scene.RegionInfo.RegionSettings.ParcelImageID.ToString());
                m_scene.RegionInfo.RegionSettings.ParcelImageID = UUID.Zero;
                needRegionSave = true;
            }

            if(mapbmp != null)
            {
                try
                {
                    byte[] data;

                    // if large region limit its size since new viewers will not use it
                    // but it is still usable for ossl
                    if(m_scene.RegionInfo.RegionSizeX > Constants.RegionSize ||
                            m_scene.RegionInfo.RegionSizeY > Constants.RegionSize)
                    {
                        int bx = mapbmp.Width;
                        int by = mapbmp.Height;
                        int mb = bx;
                        if(mb < by)
                            mb = by;
                        if(mb > Constants.RegionSize && mb > 0)
                        {
                            float scale = (float)Constants.RegionSize/(float)mb;
                            using(Bitmap scaledbmp = Util.ResizeImageSolid(mapbmp, (int)(bx * scale), (int)(by * scale)))
                                data = OpenJPEG.EncodeFromImage(scaledbmp, true);
                        }
                        else
                            data = OpenJPEG.EncodeFromImage(mapbmp, true);
                    }
                    else
                        data = OpenJPEG.EncodeFromImage(mapbmp, true);

                    if (data != null && data.Length > 0)
                    {
                        UUID terrainImageID = UUID.Random();

                        AssetBase asset = new AssetBase(
                            terrainImageID,
                            "terrainImage_" + m_scene.RegionInfo.RegionID.ToString(),
                            (sbyte)AssetType.Texture,
                            m_scene.RegionInfo.RegionID.ToString());
                        asset.Data = data;
                        asset.Description = m_regionName;
                        asset.Local = m_localV1MapAssets;
                        asset.Temporary = false;
                        asset.Flags = AssetFlags.Maptile;

                        // Store the new one
                        m_log.DebugFormat("[WORLD MAP]: Storing map image {0} for {1}", asset.ID, m_regionName);

                        m_scene.AssetService.Store(asset);

                        m_scene.RegionInfo.RegionSettings.TerrainImageID = terrainImageID;
                        needRegionSave = true;
                    }
                }
                catch (Exception e)
                {
                    m_log.Error("[WORLD MAP]: Failed generating terrain map: " + e);
                }
            }

            // V2/3 still seem to need this, or we are missing something somewhere
            byte[] overlay = GenerateOverlay();
            if (overlay != null)
            {
                UUID parcelImageID = UUID.Random();

                AssetBase parcels = new AssetBase(
                    parcelImageID,
                    "parcelImage_" + m_scene.RegionInfo.RegionID.ToString(),
                    (sbyte)AssetType.Texture,
                    m_scene.RegionInfo.RegionID.ToString());
                parcels.Data = overlay;
                parcels.Description = m_regionName;
                parcels.Temporary = false;
                parcels.Local = m_localV1MapAssets;
                parcels.Flags = AssetFlags.Maptile;

                m_scene.AssetService.Store(parcels);

                m_scene.RegionInfo.RegionSettings.ParcelImageID = parcelImageID;
                needRegionSave = true;
            }

            if (needRegionSave)
                m_scene.RegionInfo.RegionSettings.Save();
        }

        private void MakeRootAgent(ScenePresence avatar)
        {
            lock (m_rootAgents)
            {
                if (!m_rootAgents.Contains(avatar.UUID))
                {
                    m_rootAgents.Add(avatar.UUID);
                }
            }
        }

        private void MakeChildAgent(ScenePresence avatar)
        {
            lock (m_rootAgents)
            {
                m_rootAgents.Remove(avatar.UUID);
            }

            lock (m_mapBlockRequestEvent)
            {
                if (m_mapBlockRequests.ContainsKey(avatar.UUID))
                    m_mapBlockRequests.Remove(avatar.UUID);
            }
        }

        public void OnRegionUp(GridRegion otherRegion)
        {
            ulong regionhandle = otherRegion.RegionHandle;
            string httpserver = otherRegion.ServerURI + "MAP/MapItems/" + regionhandle.ToString();

             m_blacklistedregions.Remove(regionhandle);
             m_blacklistedurls.Remove(httpserver);
        }

        private Byte[] GenerateOverlay()
        {
            const  int landTileSize = Constants.LandUnit;

            // These need to be ints for bitmap generation
            int regionSizeX = (int)m_scene.RegionInfo.RegionSizeX;
            int regionLandTilesX = regionSizeX / landTileSize;

            int regionSizeY = (int)m_scene.RegionInfo.RegionSizeY;
            int regionLandTilesY = regionSizeY / landTileSize;

            bool landForSale = false;
            ILandObject land;

            // scan terrain avoiding potencial merges of large bitmaps
            //TODO  create the sell bitmap at landchannel / landmaster ?
            // and auction also, still not suported

            bool[,] saleBitmap = new bool[regionLandTilesX, regionLandTilesY];
            for (int x = 0, xx = 0; x < regionLandTilesX; x++ ,xx += landTileSize)
            {
                for (int y = 0, yy = 0; y < regionLandTilesY; y++, yy += landTileSize)
                {
                    land = m_scene.LandChannel.GetLandObject(xx, yy);
                    if (land != null && (land.LandData.Flags & (uint)ParcelFlags.ForSale) != 0)
                    {
                        saleBitmap[x, y] = true;
                        landForSale = true;
                    }
                    else
                        saleBitmap[x, y] = false;
                }
            }

            if (!landForSale)
            {
                m_log.DebugFormat("[WORLD MAP]: Region {0} has no parcels for sale, not generating overlay", m_regionName);
                return null;
            }

            m_log.DebugFormat("[WORLD MAP]: Region {0} has parcels for sale, generating overlay", m_regionName);

            using (Bitmap overlay = new Bitmap(regionSizeX, regionSizeY))
            {
                Color background = Color.FromArgb(0, 0, 0, 0);

                using (Graphics g = Graphics.FromImage(overlay))
                {
                    using (SolidBrush transparent = new SolidBrush(background))
                        g.FillRectangle(transparent, 0, 0, regionSizeX, regionSizeY);

                    // make it a bit transparent
                    using (SolidBrush yellow = new SolidBrush(Color.FromArgb(192, 249, 223, 9)))
                    {
                        for (int x = 0; x < regionLandTilesX; x++)
                        {
                            for (int y = 0; y < regionLandTilesY; y++)
                            {
                                if (saleBitmap[x, y])
                                    g.FillRectangle(
                                        yellow,
                                        x * landTileSize,
                                        regionSizeX - landTileSize - (y * landTileSize),
                                        landTileSize,
                                        landTileSize);
                            }
                        }
                    }
                }

                try
                {
                    return OpenJPEG.EncodeFromImage(overlay, false);
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[WORLD MAP]: Error creating parcel overlay: " + e.ToString());
                }
            }

            return null;
        }
    }

    public class MapRequestState
    {
        public UUID agentID;
        public uint flags;
        public uint EstateID;
        public bool godlike;
        public uint itemtype;
        public ulong regionhandle;
    }

    public struct MapBlockRequestData
    {
        public IClientAPI client;
        public int minX;
        public int minY;
        public int maxX;
        public int maxY;
        public uint flags;
    }
}
