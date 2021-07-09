using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly: Addin("OpenSim.ApplicationPlugins.RemoteController", OpenSim.VersionInfo.VersionNumber)]
[assembly: AddinDependency("OpenSim", OpenSim.VersionInfo.VersionNumber)]
