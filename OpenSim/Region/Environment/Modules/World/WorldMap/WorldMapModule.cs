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
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.StructuredData;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Types;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;

using LLSD = OpenMetaverse.StructuredData.LLSD;
using LLSDMap = OpenMetaverse.StructuredData.LLSDMap;
using LLSDArray = OpenMetaverse.StructuredData.LLSDArray;

namespace OpenSim.Region.Environment.Modules.World.WorldMap
{
    public class WorldMapModule : IRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly string m_mapLayerPath = "0001/";

        //private IConfig m_config;
        private Scene m_scene;
        private List<MapBlockData> cachedMapBlocks = new List<MapBlockData>();
        private int cachedTime = 0;
        private byte[] myMapImageJPEG;
        private bool m_Enabled = false;
        private Dictionary<UUID, MapRequestState> m_openRequests = new Dictionary<UUID, MapRequestState>();
        

        //private int CacheRegionsDistance = 256;

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            IConfig startupConfig = config.Configs["Startup"];
            if (startupConfig.GetString("WorldMapModule", "WorldMap") ==
                    "WorldMap")
                m_Enabled = true;

            if (!m_Enabled)
                return;

            myMapImageJPEG = new byte[0];

            m_scene = scene;
            string regionimage = "regionImage" + scene.RegionInfo.RegionID.ToString();
            regionimage = regionimage.Replace("-", "");
            m_log.Warn("[WEBMAP]: JPEG Map location: http://" + m_scene.RegionInfo.ExternalEndPoint.Address.ToString() + ":" + m_scene.RegionInfo.HttpPort.ToString() + "/index.php?method=" + regionimage);


            m_scene.AddHTTPHandler(regionimage, OnHTTPGetMapImage);
            m_scene.AddLLSDHandler("/MAP/MapItems/" + scene.RegionInfo.RegionHandle.ToString(),HandleRemoteMapItemRequest);
            //QuadTree.Subdivide();
            //QuadTree.Subdivide();

