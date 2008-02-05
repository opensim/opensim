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
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;

namespace OpenSim.Region.Environment.Modules
{
    public class TextureSender
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public int counter = 0;
        private AssetBase m_asset;
        public long DataPointer = 0;
        public int NumPackets = 0;
        public int PacketCounter = 0;
        public bool Cancel = false;
        public bool ImageLoaded = false;

        public bool Sending = false;

        public IClientAPI RequestUser;
        public LLUUID RequestedAssetID;
        public int RequestedDiscardLevel = -1;
        public uint StartPacketNumber = 0;

        // private int m_sentDiscardLevel = -1;

        public TextureSender(IClientAPI client, LLUUID textureID, int discardLevel, uint packetNumber)
        {
            RequestUser = client;
            RequestedAssetID = textureID;
            RequestedDiscardLevel = discardLevel;
            StartPacketNumber = packetNumber;
        }

        public void TextureReceived(AssetBase asset)
        {
            m_asset = asset;
            NumPackets = CalculateNumPackets(asset.Data.Length);
            PacketCounter = (int) StartPacketNumber;
            ImageLoaded = true;
        }

        public void UpdateRequest(int discardLevel, uint packetNumber)
        {
            RequestedDiscardLevel = discardLevel;
            StartPacketNumber = packetNumber;
            PacketCounter = (int) StartPacketNumber;
        }

        public bool SendTexturePacket()
        {
            SendPacket();
            counter++;
            if ((NumPackets == 0) || (RequestedDiscardLevel == -1) || (PacketCounter > NumPackets) ||
                ((RequestedDiscardLevel > 0) && (counter > 50 + (NumPackets/(RequestedDiscardLevel + 1)))))
            {
                return true;
            }
            return false;
        }

        private void SendPacket()
        {
            if (PacketCounter <= NumPackets)
            {
                if (PacketCounter == 0)
                {
                    if (NumPackets == 0)
                    {
                        ImageDataPacket im = new ImageDataPacket();
                        im.Header.Reliable = false;
                        im.ImageID.Packets = 1;
                        im.ImageID.ID = m_asset.FullID;
                        im.ImageID.Size = (uint) m_asset.Data.Length;
                        im.ImageData.Data = m_asset.Data;
                        im.ImageID.Codec = 2;
                        RequestUser.OutPacket(im, ThrottleOutPacketType.Texture);
                        PacketCounter++;
                    }
                    else
                    {
                        ImageDataPacket im = new ImageDataPacket();
                        im.Header.Reliable = false;
                        im.ImageID.Packets = (ushort) (NumPackets);
                        im.ImageID.ID = m_asset.FullID;
                        im.ImageID.Size = (uint) m_asset.Data.Length;
                        im.ImageData.Data = new byte[600];
                        Array.Copy(m_asset.Data, 0, im.ImageData.Data, 0, 600);
                        im.ImageID.Codec = 2;
                        RequestUser.OutPacket(im, ThrottleOutPacketType.Texture);
                        PacketCounter++;
                    }
                }
                else
                {
                    ImagePacketPacket im = new ImagePacketPacket();
                    im.Header.Reliable = false;
                    im.ImageID.Packet = (ushort) (PacketCounter);
                    im.ImageID.ID = m_asset.FullID;
                    int size = m_asset.Data.Length - 600 - (1000*(PacketCounter - 1));
                    if (size > 1000) size = 1000;
                    im.ImageData.Data = new byte[size];
                    try
                    {
                        Array.Copy(m_asset.Data, 600 + (1000*(PacketCounter - 1)), im.ImageData.Data, 0, size);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        m_log.Error("[TEXTURE]: Unable to separate texture into multiple packets: Array bounds failure on asset:" +
                                    m_asset.FullID.ToString() );
                        return;
                    }
                    RequestUser.OutPacket(im, ThrottleOutPacketType.Texture);
                    PacketCounter++;
                }
            }
        }

        private int CalculateNumPackets(int length)
        {
            int numPackets = 0;

            if (length > 600)
            {
                //over 600 bytes so split up file
                int restData = (length - 600);
                int restPackets = ((restData + 999)/1000);
                numPackets = restPackets;
            }

            return numPackets;
        }
    }
}
