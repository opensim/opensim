using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Mono.Addins;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("OpenSim")]
[assembly: AssemblyDescription("The executable for for simulator")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("http://opensimulator.org")]
[assembly: AssemblyProduct("OpenSim")]
[assembly: AssemblyCopyright("OpenSimulator developers")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("f6700ed5-1e6f-44d8-8397-e5eac42b3856")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
[assembly: AssemblyVersion(OpenSim.VersionInfo.AssemblyVersionNumber)]

[assembly: AddinRoot("OpenSim", OpenSim.VersionInfo.VersionNumber)]
[assembly: ImportAddinAssembly("OpenSim.Framework.dll")]
