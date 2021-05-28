using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly: Addin("OpenSim.ApplicationPlugins.RegionModulesController", OpenSim.VersionInfo.VersionNumber)]
[assembly: AddinDependency("OpenSim", OpenSim.VersionInfo.VersionNumber)]