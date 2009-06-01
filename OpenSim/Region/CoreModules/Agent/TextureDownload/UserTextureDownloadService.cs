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
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Limit;
using OpenSim.Framework.Statistics;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Agent.TextureDownload
{
    /// <summary>
    /// This module sets up texture senders in response to client texture requests, and places them on a
    /// processing queue once those senders have the appropriate data (i.e. a texture retrieved from the
    /// asset cache).
    /// </summary>
    public class UserTextureDownloadService
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// True if the service has been closed, probably because a user with texture requests still queued
        /// logged out.
        /// </summary>
        private bool closed;

        /// <summary>
        /// We will allow the client to request the same texture n times before dropping further requests
        ///
        /// This number includes repeated requests for the same texture at different resolutions (which we don't
        /// currently handle properly as far as I know).  However, this situation should be handled in a more
        /// sophisticated way.
        /// </summary>
        private static readonly int MAX_ALLOWED_TEXTURE_REQUESTS = 5;

        /// <summary>
        /// XXX Also going to limit requests for found textures.
        /// </summary>
        private readonly IRequestLimitStrategy<UUID> foundTextureLimitStrategy
            = new RepeatLimitStrategy<UUID>(MAX_ALLOWED_TEXTURE_REQUESTS);

        private readonly IClientAPI m_client;
        private readonly Scene m_scene;

        /// <summary>
        /// Texture Senders are placed in this queue once they have received their texture from the asset
        /// cache.  Another module actually invokes the send.
        /// </summary>
        private readonly OpenSim.Framework.BlockingQueue<ITextureSender> m_sharedSendersQueue;

        /// <summary>
        /// Holds texture senders before they have received the appropriate texture from the asset cache.
        /// </summary>
        private readonly Dictionary<UUID, TextureSender.TextureSender> m_textureSenders = new Dictionary<UUID, TextureSender.TextureSender>();

        /// <summary>
        /// We're going to limit requests for the same missing texture.
        /// XXX This is really a temporary solution to deal with the situation where a client continually requests
        /// the same missing textures
        /// </summary>
        private readonly IRequestLimitStrategy<UUID> missingTextureLimitStrategy
            = new RepeatLimitStrategy<UUID>(MAX_ALLOWED_TEXTURE_REQUESTS);

        public UserTextureDownloadService(
            IClientAPI client, Scene scene, OpenSim.Framework.BlockingQueue<ITextureSender> sharedQueue)
        {
            m_client = client;
            m_scene = scene;
            m_sharedSendersQueue = sharedQueue;
        }

        /// <summary>
        /// Handle a texture request.  This involves creating a texture sender and placing it on the
        /// previously passed in shared queue.
        /// </summary>
        /// <param name="e"></param>
        public void HandleTextureRequest(TextureRequestArgs e)
        {
            TextureSender.TextureSender textureSender;

            //TODO: should be working out the data size/ number of packets to be sent for each discard level
            if ((e.DiscardLevel >= 0) || (e.Priority != 0))
            {
                lock (m_textureSenders)
                {
                    if (m_textureSenders.TryGetValue(e.RequestedAssetID, out textureSender))
                    {
                        // If we've received new non UUID information for this request and it hasn't dispatched
                        // yet, then update the request accordingly.
                        textureSender.UpdateRequest(e.DiscardLevel, e.PacketNumber);
                    }
                    else
                    {
                        //                        m_log.DebugFormat("[TEXTURE]: Received a request for texture {0}", e.RequestedAssetID);

                        if (!foundTextureLimitStrategy.AllowRequest(e.RequestedAssetID))
                        {
                            //                            m_log.DebugFormat(
                            //                                "[TEXTURE]: Refusing request for {0} from client {1}",
                            //                                e.RequestedAssetID, m_client.AgentId);

                            return;
                        }
                        else if (!missingTextureLimitStrategy.AllowRequest(e.RequestedAssetID))
                        {
                            if (missingTextureLimitStrategy.IsFirstRefusal(e.RequestedAssetID))
                            {
                                if (StatsManager.SimExtraStats != null)
                                    StatsManager.SimExtraStats.AddBlockedMissingTextureRequest();

                                // Commenting out this message for now as it causes too much noise with other
                                // debug messages.
                                //                                m_log.DebugFormat(
                                //                                    "[TEXTURE]: Dropping requests for notified missing texture {0} for client {1} since we have received more than {2} requests",
                                //                                    e.RequestedAssetID, m_client.AgentId, MAX_ALLOWED_TEXTURE_REQUESTS);
                            }

                            return;
                        }

                        m_scene.StatsReporter.AddPendingDownloads(1);

                        TextureSender.TextureSender requestHandler = new TextureSender.TextureSender(m_client, e.DiscardLevel, e.PacketNumber);
                        m_textureSenders.Add(e.RequestedAssetID, requestHandler);

                        m_scene.AssetService.Get(e.RequestedAssetID.ToString(), this, TextureReceived);
                    }
                }
            }
            else
            {
                lock (m_textureSenders)
                {
                    if (m_textureSenders.TryGetValue(e.RequestedAssetID, out textureSender))
                    {
                        textureSender.Cancel = true;
                    }
                }
            }
        }

        protected void TextureReceived(string id, Object sender, AssetBase asset)
        {
            if (asset != null)
                TextureCallback(asset.FullID, asset);
        }

        /// <summary>
        /// The callback for the asset cache when a texture has been retrieved.  This method queues the
        /// texture sender for processing.
        /// </summary>
        /// <param name="textureID"></param>
        /// <param name="texture"></param>
        public void TextureCallback(UUID textureID, AssetBase texture)
        {
            //m_log.DebugFormat("[USER TEXTURE DOWNLOAD SERVICE]: Calling TextureCallback with {0}, texture == null is {1}", textureID, (texture == null ? true : false));

            // There may still be texture requests pending for a logged out client
            if (closed)
                return;

            lock (m_textureSenders)
            {
                TextureSender.TextureSender textureSender;
                if (m_textureSenders.TryGetValue(textureID, out textureSender))
                {
                    // XXX It may be perfectly valid for a texture to have no data...  but if we pass
                    // this on to the TextureSender it will blow up, so just discard for now.
                    // Needs investigation.
                    if (texture == null || texture.Data == null)
                    {
                        if (!missingTextureLimitStrategy.IsMonitoringRequests(textureID))
                        {
                            missingTextureLimitStrategy.MonitorRequests(textureID);

                            //                            m_log.DebugFormat(
                            //                                "[TEXTURE]: Queueing first TextureNotFoundSender for {0}, client {1}",
                            //                                textureID, m_client.AgentId);
                        }

                        ITextureSender textureNotFoundSender = new TextureNotFoundSender(m_client, textureID);
                        EnqueueTextureSender(textureNotFoundSender);
                    }
                    else
                    {
                        if (!textureSender.ImageLoaded)
                        {
                            textureSender.TextureReceived(texture);
                            EnqueueTextureSender(textureSender);

                            foundTextureLimitStrategy.MonitorRequests(textureID);
                        }
                    }

                    //m_log.InfoFormat("[TEXTURE] Removing texture sender with uuid {0}", textureID);
                    m_textureSenders.Remove(textureID);
                    //m_log.InfoFormat("[TEXTURE] Current texture senders in dictionary: {0}", m_textureSenders.Count);
                }
                else
                {
                    m_log.WarnFormat(
                        "[TEXTURE]: Got a texture uuid {0} with no sender object to handle it, this shouldn't happen",
                        textureID);
                }
            }
        }

        /// <summary>
        /// Place a ready texture sender on the processing queue.
        /// </summary>
        /// <param name="textureSender"></param>
        private void EnqueueTextureSender(ITextureSender textureSender)
        {
            textureSender.Cancel = false;
            textureSender.Sending = true;

            if (!m_sharedSendersQueue.Contains(textureSender))
            {
                m_sharedSendersQueue.Enqueue(textureSender);
            }
        }

        /// <summary>
        /// Close this module.
        /// </summary>
        internal void Close()
        {
            closed = true;

            lock (m_textureSenders)
            {
                foreach (TextureSender.TextureSender textureSender in m_textureSenders.Values)
                {
                    textureSender.Cancel = true;
                }

                m_textureSenders.Clear();
            }

            // XXX: It might be possible to also remove pending texture requests from the asset cache queues,
            // though this might also be more trouble than it's worth.
        }
    }
}
