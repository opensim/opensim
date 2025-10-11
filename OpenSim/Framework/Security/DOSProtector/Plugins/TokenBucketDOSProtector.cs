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
    /// Token Bucket algorithm implementation for smooth rate limiting.
    /// Allows bursts while maintaining average rate limit over time.
    /// </summary>
    [DOSProtectorOptions(typeof(TokenBucketDosProtectorOptions))]
    public class TokenBucketDOSProtector : BaseDOSProtector
    {
        private readonly TokenBucketDosProtectorOptions _bucketOptions;
        private readonly Dictionary<string, TokenBucket> _buckets;
        private readonly ReaderWriterLockSlim _bucketsLock;
        private readonly System.Timers.Timer _cleanupTimer;
        private bool _disposed;

        public TokenBucketDOSProtector(TokenBucketDosProtectorOptions options)
            : base(options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _bucketOptions = options;
            _buckets = new Dictionary<string, TokenBucket>();
            _bucketsLock = new ReaderWriterLockSlim();

            // Cleanup timer for stale buckets
            _cleanupTimer = new System.Timers.Timer(TimeSpan.FromMinutes(5).TotalMilliseconds);
            _cleanupTimer.Elapsed += CleanupStaleBuckets;
            _cleanupTimer.AutoReset = true;
            _cleanupTimer.Start();

            Log(DOSProtectorLogLevel.Info,
                $"[TokenBucketDOSProtector]: Initialized with capacity: {options.BucketCapacity}, " +
                $"refill rate: {options.RefillRate} tokens/sec");
        }

        public override bool IsBlocked(string key, IDOSProtectorContext context = null)
        {
            _bucketsLock.EnterReadLock();
            try
            {
                if (_buckets.TryGetValue(key, out var bucket))
                {
                    return bucket.Tokens <= 0;
                }
                return false;
            }
            finally
            {
                _bucketsLock.ExitReadLock();
            }
        }

        public override bool Process(string key, string endpoint, IDOSProtectorContext context = null)
        {
            _bucketsLock.EnterUpgradeableReadLock();
            try
            {
                TokenBucket bucket;

                if (!_buckets.TryGetValue(key, out bucket))
                {
                    _bucketsLock.EnterWriteLock();
                    try
                    {
                        bucket = new TokenBucket(
                            _bucketOptions.BucketCapacity,
                            _bucketOptions.RefillRate,
                            _bucketOptions.BucketCapacity); // Start full
                        _buckets[key] = bucket;
                    }
                    finally
                    {
                        _bucketsLock.ExitWriteLock();
                    }
                }

                // Refill tokens based on time elapsed
                bucket.Refill();

                // Try to consume a token
                if (bucket.TryConsume(_bucketOptions.TokenCost))
                {
                    Log(DOSProtectorLogLevel.Debug,
                        $"[TokenBucketDOSProtector]: {RedactClient(key)} - Request allowed, " +
                        $"remaining tokens: {bucket.Tokens:F2}");
                    return true;
                }

                Log(DOSProtectorLogLevel.Warn,
                    $"[TokenBucketDOSProtector]: {RedactClient(key)} - Rate limited, " +
                    $"tokens: {bucket.Tokens:F2}, refill in: {bucket.TimeUntilNextToken():F1}s");
                return false;
            }
            finally
            {
                _bucketsLock.ExitUpgradeableReadLock();
            }
        }

        public override void ProcessEnd(string key, string endpoint, IDOSProtectorContext context = null)
        {
            // Token bucket doesn't need explicit end processing
        }

        public override IDisposable CreateSession(string key, string endpoint, IDOSProtectorContext context = null)
        {
            if (!Process(key, endpoint, context))
                return new NullSession();

            return new SessionScope(this, key, endpoint, context);
        }

        public override void Dispose()
        {
            if (_disposed)
                return;

            _cleanupTimer?.Stop();
            _cleanupTimer?.Dispose();
            _bucketsLock?.Dispose();

            _disposed = true;
        }

        private void CleanupStaleBuckets(object sender, System.Timers.ElapsedEventArgs e)
        {
            _bucketsLock.EnterWriteLock();
            try
            {
                var now = DateTime.UtcNow;
                var staleKeys = new List<string>();

                foreach (var kvp in _buckets)
                {
                    // Remove buckets inactive for longer than TTL
                    if ((now - kvp.Value.LastAccess).TotalMilliseconds > _options.InspectionTTL.TotalMilliseconds)
                    {
                        staleKeys.Add(kvp.Key);
                    }
                }

                foreach (var key in staleKeys)
                {
                    _buckets.Remove(key);
                }

                if (staleKeys.Count > 0)
                {
                    Log(DOSProtectorLogLevel.Debug,
                        $"[TokenBucketDOSProtector]: Cleaned up {staleKeys.Count} stale buckets");
                }
            }
            finally
            {
                _bucketsLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Token bucket implementation
        /// </summary>
        private class TokenBucket
        {
            private readonly double _capacity;
            private readonly double _refillRate;
            private double _tokens;
            private DateTime _lastRefill;
            private readonly object _lock = new();

            public double Tokens
            {
                get
                {
                    lock (_lock)
                        return _tokens;
                }
            }

            public DateTime LastAccess { get; private set; }

            public TokenBucket(double capacity, double refillRate, double initialTokens)
            {
                _capacity = capacity;
                _refillRate = refillRate;
                _tokens = Math.Min(initialTokens, capacity);
                _lastRefill = DateTime.UtcNow;
                LastAccess = DateTime.UtcNow;
            }

            public void Refill()
            {
                lock (_lock)
                {
                    var now = DateTime.UtcNow;
                    var elapsed = (now - _lastRefill).TotalSeconds;

                    if (elapsed > 0)
                    {
                        var tokensToAdd = elapsed * _refillRate;
                        _tokens = Math.Min(_capacity, _tokens + tokensToAdd);
                        _lastRefill = now;
                    }

                    LastAccess = now;
                }
            }

            public bool TryConsume(double tokens)
            {
                lock (_lock)
                {
                    if (_tokens >= tokens)
                    {
                        _tokens -= tokens;
                        LastAccess = DateTime.UtcNow;
                        return true;
                    }
                    return false;
                }
            }

            public double TimeUntilNextToken()
            {
                lock (_lock)
                {
                    if (_tokens >= 1.0)
                        return 0;

                    return (1.0 - _tokens) / _refillRate;
                }
            }
        }

        private class NullSession : IDisposable
        {
            public void Dispose() { }
        }
    }
}
