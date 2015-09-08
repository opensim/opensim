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
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenMetaverse;
using Nini.Config;
using Mono.Addins;
using log4net;

/*
 * This is the zero mesher.
 * Whatever you want him to mesh, he can't, telling you that by responding with a null pointer.
 * Effectivly this is for switching off meshing and for testing as each physics machine should deal
 * with the null pointer situation.
 * But it's also a convenience thing, as physics machines can rely on having a mesher in any situation, even
 * if it's a dump one like this.
 * Note, that this mesher is *not* living in a module but in the manager itself, so
 * it's always availabe and thus the default in case of configuration errors
*/

namespace OpenSim.Region.PhysicsModule.Meshing
{

    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ZeroMesher")]
    public class ZeroMesher : IMesher, INonSharedRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private bool m_Enabled = false;

        #region INonSharedRegionModule
        public string Name
        {
            get { return "ZeroMesher"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void Initialise(IConfigSource source)
        {
            // TODO: Move this out of Startup
            IConfig config = source.Configs["Startup"];
            if (config != null)
            {
                // This is the default Mesher
                string mesher = config.GetString("meshing", Name);
                if (mesher == Name)
                    m_Enabled = true;
            }
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.RegisterModuleInterface<IMesher>(this);
        }

        public void RemoveRegion(Scene scene)
        {
            if (!m_Enabled)
                return;

            scene.UnregisterModuleInterface<IMesher>(this);
        }

        public void RegionLoaded(Scene scene)
        {
            if (!m_Enabled)
                return;
        }
        #endregion

        #region IMesher
        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod)
        {
            return CreateMesh(primName, primShape, size, lod, false);
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool shouldCache, bool convex, bool forOde)
        {
            return CreateMesh(primName, primShape, size, lod, false);
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool convex,bool forOde)
        {
            return CreateMesh(primName, primShape, size, lod, false);
        }

        public IMesh CreateMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical)
        {
            // Remove the reference to the encoded JPEG2000 data so it can be GCed
            primShape.SculptData = OpenMetaverse.Utils.EmptyBytes;

            return null;
        }

        public IMesh GetMesh(String primName, PrimitiveBaseShape primShape, Vector3 size, float lod, bool isPhysical, bool convex)
        {
            return null;
        }

        public void ReleaseMesh(IMesh mesh) { }
        public void ExpireReleaseMeshs() { }
        public void ExpireFileCache() { }

        #endregion
    }
}
