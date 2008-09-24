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
using System.Reflection;
using OpenMetaverse;
using OpenMetaverse.Imaging;
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
        }
        private void ClientLoggedOut(UUID AgentId)
        {

        }
        #endregion

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
            mapBlocks = m_scene.SceneGridService.RequestNeighbourMapBlocks(minX - 4, minY - 4, minX + 4, minY + 4);
            remoteClient.SendMapBlock(mapBlocks, flag);
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
    }
}
