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
using System.Reflection;
using System.Timers;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework.Scenes
{
    class DeleteToInventoryHolder
    {
        public DeRezAction action;
        public IClientAPI remoteClient;
        public List<SceneObjectGroup> objectGroups;
        public UUID folderID;
        public bool permissionToDelete;
    }

    /// <summary>
    /// Asynchronously derez objects.  This is used to derez large number of objects to inventory without holding
    /// up the main client thread.
    /// </summary>
    public class AsyncSceneObjectGroupDeleter
    {
        private static readonly ILog m_log
            = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// Is the deleter currently enabled?
        /// </value>
        public bool Enabled;

        private Timer m_inventoryTicker = new Timer(2000);
        private readonly Queue<DeleteToInventoryHolder> m_inventoryDeletes = new Queue<DeleteToInventoryHolder>();
        private Scene m_scene;

        public AsyncSceneObjectGroupDeleter(Scene scene)
        {
            m_scene = scene;

            m_inventoryTicker.AutoReset = false;
            m_inventoryTicker.Elapsed += InventoryRunDeleteTimer;
        }

        /// <summary>
        /// Delete the given object from the scene
        /// </summary>
        public void DeleteToInventory(DeRezAction action, UUID folderID,
                List<SceneObjectGroup> objectGroups, IClientAPI remoteClient,
                bool permissionToDelete)
        {
            if (Enabled)
                lock (m_inventoryTicker)
                    m_inventoryTicker.Stop();

            lock (m_inventoryDeletes)
            {
                DeleteToInventoryHolder dtis = new DeleteToInventoryHolder();
                dtis.action = action;
                dtis.folderID = folderID;
                dtis.objectGroups = objectGroups;
                dtis.remoteClient = remoteClient;
                dtis.permissionToDelete = permissionToDelete;

                m_inventoryDeletes.Enqueue(dtis);
            }

            if (Enabled)
                lock (m_inventoryTicker)
                    m_inventoryTicker.Start();

            // Visually remove it, even if it isnt really gone yet.  This means that if we crash before the object
            // has gone to inventory, it will reappear in the region again on restart instead of being lost.
            // This is not ideal since the object will still be available for manipulation when it should be, but it's
            // better than losing the object for now.
            if (permissionToDelete)
            {
                foreach (SceneObjectGroup g in objectGroups)
                    g.DeleteGroupFromScene(false);
            }
        }

        private void InventoryRunDeleteTimer(object sender, ElapsedEventArgs e)
        {
//            m_log.Debug("[ASYNC DELETER]: Starting send to inventory loop");

            // We must set appearance parameters in the en_US culture in order to avoid issues where values are saved
            // in a culture where decimal points are commas and then reloaded in a culture which just treats them as
            // number seperators.
            Culture.SetCurrentCulture();

            while (InventoryDeQueueAndDelete())
            {
                //m_log.Debug("[ASYNC DELETER]: Sent item successfully to inventory, continuing...");
            }
        }

        /// <summary>
        /// Move the next object in the queue to inventory.  Then delete it properly from the scene.
        /// </summary>
        /// <returns></returns>
        public bool InventoryDeQueueAndDelete()
        {
            DeleteToInventoryHolder x = null;

            try
            {
                lock (m_inventoryDeletes)
                {
                    int left = m_inventoryDeletes.Count;
                    if (left > 0)
                    {
                        x = m_inventoryDeletes.Dequeue();

//                        m_log.DebugFormat(
//                            "[ASYNC DELETER]: Sending object to user's inventory, action {1}, count {2}, {0} item(s) remaining.",
//                            left, x.action, x.objectGroups.Count);

                        try
                        {
                            IInventoryAccessModule invAccess = m_scene.RequestModuleInterface<IInventoryAccessModule>();
                            if (invAccess != null)
                                invAccess.CopyToInventory(x.action, x.folderID, x.objectGroups, x.remoteClient, false);

                            if (x.permissionToDelete)
                            {
                                foreach (SceneObjectGroup g in x.objectGroups)
                                    m_scene.DeleteSceneObject(g, true);
                            }
                        }
                        catch (Exception e)
                        {
                            m_log.ErrorFormat(
                                "[ASYNC DELETER]: Exception background sending object: {0}{1}", e.Message, e.StackTrace);
                        }

                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                // We can't put the object group details in here since the root part may have disappeared (which is where these sit).
                // FIXME: This needs to be fixed.
                m_log.ErrorFormat(
                    "[ASYNC DELETER]: Queued sending of scene object to agent {0} {1} failed: {2} {3}",
                    (x != null ? x.remoteClient.Name : "unavailable"),
                    (x != null ? x.remoteClient.AgentId.ToString() : "unavailable"),
                    e.Message,
                    e.StackTrace);
            }

//            m_log.Debug("[ASYNC DELETER]: No objects left in inventory send queue.");

            return false;
        }
    }
}
