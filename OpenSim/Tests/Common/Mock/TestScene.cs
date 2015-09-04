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
using Nini.Config;
using OpenSim.Framework;

using OpenSim.Framework.Servers;
using OpenSim.Region.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Services.Interfaces;

namespace OpenSim.Tests.Common
{
    public class TestScene : Scene
    {
        public TestScene(
            RegionInfo regInfo, AgentCircuitManager authen, 
            ISimulationDataService simDataService, IEstateDataService estateDataService,
            IConfigSource config, string simulatorVersion)
            : base(regInfo, authen, simDataService, estateDataService,
                   config, simulatorVersion)
        {
        }

        ~TestScene()
        {
            //Console.WriteLine("TestScene destructor called for {0}", RegionInfo.RegionName);
            Console.WriteLine("TestScene destructor called");
        }
        
        /// <summary>
        /// Temporarily override session authentication for tests (namely teleport).
        /// </summary>
        /// <remarks>
        /// TODO: This needs to be mocked out properly.
        /// </remarks>
        /// <param name="agent"></param>
        /// <returns></returns>
        public override bool VerifyUserPresence(AgentCircuitData agent, out string reason)
        {
            reason = String.Empty;
            return true;
        }
            
        public AsyncSceneObjectGroupDeleter SceneObjectGroupDeleter
        {
            get { return m_asyncSceneObjectDeleter; }
        }
    }
}
