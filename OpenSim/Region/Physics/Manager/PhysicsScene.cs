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
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using Axiom.Math;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Physics.Manager;

namespace OpenSim.Region.Physics.Manager
{
    public delegate void physicsCrash();

    public abstract class PhysicsScene
    {
        // The only thing that should register for this event is the InnerScene
        // Anything else could cause problems.

        public event physicsCrash OnPhysicsCrash;

        public static PhysicsScene Null
        {
            get { return new NullPhysicsScene(); }
        }
        public virtual void TriggerPhysicsBasedRestart()
        {
            physicsCrash handler = OnPhysicsCrash;
            if (handler != null)
            {
                OnPhysicsCrash();
            }
            
        }
        

        public abstract void Initialise(IMesher meshmerizer);

        public abstract PhysicsActor AddAvatar(string avName, PhysicsVector position);

        public abstract void RemoveAvatar(PhysicsActor actor);

        public abstract void RemovePrim(PhysicsActor prim);

        public abstract PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, PhysicsVector position,
                                                  PhysicsVector size, Quaternion rotation); //To be removed
        public abstract PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, PhysicsVector position,
                                                  PhysicsVector size, Quaternion rotation, bool isPhysical);
        public abstract void AddPhysicsActorTaint(PhysicsActor prim);

        public abstract void Simulate(float timeStep);

        public abstract void GetResults();

        public abstract void SetTerrain(float[] heightMap);

        public abstract void DeleteTerrain();

        public abstract bool IsThreaded { get; }

        private class NullPhysicsScene : PhysicsScene
        {
            private static int m_workIndicator;


            public override void Initialise(IMesher meshmerizer)
            {
                // Does nothing right now
            }

            public override PhysicsActor AddAvatar(string avName, PhysicsVector position)
            {
                MainLog.Instance.Verbose("PHYSICS", "NullPhysicsScene : AddAvatar({0})", position);
                return PhysicsActor.Null;
            }

            public override void RemoveAvatar(PhysicsActor actor)
            {
            }

            public override void RemovePrim(PhysicsActor prim)
            {
            }

/*
            public override PhysicsActor AddPrim(PhysicsVector position, PhysicsVector size, Quaternion rotation)
            {
                MainLog.Instance.Verbose("NullPhysicsScene : AddPrim({0},{1})", position, size);
                return PhysicsActor.Null;
            }
*/
            public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, PhysicsVector position,
                                                      PhysicsVector size, Quaternion rotation) //To be removed
            {
                return this.AddPrimShape(primName, pbs, position, size, rotation, false);
            }
            public override PhysicsActor AddPrimShape(string primName, PrimitiveBaseShape pbs, PhysicsVector position,
                                                      PhysicsVector size, Quaternion rotation, bool isPhysical)
            {
                MainLog.Instance.Verbose("PHYSICS", "NullPhysicsScene : AddPrim({0},{1})", position, size);
                return PhysicsActor.Null;
            }
            public override void AddPhysicsActorTaint(PhysicsActor prim)
            {

            }
            public override void Simulate(float timeStep)
            {
                m_workIndicator = (m_workIndicator + 1)%10;

                //OpenSim.Framework.Console.MainLog.Instance.SetStatus(m_workIndicator.ToString());
            }

            public override void GetResults()
            {
                MainLog.Instance.Verbose("PHYSICS", "NullPhysicsScene : GetResults()");
            }

            public override void SetTerrain(float[] heightMap)
            {
                MainLog.Instance.Verbose("PHYSICS", "NullPhysicsScene : SetTerrain({0} items)", heightMap.Length);
            }

            public override void DeleteTerrain()
            {
            }

            public override bool IsThreaded
            {
                get { return false; }
            }
        }
    }
}
