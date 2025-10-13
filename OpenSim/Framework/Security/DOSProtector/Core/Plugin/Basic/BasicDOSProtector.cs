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
using System.Threading;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Security.DOSProtector.SDK;


namespace OpenSim.Framework.Security.DOSProtector.Core.Plugin.Basic
{

    
    public class BasicDosProtectorOptions : BaseDosProtectorOptions
    {
        public int MaxRequestsInTimeframe { get; set; }
        public TimeSpan RequestTimeSpan { get; set; }
        public TimeSpan ForgetTimeSpan { get; set; }
        public bool AllowXForwardedFor { get; set; }
        public new string ReportingName { get; set; } = "BASICDOSPROTECTOR";
        public new ThrottleAction ThrottledAction { get; set; } = ThrottleAction.DoThrottledMethod;
        public int MaxConcurrentSessions { get; set; }
        
    }
    
    
    [DOSProtectorOptions(typeof(BasicDosProtectorOptions))]
    public class BasicDOSProtector : BaseDOSProtector
    {
        
        // General request checker
        private readonly CircularBuffer<int> _generalRequestTimes;

        // Per client request checker
        private readonly Dictionary<string, CircularBuffer<int>> _deeperInspection;

        // Track last access time for TTL-based cleanup
        private readonly Dictionary<string, int> _deeperInspectionLastAccess;

        // Blocked list
        private readonly Dictionary<string, int> _tempBlocked;

        // Active session counter
        private readonly Dictionary<string, int> _sessions;

        // Cleanup timer
        private readonly System.Timers.Timer _forgetTimer;

        // Lock order: Always acquire in this order to prevent deadlocks:
        // 1. _deeperInspection (monitor lock)
        // 2. _blockLockSlim
        // 3. _sessionLockSlim
        // 4. _generalRequestLock
        private readonly ReaderWriterLockSlim _blockLockSlim = new();
        private readonly ReaderWriterLockSlim _sessionLockSlim = new();
        private readonly object _generalRequestLock = new();
        private readonly BasicDosProtectorOptions _basicOptions;

        private bool _disposed;
        
        public BasicDOSProtector(BasicDosProtectorOptions options) 
            : base(options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _basicOptions = options;
            _generalRequestTimes = new CircularBuffer<int>(options.MaxRequestsInTimeframe + 1, true);
            _generalRequestTimes.Put(0);
            _deeperInspection = new Dictionary<string, CircularBuffer<int>>();
            _deeperInspectionLastAccess = new Dictionary<string, int>();
            _tempBlocked = new Dictionary<string, int>();
            _sessions = new Dictionary<string, int>();
            _forgetTimer = new System.Timers.Timer();
            _forgetTimer.Elapsed += delegate
            {
                List<string> removes = [];
                List<string> staleInspections = [];

                // Find expired blocks
                _blockLockSlim.EnterReadLock();
                try
                {
                    var now = Util.EnvironmentTickCount();
                    foreach (var kvp in _tempBlocked)
                    {
                        // Expired if now >= expiry (kvp.Value - now <= 0)
                        if (Util.EnvironmentTickCountSubtract(kvp.Value, now) <= 0)
                            removes.Add(kvp.Key);
                    }
                }
                finally
                {
                    _blockLockSlim.ExitReadLock();
                }

                // Find stale inspection entries (not blocked, but inactive)
                lock (_deeperInspection)
                {
                    var now = Util.EnvironmentTickCount();
                    int ttl = (int)Math.Min(_options.InspectionTTL.TotalMilliseconds, int.MaxValue / 2);

                    foreach (var kvp in _deeperInspectionLastAccess)
                    {
                        // Skip if client is currently blocked
                        bool isBlocked = false;
                        _blockLockSlim.EnterReadLock();
                        try
                        {
                            isBlocked = _tempBlocked.ContainsKey(kvp.Key);
                        }
                        finally
                        {
                            _blockLockSlim.ExitReadLock();
                        }

                        if (!isBlocked && Util.EnvironmentTickCountSubtract(now, kvp.Value) > ttl)
                        {
                            staleInspections.Add(kvp.Key);
                        }
                    }

                    // Remove stale inspections
                    if (staleInspections.Count > 0)
                    {
                        foreach (var key in staleInspections)
                        {
                            _deeperInspection.Remove(key);
                            _deeperInspectionLastAccess.Remove(key);
                        }
                        Log(DOSProtectorLogLevel.Debug, $"[{_options.ReportingName}] Cleaned up {staleInspections.Count} stale inspection entries.");
                    }
                }

                // Remove expired entries
                if (removes.Count > 0)
                {
                    lock (_deeperInspection)
                    {
                        _blockLockSlim.EnterWriteLock();
                        try
                        {
                            foreach (var t in removes)
                            {
                                _tempBlocked.Remove(t);
                                _deeperInspection.Remove(t);
                                _deeperInspectionLastAccess.Remove(t);
                            }
                        }
                        finally
                        {
                            _blockLockSlim.ExitWriteLock();
                        }
                    }

                    _sessionLockSlim.EnterWriteLock();
                    try
                    {
                        foreach (var t in removes)
                        {
                            _sessions.Remove(t);
                        }
                    }
                    finally
                    {
                        _sessionLockSlim.ExitWriteLock();
                    }

                    foreach (var str in removes)
                    {
                        Log(DOSProtectorLogLevel.Info, $"[{_options.ReportingName}] client: {RedactClient(str)} is no longer blocked.");
                    }
                }

                // Restart timer if there are still blocked clients or tracked inspections
                _blockLockSlim.EnterReadLock();
                try
                {
                    lock (_deeperInspection)
                    {
                        if (_tempBlocked.Count > 0 || _deeperInspection.Count > 0)
                            _forgetTimer.Enabled = true;
                    }
                }
                finally
                {
                    _blockLockSlim.ExitReadLock();
                }
            };

            _forgetTimer.Interval = _basicOptions.ForgetTimeSpan.TotalMilliseconds;
            _forgetTimer.AutoReset = false;
        }

