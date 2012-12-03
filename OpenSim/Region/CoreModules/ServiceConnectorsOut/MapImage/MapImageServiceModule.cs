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
using OpenSim.Services.Interfaces;
using OpenSim.Server.Base;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.MapImage
{
    /// <summary>
    /// </summary>
    /// <remarks>
    /// </remarks>

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "MapImageServiceModule")]
    public class MapImageServiceModule : ISharedRegionModule
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_enabled = false;
        private IMapImageService m_MapService;

        private Dictionary<UUID, Scene> m_scenes = new Dictionary<UUID, Scene>();

        private int m_refreshtime = 0;
        private int m_lastrefresh = 0;
        private System.Timers.Timer m_refreshTimer = new System.Timers.Timer();
        
        #region ISharedRegionModule
        
        public Type ReplaceableInterface { get { return null; } }
        public string Name { get { return "MapImageServiceModule"; } }        
        public void RegionLoaded(Scene scene) { }
        public void Close() { }
        public void PostInitialise() { }

        
        ///<summary>
        ///
        ///</summary>
        public void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("MapImageService", "");
                if (name != Name)
                    return;
            }

            IConfig config = source.Configs["MapImageService"];
            if (config == null)
                return;

            int refreshminutes = Convert.ToInt32(config.GetString("RefreshTime", "-1"));
            if (refreshminutes < 0)
            {
                m_log.WarnFormat("[MAP IMAGE SERVICE MODULE]: No refresh time given in config. Module disabled.");
                return;
            }

            m_refreshtime = refreshminutes * 60 * 1000; // convert from minutes to ms

            string service = config.GetString("LocalServiceModule", string.Empty);
            if (service == string.Empty)
            {
                m_log.WarnFormat("[MAP IMAGE SERVICE MODULE]: No service dll given in config. Unable to proceed.");
                return;
            }

            Object[] args = new Object[] { source };
            m_MapService = ServerUtils.LoadPlugin<IMapImageService>(service, args);
            if (m_MapService == null)
            {
                m_log.WarnFormat("[MAP IMAGE SERVICE MODULE]: Unable to load LocalServiceModule from {0}. MapService module disabled. Please fix the configuration.", service);
                return;
            }

            if (m_refreshtime > 0)
            {
                m_refreshTimer.Enabled = true;
                m_refreshTimer.AutoReset = true;
                m_refreshTimer.Interval = m_refreshtime;
                m_refreshTimer.Elapsed += new ElapsedEventHandler(HandleMaptileRefresh);
            }

            m_log.InfoFormat("[MAP IMAGE SERVICE MODULE]: enabled with refresh time {0} min and service object {1}",
                             refreshminutes, service);

            m_enabled = true;
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

            scene.EventManager.OnRegionReadyStatusChange += s => { if (s.Ready) UploadMapTile(s); };
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

            m_log.DebugFormat("[MAP IMAGE SERVICE MODULE]: map refresh!");
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
                        m_log.WarnFormat("[MAP IMAGE SERVICE MODULE]: something bad happened {0}", ex.Message);
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
            m_log.DebugFormat("[MAP IMAGE SERVICE MODULE]: upload maptile for {0}", scene.RegionInfo.RegionName);

            // Create a JPG map tile and upload it to the AddMapTile API
            byte[] jpgData = Utils.EmptyBytes;
            IMapImageGenerator tileGenerator = scene.RequestModuleInterface<IMapImageGenerator>();
            if (tileGenerator == null)
            {
                m_log.Warn("[MAP IMAGE SERVICE MODULE]: Cannot upload PNG map tile without an ImageGenerator");
                return;
            }

            using (Image mapTile = tileGenerator.CreateMapTile())
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    mapTile.Save(stream, ImageFormat.Jpeg);
                    jpgData = stream.ToArray();
                }
            }

            if (jpgData == Utils.EmptyBytes)
            {
                m_log.WarnFormat("[MAP IMAGE SERVICE MODULE]: Tile image generation failed");
                return;
            }

            string reason = string.Empty;
            if (!m_MapService.AddMapTile((int)scene.RegionInfo.RegionLocX, (int)scene.RegionInfo.RegionLocY, jpgData, out reason))
            {
                m_log.DebugFormat("[MAP IMAGE SERVICE MODULE]: Unable to upload tile image for {0} at {1}-{2}: {3}",
                    scene.RegionInfo.RegionName, scene.RegionInfo.RegionLocX, scene.RegionInfo.RegionLocY, reason);
            }
        }
    }
}
