using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly: Addin("LindenUDP", OpenSim.VersionInfo.AssemblyVersionNumber)]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.AssemblyVersionNumber)]
