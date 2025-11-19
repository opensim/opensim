Welcome to OpenSimulator (OpenSim for short)!

# Overview

OpenSim is a BSD Licensed Open Source project to develop a functioning
virtual worlds server platform capable of supporting multiple clients
and servers in a heterogeneous grid structure. OpenSim is written in
C#, and can run under Mono or the Microsoft .NET runtimes.

This is considered an alpha release.  Some stuff works, a lot doesn't.
If it breaks, you get to keep *both* pieces.

## 🚀 **OpenSim Modernization Project - JULES Fork**

This fork contains **comprehensive modernization work** to bring OpenSim to modern gaming standards while maintaining backward compatibility. We have completed extensive research, planning, and documentation for a complete transformation of OpenSim's graphics and physics engines.

### 📋 **Current Modernization Status: Documentation Phase Complete**

✅ **Research & Analysis Complete** - Over 9,000 lines of technical documentation  
✅ **Implementation Roadmaps Created** - 24-month development timeline  
✅ **Technical Specifications Complete** - Ready for development phase  
✅ **Performance Benchmarks Established** - Clear success metrics defined  

### 🎯 **Modernization Objectives**

- **Graphics Engine**: Upgrade from Warp3D to modern PBR rendering with Vulkan/DirectX support
- **Physics Engine**: Integrate Jolt Physics alongside optimized Bullet Physics
- **Performance**: Target 60 FPS at 1080p with 1000+ dynamic objects
- **Quality**: Achieve modern game engine visual quality standards
- **Compatibility**: Maintain 100% backward compatibility with existing content

### 📚 **Complete Technical Documentation**

Our modernization project includes comprehensive technical documentation:

#### Core Implementation Guides
- **[📋 Documentation Index](DOCUMENTATION_INDEX.md)** - Complete guide to all modernization documents
- **[🗺️ Comprehensive Development Roadmap](COMPREHENSIVE_DEVELOPMENT_ROADMAP.md)** - Master 24-month implementation plan
- **[🎨 Graphics Engine Modernization](GRAPHICS_ENGINE_MODERNIZATION.md)** - PBR rendering, modern pipeline, cross-platform graphics
- **[⚡ Physics Engine Implementation Guide](PHYSICS_ENGINE_IMPLEMENTATION_GUIDE.md)** - Jolt Physics integration, vehicle systems, performance optimization

#### Research and Analysis
- **[🔬 Physics Engine Research](PHYSICS_ENGINE_RESEARCH.md)** - Comprehensive analysis of modern physics engines
- **[🏗️ Architecture Analysis](ARCHITECTURE_ANALYSIS.md)** - Current system analysis and improvement opportunities
- **[⚡ Immediate Improvements](IMMEDIATE_IMPROVEMENTS.md)** - Quick wins and performance optimizations
- **[📊 Task Tracker](TASK_TRACKER.md)** - Active project management and progress tracking

#### Specialized Implementation Plans
- **[🎮 PhysX Implementation Plan](PHYSX_IMPLEMENTATION_PLAN.md)** - Alternative enterprise-grade physics engine
- **[📈 Implementation Summary](IMPLEMENTATION_SUMMARY.md)** - Summary of completed enhancements
- **[🚀 Next Phase Roadmap](NEXT_PHASE_ROADMAP.md)** - GUI tools and management improvements

### 🏆 **Key Modernization Features Planned**

#### Graphics Engine Transformation
- **Physically-Based Rendering (PBR)**: Complete material workflow with metallic/roughness maps
- **Modern Lighting**: Real-time global illumination, deferred rendering, advanced shadows
- **Cross-Platform Graphics**: Vulkan, DirectX 12, OpenGL support via Silk.NET
- **Post-Processing Pipeline**: TAA, SSAO, bloom, motion blur, depth of field
- **Performance Optimization**: GPU culling, instanced rendering, dynamic LOD

#### Physics Engine Modernization
- **Jolt Physics Integration**: Next-generation physics with C# bindings
- **Advanced Vehicle Physics**: Professional-grade vehicle simulation
- **Character Controllers**: Modern avatar physics and movement
- **Soft Body Simulation**: Cloth, fluids, and deformable objects
- **Performance Scaling**: Multi-threading, spatial partitioning, 1000+ objects

#### Next-Generation Features
- **Ray Tracing Support**: Optional RT reflections and global illumination
- **VR/AR Integration**: Native VR rendering pipeline and AR tracking
- **AI-Powered Optimization**: Machine learning-based performance tuning
- **Cloud-Native Features**: Distributed simulation and rendering

### 📊 **Performance Targets**

