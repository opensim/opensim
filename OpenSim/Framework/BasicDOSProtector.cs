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
using System.Reflection;
using log4net;

namespace OpenSim.Framework
{

    public class BasicDOSProtector : IDisposable
    {
        public enum ThrottleAction
        {
            DoThrottledMethod,
            DoThrow
        }

        // Thread-safe collections
        private readonly CircularBuffer<int> _generalRequestTimes;
        private readonly ConcurrentDictionary<string, CircularBuffer<int>> _deeperInspection;
        private readonly ConcurrentDictionary<string, int> _tempBlocked;
        private readonly ConcurrentDictionary<string, int> _sessions;
        
        private readonly BasicDosProtectorOptions _options;
        private readonly System.Timers.Timer _forgetTimer;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);
        
        // Single lock for _generalRequestTimes since it's shared across all requests
        private readonly object _generalRequestLock = new();
        
        private volatile bool _disposed = false;

        public BasicDOSProtector(BasicDosProtectorOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _options = options;
            
            _generalRequestTimes = new CircularBuffer<int>(options.MaxRequestsInTimeframe + 1, true);
            _generalRequestTimes.Put(0);
            
            _deeperInspection = new ConcurrentDictionary<string, CircularBuffer<int>>();
            _tempBlocked = new ConcurrentDictionary<string, int>();
            _sessions = new ConcurrentDictionary<string, int>();
            
            _forgetTimer = new System.Timers.Timer();
            _forgetTimer.Elapsed += OnForgetTimerElapsed;
            _forgetTimer.Interval = _options.ForgetTimeSpan.TotalMilliseconds;
            _forgetTimer.AutoReset = false; // Manual restart for efficiency
        }

        private void OnForgetTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (_disposed)
                return;

