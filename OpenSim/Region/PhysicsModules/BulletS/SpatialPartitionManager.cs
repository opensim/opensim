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
using OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS
{
    /// <summary>
    /// Manages spatial partitioning for the physics scene to optimize queries and updates.
    /// Helps in quickly locating objects within a specific area, reducing the overhead
    /// of distance checks and other spatial queries.
    /// </summary>
    public class SpatialPartitionManager
    {
        private float m_gridSize;
        // Dictionary mapping grid coordinates (hashed) to list of objects
        // Key is a simple hash of int x, int y, int z
        private Dictionary<long, List<BSPhysObject>> m_grid = new Dictionary<long, List<BSPhysObject>>();
        private Dictionary<uint, long> m_objectLocations = new Dictionary<uint, long>();
        private object m_lock = new object();

        /// <summary>
        /// Initializes a new instance of the SpatialPartitionManager.
        /// </summary>
        /// <param name="gridSize">The size of each spatial grid cell in meters.</param>
        public SpatialPartitionManager(float gridSize)
        {
            m_gridSize = gridSize;
        }

        /// <summary>
        /// Generates a hash key for the grid cell containing the specified position.
        /// </summary>
        private long GetKey(Vector3 pos)
        {
            int x = (int)Math.Floor(pos.X / m_gridSize);
            int y = (int)Math.Floor(pos.Y / m_gridSize);
            int z = (int)Math.Floor(pos.Z / m_gridSize);
            
            // Simple spatial hash: (x * 73856093) ^ (y * 19349663) ^ (z * 83492791)
            return (long)((x * 73856093) ^ (y * 19349663) ^ (z * 83492791));
        }

        /// <summary>
        /// Adds or updates a physical object in the spatial partition.
        /// </summary>
        /// <param name="obj">The physical object to register.</param>
        public void UpdateObject(BSPhysObject obj)
        {
            Vector3 pos = obj.RawPosition;
            long key = GetKey(pos);

            lock (m_lock)
            {
                // Check if object is already tracked and moved
                if (m_objectLocations.TryGetValue(obj.LocalID, out long oldKey))
                {
                    if (oldKey == key) return; // Haven't changed cell

                    // Remove from old cell
                    if (m_grid.TryGetValue(oldKey, out List<BSPhysObject> oldCell))
                    {
                        oldCell.Remove(obj);
                        if (oldCell.Count == 0) m_grid.Remove(oldKey);
                    }
                }

                // Add to new cell
                if (!m_grid.TryGetValue(key, out List<BSPhysObject> cell))
                {
                    cell = new List<BSPhysObject>();
                    m_grid[key] = cell;
                }
                cell.Add(obj);
                m_objectLocations[obj.LocalID] = key;
            }
        }

        /// <summary>
        /// Removes a physical object from the spatial partition.
        /// </summary>
        /// <param name="obj">The physical object to remove.</param>
        public void RemoveObject(BSPhysObject obj)
        {
            lock (m_lock)
            {
                if (m_objectLocations.TryGetValue(obj.LocalID, out long key))
                {
                    if (m_grid.TryGetValue(key, out List<BSPhysObject> cell))
                    {
                        cell.Remove(obj);
                        if (cell.Count == 0) m_grid.Remove(key);
                    }
                    m_objectLocations.Remove(obj.LocalID);
                }
            }
        }

        /// <summary>
        /// Finds all objects within a radius of a point.
        /// </summary>
        public List<BSPhysObject> GetNearbyObjects(Vector3 position, float radius)
        {
            List<BSPhysObject> result = new List<BSPhysObject>();
            
            // Determine range of cells to check
            int minX = (int)Math.Floor((position.X - radius) / m_gridSize);
            int maxX = (int)Math.Floor((position.X + radius) / m_gridSize);
            int minY = (int)Math.Floor((position.Y - radius) / m_gridSize);
            int maxY = (int)Math.Floor((position.Y + radius) / m_gridSize);
            int minZ = (int)Math.Floor((position.Z - radius) / m_gridSize);
            int maxZ = (int)Math.Floor((position.Z + radius) / m_gridSize);

            float radiusSq = radius * radius;

            lock (m_lock)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        for (int z = minZ; z <= maxZ; z++)
                        {
                            // Recompute hash for this cell
                            long key = (long)((x * 73856093) ^ (y * 19349663) ^ (z * 83492791));
                            
                            if (m_grid.TryGetValue(key, out List<BSPhysObject> cell))
                            {
                                foreach (BSPhysObject obj in cell)
                                {
                                    if (Vector3.DistanceSquared(obj.RawPosition, position) <= radiusSq)
                                    {
                                        result.Add(obj);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }
    }
}
