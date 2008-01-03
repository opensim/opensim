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
                }
                else
                {
                    throw new Exception("Got a texture with no sender object to handle it, this shouldn't happen");
                }
            }
        }

        private void EnqueueTextureSender(TextureSender textureSender)
        {
            MainLog.Instance.Debug( "TEXTUREDOWNLOAD", "Start: ["+textureSender.RequestedAssetID+"] to ["+textureSender.RequestUser.Name+"]");

            textureSender.Cancel = false;
            textureSender.Sending = true;
            textureSender.counter = 0;

            if (!m_sharedSendersQueue.Contains(textureSender))
            {
                m_sharedSendersQueue.Enqueue(textureSender);
            }
        }
    }
}