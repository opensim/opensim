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
using System.Reflection;
using OpenMetaverse.Packets;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Modules.Agent.TextureSender
{
    public class ImageDownload
    {
        public const int FIRST_IMAGE_PACKET_SIZE = 600;
        public const int IMAGE_PACKET_SIZE = 1000;

        public OpenMetaverse.AssetTexture Texture;
        public int DiscardLevel;
        public float Priority;
        public int CurrentPacket;
        public int StopPacket;

        public ImageDownload(OpenMetaverse.AssetTexture texture, int discardLevel, float priority, int packet)
        {
            Texture = texture;
            Update(discardLevel, priority, packet);
        }

        /// <summary>
        /// Updates an image transfer with new information and recalculates
        /// offsets
        /// </summary>
        /// <param name="discardLevel">New requested discard level</param>
        /// <param name="priority">New requested priority</param>
        /// <param name="packet">New requested packet offset</param>
        public void Update(int discardLevel, float priority, int packet)
        {
            Priority = priority;
            DiscardLevel = Clamp(discardLevel, 0, Texture.LayerInfo.Length - 1);
            StopPacket = GetPacketForBytePosition(Texture.LayerInfo[(Texture.LayerInfo.Length - 1) - DiscardLevel].End);
            CurrentPacket = Clamp(packet, 1, TexturePacketCount());
        }

        /// <summary>
        /// Returns the total number of packets needed to transfer this texture,
        /// including the first packet of size FIRST_IMAGE_PACKET_SIZE
        /// </summary>
        /// <returns>Total number of packets needed to transfer this texture</returns>
        public int TexturePacketCount()
        {
            return ((Texture.AssetData.Length - FIRST_IMAGE_PACKET_SIZE + IMAGE_PACKET_SIZE - 1) / IMAGE_PACKET_SIZE) + 1;
        }

        /// <summary>
        /// Returns the current byte offset for this transfer, calculated from
        /// the CurrentPacket
        /// </summary>
        /// <returns>Current byte offset for this transfer</returns>
        public int CurrentBytePosition()
        {
            return FIRST_IMAGE_PACKET_SIZE + (CurrentPacket - 1) * IMAGE_PACKET_SIZE;
        }

        /// <summary>
        /// Returns the size, in bytes, of the last packet. This will be somewhere
        /// between 1 and IMAGE_PACKET_SIZE bytes
        /// </summary>
        /// <returns>Size of the last packet in the transfer</returns>
        public int LastPacketSize()
        {
            return Texture.AssetData.Length - (FIRST_IMAGE_PACKET_SIZE + ((TexturePacketCount() - 2) * IMAGE_PACKET_SIZE));
        }

        /// <summary>
        /// Find the packet number that contains a given byte position
        /// </summary>
        /// <param name="bytePosition">Byte position</param>
        /// <returns>Packet number that contains the given byte position</returns>
        int GetPacketForBytePosition(int bytePosition)
        {
            return ((bytePosition - FIRST_IMAGE_PACKET_SIZE + IMAGE_PACKET_SIZE - 1) / IMAGE_PACKET_SIZE);
        }

        /// <summary>
        /// Clamp a given value between a range
        /// </summary>
        /// <param name="value">Value to clamp</param>
        /// <param name="min">Minimum allowable value</param>
        /// <param name="max">Maximum allowable value</param>
        /// <returns>A value inclusively between lower and upper</returns>
        static int Clamp(int value, int min, int max)
        {
            // First we check to see if we're greater than the max
            value = (value > max) ? max : value;

            // Then we check to see if we're less than the min.
            value = (value < min) ? min : value;

            // There's no check to see if min > max.
            return value;
        }
    }

    /// <summary>
    /// A TextureSender handles the process of receiving a texture requested by the client from the
    /// AssetCache, and then sending that texture back to the client.
    /// </summary>
    public class TextureSender : ITextureSender
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public bool ImageLoaded = false;

        /// <summary>
        /// Holds the texture asset to send.
        /// </summary>
        private AssetBase m_asset;
        private bool m_cancel = false;
        private bool m_sending = false;
        private bool sendFirstPacket = false;
        private int initialDiscardLevel = 0;
        private int initialPacketNum = 0;

        private ImageDownload download;
        private IClientAPI RequestUser;

        public TextureSender(IClientAPI client, int discardLevel, uint packetNumber)
        {
            RequestUser = client;
            initialDiscardLevel = discardLevel;
            initialPacketNum = (int)packetNumber;
        }

        #region ITextureSender Members

        public bool Cancel
        {
            get { return m_cancel; }
            set { m_cancel = value; }
        }

        public bool Sending
        {
            get { return m_sending; }
            set { m_sending = value; }
        }

        // See ITextureSender
        public void UpdateRequest(int discardLevel, uint packetNumber)
        {
            lock (download)
            {
                if (discardLevel < download.DiscardLevel)
                    m_log.DebugFormat("Image download {0} is changing from DiscardLevel {1} to {2}",
                        m_asset.FullID, download.DiscardLevel, discardLevel);

                if (packetNumber != download.CurrentPacket)
                    m_log.DebugFormat("Image download {0} is changing from Packet {1} to {2}",
                        m_asset.FullID, download.CurrentPacket, packetNumber);

                download.Update(discardLevel, download.Priority, (int)packetNumber);

                sendFirstPacket = true;
            }
        }

        // See ITextureSender
        public bool SendTexturePacket()
        {
            if (!m_cancel && download.CurrentPacket <= download.StopPacket)
            {
                SendPacket();
                return false;
            }
            else
            {
                m_sending = false;
                m_cancel = true;
                sendFirstPacket = false;
                return true;
            }
        }

        #endregion

        /// <summary>
        /// Load up the texture data to send.
        /// </summary>
        /// <param name="asset">
        /// A <see cref="AssetBase"/>
        /// </param>
        public void TextureReceived(AssetBase asset)
        {
            m_asset = asset;

            try
            {
                OpenMetaverse.AssetTexture texture = new OpenMetaverse.AssetTexture(m_asset.FullID, m_asset.Data);
                if (texture.DecodeLayerBoundaries())
                {
                    download = new ImageDownload(texture, initialDiscardLevel, 0.0f, initialPacketNum);
                    ImageLoaded = true;
                    m_sending = true;
                    m_cancel = false;
                    sendFirstPacket = true;

                    return;
                }
                else
                {
                    m_log.Error("JPEG2000 texture decoding failed");
                }
            }
            catch (Exception ex)
            {
                m_log.Error("JPEG2000 texture decoding threw an exception", ex);
            }

            ImageLoaded = false;
            m_sending = false;
            m_cancel = true;
        }

        /// <summary>
        /// Sends a texture packet to the client.
        /// </summary>
        private void SendPacket()
        {
            lock (download)
            {
                if (sendFirstPacket)
                {
                    sendFirstPacket = false;

                    if (m_asset.Data.Length <= 600)
                    {
                        RequestUser.SendImageFirstPart(1, m_asset.FullID, (uint)m_asset.Data.Length, m_asset.Data, 2);
                        return;
                    }
                    else
                    {
                        byte[] firstImageData = new byte[600];
                        Buffer.BlockCopy(m_asset.Data, 0, firstImageData, 0, 600);
                        RequestUser.SendImageFirstPart((ushort)download.TexturePacketCount(), m_asset.FullID, (uint)m_asset.Data.Length, firstImageData, 2);
                    }
                }

                int imagePacketSize = (download.CurrentPacket == download.TexturePacketCount() - 1) ?
                    download.LastPacketSize() : ImageDownload.IMAGE_PACKET_SIZE;

                byte[] imageData = new byte[imagePacketSize];
                Buffer.BlockCopy(m_asset.Data, download.CurrentBytePosition(), imageData, 0, imagePacketSize);

                RequestUser.SendImageNextPart((ushort)download.CurrentPacket, m_asset.FullID, imageData);
                ++download.CurrentPacket;
            }
        }
    }
}