        /// <summary>
        /// Given a string Key, Returns if that context is blocked
        /// </summary>
        /// <param name="key">A Key identifying the context</param>
        /// <param name="context">Optional context data for decision making</param>
        /// <returns>bool Yes or No, True or False for blocked</returns>
        public override bool IsBlocked(string key, IDOSProtectorContext context = null)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            _blockLockSlim.EnterReadLock();
            try
            {
                return _tempBlocked.ContainsKey(key);
            }
            finally
            {
                _blockLockSlim.ExitReadLock();
            }
        }

        /// <summary>
        /// Process the velocity of this context
        /// </summary>
        /// <param name="key"></param>
        /// <param name="endpoint"></param>
        /// <param name="context">Optional context data for decision making</param>
        /// <returns></returns>
        public override bool Process(string key, string endpoint, IDOSProtectorContext context = null)
        {
            if (_basicOptions.MaxRequestsInTimeframe < 1 || _basicOptions.RequestTimeSpan.TotalMilliseconds < 1)
                return true;

            if (string.IsNullOrEmpty(key))
                return true;

            var clientstring = key;

            // Check if already blocked
            _blockLockSlim.EnterReadLock();
            try
            {
                if (_tempBlocked.ContainsKey(clientstring))
                {
                    return _options.ThrottledAction == ThrottleAction.DoThrottledMethod 
                        ? false 
                        : throw new System.Security.SecurityException("Throttled");
                }
            }
            finally
            {
                _blockLockSlim.ExitReadLock();
            }

            // Track general request rate
            lock (_generalRequestLock)
                _generalRequestTimes.Put(Util.EnvironmentTickCount());

            // Check concurrent sessions if enabled
            if (_basicOptions.MaxConcurrentSessions > 0)
            {
                var sessionCount = 0;
                _sessionLockSlim.EnterReadLock();
                try
                {
                    _sessions.TryGetValue(key, out sessionCount);
                }
                finally
                {
                    _sessionLockSlim.ExitReadLock();
                }

                if (sessionCount >= _basicOptions.MaxConcurrentSessions)
                {
                    // Add to blocking and cleanup methods
                    lock (_deeperInspection)
                    {
                        _blockLockSlim.EnterWriteLock();
                        try
                        {
                            int blockDuration = (int)Math.Min(_basicOptions.ForgetTimeSpan.TotalMilliseconds, int.MaxValue / 2);
                            if (!_tempBlocked.ContainsKey(clientstring))
                            {
                                _tempBlocked.Add(clientstring,
                                                 Util.EnvironmentTickCount() + blockDuration);

                                _forgetTimer.Enabled = true;

                                Log(DOSProtectorLogLevel.Warn, $"[{_options.ReportingName}]: client: {RedactClient(clientstring)} is blocked for {_basicOptions.ForgetTimeSpan.TotalMilliseconds}ms based on concurrency, X-ForwardedForAllowed status is {_basicOptions.AllowXForwardedFor}, endpoint:{endpoint}");
                            }
                            else
                                _tempBlocked[clientstring] = Util.EnvironmentTickCount() + blockDuration;
                        }
                        finally
                        {
                            _blockLockSlim.ExitWriteLock();
                        }
                    }

                    return _options.ThrottledAction == ThrottleAction.DoThrottledMethod
                        ? false
                        : throw new System.Security.SecurityException("Throttled");
                }
                else
                    ProcessConcurrency(key, endpoint);
            }

            // Check if general rate limit exceeded
            bool generalLimitExceeded;
            lock (_generalRequestLock)
            {
                generalLimitExceeded = _generalRequestTimes.Size == _generalRequestTimes.Capacity &&
                    (Util.EnvironmentTickCountSubtract(Util.EnvironmentTickCount(), _generalRequestTimes.Get()) <
                     _basicOptions.RequestTimeSpan.TotalMilliseconds);
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
            _sessionLockSlim.EnterWriteLock();
            try
            {
                if (_sessions.TryGetValue(key, out int count))
                    _sessions[key] = count + 1;
                else
                    _sessions.Add(key, 1);
            }
            finally
            {
                _sessionLockSlim.ExitWriteLock();
            }
        }

        public override void ProcessEnd(string key, string endpoint, IDOSProtectorContext context = null)
        {
            if (string.IsNullOrEmpty(key))
                return;

            _sessionLockSlim.EnterWriteLock();
            try
            {
                if (_sessions.TryGetValue(key, out int count))
                {
                    count--;
                    if (count <= 0)
                        _sessions.Remove(key);
                    else
                        _sessions[key] = count;
                }
            }
            finally
            {
                _sessionLockSlim.ExitWriteLock();
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
            lock (_deeperInspection)
            {
                string clientstring = key;
                int now = Util.EnvironmentTickCount();

                if (_deeperInspection.ContainsKey(clientstring))
                {
                    _deeperInspection[clientstring].Put(now);
                    _deeperInspectionLastAccess[clientstring] = now;

                    if (_deeperInspection[clientstring].Size == _deeperInspection[clientstring].Capacity &&
                        (Util.EnvironmentTickCountSubtract(now, _deeperInspection[clientstring].Get()) <
                         _basicOptions.RequestTimeSpan.TotalMilliseconds))
                    {
                        // Looks like we're over the limit
                        _blockLockSlim.EnterWriteLock();
                        try
                        {
                            int blockDuration = (int)Math.Min(_basicOptions.ForgetTimeSpan.TotalMilliseconds, int.MaxValue / 2);
                            if (!_tempBlocked.ContainsKey(clientstring))
                            {
                                _tempBlocked.Add(clientstring, now + blockDuration);
                                _forgetTimer.Enabled = true;
                            }
                            else
                                _tempBlocked[clientstring] = now + blockDuration;
                        }
                        finally
                        {
                            _blockLockSlim.ExitWriteLock();
                        }

                        Log(DOSProtectorLogLevel.Warn, $"[{_options.ReportingName}]: client: {RedactClient(clientstring)} is blocked for {_basicOptions.ForgetTimeSpan.TotalMilliseconds}ms, X-ForwardedForAllowed status is {_basicOptions.AllowXForwardedFor}, endpoint:{endpoint}");
                        return false;
                    }
                }
                else
                {
                    _deeperInspection.Add(clientstring, new CircularBuffer<int>(_basicOptions.MaxRequestsInTimeframe + 1, true));
                    _deeperInspection[clientstring].Put(now);
                    _deeperInspectionLastAccess[clientstring] = now;

                    // Start timer if this is the first inspection entry
                    if (_deeperInspection.Count == 1)
                        _forgetTimer.Enabled = true;
                }
            }
            return true;
        }

        /// <summary>
        /// Creates a disposable session scope that automatically calls ProcessEnd when disposed.
        /// Use with 'using' statement to ensure ProcessEnd is always called.
        /// </summary>
        public override IDisposable CreateSession(string key, string endpoint, IDOSProtectorContext context = null)
        {
            ProcessConcurrency(key, endpoint);
            return new SessionScope(this, key, endpoint, context);
        }

        public override void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            _forgetTimer?.Stop();
            _forgetTimer?.Dispose();

            _blockLockSlim?.Dispose();
            _sessionLockSlim?.Dispose();
        }

    }
    
}