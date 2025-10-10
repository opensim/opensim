# Creating Custom DOS Protector Implementations

This guide explains how to create your own DOS protector implementation with custom logic and features.

## Table of Contents
- [Architecture Overview](#architecture-overview)
- [Step-by-Step Guide](#step-by-step-guide)
- [Complete Example](#complete-example)
- [Advanced Customization](#advanced-customization)
- [Best Practices](#best-practices)

## Architecture Overview

The DOS Protector framework uses a layered architecture:

```
IDOSProtector (Interface)
    ↑
BaseDOSProtector (Abstract base class)
    ↑
YourCustomProtector (Your implementation)
```

**Key Components:**

1. **IDOSProtector** - Interface defining the contract
2. **IDOSProtectorOptions** - Interface for configuration
3. **BaseDOSProtector** - Provides common functionality (logging, redaction, SessionScope)
4. **DOSProtectorOptionsAttribute** - Links your protector to its options type
5. **DOSProtectorBuilder** - Automatically discovers and instantiates your implementation

## Step-by-Step Guide

### Step 1: Create Your Options Class

First, define your configuration options by implementing `IDOSProtectorOptions`:

```csharp
using System;
using OpenSim.Framework.Security.DOSProtector.Interfaces;
using OpenSim.Framework.Security.DOSProtector.Options;

namespace OpenSim.Framework.Security.DOSProtector.Options
{
    /// <summary>
    /// Configuration options for GeographicDOSProtector
    /// </summary>
    public class GeographicDosProtectorOptions : BasicDosProtectorOptions
    {
        // Inherit all basic options, add custom ones

        /// <summary>
        /// List of blocked country codes (ISO 3166-1 alpha-2)
        /// </summary>
        public List<string> BlockedCountries { get; set; } = new();

        /// <summary>
        /// List of allowed country codes (empty = allow all)
        /// </summary>
        public List<string> AllowedCountries { get; set; } = new();

        /// <summary>
        /// Block duration for geographic violations
        /// </summary>
        public TimeSpan GeoBlockDuration { get; set; } = TimeSpan.FromHours(24);
    }
}
```

**Options Design Tips:**
- Extend `BasicDosProtectorOptions` to inherit standard configuration
- Use descriptive property names with XML documentation
- Provide sensible defaults
- Consider validation in your protector constructor

### Step 2: Create Your Protector Implementation

Implement your custom protector by extending `BaseDOSProtector`:

```csharp
using System;
using System.Collections.Generic;
using OpenSim.Framework.Security.DOSProtector.Attributes;
using OpenSim.Framework.Security.DOSProtector.Interfaces;
using OpenSim.Framework.Security.DOSProtector.Options;

namespace OpenSim.Framework.Security.DOSProtector
{
    /// <summary>
    /// DOS protector with geographic filtering
    /// </summary>
    [DOSProtectorOptions(typeof(GeographicDosProtectorOptions))]  // Link to your options
    public class GeographicDOSProtector : BaseDOSProtector
    {
        private readonly GeographicDosProtectorOptions _geoOptions;
        private readonly Dictionary<string, DateTime> _geoBlocked;
        private readonly object _geoBlockLock = new();

        // Your custom state and dependencies here

        public GeographicDOSProtector(GeographicDosProtectorOptions options)
            : base(options)  // Always call base constructor
        {
            ArgumentNullException.ThrowIfNull(options);

            _geoOptions = options;
            _geoBlocked = new Dictionary<string, DateTime>();

            // Initialize your custom components
        }

        public override bool IsBlocked(string key)
        {
            // Check geographic blocks
            lock (_geoBlockLock)
            {
                if (_geoBlocked.TryGetValue(key, out DateTime blockUntil))
                {
                    if (DateTime.UtcNow < blockUntil)
                    {
                        return true;
                    }
                    _geoBlocked.Remove(key);
                }
            }

            // Your custom blocking logic here
            return false;
        }

        public override bool Process(string key, string endpoint)
        {
            // Check if geographically blocked
            if (IsBlocked(key))
            {
                Log(DOSProtectorLogLevel.Warn,
                    $"[{_options.ReportingName}]: Geographic block active for {RedactClient(key)}");
                return false;
            }

            // Extract country from key (assuming format "IP|CountryCode")
            string[] parts = key.Split('|');
            if (parts.Length >= 2)
            {
                string country = parts[1];

                // Check allowed countries (if configured)
                if (_geoOptions.AllowedCountries.Count > 0 &&
                    !_geoOptions.AllowedCountries.Contains(country))
                {
                    BlockClient(key, _geoOptions.GeoBlockDuration);
                    Log(DOSProtectorLogLevel.Warn,
                        $"[{_options.ReportingName}]: Blocked {RedactClient(key)} - country not in allowlist");
                    return false;
                }

                // Check blocked countries
                if (_geoOptions.BlockedCountries.Contains(country))
                {
                    BlockClient(key, _geoOptions.GeoBlockDuration);
                    Log(DOSProtectorLogLevel.Warn,
                        $"[{_options.ReportingName}]: Blocked {RedactClient(key)} - country in blocklist");
                    return false;
                }
            }

            // Your custom processing logic
            return true;
        }

        public override void ProcessEnd(string key, string endpoint)
        {
            // Your cleanup logic
        }

        public override IDisposable CreateSession(string key, string endpoint)
        {
            if (!Process(key, endpoint))
            {
                return new NullSession();  // Helper for blocked sessions
            }

            return new SessionScope(this, key, endpoint);
        }

        public override void Dispose()
        {
            // Clean up your resources
            lock (_geoBlockLock)
            {
                _geoBlocked.Clear();
            }
        }

        private void BlockClient(string key, TimeSpan duration)
        {
            lock (_geoBlockLock)
            {
                _geoBlocked[key] = DateTime.UtcNow.Add(duration);
            }
        }

        // Helper class for blocked sessions
        private class NullSession : IDisposable
        {
            public void Dispose() { }
        }
    }
}
```

### Step 3: Use Your Custom Protector

The builder automatically discovers and uses your implementation:

```csharp
var options = new GeographicDosProtectorOptions
{
    MaxRequestsInTimeframe = 30,
    RequestTimeSpan = TimeSpan.FromMinutes(1),
    ForgetTimeSpan = TimeSpan.FromMinutes(5),

    // Your custom options
    BlockedCountries = new List<string> { "XX", "YY" },
    AllowedCountries = new List<string>(),
    GeoBlockDuration = TimeSpan.FromHours(24),

    ReportingName = "GeoProtector"
};

// Builder automatically detects GeographicDOSProtector via attribute
IDOSProtector protector = DOSProtectorBuilder.Build(options);

// Use like any other protector
string clientKey = $"{clientIP}|{countryCode}";
using (var session = protector.CreateSession(clientKey, endpoint))
{
    ProcessRequest(request);
}
```

## Complete Example

Here's a complete example of a reputation-based DOS protector:

```csharp
// File: Options/ReputationDosProtectorOptions.cs
using System;
using OpenSim.Framework.Security.DOSProtector.Options;

namespace OpenSim.Framework.Security.DOSProtector.Options
{
    public class ReputationDosProtectorOptions : BasicDosProtectorOptions
    {
        /// <summary>
        /// Initial reputation score for new clients (0-100)
        /// </summary>
        public int InitialReputation { get; set; } = 50;

        /// <summary>
        /// Minimum reputation before blocking
        /// </summary>
        public int BlockThreshold { get; set; } = 20;

        /// <summary>
        /// Reputation decrease per violation
        /// </summary>
        public int ViolationPenalty { get; set; } = 10;

        /// <summary>
        /// Reputation increase per successful request
        /// </summary>
        public int GoodBehaviorReward { get; set; } = 1;

        /// <summary>
        /// Rate at which reputation naturally recovers (per hour)
        /// </summary>
        public int NaturalRecoveryRate { get; set; } = 5;
    }
}

// File: ReputationDOSProtector.cs
using System;
using System.Collections.Generic;
using System.Threading;
using OpenSim.Framework.Security.DOSProtector.Attributes;
using OpenSim.Framework.Security.DOSProtector.Interfaces;
using OpenSim.Framework.Security.DOSProtector.Options;

namespace OpenSim.Framework.Security.DOSProtector
{
    [DOSProtectorOptions(typeof(ReputationDosProtectorOptions))]
    public class ReputationDOSProtector : BaseDOSProtector
    {
        private readonly ReputationDosProtectorOptions _repOptions;
        private readonly Dictionary<string, ClientReputation> _reputations;
        private readonly ReaderWriterLockSlim _reputationLock;
        private readonly System.Timers.Timer _recoveryTimer;

        public ReputationDOSProtector(ReputationDosProtectorOptions options)
            : base(options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _repOptions = options;
            _reputations = new Dictionary<string, ClientReputation>();
            _reputationLock = new ReaderWriterLockSlim();

            // Timer for natural reputation recovery
            _recoveryTimer = new System.Timers.Timer(TimeSpan.FromHours(1).TotalMilliseconds);
            _recoveryTimer.Elapsed += RecoverReputations;
            _recoveryTimer.AutoReset = true;
            _recoveryTimer.Start();
        }

        public override bool IsBlocked(string key)
        {
            _reputationLock.EnterReadLock();
            try
            {
                if (_reputations.TryGetValue(key, out var rep))
                {
                    bool blocked = rep.Score < _repOptions.BlockThreshold;
                    if (blocked)
                    {
                        Log(DOSProtectorLogLevel.Info,
                            $"[{_options.ReportingName}]: {RedactClient(key)} blocked (reputation: {rep.Score})");
                    }
                    return blocked;
                }
                return false;  // New clients not blocked
            }
            finally
            {
                _reputationLock.ExitReadLock();
            }
        }

        public override bool Process(string key, string endpoint)
        {
            if (IsBlocked(key))
                return false;

            _reputationLock.EnterWriteLock();
            try
            {
                if (!_reputations.TryGetValue(key, out var rep))
                {
                    rep = new ClientReputation
                    {
                        Score = _repOptions.InitialReputation,
                        LastAccess = DateTime.UtcNow
                    };
                    _reputations[key] = rep;
                }

                // Check rate limit (simplified)
                rep.RequestCount++;
                rep.LastAccess = DateTime.UtcNow;

                if (rep.RequestCount > _repOptions.MaxRequestsInTimeframe)
                {
                    // Violation - decrease reputation
                    rep.Score = Math.Max(0, rep.Score - _repOptions.ViolationPenalty);
                    Log(DOSProtectorLogLevel.Warn,
                        $"[{_options.ReportingName}]: Rate limit violated by {RedactClient(key)}, reputation: {rep.Score}");
                    return false;
                }

                return true;
            }
            finally
            {
                _reputationLock.ExitWriteLock();
            }
        }

        public override void ProcessEnd(string key, string endpoint)
        {
            _reputationLock.EnterWriteLock();
            try
            {
                if (_reputations.TryGetValue(key, out var rep))
                {
                    rep.RequestCount = Math.Max(0, rep.RequestCount - 1);

                    // Reward good behavior
                    if (rep.RequestCount <= _repOptions.MaxRequestsInTimeframe / 2)
                    {
                        rep.Score = Math.Min(100, rep.Score + _repOptions.GoodBehaviorReward);
                    }
                }
            }
            finally
            {
                _reputationLock.ExitWriteLock();
            }
        }

        public override IDisposable CreateSession(string key, string endpoint)
        {
            if (!Process(key, endpoint))
                return new NullSession();

            return new SessionScope(this, key, endpoint);
        }

        public override void Dispose()
        {
            _recoveryTimer?.Stop();
            _recoveryTimer?.Dispose();
            _reputationLock?.Dispose();
        }

        private void RecoverReputations(object sender, System.Timers.ElapsedEventArgs e)
        {
            _reputationLock.EnterWriteLock();
            try
            {
                var now = DateTime.UtcNow;
                var staleClients = new List<string>();

                foreach (var kvp in _reputations)
                {
                    var rep = kvp.Value;

                    // Natural recovery
                    rep.Score = Math.Min(100, rep.Score + _repOptions.NaturalRecoveryRate);

                    // Remove stale entries (no activity for 24 hours)
                    if ((now - rep.LastAccess).TotalHours > 24)
                    {
                        staleClients.Add(kvp.Key);
                    }
                }

                foreach (var key in staleClients)
                {
                    _reputations.Remove(key);
                }

                Log(DOSProtectorLogLevel.Debug,
                    $"[{_options.ReportingName}]: Reputation recovery completed, removed {staleClients.Count} stale entries");
            }
            finally
            {
                _reputationLock.ExitWriteLock();
            }
        }

        private class ClientReputation
        {
            public int Score { get; set; }
            public int RequestCount { get; set; }
            public DateTime LastAccess { get; set; }
        }

        private class NullSession : IDisposable
        {
            public void Dispose() { }
        }
    }
}
```

## Advanced Customization

### Combining Multiple Protection Strategies

```csharp
[DOSProtectorOptions(typeof(HybridDosProtectorOptions))]
public class HybridDOSProtector : BaseDOSProtector
{
    private readonly BasicDOSProtector _rateLimit;
    private readonly GeographicDOSProtector _geoFilter;
    private readonly ReputationDOSProtector _reputation;

    public HybridDOSProtector(HybridDosProtectorOptions options)
        : base(options)
    {
        // Compose multiple protectors
        _rateLimit = new BasicDOSProtector(options.RateLimitOptions);
        _geoFilter = new GeographicDOSProtector(options.GeoOptions);
        _reputation = new ReputationDOSProtector(options.ReputationOptions);
    }

    public override bool Process(string key, string endpoint)
    {
        // All protectors must pass
        return _geoFilter.Process(key, endpoint) &&
               _reputation.Process(key, endpoint) &&
               _rateLimit.Process(key, endpoint);
    }

    // Implement other methods...
}
```

### Adding Metrics and Monitoring

```csharp
public class MetricsDOSProtector : BaseDOSProtector
{
    private long _totalRequests;
    private long _blockedRequests;
    private long _throttledRequests;

    public MetricsDOSProtector(BasicDosProtectorOptions options)
        : base(options)
    {
    }

    public override bool Process(string key, string endpoint)
    {
        Interlocked.Increment(ref _totalRequests);

        if (IsBlocked(key))
        {
            Interlocked.Increment(ref _blockedRequests);
            return false;
        }

        // Your rate limiting logic...
        bool allowed = CheckRateLimit(key);

        if (!allowed)
        {
            Interlocked.Increment(ref _throttledRequests);
        }

        return allowed;
    }

    public Dictionary<string, long> GetMetrics()
    {
        return new Dictionary<string, long>
        {
            ["TotalRequests"] = Interlocked.Read(ref _totalRequests),
            ["BlockedRequests"] = Interlocked.Read(ref _blockedRequests),
            ["ThrottledRequests"] = Interlocked.Read(ref _throttledRequests),
            ["BlockRate"] = _totalRequests > 0
                ? (long)((_blockedRequests * 100.0) / _totalRequests)
                : 0
        };
    }
}
```

### External Data Integration

```csharp
public class DatabaseBackedDOSProtector : BaseDOSProtector
{
    private readonly IDatabase _database;

    public DatabaseBackedDOSProtector(DatabaseDosProtectorOptions options, IDatabase database)
        : base(options)
    {
        _database = database;
    }

    public override bool IsBlocked(string key)
    {
        // Check database for blocked clients
        return _database.IsClientBlocked(key);
    }

    public override bool Process(string key, string endpoint)
    {
        // Update database with request
        _database.RecordRequest(key, endpoint, DateTime.UtcNow);

        // Check rate limit from database stats
        var stats = _database.GetClientStats(key, _options.RequestTimeSpan);
        if (stats.RequestCount > _options.MaxRequestsInTimeframe)
        {
            _database.BlockClient(key, _options.ForgetTimeSpan);
            return false;
        }

        return true;
    }
}
```

## Best Practices

### 1. Thread Safety

Always use appropriate locking for shared state:

```csharp
// ✅ Good - proper locking
private readonly ReaderWriterLockSlim _lock = new();

public override bool Process(string key, string endpoint)
{
    _lock.EnterWriteLock();
    try
    {
        // Modify shared state
    }
    finally
    {
        _lock.ExitWriteLock();
    }
}
```

### 2. Use BaseDOSProtector Features

Leverage the base class helpers:

```csharp
// ✅ Use provided logging
Log(DOSProtectorLogLevel.Warn, $"Client {RedactClient(key)} blocked");

// ✅ Use provided redaction
string safeKey = RedactClient(key);

// ✅ Use provided SessionScope
return new SessionScope(this, key, endpoint);
```

### 3. Dispose Pattern

Clean up resources properly:

```csharp
private bool _disposed;

public override void Dispose()
{
    if (_disposed)
        return;

    _timer?.Stop();
    _timer?.Dispose();
    _lock?.Dispose();
    // Clean up other resources

    _disposed = true;
}
```

### 4. Validation in Constructor

```csharp
public MyDOSProtector(MyDosProtectorOptions options)
    : base(options)
{
    ArgumentNullException.ThrowIfNull(options);

    if (options.MaxRequestsInTimeframe < 1)
        throw new ArgumentException("MaxRequestsInTimeframe must be positive");

    if (options.RequestTimeSpan <= TimeSpan.Zero)
        throw new ArgumentException("RequestTimeSpan must be positive");

    // Initialize after validation
}
```

### 5. Comprehensive Logging

```csharp
// Log at appropriate levels
Log(DOSProtectorLogLevel.Debug, "Cleanup operation started");
Log(DOSProtectorLogLevel.Info, $"Client {RedactClient(key)} unblocked");
Log(DOSProtectorLogLevel.Warn, $"Client {RedactClient(key)} blocked");
Log(DOSProtectorLogLevel.Error, "Critical error in protector");
```

### 6. Test Your Implementation

```csharp
[Test]
public void TestBlocking()
{
    var options = new MyDosProtectorOptions
    {
        MaxRequestsInTimeframe = 5,
        RequestTimeSpan = TimeSpan.FromSeconds(10)
    };

    var protector = new MyDOSProtector(options);

    // Should allow first 5 requests
    for (int i = 0; i < 5; i++)
    {
        Assert.IsTrue(protector.Process("client1", "endpoint"));
    }

    // Should block 6th request
    Assert.IsFalse(protector.Process("client1", "endpoint"));
}
```

## Registration Checklist

When creating a custom DOS protector, ensure:

- ✅ Options class implements or extends `IDOSProtectorOptions`
- ✅ Protector class extends `BaseDOSProtector`
- ✅ Protector class has `[DOSProtectorOptions(typeof(YourOptionsType))]` attribute
- ✅ Constructor calls `base(options)` and validates options
- ✅ All abstract methods are implemented (`IsBlocked`, `Process`, `ProcessEnd`, `CreateSession`, `Dispose`)
- ✅ Thread-safe implementation for shared state
- ✅ Proper resource cleanup in `Dispose()`
- ✅ Logging uses `Log()` and `RedactClient()` helpers
- ✅ SessionScope returned from `CreateSession()`

Your custom protector will be automatically discovered by `DOSProtectorBuilder`!
