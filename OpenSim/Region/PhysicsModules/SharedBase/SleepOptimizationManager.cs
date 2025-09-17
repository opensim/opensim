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
using OpenMetaverse;
using log4net;

namespace OpenSim.Region.PhysicsModules.SharedBase
{
    /// <summary>
    /// Sleep optimization manager for static physics objects
    /// Reduces CPU usage by putting stationary objects to sleep
    /// </summary>
    public class SleepOptimizationManager : IPoolStatisticsProvider
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[SLEEP OPTIMIZATION]";
        
        private readonly ConcurrentDictionary<uint, SleepCandidate> _candidates = new ConcurrentDictionary<uint, SleepCandidate>();
        private readonly ConcurrentDictionary<uint, ISleepableObject> _sleepingObjects = new ConcurrentDictionary<uint, ISleepableObject>();
        private readonly object _lockObject = new object();
        
        // Configuration
        private float _sleepTimeout = 2.0f;
        private float _velocityThreshold = 0.01f;
        private bool _enabled = true;
        
        // Statistics
        private long _totalObjectsProcessed = 0;
        private long _objectsPutToSleep = 0;
        private long _objectsWokenUp = 0;
        private long _sleepChecks = 0;
        
        public string PoolName => "SleepOptimization";
        
        public SleepOptimizationManager(float sleepTimeout = 2.0f, float velocityThreshold = 0.01f)
        {
            _sleepTimeout = sleepTimeout;
            _velocityThreshold = velocityThreshold;
            PhysicsProfiler.RegisterPool(this);
        }
        
