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
using System.Linq;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Scenes
{
    /// <summary>
    /// Represents a coalescene of scene objects.  A coalescence occurs when objects that are not in the same linkset
    /// are grouped together.
    /// </summary>
    public class CoalescedSceneObjects
    {
        /// <summary>
        /// The creator of this coalesence, though not necessarily the objects within it.
        /// </summary>
        public UUID CreatorId { get; set; }

        /// <summary>
        /// The number of objects in this coalesence
        /// </summary>
        public int Count
        {
            get
            {
                lock (m_memberObjects)
                    return m_memberObjects.Count;
            }
        }

        /// <summary>
        /// Does this coalesence have any member objects?
        /// </summary>
        public bool HasObjects { get { return Count > 0; } }

        /// <summary>
        /// Get the objects currently in this coalescence
        /// </summary>
        public List<SceneObjectGroup> Objects
        {
            get
            {
                lock (m_memberObjects)
                    return new List<SceneObjectGroup>(m_memberObjects);
            }
        }

        /// <summary>
        /// Get the scene that contains the objects in this coalescence.  If there are no objects then null is returned.
        /// </summary>
        public Scene Scene
        {
            get
            {
                if (!HasObjects)
                    return null;
                else
                    return Objects[0].Scene;
            }
        }

        /// <summary>
        /// At this point, we need to preserve the order of objects added to the coalescence, since the first
        /// one will end up matching the item name when rerezzed.
        /// </summary>
        protected List<SceneObjectGroup> m_memberObjects = new List<SceneObjectGroup>();

        public CoalescedSceneObjects(UUID creatorId)
        {
            CreatorId = creatorId;
        }

        public CoalescedSceneObjects(UUID creatorId, params SceneObjectGroup[] objs) : this(creatorId)
        {
            foreach (SceneObjectGroup obj in objs)
                Add(obj);
        }

        /// <summary>
        /// Add an object to the coalescence.
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="offset">The offset of the object within the group</param>
        public void Add(SceneObjectGroup obj)
        {
            lock (m_memberObjects)
                m_memberObjects.Add(obj);
        }

        /// <summary>
        /// Removes a scene object from the coalescene
        /// </summary>
        /// <param name="sceneObjectId"></param>
        /// <returns>true if the object was there to be removed, false if not.</returns>
        public bool Remove(SceneObjectGroup obj)
        {
            lock (m_memberObjects)
                return m_memberObjects.Remove(obj);
        }

        /// <summary>
        /// Get the total size of the coalescence (the size required to cover all the objects within it) and the
        /// offsets of each of those objects.
        /// </summary>
        /// <param name="size"></param>
        /// <returns>
        /// An array of offsets.  The order of objects is the same as returned from the Objects property
        /// </returns>
        public Vector3[] GetSizeAndOffsets(out Vector3 size)
        {
            float minX, minY, minZ;
            float maxX, maxY, maxZ;

            Vector3[] offsets
                = Scene.GetCombinedBoundingBox(
                    Objects, out minX, out maxX, out minY, out maxY, out minZ, out maxZ);

            float sizeX = maxX - minX;
            float sizeY = maxY - minY;
            float sizeZ = maxZ - minZ;

            size = new Vector3(sizeX, sizeY, sizeZ);

            return offsets;
        }
    }
}