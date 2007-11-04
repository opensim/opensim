using System;
using System.Collections.Generic;
using System.Text;
using Mono.Addins;
using Mono.Addins.Description;

[assembly: AddinRoot("OpenSim", "0.4")]
namespace OpenSim
{
    [TypeExtensionPoint("/OpenSim/Startup")]
    public interface IApplicationPlugin
    {
        void Initialise(OpenSimMain openSim);
        void Close();
    }
}

