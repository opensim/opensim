# DOS Protector Usage Guide

This guide demonstrates common usage patterns and scenarios for the DOS Protector framework.

## Table of Contents
- [Basic Usage](#basic-usage)
- [Session Management Patterns](#session-management-patterns)
- [Advanced Configuration](#advanced-configuration)
- [Common Scenarios](#common-scenarios)
- [Logging Configuration](#logging-configuration)
- [Best Practices](#best-practices)

## Basic Usage

### Simple Rate Limiting

```csharp
using OpenSim.Framework.Security.DOSProtector;
using OpenSim.Framework.Security.DOSProtector.Options;
using OpenSim.Framework.Security.DOSProtector.Interfaces;

// Configure basic rate limiting
var options = new BasicDosProtectorOptions
{
    MaxRequestsInTimeframe = 10,              // 10 requests
    RequestTimeSpan = TimeSpan.FromSeconds(30), // per 30 seconds
    ForgetTimeSpan = TimeSpan.FromMinutes(5),   // block for 5 minutes
    ReportingName = "LoginService"
};

IDOSProtector protector = DOSProtectorBuilder.Build(options);

// In your request handler
string clientIP = GetClientIP(request);
if (protector.Process(clientIP, "Login"))
{
    try
    {
        // Handle login request
        ProcessLogin(request);
    }
    finally
    {
        protector.ProcessEnd(clientIP, "Login");
    }
}
else
{
    // Client is throttled
    return HttpStatusCode.TooManyRequests;
}
```

### Concurrent Session Limiting

```csharp
var options = new BasicDosProtectorOptions
{
    MaxRequestsInTimeframe = 20,
    RequestTimeSpan = TimeSpan.FromSeconds(10),
    ForgetTimeSpan = TimeSpan.FromMinutes(2),
    MaxConcurrentSessions = 3,  // Maximum 3 simultaneous requests per client
    ReportingName = "AssetService"
};

IDOSProtector protector = DOSProtectorBuilder.Build(options);

// The protector will block if client already has 3 active requests
```

## Session Management Patterns

### Pattern 1: Using Statement (Recommended)

The `using` statement ensures `ProcessEnd` is always called, even if exceptions occur:

```csharp
string clientKey = GetClientIdentifier(request);
string endpoint = request.Url.PathAndQuery;

using (var session = protector.CreateSession(clientKey, endpoint))
{
    // Your protected code
    // ProcessEnd is automatically called when scope exits
    return ProcessRequest(request);
}
```

### Pattern 2: Try-Finally (Manual Control)

Use when you need more control over the session lifecycle:

```csharp
string clientKey = GetClientIdentifier(request);

if (!protector.IsBlocked(clientKey))
{
    if (protector.Process(clientKey, endpoint))
    {
        try
        {
            return ProcessRequest(request);
        }
        finally
        {
            protector.ProcessEnd(clientKey, endpoint);
        }
    }
}

return HttpStatusCode.TooManyRequests;
```

### Pattern 3: Pre-Check for Blocked Clients

Optimize by checking block status before expensive operations:

```csharp
string clientKey = GetClientIdentifier(request);

// Fast check - no locking if already blocked
if (protector.IsBlocked(clientKey))
{
    LogThrottledRequest(clientKey);
    return HttpStatusCode.TooManyRequests;
}

// Proceed with rate limiting and processing
using (var session = protector.CreateSession(clientKey, endpoint))
{
    return ProcessRequest(request);
}
```

## Advanced Configuration

### Block Extension Limiting

Prevent permanent blocks from aggressive retry attacks:

```csharp
var options = new AdvancedDosProtectorOptions
{
    MaxRequestsInTimeframe = 5,
    RequestTimeSpan = TimeSpan.FromSeconds(10),
    ForgetTimeSpan = TimeSpan.FromMinutes(2),

    // Enable block extension limiting
    LimitBlockExtensions = true,
    MaxBlockExtensions = 3,                    // Max 3 extensions
    MaxTotalBlockDuration = TimeSpan.FromHours(1), // Max 1 hour total

    ReportingName = "SensitiveAPI"
};

IDOSProtector protector = DOSProtectorBuilder.Build(options);
```

**How it works:**
- Client exceeds rate limit → blocked for 2 minutes
- Client retries during block → block extended to 4 minutes (extension 1)
- Client retries again → block extended to 6 minutes (extension 2)
- Client retries again → block extended to 8 minutes (extension 3)
- Client retries again → **block NOT extended** (max extensions reached)
- After max extensions or max duration, client must wait for block to expire naturally

### X-Forwarded-For Support

Handle requests behind proxies or load balancers:

```csharp
var options = new BasicDosProtectorOptions
{
    MaxRequestsInTimeframe = 10,
    RequestTimeSpan = TimeSpan.FromSeconds(30),
    ForgetTimeSpan = TimeSpan.FromMinutes(5),
    AllowXForwardedFor = true,  // Use X-Forwarded-For header
    ReportingName = "ProxiedService"
};

IDOSProtector protector = DOSProtectorBuilder.Build(options);

// Extract client IP from X-Forwarded-For if available
string clientKey = GetClientKeyWithProxy(request, options.AllowXForwardedFor);
```

## Common Scenarios

### Scenario 1: Protecting HTTP Endpoints

```csharp
public class ProtectedHttpHandler : BaseHttpHandler
{
    private readonly IDOSProtector _dosProtector;

    public ProtectedHttpHandler()
    {
        var options = new BasicDosProtectorOptions
        {
            MaxRequestsInTimeframe = 30,
            RequestTimeSpan = TimeSpan.FromMinutes(1),
            ForgetTimeSpan = TimeSpan.FromMinutes(10),
            MaxConcurrentSessions = 5,
            ReportingName = "HTTPHandler"
        };
        _dosProtector = DOSProtectorBuilder.Build(options);
    }

    public override byte[] Handle(string path, Stream request, IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
    {
        string clientKey = httpRequest.RemoteIPEndPoint.Address.ToString();

        using (var session = _dosProtector.CreateSession(clientKey, path))
        {
            return ProcessHttpRequest(path, request, httpRequest, httpResponse);
        }
    }
}
```

### Scenario 2: Protecting XMLRPC/REST Services

```csharp
public class ProtectedXMLRPCService
{
    private readonly IDOSProtector _dosProtector;

    public ProtectedXMLRPCService()
    {
        var options = new AdvancedDosProtectorOptions
        {
            MaxRequestsInTimeframe = 10,
            RequestTimeSpan = TimeSpan.FromSeconds(60),
            ForgetTimeSpan = TimeSpan.FromMinutes(15),
            MaxConcurrentSessions = 2,
            LimitBlockExtensions = true,
            MaxBlockExtensions = 5,
            MaxTotalBlockDuration = TimeSpan.FromHours(2),
            ReportingName = "XMLRPC"
        };
        _dosProtector = DOSProtectorBuilder.Build(options);
    }

    public XmlRpcResponse HandleRequest(XmlRpcRequest request, IPEndPoint client)
    {
        string clientKey = client.Address.ToString();

        if (_dosProtector.IsBlocked(clientKey))
        {
            return ErrorResponse("Too many requests. Please try again later.");
        }

        using (var session = _dosProtector.CreateSession(clientKey, request.MethodName))
        {
            return ProcessXMLRPCRequest(request);
        }
    }
}
```

### Scenario 3: Multiple Protection Levels

Different endpoints may require different protection levels:

```csharp
public class MultiLevelProtectedService
{
    private readonly IDOSProtector _loginProtector;
    private readonly IDOSProtector _apiProtector;
    private readonly IDOSProtector _bulkProtector;

    public MultiLevelProtectedService()
    {
        // Strict protection for login (credential stuffing attacks)
        _loginProtector = DOSProtectorBuilder.Build(new BasicDosProtectorOptions
        {
            MaxRequestsInTimeframe = 3,
            RequestTimeSpan = TimeSpan.FromMinutes(1),
            ForgetTimeSpan = TimeSpan.FromMinutes(30),
            ReportingName = "Login"
        });

        // Moderate protection for API calls
        _apiProtector = DOSProtectorBuilder.Build(new BasicDosProtectorOptions
        {
            MaxRequestsInTimeframe = 60,
            RequestTimeSpan = TimeSpan.FromMinutes(1),
            ForgetTimeSpan = TimeSpan.FromMinutes(5),
            MaxConcurrentSessions = 10,
            ReportingName = "API"
        });

        // Lenient protection for bulk operations
        _bulkProtector = DOSProtectorBuilder.Build(new BasicDosProtectorOptions
        {
            MaxRequestsInTimeframe = 100,
            RequestTimeSpan = TimeSpan.FromMinutes(1),
            ForgetTimeSpan = TimeSpan.FromMinutes(2),
            MaxConcurrentSessions = 3,
            ReportingName = "Bulk"
        });
    }

    public void HandleLogin(string client, LoginRequest request)
    {
        using (var session = _loginProtector.CreateSession(client, "Login"))
        {
            ProcessLogin(request);
        }
    }

    public void HandleAPICall(string client, APIRequest request)
    {
        using (var session = _apiProtector.CreateSession(client, "API"))
        {
            ProcessAPICall(request);
        }
    }

    public void HandleBulkOperation(string client, BulkRequest request)
    {
        using (var session = _bulkProtector.CreateSession(client, "Bulk"))
        {
            ProcessBulkOperation(request);
        }
    }
}
```

## Logging Configuration

### Control Log Verbosity

```csharp
var options = new BasicDosProtectorOptions
{
    MaxRequestsInTimeframe = 10,
    RequestTimeSpan = TimeSpan.FromSeconds(30),
    ForgetTimeSpan = TimeSpan.FromMinutes(5),

    // Control logging during attacks
    LogLevel = DOSProtectorLogLevel.Warn,  // Only warnings and errors

    ReportingName = "QuietService"
};
```

**Available Log Levels:**
- `None` - No logging (not recommended)
- `Error` - Only critical errors
- `Warn` - Warnings and blocks (default, recommended for production)
- `Info` - Include unblock events
- `Debug` - Verbose logging including cleanup operations

### GDPR-Compliant Logging

```csharp
var options = new BasicDosProtectorOptions
{
    MaxRequestsInTimeframe = 10,
    RequestTimeSpan = TimeSpan.FromSeconds(30),
    ForgetTimeSpan = TimeSpan.FromMinutes(5),

    // Redact IP addresses in logs
    RedactClientIdentifiers = true,  // "192.168.1.100" becomes "192.168.***.***"

    LogLevel = DOSProtectorLogLevel.Warn,
    ReportingName = "GDPRCompliantService"
};
```

## Best Practices

### 1. Choose Appropriate Limits

```csharp
// ❌ Too strict - legitimate users will be blocked
MaxRequestsInTimeframe = 1,
RequestTimeSpan = TimeSpan.FromMinutes(1)

// ✅ Reasonable - allows normal usage, blocks abuse
MaxRequestsInTimeframe = 30,
RequestTimeSpan = TimeSpan.FromMinutes(1)
```

### 2. Always Use Session Scope

```csharp
// ❌ Risk of resource leak if exception occurs
protector.Process(client, endpoint);
DoWork();
protector.ProcessEnd(client, endpoint);

// ✅ Guaranteed cleanup even with exceptions
using (var session = protector.CreateSession(client, endpoint))
{
    DoWork();
}
```

### 3. Use Descriptive Reporting Names

```csharp
// ❌ Generic, hard to debug
ReportingName = "Service"

// ✅ Specific, easy to identify in logs
ReportingName = "AssetService-TextureDownload"
```

### 4. Set Appropriate TTL

```csharp
// High-traffic service - clean up frequently
InspectionTTL = TimeSpan.FromMinutes(5)

// Low-traffic service - keep data longer
InspectionTTL = TimeSpan.FromMinutes(30)
```

### 5. Dispose Protector on Shutdown

```csharp
public class MyService : IDisposable
{
    private readonly IDOSProtector _protector;

    public void Dispose()
    {
        _protector?.Dispose();  // Stops timers, releases locks
    }
}
```

### 6. Consider Block Duration Impact

```csharp
// ❌ Too long - legitimate users locked out
ForgetTimeSpan = TimeSpan.FromHours(24)

// ✅ Reasonable - discourages attacks, allows recovery
ForgetTimeSpan = TimeSpan.FromMinutes(5)

// ✅ For sensitive operations (login)
ForgetTimeSpan = TimeSpan.FromMinutes(30)
```

## Performance Tips

1. **Pre-check for blocked clients** before expensive operations
2. **Use concurrent session limits** to protect against resource exhaustion
3. **Set appropriate TTL** to balance memory usage and effectiveness
4. **Use BasicDOSProtector** unless you specifically need Advanced features
5. **Batch operations** when possible to reduce per-request overhead

## Troubleshooting

### Issue: Legitimate users being blocked

**Solution:** Increase limits or reduce block duration
```csharp
MaxRequestsInTimeframe = 50,  // Increase from 30
RequestTimeSpan = TimeSpan.FromMinutes(1),
ForgetTimeSpan = TimeSpan.FromMinutes(2)  // Reduce from 5
```

### Issue: Memory usage growing

**Solution:** Reduce TTL for inactive client cleanup
```csharp
InspectionTTL = TimeSpan.FromMinutes(5)  // Reduce from default 10
```

### Issue: Attacks not being blocked

**Solution:** Tighten limits and enable Advanced features
```csharp
var options = new AdvancedDosProtectorOptions
{
    MaxRequestsInTimeframe = 10,  // Reduce from 30
    RequestTimeSpan = TimeSpan.FromSeconds(30),
    ForgetTimeSpan = TimeSpan.FromMinutes(10),
    LimitBlockExtensions = true,  // Enable
    MaxBlockExtensions = 3
};
```

### Issue: Log spam during attack

**Solution:** Reduce log level and enable redaction
```csharp
LogLevel = DOSProtectorLogLevel.Error,  // Only errors
RedactClientIdentifiers = true
```