            scene.EventManager.OnRegisterCaps += OnRegisterCaps;
            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += ClientLoggedOut;
        }
        public void PostInitialise()
        {

        }

        public void Close()
        {
        }
        public string Name
        {
            get { return "WorldMapModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        public void OnRegisterCaps(UUID agentID, Caps caps)
        {
            m_log.DebugFormat("[VOICE] OnRegisterCaps: agentID {0} caps {1}", agentID, caps);
            string capsBase = "/CAPS/" + caps.CapsObjectPath;
            caps.RegisterHandler("MapLayer",
                                 new RestStreamHandler("POST", capsBase + m_mapLayerPath,
                                                       delegate(string request, string path, string param,
                                                                OSHttpRequest httpRequest, OSHttpResponse httpResponse)
                                                           {
                                                               return MapLayerRequest(request, path, param,
                                                                                      agentID, caps);
                                                           }));
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
            //{
                //m_log.DebugFormat("[MAPLAYER]: request: {0}, path: {1}, param: {2}, agent:{3}",
                                  //request, path, param,agentID.ToString());

            // this is here because CAPS map requests work even beyond the 10,000 limit.
            ScenePresence avatarPresence = null;

            m_scene.TryGetAvatar(agentID, out avatarPresence);

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
                    List<MapBlockData> mapBlocks;

                    mapBlocks = m_scene.SceneGridService.RequestNeighbourMapBlocks((int)m_scene.RegionInfo.RegionLocX - 8, (int)m_scene.RegionInfo.RegionLocY - 8, (int)m_scene.RegionInfo.RegionLocX + 8, (int)m_scene.RegionInfo.RegionLocY + 8);
                    avatarPresence.ControllingClient.SendMapBlock(mapBlocks,0);

                    lock (cachedMapBlocks)
                        cachedMapBlocks = mapBlocks;

                    cachedTime = Util.UnixTimeSinceEpoch();
                }
            }
            LLSDMapLayerResponse mapResponse = new LLSDMapLayerResponse();
            mapResponse.LayerData.Array.Add(GetLLSDMapLayerResponse());
            return mapResponse.ToString();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="mapReq"></param>
        /// <returns></returns>
        public LLSDMapLayerResponse GetMapLayer(LLSDMapRequest mapReq)
        {
            m_log.Debug("[CAPS]: MapLayer Request in region: " + m_scene.RegionInfo.RegionName);
            LLSDMapLayerResponse mapResponse = new LLSDMapLayerResponse();
            mapResponse.LayerData.Array.Add(GetLLSDMapLayerResponse());
            return mapResponse;
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        protected static LLSDMapLayer GetLLSDMapLayerResponse()
        {
            LLSDMapLayer mapLayer = new LLSDMapLayer();
            mapLayer.Right = 5000;
            mapLayer.Top = 5000;
            mapLayer.ImageID = new UUID("00000000-0000-1111-9999-000000000006");

            return mapLayer;
        }
        #region EventHandlers


        private void OnNewClient(IClientAPI client)
        {
            // All friends establishment protocol goes over instant message
            // There's no way to send a message from the sim
            // to a user to 'add a friend' without causing dialog box spam
            //
            // The base set of friends are added when the user signs on in their XMLRPC response
            // Generated by LoginService.  The friends are retreived from the database by the UserManager

            // Subscribe to instant messages

            //client.OnInstantMessage += OnInstantMessage;
            //client.OnApproveFriendRequest += OnApprovedFriendRequest;
            //client.OnDenyFriendRequest += OnDenyFriendRequest;
            //client.OnTerminateFriendship += OnTerminateFriendship;

            //doFriendListUpdateOnline(client.AgentId);
            client.OnRequestMapBlocks += RequestMapBlocks;
            client.OnMapItemRequest += HandleMapItemRequest;
        }
        private void ClientLoggedOut(UUID AgentId)
        {

        }
        #endregion

        public virtual void HandleMapItemRequest(IClientAPI remoteClient, uint flags, 
            uint EstateID, bool godlike, uint itemtype, ulong regionhandle)
        {
            uint xstart = 0;
            uint ystart = 0;
            Helpers.LongToUInts(m_scene.RegionInfo.RegionHandle, out xstart, out ystart);
            if (itemtype == 6) // we only sevice 6 right now (avatar green dots)
            {
                if (regionhandle == 0 || regionhandle == m_scene.RegionInfo.RegionHandle)
                {    
                    List<ScenePresence> avatars = m_scene.GetAvatars();
                    int tc = System.Environment.TickCount;
                    List<mapItemReply> mapitems = new List<mapItemReply>();
                    mapItemReply mapitem = new mapItemReply();
                    if (avatars.Count == 0 || avatars.Count == 1)
                    {
                        mapitem = new mapItemReply();
                        mapitem.x = (uint)(xstart + 1);
                        mapitem.y = (uint)(ystart + 1);
                        mapitem.id = UUID.Zero;
                        mapitem.name = Util.Md5Hash(m_scene.RegionInfo.RegionName + tc.ToString());
                        mapitem.Extra = 0;
                        mapitem.Extra2 = 0;
                        mapitems.Add(mapitem);
                    }
                    else
                    {
                        
                        foreach (ScenePresence av in avatars)
                        {
                            // Don't send a green dot for yourself
                            if (av.UUID != remoteClient.AgentId)
                            {
                                mapitem = new mapItemReply();
                                mapitem.x = (uint)(xstart + av.AbsolutePosition.X);
                                mapitem.y = (uint)(ystart + av.AbsolutePosition.Y);
                                mapitem.id = UUID.Zero;
                                mapitem.name = Util.Md5Hash(m_scene.RegionInfo.RegionName + tc.ToString());
                                mapitem.Extra = 1;
                                mapitem.Extra2 = 0;
                                mapitems.Add(mapitem);
                            }
                        }
                    }
                    remoteClient.SendMapItemReply(mapitems.ToArray(), itemtype, flags);
                }
                else
                {
                    RegionInfo mreg = m_scene.SceneGridService.RequestNeighbouringRegionInfo(regionhandle);
                    if (mreg != null)
                    {
                        string httpserver = "http://" + mreg.ExternalEndPoint.Address.ToString() + ":" + mreg.HttpPort + "/MAP/MapItems/" + regionhandle.ToString();
                        
                        RequestMapItems(httpserver,remoteClient.AgentId,flags,EstateID,godlike,itemtype,regionhandle);
                        
                    }
                }
            }

        }

        public delegate LLSDMap RequestMapItemsDelegate(string httpserver, UUID id, uint flags,
            uint EstateID, bool godlike, uint itemtype, ulong regionhandle);

        private void RequestMapItemsCompleted(IAsyncResult iar)
        {
            
            RequestMapItemsDelegate icon = (RequestMapItemsDelegate)iar.AsyncState;
            LLSDMap response = icon.EndInvoke(iar);
            


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
                    m_scene.TryGetAvatar(mrs.agentID, out av);
                    if (av != null)
                    {
                        if (response.ContainsKey(mrs.itemtype.ToString()))
                        {
                            List<mapItemReply> returnitems = new List<mapItemReply>();
                            LLSDArray itemarray = (LLSDArray)response[mrs.itemtype.ToString()];
                            for (int i = 0; i < itemarray.Count; i++)
                            {
                                LLSDMap mapitem = (LLSDMap)itemarray[i];
                                mapItemReply mi = new mapItemReply();
                                mi.x = (uint)mapitem["X"].AsInteger();
                                mi.y = (uint)mapitem["Y"].AsInteger();
                                mi.id = mapitem["ID"].AsUUID();
                                mi.Extra = mapitem["Extra"].AsInteger();
                                mi.Extra2 = mapitem["Extra2"].AsInteger();
                                mi.name = mapitem["Name"].AsString();
                                returnitems.Add(mi);
                            }
                            av.ControllingClient.SendMapItemReply(returnitems.ToArray(), mrs.itemtype, mrs.flags);
                        }
                        
                    }

                }
            }
        }
        public void RequestMapItems(string httpserver, UUID id, uint flags,
            uint EstateID, bool godlike, uint itemtype, ulong regionhandle)
        {
            //m_log.Info("[INTER]: " + debugRegionName + ": SceneCommunicationService: Sending InterRegion Notification that region is up " + region.RegionName);
            RequestMapItemsDelegate d = RequestMapItemsAsync;
            d.BeginInvoke(httpserver, id,flags,EstateID,godlike,itemtype,regionhandle,RequestMapItemsCompleted, d);
            //bool val = m_commsProvider.InterRegion.RegionUp(new SerializableRegionInfo(region));
        }
        private LLSDMap RequestMapItemsAsync(string httpserver, UUID id, uint flags,
            uint EstateID, bool godlike, uint itemtype, ulong regionhandle)
        {
            UUID requestID = UUID.Random();
            
            MapRequestState mrs = new MapRequestState();
            mrs.agentID = id;
            mrs.EstateID = EstateID;
            mrs.flags = flags;
            mrs.godlike = godlike;
            mrs.itemtype=itemtype;
            mrs.regionhandle = regionhandle;

            lock (m_openRequests)
                m_openRequests.Add(requestID, mrs);

            WebRequest mapitemsrequest = WebRequest.Create(httpserver);
            mapitemsrequest.Method = "POST";
            mapitemsrequest.ContentType = "application/xml+llsd";
            LLSDMap RAMap = new LLSDMap();

            string RAMapString = RAMap.ToString();
            LLSD LLSDofRAMap = RAMap; // RENAME if this works

            byte[] buffer = LLSDParser.SerializeXmlBytes(LLSDofRAMap);
            LLSDMap responseMap = new LLSDMap();
            responseMap["requestID"] = LLSD.FromUUID(requestID);

            Stream os = null;
            try
            { // send the Post
                mapitemsrequest.ContentLength = buffer.Length;   //Count bytes to send
                os = mapitemsrequest.GetRequestStream();
                os.Write(buffer, 0, buffer.Length);         //Send it
                os.Close();
                m_log.InfoFormat("[WorldMap]: Getting MapItems from Sim {0}", httpserver);
            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[WorldMap] Bad send on GetMapItems {0}", ex.Message);
                responseMap["connect"] = LLSD.FromBoolean(false);

                return responseMap;
            }

            //m_log.Info("[OGP] waiting for a reply after rez avatar send");
            string response_mapItems_reply = null;
            { // get the response
                try
                {
                    WebResponse webResponse = mapitemsrequest.GetResponse();
                    if (webResponse == null)
                    {
                        //m_log.Info("[OGP:] Null reply on rez_avatar post");
                    }

                    StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                    response_mapItems_reply = sr.ReadToEnd().Trim();
                    //m_log.InfoFormat("[OGP]: rez_avatar reply was {0} ", response_mapItems_reply);

                }
                catch (WebException ex)
                {
                    //m_log.InfoFormat("[OGP]: exception on read after send of rez avatar {0}", ex.Message);
                    responseMap["connect"] = LLSD.FromBoolean(false);

                    return responseMap;
                }
                LLSD rezResponse = null;
                try
                {
                    rezResponse = LLSDParser.DeserializeXml(response_mapItems_reply);

                    responseMap = (LLSDMap)rezResponse;
                    responseMap["requestID"] = LLSD.FromUUID(requestID);
                }
                catch (Exception ex)
                {
                    //m_log.InfoFormat("[OGP]: exception on parse of rez reply {0}", ex.Message);
                    responseMap["connect"] = LLSD.FromBoolean(false);

                    return responseMap;
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
            List<MapBlockData> mapBlocks;
            if ((flag & 0x10000) != 0)  // user clicked on the map a tile that isn't visible
            {
                List<MapBlockData> response = new List<MapBlockData>();
                
                // this should return one mapblock at most. But make sure: Look whether the one we requested is in there
                mapBlocks = m_scene.SceneGridService.RequestNeighbourMapBlocks(minX, minY, maxX, maxY);
                if (mapBlocks != null)
                {
                    foreach (MapBlockData block in mapBlocks)
                    {
                        if (block.X == minX && block.Y == minY)
                        {
                            // found it => add it to response
                            response.Add(block);
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
                    block.Access = 254; // == not there
                    response.Add(block);
                }
                remoteClient.SendMapBlock(response, 0);
            }
            else
            {
                // normal mapblock request. Use the provided values
                mapBlocks = m_scene.SceneGridService.RequestNeighbourMapBlocks(minX - 4, minY - 4, maxX + 4, maxY + 4);
                remoteClient.SendMapBlock(mapBlocks, flag);
            }
        }

        public Hashtable OnHTTPGetMapImage(Hashtable keysvals)
        {
            m_log.Info("[WEBMAP]: Sending map image jpeg");
            Hashtable reply = new Hashtable();
            int statuscode = 200;
            byte[] jpeg = new byte[0];

            if (myMapImageJPEG.Length == 0)
            {
                MemoryStream imgstream = new MemoryStream();
                Bitmap mapTexture = new Bitmap(1,1);
                ManagedImage managedImage;
                Image image = (Image)mapTexture;

                try
                {
                    // Taking our jpeg2000 data, decoding it, then saving it to a byte array with regular jpeg data

                    imgstream = new MemoryStream();

                    // non-async because we know we have the asset immediately.
                    AssetBase mapasset = m_scene.AssetCache.GetAsset(m_scene.RegionInfo.lastMapUUID, true);

                    // Decode image to System.Drawing.Image
                    if (OpenJPEG.DecodeToImage(mapasset.Data, out managedImage, out image))
                    {
                        // Save to bitmap
                        mapTexture = new Bitmap(image);

                        ImageCodecInfo myImageCodecInfo;

                        Encoder myEncoder;

                        EncoderParameter myEncoderParameter;
                        EncoderParameters myEncoderParameters = new EncoderParameters();

                        myImageCodecInfo = GetEncoderInfo("image/jpeg");

                        myEncoder = Encoder.Quality;

                        myEncoderParameter = new EncoderParameter(myEncoder, 95L);
                        myEncoderParameters.Param[0] = myEncoderParameter;

                    myEncoderParameter = new EncoderParameter(myEncoder, 95L);
                    myEncoderParameters.Param[0] = myEncoderParameter;

                    // Save bitmap to stream
                    mapTexture.Save(imgstream, myImageCodecInfo, myEncoderParameters);

                        // Write the stream to a byte array for output
                        jpeg = imgstream.ToArray();
                        myMapImageJPEG = jpeg;
                    }
                }
                catch (Exception)
                {
                    // Dummy!
                    m_log.Warn("[WEBMAP]: Unable to generate Map image");
                }
                finally
                {
                    // Reclaim memory, these are unmanaged resources
                    mapTexture.Dispose();
                    image.Dispose();
                    imgstream.Close();
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
        public LLSD HandleRemoteMapItemRequest(string path, LLSD request, string endpoint)
        {
            uint xstart = 0;
            uint ystart = 0;
            
            Helpers.LongToUInts(m_scene.RegionInfo.RegionHandle,out xstart,out ystart);

            LLSDMap responsemap = new LLSDMap();
            List<ScenePresence> avatars = m_scene.GetAvatars();
            LLSDArray responsearr = new LLSDArray(avatars.Count);
            LLSDMap responsemapdata = new LLSDMap();
            int tc = System.Environment.TickCount;
            /* 
            foreach (ScenePresence av in avatars)
            {
                responsemapdata = new LLSDMap();
                responsemapdata["X"] = LLSD.FromInteger((int)(xstart + av.AbsolutePosition.X));
                responsemapdata["Y"] = LLSD.FromInteger((int)(ystart + av.AbsolutePosition.Y));
                responsemapdata["ID"] = LLSD.FromUUID(UUID.Zero);
                responsemapdata["Name"] = LLSD.FromString("TH");
                responsemapdata["Extra"] = LLSD.FromInteger(0);
                responsemapdata["Extra2"] = LLSD.FromInteger(0);
                responsearr.Add(responsemapdata);
            }
            responsemap["1"] = responsearr;
            */
            if (avatars.Count == 0)
            {
                responsemapdata = new LLSDMap();
                responsemapdata["X"] = LLSD.FromInteger((int)(xstart + 1));
                responsemapdata["Y"] = LLSD.FromInteger((int)(ystart + 1));
                responsemapdata["ID"] = LLSD.FromUUID(UUID.Zero);
                responsemapdata["Name"] = LLSD.FromString(Util.Md5Hash(m_scene.RegionInfo.RegionName + tc.ToString()));
                responsemapdata["Extra"] = LLSD.FromInteger(0);
                responsemapdata["Extra2"] = LLSD.FromInteger(0);
                responsearr.Add(responsemapdata);
                
                responsemap["6"] = responsearr;
            }
            else 
            {
                responsearr = new LLSDArray(avatars.Count);
                foreach (ScenePresence av in avatars)
                {
                    responsemapdata = new LLSDMap();
                    responsemapdata["X"] = LLSD.FromInteger((int)(xstart + av.AbsolutePosition.X));
                    responsemapdata["Y"] = LLSD.FromInteger((int)(ystart + av.AbsolutePosition.Y));
                    responsemapdata["ID"] = LLSD.FromUUID(UUID.Zero);
                    responsemapdata["Name"] = LLSD.FromString(Util.Md5Hash(m_scene.RegionInfo.RegionName + tc.ToString()));
                    responsemapdata["Extra"] = LLSD.FromInteger(1);
                    responsemapdata["Extra2"] = LLSD.FromInteger(0);
                    responsearr.Add(responsemapdata);
                }
                responsemap["6"] = responsearr;
            }
            return responsemap;
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
