using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly: Addin("OpenSim.ApplicationPlugins.LoadRegions", OpenSim.VersionInfo.AssemblyVersionNumber)]
[assembly: AddinDependency("OpenSim", OpenSim.VersionInfo.AssemblyVersionNumber)]
