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
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.BasicPhysicsPlugin
{
    /// <summary>
    /// This is an incomplete extremely basic physics implementation
    /// </summary>
    /// <remarks>
    /// Not useful for anything at the moment apart from some regression testing in other components where some form
    /// of physics plugin is needed.
    /// </remarks>
    public class BasicScene : PhysicsScene
    {
        private List<BasicActor> _actors = new List<BasicActor>();
        private List<BasicPhysicsPrim> _prims = new List<BasicPhysicsPrim>();
        private float[] _heightMap;
        private Vector3 m_regionExtent;

        //protected internal string sceneIdentifier;

        public BasicScene(string engineType, string _sceneIdentifier)
        {
            EngineType = engineType;
            Name = EngineType + "/" + _sceneIdentifier;
            //sceneIdentifier = _sceneIdentifier;
        }

        public override void Initialise(IMesher meshmerizer, IConfigSource config)
        {
            throw new Exception("Should not be called.");
        }

        public override void Initialise(IMesher meshmerizer, IConfigSource config, Vector3 regionExtent)
        {
            m_regionExtent = regionExtent;
        }

        public override void Dispose() {}

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, Vector3 position,
                                                  Vector3 size, Quaternion rotation, bool isPhysical, uint localid)
        {
            BasicPhysicsPrim prim = new BasicPhysicsPrim(primName, localid, position, size, rotation, pbs);
            prim.IsPhysical = isPhysical;

            _prims.Add(prim);

            return prim;
        }

        public override PhysicsActor AddAvatar(string avName, Vector3 position, Vector3 velocity, Vector3 size, bool isFlying)
        {
            BasicActor act = new BasicActor(size);
            act.Position = position;
            act.Velocity = velocity;
            act.Flying = isFlying;
            _actors.Add(act);
            return act;
        }

        public override void RemovePrim(PhysicsActor actor)
        {
            BasicPhysicsPrim prim = (BasicPhysicsPrim)actor;
            if (_prims.Contains(prim))
                _prims.Remove(prim);
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
            BasicActor act = (BasicActor)actor;
            if (_actors.Contains(act))
                _actors.Remove(act);
        }

        public override void AddPhysicsActorTaint(PhysicsActor prim)
        {
        }

        public override float Simulate(float timeStep)
        {
//            Console.WriteLine("Simulating");

            float fps = 0;
            for (int i = 0; i < _actors.Count; ++i)
            {
                BasicActor actor = _actors[i];
                Vector3 actorPosition = actor.Position;
                Vector3 actorVelocity = actor.Velocity;

//                Console.WriteLine(
//                    "Processing actor {0}, starting pos {1}, starting vel {2}", i, actorPosition, actorVelocity);

                actorPosition.X += actor.Velocity.X * timeStep;
                actorPosition.Y += actor.Velocity.Y * timeStep;

                if (actor.Position.Y < 0)
                {
                    actorPosition.Y = 0.1F;
                }
                else if (actor.Position.Y >= m_regionExtent.Y)
                {
                    actorPosition.Y = (m_regionExtent.Y - 0.1f);
                }

                if (actor.Position.X < 0)
                {
                    actorPosition.X = 0.1F;
                }
                else if (actor.Position.X >= m_regionExtent.X)
                {
                    actorPosition.X = (m_regionExtent.X - 0.1f);
                }

                float terrainHeight = 0;
                if (_heightMap != null)
                    terrainHeight = _heightMap[(int)actor.Position.Y * (int)m_regionExtent.Y + (int)actor.Position.X];

                float height = terrainHeight + actor.Size.Z;
//                Console.WriteLine("height {0}, actorPosition {1}", height, actorPosition);

                if (actor.Flying)
                {
                    if (actor.Position.Z + (actor.Velocity.Z * timeStep) < terrainHeight + 2)
                    {
                        actorPosition.Z = height;
                        actorVelocity.Z = 0;
                        actor.IsColliding = true;
                    }
                    else
                    {
                        actorPosition.Z += actor.Velocity.Z * timeStep;
                        actor.IsColliding = false;
                    }
                }
                else
                {
                    actorPosition.Z = height;
                    actorVelocity.Z = 0;
                    actor.IsColliding = true;
                }

                actor.Position = actorPosition;
                actor.Velocity = actorVelocity;
            }

            return fps;
        }

        public override void GetResults()
        {
        }

        public override bool IsThreaded
        {
            get { return (false); // for now we won't be multithreaded
            }
        }

        public override void SetTerrain(float[] heightMap)
        {
            _heightMap = heightMap;
        }

        public override void DeleteTerrain()
        {
        }

        public override void SetWaterLevel(float baseheight)
        {
        }

        public override Dictionary<uint, float> GetTopColliders()
        {
            Dictionary<uint, float> returncolliders = new Dictionary<uint, float>();
            return returncolliders;
        }
    }
}
