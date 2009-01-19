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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using C5;
using OpenSim.Framework.Communications.Cache;
using OpenMetaverse.Imaging;


namespace OpenSim.Region.ClientStack.LindenUDP
{

    /// <summary>
    /// Client image priority + discardlevel sender/manager
    /// </summary>
    public class LLImageManager
    {
        /// <summary>
        /// Priority Queue for images.  Contains lots of data
        /// </summary>
        private readonly IPriorityQueue<Prio<J2KImage>> pq = new IntervalHeap<Prio<J2KImage>>();
        
        /// <summary>
        /// Dictionary of PriorityQueue handles by AssetId
        /// </summary>
        private readonly Dictionary<UUID, IPriorityQueueHandle<Prio<J2KImage>>> PQHandles =
            new Dictionary<UUID, IPriorityQueueHandle<Prio<J2KImage>>>();

        private LLClientView m_client;
        private readonly AssetCache m_assetCache;
        private bool m_shuttingdown = false;
        private readonly IJ2KDecoder m_j2kDecodeModule;

        private readonly AssetBase MissingSubstitute;

        /// <summary>
        /// Client image priority + discardlevel sender/manager
        /// </summary>
        /// <param name="client">LLClientView of client</param>
        /// <param name="pAssetCache">The Asset retrieval system</param>
        /// <param name="pJ2kDecodeModule">The Jpeg2000 Decoder</param>
        public LLImageManager(LLClientView client, AssetCache pAssetCache, IJ2KDecoder pJ2kDecodeModule)
        {
            m_client = client;
            m_assetCache = pAssetCache;
            if (pAssetCache != null)
                MissingSubstitute = pAssetCache.GetAsset(UUID.Parse("5748decc-f629-461c-9a36-a35a221fe21f"), true);
            m_j2kDecodeModule = pJ2kDecodeModule;
        }

        /// <summary>
        /// Enqueues a texture request
        /// </summary>
        /// <param name="req">Request from the client to get a texture</param>
        public void EnqueueReq(TextureRequestArgs req)
        {
            if (m_shuttingdown)
                return;

            //if (req.RequestType == 1) // avatar body texture!
            //    return;
            
            AddQueueItem(req.RequestedAssetID, (int)req.Priority + 100000);
            //if (pq[PQHandles[req.RequestedAssetID]].data.Missing)
            //{
            //    pq[PQHandles[req.RequestedAssetID]] -= 900000;
            //}
            //
            //if (pq[PQHandles[req.RequestedAssetID]].data.HasData && pq[PQHandles[req.RequestedAssetID]].data.Layers.Length > 0)
            //{
               
            //}
            
            pq[PQHandles[req.RequestedAssetID]].data.requestedUUID = req.RequestedAssetID;
            pq[PQHandles[req.RequestedAssetID]].data.Priority = (int)req.Priority;

            lock (pq[PQHandles[req.RequestedAssetID]].data)
            pq[PQHandles[req.RequestedAssetID]].data.Update(req.DiscardLevel, (int)req.PacketNumber);
        }


        /// <summary>
        /// Callback for the asset system
        /// </summary>
        /// <param name="assetID">UUID of the asset that we have received</param>
        /// <param name="asset">AssetBase of the asset that we've received</param>
        public void AssetDataCallback(UUID assetID, AssetBase asset)
        {
            if (m_shuttingdown)
                return;

            //Console.WriteLine("AssetCallback for assetId" + assetID);
            
            if (asset == null || asset.Data == null)
            {
                lock (pq)
                {
                    //pq[PQHandles[assetID]].data.Missing = true;
                    pq[PQHandles[assetID]].data.asset = MissingSubstitute;
                    pq[PQHandles[assetID]].data.Missing = false;
                }
            }
            //else


            pq[PQHandles[assetID]].data.asset = asset;
            
            lock (pq[PQHandles[assetID]].data)
                pq[PQHandles[assetID]].data.Update((int)pq[PQHandles[assetID]].data.Priority, (int)pq[PQHandles[assetID]].data.CurrentPacket);
            
            
            
        }

