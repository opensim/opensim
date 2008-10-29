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
using System.Collections.Generic;
using System.Threading;
using OpenMetaverse;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using BlockingQueue = OpenSim.Framework.BlockingQueue<OpenSim.Region.Environment.Interfaces.ITextureSender>;

namespace OpenSim.Region.Environment.Modules.Agent.TextureDownload
{
    public class TextureDownloadModule : IRegionModule
    {
        //private static readonly log4net.ILog m_log
        //    = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// There is one queue for all textures waiting to be sent, regardless of the requesting user.
        /// </summary>
        private readonly OpenSim.Framework.BlockingQueue<ITextureSender> m_queueSenders
            = new OpenSim.Framework.BlockingQueue<ITextureSender>();

        /// <summary>
        /// Each user has their own texture download service.
        /// </summary>
        private readonly Dictionary<UUID, UserTextureDownloadService> m_userTextureServices =
            new Dictionary<UUID, UserTextureDownloadService>();

        private Scene m_scene;
        private List<Scene> m_scenes = new List<Scene>();

        private Thread m_thread;

        public TextureDownloadModule()
        {
        }

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource config)
        {
            if (m_scene == null)
            {
                //Console.WriteLine("Creating Texture download module");
                m_thread = new Thread(new ThreadStart(ProcessTextureSenders));
                m_thread.Name = "ProcessTextureSenderThread";
                m_thread.IsBackground = true;
                m_thread.Start();
                ThreadTracker.Add(m_thread);
            }

            if (!m_scenes.Contains(scene))
            {
                m_scenes.Add(scene);
                m_scene = scene;
                m_scene.EventManager.OnNewClient += NewClient;
                m_scene.EventManager.OnRemovePresence += EventManager_OnRemovePresence;
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "TextureDownloadModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
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

                    m_userTextureServices.Remove(agentId);
                }
            }
        }

        public void NewClient(IClientAPI client)
        {
            client.OnRequestTexture += TextureRequest;
        }

        /// <summary>
        /// Does this user have a registered texture download service?
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="textureService"></param>
        /// <returns>Always returns true, since a service is created if one does not already exist</returns>
        private bool TryGetUserTextureService(
            IClientAPI client, out UserTextureDownloadService textureService)
        {
            lock (m_userTextureServices)
            {
                if (m_userTextureServices.TryGetValue(client.AgentId, out textureService))
                {
                    return true;
                }

                textureService = new UserTextureDownloadService(client, m_scene, m_queueSenders);
                m_userTextureServices.Add(client.AgentId, textureService);

                return true;
            }
        }

        /// <summary>
        /// Start the process of requesting a given texture.
        /// </summary>
        /// <param name="sender"> </param>
        /// <param name="e"></param>
        public void TextureRequest(Object sender, TextureRequestArgs e)
        {
            IClientAPI client = (IClientAPI) sender;
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

        /// <summary>
        /// Called when the texture has finished sending.
        /// </summary>
        /// <param name="sender"></param>
        private void TextureSent(ITextureSender sender)
        {
            sender.Sending = false;
            //m_log.DebugFormat("[TEXTURE]: Removing download stat for {0}", sender.assetID);
            m_scene.AddPendingDownloads(-1);
        }
    }
}