            try
            {
                var now = Util.EnvironmentTickCount();
                List<string> toRemove = [];

                // Find expired blocks
                foreach (var kvp in _tempBlocked)
                {
                    if (Util.EnvironmentTickCountSubtract(now, kvp.Value) > 0)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }

                // Remove expired entries
                foreach (var key in toRemove)
                {
                    if (!_tempBlocked.TryRemove(key, out _)) 
                        continue;
                    
                    _deeperInspection.TryRemove(key, out _);
                    _sessions.TryRemove(key, out _);
                        
                    m_log.Info($"[{_options.ReportingName}] client: {key} is no longer blocked.");
                }

                // Restart timer if there are still blocked clients
                if (_tempBlocked.Count > 0)
                {
                    _forgetTimer.Start();
                }
            }
            catch (Exception ex)
            {
                m_log.Error($"[{_options.ReportingName}] Error in forget timer: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Given a string Key, Returns if that context is blocked
        /// </summary>
        /// <param name="key">A Key identifying the context</param>
        /// <returns>bool Yes or No, True or False for blocked</returns>
        public bool IsBlocked(string key)
        {
            return !string.IsNullOrEmpty(key) && _tempBlocked.ContainsKey(key);
        }

        /// <summary>
        /// Process the velocity of this context
        /// </summary>
        /// <param name="key"></param>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        public bool Process(string key, string endpoint)
        {
            if (_options.MaxRequestsInTimeframe < 1 || _options.RequestTimeSpan.TotalMilliseconds < 1)
                return true;

            if (string.IsNullOrEmpty(key))
                return true;

            var clientstring = key;

            // Check if already blocked
            if (_tempBlocked.ContainsKey(clientstring))
            {
                return _options.ThrottledAction == ThrottleAction.DoThrottledMethod ? false : throw new System.Security.SecurityException("Throttled");
            }

            // Track general request rate
            lock (_generalRequestLock)
            {
                _generalRequestTimes.Put(Util.EnvironmentTickCount());
            }

            // Check concurrent sessions
            if (_options.MaxConcurrentSessions > 0)
            {
                var sessionCount = _sessions.GetOrAdd(key, 0);
                
                if (sessionCount > _options.MaxConcurrentSessions)
                {
                    BlockClient(clientstring, endpoint, "concurrency");

                    return _options.ThrottledAction == ThrottleAction.DoThrottledMethod ? false : throw new System.Security.SecurityException("Throttled");
                }
                else
                {
                    ProcessConcurrency(key, endpoint);
                }
            }

            // Check if general rate limit exceeded
            bool generalLimitExceeded;
            lock (_generalRequestLock)
            {
                generalLimitExceeded = _generalRequestTimes.Size == _generalRequestTimes.Capacity && 
                                       (Util.EnvironmentTickCountSubtract(Util.EnvironmentTickCount(), _generalRequestTimes.Get()) <
                                           _options.RequestTimeSpan.TotalMilliseconds);
            }

            if (generalLimitExceeded)
            {
                // Trigger deeper inspection
                if (!DeeperInspection(key, endpoint))
                {
                    return _options.ThrottledAction == ThrottleAction.DoThrottledMethod 
                        ? false 
                        : throw new System.Security.SecurityException("Throttled");
                }
            }

            return true;
        }

        private void ProcessConcurrency(string key, string endpoint)
        {
            _sessions.AddOrUpdate(key, 1, (k, oldValue) => oldValue + 1);
        }

        public void ProcessEnd(string key, string endpoint)
        {
            if (string.IsNullOrEmpty(key))
                return;

            _sessions.AddOrUpdate(key, 
                0, // If key doesn't exist, set to 0 (shouldn't happen in normal flow)
                (k, oldValue) =>
                {
                    var newValue = oldValue - 1;
                    return newValue < 0 ? 0 : newValue; // Prevent negative values
                });

            // Remove if zero
            if (_sessions.TryGetValue(key, out var count) && count <= 0)
            {
                _sessions.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// At this point, the rate limiting code needs to track 'per user' velocity.
        /// </summary>
        /// <param name="key">Context Key, string representing a rate limiting context</param>
        /// <param name="endpoint"></param>
        /// <returns></returns>
        private bool DeeperInspection(string key, string endpoint)
        {
            int now = Util.EnvironmentTickCount();

            // Get or create client buffer
            CircularBuffer<int> clientBuffer = _deeperInspection.GetOrAdd(key, k =>
            {
                var buffer = new CircularBuffer<int>(_options.MaxRequestsInTimeframe + 1, true);
                
                // Start timer on first deeper inspection entry
                if (_deeperInspection.Count == 1)
                {
                    _forgetTimer.Start();
                }
                
                return buffer;
            });

            // Thread-safe update of buffer
            lock (clientBuffer)
            {
                clientBuffer.Put(now);

                // Check if limit exceeded ! DO NOT INVERT "IF"!
                if (clientBuffer.Size == clientBuffer.Capacity &&
                    (Util.EnvironmentTickCountSubtract(now, clientBuffer.Get()) <
                     _options.RequestTimeSpan.TotalMilliseconds))
                {

                    // Block this client
                    BlockClient(key, endpoint, "rate limit");
                    return false;
                }
            }

            return true;
        }

        private void BlockClient(string key, string endpoint, string reason)
        {
            var blockUntil = Util.EnvironmentTickCount() + (int)_options.ForgetTimeSpan.TotalMilliseconds;
            
            _tempBlocked.AddOrUpdate(key, blockUntil, (k, oldValue) => blockUntil);
            
            // Ensure timer is running
            if (!_forgetTimer.Enabled)
            {
                _forgetTimer.Start();
            }

            m_log.Warn($"[{_options.ReportingName}]: client: {key} is blocked for {_options.ForgetTimeSpan.TotalMilliseconds}ms based on {reason}, X-ForwardedForAllowed status is {_options.AllowXForwardedFor}, endpoint:{endpoint}");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_forgetTimer != null)
            {
                _forgetTimer.Stop();
                _forgetTimer.Elapsed -= OnForgetTimerElapsed;
                _forgetTimer.Dispose();
            }

            _deeperInspection?.Clear();
            _tempBlocked?.Clear();
            _sessions?.Clear();
        }
    }


    public class BasicDosProtectorOptions
    {
        public int MaxRequestsInTimeframe;
        public TimeSpan RequestTimeSpan;
        public TimeSpan ForgetTimeSpan;
        public bool AllowXForwardedFor;
        public string ReportingName = "BASICDOSPROTECTOR";
        public BasicDOSProtector.ThrottleAction ThrottledAction = BasicDOSProtector.ThrottleAction.DoThrottledMethod;
        public int MaxConcurrentSessions;
    }
}