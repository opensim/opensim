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
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.Packets;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.Framework.DynamicAttributes.DAExampleModule
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "DAExampleModule")]
    public class DAExampleModule : INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly bool ENABLED = false;   // enable for testing

        public const string Namespace = "Example";
        public const string StoreName = "DA";

        protected Scene m_scene;
        protected IDialogModule m_dialogMod;

        public string Name { get { return "DAExample Module"; } }
        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source) {}

        public void AddRegion(Scene scene)
        {
            if (ENABLED)
            {
                m_scene = scene;
                m_scene.EventManager.OnSceneGroupMove += OnSceneGroupMove;
                m_dialogMod = m_scene.RequestModuleInterface<IDialogModule>();

                m_log.DebugFormat("[DA EXAMPLE MODULE]: Added region {0}", m_scene.Name);
            }
        }

        public void RemoveRegion(Scene scene)
        {
            if (ENABLED)
            {
                m_scene.EventManager.OnSceneGroupMove -= OnSceneGroupMove;
            }
        }

        public void RegionLoaded(Scene scene) {}

        public void Close()
        {
            RemoveRegion(m_scene);
        }

        protected bool OnSceneGroupMove(UUID groupId, Vector3 delta)
        {
            OSDMap attrs = null;
            SceneObjectPart sop = m_scene.GetSceneObjectPart(groupId);

            if (sop == null)
                return true;

            if (!sop.DynAttrs.TryGetStore(Namespace, StoreName, out attrs))
                attrs = new OSDMap();

            OSDInteger newValue;

            // We have to lock on the entire dynamic attributes map to avoid race conditions with serialization code.
            lock (sop.DynAttrs)
            {
                if (!attrs.ContainsKey("moves"))
                    newValue = new OSDInteger(1);
                else
                    newValue = new OSDInteger(attrs["moves"].AsInteger() + 1);

                attrs["moves"] = newValue;

                sop.DynAttrs.SetStore(Namespace, StoreName, attrs);
            }

            sop.ParentGroup.HasGroupChanged = true;

            string msg = string.Format("{0} {1} moved {2} times", sop.Name, sop.UUID, newValue);
            m_log.DebugFormat("[DA EXAMPLE MODULE]: {0}", msg);
            m_dialogMod.SendGeneralAlert(msg);

            return true;
        }
    }
}