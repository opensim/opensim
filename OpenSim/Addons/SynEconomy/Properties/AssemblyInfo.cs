using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Mono.Addins;

[assembly: AssemblyTitle("OpenSim.Addons.SynEconomy")]
[assembly: AssemblyDescription("Avatar-balance money module (SynGrid)")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("http://opensimulator.org")]
[assembly: AssemblyProduct("OpenSim.Addons.SynEconomy")]
[assembly: AssemblyCopyright("Copyright (c) OpenSimulator.org Developers")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("e10d9b3a-1b7a-4d3e-9a3b-7e9b2c1d5f7a")]

[assembly: AssemblyVersion(OpenSim.VersionInfo.AssemblyVersionNumber)]

[assembly: Addin("OpenSim.SynEconomy", OpenSim.VersionInfo.VersionNumber)]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]
