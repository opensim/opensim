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
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

// You will need to uncomment these lines if you are adding a region module to some other assembly which does not already
// specify its assembly.  Otherwise, the region modules in the assembly will not be picked up when OpenSimulator scans
// the available DLLs
//[assembly: Addin("MyModule", "1.0")]
//[assembly: AddinDependency("OpenSim", "0.8.1")]

namespace OpenSim.Region.OptionalModules.Example.BareBonesShared
{
    /// <summary>
    /// Simplest possible example of a shared region module.
    /// </summary>
    /// <remarks>
    /// This module is the simplest possible example of a shared region module (a module which is shared by every
    /// scene/region running on the simulator).  If anybody wants to create a more complex example in the future then 
    /// please create a separate class.
    /// 
    /// This module is not active by default.  If you want to see it in action, 
    /// then just uncomment the line below starting with [Extension(Path...
    /// 
    /// When the module is enabled it will print messages when it receives certain events to the screen and the log
    /// file.
    /// </remarks>
    //[Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "BareBonesSharedModule")]
    public class BareBonesSharedModule : ISharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);                
        
        public string Name { get { return "Bare Bones Shared Module"; } }        
        
        public Type ReplaceableInterface { get { return null; } }
        
        public void Initialise(IConfigSource source)
        {
            m_log.DebugFormat("[BARE BONES SHARED]: INITIALIZED MODULE");
        }
        
        public void PostInitialise()
        {
            m_log.DebugFormat("[BARE BONES SHARED]: POST INITIALIZED MODULE");
        }
        
        public void Close()
        {
            m_log.DebugFormat("[BARE BONES SHARED]: CLOSED MODULE");
        }
        
        public void AddRegion(Scene scene)
        {
            m_log.DebugFormat("[BARE BONES SHARED]: REGION {0} ADDED", scene.RegionInfo.RegionName);
        }
        
        public void RemoveRegion(Scene scene)
        {
            m_log.DebugFormat("[BARE BONES SHARED]: REGION {0} REMOVED", scene.RegionInfo.RegionName);
        }        
        
        public void RegionLoaded(Scene scene)
        {
            m_log.DebugFormat("[BARE BONES SHARED]: REGION {0} LOADED", scene.RegionInfo.RegionName);
        }                
    }
}