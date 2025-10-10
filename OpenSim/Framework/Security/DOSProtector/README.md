# DOS Protector

A flexible, production-ready Denial-of-Service (DOS) protection framework for OpenSimulator.

## Overview

The DOS Protector provides rate limiting and concurrent session management to protect OpenSimulator services from abuse, whether from malicious actors or misconfigured clients. It features a modular architecture with multiple implementations to suit different security requirements.

## Features

### Core Protection
- **Rate Limiting**: Control maximum requests per timeframe per client
- **Concurrent Session Management**: Limit simultaneous active sessions per client
- **Automatic Blocking**: Temporary blocks for clients exceeding limits
- **TTL-Based Cleanup**: Automatic memory management for inactive clients

### Advanced Features
- **Block Extension Limiting**: Prevent permanent blocks from aggressive retries
- **Configurable Logging**: Control log verbosity to prevent log spam during attacks
- **IP Redaction**: GDPR-compliant logging with client identifier masking
- **X-Forwarded-For Support**: Handle proxied requests correctly

### Architecture
- **Interface-Based Design**: Easy to extend with custom implementations
- **Attribute-Driven Discovery**: Automatic implementation selection via `DOSProtectorBuilder`
- **Thread-Safe**: Uses `ReaderWriterLockSlim` for optimal concurrent access
- **Resource-Aware**: Memory leak prevention with TTL cleanup timer

## Available Implementations

### BasicDOSProtector
Standard DOS protection suitable for most scenarios:
- Rate limiting per client
- Concurrent session tracking
- Configurable blocking duration
- Memory leak prevention via TTL cleanup

**Use when:** You need reliable DOS protection without complex requirements.

### AdvancedDOSProtector
Extended protection with additional security features:
- All BasicDOSProtector features
- Block extension limiting (prevents permanent blocks)
- Configurable maximum block duration
- Configurable maximum block extension count

**Use when:** You need protection against sophisticated retry attacks or require stricter block policies.

### Plugin Support
The framework supports dynamic plugin loading:
- Load custom DOS protector implementations from external DLLs
- Configure plugin paths via INI file
- Hot-reload plugins using console commands
- No core code modifications required

**See:** [PLUGIN.md](PLUGIN.md) for plugin development guide.

## Quick Start

```csharp
using OpenSim.Framework.Security.DOSProtector;
using OpenSim.Framework.Security.DOSProtector.Options;

// Configure protection
var options = new BasicDosProtectorOptions
{
    MaxRequestsInTimeframe = 5,
    RequestTimeSpan = TimeSpan.FromSeconds(10),
    ForgetTimeSpan = TimeSpan.FromMinutes(2),
    MaxConcurrentSessions = 3,
    ReportingName = "MyService"
};

// Create protector (automatically selects implementation based on options type)
var protector = DOSProtectorBuilder.Build(options);

// Protect your code
string clientKey = GetClientIdentifier(request);
using (var session = protector.CreateSession(clientKey, endpoint))
{
    // Your protected code here
    // Session automatically decremented on dispose
}
```

## Documentation

- **[OS_INTEGRATION.md](OS_INTEGRATION.md)** - OpenSimulator integration guide (⭐ start here!)
- **[USAGE.md](USAGE.md)** - Detailed usage scenarios and patterns
- **[CUSTOMIZE.md](CUSTOMIZE.md)** - Guide to creating custom DOS protector implementations
- **[PLUGIN.md](PLUGIN.md)** - Plugin development and deployment guide

## Configuration Options

### Basic Options
- `MaxRequestsInTimeframe` - Maximum requests allowed in timeframe
- `RequestTimeSpan` - Time window for rate limiting
- `ForgetTimeSpan` - Duration of temporary blocks
- `MaxConcurrentSessions` - Maximum simultaneous sessions (0 = unlimited)
- `InspectionTTL` - Time until inactive client data is cleaned up (default: 10 minutes)
- `LogLevel` - Logging verbosity (None, Error, Warn, Info, Debug)
- `RedactClientIdentifiers` - Redact IPs in logs for privacy (default: false)

### Advanced Options
- `LimitBlockExtensions` - Enable block extension limiting (default: false)
- `MaxBlockExtensions` - Maximum block extensions allowed (default: 3)
- `MaxTotalBlockDuration` - Maximum total block duration (default: 1 hour)

## Thread Safety

All DOS protector implementations are thread-safe and use lock ordering to prevent deadlocks:
1. `_deeperInspection` (monitor lock)
2. `_blockLockSlim` (ReaderWriterLockSlim)
3. `_sessionLockSlim` (ReaderWriterLockSlim)
4. `_generalRequestLock` (monitor lock)

## Performance Considerations

- Uses `ReaderWriterLockSlim` for optimal read-heavy workloads
- Memory-efficient circular buffers for request tracking
- Background timer for cleanup (not blocking main operations)
- Lazy initialization of per-client data structures

## Migration from Legacy BasicDOSProtector

The legacy `OpenSim.Framework.BasicDOSProtector` remains available for backward compatibility but is deprecated. To migrate:

1. Change namespace:
   ```csharp
   // Old
   using OpenSim.Framework;

   // New
   using OpenSim.Framework.Security.DOSProtector;
   using OpenSim.Framework.Security.DOSProtector.Options;
   ```

2. Update instantiation to use options pattern and builder:
   ```csharp
   // Old
   var protector = new BasicDOSProtector(options);

   // New
   var protector = DOSProtectorBuilder.Build(options);
   ```

## License

Copyright (c) Contributors, http://opensimulator.org/

Licensed under BSD 3-Clause License. See [CONTRIBUTORS.TXT](../../../../CONTRIBUTORS.TXT) for full license text.
