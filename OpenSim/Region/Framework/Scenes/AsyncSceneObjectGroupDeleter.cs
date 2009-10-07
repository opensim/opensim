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

namespace OpenSim.Region.Framework.Scenes
{
    class DeleteToInventoryHolder
    {
        public DeRezAction action;
        public IClientAPI remoteClient;
        public SceneObjectGroup objectGroup;
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
                SceneObjectGroup objectGroup, IClientAPI remoteClient, 
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
                dtis.objectGroup = objectGroup;
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
                objectGroup.DeleteGroup(false);
        }
        
        private void InventoryRunDeleteTimer(object sender, ElapsedEventArgs e)
        {
            m_log.Debug("[SCENE]: Starting send to inventory loop");
            
            while (InventoryDeQueueAndDelete())
            {
                //m_log.Debug("[SCENE]: Sent item successfully to inventory, continuing...");
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
                        if (!x.objectGroup.CanBeDeleted())
                        {
                            m_inventoryDeletes.Enqueue(x);
                            return true;
                        }

                        m_log.DebugFormat(
                            "[SCENE]: Sending object to user's inventory, {0} item(s) remaining.", left);
                        
                        try
                        {
                            m_scene.DeleteToInventory(x.action, x.folderID, x.objectGroup, x.remoteClient);
                            if (x.permissionToDelete)
                                m_scene.DeleteSceneObject(x.objectGroup, false);
                        }
                        catch (Exception e)
                        {
                            m_log.DebugFormat("Exception background sending object: " + e);
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
                    "[SCENE]: Queued sending of scene object to agent {0} {1} failed: {2}",
                    (x != null ? x.remoteClient.Name : "unavailable"), (x != null ? x.remoteClient.AgentId.ToString() : "unavailable"), e.ToString());
            }

            m_log.Debug("[SCENE]: No objects left in inventory send queue.");
            return false;
        }
    }
}
