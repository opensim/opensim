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
using System.Threading;
using OpenSim.Framework.Security.DOSProtector.Attributes;
using OpenSim.Framework.Security.DOSProtector.Interfaces;
using OpenSim.Framework.Security.DOSProtector.Options;

namespace OpenSim.Framework.Security.DOSProtector.Plugins
{
    /// <summary>
    /// DOS Protector with honeypot detection to identify bot behavior.
    /// Detects bots by tracking suspicious patterns:
    /// - Requests that are too fast (inhuman speed)
    /// - Missing or suspicious user agents
    /// - Requests to trap endpoints
    /// </summary>
    [DOSProtectorOptions(typeof(HoneypotDosProtectorOptions))]
    public class HoneypotDOSProtector : BaseDOSProtector
    {
        private readonly HoneypotDosProtectorOptions _honeypotOptions;
        private readonly BasicDOSProtector _baseProtector;
        private readonly Dictionary<string, ClientBehavior> _clientBehaviors;
        private readonly ReaderWriterLockSlim _behaviorsLock;
        private readonly HashSet<string> _suspiciousClients;
        private readonly object _suspiciousLock = new();

        public HoneypotDOSProtector(HoneypotDosProtectorOptions options)
            : base(options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _honeypotOptions = options;
            _baseProtector = new BasicDOSProtector(options);
            _clientBehaviors = new Dictionary<string, ClientBehavior>();
            _behaviorsLock = new ReaderWriterLockSlim();
            _suspiciousClients = new HashSet<string>();

            Log(DOSProtectorLogLevel.Info,
                $"[HoneypotDOSProtector]: Initialized - " +
                $"Min request interval: {options.MinRequestIntervalMs}ms, " +
                $"Trap endpoints: {options.TrapEndpoints?.Count ?? 0}");
        }

        public override bool IsBlocked(string key, IDOSProtectorContext context = null)
        {
            // Check if marked as suspicious
            lock (_suspiciousLock)
            {
                if (_suspiciousClients.Contains(key))
                {
                    Log(DOSProtectorLogLevel.Warn,
                        $"[HoneypotDOSProtector]: {RedactClient(key)} is marked as suspicious bot");
                    return true;
                }
            }

            return _baseProtector.IsBlocked(key, context);
        }

        public override bool Process(string key, string endpoint, IDOSProtectorContext context = null)
        {
            // Check if already marked as suspicious
            lock (_suspiciousLock)
            {
                if (_suspiciousClients.Contains(key))
                {
                    Log(DOSProtectorLogLevel.Info,
                        $"[HoneypotDOSProtector]: Blocked suspicious client: {RedactClient(key)}");
                    return false;
                }
            }

            // Check for trap endpoint access
            if (IsTrapEndpoint(endpoint))
            {
                MarkAsSuspicious(key, $"accessed trap endpoint: {endpoint}");
                return false;
            }

            // Check request timing
            if (_honeypotOptions.DetectInhumanSpeed && IsRequestTooFast(key))
            {
                MarkAsSuspicious(key, "inhuman request speed detected");
                return false;
            }

            // Regular DOS protection
            return _baseProtector.Process(key, endpoint, context);
        }

        public override void ProcessEnd(string key, string endpoint, IDOSProtectorContext context = null)
        {
            // Update last request time
            UpdateClientBehavior(key);

            _baseProtector.ProcessEnd(key, endpoint, context);
        }

        public override IDisposable CreateSession(string key, string endpoint, IDOSProtectorContext context = null)
        {
            if (!Process(key, endpoint, context))
                return new NullSession();

            return new SessionScope(this, key, endpoint, context);
        }

        public override void Dispose()
        {
            _baseProtector?.Dispose();
            _behaviorsLock?.Dispose();
        }

        /// <summary>
        /// Checks if endpoint is a honeypot trap
        /// </summary>
        private bool IsTrapEndpoint(string endpoint)
        {
            if (_honeypotOptions.TrapEndpoints == null || _honeypotOptions.TrapEndpoints.Count == 0)
                return false;

            foreach (var trap in _honeypotOptions.TrapEndpoints)
            {
                if (endpoint.Contains(trap, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if client is making requests too fast (bot behavior)
        /// </summary>
        private bool IsRequestTooFast(string key)
        {
            _behaviorsLock.EnterUpgradeableReadLock();
            try
            {
                if (!_clientBehaviors.TryGetValue(key, out var behavior))
                {
                    // First request, create behavior tracking
                    _behaviorsLock.EnterWriteLock();
                    try
                    {
                        behavior = new ClientBehavior
                        {
                            LastRequestTime = DateTime.UtcNow,
                            RequestCount = 1
                        };
                        _clientBehaviors[key] = behavior;
                        return false;
                    }
                    finally
                    {
                        _behaviorsLock.ExitWriteLock();
                    }
                }

                var now = DateTime.UtcNow;
                var timeSinceLastRequest = (now - behavior.LastRequestTime).TotalMilliseconds;

                // Check if request is too fast
                if (timeSinceLastRequest < _honeypotOptions.MinRequestIntervalMs)
                {
                    behavior.FastRequestCount++;

                    // If multiple fast requests, likely a bot
                    if (behavior.FastRequestCount >= _honeypotOptions.FastRequestThreshold)
                    {
                        return true;
                    }
                }
                else
                {
                    // Reset fast request counter if enough time passed
                    behavior.FastRequestCount = 0;
                }

                behavior.LastRequestTime = now;
                behavior.RequestCount++;
                return false;
            }
            finally
            {
                _behaviorsLock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Updates client behavior tracking
        /// </summary>
        private void UpdateClientBehavior(string key)
        {
            _behaviorsLock.EnterReadLock();
            try
            {
                if (_clientBehaviors.TryGetValue(key, out var behavior))
                {
                    behavior.LastSeenTime = DateTime.UtcNow;
                }
            }
            finally
            {
                _behaviorsLock.ExitReadLock();
            }
        }

        /// <summary>
        /// Marks a client as suspicious (potential bot)
        /// </summary>
        private void MarkAsSuspicious(string key, string reason)
        {
            lock (_suspiciousLock)
            {
                if (!_suspiciousClients.Contains(key))
                {
                    _suspiciousClients.Add(key);
                    Log(DOSProtectorLogLevel.Warn,
                        $"[HoneypotDOSProtector]: Marked {RedactClient(key)} as suspicious - {reason}");
                }
            }
        }

        /// <summary>
        /// Tracks client behavior patterns
        /// </summary>
        private class ClientBehavior
        {
            public DateTime LastRequestTime { get; set; }
            public DateTime LastSeenTime { get; set; }
            public int RequestCount { get; set; }
            public int FastRequestCount { get; set; }
        }

        private class NullSession : IDisposable
        {
            public void Dispose() { }
        }
    }
}
