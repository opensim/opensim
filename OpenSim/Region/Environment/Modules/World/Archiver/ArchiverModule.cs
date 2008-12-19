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
 *     * Neither the name of the OpenSim Project nor the
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

using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.World.Serialiser;
using OpenSim.Region.Environment.Scenes;
using System.Collections.Generic;
using System.Reflection;
using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Region.Environment.Modules.World.Archiver
{
    /// <summary>
    /// This module loads and saves OpenSimulator archives
    /// </summary>
    public class ArchiverModule : IRegionModule, IRegionArchiverModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Scene to which this module belongs
        /// </summary>
        /// <param name="scene"></param>
        /// <param name="source"></param>
        private Scene m_scene;

        public string Name { get { return "ArchiverModule"; } }

        public bool IsSharedModule { get { return false; } }

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IRegionArchiverModule>(this);
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public void ArchiveRegion(string savePath)
        {
            m_log.InfoFormat("[SCENE]: Writing archive for region {0} to {1}", m_scene.RegionInfo.RegionName, savePath);
            
            new ArchiveWriteRequestPreparation(m_scene, savePath).ArchiveRegion();
        }

        public void DearchiveRegion(string loadPath)
        {
            m_log.InfoFormat("[SCENE]: Loading archive to region {0} from {1}", m_scene.RegionInfo.RegionName, loadPath);
            
            new ArchiveReadRequest(m_scene, loadPath);
        }
    }
}
