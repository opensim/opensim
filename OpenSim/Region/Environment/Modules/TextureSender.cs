using System;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;

namespace OpenSim.Region.Environment.Modules
{
    public class TextureSender
    {
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
                        MainLog.Instance.Error("TEXTURE",
                                               "Unable to separate texture into multiple packets: Array bounds failure on asset:" +
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