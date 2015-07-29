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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
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
    public class WorldMapModule : INonSharedRegionModule, IWorldMapModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
#pragma warning disable 414
        private static string LogHeader = "[WORLD MAP]";
#pragma warning restore 414

        private static readonly string DEFAULT_WORLD_MAP_EXPORT_PATH = "exportmap.jpg";
        private static readonly UUID STOP_UUID = UUID.Random();
        private static readonly string m_mapLayerPath = "0001/";

        private IMapImageGenerator m_mapImageGenerator;
        private IMapImageUploadModule m_mapImageServiceModule;

        private OpenSim.Framework.BlockingQueue<MapRequestState> requests = new OpenSim.Framework.BlockingQueue<MapRequestState>();

        protected Scene m_scene;
        private List<MapBlockData> cachedMapBlocks = new List<MapBlockData>();
        private int cachedTime = 0;
        private int blacklistTimeout = 10*60*1000; // 10 minutes
        private byte[] myMapImageJPEG;
        protected volatile bool m_Enabled = false;
        private Dictionary<UUID, MapRequestState> m_openRequests = new Dictionary<UUID, MapRequestState>();
        private Dictionary<string, int> m_blacklistedurls = new Dictionary<string, int>();
        private Dictionary<ulong, int> m_blacklistedregions = new Dictionary<ulong, int>();
        private Dictionary<ulong, string> m_cachedRegionMapItemsAddress = new Dictionary<ulong, string>();
        private List<UUID> m_rootAgents = new List<UUID>();
        private volatile bool threadrunning = false;

        private IServiceThrottleModule m_ServiceThrottle;

        //private int CacheRegionsDistance = 256;

        #region INonSharedRegionModule Members
        public virtual void Initialise (IConfigSource config)
        {
            string[] configSections = new string[] { "Map", "Startup" };

            if (Util.GetConfigVarFromSections<string>(
                config, "WorldMapModule", configSections, "WorldMap") == "WorldMap")
                m_Enabled = true;

            blacklistTimeout 
                = Util.GetConfigVarFromSections<int>(config, "BlacklistTimeout", configSections, 10 * 60) * 1000;
        }

        public virtual void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (scene)
            {
                m_scene = scene;

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

        public virtual void RemoveRegion (Scene scene)
        {
            if (!m_Enabled)
                return;

            lock (m_scene)
            {
                m_Enabled = false;
                RemoveHandlers();
                m_scene = null;
            }
        }

        public virtual void RegionLoaded (Scene scene)
        {
            if (!m_Enabled)
                return;

            m_ServiceThrottle = scene.RequestModuleInterface<IServiceThrottleModule>();

            m_mapImageGenerator = m_scene.RequestModuleInterface<IMapImageGenerator>();
            m_mapImageServiceModule = m_scene.RequestModuleInterface<IMapImageUploadModule>();
        }

        public virtual void Close()
        {
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
            myMapImageJPEG = new byte[0];

            string regionimage = "regionImage" + m_scene.RegionInfo.RegionID.ToString();
            regionimage = regionimage.Replace("-", "");
            m_log.Info("[WORLD MAP]: JPEG Map location: " + m_scene.RegionInfo.ServerURI + "index.php?method=" + regionimage);

            MainServer.Instance.AddHTTPHandler(regionimage,
                new GenericHTTPDOSProtector(OnHTTPGetMapImage, OnHTTPThrottled, new BasicDosProtectorOptions()
                    {
                        AllowXForwardedFor = false,
                        ForgetTimeSpan = TimeSpan.FromMinutes(2),
                        MaxRequestsInTimeframe = 4,
                        ReportingName = "MAPDOSPROTECTOR",
                        RequestTimeSpan = TimeSpan.FromSeconds(10),
                        ThrottledAction = BasicDOSProtector.ThrottleAction.DoThrottledMethod
                    }).Process);
            MainServer.Instance.AddLLSDHandler(
                "/MAP/MapItems/" + m_scene.RegionInfo.RegionHandle.ToString(), HandleRemoteMapItemRequest);

            m_scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            m_scene.EventManager.OnNewClient += OnNewClient;
            m_scene.EventManager.OnClientClosed += ClientLoggedOut;
            m_scene.EventManager.OnMakeChildAgent += MakeChildAgent;
            m_scene.EventManager.OnMakeRootAgent += MakeRootAgent;
            m_scene.EventManager.OnRegionUp += OnRegionUp;

//            StartThread(new object());
        }

        // this has to be called with a lock on m_scene
        protected virtual void RemoveHandlers()
        {
//            StopThread();

            m_scene.EventManager.OnRegionUp -= OnRegionUp;
            m_scene.EventManager.OnMakeRootAgent -= MakeRootAgent;
            m_scene.EventManager.OnMakeChildAgent -= MakeChildAgent;
            m_scene.EventManager.OnClientClosed -= ClientLoggedOut;
            m_scene.EventManager.OnNewClient -= OnNewClient;
            m_scene.EventManager.OnRegisterCaps -= OnRegisterCaps;

            string regionimage = "regionImage" + m_scene.RegionInfo.RegionID.ToString();
            regionimage = regionimage.Replace("-", "");
            MainServer.Instance.RemoveLLSDHandler("/MAP/MapItems/" + m_scene.RegionInfo.RegionHandle.ToString(),
                                                              HandleRemoteMapItemRequest);
            MainServer.Instance.RemoveHTTPHandler("", regionimage);
        }

        public void OnRegisterCaps(UUID agentID, Caps caps)
        {
            //m_log.DebugFormat("[WORLD MAP]: OnRegisterCaps: agentID {0} caps {1}", agentID, caps);
            string capsBase = "/CAPS/" + caps.CapsObjectPath;
            caps.RegisterHandler(
                "MapLayer",
                new RestStreamHandler(
                    "POST",
                    capsBase + m_mapLayerPath,
                    (request, path, param, httpRequest, httpResponse)
                        => MapLayerRequest(request, path, param, agentID, caps),
                    "MapLayer",
                    agentID.ToString()));
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
        public string MapLayerRequest(string request, string path, string param,
                                      UUID agentID, Caps caps)
        {
            //try
            //
            //m_log.DebugFormat("[MAPLAYER]: path: {0}, param: {1}, agent:{2}",
            //                  path, param, agentID.ToString());

            // There is a major hack going on in this method. The viewer doesn't request
            // map blocks (RequestMapBlocks) above 2048. That means that if we don't hack,
            // grids above that cell don't have a map at all. So, here's the hack: we wait
            // for this CAP request to come, and we inject the map blocks at this point.
            // In a normal scenario, this request simply sends back the MapLayer (the blue color).
            // In the hacked scenario, it also sends the map blocks via UDP.
            //
            // 6/8/2011 -- I'm adding an explicit 2048 check, so that we never forget that there is
            // a hack here, and so that regions below 4096 don't get spammed with unnecessary map blocks.

            if (m_scene.RegionInfo.RegionLocX >= 2048 || m_scene.RegionInfo.RegionLocY >= 2048)
            {
                ScenePresence avatarPresence = null;

                m_scene.TryGetScenePresence(agentID, out avatarPresence);

                if (avatarPresence != null)
                {
                    bool lookup = false;

                    lock (cachedMapBlocks)
                    {
                        if (cachedMapBlocks.Count > 0 && ((cachedTime + 1800) > Util.UnixTimeSinceEpoch()))
                        {
                            List<MapBlockData> mapBlocks;

                            mapBlocks = cachedMapBlocks;
                            avatarPresence.ControllingClient.SendMapBlock(mapBlocks, 0);
                        }
                        else
                        {
                            lookup = true;
                        }
                    }
                    if (lookup)
                    {
                        List<MapBlockData> mapBlocks = new List<MapBlockData>(); ;

                        // Get regions that are within 8 regions of here
                        List<GridRegion> regions = m_scene.GridService.GetRegionRange(m_scene.RegionInfo.ScopeID,
                                    (int)Util.RegionToWorldLoc(m_scene.RegionInfo.RegionLocX - 8),
                                    (int)Util.RegionToWorldLoc(m_scene.RegionInfo.RegionLocX + 8),
                                    (int)Util.RegionToWorldLoc(m_scene.RegionInfo.RegionLocY - 8),
                                    (int)Util.RegionToWorldLoc(m_scene.RegionInfo.RegionLocY + 8) );
                        foreach (GridRegion r in regions)
                        {
                            MapBlockData block = MapBlockFromGridRegion(r, 0);
                            mapBlocks.Add(block);
                        }
                        avatarPresence.ControllingClient.SendMapBlock(mapBlocks, 0);

                        lock (cachedMapBlocks)
                            cachedMapBlocks = mapBlocks;

                        cachedTime = Util.UnixTimeSinceEpoch();
                    }
                }
            }

            LLSDMapLayerResponse mapResponse = new LLSDMapLayerResponse();
            mapResponse.LayerData.Array.Add(GetOSDMapLayerResponse());
            return mapResponse.ToString();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="mapReq"></param>
        /// <returns></returns>
        public LLSDMapLayerResponse GetMapLayer(LLSDMapRequest mapReq)
        {
            // m_log.DebugFormat("[WORLD MAP]: MapLayer Request in region: {0}", m_scene.RegionInfo.RegionName);
            LLSDMapLayerResponse mapResponse = new LLSDMapLayerResponse();
            mapResponse.LayerData.Array.Add(GetOSDMapLayerResponse());
            return mapResponse;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        protected static OSDMapLayer GetOSDMapLayerResponse()
        {
            OSDMapLayer mapLayer = new OSDMapLayer();
            mapLayer.Right = 5000;
            mapLayer.Top = 5000;
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
        }
        #endregion

        /// <summary>
        /// Starts the MapItemRequest Thread
        /// Note that this only gets started when there are actually agents in the region
        /// Additionally, it gets stopped when there are none.
        /// </summary>
        /// <param name="o"></param>
        private void StartThread(object o)
        {
            if (threadrunning) return;
            threadrunning = true;

//            m_log.Debug("[WORLD MAP]: Starting remote MapItem request thread");

            WorkManager.StartThread(
                process,
                string.Format("MapItemRequestThread ({0})", m_scene.RegionInfo.RegionName),
                ThreadPriority.BelowNormal,
                true,
                true);
        }

        /// <summary>
        /// Enqueues a 'stop thread' MapRequestState.  Causes the MapItemRequest thread to end
        /// </summary>
        private void StopThread()
        {
            MapRequestState st = new MapRequestState();
            st.agentID = STOP_UUID;
            st.EstateID=0;
            st.flags=0;
            st.godlike=false;
            st.itemtype=0;
            st.regionhandle=0;

            requests.Enqueue(st);
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
            uint xstart = 0;
            uint ystart = 0;
            Util.RegionHandleToWorldLoc(m_scene.RegionInfo.RegionHandle, out xstart, out ystart);
            if (itemtype == (int)GridItemType.AgentLocations)
            {
                if (regionhandle == 0 || regionhandle == m_scene.RegionInfo.RegionHandle)
                {
                    // Just requesting map info about the current, local region
                    int tc = Environment.TickCount;
                    List<mapItemReply> mapitems = new List<mapItemReply>();
                    mapItemReply mapitem = new mapItemReply();
                    if (m_scene.GetRootAgentCount() <= 1)
                    {
                        mapitem = new mapItemReply(
                                        xstart + 1,
                                        ystart + 1,
                                        UUID.Zero,
                                        Util.Md5Hash(m_scene.RegionInfo.RegionName + tc.ToString()),
                                        0, 0);
                        mapitems.Add(mapitem);
                    }
                    else
                    {
                        m_scene.ForEachRootScenePresence(delegate(ScenePresence sp)
                        {
                            // Don't send a green dot for yourself
                            if (sp.UUID != remoteClient.AgentId)
                            {
                                mapitem = new mapItemReply(
                                        xstart + (uint)sp.AbsolutePosition.X,
                                        ystart + (uint)sp.AbsolutePosition.Y,
                                        UUID.Zero,
                                        Util.Md5Hash(m_scene.RegionInfo.RegionName + tc.ToString()),
                                        1, 0);
                                mapitems.Add(mapitem);
                            }
                        });
                    }
                    remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                }
                else
                {
                    // Remote Map Item Request

                    // ensures that the blockingqueue doesn't get borked if the GetAgents() timing changes.
                    RequestMapItems("",remoteClient.AgentId,flags,EstateID,godlike,itemtype,regionhandle);
                }
            }
            else if (itemtype == (int)GridItemType.LandForSale) // Service 7 (MAP_ITEM_LAND_FOR_SALE)
            {
                if (regionhandle == 0 || regionhandle == m_scene.RegionInfo.RegionHandle)
                {
                    // Parcels
                    ILandChannel landChannel = m_scene.LandChannel;
                    List<ILandObject> parcels = landChannel.AllParcels();

                    // Local Map Item Request
                    List<mapItemReply> mapitems = new List<mapItemReply>();
                    mapItemReply mapitem = new mapItemReply();
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
                                Vector3 min = parcel.AABBMin;
                                Vector3 max = parcel.AABBMax;
                                float x = (min.X+max.X)/2;
                                float y = (min.Y+max.Y)/2;

                                mapitem = new mapItemReply(
                                                xstart + (uint)x,
                                                ystart + (uint)y,
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
                }
                else
                {
                    // Remote Map Item Request

                    // ensures that the blockingqueue doesn't get borked if the GetAgents() timing changes.
                    RequestMapItems("",remoteClient.AgentId,flags,EstateID,godlike,itemtype,regionhandle);
                }
            }
            else if (itemtype == (int)GridItemType.Telehub) // Service 1 (MAP_ITEM_TELEHUB)
            {
                if (regionhandle == 0 || regionhandle == m_scene.RegionInfo.RegionHandle)
                {
                    List<mapItemReply> mapitems = new List<mapItemReply>();
                    mapItemReply mapitem = new mapItemReply();

                    SceneObjectGroup sog = m_scene.GetSceneObjectGroup(m_scene.RegionInfo.RegionSettings.TelehubObject);
                    if (sog != null)
                    {
                        mapitem = new mapItemReply(
                                            xstart + (uint)sog.AbsolutePosition.X,
                                            ystart + (uint)sog.AbsolutePosition.Y,
                                            UUID.Zero,
                                            sog.Name,
                                            0,  // color (not used)
                                            0   // 0 = telehub / 1 = infohub
                        );
                        mapitems.Add(mapitem);

                        remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                    }
                }
                else
                {
                    // Remote Map Item Request
                    RequestMapItems("",remoteClient.AgentId,flags,EstateID,godlike,itemtype,regionhandle);
                }
            }
        }

        private int nAsyncRequests = 0;
        /// <summary>
        /// Processing thread main() loop for doing remote mapitem requests
        /// </summary>
        public void process()
        {
            //const int MAX_ASYNC_REQUESTS = 20;
            try
            {
                while (true)
                {
                    MapRequestState st = requests.Dequeue(1000);

                    // end gracefully
                    if (st.agentID == STOP_UUID)
                        break;

                    if (st.agentID != UUID.Zero)
                    {
                        bool dorequest = true;
                        lock (m_rootAgents)
                        {
                            if (!m_rootAgents.Contains(st.agentID))
                                dorequest = false;
                        }

                        if (dorequest &&  !m_blacklistedregions.ContainsKey(st.regionhandle))
                        {
                            while (nAsyncRequests >= MAX_ASYNC_REQUESTS) // hit the break
                                Thread.Sleep(80);

                            RequestMapItemsDelegate d = RequestMapItemsAsync;
                            d.BeginInvoke(st.agentID, st.flags, st.EstateID, st.godlike, st.itemtype, st.regionhandle, RequestMapItemsCompleted, null);
                            //OSDMap response = RequestMapItemsAsync(st.agentID, st.flags, st.EstateID, st.godlike, st.itemtype, st.regionhandle);
                            //RequestMapItemsCompleted(response);
                            Interlocked.Increment(ref nAsyncRequests);
                        }
                    }

                    Watchdog.UpdateThread();
                }
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[WORLD MAP]: Map item request thread terminated abnormally with exception {0}", e);
            }

            threadrunning = false;
            Watchdog.RemoveThread();
        }

        const int MAX_ASYNC_REQUESTS = 20;

        /// <summary>
        /// Enqueues the map item request into the services throttle processing thread
        /// </summary>
        /// <param name="state"></param>
        public void EnqueueMapItemRequest(MapRequestState st)
        {

            m_ServiceThrottle.Enqueue("map-item", st.regionhandle.ToString() + st.agentID.ToString(), delegate
            {
                if (st.agentID != UUID.Zero)
                {
                    bool dorequest = true;
                    lock (m_rootAgents)
                    {
                        if (!m_rootAgents.Contains(st.agentID))
                            dorequest = false;
                    }

                    if (dorequest && !m_blacklistedregions.ContainsKey(st.regionhandle))
                    {
                        if (nAsyncRequests >= MAX_ASYNC_REQUESTS) // hit the break
                        {
                            // AH!!! Recursive !
                            // Put this request back in the queue and return
                            EnqueueMapItemRequest(st);
                            return;
                        }

                        RequestMapItemsDelegate d = RequestMapItemsAsync;
                        d.BeginInvoke(st.agentID, st.flags, st.EstateID, st.godlike, st.itemtype, st.regionhandle, RequestMapItemsCompleted, null);
                        //OSDMap response = RequestMapItemsAsync(st.agentID, st.flags, st.EstateID, st.godlike, st.itemtype, st.regionhandle);
                        //RequestMapItemsCompleted(response);
                        Interlocked.Increment(ref nAsyncRequests);
                    }
                }
            });
        }

        /// <summary>
        /// Sends the mapitem response to the IClientAPI
        /// </summary>
        /// <param name="response">The OSDMap Response for the mapitem</param>
        private void RequestMapItemsCompleted(IAsyncResult iar)
        {
            AsyncResult result = (AsyncResult)iar;
            RequestMapItemsDelegate icon = (RequestMapItemsDelegate)result.AsyncDelegate;

            OSDMap response = (OSDMap)icon.EndInvoke(iar);

            Interlocked.Decrement(ref nAsyncRequests);

            if (!response.ContainsKey("requestID"))
                return;

            UUID requestID = response["requestID"].AsUUID();

            if (requestID != UUID.Zero)
            {
                MapRequestState mrs = new MapRequestState();
                mrs.agentID = UUID.Zero;
                lock (m_openRequests)
                {
                    if (m_openRequests.ContainsKey(requestID))
                    {
                        mrs = m_openRequests[requestID];
                        m_openRequests.Remove(requestID);
                    }
                }

                if (mrs.agentID != UUID.Zero)
                {
                    ScenePresence av = null;
                    m_scene.TryGetScenePresence(mrs.agentID, out av);
                    if (av != null)
                    {
                        if (response.ContainsKey(mrs.itemtype.ToString()))
                        {
                            List<mapItemReply> returnitems = new List<mapItemReply>();
                            OSDArray itemarray = (OSDArray)response[mrs.itemtype.ToString()];
                            for (int i = 0; i < itemarray.Count; i++)
                            {
                                OSDMap mapitem = (OSDMap)itemarray[i];
                                mapItemReply mi = new mapItemReply();
                                mi.FromOSD(mapitem);
                                returnitems.Add(mi);
                            }
                            av.ControllingClient.SendMapItemReply(returnitems.ToArray(), mrs.itemtype, mrs.flags);
                        }

                        // Service 7 (MAP_ITEM_LAND_FOR_SALE)
                        uint itemtype = (uint)GridItemType.LandForSale;

                        if (response.ContainsKey(itemtype.ToString()))
                        {
                            List<mapItemReply> returnitems = new List<mapItemReply>();
                            OSDArray itemarray = (OSDArray)response[itemtype.ToString()];
                            for (int i = 0; i < itemarray.Count; i++)
                            {
                                OSDMap mapitem = (OSDMap)itemarray[i];
                                mapItemReply mi = new mapItemReply();
                                mi.FromOSD(mapitem);
                                returnitems.Add(mi);
                            }
                            av.ControllingClient.SendMapItemReply(returnitems.ToArray(), itemtype, mrs.flags);
                        }

                        // Service 1 (MAP_ITEM_TELEHUB)
                        itemtype = (uint)GridItemType.Telehub;

                        if (response.ContainsKey(itemtype.ToString()))
                        {
                            List<mapItemReply> returnitems = new List<mapItemReply>();
                            OSDArray itemarray = (OSDArray)response[itemtype.ToString()];
                            for (int i = 0; i < itemarray.Count; i++)
                            {
                                OSDMap mapitem = (OSDMap)itemarray[i];
                                mapItemReply mi = new mapItemReply();
                                mi.FromOSD(mapitem);
                                returnitems.Add(mi);
                            }
                            av.ControllingClient.SendMapItemReply(returnitems.ToArray(), itemtype, mrs.flags);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Enqueue the MapItem request for remote processing
        /// </summary>
        /// <param name="httpserver">blank string, we discover this in the process</param>
        /// <param name="id">Agent ID that we are making this request on behalf</param>
        /// <param name="flags">passed in from packet</param>
        /// <param name="EstateID">passed in from packet</param>
        /// <param name="godlike">passed in from packet</param>
        /// <param name="itemtype">passed in from packet</param>
        /// <param name="regionhandle">Region we're looking up</param>
        public void RequestMapItems(string httpserver, UUID id, uint flags,
            uint EstateID, bool godlike, uint itemtype, ulong regionhandle)
        {
            MapRequestState st = new MapRequestState();
            st.agentID = id;
            st.flags = flags;
            st.EstateID = EstateID;
            st.godlike = godlike;
            st.itemtype = itemtype;
            st.regionhandle = regionhandle;
            EnqueueMapItemRequest(st);
        }

        private delegate OSDMap RequestMapItemsDelegate(UUID id, uint flags,
            uint EstateID, bool godlike, uint itemtype, ulong regionhandle);
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
        private OSDMap RequestMapItemsAsync(UUID id, uint flags,
            uint EstateID, bool godlike, uint itemtype, ulong regionhandle)
        {
//            m_log.DebugFormat("[WORLDMAP]: RequestMapItemsAsync; region handle: {0} {1}", regionhandle, itemtype);

            string httpserver = "";
            bool blacklisted = false;
            lock (m_blacklistedregions)
            {
                if (m_blacklistedregions.ContainsKey(regionhandle))
                {
                    if (Environment.TickCount > (m_blacklistedregions[regionhandle] + blacklistTimeout))
                    {
                        m_log.DebugFormat("[WORLD MAP]: Unblock blacklisted region {0}", regionhandle);

                        m_blacklistedregions.Remove(regionhandle);
                    }
                    else
                        blacklisted = true;
                }
            }

            if (blacklisted)
                return new OSDMap();

            UUID requestID = UUID.Random();
            lock (m_cachedRegionMapItemsAddress)
            {
                if (m_cachedRegionMapItemsAddress.ContainsKey(regionhandle))
                    httpserver = m_cachedRegionMapItemsAddress[regionhandle];
            }
            if (httpserver.Length == 0)
            {
                uint x = 0, y = 0;
                Util.RegionHandleToWorldLoc(regionhandle, out x, out y);
                GridRegion mreg = m_scene.GridService.GetRegionByPosition(m_scene.RegionInfo.ScopeID, (int)x, (int)y);

                if (mreg != null)
                {
                    httpserver = mreg.ServerURI + "MAP/MapItems/" + regionhandle.ToString();
                    lock (m_cachedRegionMapItemsAddress)
                    {
                        if (!m_cachedRegionMapItemsAddress.ContainsKey(regionhandle))
                            m_cachedRegionMapItemsAddress.Add(regionhandle, httpserver);
                    }
                }
                else
                {
                    lock (m_blacklistedregions)
                    {
                        if (!m_blacklistedregions.ContainsKey(regionhandle))
                            m_blacklistedregions.Add(regionhandle, Environment.TickCount);
                    }
                    //m_log.InfoFormat("[WORLD MAP]: Blacklisted region {0}", regionhandle.ToString());
                }
            }

            blacklisted = false;
            lock (m_blacklistedurls)
            {
                if (m_blacklistedurls.ContainsKey(httpserver))
                {
                    if (Environment.TickCount > (m_blacklistedurls[httpserver] + blacklistTimeout))
                    {
                        m_log.DebugFormat("[WORLD MAP]: Unblock blacklisted URL {0}", httpserver);

                        m_blacklistedurls.Remove(httpserver);
                    }
                    else
                        blacklisted = true;
                }
            }

            // Can't find the http server
            if (httpserver.Length == 0 || blacklisted)
                return new OSDMap();

            MapRequestState mrs = new MapRequestState();
            mrs.agentID = id;
            mrs.EstateID = EstateID;
            mrs.flags = flags;
            mrs.godlike = godlike;
            mrs.itemtype=itemtype;
            mrs.regionhandle = regionhandle;

            lock (m_openRequests)
                m_openRequests.Add(requestID, mrs);

            WebRequest mapitemsrequest = null;
            try
            {
                mapitemsrequest = WebRequest.Create(httpserver);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[WORLD MAP]: Access to {0} failed with {1}", httpserver, e);
                return new OSDMap();
            }

            mapitemsrequest.Method = "POST";
            mapitemsrequest.ContentType = "application/xml+llsd";
            OSDMap RAMap = new OSDMap();

            // string RAMapString = RAMap.ToString();
            OSD LLSDofRAMap = RAMap; // RENAME if this works

            byte[] buffer = OSDParser.SerializeLLSDXmlBytes(LLSDofRAMap);
            OSDMap responseMap = new OSDMap();
            responseMap["requestID"] = OSD.FromUUID(requestID);

            Stream os = null;
            try
            { // send the Post
                mapitemsrequest.ContentLength = buffer.Length;   //Count bytes to send
                os = mapitemsrequest.GetRequestStream();
                os.Write(buffer, 0, buffer.Length);         //Send it
                //m_log.DebugFormat("[WORLD MAP]: Getting MapItems from {0}", httpserver);
            }
            catch (WebException ex)
            {
                m_log.WarnFormat("[WORLD MAP]: Bad send on GetMapItems {0}", ex.Message);
                responseMap["connect"] = OSD.FromBoolean(false);
                lock (m_blacklistedurls)
                {
                    if (!m_blacklistedurls.ContainsKey(httpserver))
                        m_blacklistedurls.Add(httpserver, Environment.TickCount);
                }

                m_log.WarnFormat("[WORLD MAP]: Blacklisted {0}", httpserver);

                return responseMap;
            }
            catch
            {
                m_log.DebugFormat("[WORLD MAP]: RequestMapItems failed for {0}", httpserver);
                responseMap["connect"] = OSD.FromBoolean(false);
                return responseMap;
            }
            finally
            {
                if (os != null)
                    os.Dispose();
            }

            string response_mapItems_reply = null;
            {
                try
                {
                    using (WebResponse webResponse = mapitemsrequest.GetResponse())
                    {
                        if (webResponse != null)
                        {
                            using (Stream s = webResponse.GetResponseStream())
                                using (StreamReader sr = new StreamReader(s))
                                    response_mapItems_reply = sr.ReadToEnd().Trim();
                        }
                        else
                        {
                            return new OSDMap();
                        }
                        }
                }
                catch (WebException)
                {
                    responseMap["connect"] = OSD.FromBoolean(false);
                    lock (m_blacklistedurls)
                    {
                        if (!m_blacklistedurls.ContainsKey(httpserver))
                            m_blacklistedurls.Add(httpserver, Environment.TickCount);
                    }

                    m_log.WarnFormat("[WORLD MAP]: Blacklisted {0}", httpserver);

                    return responseMap;
                }
                catch
                {
                    m_log.DebugFormat("[WORLD MAP]: RequestMapItems failed for {0}", httpserver);
                    responseMap["connect"] = OSD.FromBoolean(false);
                    lock (m_blacklistedregions)
                    {
                        if (!m_blacklistedregions.ContainsKey(regionhandle))
                            m_blacklistedregions.Add(regionhandle, Environment.TickCount);
                    }

                    return responseMap;
                }

                OSD rezResponse = null;
                try
                {
                    rezResponse = OSDParser.DeserializeLLSDXml(response_mapItems_reply);

                    responseMap = (OSDMap)rezResponse;
                    responseMap["requestID"] = OSD.FromUUID(requestID);
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[WORLD MAP]: exception on parse of RequestMapItems reply from {0}: {1}", httpserver, ex.Message);
                    responseMap["connect"] = OSD.FromBoolean(false);

                    lock (m_blacklistedregions)
                    {
                        if (!m_blacklistedregions.ContainsKey(regionhandle))
                            m_blacklistedregions.Add(regionhandle, Environment.TickCount);
                    }

                    return responseMap;
                }
            }

            if (!responseMap.ContainsKey(itemtype.ToString())) // remote sim doesnt have the stated region handle
            {
                m_log.DebugFormat("[WORLD MAP]: Remote sim does not have the stated region. Blacklisting.");
                lock (m_blacklistedregions)
                {
                    if (!m_blacklistedregions.ContainsKey(regionhandle))
                        m_blacklistedregions.Add(regionhandle, Environment.TickCount);
                }
            }

            return responseMap;
        }

        /// <summary>
        /// Requests map blocks in area of minX, maxX, minY, MaxY in world cordinates
        /// </summary>
        /// <param name="minX"></param>
        /// <param name="minY"></param>
        /// <param name="maxX"></param>
        /// <param name="maxY"></param>
        public virtual void RequestMapBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag)
        {
            if ((flag & 0x10000) != 0)  // user clicked on qthe map a tile that isn't visible
            {
                List<MapBlockData> response = new List<MapBlockData>();

                // this should return one mapblock at most. It is triggered by a click
                // on an unloaded square.
                // But make sure: Look whether the one we requested is in there
                List<GridRegion> regions = m_scene.GridService.GetRegionRange(m_scene.RegionInfo.ScopeID,
                                    (int)Util.RegionToWorldLoc((uint)minX), (int)Util.RegionToWorldLoc((uint)maxX),
                                    (int)Util.RegionToWorldLoc((uint)minY), (int)Util.RegionToWorldLoc((uint)maxY) );

                m_log.DebugFormat("[WORLD MAP MODULE] RequestMapBlocks min=<{0},{1}>, max=<{2},{3}>, flag={4}, cntFound={5}",
                                            minX, minY, maxX, maxY, flag.ToString("X"), regions.Count);
                if (regions != null)
                {
                    foreach (GridRegion r in regions)
                    {
                        if (r.RegionLocX == Util.RegionToWorldLoc((uint)minX)
                            && r.RegionLocY == Util.RegionToWorldLoc((uint)minY) )
                        {
                            // found it => add it to response
                            // Version 2 viewers can handle the larger regions
                            if ((flag & 2) == 2)
                                response.AddRange(Map2BlockFromGridRegion(r, flag));
                            else
                                response.Add(MapBlockFromGridRegion(r, flag));
                            break;
                        }
                    }
                }

                if (response.Count == 0)
                {
                    // response still empty => couldn't find the map-tile the user clicked on => tell the client
                    MapBlockData block = new MapBlockData();
                    block.X = (ushort)minX;
                    block.Y = (ushort)minY;
                    block.Access = (byte)SimAccess.Down; // means 'simulator is offline'
                    // block.Access = (byte)SimAccess.NonExistent;
                    response.Add(block);
                }
                // The lower 16 bits are an unsigned int16
                remoteClient.SendMapBlock(response, flag & 0xffff);
            }
            else
            {
                // normal mapblock request. Use the provided values
                GetAndSendBlocks(remoteClient, minX, minY, maxX, maxY, flag);
            }
        }

        protected virtual List<MapBlockData> GetAndSendBlocks(IClientAPI remoteClient, int minX, int minY, int maxX, int maxY, uint flag)
        {
            List<MapBlockData> mapBlocks = new List<MapBlockData>();
            List<GridRegion> regions = m_scene.GridService.GetRegionRange(m_scene.RegionInfo.ScopeID,
                        (int)Util.RegionToWorldLoc((uint)(minX - 4)), (int)Util.RegionToWorldLoc((uint)(maxX + 4)),
                        (int)Util.RegionToWorldLoc((uint)(minY - 4)), (int)Util.RegionToWorldLoc((uint)(maxY + 4)) );
            //m_log.DebugFormat("{0} GetAndSendBlocks. min=<{1},{2}>, max=<{3},{4}>, cntFound={5}",
            //                            LogHeader, minX, minY, maxX, maxY, regions.Count);
            foreach (GridRegion r in regions)
            {
                // Version 2 viewers can handle the larger regions
                if ((flag & 2) == 2)
                    mapBlocks.AddRange(Map2BlockFromGridRegion(r, flag));
                else
                    mapBlocks.Add(MapBlockFromGridRegion(r, flag));
            }
            remoteClient.SendMapBlock(mapBlocks, flag & 0xffff);

            return mapBlocks;
        }

        // Fill a passed MapBlockData from a GridRegion
        public MapBlockData MapBlockFromGridRegion(GridRegion r, uint flag)
        {
            MapBlockData block = new MapBlockData();

            block.Access = r.Access;
            switch (flag & 0xffff)
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
            block.X = (ushort)Util.WorldToRegionLoc((uint)r.RegionLocX);
            block.Y = (ushort)Util.WorldToRegionLoc((uint)r.RegionLocY);
            block.SizeX = (ushort) r.RegionSizeX;
            block.SizeY = (ushort) r.RegionSizeY;

            return block;
        }

        public List<MapBlockData> Map2BlockFromGridRegion(GridRegion r, uint flag)
        {
            List<MapBlockData> blocks = new List<MapBlockData>();
            MapBlockData block = new MapBlockData();
            if (r == null)
            {
                block.Access = (byte)SimAccess.Down;
                block.MapImageId = UUID.Zero;
                blocks.Add(block);
            }
            else
            {
                block.Access = r.Access;
                switch (flag & 0xffff)
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
                blocks.Add(block);
            }
            return blocks;
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

        public Hashtable OnHTTPGetMapImage(Hashtable keysvals)
        {
            m_log.Debug("[WORLD MAP]: Sending map image jpeg");
            Hashtable reply = new Hashtable();
            int statuscode = 200;
            byte[] jpeg = new byte[0];

            if (myMapImageJPEG.Length == 0)
            {
                MemoryStream imgstream = null;
                Bitmap mapTexture = new Bitmap(1,1);
                ManagedImage managedImage;
                Image image = (Image)mapTexture;

                try
                {
                    // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data

                    imgstream = new MemoryStream();

                    // non-async because we know we have the asset immediately.
                    AssetBase mapasset = m_scene.AssetService.Get(m_scene.RegionInfo.RegionSettings.TerrainImageID.ToString());

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
                catch (Exception)
                {
                    // Dummy!
                    m_log.Warn("[WORLD MAP]: Unable to generate Map image");
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

            reply["str_response_string"] = Convert.ToBase64String(jpeg);
            reply["int_response_code"] = statuscode;
            reply["content_type"] = "image/jpeg";

            return reply;
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
                "[WORLD MAP]: Exporting world map for {0} to {1}", m_scene.RegionInfo.RegionName, exportPath);

            List<MapBlockData> mapBlocks = new List<MapBlockData>();
            List<GridRegion> regions = m_scene.GridService.GetRegionRange(m_scene.RegionInfo.ScopeID,
                    (int)Util.RegionToWorldLoc(m_scene.RegionInfo.RegionLocX - 9),
                    (int)Util.RegionToWorldLoc(m_scene.RegionInfo.RegionLocX + 9),
                    (int)Util.RegionToWorldLoc(m_scene.RegionInfo.RegionLocY - 9),
                    (int)Util.RegionToWorldLoc(m_scene.RegionInfo.RegionLocY + 9));
            List<AssetBase> textures = new List<AssetBase>();
            List<Image> bitImages = new List<Image>();

            foreach (GridRegion r in regions)
            {
                MapBlockData mapBlock = MapBlockFromGridRegion(r, 0);
                AssetBase texAsset = m_scene.AssetService.Get(mapBlock.MapImageId.ToString());

                if (texAsset != null)
                {
                    textures.Add(texAsset);
                }
                //else
                //{
                //    // WHAT?!? This doesn't seem right. Commenting (diva)
                //    texAsset = m_scene.AssetService.Get(mapBlock.MapImageId.ToString());
                //    if (texAsset != null)
                //    {
                //        textures.Add(texAsset);
                //    }
                //}
            }

            foreach (AssetBase asset in textures)
            {
                ManagedImage managedImage;
                Image image;

                if (OpenJPEG.DecodeToImage(asset.Data, out managedImage, out image))
                    bitImages.Add(image);
            }

            Bitmap mapTexture = new Bitmap(2560, 2560);
            Graphics g = Graphics.FromImage(mapTexture);
            SolidBrush sea = new SolidBrush(Color.DarkBlue);
            g.FillRectangle(sea, 0, 0, 2560, 2560);

            for (int i = 0; i < mapBlocks.Count; i++)
            {
                ushort x = (ushort)((mapBlocks[i].X - m_scene.RegionInfo.RegionLocX) + 10);
                ushort y = (ushort)((mapBlocks[i].Y - m_scene.RegionInfo.RegionLocY) + 10);
                g.DrawImage(bitImages[i], (x * 128), 2560 - (y * 128), 128, 128); // y origin is top
            }

            mapTexture.Save(exportPath, ImageFormat.Jpeg);

            m_log.InfoFormat(
                "[WORLD MAP]: Successfully exported world map for {0} to {1}",
                m_scene.RegionInfo.RegionName, exportPath);
        }

        public void HandleGenerateMapConsoleCommand(string module, string[] cmdparams)
        {
            Scene consoleScene = m_scene.ConsoleScene();

            if (consoleScene != null && consoleScene != m_scene)
                return;

            if (m_mapImageGenerator == null)
            {
                Console.WriteLine("No map image generator available for {0}", m_scene.Name);
                return;
            }

            using (Bitmap mapbmp = m_mapImageGenerator.CreateMapTile())
            {
                GenerateMaptile(mapbmp);
                m_mapImageServiceModule.UploadMapTile(m_scene, mapbmp);
            }
        }

        public OSD HandleRemoteMapItemRequest(string path, OSD request, string endpoint)
        {
            uint xstart = 0;
            uint ystart = 0;

            Util.RegionHandleToWorldLoc(m_scene.RegionInfo.RegionHandle,out xstart,out ystart);
            // m_log.DebugFormat("{0} HandleRemoteMapItemRequest. loc=<{1},{2}>",
            //             LogHeader, Util.WorldToRegionLoc(xstart), Util.WorldToRegionLoc(ystart));

            // Service 6 (MAP_ITEM_AGENTS_LOCATION; green dots)

            OSDMap responsemap = new OSDMap();
            int tc = Environment.TickCount;
            if (m_scene.GetRootAgentCount() == 0)
            {
                OSDMap responsemapdata = new OSDMap();
                responsemapdata["X"] = OSD.FromInteger((int)(xstart + 1));
                responsemapdata["Y"] = OSD.FromInteger((int)(ystart + 1));
                responsemapdata["ID"] = OSD.FromUUID(UUID.Zero);
                responsemapdata["Name"] = OSD.FromString(Util.Md5Hash(m_scene.RegionInfo.RegionName + tc.ToString()));
                responsemapdata["Extra"] = OSD.FromInteger(0);
                responsemapdata["Extra2"] = OSD.FromInteger(0);
                OSDArray responsearr = new OSDArray();
                responsearr.Add(responsemapdata);

                responsemap["6"] = responsearr;
            }
            else
            {
                OSDArray responsearr = new OSDArray(m_scene.GetRootAgentCount());
                m_scene.ForEachRootScenePresence(delegate(ScenePresence sp)
                {
                    OSDMap responsemapdata = new OSDMap();
                    responsemapdata["X"] = OSD.FromInteger((int)(xstart + sp.AbsolutePosition.X));
                    responsemapdata["Y"] = OSD.FromInteger((int)(ystart + sp.AbsolutePosition.Y));
                    responsemapdata["ID"] = OSD.FromUUID(UUID.Zero);
                    responsemapdata["Name"] = OSD.FromString(Util.Md5Hash(m_scene.RegionInfo.RegionName + tc.ToString()));
                    responsemapdata["Extra"] = OSD.FromInteger(1);
                    responsemapdata["Extra2"] = OSD.FromInteger(0);
                    responsearr.Add(responsemapdata);
                });
                responsemap["6"] = responsearr;
            }

            // Service 7 (MAP_ITEM_LAND_FOR_SALE)

            ILandChannel landChannel = m_scene.LandChannel;
            List<ILandObject> parcels = landChannel.AllParcels();

            if ((parcels == null) || (parcels.Count == 0))
            {
                OSDMap responsemapdata = new OSDMap();
                responsemapdata["X"] = OSD.FromInteger((int)(xstart + 1));
                responsemapdata["Y"] = OSD.FromInteger((int)(ystart + 1));
                responsemapdata["ID"] = OSD.FromUUID(UUID.Zero);
                responsemapdata["Name"] = OSD.FromString("");
                responsemapdata["Extra"] = OSD.FromInteger(0);
                responsemapdata["Extra2"] = OSD.FromInteger(0);
                OSDArray responsearr = new OSDArray();
                responsearr.Add(responsemapdata);

                responsemap["7"] = responsearr;
            }
            else
            {
                OSDArray responsearr = new OSDArray(m_scene.GetRootAgentCount());
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
                        Vector3 min = parcel.AABBMin;
                        Vector3 max = parcel.AABBMax;
                        float x = (min.X+max.X)/2;
                        float y = (min.Y+max.Y)/2;

                        OSDMap responsemapdata = new OSDMap();
                        responsemapdata["X"] = OSD.FromInteger((int)(xstart + x));
                        responsemapdata["Y"] = OSD.FromInteger((int)(ystart + y));
                        // responsemapdata["Z"] = OSD.FromInteger((int)m_scene.GetGroundHeight(x,y));
                        responsemapdata["ID"] = OSD.FromUUID(parcel.GlobalID);
                        responsemapdata["Name"] = OSD.FromString(parcel.Name);
                        responsemapdata["Extra"] = OSD.FromInteger(parcel.Area);
                        responsemapdata["Extra2"] = OSD.FromInteger(parcel.SalePrice);
                        responsearr.Add(responsemapdata);
                    }
                }
                responsemap["7"] = responsearr;
            }

            if (m_scene.RegionInfo.RegionSettings.TelehubObject != UUID.Zero)
            {
                SceneObjectGroup sog = m_scene.GetSceneObjectGroup(m_scene.RegionInfo.RegionSettings.TelehubObject);
                if (sog != null)
                {
                    OSDArray responsearr = new OSDArray();
                    OSDMap responsemapdata = new OSDMap();
                    responsemapdata["X"] = OSD.FromInteger((int)(xstart + sog.AbsolutePosition.X));
                    responsemapdata["Y"] = OSD.FromInteger((int)(ystart + sog.AbsolutePosition.Y));
                    // responsemapdata["Z"] = OSD.FromInteger((int)m_scene.GetGroundHeight(x,y));
                    responsemapdata["ID"] = OSD.FromUUID(sog.UUID);
                    responsemapdata["Name"] = OSD.FromString(sog.Name);
                    responsemapdata["Extra"] = OSD.FromInteger(0); // color (unused)
                    responsemapdata["Extra2"] = OSD.FromInteger(0); // 0 = telehub / 1 = infohub
                    responsearr.Add(responsemapdata);

                    responsemap["1"] = responsearr;
                }
            }

            return responsemap;
        }

        public void GenerateMaptile()
        {
            // Cannot create a map for a nonexistent heightmap
            if (m_scene.Heightmap == null)
                return;

            m_log.DebugFormat("[WORLD MAP]: Generating map image for {0}", m_scene.Name);

            using (Bitmap mapbmp = m_mapImageGenerator.CreateMapTile())
                GenerateMaptile(mapbmp);
        }

        private void GenerateMaptile(Bitmap mapbmp)
        {
            byte[] data;

            try
            {
                data = OpenJPEG.EncodeFromImage(mapbmp, true);
            }
            catch (Exception e) // LEGIT: Catching problems caused by OpenJPEG p/invoke
            {
                m_log.Error("[WORLD MAP]: Failed generating terrain map: " + e);
                return;
            }

            byte[] overlay = GenerateOverlay();

            UUID terrainImageID = UUID.Random();
            UUID parcelImageID = UUID.Zero;

            AssetBase asset = new AssetBase(
                terrainImageID,
                "terrainImage_" + m_scene.RegionInfo.RegionID.ToString(),
                (sbyte)AssetType.Texture,
                m_scene.RegionInfo.RegionID.ToString());
            asset.Data = data;
            asset.Description = m_scene.RegionInfo.RegionName;
            asset.Temporary = false;
            asset.Flags = AssetFlags.Maptile;

            // Store the new one
            m_log.DebugFormat("[WORLD MAP]: Storing map tile {0} for {1}", asset.ID, m_scene.RegionInfo.RegionName);
            
            m_scene.AssetService.Store(asset);

            if (overlay != null)
            {
                parcelImageID = UUID.Random();

                AssetBase parcels = new AssetBase(
                    parcelImageID,
                    "parcelImage_" + m_scene.RegionInfo.RegionID.ToString(),
                    (sbyte)AssetType.Texture,
                    m_scene.RegionInfo.RegionID.ToString());
                parcels.Data = overlay;
                parcels.Description = m_scene.RegionInfo.RegionName;
                parcels.Temporary = false;
                parcels.Flags = AssetFlags.Maptile;

                m_scene.AssetService.Store(parcels);
            }

            // Switch to the new one
            UUID lastTerrainImageID = m_scene.RegionInfo.RegionSettings.TerrainImageID;
            UUID lastParcelImageID = m_scene.RegionInfo.RegionSettings.ParcelImageID;
            m_scene.RegionInfo.RegionSettings.TerrainImageID = terrainImageID;
            m_scene.RegionInfo.RegionSettings.ParcelImageID = parcelImageID;
            m_scene.RegionInfo.RegionSettings.Save();

            // Delete the old one
            // m_log.DebugFormat("[WORLDMAP]: Deleting old map tile {0}", lastTerrainImageID);
            m_scene.AssetService.Delete(lastTerrainImageID.ToString());
            if (lastParcelImageID != UUID.Zero)
                m_scene.AssetService.Delete(lastParcelImageID.ToString());
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
        }

        public void OnRegionUp(GridRegion otherRegion)
        {
            ulong regionhandle = otherRegion.RegionHandle;
            string httpserver = otherRegion.ServerURI + "MAP/MapItems/" + regionhandle.ToString();

            lock (m_blacklistedregions)
            {
                if (!m_blacklistedregions.ContainsKey(regionhandle))
                    m_blacklistedregions.Remove(regionhandle);
            }

            lock (m_blacklistedurls)
            {
                if (m_blacklistedurls.ContainsKey(httpserver))
                    m_blacklistedurls.Remove(httpserver);
            }

            lock (m_cachedRegionMapItemsAddress)
            {
                if (!m_cachedRegionMapItemsAddress.ContainsKey(regionhandle))
                    m_cachedRegionMapItemsAddress.Remove(regionhandle);
            }
        }

        private Byte[] GenerateOverlay()
        {
            // These need to be ints for bitmap generation
            int regionSizeX = (int)m_scene.RegionInfo.RegionSizeX;
            int regionSizeY = (int)m_scene.RegionInfo.RegionSizeY;

            int landTileSize = LandManagementModule.LandUnit;
            int regionLandTilesX = regionSizeX / landTileSize;
            int regionLandTilesY = regionSizeY / landTileSize;

            using (Bitmap overlay = new Bitmap(regionSizeX, regionSizeY))
            {
                bool[,] saleBitmap = new bool[regionLandTilesX, regionLandTilesY];
                for (int x = 0; x < regionLandTilesX; x++)
                {
                    for (int y = 0; y < regionLandTilesY; y++)
                        saleBitmap[x, y] = false;
                }

                bool landForSale = false;

                List<ILandObject> parcels = m_scene.LandChannel.AllParcels();

                Color background = Color.FromArgb(0, 0, 0, 0);

                using (Graphics g = Graphics.FromImage(overlay))
                {
                    using (SolidBrush transparent = new SolidBrush(background))
                        g.FillRectangle(transparent, 0, 0, regionSizeX, regionSizeY);

                    foreach (ILandObject land in parcels)
                    {
                        // m_log.DebugFormat("[WORLD MAP]: Parcel {0} flags {1}", land.LandData.Name, land.LandData.Flags);
                        if ((land.LandData.Flags & (uint)ParcelFlags.ForSale) != 0)
                        {
                            landForSale = true;

                            saleBitmap = land.MergeLandBitmaps(saleBitmap, land.GetLandBitmap());
                        }
                    }

                    if (!landForSale)
                    {
                        m_log.DebugFormat("[WORLD MAP]: Region {0} has no parcels for sale, not generating overlay", m_scene.RegionInfo.RegionName);
                        return null;
                    }

                    m_log.DebugFormat("[WORLD MAP]: Region {0} has parcels for sale, generating overlay", m_scene.RegionInfo.RegionName);

                    using (SolidBrush yellow = new SolidBrush(Color.FromArgb(255, 249, 223, 9)))
                    {
                        for (int x = 0 ; x < regionLandTilesX ; x++)
                        {
                            for (int y = 0 ; y < regionLandTilesY ; y++)
                            {
                                if (saleBitmap[x, y])
                                    g.FillRectangle(
                                        yellow, x * landTileSize, 
                                        regionSizeX - landTileSize - (y * landTileSize), 
                                        landTileSize, 
                                        landTileSize);
                            }
                        }
                    }
                }

                try
                {
                    return OpenJPEG.EncodeFromImage(overlay, true);
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[WORLD MAP]: Error creating parcel overlay: " + e.ToString());
                }
            }

            return null;
        }
    }

    public struct MapRequestState
    {
        public UUID agentID;
        public uint flags;
        public uint EstateID;
        public bool godlike;
        public uint itemtype;
        public ulong regionhandle;
    }
}
