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
using Mono.Addins;
using Nini.Config;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Interfaces
{
    [TypeExtensionPoint(Path = "/OpenSim/RegionModules", NodeName="RegionModule")]
    public interface IRegionModuleBase
    {
        /// <value>
        /// The name of the module
        /// </value>
        string Name { get; }

        /// <summary>
        /// If this returns non-null, it is the type of an interface that
        /// this module intends to register.
        /// This will cause the loader to defer loading of this module
        /// until all other modules have been loaded. If no other module
        /// has registered the interface by then, this module will be
        /// activated, else it will remain inactive, letting the other module
        /// take over. This should return non-null ONLY in modules that are
        /// intended to be easily replaceable, e.g. stub implementations
        /// that the developer expects to be replaced by third party provided
        /// modules.
        /// </summary>
        Type ReplaceableInterface { get; }

        /// <summary>
        /// This is called to initialize the region module. For shared modules, this is called
        /// exactly once, after creating the single (shared) instance. For non-shared modules,
        /// this is called once on each instance, after the instace for the region has been created.
        /// </summary>
        /// <param name="source">
        /// A <see cref="IConfigSource"/>
        /// </param>
        void Initialise(IConfigSource source);

        /// <summary>
        /// This is the inverse to <see cref="Initialise"/>. After a Close(), this instance won't be usable anymore.
        /// </summary>
        void Close();

        /// <summary>
        /// This is called whenever a <see cref="Scene"/> is added. For shared modules, this can happen several times.
        /// For non-shared modules, this happens exactly once, after <see cref="Initialise"/> has been called.
        /// </summary>
        /// <param name="scene">
        /// A <see cref="Scene"/>
        /// </param>
        void AddRegion(Scene scene);

        /// <summary>
        /// This is called whenever a <see cref="Scene"/> is removed. For shared modules, this can happen several times.
        /// For non-shared modules, this happens exactly once, if the scene this instance is associated with is removed.
        /// </summary>
        /// <param name="scene">
        /// A <see cref="Scene"/>
        /// </param>
        void RemoveRegion(Scene scene);

        /// <summary>
        /// This will be called once for every scene loaded. In a shared module
        /// this will be multiple times in one instance, while a nonshared
        /// module instance will only be called once.
        /// This method is called after AddRegion has been called in all
        /// modules for that scene, providing an opportunity to request 
        /// another module's interface, or hook an event from another module.
        /// </summary>
        /// <param name="scene">
        /// A <see cref="Scene"/>
        /// </param>
        void RegionLoaded(Scene scene);
    }

}
