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
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using log4net;

namespace OpenSim.Region.ClientStack.LindenUDP
{
    public class LLImageManager
    {
        private sealed class J2KImageComparer : IComparer<J2KImage>
        {
            public int Compare(J2KImage x, J2KImage y)
            {
                return x.m_requestedPriority.CompareTo(y.m_requestedPriority);
            }
        }

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool m_shuttingdown = false;
        private long m_lastloopprocessed = 0;
        private AssetBase m_missingImage = null;

        private LLClientView m_client; //Client we're assigned to
        private IAssetService m_assetCache; //Asset Cache
        private IJ2KDecoder m_j2kDecodeModule; //Our J2K module
        private C5.IntervalHeap<J2KImage> m_priorityQueue = new C5.IntervalHeap<J2KImage>(10, new J2KImageComparer());

        public LLImageManager(LLClientView client, IAssetService pAssetCache, IJ2KDecoder pJ2kDecodeModule)
        {
            m_client = client;
            m_assetCache = pAssetCache;
            if (pAssetCache != null)
                m_missingImage = pAssetCache.Get("5748decc-f629-461c-9a36-a35a221fe21f");
            else
                m_log.Error("[ClientView] - couldn't set missing image asset, falling back to missing image packet. This is known to crash the client");

            m_j2kDecodeModule = pJ2kDecodeModule;
        }

        public LLClientView Client
        {
            get { return m_client; }
        }

        public AssetBase MissingImage
        {
            get { return m_missingImage; }
        }

        public void EnqueueReq(TextureRequestArgs newRequest)
        {
            //newRequest is the properties of our new texture fetch request.
            //Basically, here is where we queue up "new" requests..
            // .. or modify existing requests to suit.

            //Make sure we're not shutting down..
            if (!m_shuttingdown)
            {
                J2KImage imgrequest;

                // Do a linear search for this texture download
                lock (m_priorityQueue)
                    m_priorityQueue.Find(delegate(J2KImage img) { return img.m_requestedUUID == newRequest.RequestedAssetID; }, out imgrequest);

                if (imgrequest != null)
                {
                    if (newRequest.DiscardLevel == -1 && newRequest.Priority == 0f)
                    {
                        //m_log.Debug("[TEX]: (CAN) ID=" + newRequest.RequestedAssetID);

                        try 
                        {
                            lock (m_priorityQueue)
                                m_priorityQueue.Delete(imgrequest.m_priorityQueueHandle); 
                        }
                        catch (Exception) { }
                    }
                    else
                    {
                        //m_log.DebugFormat("[TEX]: (UPD) ID={0}: D={1}, S={2}, P={3}",
                        //    newRequest.RequestedAssetID, newRequest.DiscardLevel, newRequest.PacketNumber, newRequest.Priority);

                        //Check the packet sequence to make sure this isn't older than 
                        //one we've already received
                        if (newRequest.requestSequence > imgrequest.m_lastSequence)
                        {
                            //Update the sequence number of the last RequestImage packet
                            imgrequest.m_lastSequence = newRequest.requestSequence;

                            //Update the requested discard level
                            imgrequest.m_requestedDiscardLevel = newRequest.DiscardLevel;

                            //Update the requested packet number
                            imgrequest.m_requestedPacketNumber = newRequest.PacketNumber;

                            //Update the requested priority
                            imgrequest.m_requestedPriority = newRequest.Priority;
                            try 
                            { 
                                lock (m_priorityQueue)
                                    m_priorityQueue.Replace(imgrequest.m_priorityQueueHandle, imgrequest); 
                            }
                            catch (Exception) 
                            { 
                                imgrequest.m_priorityQueueHandle = null; 
                                lock (m_priorityQueue)
                                    m_priorityQueue.Add(ref imgrequest.m_priorityQueueHandle, imgrequest); 
                            }

                            //Run an update
                            imgrequest.RunUpdate();
                        }
                    }
                }
                else
                {
                    if (newRequest.DiscardLevel == -1 && newRequest.Priority == 0f)
                    {
                        //m_log.DebugFormat("[TEX]: (IGN) ID={0}: D={1}, S={2}, P={3}",
                        //    newRequest.RequestedAssetID, newRequest.DiscardLevel, newRequest.PacketNumber, newRequest.Priority);
                    }
                    else
                    {
                        //m_log.DebugFormat("[TEX]: (NEW) ID={0}: D={1}, S={2}, P={3}",
                        //    newRequest.RequestedAssetID, newRequest.DiscardLevel, newRequest.PacketNumber, newRequest.Priority);

                        imgrequest = new J2KImage(this);

                        //Assign our decoder module
                        imgrequest.m_j2kDecodeModule = m_j2kDecodeModule;

                        //Assign our asset cache module
                        imgrequest.m_assetCache = m_assetCache;

                        //Assign the requested discard level
                        imgrequest.m_requestedDiscardLevel = newRequest.DiscardLevel;

                        //Assign the requested packet number
                        imgrequest.m_requestedPacketNumber = newRequest.PacketNumber;

                        //Assign the requested priority
                        imgrequest.m_requestedPriority = newRequest.Priority;

                        //Assign the asset uuid
                        imgrequest.m_requestedUUID = newRequest.RequestedAssetID;

                        //Assign the requested priority
                        imgrequest.m_requestedPriority = newRequest.Priority;

                        //Add this download to the priority queue
                        lock (m_priorityQueue)
                            m_priorityQueue.Add(ref imgrequest.m_priorityQueueHandle, imgrequest);

                        //Run an update
                        imgrequest.RunUpdate();
                    }
                }
            }
        }

