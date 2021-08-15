using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly: Addin("OpenSim.Region.PhysicsModule.BulletS", OpenSim.VersionInfo.AssemblyVersionNumber)]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.AssemblyVersionNumber)]