        /// <summary>
        /// Processes the image queue.  Pops count elements off and processes them
        /// </summary>
        /// <param name="count">number of images to peek off the queue</param>
        public void ProcessImageQueue(int count)
        {
            if (m_shuttingdown)
                return;


            IPriorityQueueHandle<Prio<J2KImage>> h = null;
            for (int j = 0; j < count; j++)
            {

                lock (pq)
                {
                    if (!pq.IsEmpty)
                    {
                        //peek off the top
                        Prio<J2KImage> process = pq.FindMax(out h);

                        // Do we have the Asset Data?
                        if (!process.data.HasData)
                        {
                            // Did we request the asset data?
                            if (!process.data.dataRequested)
                            {
                                m_assetCache.GetAsset(process.data.requestedUUID, AssetDataCallback, true);
                                pq[h].data.dataRequested = true;
                            }

                            // Is the asset missing?
                            if (process.data.Missing)
                            {
                                
                                    //m_client.sendtextur
                                    pq[h] -= 90000;
                                    /*
                                    {
                                        OpenMetaverse.Packets.ImageNotInDatabasePacket imdback =
                                            new OpenMetaverse.Packets.ImageNotInDatabasePacket();
                                        imdback.ImageID =
                                            new OpenMetaverse.Packets.ImageNotInDatabasePacket.ImageIDBlock();
                                        imdback.ImageID.ID = process.data.requestedUUID;
                                        m_client.OutPacket(imdback, ThrottleOutPacketType.Texture);
                                    }
                                    */

                                    // Substitute a blank image
                                    process.data.asset = MissingSubstitute;
                                    process.data.Missing = false;
                                    
                                // If the priority is less then -4billion, the client has forgotten about it.
                                if (pq[h] < -400000000)
                                {
                                    RemoveItemFromQueue(pq[h].data.requestedUUID);
                                    continue;
                                }
                            }
                            // Lower the priority to give the next image a chance
                            pq[h] -= 100000;
                        }
                        else if (process.data.HasData)
                        {
                            // okay, we've got the data
                            lock (process.data)
                            {
                                if (!process.data.J2KDecode && !process.data.J2KDecodeWaiting)
                                {
                                    process.data.J2KDecodeWaiting = true;

                                    // Do we have a jpeg decoder?
                                    if (m_j2kDecodeModule != null)
                                    {
                                        // Send it off to the jpeg decoder
                                        m_j2kDecodeModule.decode(process.data.requestedUUID, process.data.Data,
                                                                 j2kDecodedCallback);
                                    }
                                    else
                                    {
                                        // no module, no layers, full resolution only
                                        j2kDecodedCallback(process.data.AssetId, new OpenJPEG.J2KLayerInfo[0]);
                                    }

                                   

                                } // Are we waiting?
                                else if (!process.data.J2KDecodeWaiting)
                                {
                                    // Send more data at a time for higher discard levels
                                    for (int i = 0; i < (2*(5 - process.data.DiscardLevel) + 1)*2; i++)
                                        if (!process.data.SendPacket(m_client))
                                        {
                                            pq[h] -= (500000*i);
                                            break;
                                        }
                                }
                                // If the priority is less then -4 billion, the client has forgotten about it, pop it off
                                if (pq[h] < -400000000)
                                {
                                    RemoveItemFromQueue(pq[h].data.requestedUUID);
                                    continue;
                                }
                            }

                            //pq[h] = process;
                        }
                         
                        // uncomment the following line to see the upper most asset and the priority
                        //Console.WriteLine(process.ToString());
                        
                        // Lower priority to give the next image a chance to bubble up
                        pq[h] -= 50000;
                    }
                }
            }

        }


        /// <summary>
        /// Callback for when the image has been decoded
        /// </summary>
        /// <param name="AssetId">The UUID of the Asset</param>
        /// <param name="layers">The Jpeg2000 discard level Layer start and end byte offsets Array.  0 elements for failed or no decoder</param>
        public void j2kDecodedCallback(UUID AssetId, OpenJPEG.J2KLayerInfo[] layers)
        {
            // are we shutting down? if so, end.
            if (m_shuttingdown)
                return;


            lock (PQHandles)
            {
                // Update our asset data
                if (PQHandles.ContainsKey(AssetId))
                {
                    pq[PQHandles[AssetId]].data.Layers = layers;
                    pq[PQHandles[AssetId]].data.J2KDecode = true;
                    pq[PQHandles[AssetId]].data.J2KDecodeWaiting = false;
                    lock (pq[PQHandles[AssetId]].data)
                        pq[PQHandles[AssetId]].data.Update((int)pq[PQHandles[AssetId]].data.Priority, (int)pq[PQHandles[AssetId]].data.CurrentPacket);

                    // Send the first packet
                    pq[PQHandles[AssetId]].data.SendPacket(m_client);
                }
            }
        }


