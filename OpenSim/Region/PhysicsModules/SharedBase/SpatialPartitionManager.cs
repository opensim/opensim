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
using OpenMetaverse;
using log4net;

namespace OpenSim.Region.PhysicsModules.SharedBase
{
    /// <summary>
    /// Spatial partitioning system for efficient collision detection and object management
    /// </summary>
    public class SpatialPartitionManager : IPoolStatisticsProvider
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[SPATIAL PARTITION]";
        
        private readonly ConcurrentDictionary<long, SpatialCell> _cells = new ConcurrentDictionary<long, SpatialCell>();
        private readonly object _lockObject = new object();
        private readonly float _gridSize;
        private readonly PhysicsObjectPool<List<ISpatialObject>> _listPool;
        
        // Statistics
        private long _totalQueries = 0;
        private long _totalObjects = 0;
        private long _cellsCreated = 0;
        private long _cellsAccessed = 0;
        
        public string PoolName => "SpatialPartition";
        
        public SpatialPartitionManager(float gridSize = 32.0f)
        {
            _gridSize = gridSize;
            _listPool = new PhysicsObjectPool<List<ISpatialObject>>(50);
            PhysicsProfiler.RegisterPool(this);
        }
        
        /// <summary>
        /// Add an object to the spatial partition
        /// </summary>
        public void AddObject(ISpatialObject obj)
        {
            if (obj == null) return;
            
            var cellKeys = GetCellKeys(obj.GetAABB());
            
            lock (_lockObject)
            {
                foreach (long key in cellKeys)
                {
                    var cell = _cells.GetOrAdd(key, k => 
                    {
                        _cellsCreated++;
                        return new SpatialCell();
                    });
                    
                    cell.AddObject(obj);
                }
                _totalObjects++;
            }
        }
        
        /// <summary>
        /// Remove an object from the spatial partition
        /// </summary>
        public void RemoveObject(ISpatialObject obj)
        {
            if (obj == null) return;
            
            var cellKeys = GetCellKeys(obj.GetAABB());
            
            lock (_lockObject)
            {
                foreach (long key in cellKeys)
                {
                    if (_cells.TryGetValue(key, out SpatialCell cell))
                    {
                        cell.RemoveObject(obj);
                        
                        // Clean up empty cells
                        if (cell.IsEmpty)
                        {
                            _cells.TryRemove(key, out _);
                        }
                    }
                }
                _totalObjects--;
            }
        }
        
        /// <summary>
        /// Update an object's position in the spatial partition
        /// </summary>
        public void UpdateObject(ISpatialObject obj, AABB oldAABB)
        {
            if (obj == null) return;
            
            var oldKeys = GetCellKeys(oldAABB);
            var newKeys = GetCellKeys(obj.GetAABB());
            
            // Check if the object moved to different cells
            if (!AreSameCells(oldKeys, newKeys))
            {
                RemoveFromCells(obj, oldKeys);
                AddToCells(obj, newKeys);
            }
        }
        
        /// <summary>
        /// Query objects in a specific area
        /// </summary>
        public List<ISpatialObject> QueryArea(AABB aabb)
        {
            var results = _listPool.Get();
            results.Clear();
            
            var cellKeys = GetCellKeys(aabb);
            var processedObjects = new HashSet<ISpatialObject>();
            
            lock (_lockObject)
            {
                _totalQueries++;
                
                foreach (long key in cellKeys)
                {
                    if (_cells.TryGetValue(key, out SpatialCell cell))
                    {
                        _cellsAccessed++;
                        foreach (var obj in cell.Objects)
                        {
                            if (processedObjects.Add(obj) && obj.GetAABB().Intersects(aabb))
                            {
                                results.Add(obj);
                            }
                        }
                    }
                }
            }
            
            return results;
        }
        
        /// <summary>
        /// Return a query result list to the pool
        /// </summary>
        public void ReturnQueryResult(List<ISpatialObject> list)
        {
            if (list != null)
            {
                list.Clear();
                _listPool.Return(list);
            }
        }
        
