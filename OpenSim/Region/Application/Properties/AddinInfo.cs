using System;
using Mono.Addins;
using Mono.Addins.Description;

[assembly: AddinRoot("OpenSim", OpenSim.VersionInfo.AssemblyVersionNumber)]
[assembly: ImportAddinAssembly("OpenSim.Framework.dll")]

