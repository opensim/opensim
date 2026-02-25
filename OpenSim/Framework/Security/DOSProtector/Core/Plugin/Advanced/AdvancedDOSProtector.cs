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

namespace OpenSim.Framework.Security.DOSProtector.Core.Plugin.Advanced
{
    
    /// <summary>
    /// Advanced configuration options for AdvancedDOSProtector
    /// Extends BasicDosProtectorOptions with additional security features
    /// </summary>
    public class AdvancedDosProtectorOptions : BaseDosProtectorOptions
    {
        public int MaxRequestsInTimeframe { get; set; }
        public TimeSpan RequestTimeSpan { get; set; }
        public TimeSpan ForgetTimeSpan { get; set; }
        public bool AllowXForwardedFor { get; set; }
        public new string ReportingName { get; set; } = "ADVANCEDDOSPROTECTOR";
        public new ThrottleAction ThrottledAction { get; set; } = ThrottleAction.DoThrottledMethod;
        public int MaxConcurrentSessions { get; set; }
        
        /// <summary>
        /// Enable block extension limiting to prevent permanent blocks.
        /// When enabled, uses MaxBlockExtensions and/or MaxTotalBlockDuration.
        /// Default: false (preserves existing unlimited extension behavior).
        /// </summary>
        public bool LimitBlockExtensions { get; set; } = false;

        /// <summary>
        /// Maximum number of times a block can be extended.
        /// Only applies when LimitBlockExtensions = true.
        /// 0 = no limit on extension count (only duration matters).
        /// Default: 3
        /// </summary>
        public int MaxBlockExtensions { get; set; } = 3;

        /// <summary>
        /// Maximum total duration a client can remain blocked.
        /// Only applies when LimitBlockExtensions = true.
        /// TimeSpan.Zero = no duration limit (only extension count matters).
        /// Default: 1 hour
        /// </summary>
        public TimeSpan MaxTotalBlockDuration { get; set; } = TimeSpan.FromHours(1);
    }
    
    
    /// <summary>
    /// Advanced DOS protection with extended features:
    /// - Inherits all BasicDOSProtector functionality (memory leak fix, TTL cleanup, etc.)
    /// - Adds block extension limiting to prevent permanent blocks
    /// - Configurable via AdvancedDosProtectorOptions
    /// </summary>
    [DOSProtectorOptions(typeof(AdvancedDosProtectorOptions))]
    public class AdvancedDOSProtector : BaseDOSProtector
    {
        // General request checker
        private readonly CircularBuffer<int> _generalRequestTimes;
        private readonly AdvancedDosProtectorOptions _advancedOptions;

        // Per client request checker
        private readonly Dictionary<string, CircularBuffer<int>> _deeperInspection;

        // Track last access time for TTL-based cleanup
        private readonly Dictionary<string, int> _deeperInspectionLastAccess;

        // Blocked list
        private readonly Dictionary<string, int> _tempBlocked;

        // Block extension tracking (for LimitBlockExtensions feature)
        private readonly Dictionary<string, int> _blockStartTimes;
        private readonly Dictionary<string, int> _blockExtensionCounts;

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

        private bool _disposed;

        public AdvancedDOSProtector(AdvancedDosProtectorOptions options) 
            : base(options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _advancedOptions = options;
            _generalRequestTimes = new CircularBuffer<int>(options.MaxRequestsInTimeframe + 1, true);
            _generalRequestTimes.Put(0);
            _deeperInspection = new Dictionary<string, CircularBuffer<int>>();
            _deeperInspectionLastAccess = new Dictionary<string, int>();
            _tempBlocked = new Dictionary<string, int>();
            _blockStartTimes = new Dictionary<string, int>();
            _blockExtensionCounts = new Dictionary<string, int>();
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
                                _blockStartTimes.Remove(t);
                                _blockExtensionCounts.Remove(t);
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

            _forgetTimer.Interval = _advancedOptions.ForgetTimeSpan.TotalMilliseconds;
            _forgetTimer.AutoReset = false;
        }

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

