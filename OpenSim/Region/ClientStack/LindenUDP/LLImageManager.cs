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
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using log4net;
using System.Reflection;

namespace OpenSim.Region.ClientStack.LindenUDP
{

    public class LLImageManager
    {
        
        //Public interfaces:
        //Constructor - (LLClientView, IAssetCache, IJ2KDecoder);
        //void EnqueueReq - (TextureRequestArgs)
        //ProcessImageQueue
        //Close
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool m_shuttingdown = false; 

        private LLClientView m_client; //Client we're assigned to
        private IAssetCache m_assetCache; //Asset Cache
        private IJ2KDecoder m_j2kDecodeModule; //Our J2K module

        private readonly AssetBase m_missingsubstitute; //Sustitute for bad decodes
        private Dictionary<UUID,J2KImage> m_imagestore; // Our main image storage dictionary
        private SortedList<double,UUID> m_priorities; // For fast image lookup based on priority
        private Dictionary<int, int> m_priorityresolver; //Enabling super fast assignment of images with the same priorities

        private const double doubleMinimum = .0000001;
        //Constructor
        public LLImageManager(LLClientView client, IAssetCache pAssetCache, IJ2KDecoder pJ2kDecodeModule)
        {
            
            m_imagestore = new Dictionary<UUID,J2KImage>();
            m_priorities = new SortedList<double,UUID>();
            m_priorityresolver = new Dictionary<int, int>();
            m_client = client;
            m_assetCache = pAssetCache;
            if (pAssetCache != null)
                m_missingsubstitute = pAssetCache.GetAsset(UUID.Parse("5748decc-f629-461c-9a36-a35a221fe21f"), true);
            m_j2kDecodeModule = pJ2kDecodeModule;
        }

        public void EnqueueReq(TextureRequestArgs newRequest)
        {
            //newRequest is the properties of our new texture fetch request.
            //Basically, here is where we queue up "new" requests..
            // .. or modify existing requests to suit.

            //Make sure we're not shutting down..
            if (!m_shuttingdown)
            {

                //Do we already know about this UUID?
                if (m_imagestore.ContainsKey(newRequest.RequestedAssetID))
                {
                    //Check the packet sequence to make sure this isn't older than 
                    //one we've already received

                    J2KImage imgrequest = m_imagestore[newRequest.RequestedAssetID];

                    //if (newRequest.requestSequence > imgrequest.m_lastSequence)
                    //{
                        imgrequest.m_lastSequence = newRequest.requestSequence;

                        //First of all, is this being killed?
                        if (newRequest.Priority == 0.0f && newRequest.DiscardLevel == -1)
                        {
                            //Remove the old priority
                            m_priorities.Remove(imgrequest.m_designatedPriorityKey);
                            m_imagestore.Remove(imgrequest.m_requestedUUID);
                            imgrequest = null;
                        }
                        else
                        {


                            //Check the priority
                            double priority = imgrequest.m_requestedPriority;
                            if (priority != newRequest.Priority)
                            {
                                //Remove the old priority
                                m_priorities.Remove(imgrequest.m_designatedPriorityKey);
                                //Assign a new unique priority
                                imgrequest.m_requestedPriority = newRequest.Priority;
                                imgrequest.m_designatedPriorityKey = AssignPriority(newRequest.RequestedAssetID, newRequest.Priority);
                            }

                            //Update the requested discard level
                            imgrequest.m_requestedDiscardLevel = newRequest.DiscardLevel;

                            //Update the requested packet number
                            imgrequest.m_requestedPacketNumber = newRequest.PacketNumber;

                            //Run an update
                            imgrequest.RunUpdate();
                        }
                    //}
                }
                else
                {
                    J2KImage imgrequest = new J2KImage();

                    //Assign our missing substitute
                    imgrequest.m_MissingSubstitute = m_missingsubstitute;

                    //Assign our decoder module
                    imgrequest.m_j2kDecodeModule = m_j2kDecodeModule;

                    //Assign our asset cache module
                    imgrequest.m_assetCache = m_assetCache;

                    //Assign a priority based on our request
                    imgrequest.m_designatedPriorityKey = AssignPriority(newRequest.RequestedAssetID, newRequest.Priority);

                    //Assign the requested discard level
                    imgrequest.m_requestedDiscardLevel = newRequest.DiscardLevel;

                    //Assign the requested packet number
                    imgrequest.m_requestedPacketNumber = newRequest.PacketNumber;

                    //Assign the requested priority
                    imgrequest.m_requestedPriority = newRequest.Priority;

                    //Assign the asset uuid
                    imgrequest.m_requestedUUID = newRequest.RequestedAssetID;

                    m_imagestore.Add(imgrequest.m_requestedUUID, imgrequest);

                    //Run an update
                    imgrequest.RunUpdate();

                }
            }
        }

