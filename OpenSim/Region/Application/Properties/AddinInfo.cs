using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly: AddinRoot("OpenSim", OpenSim.VersionInfo.VersionNumber)]
[assembly: ImportAddinAssembly("OpenSim.Framework.dll")]

