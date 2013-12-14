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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using System.Net;
using System.IO;
using System.Timers;
using System.Drawing;
using System.Drawing.Imaging;

using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

//namespace OpenSim.Region.OptionalModules.Simian
namespace OpenSim.Services.Connectors.SimianGrid
{
    /// <summary>
    /// </summary>
    /// <remarks>
    /// </remarks>

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SimianGridMaptile")]
    public class SimianGridMaptile : ISharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_enabled = false;
        private string m_serverUrl = String.Empty;
        private Dictionary<UUID, Scene> m_scenes = new Dictionary<UUID, Scene>();

        private int m_refreshtime = 0;
        private int m_lastrefresh = 0;
        private System.Timers.Timer m_refreshTimer = new System.Timers.Timer();
        
        #region ISharedRegionModule
        
        public Type ReplaceableInterface { get { return null; } }
        public string Name { get { return "SimianGridMaptile"; } }        
        public void RegionLoaded(Scene scene) { }
        public void Close() { }
        
        ///<summary>
        ///
        ///</summary>
        public void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["SimianGridMaptiles"];
            if (config == null)
                return;
            
            if (! config.GetBoolean("Enabled", false))
                return;

            m_serverUrl = config.GetString("MaptileURL");
            if (String.IsNullOrEmpty(m_serverUrl))
                return;

            int refreshseconds = Convert.ToInt32(config.GetString("RefreshTime"));
            if (refreshseconds <= 0)
                return;

            m_refreshtime = refreshseconds * 1000; // convert from seconds to ms
            m_log.InfoFormat("[SIMIAN MAPTILE] enabled with refresh timeout {0} and URL {1}",
                             m_refreshtime,m_serverUrl);

            m_enabled = true;
        }

        ///<summary>
        ///
        ///</summary>
        public void PostInitialise()
        {
            if (m_enabled)
            {
                m_refreshTimer.Enabled = true;
                m_refreshTimer.AutoReset = true;
                m_refreshTimer.Interval = 5 * 60 * 1000; // every 5 minutes
                m_refreshTimer.Elapsed += new ElapsedEventHandler(HandleMaptileRefresh);
            }
        }


        ///<summary>
        ///
        ///</summary>
        public void AddRegion(Scene scene)
        {
            if (! m_enabled)
                return;

            // Every shared region module has to maintain an indepedent list of
            // currently running regions
            lock (m_scenes)
                m_scenes[scene.RegionInfo.RegionID] = scene;
        }

        ///<summary>
        ///
        ///</summary>
        public void RemoveRegion(Scene scene)
        {
            if (! m_enabled)
                return;

            lock (m_scenes)
                m_scenes.Remove(scene.RegionInfo.RegionID);
        }

        #endregion ISharedRegionModule

        ///<summary>
        ///
        ///</summary>
        private void HandleMaptileRefresh(object sender, EventArgs ea)
        {
            // this approach is a bit convoluted becase we want to wait for the
            // first upload to happen on startup but after all the objects are
            // loaded and initialized
            if (m_lastrefresh > 0 && Util.EnvironmentTickCountSubtract(m_lastrefresh) < m_refreshtime)
                return;

            m_log.DebugFormat("[SIMIAN MAPTILE] map refresh fired");
            lock (m_scenes)
            {
                foreach (IScene scene in m_scenes.Values)
                {
                    try
                    {
                        UploadMapTile(scene);
                    }
                    catch (Exception ex)
                    {
                        m_log.WarnFormat("[SIMIAN MAPTILE] something bad happened {0}",ex.Message);
                    }
                }
            }

            m_lastrefresh = Util.EnvironmentTickCount();
        }

        ///<summary>
        ///
        ///</summary>
        private void UploadMapTile(IScene scene)
        {
            m_log.DebugFormat("[SIMIAN MAPTILE]: upload maptile for {0}",scene.RegionInfo.RegionName);

            // Create a PNG map tile and upload it to the AddMapTile API
            byte[] pngData = Utils.EmptyBytes;
            IMapImageGenerator tileGenerator = scene.RequestModuleInterface<IMapImageGenerator>();
            if (tileGenerator == null)
            {
                m_log.Warn("[SIMIAN MAPTILE]: Cannot upload PNG map tile without an ImageGenerator");
                return;
            }

            using (Image mapTile = tileGenerator.CreateMapTile())
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    mapTile.Save(stream, ImageFormat.Png);
                    pngData = stream.ToArray();
                }
            }

            NameValueCollection requestArgs = new NameValueCollection
                {
                        { "RequestMethod", "xAddMapTile" },
                        { "X", scene.RegionInfo.RegionLocX.ToString() },
                        { "Y", scene.RegionInfo.RegionLocY.ToString() },
                        { "ContentType", "image/png" },
                        { "EncodedData", System.Convert.ToBase64String(pngData) }
                };
                            
            OSDMap response = SimianGrid.PostToService(m_serverUrl,requestArgs);
            if (! response["Success"].AsBoolean())
            {
                m_log.WarnFormat("[SIMIAN MAPTILE] failed to store map tile; {0}",response["Message"].AsString());
                return;
            }

            // List<MultipartForm.Element> postParameters = new List<MultipartForm.Element>()
            // {
            //     new MultipartForm.Parameter("X", scene.RegionInfo.RegionLocX.ToString()),
            //     new MultipartForm.Parameter("Y", scene.RegionInfo.RegionLocY.ToString()),
            //     new MultipartForm.File("Tile", "tile.png", "image/png", pngData)
            // };

            // string errorMessage = null;
            // int tickstart = Util.EnvironmentTickCount();

            // // Make the remote storage request
            // try
            // {
            //     HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(m_serverUrl);
            //     request.Timeout = 20000;
            //     request.ReadWriteTimeout = 5000;

            //     using (HttpWebResponse response = MultipartForm.Post(request, postParameters))
            //     {
            //         using (Stream responseStream = response.GetResponseStream())
            //         {
            //             string responseStr = responseStream.GetStreamString();
            //             OSD responseOSD = OSDParser.Deserialize(responseStr);
            //             if (responseOSD.Type == OSDType.Map)
            //             {
            //                 OSDMap responseMap = (OSDMap)responseOSD;
            //                 if (responseMap["Success"].AsBoolean())
            //                     return;

            //                 errorMessage = "Upload failed: " + responseMap["Message"].AsString();
            //             }
            //             else
            //             {
            //                 errorMessage = "Response format was invalid:\n" + responseStr;
            //             }
            //         }
            //     }
            // }
            // catch (WebException we)
            // {
            //     errorMessage = we.Message;
            //     if (we.Status == WebExceptionStatus.ProtocolError)
            //     {
            //         HttpWebResponse webResponse = (HttpWebResponse)we.Response;
            //         errorMessage = String.Format("[{0}] {1}",
            //                                      webResponse.StatusCode,webResponse.StatusDescription);
            //     }
            // }
            // catch (Exception ex)
            // {
            //     errorMessage = ex.Message;
            // }
            // finally
            // {
            //     // This just dumps a warning for any operation that takes more than 100 ms
            //     int tickdiff = Util.EnvironmentTickCountSubtract(tickstart);
            //     m_log.DebugFormat("[SIMIAN MAPTILE]: map tile uploaded in {0}ms",tickdiff);
            // }

            // m_log.WarnFormat("[SIMIAN MAPTILE]: Failed to store {0} byte tile for {1}: {2}",
            //                  pngData.Length, scene.RegionInfo.RegionName, errorMessage);

        }
    }
}