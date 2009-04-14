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
            else
                m_log.Error("[ClientView] - couldn't set missing image, all manner of things will probably break");
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
            if (m_client == null)
                return;

            if (m_client.PacketHandler == null)
                return;

            if (m_client.PacketHandler.PacketQueue == null)
                return;


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
}
