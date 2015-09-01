/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;

namespace OpenSim.Framework
{
    /// <summary>
    /// Exception thrown if Initialise has been called, but failed.
    /// </summary>
    public class PluginNotInitialisedException : Exception
    {
        public PluginNotInitialisedException () : base() {}
        public PluginNotInitialisedException (string msg) : base(msg) {}
        public PluginNotInitialisedException (string msg, Exception e) : base(msg, e) {}
    }

    /// <summary>
    /// This interface, describes a generic plugin
    /// </summary>
    public interface IPlugin : IDisposable
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
        /// Default-initialises the plugin
        /// </summary>
        void Initialise();
    }

    /// <summary>
    /// Any plugins which need to pass parameters to their initialisers must
    /// inherit this class and use it to set the PluginLoader Initialiser property
    /// </summary>
    public class PluginInitialiserBase
    {
        // this would be a lot simpler if C# supported currying or typedefs

        // default initialisation
        public virtual void Initialise (IPlugin plugin)
        {
            plugin.Initialise();
        }
    }

}