        public bool ProcessImageQueue(int count, int maxpack)
        {
            lock (this)
            {
                //count is the number of textures we want to process in one go.
                //As part of this class re-write, that number will probably rise
                //since we're processing in a more efficient manner.

                // this can happen during Close()
                if (m_client == null)
                    return false;

                int numCollected = 0;

                //Calculate our threshold
                int threshold;
                if (m_lastloopprocessed == 0)
                {
                    if (m_client.PacketHandler == null || m_client.PacketHandler.PacketQueue == null || m_client.PacketHandler.PacketQueue.TextureThrottle == null)
                        return false;
                    //This is decent for a semi fast machine, but we'll calculate it more accurately based on time below
                    threshold = m_client.PacketHandler.PacketQueue.TextureThrottle.Current / 6300;
                    m_lastloopprocessed = DateTime.Now.Ticks;
                }
                else
                {
                    double throttleseconds = ((double)DateTime.Now.Ticks - (double)m_lastloopprocessed) / (double)TimeSpan.TicksPerSecond;
                    throttleseconds = throttleseconds * m_client.PacketHandler.PacketQueue.TextureThrottle.Current;

                    //Average of 1000 bytes per packet
                    throttleseconds = throttleseconds / 1000;

                    //Safe-zone multiplier of 2.0
                    threshold = (int)(throttleseconds * 2.0);
                    m_lastloopprocessed = DateTime.Now.Ticks;

                }

                if (m_client.PacketHandler == null)
                    return false;

                if (m_client.PacketHandler.PacketQueue == null)
                    return false;

                if (threshold < 10)
                    threshold = 10;

                //Uncomment this to see what the texture stack is doing
                //m_log.Debug("Queue: " + m_client.PacketHandler.PacketQueue.getQueueCount(ThrottleOutPacketType.Texture).ToString() + " Threshold: " + threshold.ToString() + " outstanding: " + m_outstandingtextures.ToString());
                if (true) //m_client.PacketHandler.PacketQueue.GetQueueCount(ThrottleOutPacketType.Texture) < threshold)
                {
                    while (m_priorityQueue.Count > 0)
                    {
                        J2KImage imagereq = null;
                        lock (m_priorityQueue)
                            imagereq = m_priorityQueue.FindMax();

                        if (imagereq.m_decoded == true)
                        {
                            // we need to test this here now that we are dropping assets
                            if (!imagereq.m_hasasset)
                            {
                                m_log.WarnFormat("[LLIMAGE MANAGER]: Re-requesting the image asset {0}", imagereq.m_requestedUUID);
                                imagereq.RunUpdate();
                                continue;
                            }

                            ++numCollected;

                            //SendPackets will send up to ten packets per cycle
                            if (imagereq.SendPackets(m_client, maxpack))
                            {
                                // Send complete. Destroy any knowledge of this transfer
                                try 
                                { 
                                    lock (m_priorityQueue)
                                        m_priorityQueue.Delete(imagereq.m_priorityQueueHandle); 
                                }
                                catch (Exception) { }
                            }
                        }

                        if (numCollected == count)
                            break;
                    }
                }

                return m_priorityQueue.Count > 0;
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
}
