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
using System.IO;
using System.Text;
using System.Timers;
using Nini.Config;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Autooar
{
    public class AutooarModule : IRegionModule
    {
        private readonly Timer m_timer = new Timer(60000*20);
        private readonly List<Scene> m_scenes = new List<Scene>();
        private IConfigSource config;
        private bool m_enabled = false;
        

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scenes.Add(scene);
            config = source;
        }

        public void PostInitialise()
        {
            if (config.Configs["autooar"] != null)
            {
                m_enabled = config.Configs["autooar"].GetBoolean("Enabled", m_enabled);
            }

            if (m_enabled)
            {
                m_timer.Elapsed += m_timer_Elapsed;
                m_timer.AutoReset = true;
                m_timer.Start();
            }
        }

        void m_timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!Directory.Exists("autooars"))
                Directory.CreateDirectory("autooars");

            foreach (Scene scene in m_scenes)
            {
                IRegionArchiverModule archiver = scene.RequestModuleInterface<IRegionArchiverModule>();

                archiver.ArchiveRegion(Path.Combine("autooars",
                                                    scene.RegionInfo.RegionName + "_" + scene.RegionInfo.RegionLocX +
                                                    "x" + scene.RegionInfo.RegionLocY + ".oar.tar.gz"));
            }
        }

        public void Close()
        {
            if (m_timer.Enabled)
                m_timer.Stop();
        }

        public string Name
        {
            get { return "Automatic OAR Module"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }
    }
}
