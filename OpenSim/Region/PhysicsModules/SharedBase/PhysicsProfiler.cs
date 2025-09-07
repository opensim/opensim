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
using System.Diagnostics;
using log4net;

namespace OpenSim.Region.PhysicsModules.SharedBase
{
    /// <summary>
    /// Physics performance profiler to track timing and resource usage
    /// </summary>
    public static class PhysicsProfiler
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly string LogHeader = "[PHYSICS PROFILER]";
        
        private static readonly ConcurrentDictionary<string, PerformanceMetrics> _metrics = new ConcurrentDictionary<string, PerformanceMetrics>();
        private static readonly object _lockObject = new object();
        private static DateTime _lastReport = DateTime.UtcNow;
        private static bool _enabled = false;
        
        public static bool Enabled 
        { 
            get { return _enabled; } 
            set { _enabled = value; }
        }
        
        public static int ReportIntervalSeconds { get; set; } = 30;
        
        /// <summary>
        /// Start timing an operation
        /// </summary>
        public static IDisposable StartTiming(string operation)
        {
            if (!_enabled) return new NullTimer();
            
            return new PerformanceTimer(operation);
        }
        
        /// <summary>
        /// Record a timing manually
        /// </summary>
        public static void RecordTiming(string operation, double milliseconds)
        {
            if (!_enabled) return;
            
            _metrics.AddOrUpdate(operation, 
                new PerformanceMetrics { TotalTime = milliseconds, CallCount = 1, MaxTime = milliseconds, MinTime = milliseconds },
                (key, existing) => 
                {
                    lock (existing)
                    {
                        existing.TotalTime += milliseconds;
                        existing.CallCount++;
                        existing.MaxTime = Math.Max(existing.MaxTime, milliseconds);
                        existing.MinTime = Math.Min(existing.MinTime, milliseconds);
                        return existing;
                    }
                });
            
            CheckReporting();
        }
        
        /// <summary>
        /// Get current performance metrics
        /// </summary>
        public static Dictionary<string, PerformanceMetrics> GetMetrics()
        {
            var result = new Dictionary<string, PerformanceMetrics>();
            foreach (var kvp in _metrics)
            {
                lock (kvp.Value)
                {
                    result[kvp.Key] = new PerformanceMetrics
                    {
                        TotalTime = kvp.Value.TotalTime,
                        CallCount = kvp.Value.CallCount,
                        MaxTime = kvp.Value.MaxTime,
                        MinTime = kvp.Value.MinTime
                    };
                }
            }
            return result;
        }
        
        /// <summary>
        /// Reset all metrics
        /// </summary>
        public static void Reset()
        {
            _metrics.Clear();
            _lastReport = DateTime.UtcNow;
        }
        
        private static void CheckReporting()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastReport).TotalSeconds >= ReportIntervalSeconds)
            {
                lock (_lockObject)
                {
                    if ((now - _lastReport).TotalSeconds >= ReportIntervalSeconds)
                    {
                        ReportMetrics();
                        _lastReport = now;
                    }
                }
            }
        }
        
        private static void ReportMetrics()
        {
            if (_metrics.Count == 0) return;
            
            m_log.InfoFormat("{0} Performance Report:", LogHeader);
            m_log.InfoFormat("{0} {1,-30} {2,8} {3,8} {4,8} {5,8}", LogHeader, "Operation", "Count", "Total", "Avg", "Max");
            
            foreach (var kvp in _metrics)
            {
                lock (kvp.Value)
                {
                    var metrics = kvp.Value;
                    var avgTime = metrics.CallCount > 0 ? metrics.TotalTime / metrics.CallCount : 0;
                    m_log.InfoFormat("{0} {1,-30} {2,8:N0} {3,8:F1} {4,8:F1} {5,8:F1}", 
                        LogHeader, kvp.Key, metrics.CallCount, metrics.TotalTime, avgTime, metrics.MaxTime);
                }
            }
        }
        
        private class PerformanceTimer : IDisposable
        {
            private readonly string _operation;
            private readonly Stopwatch _stopwatch;
            
            public PerformanceTimer(string operation)
            {
                _operation = operation;
                _stopwatch = Stopwatch.StartNew();
            }
            
            public void Dispose()
            {
                _stopwatch.Stop();
                RecordTiming(_operation, _stopwatch.Elapsed.TotalMilliseconds);
            }
        }
        
        private class NullTimer : IDisposable
        {
            public void Dispose() { }
        }
        
        public class PerformanceMetrics
        {
            public double TotalTime { get; set; }
            public long CallCount { get; set; }
            public double MaxTime { get; set; }
            public double MinTime { get; set; }
        }
    }
}