        /// <summary>
        /// This image has had a good life.  It's now expired.   Remove it off the queue
        /// </summary>
        /// <param name="AssetId">UUID of asset to remove off the queue</param>
        private void RemoveItemFromQueue(UUID AssetId)
        {
            lock (PQHandles)
            {
                if (PQHandles.ContainsKey(AssetId))
                {
                    IPriorityQueueHandle<Prio<J2KImage>> h = PQHandles[AssetId];
                    PQHandles.Remove(AssetId);
                    pq.Delete(h);
                }
            }
        }
        

        /// <summary>
        /// Adds an image to the queue and update priority
        /// if the item is already in the queue, just update the priority
        /// </summary>
        /// <param name="AssetId">UUID of the asset</param>
        /// <param name="priority">Priority to set</param>
        private void AddQueueItem(UUID AssetId, int priority)
        {
            IPriorityQueueHandle<Prio<J2KImage>> h = null;

            lock (PQHandles)
            {
                if (PQHandles.ContainsKey(AssetId))
                {
                    h = PQHandles[AssetId];
                    pq[h] = pq[h].SetPriority(priority);
                    
                }
                else
                {
                    J2KImage newreq = new J2KImage();
                    newreq.requestedUUID = AssetId;
                    pq.Add(ref h, new Prio<J2KImage>(newreq, priority));
                    PQHandles.Add(AssetId, h);
                }
            }
        }

        /// <summary>
        /// Okay, we're ending.   Clean up on isle 9
        /// </summary>
        public void Close()
        {
            m_shuttingdown = true;
               
            lock (pq)
            {
                while (!pq.IsEmpty)
                {
                    pq.DeleteMin();
                }
            }
            

            lock (PQHandles)
                PQHandles.Clear();
            m_client = null;
        }

    }

    /// <summary>
    /// Image Data for this send
    /// Encapsulates the image sending data and method
    /// </summary>
    public class J2KImage
    {
        private AssetBase m_asset_ref = null;
        public volatile int LastPacketNum = 0;
        public volatile int DiscardLimit = 0;
        public volatile bool dataRequested = false;
        public OpenJPEG.J2KLayerInfo[] Layers = new OpenJPEG.J2KLayerInfo[0];

        public const int FIRST_IMAGE_PACKET_SIZE = 600;
        public const int IMAGE_PACKET_SIZE = 1000;

        public volatile int DiscardLevel;
        public float Priority;
        public volatile int CurrentPacket = 1;
        public volatile int StopPacket;
        public bool Missing = false;
        public bool J2KDecode = false;
        public bool J2KDecodeWaiting = false;

        private volatile bool sendFirstPacket = true;

        // Having this *AND* the AssetId allows us to remap asset data to AssetIds as necessary.
        public UUID requestedUUID = UUID.Zero;

        public J2KImage(AssetBase asset)
        {
            m_asset_ref = asset;
        }

        public J2KImage()
        {

        }

        public AssetBase asset
        {
            set { m_asset_ref = value; }
        }

        // We make the asset a reference so that we don't duplicate the byte[]
        // it's read only anyway, so no worries here
        // we want to avoid duplicating the byte[] for the images at all costs to avoid memory bloat! :)

        /// <summary>
        /// ID of the AssetBase
        /// </summary>
        public UUID AssetId
        {
            get { return m_asset_ref.FullID; }
        }

        /// <summary>
        /// Asset Data
        /// </summary>
        public byte[] Data
        {
            get { return m_asset_ref.Data; }
        }

        /// <summary>
        /// Returns true if we have the asset
        /// </summary>
        public bool HasData
        {
            get { return !(m_asset_ref == null); }
        }

        /// <summary>
        /// Called from the PriorityQueue handle .ToString().  Prints data on this asset
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("ID:{0}, RD:{1}, CP:{2}", requestedUUID, HasData, CurrentPacket);
        }

        /// <summary>
        /// Returns the total number of packets needed to transfer this texture,
        /// including the first packet of size FIRST_IMAGE_PACKET_SIZE
        /// </summary>
        /// <returns>Total number of packets needed to transfer this texture</returns>
        public int TexturePacketCount()
        {
            if (!HasData)
                return 0;
            return ((m_asset_ref.Data.Length - FIRST_IMAGE_PACKET_SIZE + IMAGE_PACKET_SIZE - 1) / IMAGE_PACKET_SIZE) + 1;
        }