        /// <summary>
        /// Clear all objects from the spatial partition
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                _cells.Clear();
                _totalObjects = 0;
            }
        }
        
        /// <summary>
        /// Get cell keys for an AABB
        /// </summary>
        private HashSet<long> GetCellKeys(AABB aabb)
        {
            var keys = new HashSet<long>();
            
            int minX = (int)Math.Floor(aabb.Min.X / _gridSize);
            int maxX = (int)Math.Floor(aabb.Max.X / _gridSize);
            int minY = (int)Math.Floor(aabb.Min.Y / _gridSize);
            int maxY = (int)Math.Floor(aabb.Max.Y / _gridSize);
            int minZ = (int)Math.Floor(aabb.Min.Z / _gridSize);
            int maxZ = (int)Math.Floor(aabb.Max.Z / _gridSize);
            
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        keys.Add(GetCellKey(x, y, z));
                    }
                }
            }
            
            return keys;
        }
        
        /// <summary>
        /// Generate a unique key for a cell coordinate
        /// </summary>
        private long GetCellKey(int x, int y, int z)
        {
            // Use a proper hash function to create a unique key
            return (long)HashCode.Combine(x, y, z);
        }
        
        private bool AreSameCells(HashSet<long> oldKeys, HashSet<long> newKeys)
        {
            return oldKeys.SetEquals(newKeys);
        }
        
        private void RemoveFromCells(ISpatialObject obj, HashSet<long> cellKeys)
        {
            foreach (long key in cellKeys)
            {
                if (_cells.TryGetValue(key, out SpatialCell cell))
                {
                    cell.RemoveObject(obj);
                    if (cell.IsEmpty)
                    {
                        _cells.TryRemove(key, out _);
                    }
                }
            }
        }
        
        private void AddToCells(ISpatialObject obj, HashSet<long> cellKeys)
        {
            foreach (long key in cellKeys)
            {
                var cell = _cells.GetOrAdd(key, k => 
                {
                    _cellsCreated++;
                    return new SpatialCell();
                });
                cell.AddObject(obj);
            }
        }
        
        public PoolStatistics GetPoolStatistics()
        {
            lock (_lockObject)
            {
                return new PoolStatistics
                {
                    CurrentCount = _cells.Count,
                    MaxPoolSize = int.MaxValue,
                    TotalCreated = _cellsCreated,
                    TotalReused = _cellsAccessed,
                    TotalReturned = _totalQueries,
                    ReusePercentage = _totalQueries > 0 ? (_cellsAccessed * 100.0) / _totalQueries : 0
                };
            }
        }
    }
    
    /// <summary>
    /// A single cell in the spatial partition
    /// </summary>
    internal class SpatialCell
    {
        private readonly HashSet<ISpatialObject> _objects = new HashSet<ISpatialObject>();
        private readonly object _lock = new object();
        
        public IEnumerable<ISpatialObject> Objects
        {
            get
            {
                lock (_lock)
                {
                    return _objects;
                }
            }
        }
        
        public bool IsEmpty
        {
            get
            {
                lock (_lock)
                {
                    return _objects.Count == 0;
                }
            }
        }
        
        public void AddObject(ISpatialObject obj)
        {
            lock (_lock)
            {
                _objects.Add(obj);
            }
        }
        
        public void RemoveObject(ISpatialObject obj)
        {
            lock (_lock)
            {
                _objects.Remove(obj);
            }
        }
    }
    
    /// <summary>
    /// Interface for objects that can be spatially partitioned
    /// </summary>
    public interface ISpatialObject
    {
        uint LocalID { get; }
        AABB GetAABB();
    }
    
    /// <summary>
    /// Axis-Aligned Bounding Box for spatial calculations
    /// </summary>
    public struct AABB
    {
        public Vector3 Min;
        public Vector3 Max;
        
        public AABB(Vector3 min, Vector3 max)
        {
            Min = min;
            Max = max;
        }
        
        public AABB(Vector3 center, float radius)
        {
            var extent = new Vector3(radius);
            Min = center - extent;
            Max = center + extent;
        }
        
        public bool Intersects(AABB other)
        {
            return !(Max.X < other.Min.X || Min.X > other.Max.X ||
                     Max.Y < other.Min.Y || Min.Y > other.Max.Y ||
                     Max.Z < other.Min.Z || Min.Z > other.Max.Z);
        }
        
        public bool Contains(Vector3 point)
        {
            return point.X >= Min.X && point.X <= Max.X &&
                   point.Y >= Min.Y && point.Y <= Max.Y &&
                   point.Z >= Min.Z && point.Z <= Max.Z;
        }
        
        public Vector3 Center => (Min + Max) * 0.5f;
        public Vector3 Size => Max - Min;
    }
}