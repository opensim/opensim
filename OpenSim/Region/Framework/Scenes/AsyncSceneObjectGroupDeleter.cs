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
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        /// <value>
        /// Is the deleter currently enabled?
        /// </value>
        public bool Enabled;
        private Scene m_scene;

        static private ConcurrentQueue<DeleteToInventoryHolder> m_inventoryDeletes = new ConcurrentQueue<DeleteToInventoryHolder>();
        static private object m_threadLock = new object();
        static private bool m_running;

        public AsyncSceneObjectGroupDeleter(Scene scene)
        {
            m_scene = scene;
        }

        /// <summary>
        /// Delete the given object from the scene
        /// </summary>
        public void DeleteToInventory(DeRezAction action, UUID folderID,
                List<SceneObjectGroup> objectGroups, IClientAPI remoteClient,
                bool permissionToDelete)
        {
            DeleteToInventoryHolder dtis = new DeleteToInventoryHolder();
            dtis.action = action;
            dtis.folderID = folderID;
            dtis.objectGroups = objectGroups;
            dtis.remoteClient = remoteClient;
            dtis.permissionToDelete = permissionToDelete;

            m_inventoryDeletes.Enqueue(dtis);

            if (permissionToDelete)
            {
                foreach (SceneObjectGroup g in objectGroups)
                    g.DeleteGroupFromScene(false);
            }

            if(Monitor.TryEnter(m_threadLock))
            {
                if(!m_running)
                {
                    if(Enabled)
                    {
                        m_running = true;
                        Util.FireAndForget(x => InventoryDeQueueAndDelete());
                    }
                    else
                    {
                        m_running = true;
                        InventoryDeQueueAndDelete();
                    }
                }
                Monitor.Exit(m_threadLock);
            }
        }

        /// <summary>
        /// Move the next object in the queue to inventory.  Then delete it properly from the scene.
        /// </summary>
        /// <returns></returns>
        public void InventoryDeQueueAndDelete()
        {
            lock (m_threadLock)
            {
                IInventoryAccessModule invAccess = m_scene.RequestModuleInterface<IInventoryAccessModule>();
                if (invAccess == null)
                    return;

                int count = 0;
                while (m_inventoryDeletes.TryDequeue(out DeleteToInventoryHolder x))
                {
                    //  m_log.DebugFormat(
                    //  "[ASYNC DELETER]: Sending object to user's inventory, action {1}, count {2}, {0} item(s) remaining.",
                    //  left, x.action, x.objectGroups.Count);
                    try
                    {
                        invAccess.CopyToInventory(x.action, x.folderID, x.objectGroups, x.remoteClient, false);
                        if (x.permissionToDelete)
                        {
                            foreach (SceneObjectGroup g in x.objectGroups)
                                m_scene.DeleteSceneObject(g, true);
                        }

                        count += x.objectGroups.Count;
                        if(count > 256)
                        {
                            Thread.Sleep(50); // throttle
                            count = 0;
                        }
                    }
                    catch (Exception e)
                    {
                        //m_log.ErrorFormat(
                        //    "[ASYNC OBJECT DELETER]: Exception background sending object: {0}{1}", e.Message, e.StackTrace);
                    }
                }
                // m_log.Debug("[ASYNC DELETER]: No objects left in inventory send queue.");
                m_running = false;
            }
        }
    }
}
