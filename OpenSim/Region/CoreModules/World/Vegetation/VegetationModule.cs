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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Vegetation
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "VegetationModule")]
    public class VegetationModule : INonSharedRegionModule, IVegetationModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Scene m_scene;

        protected static readonly PCode[] creationCapabilities = new PCode[] { PCode.Grass, PCode.NewTree, PCode.Tree };
        public PCode[] CreationCapabilities { get { return creationCapabilities; } }

        public void Initialise(IConfigSource source)
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_scene.RegisterModuleInterface<IVegetationModule>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            m_scene.UnregisterModuleInterface<IVegetationModule>(this);
        }

        public void Close() {}
        public string Name { get { return "Vegetation Module"; } }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public SceneObjectGroup AddTree(
            UUID uuid, UUID groupID, Vector3 scale, Quaternion rotation, Vector3 position, Tree treeType, bool newTree)
        {
            PrimitiveBaseShape treeShape = new PrimitiveBaseShape();
            treeShape.PathCurve = 16;
            treeShape.PathEnd = 49900;
            treeShape.PCode = newTree ? (byte)PCode.NewTree : (byte)PCode.Tree;
            treeShape.Scale = scale;
            treeShape.State = (byte)treeType;

            return m_scene.AddNewPrim(uuid, groupID, position, rotation, treeShape);
        }

        public SceneObjectGroup CreateEntity(
            UUID ownerID, UUID groupID, Vector3 pos, Quaternion rot, PrimitiveBaseShape shape)
        {
            if (Array.IndexOf(creationCapabilities, (PCode)shape.PCode) < 0)
            {
                m_log.DebugFormat("[VEGETATION]: PCode {0} not handled by {1}", shape.PCode, Name);
                return null;
            }

            SceneObjectGroup sceneObject = new SceneObjectGroup(ownerID, pos, rot, shape);
            SceneObjectPart rootPart = sceneObject.GetPart(sceneObject.UUID);

            // if grass or tree, make phantom
            //rootPart.TrimPermissions();
            rootPart.AddFlag(PrimFlags.Phantom);
            if (rootPart.Shape.PCode != (byte)PCode.Grass)
                AdaptTree(ref shape);

            sceneObject.SetGroup(groupID, null);
            m_scene.AddNewSceneObject(sceneObject, true);
            sceneObject.AggregatePerms();

            return sceneObject;
        }

        protected void AdaptTree(ref PrimitiveBaseShape tree)
        {
            // Tree size has to be adapted depending on its type
            switch ((Tree)tree.State)
            {
                case Tree.Cypress1:
                case Tree.Cypress2:
                    tree.Scale *= new Vector3(8, 8, 20);
                    break;

                // case... other tree types
                // tree.Scale *= new Vector3(?, ?, ?);
                // break;

                default:
                    tree.Scale *= new Vector3(8, 8, 8);
                    break;
            }
        }
    }
}