        private double AssignPriority(UUID pAssetID, double pPriority)
        {
            
            //First, find out if we can just assign directly
            if (m_priorityresolver.ContainsKey((int)pPriority) == false)
            {
                m_priorities.Add((double)((int)pPriority), pAssetID);
                m_priorityresolver.Add((int)pPriority, 0);
                return (double)((int)pPriority);
            }
            else
            {
                //Use the hash lookup goodness of a secondary dictionary to find a free slot
                double mFreePriority = ((int)pPriority) + (doubleMinimum * (m_priorityresolver[(int)pPriority] + 1));
                m_priorities[mFreePriority] = pAssetID;
                m_priorityresolver[(int)pPriority]++;
                return mFreePriority;
            }



        }

        public void ProcessImageQueue(int count)
        {
            
            //Count is the number of textures we want to process in one go.
            //As part of this class re-write, that number will probably rise
            //since we're processing in a more efficient manner.
            
            int numCollected = 0;
            //First of all make sure our packet queue isn't above our threshold 
            if (m_client.PacketHandler.PacketQueue.TextureOutgoingPacketQueueCount < 200)
            {
                
                for (int x = m_priorities.Count - 1; x > -1; x--)
                {
                    
                    J2KImage imagereq = m_imagestore[m_priorities.Values[x]];
                    if (imagereq.m_decoded == true && !imagereq.m_completedSendAtCurrentDiscardLevel)
                    {

                        numCollected++;
                        //SendPackets will send up to ten packets per cycle
                        //m_log.Debug("Processing packet with priority of " + imagereq.m_designatedPriorityKey.ToString());
                        if (imagereq.SendPackets(m_client))
                        {
                            //Send complete
                            imagereq.m_completedSendAtCurrentDiscardLevel = true;
                            //Re-assign priority to bottom
                            //Remove the old priority
                            m_priorities.Remove(imagereq.m_designatedPriorityKey);
                            int lowest;
                            if (m_priorities.Count > 0)
                            {
                                lowest = (int)m_priorities.Keys[0];
                                lowest--;
                            }
                            else
                            {
                                lowest = -10000;
                            }
                            m_priorities.Add((double)lowest, imagereq.m_requestedUUID);
                            imagereq.m_designatedPriorityKey = (double)lowest;
                            if (m_priorityresolver.ContainsKey((int)lowest))
                            {
                                m_priorityresolver[(int)lowest]++;
                            }
                            else
                            {
                                m_priorityresolver.Add((int)lowest, 0);
                            }
                        }
                        //m_log.Debug("...now has priority of " + imagereq.m_designatedPriorityKey.ToString());
                        if (numCollected == count)
                        {
                            break;
                        }
                    }
                }
            }



        }

        //Faux destructor
        public void Close()
        {
            
            m_shuttingdown = true;
            m_j2kDecodeModule = null;
            m_assetCache = null;
            m_client = null;
        }


    }

    /*
     * 
     *          J2KImage
     *          
     *          We use this class to store image data and associated request data and attributes
     *          
     * 
     * 
     */

    public class J2KImage
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public double m_designatedPriorityKey;
        public double m_requestedPriority = 0.0d;
        public uint m_lastSequence = 0;
        public uint m_requestedPacketNumber;
        public sbyte m_requestedDiscardLevel;
        public UUID m_requestedUUID;
        public IJ2KDecoder m_j2kDecodeModule;
        public IAssetCache m_assetCache;
        public OpenJPEG.J2KLayerInfo[] Layers = new OpenJPEG.J2KLayerInfo[0];
        public AssetBase m_MissingSubstitute = null;
        public bool m_decoded = false;
        public bool m_completedSendAtCurrentDiscardLevel;
        
