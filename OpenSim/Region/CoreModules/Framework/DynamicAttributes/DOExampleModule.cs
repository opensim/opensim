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
using OpenSim.Region.CoreModules.Framework.DynamicAttributes.DAExampleModule;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.DynamicAttributes.DOExampleModule
{
    /// <summary>
    /// Example module for experimenting with and demonstrating dynamic object ideas.
    /// </summary>
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "DOExampleModule")]
    public class DOExampleModule : INonSharedRegionModule
    {
        public class MyObject
        {
            public int Moves { get; set; }

            public MyObject(int moves)
            {
                Moves = moves;
            }
        }

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static readonly bool ENABLED = false;   // enable for testing

        private Scene m_scene;
        private IDialogModule m_dialogMod;

        public string Name { get { return "DO"; } }
        public Type ReplaceableInterface { get { return null; } }

        public void Initialise(IConfigSource source) {}

        public void AddRegion(Scene scene)
        {
            if (ENABLED)
            {
                m_scene = scene;
                m_scene.EventManager.OnObjectAddedToScene += OnObjectAddedToScene;
                m_scene.EventManager.OnSceneGroupMove += OnSceneGroupMove;
                m_dialogMod = m_scene.RequestModuleInterface<IDialogModule>();
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

        private void OnObjectAddedToScene(SceneObjectGroup so)
        {
            SceneObjectPart rootPart = so.RootPart;

            OSDMap attrs;

            int movesSoFar = 0;

//            Console.WriteLine("Here for {0}", so.Name);

            if (rootPart.DynAttrs.TryGetStore(DAExampleModule.Namespace, DAExampleModule.StoreName, out attrs))
            {
                movesSoFar = attrs["moves"].AsInteger();

                m_log.DebugFormat(
                    "[DO EXAMPLE MODULE]: Found saved moves {0} for {1} in {2}", movesSoFar, so.Name, m_scene.Name);
            }

            rootPart.DynObjs.Add(DAExampleModule.Namespace, Name, new MyObject(movesSoFar));
        }

        private bool OnSceneGroupMove(UUID groupId, Vector3 delta)
        {
            SceneObjectGroup so = m_scene.GetSceneObjectGroup(groupId);

            if (so == null)
                return true;

            object rawObj = so.RootPart.DynObjs.Get(Name);

            if (rawObj != null)
            {
                MyObject myObj = (MyObject)rawObj;

                m_dialogMod.SendGeneralAlert(string.Format("{0} {1} moved {2} times", so.Name, so.UUID, ++myObj.Moves));
            }

            return true;
        }
    }
}