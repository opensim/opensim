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
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using log4net;
using System.Reflection;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    /// <summary>
    /// Stores information about a current texture download and a reference to the texture asset
    /// </summary>
    public class J2KImage
    {
        private const int IMAGE_PACKET_SIZE = 1000;
        private const int FIRST_PACKET_SIZE = 600;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public uint m_lastSequence;
        public float m_requestedPriority;
        public uint m_requestedPacketNumber;
        public sbyte m_requestedDiscardLevel;
        public UUID m_requestedUUID;
        public IJ2KDecoder m_j2kDecodeModule;
        public IAssetService m_assetCache;
        public OpenJPEG.J2KLayerInfo[] m_layers;
        public bool m_decoded;
        public bool m_hasasset;
        public C5.IPriorityQueueHandle<J2KImage> m_priorityQueueHandle;

        private uint m_packetNumber;
        private bool m_decoderequested;
        private bool m_asset_requested;
        private bool m_sentinfo;
        private uint m_stopPacket;
        private AssetBase m_asset;
        private int m_assetDataLength;
        private LLImageManager m_imageManager;

        #region Properties

        public uint m_pPacketNumber
        {
            get { return m_packetNumber; }
        }
        public uint m_pStopPacketNumber
        {
            get { return m_stopPacket; }
        }

        public byte[] Data
        {
            get
            {
                if (m_asset != null)
                    return m_asset.Data;
                else
                    return null;
            }
        }

        public ushort TexturePacketCount()
        {
            if (!m_decoded)
                return 0;

            try
            {
                return (ushort)(((m_assetDataLength - FIRST_PACKET_SIZE + IMAGE_PACKET_SIZE - 1) / IMAGE_PACKET_SIZE) + 1);
            }
            catch (Exception)
            {
                // If the asset is missing/destroyed/truncated, we will land
                // here
                //
                return 0;
            }
        }

        #endregion Properties

        public J2KImage(LLImageManager imageManager)
        {
            m_imageManager = imageManager;
        }

        public bool SendPackets(LLClientView client, int maxpack)
        {
            if (m_packetNumber <= m_stopPacket)
            {
                bool SendMore = true;
                if (!m_sentinfo || (m_packetNumber == 0))
                {
                    if (SendFirstPacket(client))
                    {
                        SendMore = false;
                    }
                    m_sentinfo = true;
                    m_packetNumber++;
                }
                // bool ignoreStop = false;
                if (m_packetNumber < 2)
                {
                    m_packetNumber = 2;
                }

                int count = 0;
                while (SendMore && count < maxpack && m_packetNumber <= m_stopPacket)
                {
                    count++;
                    SendMore = SendPacket(client);
                    m_packetNumber++;
                }

                if (m_packetNumber > m_stopPacket)
                    return true;
            }

            return false;
        }

        public void RunUpdate()
        {
            //This is where we decide what we need to update
            //and assign the real discardLevel and packetNumber
            //assuming of course that the connected client might be bonkers

            if (!m_hasasset)
            {
                if (!m_asset_requested)
                {
                    m_asset_requested = true;
                    m_assetCache.Get(m_requestedUUID.ToString(), this, AssetReceived);
                }
            }
            else
            {
                if (!m_decoded)
                {
                    //We need to decode the requested image first
                    if (!m_decoderequested)
                    {
                        //Request decode
                        m_decoderequested = true;
                        // Do we have a jpeg decoder?
                        if (m_j2kDecodeModule != null)
                        {
                            if (Data == null)
                            {
                                J2KDecodedCallback(m_requestedUUID, new OpenJPEG.J2KLayerInfo[0]);
                            }
                            else
                            {
                                // Send it off to the jpeg decoder
                                m_j2kDecodeModule.BeginDecode(m_requestedUUID, Data, J2KDecodedCallback);
                            }

                        }
                        else
                        {
                            J2KDecodedCallback(m_requestedUUID, new OpenJPEG.J2KLayerInfo[0]);
                        }
                    }
                }
                else
                {
                    // Check for missing image asset data
                    if (m_asset == null || m_asset.Data == null)
                    {
                        // FIXME:
                        m_packetNumber = m_stopPacket;
                        return;
                    }

                    if (m_requestedDiscardLevel >= 0 || m_stopPacket == 0)
                    {
                        int maxDiscardLevel = Math.Max(0, m_layers.Length - 1);

                        // Treat initial texture downloads with a DiscardLevel of -1 a request for the highest DiscardLevel
                        if (m_requestedDiscardLevel < 0 && m_stopPacket == 0)
                            m_requestedDiscardLevel = (sbyte)maxDiscardLevel;

                        // Clamp at the highest discard level
                        m_requestedDiscardLevel = (sbyte)Math.Min(m_requestedDiscardLevel, maxDiscardLevel);

                        //Calculate the m_stopPacket
                        if (m_layers.Length > 0)
                        {
                            m_stopPacket = (uint)GetPacketForBytePosition(m_layers[(m_layers.Length - 1) - m_requestedDiscardLevel].End);
                            //I don't know why, but the viewer seems to expect the final packet if the file
                            //is just one packet bigger.
                            if (TexturePacketCount() == m_stopPacket + 1)
                            {
                                m_stopPacket = TexturePacketCount();
                            }
                        }
                        else
                        {
                            m_stopPacket = TexturePacketCount();
                        }

                        m_packetNumber = m_requestedPacketNumber;
                    }
                }
            }
        }

        private bool SendPacket(LLClientView client)
        {
            bool complete = false;
            int imagePacketSize = ((int)m_packetNumber == (TexturePacketCount())) ? LastPacketSize() : IMAGE_PACKET_SIZE;

            try
            {
                if ((CurrentBytePosition() + IMAGE_PACKET_SIZE) > m_assetDataLength)
                {
                    imagePacketSize = LastPacketSize();
                    complete = true;
                    if ((CurrentBytePosition() + imagePacketSize) > m_assetDataLength)
                    {
                        imagePacketSize = m_assetDataLength - CurrentBytePosition();
                        complete = true;
                    }
                }

                // It's concievable that the client might request packet one
                // from a one packet image, which is really packet 0,
                // which would leave us with a negative imagePacketSize..
                if (imagePacketSize > 0)
                {
                    byte[] imageData = new byte[imagePacketSize];
                    try
                    {
                        Buffer.BlockCopy(m_asset.Data, CurrentBytePosition(), imageData, 0, imagePacketSize);
                    }
                    catch (Exception e)
                    {
                        m_log.Error("Error copying texture block. Out of memory? imagePacketSize was " + imagePacketSize.ToString() + " on packet " + m_packetNumber.ToString() + " out of " + m_stopPacket.ToString() + ". Exception: " + e.ToString());
                        return false;
                    }

                    //Send the packet
                    client.SendImageNextPart((ushort)(m_packetNumber - 1), m_requestedUUID, imageData);
                }
                if (complete)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private int GetPacketForBytePosition(int bytePosition)
        {
            return ((bytePosition - FIRST_PACKET_SIZE + IMAGE_PACKET_SIZE - 1) / IMAGE_PACKET_SIZE) + 1;
        }

        private int LastPacketSize()
        {
            if (m_packetNumber == 1)
                return m_assetDataLength;
            int lastsize = (m_assetDataLength - FIRST_PACKET_SIZE) % IMAGE_PACKET_SIZE;
            //If the last packet size is zero, it's really cImagePacketSize, it sits on the boundary
            if (lastsize == 0)
            {
                lastsize = IMAGE_PACKET_SIZE;
            }
            return lastsize;
        }

        private int CurrentBytePosition()
        {
            if (m_packetNumber == 0)
                return 0;
            if (m_packetNumber == 1)
                return FIRST_PACKET_SIZE;

            int result = FIRST_PACKET_SIZE + ((int)m_packetNumber - 2) * IMAGE_PACKET_SIZE;
            if (result < 0)
            {
                result = FIRST_PACKET_SIZE;
            }
            return result;
        }

        private bool SendFirstPacket(LLClientView client)
        {
            // this means we don't have 
            if (Data == null)
            {
                client.SendImageNotFound(m_requestedUUID);
                m_log.WarnFormat("[TEXTURE]: Got null Data element on a asset {0}..  and the missing image Data property is al", m_requestedUUID);
                return true;
            }
            // Do we have less then 1 packet's worth of data?
            else if (m_assetDataLength <= FIRST_PACKET_SIZE)
            {
                // Send only 1 packet
                client.SendImageFirstPart(1, m_requestedUUID, (uint)m_assetDataLength, m_asset.Data, 2);
                m_stopPacket = 0;
                return true;
            }
            else
            {
                byte[] firstImageData = new byte[FIRST_PACKET_SIZE];
                try
                {
                    Buffer.BlockCopy(m_asset.Data, 0, firstImageData, 0, (int)FIRST_PACKET_SIZE);
                    client.SendImageFirstPart(TexturePacketCount(), m_requestedUUID, (uint)m_assetDataLength, firstImageData, 2);
                }
                catch (Exception)
                {
                    m_log.Error("Texture block copy failed. Possibly out of memory?");
                    return true;
                }
            }
            return false;
        }

        private void J2KDecodedCallback(UUID AssetId, OpenJPEG.J2KLayerInfo[] layers)
        {
            m_layers = layers;
            m_decoded = true;
            RunUpdate();
        }

        private void AssetDataCallback(UUID AssetID, AssetBase asset)
        {
            m_hasasset = true;

            if (asset == null || asset.Data == null)
            {
                m_asset = null;
                m_decoded = true;
            }
            else
            {
                m_asset = asset;
                m_assetDataLength = m_asset.Data.Length;
            }

            RunUpdate();
        }

        private void AssetReceived(string id, Object sender, AssetBase asset)
        {
            UUID assetID = UUID.Zero;
            if (asset != null)
                assetID = asset.FullID;

            AssetDataCallback(assetID, asset);

        }
    }
}
