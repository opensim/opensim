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
using System.Collections.Concurrent;
//using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework.Scenes
{
    class FetchHolder
    {
        public IClientAPI Client { get; private set; }
        public UUID[] Items { get; private set; }
        public UUID[] Owners { get; private set; }

        public FetchHolder(IClientAPI client, UUID[] items, UUID[] owners)
        {
            Client = client;
            Items = items;
            Owners = owners;
        }
    }

    /// <summary>
    /// Send FetchInventoryReply information to clients asynchronously on a single thread rather than asynchronously via
    /// multiple threads.
    /// </summary>
    /// <remarks>
    /// If the main root inventory is right-clicked on a version 1 viewer for a user with a large inventory, a very
    /// very large number of FetchInventory requests are sent to the simulator.  Each is handled on a separate thread
    /// by the IClientAPI, but the sheer number of requests overwhelms the number of threads available and ends up
    /// freezing the inbound packet handling.
    ///
    /// This class makes the first FetchInventory packet thread process the queue.  If new requests come
    /// in while it is processing, then the subsequent threads just add the requests and leave it to the original
    /// thread to process them.
    ///
    /// This might slow down outbound packets but these are limited by the IClientAPI outbound queues
    /// anyway.
    ///
    /// It might be possible to ignore FetchInventory requests altogether, particularly as they are redundant wrt to
    /// FetchInventoryDescendents requests, but this would require more investigation.
    /// </remarks>
    public class AsyncInventorySender
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Scene m_scene;

        /// <summary>
        /// Queues fetch requests
        /// </summary>
        private static ConcurrentQueue<FetchHolder> m_fetchHolder = new ConcurrentQueue<FetchHolder>();
        static private object m_threadLock = new object();
        static private bool m_running;

        /// <summary>
        /// Signal whether a queue is currently being processed or not.
        /// </summary>
        public AsyncInventorySender(Scene scene)
        {
            m_scene = scene;
        }

        /// <summary>
        /// Handle a fetch inventory request from the client
        /// </summary>
        /// <param name="remoteClient"></param>
        /// <param name="itemID"></param>
        /// <param name="ownerID"></param>
        public void HandleFetchInventory(IClientAPI remoteClient, UUID[] items, UUID[] owners)
        {
               //m_log.DebugFormat(
               //     "[ASYNC INVENTORY SENDER]: Putting request from {0} for {1} on queue", remoteClient.Name, itemID);

            m_fetchHolder.Enqueue(new FetchHolder(remoteClient, items, owners));
            if (Monitor.TryEnter(m_threadLock))
            {
                if (!m_running)
                {
                    m_running = true;
                    Util.FireAndForget(x => ProcessQueue());
                }
                Monitor.Exit(m_threadLock);
            }
        }

        /// <summary>
        /// Process the queue of fetches
        /// </summary>
        protected void ProcessQueue()
        {
            lock(m_threadLock)
            {
                try
                {
                    while (m_fetchHolder.TryDequeue(out FetchHolder fh))
                    {
                        if (!fh.Client.IsActive)
                            continue;
                        // m_log.DebugFormat(
                        //     "[ASYNC INVENTORY SENDER]: Handling request from {0} for {1} on queue", fh.Client.Name, fh.ItemID);

                        var items = new List<InventoryItemBase>();
                        for(int i = 0; i < fh.Items.Length; ++i )
                        {
                            InventoryItemBase item = m_scene.InventoryService.GetItem(fh.Owners[i], fh.Items[i]);
                            if (item == null)
                                continue;

                            /*
                            if (item.AssetType == (int)AssetType.Link)
                            {
                                InventoryItemBase itemlk = m_scene.InventoryService.GetItem(fh.Owners[i], item.AssetID);
                                if(itemlk != null)
                                    items.Add(itemlk);
                            }
                            */

                            items.Add(item);
                        }

                        fh.Client.SendInventoryItemDetails(items.ToArray());
                         // TODO: Possibly log any failure
                    }
                }
                catch  { }
                m_running = false;
            }
        }
    }
}
