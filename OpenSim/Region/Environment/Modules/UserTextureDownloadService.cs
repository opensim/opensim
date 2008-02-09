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
* 
*/

using System;
using System.Collections.Generic;
using libsecondlife;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class UserTextureDownloadService
    {
        private static readonly log4net.ILog m_log 
            = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        
        private readonly Dictionary<LLUUID, TextureSender> m_textureSenders = new Dictionary<LLUUID, TextureSender>();
        private readonly BlockingQueue<TextureSender> m_sharedSendersQueue;
        private readonly Scene m_scene;

        public UserTextureDownloadService(Scene scene, BlockingQueue<TextureSender> sharedQueue)
        {
            m_scene = scene;
            m_sharedSendersQueue = sharedQueue;
        }

        public void HandleTextureRequest(IClientAPI client, TextureRequestArgs e)
        {
            TextureSender textureSender;

            //TODO: should be working out the data size/ number of packets to be sent for each discard level
            if ((e.DiscardLevel >= 0) || (e.Priority != 0))
            {
                lock (m_textureSenders)
                {
                    if (m_textureSenders.TryGetValue(e.RequestedAssetID, out textureSender))
                    {
                        textureSender.UpdateRequest(e.DiscardLevel, e.PacketNumber);

                        if ((textureSender.ImageLoaded) &&
                            (textureSender.Sending == false))
                        {
                            EnqueueTextureSender(textureSender);
                        }
                    }
                    else
                    {
                        TextureSender requestHandler =
                            new TextureSender(client, e.RequestedAssetID, e.DiscardLevel, e.PacketNumber);
                        m_textureSenders.Add(e.RequestedAssetID, requestHandler);
                        m_scene.AssetCache.GetAsset(e.RequestedAssetID, TextureCallback);
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

        public void TextureCallback(LLUUID textureID, AssetBase asset)
        {
            lock (m_textureSenders)
            {
                TextureSender textureSender;

                if (m_textureSenders.TryGetValue(textureID, out textureSender))
                {
                    if (!textureSender.ImageLoaded)
                    {
                        textureSender.TextureReceived(asset);
                        EnqueueTextureSender(textureSender);
                    }
                    
                    //m_log.Info(String.Format("[TEXTURE SENDER] Removing texture sender with uuid {0}", textureID));
                    m_textureSenders.Remove(textureID);                    
                    //m_log.Info(String.Format("[TEXTURE SENDER] Current texture senders in dictionary: {0}", m_textureSenders.Count));
                }
                else
                {
                    throw new Exception("Got a texture with no sender object to handle it, this shouldn't happen");
                }
            }
        }

        private void EnqueueTextureSender(TextureSender textureSender)
        {
            textureSender.Cancel = false;
            textureSender.Sending = true;
            textureSender.counter = 0;

            if (!m_sharedSendersQueue.Contains(textureSender))
            {
                m_sharedSendersQueue.Enqueue(textureSender);
            }
        }

        internal void Close()
        {
            lock (m_textureSenders)
            {
                foreach( TextureSender textureSender in m_textureSenders.Values )
                {
                    textureSender.Cancel = true;
                }

                m_textureSenders.Clear();
            }
        }
    }
}
