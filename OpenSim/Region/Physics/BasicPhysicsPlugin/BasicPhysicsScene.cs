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
    public class BasicScene : PhysicsScene
    {
        private List<BasicActor> _actors = new List<BasicActor>();
        private float[] _heightMap;

        //protected internal string sceneIdentifier;

        public BasicScene(string _sceneIdentifier)
        {
            //sceneIdentifier = _sceneIdentifier;
        }

        public override void Initialise(IMesher meshmerizer, IConfigSource config)
        {
        }

        public override void Dispose()
        {

        }
        public override PhysicsActor AddAvatar(string avName, Vector3 position, Vector3 size, bool isFlying)
        {
            BasicActor act = new BasicActor();
            act.Position = position;
            act.Flying = isFlying;
            _actors.Add(act);
            return act;
        }

        public override void RemovePrim(PhysicsActor prim)
        {
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
            BasicActor act = (BasicActor) actor;
            if (_actors.Contains(act))
            {
                _actors.Remove(act);
            }
        }

/*
        public override PhysicsActor AddPrim(Vector3 position, Vector3 size, Quaternion rotation)
        {
            return null;
        }
*/

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, Vector3 position,
                                                  Vector3 size, Quaternion rotation)
        {
            return AddPrimShape(primName, pbs, position, size, rotation, false);
        }

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, Vector3 position,
                                                  Vector3 size, Quaternion rotation, bool isPhysical)
        {
            return null;
        }

        public override void AddPhysicsActorTaint(PhysicsActor prim)
        {
        }

        public override float Simulate(float timeStep)
        {
            float fps = 0;
            for (int i = 0; i < _actors.Count; ++i)
            {
                BasicActor actor = _actors[i];
                Vector3 actorPosition = actor.Position;
                Vector3 actorVelocity = actor.Velocity;

                actorPosition.X += actor.Velocity.X*timeStep;
                actorPosition.Y += actor.Velocity.Y*timeStep;

                if (actor.Position.Y < 0)
                {
                    actorPosition.Y = 0.1F;
                }
                else if (actor.Position.Y >= Constants.RegionSize)
                {
                    actorPosition.Y = ((int)Constants.RegionSize - 0.1f);
                }

                if (actor.Position.X < 0)
                {
                    actorPosition.X = 0.1F;
                }
                else if (actor.Position.X >= Constants.RegionSize)
                {
                    actorPosition.X = ((int)Constants.RegionSize - 0.1f);
                }

                float height = _heightMap[(int)actor.Position.Y * Constants.RegionSize + (int)actor.Position.X] + actor.Size.Z;
                if (actor.Flying)
                {
                    if (actor.Position.Z + (actor.Velocity.Z*timeStep) <
                        _heightMap[(int)actor.Position.Y * Constants.RegionSize + (int)actor.Position.X] + 2)
                    {
                        actorPosition.Z = height;
                        actorVelocity.Z = 0;
                        actor.IsColliding = true;
                    }
                    else
                    {
                        actorPosition.Z += actor.Velocity.Z*timeStep;
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
