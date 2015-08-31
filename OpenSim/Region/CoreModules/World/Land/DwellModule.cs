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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenMetaverse.Messages.Linden;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.CoreModules.Framework.InterfaceCommander;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Services.Interfaces;
using Caps = OpenSim.Framework.Capabilities.Caps;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.World.Land
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "DefaultDwellModule")]
    public class DefaultDwellModule : IDwellModule, INonSharedRegionModule
    {
        private Scene m_scene;
        private IConfigSource m_Config;
        private bool m_Enabled = false;

        public Type ReplaceableInterface
        {
            get { return typeof(IDwellModule); }
        }

        public string Name
        {
            get { return "DefaultDwellModule"; }
        }

        public void Initialise(IConfigSource source)
        {
            m_Config = source;

            IConfig DwellConfig = m_Config.Configs ["Dwell"];

            if (DwellConfig == null) {
                m_Enabled = false;
                return;
            }
            m_Enabled = (DwellConfig.GetString ("DwellModule", "DefaultDwellModule") == "DefaultDwellModule");
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            m_scene = scene;

            m_scene.EventManager.OnNewClient += OnNewClient;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void Close()
        {
        }

        public void OnNewClient(IClientAPI client)
        {
            client.OnParcelDwellRequest += ClientOnParcelDwellRequest;
        }

        private void ClientOnParcelDwellRequest(int localID, IClientAPI client)
        {
            ILandObject parcel = m_scene.LandChannel.GetLandObject(localID);
            if (parcel == null)
                return;

            client.SendParcelDwellReply(localID, parcel.LandData.GlobalID, parcel.LandData.Dwell);
        }

        public int GetDwell(UUID parcelID)
        {
            return 0;
        }
    }
}