        private sbyte m_discardLevel=-1;
        private uint m_packetNumber;
        private bool m_decoderequested = false;
        private bool m_hasasset = false;
        private bool m_asset_requested = false;
        private bool m_sentinfo = false;
        private uint m_stopPacket = 0;
        private const int cImagePacketSize = 1000;
        private const int cFirstPacketSize = 600;
        private AssetBase m_asset = null;
        

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
            get { return m_asset.Data; }
        }

        public ushort TexturePacketCount()
        {
            if (!m_decoded)
                return 0;
            return (ushort)(((m_asset.Data.Length - cFirstPacketSize + cImagePacketSize - 1) / cImagePacketSize) + 1);
        }

        public void J2KDecodedCallback(UUID AssetId, OpenJPEG.J2KLayerInfo[] layers)
        {
           Layers = layers;
           m_decoded = true;
           RunUpdate();
        }

        public void AssetDataCallback(UUID AssetID, AssetBase asset)
        {
            m_hasasset = true;
            if (asset == null || asset.Data == null)
            {
                m_asset = m_MissingSubstitute;
            }
            else
            {
                m_asset = asset;              
            }
            RunUpdate();
        }

        private int GetPacketForBytePosition(int bytePosition)
        {
            return ((bytePosition - cFirstPacketSize + cImagePacketSize - 1) / cImagePacketSize) + 1;
        }
        public int LastPacketSize()
        {
            if (m_packetNumber == 1)
                return m_asset.Data.Length;
            return (m_asset.Data.Length - cFirstPacketSize) % cImagePacketSize;
        }
 
        public int CurrentBytePosition()
        {
            if (m_packetNumber == 0)
                return 0;
            if (m_packetNumber == 1)
                return cFirstPacketSize;

            int result = cFirstPacketSize + ((int)m_packetNumber - 2) * cImagePacketSize;
            if (result < 0)
            {
                result = cFirstPacketSize;
            }
            return result;
        }
        public bool SendFirstPacket(LLClientView client)
        {

            // Do we have less then 1 packet's worth of data?
            if (m_asset.Data.Length <= cFirstPacketSize)
            {
                // Send only 1 packet
                client.SendImageFirstPart(1, m_requestedUUID, (uint)m_asset.Data.Length, m_asset.Data, 2);
                m_stopPacket = 0;
                return true;
            }
            else
            {
                byte[] firstImageData = new byte[cFirstPacketSize];
                try 
                { 
                    Buffer.BlockCopy(m_asset.Data, 0, firstImageData, 0, (int)cFirstPacketSize);
                    client.SendImageFirstPart(TexturePacketCount(), m_requestedUUID, (uint)m_asset.Data.Length, firstImageData, 2);                
                }
                catch (Exception)
                {
                    m_log.Error("Texture block copy failed. Possibly out of memory?");
                    return true;
                }
            }
            return false;

        }
        private bool SendPacket(LLClientView client)
        {
            bool complete = false;
            int imagePacketSize = ((int)m_packetNumber == (TexturePacketCount())) ? LastPacketSize() : cImagePacketSize;

            if ((CurrentBytePosition() + cImagePacketSize) > m_asset.Data.Length)
            {
                imagePacketSize = LastPacketSize();
                complete=true;
                if ((CurrentBytePosition() + imagePacketSize) > m_asset.Data.Length)
                {
                    imagePacketSize = m_asset.Data.Length - CurrentBytePosition();
                    complete = true;
                }
            }
            
            //It's concievable that the client might request packet one
            //from a one packet image, which is really packet 0,
            //which would leave us with a negative imagePacketSize..
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
                client.SendImageNextPart((ushort)(m_packetNumber-1), m_requestedUUID, imageData);
                
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
        public bool SendPackets(LLClientView client)
        {

            if (!m_completedSendAtCurrentDiscardLevel)
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

                    if (m_packetNumber < 2)
                    {
                        m_packetNumber = 2;
                    }
                    
                    int count=0;  
                    while (SendMore && count < 5 && m_packetNumber <= m_stopPacket)
                    {
                        count++;
                        SendMore = SendPacket(client);
                        m_packetNumber++;
                    }
                    if (m_packetNumber > m_stopPacket)
                    {

                        return true;

                    }

                }

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
                    m_assetCache.GetAsset(m_requestedUUID, AssetDataCallback, true);

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
                            // Send it off to the jpeg decoder
                            m_j2kDecodeModule.decode(m_requestedUUID, Data, J2KDecodedCallback);

                        }
                        else
                        {
                            J2KDecodedCallback(m_requestedUUID, new OpenJPEG.J2KLayerInfo[0]);
                        }
                    }

                }
                else
                {

   
                    //discardLevel of -1 means just update the priority
                    if (m_requestedDiscardLevel != -1)
                    {

                        //Evaluate the discard level
                        //First, is it positive?
                        if (m_requestedDiscardLevel >= 0)
                        {
                            if (m_requestedDiscardLevel > Layers.Length - 1)
                            {
                                m_discardLevel = (sbyte)(Layers.Length - 1);
                            }
                            else
                            {
                                m_discardLevel = m_requestedDiscardLevel;
                            }
                
                            //Calculate the m_stopPacket
                            if (Layers.Length > 0)
                            {
                                m_stopPacket = (uint)GetPacketForBytePosition(Layers[(Layers.Length - 1) - m_discardLevel].End);
                            }
                            else
                            {
                                m_stopPacket = TexturePacketCount();
                            }
                            //Don't reset packet number unless we're waiting or it's ahead of us
                            if (m_completedSendAtCurrentDiscardLevel || m_requestedPacketNumber>m_packetNumber)
                            {
                                m_packetNumber = m_requestedPacketNumber;
                            }
                   
                            if (m_packetNumber <= m_stopPacket)
                            {
                                m_completedSendAtCurrentDiscardLevel = false;
                            }

                        }

                    }
                }
            }
        }

    }
}
