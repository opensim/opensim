using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly: Addin("OpenSim.Region.OptionalModules", OpenSim.VersionInfo.VersionNumber)]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]
