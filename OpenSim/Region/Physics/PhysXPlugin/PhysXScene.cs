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
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using PhysXWrapper;
using Quaternion=OpenMetaverse.Quaternion;
using System.Reflection;
using log4net;
using OpenMetaverse;

namespace OpenSim.Region.Physics.PhysXPlugin
{
    public class PhysXScene : PhysicsScene
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private List<PhysXCharacter> _characters = new List<PhysXCharacter>();
        private List<PhysXPrim> _prims = new List<PhysXPrim>();
        private float[] _heightMap = null;
        private NxPhysicsSDK mySdk;
        private NxScene scene;

        // protected internal string sceneIdentifier;
        public PhysXScene(string _sceneIdentifier)
        {
            //sceneIdentifier = _sceneIdentifier;

            mySdk = NxPhysicsSDK.CreateSDK();
            m_log.Info("Sdk created - now creating scene");
            scene = mySdk.CreateScene();
        }

        public override void Initialise(IMesher meshmerizer, IConfigSource config)
        {
            // Does nothing right now
        }
        public override void Dispose()
        {

        }

        public override void SetWaterLevel(float baseheight)
        {

        }

        public override PhysicsActor AddAvatar(string avName, Vector3 position, Vector3 size, bool isFlying)
        {
            Vec3 pos = new Vec3();
            pos.X = position.X;
            pos.Y = position.Y;
            pos.Z = position.Z;
            PhysXCharacter act = new PhysXCharacter(scene.AddCharacter(pos));
            act.Flying = isFlying;
            act.Position = position;
            _characters.Add(act);
            return act;
        }

        public override void RemovePrim(PhysicsActor prim)
        {
        }

        public override void RemoveAvatar(PhysicsActor actor)
        {
        }

        private PhysicsActor AddPrim(Vector3 position, Vector3 size, Quaternion rotation)
        {
            Vec3 pos = new Vec3();
            pos.X = position.X;
            pos.Y = position.Y;
            pos.Z = position.Z;
            Vec3 siz = new Vec3();
            siz.X = size.X;
            siz.Y = size.Y;
            siz.Z = size.Z;
            PhysXPrim act = new PhysXPrim(scene.AddNewBox(pos, siz));
            _prims.Add(act);
            return act;
        }

        public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, Vector3 position,
                                                  Vector3 size, Quaternion rotation, bool isPhysical, uint localid)
        {
            return AddPrim(position, size, rotation);
        }

        public override void AddPhysicsActorTaint(PhysicsActor prim)
        {
        }

        public override float Simulate(float timeStep)
        {
            float fps = 0f;
            try
            {
                foreach (PhysXCharacter actor in _characters)
                {
                    actor.Move(timeStep);
                }
                scene.Simulate(timeStep);
                scene.FetchResults();
                scene.UpdateControllers();

                foreach (PhysXCharacter actor in _characters)
                {
                    actor.UpdatePosition();
                }
            }
            catch (Exception e)
            {
                m_log.Error(e.Message);
            }
            return fps;
        }

        public override void GetResults()
        {
        }

        public override bool IsThreaded
        {
            // for now we won't be multithreaded
            get { return (false); }
        }

        public override void SetTerrain(float[] heightMap)
        {
            if (_heightMap != null)
            {
                m_log.Debug("PhysX - deleting old terrain");
                scene.DeleteTerrain();
            }
            _heightMap = heightMap;
            scene.AddTerrain(heightMap);
        }

        public override void DeleteTerrain()
        {
            scene.DeleteTerrain();
        }

        public override Dictionary<uint, float> GetTopColliders()
        {
            Dictionary<uint, float> returncolliders = new Dictionary<uint, float>();
            return returncolliders;
        }
    }
}
