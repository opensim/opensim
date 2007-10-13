using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Interfaces
{
    /// <summary>
    /// This interface, describes a generic plugin
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// Returns the plugin version
        /// </summary>
        /// <returns>Plugin version in MAJOR.MINOR.REVISION.BUILD format</returns>
        string Version { get; }

        /// <summary>
        /// Returns the plugin name
        /// </summary>
        /// <returns>Plugin name, eg MySQL User Provider</returns>
        string Name { get; }

        /// <summary>
        /// Initialises the plugin (artificial constructor)
        /// </summary>
        void Initialise();
    }
}