        /// <summary>
        /// Enable or disable sleep optimization
        /// </summary>
        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }
        
        /// <summary>
        /// Time before static objects are put to sleep (seconds)
        /// </summary>
        public float SleepTimeout
        {
            get { return _sleepTimeout; }
            set { _sleepTimeout = Math.Max(0.1f, value); }
        }
        
        /// <summary>
        /// Velocity threshold below which objects are considered for sleep
        /// </summary>
        public float VelocityThreshold
        {
            get { return _velocityThreshold; }
            set { _velocityThreshold = Math.Max(0.001f, value); }
        }
        
        /// <summary>
        /// Process an object for sleep optimization
        /// </summary>
        public void ProcessObject(ISleepableObject obj, float timeStep)
        {
            if (!_enabled || obj == null || !obj.CanSleep)
                return;
            
            using (PhysicsProfiler.StartTiming("SleepOptimization.ProcessObject"))
            {
                lock (_lockObject)
                {
                    _totalObjectsProcessed++;
                    _sleepChecks++;
                    
                    var velocity = obj.GetVelocity();
                    var angularVelocity = obj.GetAngularVelocity();
                    
                    bool isMoving = velocity.LengthSquared() > _velocityThreshold * _velocityThreshold ||
                                   angularVelocity.LengthSquared() > _velocityThreshold * _velocityThreshold;
                    
                    if (obj.IsSleeping)
                    {
                        // Object is already sleeping, check if it should wake up
                        if (isMoving || obj.HasExternalForces())
                        {
                            WakeUpObject(obj);
                        }
                    }
                    else
                    {
                        // Object is awake, check if it should sleep
                        if (!isMoving && !obj.HasExternalForces())
                        {
                            // Object is stationary, add to sleep candidates or update existing candidate
                            var candidate = _candidates.GetOrAdd(obj.LocalID, 
                                new SleepCandidate(obj, DateTime.UtcNow));
                            
                            if (candidate.Object != obj)
                            {
                                // Object ID was reused, update the candidate
                                candidate.Object = obj;
                                candidate.StartTime = DateTime.UtcNow;
                            }
                            
                            // Check if object has been stationary long enough
                            if ((DateTime.UtcNow - candidate.StartTime).TotalSeconds >= _sleepTimeout)
                            {
                                PutObjectToSleep(obj);
                                _candidates.TryRemove(obj.LocalID, out _);
                            }
                        }
                        else
                        {
                            // Object is moving, remove from sleep candidates
                            _candidates.TryRemove(obj.LocalID, out _);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Wake up an object
        /// </summary>
        public void WakeUpObject(ISleepableObject obj)
        {
            if (obj == null) return;
            
            lock (_lockObject)
            {
                if (_sleepingObjects.TryRemove(obj.LocalID, out _))
                {
                    obj.WakeUp();
                    _objectsWokenUp++;
                    
                    DetailLog("{0},WakeUpObject,id={1}", LogHeader, obj.LocalID);
                }
                
                // Also remove from candidates if present
                _candidates.TryRemove(obj.LocalID, out _);
            }
        }
        
        /// <summary>
        /// Put an object to sleep
        /// </summary>
        private void PutObjectToSleep(ISleepableObject obj)
        {
            if (obj == null || obj.IsSleeping) return;
            
            _sleepingObjects.TryAdd(obj.LocalID, obj);
            obj.Sleep();
            _objectsPutToSleep++;
            
            DetailLog("{0},PutObjectToSleep,id={1}", LogHeader, obj.LocalID);
        }
        
        /// <summary>
        /// Remove an object from sleep management
        /// </summary>
        public void RemoveObject(uint localID)
        {
            lock (_lockObject)
            {
                _candidates.TryRemove(localID, out _);
                _sleepingObjects.TryRemove(localID, out _);
            }
        }
        
        /// <summary>
        /// Get statistics about sleep optimization
        /// </summary>
        public SleepOptimizationStatistics GetStatistics()
        {
            lock (_lockObject)
            {
                return new SleepOptimizationStatistics
                {
                    TotalObjectsProcessed = _totalObjectsProcessed,
                    ObjectsPutToSleep = _objectsPutToSleep,
                    ObjectsWokenUp = _objectsWokenUp,
                    CurrentSleepingObjects = _sleepingObjects.Count,
                    CurrentSleepCandidates = _candidates.Count,
                    SleepChecks = _sleepChecks
                };
            }
        }
        
        /// <summary>
        /// Clear all sleep data
        /// </summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                // Wake up all sleeping objects
                foreach (var obj in _sleepingObjects.Values)
                {
                    obj.WakeUp();
                }
                
                _candidates.Clear();
                _sleepingObjects.Clear();
            }
        }
        
        public PoolStatistics GetPoolStatistics()
        {
            lock (_lockObject)
            {
                var efficiencyPercent = _totalObjectsProcessed > 0 ? 
                    (_objectsPutToSleep * 100.0) / _totalObjectsProcessed : 0;
                
                return new PoolStatistics
                {
                    CurrentCount = _sleepingObjects.Count,
                    MaxPoolSize = int.MaxValue,
                    TotalCreated = _objectsPutToSleep,
                    TotalReused = _objectsWokenUp,
                    TotalReturned = _sleepChecks,
                    ReusePercentage = efficiencyPercent
                };
            }
        }
        
        private void DetailLog(string msg, params object[] args)
        {
            if (m_log.IsDebugEnabled)
                m_log.DebugFormat(msg, args);
        }
    }
    
    /// <summary>
    /// Candidate object for sleep optimization
    /// </summary>
    internal class SleepCandidate
    {
        public ISleepableObject Object { get; set; }
        public DateTime StartTime { get; set; }
        
        public SleepCandidate(ISleepableObject obj, DateTime startTime)
        {
            Object = obj;
            StartTime = startTime;
        }
    }
    
    /// <summary>
    /// Interface for objects that can be put to sleep for optimization
    /// </summary>
    public interface ISleepableObject
    {
        uint LocalID { get; }
        bool CanSleep { get; }
        bool IsSleeping { get; }
        
        Vector3 GetVelocity();
        Vector3 GetAngularVelocity();
        bool HasExternalForces();
        
        void Sleep();
        void WakeUp();
    }
    
    /// <summary>
    /// Statistics for sleep optimization performance
    /// </summary>
    public struct SleepOptimizationStatistics
    {
        public long TotalObjectsProcessed;
        public long ObjectsPutToSleep;
        public long ObjectsWokenUp;
        public int CurrentSleepingObjects;
        public int CurrentSleepCandidates;
        public long SleepChecks;
        
        public double SleepEfficiencyPercent => TotalObjectsProcessed > 0 ? 
            (ObjectsPutToSleep * 100.0) / TotalObjectsProcessed : 0;
        
        public override string ToString()
        {
            return $"Sleep[Sleeping: {CurrentSleepingObjects}, Candidates: {CurrentSleepCandidates}, " +
                   $"Efficiency: {SleepEfficiencyPercent:F1}%, Checks: {SleepChecks}]";
        }
    }
}