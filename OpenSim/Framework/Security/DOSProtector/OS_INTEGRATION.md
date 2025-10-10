# OpenSimulator Integration Guide

This guide explains how to integrate the DOS Protector framework into OpenSimulator's core services and regions.

## Table of Contents
- [Overview](#overview)
- [Integration Points](#integration-points)
- [Step-by-Step Integration](#step-by-step-integration)
- [Service-Specific Examples](#service-specific-examples)
- [Configuration](#configuration)
- [Testing](#testing)
- [Troubleshooting](#troubleshooting)

## Overview

The DOS Protector framework needs to be initialized during OpenSimulator startup and integrated into services that handle external requests. This document provides guidance for both core developers and administrators.

### What Needs Integration

1. **Startup Initialization** - Load configuration and register console commands
2. **Service Integration** - Add DOS protection to HTTP/XMLRPC/LLSD endpoints
3. **Configuration Files** - Set up INI files for protector options

### Integration Levels

- **Level 1 (Minimal)**: Just use the builder without config loader
- **Level 2 (Recommended)**: Full integration with config loader and console commands
- **Level 3 (Advanced)**: Custom protectors with plugin support

## Integration Points

### 1. Startup Initialization (Required for Plugins & Commands)

The DOS Protector system should be initialized during application startup, before any services start processing requests.

**For Region Servers:**
- File: `OpenSim/Region/Application/OpenSim.cs`
- Method: `Startup()` or similar initialization method

**For Robust/Grid Services:**
- File: `OpenSim/Server/Base/ServerBase.cs`
- Method: `StartupSpecific()` or constructor

**Initialization Code:**

```csharp
using OpenSim.Framework.Security.DOSProtector;
using OpenSim.Framework.Security.DOSProtector.Commands;

// In your startup method:
public override void Startup()
{
    // ... existing startup code ...

    // Load DOS Protector configuration (reads DOSProtector.ini)
    DOSProtectorConfigLoader.LoadConfig();

    // Register console commands
    DOSProtectorCommands.RegisterCommands(MainConsole.Instance);

    m_log.Info("[STARTUP]: DOS Protector initialized");

    // ... rest of startup code ...
}
```

**Why this matters:**
- Loads plugin DLLs from configured paths
- Makes console commands available (`dosprotector list`, `dosprotector refresh`)
- Configures the builder before first use
- Only needs to be called once per application lifetime

### 2. Service Integration

Integrate DOS protection into services that handle external requests.

#### HTTP Handler Integration

```csharp
using OpenSim.Framework.Security.DOSProtector;
using OpenSim.Framework.Security.DOSProtector.Interfaces;
using OpenSim.Framework.Security.DOSProtector.Options;

public class YourHttpHandler : BaseStreamHandler
{
    private readonly IDOSProtector _dosProtector;

    public YourHttpHandler(string path) : base("POST", path)
    {
        // Configure protection
        var options = new BasicDosProtectorOptions
        {
            MaxRequestsInTimeframe = 30,
            RequestTimeSpan = TimeSpan.FromMinutes(1),
            ForgetTimeSpan = TimeSpan.FromMinutes(5),
            MaxConcurrentSessions = 5,
            ReportingName = "YourService",
            LogLevel = DOSProtectorLogLevel.Warn
        };

        _dosProtector = DOSProtectorBuilder.Build(options);
    }

    protected override byte[] ProcessRequest(string path, Stream request,
        IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
    {
        string clientIP = httpRequest.RemoteIPEndPoint.Address.ToString();

        // Use session scope for automatic cleanup
        using (var session = _dosProtector.CreateSession(clientIP, path))
        {
            // Your request processing logic
            return HandleRequest(path, request, httpRequest, httpResponse);
        }
    }

    public override void Dispose()
    {
        _dosProtector?.Dispose();
        base.Dispose();
    }
}
```

#### XMLRPC Service Integration

```csharp
using OpenSim.Framework.Security.DOSProtector;
using OpenSim.Framework.Security.DOSProtector.Interfaces;
using OpenSim.Framework.Security.DOSProtector.Options;

public class YourXMLRPCService
{
    private readonly IDOSProtector _dosProtector;

    public YourXMLRPCService()
    {
        var options = new BasicDosProtectorOptions
        {
            MaxRequestsInTimeframe = 20,
            RequestTimeSpan = TimeSpan.FromMinutes(1),
            ForgetTimeSpan = TimeSpan.FromMinutes(10),
            ReportingName = "XMLRPC-YourService"
        };

        _dosProtector = DOSProtectorBuilder.Build(options);
    }

    public XmlRpcResponse HandleXMLRPCRequest(XmlRpcRequest request, IPEndPoint client)
    {
        string clientIP = client.Address.ToString();

        // Check if blocked before processing
        if (_dosProtector.IsBlocked(clientIP))
        {
            return ErrorResponse("Too many requests. Please try again later.");
        }

        using (var session = _dosProtector.CreateSession(clientIP, request.MethodName))
        {
            return ProcessRequest(request);
        }
    }
}
```

## Step-by-Step Integration

### Step 1: Add Startup Initialization

**File: `OpenSim/Region/Application/OpenSim.cs`**

```csharp
// Add using directives at top
using OpenSim.Framework.Security.DOSProtector;
using OpenSim.Framework.Security.DOSProtector.Commands;

// In Startup() method, add after base initialization:
public override void Startup()
{
    base.Startup();

    // Initialize DOS Protector
    try
    {
        DOSProtectorConfigLoader.LoadConfig();
        DOSProtectorCommands.RegisterCommands(MainConsole.Instance);
        m_log.Info("[OPENSIM]: DOS Protector system initialized");
    }
    catch (Exception e)
    {
        m_log.Error("[OPENSIM]: Failed to initialize DOS Protector: " + e.Message);
    }

    // ... rest of startup code ...
}
```

### Step 2: Integrate into Critical Services

Identify services that need protection. Common candidates:

**Login Service:**
```csharp
// OpenSim/Services/LLLoginService/LLLoginService.cs
private readonly IDOSProtector _loginProtector;

public LLLoginService(IConfigSource config)
{
    var options = new BasicDosProtectorOptions
    {
        MaxRequestsInTimeframe = 3,      // Strict: prevent credential stuffing
        RequestTimeSpan = TimeSpan.FromMinutes(1),
        ForgetTimeSpan = TimeSpan.FromMinutes(30),  // Long block for login attempts
        ReportingName = "LoginService"
    };
    _loginProtector = DOSProtectorBuilder.Build(options);
}

public LoginResponse Login(LoginRequest request, string clientIP)
{
    using (var session = _loginProtector.CreateSession(clientIP, "Login"))
    {
        return ProcessLogin(request);
    }
}
```

**Asset Service:**
```csharp
// OpenSim/Services/AssetService/AssetService.cs
private readonly IDOSProtector _assetProtector;

public AssetService(IConfigSource config)
{
    var options = new BasicDosProtectorOptions
    {
        MaxRequestsInTimeframe = 100,    // High volume expected
        RequestTimeSpan = TimeSpan.FromMinutes(1),
        ForgetTimeSpan = TimeSpan.FromMinutes(2),
        MaxConcurrentSessions = 20,
        ReportingName = "AssetService"
    };
    _assetProtector = DOSProtectorBuilder.Build(options);
}
```

**Inventory Service:**
```csharp
// OpenSim/Services/InventoryService/XInventoryService.cs
private readonly IDOSProtector _inventoryProtector;

public XInventoryService(IConfigSource config)
{
    var options = new BasicDosProtectorOptions
    {
        MaxRequestsInTimeframe = 50,
        RequestTimeSpan = TimeSpan.FromMinutes(1),
        ForgetTimeSpan = TimeSpan.FromMinutes(5),
        MaxConcurrentSessions = 10,
        ReportingName = "InventoryService"
    };
    _inventoryProtector = DOSProtectorBuilder.Build(options);
}
```

### Step 3: Create Configuration Files

**File: `bin/DOSProtector.ini`**

```ini
[DOSProtector]
    ; Enable plugin loading
    EnablePlugins = true

    ; Plugin directories (optional)
    ; PluginPaths = "./plugins/dosprotectors"

    ; Verbose logging
    VerbosePluginLoading = false
```

**File: `bin/config-include/DOSProtection.ini` (Optional)**

```ini
; Service-specific DOS protection configuration
; This is read by your services if you implement config-based options

[LoginServiceDOS]
    Enabled = true
    MaxRequestsInTimeframe = 3
    RequestTimeSpan = 60
    ForgetTimeSpan = 1800
    ReportingName = "LoginService"

[AssetServiceDOS]
    Enabled = true
    MaxRequestsInTimeframe = 100
    RequestTimeSpan = 60
    ForgetTimeSpan = 120
    MaxConcurrentSessions = 20
    ReportingName = "AssetService"

[InventoryServiceDOS]
    Enabled = true
    MaxRequestsInTimeframe = 50
    RequestTimeSpan = 60
    ForgetTimeSpan = 300
    MaxConcurrentSessions = 10
    ReportingName = "InventoryService"
```

### Step 4: Load Service Configuration (Optional)

Helper method to load DOS protector options from INI:

```csharp
using Nini.Config;
using OpenSim.Framework.Security.DOSProtector.Options;

public static class DOSProtectorConfigHelper
{
    public static BasicDosProtectorOptions LoadFromConfig(
        IConfigSource config,
        string section,
        BasicDosProtectorOptions defaults = null)
    {
        var options = defaults ?? new BasicDosProtectorOptions();
        var cfg = config.Configs[section];

        if (cfg == null)
            return options;

        if (!cfg.GetBoolean("Enabled", true))
            return null;  // DOS protection disabled for this service

        options.MaxRequestsInTimeframe = cfg.GetInt("MaxRequestsInTimeframe",
            options.MaxRequestsInTimeframe);
        options.RequestTimeSpan = TimeSpan.FromSeconds(cfg.GetInt("RequestTimeSpan",
            (int)options.RequestTimeSpan.TotalSeconds));
        options.ForgetTimeSpan = TimeSpan.FromSeconds(cfg.GetInt("ForgetTimeSpan",
            (int)options.ForgetTimeSpan.TotalSeconds));
        options.MaxConcurrentSessions = cfg.GetInt("MaxConcurrentSessions",
            options.MaxConcurrentSessions);
        options.ReportingName = cfg.GetString("ReportingName",
            options.ReportingName);

        return options;
    }
}

// Usage in service:
public YourService(IConfigSource config)
{
    var options = DOSProtectorConfigHelper.LoadFromConfig(
        config,
        "YourServiceDOS",
        new BasicDosProtectorOptions { /* defaults */ });

    if (options != null)
        _dosProtector = DOSProtectorBuilder.Build(options);
}
```

## Service-Specific Examples

### Example 1: LLLoginService Protection

```csharp
// OpenSim/Services/LLLoginService/LLLoginService.cs

using OpenSim.Framework.Security.DOSProtector;
using OpenSim.Framework.Security.DOSProtector.Interfaces;
using OpenSim.Framework.Security.DOSProtector.Options;

public class LLLoginService : ILoginService
{
    private static readonly ILog m_log = LogManager.GetLogger(
        MethodBase.GetCurrentMethod().DeclaringType);
    private readonly IDOSProtector m_dosProtector;

    public LLLoginService(IConfigSource config)
    {
        // ... existing initialization ...

        // Initialize DOS protection
        var dosOptions = new BasicDosProtectorOptions
        {
            MaxRequestsInTimeframe = 3,
            RequestTimeSpan = TimeSpan.FromMinutes(1),
            ForgetTimeSpan = TimeSpan.FromMinutes(30),
            ReportingName = "LoginService",
            LogLevel = DOSProtectorLogLevel.Warn,
            RedactClientIdentifiers = true  // GDPR compliance
        };

        m_dosProtector = DOSProtectorBuilder.Build(dosOptions);
        m_log.Info("[LLOGIN SERVICE]: DOS protection enabled");
    }

    public LoginResponse Login(string firstName, string lastName, string passwd,
        string startLocation, UUID scopeID, string clientVersion,
        string channel, string mac, string id0, IPEndPoint clientIP)
    {
        string clientKey = clientIP.Address.ToString();

        // Check if client is already blocked
        if (m_dosProtector.IsBlocked(clientKey))
        {
            m_log.WarnFormat("[LLOGIN SERVICE]: Login denied for {0} - rate limited", clientKey);
            return FailedLogin("Too many login attempts. Please try again later.");
        }

        // Process login with DOS protection
        using (var session = m_dosProtector.CreateSession(clientKey, "Login"))
        {
            return ProcessLogin(firstName, lastName, passwd, startLocation,
                scopeID, clientVersion, channel, mac, id0, clientIP);
        }
    }

    public void Dispose()
    {
        m_dosProtector?.Dispose();
    }
}
```

### Example 2: BaseHttpHandler Protection

```csharp
// OpenSim/Framework/Servers/HttpServer/BaseStreamHandler.cs
// Add optional DOS protection support to base class

using OpenSim.Framework.Security.DOSProtector.Interfaces;

public abstract class BaseStreamHandler : BaseRequestHandler, IStreamHandler
{
    protected IDOSProtector DOSProtector { get; set; }

    protected override byte[] ProcessRequest(string path, Stream request,
        IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
    {
        // If DOS protection is configured, use it
        if (DOSProtector != null)
        {
            string clientIP = httpRequest.RemoteIPEndPoint.Address.ToString();

            if (DOSProtector.IsBlocked(clientIP))
            {
                httpResponse.StatusCode = (int)HttpStatusCode.TooManyRequests;
                return Array.Empty<byte>();
            }

            using (var session = DOSProtector.CreateSession(clientIP, path))
            {
                return HandleRequest(path, request, httpRequest, httpResponse);
            }
        }

        return HandleRequest(path, request, httpRequest, httpResponse);
    }

    protected abstract byte[] HandleRequest(string path, Stream request,
        IOSHttpRequest httpRequest, IOSHttpResponse httpResponse);
}
```

## Configuration

### Core Configuration (DOSProtector.ini)

```ini
[DOSProtector]
    ; Enable/disable plugin system
    EnablePlugins = true

    ; Comma-separated plugin paths
    ; Can be absolute or relative to bin/
    PluginPaths = "./plugins/dosprotectors,C:\OpenSim\CustomProtectors"

    ; Enable detailed logging during plugin discovery
    VerbosePluginLoading = false
```

### Service Configuration Example

```ini
[LoginServiceDOS]
    Enabled = true
    MaxRequestsInTimeframe = 3
    RequestTimeSpan = 60        ; seconds
    ForgetTimeSpan = 1800       ; seconds (30 minutes)
    ReportingName = "LoginService"
    LogLevel = Warn
    RedactClientIdentifiers = true

[AssetServiceDOS]
    Enabled = true
    MaxRequestsInTimeframe = 100
    RequestTimeSpan = 60
    ForgetTimeSpan = 120
    MaxConcurrentSessions = 20
    ReportingName = "AssetService"
    LogLevel = Error           ; Less verbose for high-traffic service

[GridServiceDOS]
    Enabled = true
    MaxRequestsInTimeframe = 50
    RequestTimeSpan = 60
    ForgetTimeSpan = 300
    MaxConcurrentSessions = 15
    ReportingName = "GridService"
```

## Testing

### 1. Verify Initialization

Start OpenSimulator and check console output:

```
[OPENSIM]: DOS Protector system initialized
[DOSProtectorBuilder]: Initializing DOS protector registry
[DOSProtectorBuilder]: Discovered 2 DOS protector implementations
```

### 2. Test Console Commands

```
dosprotector list
```

Expected output:
```
Discovered 2 DOS Protector implementation(s):

  1. BasicDOSProtector
     Options Type: BasicDosProtectorOptions

  2. AdvancedDOSProtector
     Options Type: AdvancedDosProtectorOptions
```

### 3. Test Rate Limiting

Use a script to generate rapid requests:

```bash
# Bash script to test rate limiting
for i in {1..10}; do
  curl http://localhost:9000/your-endpoint
  echo "Request $i"
done
```

Check logs for blocks:
```
[LoginService]: client: 127.0.0.1 is blocked for 120000ms based on concurrency
```

### 4. Test Block Recovery

Wait for `ForgetTimeSpan` to elapse, then verify client is unblocked:

```
[LoginService] client: 127.0.0.1 is no longer blocked.
```

## Troubleshooting

### Issue: "DOS Protector not initialized"

**Cause:** `DOSProtectorConfigLoader.LoadConfig()` not called

**Solution:** Add initialization to startup code (see Step 1)

### Issue: "Console commands not available"

**Cause:** `DOSProtectorCommands.RegisterCommands()` not called

**Solution:**
```csharp
DOSProtectorCommands.RegisterCommands(MainConsole.Instance);
```

### Issue: "Plugins not loading"

**Solutions:**
1. Check `DOSProtector.ini` exists in `bin/` directory
2. Verify `PluginPaths` is correct
3. Run `dosprotector refresh` to force reload
4. Check logs for load errors:
   ```
   [DOSProtectorBuilder]: Could not load plugin assembly: <reason>
   ```

### Issue: "Legitimate users being blocked"

**Solution:** Adjust limits in service configuration:
```csharp
MaxRequestsInTimeframe = 50,  // Increase from 30
ForgetTimeSpan = TimeSpan.FromMinutes(2)  // Decrease from 5
```

### Issue: "High memory usage"

**Solution:** Reduce InspectionTTL:
```csharp
InspectionTTL = TimeSpan.FromMinutes(5)  // Reduce from default 10
```

## Best Practices

1. **Initialize Once:** Call `DOSProtectorConfigLoader.LoadConfig()` only once at startup
2. **Dispose Properly:** Always dispose protectors in service cleanup
3. **Use Session Scope:** Prefer `using (var session = ...)` pattern
4. **Appropriate Limits:** Set limits based on service type and expected load
5. **Monitor Logs:** Watch for DOS protection events in production
6. **Test Thoroughly:** Test with realistic load before production deployment
7. **Document Configuration:** Add comments to INI files explaining limits

## Migration from Legacy BasicDOSProtector

If you have existing code using the old `OpenSim.Framework.BasicDOSProtector`:

**Old Code:**
```csharp
using OpenSim.Framework;
var protector = new BasicDOSProtector(options);
```

**New Code:**
```csharp
using OpenSim.Framework.Security.DOSProtector;
using OpenSim.Framework.Security.DOSProtector.Options;
var protector = DOSProtectorBuilder.Build(options);
```

The legacy class remains for backward compatibility but is deprecated.

## Further Reading

- [README.md](README.md) - Framework overview
- [USAGE.md](USAGE.md) - Usage patterns and examples
- [CUSTOMIZE.md](CUSTOMIZE.md) - Creating custom protectors
- [PLUGIN.md](PLUGIN.md) - Plugin development guide