        /// <summary>
        /// Returns the current byte offset for this transfer, calculated from
        /// the CurrentPacket
        /// </summary>
        /// <returns>Current byte offset for this transfer</returns>
        public int CurrentBytePosition()
        {
            if (CurrentPacket == 0)
                return 0;
            if (CurrentPacket == 1)
                return FIRST_IMAGE_PACKET_SIZE;

            int result = FIRST_IMAGE_PACKET_SIZE + (CurrentPacket - 2) * IMAGE_PACKET_SIZE;
            if (result < 0)
            {
                result = FIRST_IMAGE_PACKET_SIZE;
            }
            return result;
        }

        /// <summary>
        /// Returns the size, in bytes, of the last packet. This will be somewhere
        /// between 1 and IMAGE_PACKET_SIZE bytes
        /// </summary>
        /// <returns>Size of the last packet in the transfer</returns>
        public int LastPacketSize()
        {
            if (CurrentPacket == 1)
                return m_asset_ref.Data.Length;
            return (m_asset_ref.Data.Length - FIRST_IMAGE_PACKET_SIZE) % IMAGE_PACKET_SIZE; // m_asset_ref.Data.Length - (FIRST_IMAGE_PACKET_SIZE + ((TexturePacketCount() - 1) * IMAGE_PACKET_SIZE));
        }

        /// <summary>
        /// Find the packet number that contains a given byte position
        /// </summary>
        /// <param name="bytePosition">Byte position</param>
        /// <returns>Packet number that contains the given byte position</returns>
        int GetPacketForBytePosition(int bytePosition)
        {
            return ((bytePosition - FIRST_IMAGE_PACKET_SIZE + IMAGE_PACKET_SIZE - 1) / IMAGE_PACKET_SIZE) + 1;
        }

        /// <summary>
        /// Updates the Image sending limits based on the discard
        /// If we don't have any Layers, Send the full texture
        /// </summary>
        /// <param name="discardLevel">jpeg2000 discard level. 5-0</param>
        /// <param name="packet">Which packet to start from</param>
        public void Update(int discardLevel, int packet)
        {
            //Requests for 0 means that the client wants us to resend the whole image
            //Requests for -1 mean 'update priority but don't change discard level'

            if (packet == 0 || packet == -1)
                return;

            // Check if we've got layers
            if (Layers.Length > 0)
            {
                DiscardLevel = Util.Clamp<int>(discardLevel, 0, Layers.Length - 1);
                StopPacket = GetPacketForBytePosition(Layers[(Layers.Length - 1) - DiscardLevel].End);
                CurrentPacket = Util.Clamp<int>(packet, 1, TexturePacketCount() - 1);
                // sendFirstPacket = true;
            }
            else
            {
                // No layers, send full image
                DiscardLevel = 0;
                StopPacket = TexturePacketCount() - 1;
                CurrentPacket = Util.Clamp<int>(packet, 1, TexturePacketCount() - 1);

            }
        }

