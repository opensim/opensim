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

using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using pCampBot.Interfaces;

namespace pCampBot
{
    /// <summary>
    /// Do nothing
    /// </summary>
    public class InventoryDownloadBehaviour : AbstractBehaviour
    {
        private bool m_initialized;
        private int m_Requests = 2;
        private Stopwatch m_StopWatch = new Stopwatch();
        private List<UUID> m_processed = new List<UUID>();

        public InventoryDownloadBehaviour() 
        { 
            AbbreviatedName = "inv";
            Name = "Inventory";
        }

        public override void Action()
        {
            if (!m_initialized)
            {
                m_initialized = true;
                Bot.Client.Settings.HTTP_INVENTORY = true;
                Bot.Client.Settings.FETCH_MISSING_INVENTORY = true;
                Bot.Client.Inventory.FolderUpdated += Inventory_FolderUpdated;
                Console.WriteLine("Lib owner is " + Bot.Client.Inventory.Store.LibraryRootNode.Data.OwnerID);
                m_StopWatch.Start();
                Bot.Client.Inventory.RequestFolderContents(Bot.Client.Inventory.Store.RootFolder.UUID, Bot.Client.Self.AgentID, true, true, InventorySortOrder.ByDate);
                Bot.Client.Inventory.RequestFolderContents(Bot.Client.Inventory.Store.LibraryRootNode.Data.UUID, Bot.Client.Inventory.Store.LibraryRootNode.Data.OwnerID, true, true, InventorySortOrder.ByDate);
            }

            Thread.Sleep(1000);
            Console.WriteLine("Total items: " + Bot.Client.Inventory.Store.Items.Count + "; Total requests: " + m_Requests + "; Time: " + m_StopWatch.Elapsed);

        }

        void Inventory_FolderUpdated(object sender, FolderUpdatedEventArgs e)
        {
            if (e.Success)
            {
                //Console.WriteLine("Folder " + e.FolderID + " updated");
                bool fetch = false;
                lock (m_processed)
                {
                    if (!m_processed.Contains(e.FolderID))
                    {
                        m_processed.Add(e.FolderID);
                        fetch = true;
                    }
                }

                if (fetch)
                {
                    List<InventoryFolder> m_foldersToFetch = new List<InventoryFolder>();
                    foreach (InventoryBase item in Bot.Client.Inventory.Store.GetContents(e.FolderID))
                    {
                        if (item is InventoryFolder)
                        {
                            InventoryFolder f = new InventoryFolder(item.UUID);
                            f.OwnerID = item.OwnerID;
                            m_foldersToFetch.Add(f);
                        }
                    }
                    if (m_foldersToFetch.Count > 0)
                    {
                        m_Requests += 1;
                        Bot.Client.Inventory.RequestFolderContentsCap(m_foldersToFetch, Bot.Client.Network.CurrentSim.Caps.CapabilityURI("FetchInventoryDescendents2"), true, true, InventorySortOrder.ByDate);
                    }
                }

                if (Bot.Client.Inventory.Store.Items.Count >= 15739)
                {
                    m_StopWatch.Stop();
                    Console.WriteLine("Stop! Total items: " + Bot.Client.Inventory.Store.Items.Count + "; Total requests: " + m_Requests + "; Time: " + m_StopWatch.Elapsed);
                }
            }

        }

        public override void Interrupt() 
        {
            m_interruptEvent.Set();
        }
    }
}