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

using System.Collections.Generic;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenSim.Framework;
using OpenMetaverse;

namespace OpenSim.Region.PhysicsModules.SharedBase
{
    class NullPhysicsScene : PhysicsScene
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private static int m_workIndicator;

        public override PhysicsActor AddAvatar(
            string avName, Vector3 position, Vector3 velocity, Vector3 size, bool isFlying)
        {
            m_log.InfoFormat("[PHYSICS]: NullPhysicsScene : AddAvatar({0})", position);
            return PhysicsActor.Null;
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
        }

        public override void RemovePrim(PhysicsActor prim)
        {
        }
        public override void SetWaterLevel(float baseheight)
        {

        }

/*
        public override PhysicsActor AddPrim(Vector3 position, Vector3 size, Quaternion rotation)
        {
            m_log.InfoFormat("NullPhysicsScene : AddPrim({0},{1})", position, size);
            return PhysicsActor.Null;
        }
*/

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, Vector3 position,
                                                  Vector3 size, Quaternion rotation, bool isPhysical, uint localid)
        {
            m_log.InfoFormat("[PHYSICS]: NullPhysicsScene : AddPrim({0},{1})", position, size);
            return PhysicsActor.Null;
        }

        public override void AddPhysicsActorTaint(PhysicsActor prim)
        {
        }

        public override float Simulate(float timeStep)
        {
            m_workIndicator = (m_workIndicator + 1) % 10;

            return 0f;
        }

        public override void GetResults()
        {
            m_log.Info("[PHYSICS]: NullPhysicsScene : GetResults()");
        }

        public override void SetTerrain(float[] heightMap)
        {
            m_log.InfoFormat("[PHYSICS]: NullPhysicsScene : SetTerrain({0} items)", heightMap.Length);
        }

        public override void DeleteTerrain()
        {
        }

        public override bool IsThreaded
        {
            get { return false; }
        }

        public override void Dispose()
        {
        }

        public override Dictionary<uint,float> GetTopColliders()
        {
            Dictionary<uint, float> returncolliders = new Dictionary<uint, float>();
            return returncolliders;
        }
    }
}