        /// <summary>
        /// Sends a texture packet to the client.
        /// </summary>
        /// <param name="client">Client to send texture to</param>
        /// <returns>true if a packet was sent, false if not</returns>
        public bool SendPacket(LLClientView client)
        {
            // If we've hit the end of the send or if the client set -1, return false.  
            if (CurrentPacket > StopPacket || StopPacket == -1)
                return false;

            // The first packet contains up to 600 bytes and the details of the image.   Number of packets, image size in bytes, etc.
            // This packet only gets sent once unless we're restarting the transfer from 0!
            if (sendFirstPacket)
            {
                sendFirstPacket = false;

                // Do we have less then 1 packet's worth of data?
                if (m_asset_ref.Data.Length <= FIRST_IMAGE_PACKET_SIZE)
                {
                    // Send only 1 packet
                    client.SendImageFirstPart(1, requestedUUID , (uint)m_asset_ref.Data.Length, m_asset_ref.Data, 2);
                    CurrentPacket = 2; // Makes it so we don't come back to SendPacket and error trying to send a second packet
                    return true;
                }
                else
                {
                    
                    // Send first packet
                    byte[] firstImageData = new byte[FIRST_IMAGE_PACKET_SIZE];
                    try { Buffer.BlockCopy(m_asset_ref.Data, 0, firstImageData, 0, FIRST_IMAGE_PACKET_SIZE); }
                    catch (Exception)
                    {
                        Console.WriteLine(String.Format("Err: srcLen:{0}, BytePos:{1}, desLen:{2}, pktsize{3}", m_asset_ref.Data.Length, CurrentBytePosition(), firstImageData.Length, FIRST_IMAGE_PACKET_SIZE));

                        //m_log.Error("Texture data copy failed on first packet for " + m_asset_ref.FullID.ToString());
                        //m_cancel = true;
                        //m_sending = false;
                        return false;
                    }
                    client.SendImageFirstPart((ushort)TexturePacketCount(), requestedUUID, (uint)m_asset_ref.Data.Length, firstImageData, 2);
                    ++CurrentPacket; // sets CurrentPacket to 1
                }
            }

            // figure out if we're on the last packet, if so, use the last packet size.  If not, use 1000.
            // we know that the total image size is greater then 1000 if we're here
            int imagePacketSize = (CurrentPacket == (TexturePacketCount() ) ) ? LastPacketSize() : IMAGE_PACKET_SIZE;
            
            //if (imagePacketSize > 0)
            //    imagePacketSize = IMAGE_PACKET_SIZE;
            //if (imagePacketSize != 1000)
            //    Console.WriteLine("ENdPacket");
            //Console.WriteLine(String.Format("srcLen:{0}, BytePos:{1}, desLen:{2}, pktsize{3}", m_asset_ref.Data.Length, CurrentBytePosition(),0, imagePacketSize));

            bool atEnd = false;

            // edge case
            if ((CurrentBytePosition() + IMAGE_PACKET_SIZE) > m_asset_ref.Data.Length)
            {
                imagePacketSize = LastPacketSize();
                atEnd = true;
                // edge case 2!
                if ((CurrentBytePosition() + imagePacketSize) > m_asset_ref.Data.Length)
                {
                    imagePacketSize = m_asset_ref.Data.Length - CurrentBytePosition();
                    atEnd = true;
                }
            }

            byte[] imageData = new byte[imagePacketSize];
            try { Buffer.BlockCopy(m_asset_ref.Data, CurrentBytePosition(), imageData, 0, imagePacketSize); }
            catch (Exception e)
            {
                Console.WriteLine(String.Format("Err: srcLen:{0}, BytePos:{1}, desLen:{2}, pktsize:{3}, currpak:{4}, stoppak:{5}, totalpak:{6}", m_asset_ref.Data.Length, CurrentBytePosition(), 
                    imageData.Length, imagePacketSize, CurrentPacket,StopPacket,TexturePacketCount()));
                System.Console.WriteLine(e.ToString());
                //m_log.Error("Texture data copy failed for " + m_asset_ref.FullID.ToString());
                //m_cancel = true;
                //m_sending = false;
                return false;
            }

            // Send next packet to the client
            client.SendImageNextPart((ushort)(CurrentPacket - 1), requestedUUID, imageData);
            
            ++CurrentPacket;

            if (atEnd)
                CurrentPacket = StopPacket + 1;

            return true;
        }
        
    }

    /// <summary>
    /// Generic Priority Queue element
    /// Contains a Priority and a Reference type Data Element
    /// </summary>
    /// <typeparam name="D">Reference type data element</typeparam>
    struct Prio<D> : IComparable<Prio<D>> where D : class
    {
        public D data;
        private int priority;

        public Prio(D data, int priority)
        {
            this.data = data;
            this.priority = priority;
        }

        public int CompareTo(Prio<D> that)
        {
            return this.priority.CompareTo(that.priority);
        }

        public bool Equals(Prio<D> that)
        {
            return this.priority == that.priority;
        }

        public static Prio<D> operator +(Prio<D> tp, int delta)
        {
            return new Prio<D>(tp.data, tp.priority + delta);
        }

        public static bool operator <(Prio<D> tp, int check)
        {
            return (tp.priority < check);
        }

        public static bool operator >(Prio<D> tp, int check)
        {
            return (tp.priority > check);
        }

        public static Prio<D> operator -(Prio<D> tp, int delta)
        {
            if (tp.priority - delta < 0) 
                return new Prio<D>(tp.data, tp.priority - delta);
            else
                return new Prio<D>(tp.data, 0);
        }

        public override String ToString()
        {
            return String.Format("{0}[{1}]", data, priority);
        }

        internal Prio<D> SetPriority(int pPriority)
        {
            return new Prio<D>(this.data, pPriority);
        }
    }
}
