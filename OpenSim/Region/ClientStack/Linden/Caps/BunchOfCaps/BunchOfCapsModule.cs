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
using System.Collections.Generic;
using System.Reflection;

using log4net;
using Nini.Config;
using OpenMetaverse;
using Mono.Addins;

using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps = OpenSim.Framework.Capabilities.Caps;

[assembly: Addin("LindenCaps", OpenSim.VersionInfo.VersionNumber)]
[assembly: AddinDependency("OpenSim.Region.Framework", OpenSim.VersionInfo.VersionNumber)]
namespace OpenSim.Region.ClientStack.Linden
{

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "BunchOfCapsModule")]
    public class BunchOfCapsModule : INonSharedRegionModule
    {
//        private static readonly ILog m_log =
//            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Scene m_Scene;

        #region INonSharedRegionModule

        public string Name { get { return "BunchOfCapsModule"; } }

        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source)
        {
        }

        public void Close() { } 

        public void AddRegion(Scene scene)
        {
            m_Scene = scene;
            m_Scene.EventManager.OnRegisterCaps += OnRegisterCaps;
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void PostInitialise() { }
        #endregion 

        private void OnRegisterCaps(UUID agentID, Caps caps)
        {
            new BunchOfCaps(m_Scene, caps);
        }

    }
}