        public override bool Process(string key, string endpoint, IDOSProtectorContext context = null)
        {
            if (_advancedOptions.MaxRequestsInTimeframe < 1 || _advancedOptions.RequestTimeSpan.TotalMilliseconds < 1)
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
            if (_advancedOptions.MaxConcurrentSessions > 0)
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

                if (sessionCount >= _advancedOptions.MaxConcurrentSessions)
                {
                    // Add to blocking with extension limit check
                    lock (_deeperInspection)
                    {
                        _blockLockSlim.EnterWriteLock();
                        try
                        {
                            int now = Util.EnvironmentTickCount();
                            int blockDuration = (int)Math.Min(_advancedOptions.ForgetTimeSpan.TotalMilliseconds, int.MaxValue / 2);
                            bool isNewBlock = !_tempBlocked.ContainsKey(clientstring);

                            if (isNewBlock)
                            {
                                // New block - always add
                                _tempBlocked.Add(clientstring, now + blockDuration);
                                _blockStartTimes[clientstring] = now;
                                _blockExtensionCounts[clientstring] = 0;
                                _forgetTimer.Enabled = true;

                                Log(DOSProtectorLogLevel.Warn, $"[{_options.ReportingName}]: client: {RedactClient(clientstring)} is blocked for {_advancedOptions.ForgetTimeSpan.TotalMilliseconds}ms based on concurrency, X-ForwardedForAllowed status is {_advancedOptions.AllowXForwardedFor}, endpoint:{endpoint}");
                            }
                            else
                            {
                                // Existing block - check if extension is allowed
                                if (TryExtendBlock(clientstring, now, blockDuration))
                                {
                                    _tempBlocked[clientstring] = now + blockDuration;
                                }
                            }
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
                     _advancedOptions.RequestTimeSpan.TotalMilliseconds);
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
                         _advancedOptions.RequestTimeSpan.TotalMilliseconds))
                    {
                        // Looks like we're over the limit
                        _blockLockSlim.EnterWriteLock();
                        try
                        {
                            int blockDuration = (int)Math.Min(_advancedOptions.ForgetTimeSpan.TotalMilliseconds, int.MaxValue / 2);
                            bool isNewBlock = !_tempBlocked.ContainsKey(clientstring);

                            if (isNewBlock)
                            {
                                // New block - always add
                                _tempBlocked.Add(clientstring, now + blockDuration);
                                _blockStartTimes[clientstring] = now;
                                _blockExtensionCounts[clientstring] = 0;
                                _forgetTimer.Enabled = true;
                            }
                            else
                            {
                                // Existing block - check if extension is allowed
                                if (TryExtendBlock(clientstring, now, blockDuration))
                                {
                                    _tempBlocked[clientstring] = now + blockDuration;
                                }
                            }
                        }
                        finally
                        {
                            _blockLockSlim.ExitWriteLock();
                        }

                        Log(DOSProtectorLogLevel.Warn, $"[{_options.ReportingName}]: client: {RedactClient(clientstring)} is blocked for {_advancedOptions.ForgetTimeSpan.TotalMilliseconds}ms, X-ForwardedForAllowed status is {_advancedOptions.AllowXForwardedFor}, endpoint:{endpoint}");
                        return false;
                    }
                }
                else
                {
                    _deeperInspection.Add(clientstring, new CircularBuffer<int>(_advancedOptions.MaxRequestsInTimeframe + 1, true));
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
        /// Check if block extension is allowed based on configured limits
        /// </summary>
        /// <returns>True if extension is allowed, false otherwise</returns>
        private bool TryExtendBlock(string clientstring, int now, int blockDuration)
        {
            if (!_advancedOptions.LimitBlockExtensions)
                return true; // Feature disabled, always allow

            // Check extension count limit
            if (_advancedOptions.MaxBlockExtensions > 0)
            {
                int currentExtensions = _blockExtensionCounts.GetValueOrDefault(clientstring, 0);
                if (currentExtensions >= _advancedOptions.MaxBlockExtensions)
                {
                    Log(DOSProtectorLogLevel.Info, $"[{_options.ReportingName}]: client: {RedactClient(clientstring)} reached max block extensions ({_advancedOptions.MaxBlockExtensions}), not extending block");
                    return false;
                }
            }

            // Check total duration limit
            if (_advancedOptions.MaxTotalBlockDuration > TimeSpan.Zero)
            {
                int blockStartTime = _blockStartTimes.GetValueOrDefault(clientstring, now);
                int totalBlockedMs = Util.EnvironmentTickCountSubtract(now, blockStartTime);

                if (totalBlockedMs >= _advancedOptions.MaxTotalBlockDuration.TotalMilliseconds)
                {
                    Log(DOSProtectorLogLevel.Info, $"[{_options.ReportingName}]: client: {RedactClient(clientstring)} reached max total block duration ({_advancedOptions.MaxTotalBlockDuration.TotalMilliseconds}ms), not extending block");
                    return false;
                }
            }

            // Extension allowed - increment counter
            _blockExtensionCounts[clientstring] = _blockExtensionCounts.GetValueOrDefault(clientstring, 0) + 1;
            return true;
        }

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
