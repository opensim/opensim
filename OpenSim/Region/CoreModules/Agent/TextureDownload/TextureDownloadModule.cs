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
using System.Threading;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using BlockingQueue = OpenSim.Framework.BlockingQueue<OpenSim.Region.Framework.Interfaces.ITextureSender>;
using OpenSim.Services.Interfaces;

namespace OpenSim.Region.CoreModules.Agent.TextureDownload
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule")]
    public class TextureDownloadModule : INonSharedRegionModule
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// There is one queue for all textures waiting to be sent, regardless of the requesting user.
        /// </summary>
        private readonly BlockingQueue m_queueSenders
            = new BlockingQueue();

        /// <summary>
        /// Each user has their own texture download service.
        /// </summary>
        private readonly Dictionary<UUID, UserTextureDownloadService> m_userTextureServices =
            new Dictionary<UUID, UserTextureDownloadService>();

        private Scene m_scene;
        private List<Scene> m_scenes = new List<Scene>();

        public TextureDownloadModule()
        {
        }

        #region INonSharedRegionModule Members

        public void Initialise(IConfigSource config)
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_scene == null)
            {
                //m_log.Debug("Creating Texture download module");
                m_scene = scene;
                //m_thread = new Thread(new ThreadStart(ProcessTextureSenders));
                //m_thread.Name = "ProcessTextureSenderThread";
                //m_thread.IsBackground = true;
                //m_thread.Start();
                //ThreadTracker.Add(m_thread);
            }

            if (!m_scenes.Contains(scene))
            {
                m_scenes.Add(scene);
                m_scene = scene;
                m_scene.EventManager.OnNewClient += NewClient;
                m_scene.EventManager.OnRemovePresence += EventManager_OnRemovePresence;
            }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if(m_scenes.Contains(scene))
                m_scenes.Remove(scene);
            scene.EventManager.OnNewClient -= NewClient;
            scene.EventManager.OnRemovePresence -= EventManager_OnRemovePresence;
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "TextureDownloadModule"; }
        }

        #endregion

        /// <summary>
        /// Cleanup the texture service related objects for the removed presence.
        /// </summary>
        /// <param name="agentId"> </param>
        private void EventManager_OnRemovePresence(UUID agentId)
        {
            UserTextureDownloadService textureService;

            lock (m_userTextureServices)
            {
                if (m_userTextureServices.TryGetValue(agentId, out textureService))
                {
                    textureService.Close();
                    //m_log.DebugFormat("[TEXTURE MODULE]: Removing UserTextureServices from {0}", m_scene.RegionInfo.RegionName);
                    m_userTextureServices.Remove(agentId);
                }
            }
        }

        public void NewClient(IClientAPI client)
        {
            UserTextureDownloadService textureService;

            lock (m_userTextureServices)
            {
                if (m_userTextureServices.TryGetValue(client.AgentId, out textureService))
                {
                    textureService.Close();
                    //m_log.DebugFormat("[TEXTURE MODULE]: Removing outdated UserTextureServices from {0}", m_scene.RegionInfo.RegionName);
                    m_userTextureServices.Remove(client.AgentId);
                }
                m_userTextureServices.Add(client.AgentId, new UserTextureDownloadService(client, m_scene, m_queueSenders));
            }

            client.OnRequestTexture += TextureRequest;
        }

        /// I'm commenting this out, and replacing it with the implementation below, which
        /// may return a null value. This is necessary for avoiding race conditions 
        /// recreating UserTextureServices for clients that have just been closed.
        /// That behavior of always returning a UserTextureServices was causing the
        /// A-B-A problem (mantis #2855).
        /// 
        ///// <summary>
        ///// Does this user have a registered texture download service?
        ///// </summary>
        ///// <param name="userID"></param>
        ///// <param name="textureService"></param>
        ///// <returns>Always returns true, since a service is created if one does not already exist</returns>
        //private bool TryGetUserTextureService(
        //    IClientAPI client, out UserTextureDownloadService textureService)
        //{
        //    lock (m_userTextureServices)
        //    {
        //        if (m_userTextureServices.TryGetValue(client.AgentId, out textureService))
        //        {
        //            //m_log.DebugFormat("[TEXTURE MODULE]: Found existing UserTextureServices in ", m_scene.RegionInfo.RegionName);
        //            return true;
        //        }

        //        m_log.DebugFormat("[TEXTURE MODULE]: Creating new UserTextureServices in ", m_scene.RegionInfo.RegionName);
        //        textureService = new UserTextureDownloadService(client, m_scene, m_queueSenders);
        //        m_userTextureServices.Add(client.AgentId, textureService);

        //        return true;
        //    }
        //}

        /// <summary>
        /// Does this user have a registered texture download service?
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="textureService"></param>
        /// <returns>A UserTextureDownloadService or null in the output parameter, and true or false accordingly.</returns>
        private bool TryGetUserTextureService(IClientAPI client, out UserTextureDownloadService textureService)
        {
            lock (m_userTextureServices)
            {
                if (m_userTextureServices.TryGetValue(client.AgentId, out textureService))
                {
                    //m_log.DebugFormat("[TEXTURE MODULE]: Found existing UserTextureServices in ", m_scene.RegionInfo.RegionName);
                    return true;
                }

                textureService = null;
                return false;
            }
        }

        /// <summary>
        /// Start the process of requesting a given texture.
        /// </summary>
        /// <param name="sender"> </param>
        /// <param name="e"></param>
        public void TextureRequest(Object sender, TextureRequestArgs e)
        {
            IClientAPI client = (IClientAPI)sender;

            if (e.Priority == 1016001f) // Preview
            {
                if (client.Scene is Scene)
                {
                    Scene scene = (Scene)client.Scene;

                    CachedUserInfo profile = scene.CommsManager.UserProfileCacheService.GetUserDetails(client.AgentId);
                    if (profile == null) // Deny unknown user
                        return;

                    IInventoryService invService = scene.InventoryService;
                    if (invService.GetRootFolder(client.AgentId) == null) // Deny no inventory
                        return;

                    // Diva 2009-08-13: this test doesn't make any sense to many devs
                    //if (profile.UserProfile.GodLevel < 200 && profile.RootFolder.FindAsset(e.RequestedAssetID) == null) // Deny if not owned
                    //{
                    //    m_log.WarnFormat("[TEXTURE]: user {0} doesn't have permissions to texture {1}");
                    //    return;
                    //}

                    m_log.Debug("Texture preview");
                }
            }

            UserTextureDownloadService textureService;

            if (TryGetUserTextureService(client, out textureService))
            {
                textureService.HandleTextureRequest(e);
            }
        }

        /// <summary>
        /// Entry point for the thread dedicated to processing the texture queue.
        /// </summary>
        public void ProcessTextureSenders()
        {
            ITextureSender sender = null;

            try
            {
                while (true)
                {
                    sender = m_queueSenders.Dequeue();

                    if (sender.Cancel)
                    {
                        TextureSent(sender);

                        sender.Cancel = false;
                    }
                    else
                    {
                        bool finished = sender.SendTexturePacket();
                        if (finished)
                        {
                            TextureSent(sender);
                        }
                        else
                        {
                            m_queueSenders.Enqueue(sender);
                        }
                    }

                    // Make sure that any sender we currently have can get garbage collected
                    sender = null;

                    //m_log.InfoFormat("[TEXTURE] Texture sender queue size: {0}", m_queueSenders.Count());
                }
            }
            catch (Exception e)
            {
                // TODO: Let users in the sim and those entering it and possibly an external watchdog know what has happened
                m_log.ErrorFormat(
                    "[TEXTURE]: Texture send thread terminating with exception.  PLEASE REBOOT YOUR SIM - TEXTURES WILL NOT BE AVAILABLE UNTIL YOU DO.  Exception is {0}",
                    e);
            }
        }

        /// <summary>
        /// Called when the texture has finished sending.
        /// </summary>
        /// <param name="sender"></param>
        private void TextureSent(ITextureSender sender)
        {
            sender.Sending = false;
            //m_log.DebugFormat("[TEXTURE]: Removing download stat for {0}", sender.assetID);
            m_scene.StatsReporter.AddPendingDownloads(-1);
        }
    }
}
