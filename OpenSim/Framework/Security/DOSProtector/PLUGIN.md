# DOS Protector Plugin Development

This guide explains how to create DOS protector plugins as external assemblies that can be loaded dynamically into OpenSimulator.

## Table of Contents
- [Overview](#overview)
- [Quick Start](#quick-start)
- [Plugin Structure](#plugin-structure)
- [Configuration](#configuration)
- [Deployment](#deployment)
- [Console Commands](#console-commands)
- [Troubleshooting](#troubleshooting)

## Overview

The DOS Protector framework supports dynamic plugin loading, allowing you to:
- Develop custom DOS protectors as separate DLL assemblies
- Deploy plugins without modifying core OpenSimulator code
- Hot-reload plugins using console commands
- Configure plugin paths via INI file

**Plugin Discovery Process:**
1. Core assembly (OpenSim.Framework.Security.dll) is scanned
2. All loaded assemblies in AppDomain are scanned
3. Configured plugin directories are scanned and loaded
4. Found protectors are registered in the builder cache

## Quick Start

### 1. Create Plugin Project

Create a new C# class library project:

```bash
dotnet new classlib -n MyDOSProtectorPlugin
cd MyDOSProtectorPlugin
```

### 2. Add References

Add references to OpenSimulator assemblies:

```xml
<ItemGroup>
    <Reference Include="OpenSim.Framework.Security">
        <HintPath>path\to\opensim\bin\OpenSim.Framework.Security.dll</HintPath>
    </Reference>
    <Reference Include="OpenSim.Framework">
        <HintPath>path\to\opensim\bin\OpenSim.Framework.dll</HintPath>
    </Reference>
    <Reference Include="log4net">
        <HintPath>path\to\opensim\bin\log4net.dll</HintPath>
    </Reference>
</ItemGroup>
```

### 3. Implement Your Protector

```csharp
using System;
using OpenSim.Framework.Security.DOSProtector;
using OpenSim.Framework.Security.DOSProtector.Attributes;
using OpenSim.Framework.Security.DOSProtector.Interfaces;
using OpenSim.Framework.Security.DOSProtector.Options;

namespace MyDOSProtectorPlugin
{
    // Define your options
    public class CustomDosProtectorOptions : BasicDosProtectorOptions
    {
        public string CustomSetting { get; set; } = "default";
    }

    // Implement your protector
    [DOSProtectorOptions(typeof(CustomDosProtectorOptions))]
    public class CustomDOSProtector : BaseDOSProtector
    {
        private readonly CustomDosProtectorOptions _customOptions;

        public CustomDOSProtector(CustomDosProtectorOptions options)
            : base(options)
        {
            _customOptions = options;
            Log(DOSProtectorLogLevel.Info,
                $"[CustomDOSProtector]: Initialized with setting: {options.CustomSetting}");
        }

        public override bool IsBlocked(string key)
        {
            // Your custom blocking logic
            return false;
        }

        public override bool Process(string key, string endpoint)
        {
            Log(DOSProtectorLogLevel.Debug,
                $"[CustomDOSProtector]: Processing {RedactClient(key)}");
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
                return new NullSession();
            return new SessionScope(this, key, endpoint);
        }

        public override void Dispose()
        {
            // Clean up resources
        }

        private class NullSession : IDisposable
        {
            public void Dispose() { }
        }
    }
}
```

### 4. Build Plugin

```bash
dotnet build -c Release
```

### 5. Deploy Plugin

Copy your compiled DLL to a plugin directory:

```bash
mkdir C:\OpenSim\plugins\dosprotectors
copy bin\Release\net8.0\MyDOSProtectorPlugin.dll C:\OpenSim\plugins\dosprotectors\
```

### 6. Configure OpenSimulator

Create or edit `bin/DOSProtector.ini`:

```ini
[DOSProtector]
    EnablePlugins = true
    PluginPaths = "C:\OpenSim\plugins\dosprotectors"
```

### 7. Verify Plugin Loading

Start OpenSimulator and check the console:

```
[DOSProtectorBuilder]: Initializing DOS protector registry
[DOSProtectorBuilder]: Loaded plugin assembly: MyDOSProtectorPlugin.dll
[DOSProtectorBuilder]: Registered CustomDOSProtector for CustomDosProtectorOptions
[DOSProtectorBuilder]: Discovered 3 DOS protector implementations
```

Use console command to verify:

```
dosprotector list
```

## Plugin Structure

### Recommended Project Structure

```
MyDOSProtectorPlugin/
├── MyDOSProtectorPlugin.csproj
├── Options/
│   └── CustomDosProtectorOptions.cs
├── CustomDOSProtector.cs
├── README.md
└── bin/
    └── Release/
        └── net8.0/
            └── MyDOSProtectorPlugin.dll
```

### Options Class

```csharp
using OpenSim.Framework.Security.DOSProtector.Options;

namespace MyDOSProtectorPlugin.Options
{
    public class CustomDosProtectorOptions : BasicDosProtectorOptions
    {
        // Inherit all basic options

        // Add your custom options
        public int CustomThreshold { get; set; } = 100;
        public TimeSpan CustomTimeout { get; set; } = TimeSpan.FromMinutes(5);
        public List<string> CustomAllowList { get; set; } = new();
    }
}
```

### Protector Implementation

```csharp
using OpenSim.Framework.Security.DOSProtector;
using OpenSim.Framework.Security.DOSProtector.Attributes;
using OpenSim.Framework.Security.DOSProtector.Interfaces;
using MyDOSProtectorPlugin.Options;

namespace MyDOSProtectorPlugin
{
    [DOSProtectorOptions(typeof(CustomDosProtectorOptions))]
    public class CustomDOSProtector : BaseDOSProtector
    {
        private readonly CustomDosProtectorOptions _customOptions;

        // Your implementation here
    }
}
```

## Configuration

### INI Configuration Format

The `DOSProtector.ini` file should be placed in the `bin/` directory:

```ini
[DOSProtector]
    ; Enable/disable plugin loading
    EnablePlugins = true

    ; Plugin paths (comma-separated)
    ; Can be:
    ;   - Absolute paths: C:\Plugins\DOSProtectors
    ;   - Relative paths: ./plugins/dosprotectors
    ;   - Direct DLL files: ../MyPlugin.dll
    PluginPaths = "C:\OpenSim\plugins\dosprotectors,./addons/custom-protectors"

    ; Enable verbose logging during plugin discovery
    VerbosePluginLoading = false
```

### Multiple Plugin Paths

```ini
[DOSProtector]
    EnablePlugins = true
    PluginPaths = "C:\Plugins\DOSProtectors,D:\CustomProtectors,./local-plugins"
```

### Relative Paths

Relative paths are resolved from the `bin/` directory:

```ini
[DOSProtector]
    EnablePlugins = true
    ; Resolves to: <opensim>/bin/plugins/dosprotectors
    PluginPaths = "./plugins/dosprotectors"
```

### Direct DLL Files

```ini
[DOSProtector]
    EnablePlugins = true
    ; Load specific DLL
    PluginPaths = "C:\Plugins\MySpecialProtector.dll"
```

## Deployment

### Option 1: Plugin Directory (Recommended)

1. Create a dedicated plugin directory:
   ```bash
   mkdir C:\OpenSim\plugins\dosprotectors
   ```

2. Copy your plugin DLL:
   ```bash
   copy MyDOSProtectorPlugin.dll C:\OpenSim\plugins\dosprotectors\
   ```

3. Configure in INI:
   ```ini
   PluginPaths = "C:\OpenSim\plugins\dosprotectors"
   ```

### Option 2: bin/addons Directory

1. Create addons directory in bin:
   ```bash
   mkdir bin\addons\dosprotectors
   ```

2. Copy plugin DLL:
   ```bash
   copy MyDOSProtectorPlugin.dll bin\addons\dosprotectors\
   ```

3. Configure with relative path:
   ```ini
   PluginPaths = "./addons/dosprotectors"
   ```

### Option 3: Direct DLL Reference

```ini
PluginPaths = "C:\Dev\MyDOSProtectorPlugin\bin\Release\net8.0\MyDOSProtectorPlugin.dll"
```

**Note:** Useful during development for quick iteration.

## Console Commands

### List Discovered Protectors

```
dosprotector list
```

**Output:**
```
Discovered 3 DOS Protector implementation(s):

  1. BasicDOSProtector
     Options Type: BasicDosProtectorOptions

  2. AdvancedDOSProtector
     Options Type: AdvancedDosProtectorOptions

  3. CustomDOSProtector
     Options Type: CustomDosProtectorOptions
```

### Refresh Plugin Cache

Use after deploying a new plugin or updating an existing one:

```
dosprotector refresh
```

**Output:**
```
Refreshing DOS protector plugin cache...
[DOSProtectorBuilder]: Refreshing DOS protector cache
[DOSProtectorBuilder]: Loaded plugin assembly: MyDOSProtectorPlugin.dll
Cache refreshed successfully. Discovered 3 implementation(s).
```

### Reload Configuration

Reload `DOSProtector.ini` without restarting OpenSimulator:

```
dosprotector reload-config
```

**Output:**
```
Reloading DOS protector configuration...
[DOSProtectorConfigLoader]: Loading DOS Protector configuration from bin/DOSProtector.ini
Configuration reloaded successfully.
Discovered 3 DOS protector implementation(s).
```

## Troubleshooting

### Plugin Not Discovered

**Problem:** Plugin DLL is deployed but not showing in `dosprotector list`

**Solutions:**

1. **Check attribute:**
   ```csharp
   [DOSProtectorOptions(typeof(YourOptionsType))]  // Must be present
   public class YourDOSProtector : BaseDOSProtector
   ```

2. **Verify path in INI:**
   ```ini
   PluginPaths = "C:\Plugins\DOSProtectors"  ; Check path exists
   ```

3. **Check logs:**
   ```
   [DOSProtectorBuilder]: Could not load plugin assembly: <reason>
   ```

4. **Refresh cache:**
   ```
   dosprotector refresh
   ```

### Assembly Loading Errors

**Problem:** `FileNotFoundException` or `BadImageFormatException`

**Solutions:**

1. **Ensure correct target framework:**
   - Plugin must target same .NET version as OpenSimulator
   - Check project file: `<TargetFramework>net8.0</TargetFramework>`

2. **Include all dependencies:**
   - Copy referenced DLLs to plugin directory
   - Or reference OpenSimulator DLLs directly (don't copy)

3. **Check assembly architecture:**
   - Must match OpenSimulator (x64/x86)

### Duplicate Protector Warning

**Problem:** `Duplicate protector for CustomDosProtectorOptions, replacing...`

**Cause:** Multiple plugins implement the same options type

**Solutions:**

1. **Rename your options type:**
   ```csharp
   public class MyCustomDosProtectorOptions : BasicDosProtectorOptions
   ```

2. **Remove old plugin:**
   ```bash
   del C:\Plugins\DOSProtectors\OldVersion.dll
   ```

3. **Refresh cache:**
   ```
   dosprotector refresh
   ```

### Plugin Not Taking Effect

**Problem:** Plugin loads but doesn't seem to work

**Verifications:**

1. **Confirm options type is used:**
   ```csharp
   var options = new CustomDosProtectorOptions { ... };
   var protector = DOSProtectorBuilder.Build(options);
   // Verify protector type
   ```

2. **Check logs for your protector messages:**
   ```csharp
   Log(DOSProtectorLogLevel.Info, "[CustomDOSProtector]: Initialized");
   ```

3. **Test with debugger:**
   - Attach debugger to OpenSim.exe
   - Set breakpoint in your protector methods

## Best Practices

### 1. Version Your Plugin

Include version info in assembly:

```csharp
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
```

### 2. Include Dependencies

If your plugin uses external libraries:

```xml
<ItemGroup>
    <PackageReference Include="SomeLibrary" Version="1.0.0" />
</ItemGroup>
```

Copy dependencies to plugin directory:

```xml
<PropertyGroup>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
</PropertyGroup>
```

### 3. Namespace Conventions

```csharp
namespace CompanyName.OpenSim.DOSProtectors
{
    // Your plugin code
}
```

### 4. Logging Prefix

Use consistent logging prefix:

```csharp
Log(DOSProtectorLogLevel.Info, "[MyPlugin]: Starting initialization");
```

### 5. Dispose Pattern

Always clean up resources:

```csharp
public override void Dispose()
{
    _timer?.Stop();
    _timer?.Dispose();
    _cache?.Clear();
}
```

### 6. Configuration Validation

Validate options in constructor:

```csharp
public CustomDOSProtector(CustomDosProtectorOptions options)
    : base(options)
{
    if (options.CustomThreshold < 1)
        throw new ArgumentException("CustomThreshold must be positive");

    // Continue initialization
}
```

## Example: Complete Plugin

See [CUSTOMIZE.md](CUSTOMIZE.md) for complete implementation examples including:
- Reputation-based protector
- Geographic filtering protector
- Database-backed protector
- Hybrid multi-strategy protector

## Further Reading

- [README.md](README.md) - Overview and features
- [USAGE.md](USAGE.md) - Usage patterns and scenarios
- [CUSTOMIZE.md](CUSTOMIZE.md) - Detailed customization guide
