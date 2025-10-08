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
using System.Collections.Concurrent;
using System.Collections.Generic;
using log4net;

namespace OpenSim.Region.PhysicsModules.SharedBase
{
    /// <summary>
    /// Generic object pool for physics objects to reduce garbage collection pressure
    /// </summary>
    public class PhysicsObjectPool<T> where T : class, new()
    {
        protected static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        protected static readonly string LogHeader = "[PHYSICS OBJECT POOL]";
        
        private readonly ConcurrentQueue<T> _pool = new ConcurrentQueue<T>();
        private readonly int _maxPoolSize;
        private int _currentCount = 0;
        private readonly object _lockObject = new object();
        
        // Statistics
        private long _totalCreated = 0;
        private long _totalReused = 0;
        private long _totalReturned = 0;
        
        public PhysicsObjectPool(int maxPoolSize = 100)
        {
            _maxPoolSize = maxPoolSize;
        }
        
        /// <summary>
        /// Get an object from the pool or create a new one
        /// </summary>
        public T Get()
        {
            if (_pool.TryDequeue(out T item))
            {
                lock (_lockObject)
                {
                    _currentCount--;
                    _totalReused++;
                }
                return item;
            }
            
            // Pool is empty, create new object
            lock (_lockObject)
            {
                _totalCreated++;
            }
            return new T();
        }
        
        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void Return(T item)
        {
            if (item == null) return;
            
            lock (_lockObject)
            {
                if (_currentCount < _maxPoolSize)
                {
                    _pool.Enqueue(item);
                    _currentCount++;
                    _totalReturned++;
                }
                // If pool is full, just let the object be garbage collected
            }
        }
        
        /// <summary>
        /// Get pool statistics
        /// </summary>
        public PoolStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new PoolStatistics
                {
                    CurrentCount = _currentCount,
                    MaxPoolSize = _maxPoolSize,
                    TotalCreated = _totalCreated,
                    TotalReused = _totalReused,
                    TotalReturned = _totalReturned,
                    ReusePercentage = _totalCreated > 0 ? (_totalReused * 100.0) / (_totalCreated + _totalReused) : 0
                };
            }
        }
        
        /// <summary>
        /// Clear the pool
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                while (_pool.TryDequeue(out T _))
                {
                    _currentCount--;
                }
            }
        }
    }
    
    /// <summary>
    /// Statistics for object pool performance monitoring
    /// </summary>
    public struct PoolStatistics
    {
        public int CurrentCount;
        public int MaxPoolSize;
        public long TotalCreated;
        public long TotalReused;
        public long TotalReturned;
        public double ReusePercentage;
        
        public override string ToString()
        {
            return $"Pool[Current: {CurrentCount}, Max: {MaxPoolSize}, Created: {TotalCreated}, " +
                   $"Reused: {TotalReused}, Returned: {TotalReturned}, Reuse%: {ReusePercentage:F1}]";
        }
    }
    
    /// <summary>
    /// Poolable object interface for objects that need cleanup when returned to pool
    /// </summary>
    public interface IPoolable
    {
        void Reset();
    }
    
    /// <summary>
    /// Specialized object pool for IPoolable objects
    /// </summary>
    public class PoolableObjectPool<T> : PhysicsObjectPool<T> where T : class, IPoolable, new()
    {
        public PoolableObjectPool(int maxPoolSize = 100) : base(maxPoolSize)
        {
        }
        
        /// <summary>
        /// Return an object to the pool after resetting it
        /// </summary>
        public new void Return(T item)
        {
            if (item == null) return;
            
            try
            {
                item.Reset();
                base.Return(item);
            }
            catch (Exception ex)
            {
                // If reset fails, don't return to pool to avoid corrupted objects
                m_log.WarnFormat("{0}: Failed to reset poolable object: {1}", LogHeader, ex.Message);
            }
        }
    }
}