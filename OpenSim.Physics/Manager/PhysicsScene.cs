/*
* Copyright (c) OpenSim project, http://sim.opensecondlife.org/
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Physics.Manager
{
    public abstract class PhysicsScene
    {
        public static PhysicsScene Null
        {
            get
            {
                return new NullPhysicsScene();
            }
        }

        public abstract PhysicsActor AddAvatar(PhysicsVector position);

        public abstract PhysicsActor AddPrim(PhysicsVector position, PhysicsVector size);

        public abstract void Simulate(float timeStep);

        public abstract void GetResults();

        public abstract void SetTerrain(float[] heightMap);
        
        public abstract void DeleteTerrain();

        public abstract bool IsThreaded
        {
            get;
        }
    }

    public class NullPhysicsScene : PhysicsScene
    {
        private static int m_workIndicator;

        public override PhysicsActor AddAvatar(PhysicsVector position)
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("NullPhysicsScene : AddAvatar({0})", position);
            return PhysicsActor.Null;
        }

        public override PhysicsActor AddPrim(PhysicsVector position, PhysicsVector size)
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("NullPhysicsScene : AddPrim({0},{1})", position, size);
            return PhysicsActor.Null;
        }

        public override void Simulate(float timeStep)
        {
            m_workIndicator = (m_workIndicator + 1) % 10;

            //OpenSim.Framework.Console.MainConsole.Instance.SetStatus(m_workIndicator.ToString());
        }

        public override void GetResults()
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("NullPhysicsScene : GetResults()");
        }

        public override void SetTerrain(float[] heightMap)
        {
            OpenSim.Framework.Console.MainConsole.Instance.WriteLine("NullPhysicsScene : SetTerrain({0} items)", heightMap.Length);
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