Our modernization aims to achieve:
- **Graphics**: 60 FPS at 1080p on mid-range hardware (GTX 1060/RX 580 class)
- **Physics**: 1000+ dynamic objects at 60Hz simulation rate
- **Quality**: Modern game engine visual quality standards
- **Compatibility**: 100% backward compatibility maintained
- **Platforms**: Windows, Linux, macOS with consistent performance

### 🛣️ **Implementation Timeline**

**Phase 1** (Months 1-3): Graphics Foundation
- Warp3D replacement with modern renderer
- PBR material system implementation
- VectorRender modernization with SkiaSharp

**Phase 2** (Months 4-8): Physics Transformation  
- Jolt Physics C# bindings and integration
- Advanced vehicle and character physics
- Performance optimization and testing

**Phase 3** (Months 9-12): Advanced Features
- Ray tracing and global illumination
- VR/AR support and cloud-native features
- Enterprise deployment tools

### 🤝 **Community and Development**

**Getting Started with Modernization**:
1. Review the [Documentation Index](DOCUMENTATION_INDEX.md) for complete technical overview
2. Check [Task Tracker](TASK_TRACKER.md) for current development priorities  
3. Read implementation guides for areas of interest
4. Join the modernization effort through community contributions

**For Developers**: Start with [Comprehensive Development Roadmap](COMPREHENSIVE_DEVELOPMENT_ROADMAP.md)  
**For Project Managers**: Review success metrics and timeline in master roadmap  
**For Contributors**: Check [Task Tracker](TASK_TRACKER.md) for contribution opportunities

# Compiling OpenSim

Please see BUILDING.md

# Running OpenSim on Windows

You will need dotnet 8.0 runtime (https://dotnet.microsoft.com/en-us/download/dotnet/8.0)


To run OpenSim from a command prompt

 * cd to the bin/ directory where you unpacked OpenSim
 * review and change configuration files (.ini) for your needs. see the "Configuring OpenSim" section
 * run OpenSim.exe


# Running OpenSim on Linux/Mac

You will need

 * [dotnet 8.0 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
 * libgdiplus 
 
 if you have mono 6.x complete, you already have libgdiplus, otherwise you need to install it
 using a package manager for your operating system, like apt, brew, macports, etc
 for example on debian:
 
 `apt-get update && apt-get install -y apt-utils libgdiplus libc6-dev`
 
To run OpenSim, from the unpacked distribution type:

 * cd bin
 * review and change configuration files (.ini) for your needs. see the "Configuring OpenSim" section
 * run ./opensim.sh


# Configuring OpenSim

When OpenSim starts for the first time, you will be prompted with a
series of questions that look something like:

	[09-17 03:54:40] DEFAULT REGION CONFIG: Simulator Name [OpenSim Test]:

For all the options except simulator name, you can safely hit enter to accept
the default if you want to connect using a client on the same machine or over
your local network.

You will then be asked "Do you wish to join an existing estate?".  If you're
starting OpenSim for the first time then answer no (which is the default) and
provide an estate name.

Shortly afterwards, you will then be asked to enter an estate owner first name,
last name, password and e-mail (which can be left blank).  Do not forget these
details, since initially only this account will be able to manage your region
in-world.  You can also use these details to perform your first login.

Once you are presented with a prompt that looks like:

	Region (My region name) #

You have successfully started OpenSim.

If you want to create another user account to login rather than the estate
account, then type "create user" on the OpenSim console and follow the prompts.

Helpful resources:
 * http://opensimulator.org/wiki/Configuration
 * http://opensimulator.org/wiki/Configuring_Regions

# Connecting to your OpenSim

By default your sim will be available for login on port 9000.  You can login by
adding -loginuri http://127.0.0.1:9000 to the command that starts Second Life
(e.g. in the Target: box of the client icon properties on Windows).  You can
also login using the network IP address of the machine running OpenSim (e.g.
http://192.168.1.2:9000)

To login, use the avatar details that you gave for your estate ownership or the
one you set up using the "create user" command.

# Bug reports

In the very likely event of bugs biting you (err, your OpenSim) we
encourage you to see whether the problem has already been reported on
the [OpenSim mantis system](http://opensimulator.org/mantis/main_page.php).

If your bug has already been reported, you might want to add to the
bug description and supply additional information.

If your bug has not been reported yet, file a bug report ("opening a
mantis"). Useful information to include:
 * description of what went wrong
 * stack trace
 * OpenSim.log (attach as file)
 * OpenSim.ini (attach as file)


# More Information on OpenSim

More extensive information on building, running, and configuring
OpenSim, as well as how to report bugs, and participate in the OpenSim
project can always be found at http://opensimulator.org.

Thanks for trying OpenSim, we hope it is a pleasant experience.

