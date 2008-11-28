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

using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Scenes.Tests
{  
    /// <summary>
    /// Scene presence tests
    /// </summary>
    [TestFixture]    
    public class ScenePresenceTests
    {      
        /// <summary>
        /// Test adding a root agent to a scene.  Doesn't yet actually complete crossing the agent into the scene.
        /// </summary>
        [Test]
        public void TestAddRootAgent()
        {
            Scene scene = SceneTestUtils.SetupScene();
            UUID agentId = UUID.Parse("00000000-0000-0000-0000-000000000001");
            string firstName = "testfirstname";
            
            AgentCircuitData agent = new AgentCircuitData();
            agent.AgentID = agentId;
            agent.firstname = firstName;
            agent.lastname = "testlastname";
            agent.SessionID = UUID.Zero;
            agent.SecureSessionID = UUID.Zero;
            agent.circuitcode = 123;
            agent.BaseFolder = UUID.Zero;
            agent.InventoryFolder = UUID.Zero;
            agent.startpos = Vector3.Zero;
            agent.CapsPath = "http://wibble.com";
            
            scene.NewUserConnection(agent);
            scene.AddNewClient(new TestClient(agent), false);
            
            ScenePresence presence = scene.GetScenePresence(agentId);
            
            Assert.That(presence, Is.Not.Null, "presence is null");
            Assert.That(presence.Firstname, Is.EqualTo(firstName), "First name not same"); 
        }
        
        /// <summary>
        /// Test removing an uncrossed root agent from a scene.
        /// </summary> 
        [Test]       
        public void TestRemoveRootAgent()
        {
            Scene scene = SceneTestUtils.SetupScene();
            UUID agentId = UUID.Parse("00000000-0000-0000-0000-000000000001");
            
            SceneTestUtils.AddRootAgent(scene, agentId);
            
            scene.RemoveClient(agentId);
            
            ScenePresence presence = scene.GetScenePresence(agentId);
            
            Assert.That(presence, Is.Null, "presence is not null");            
        }
    }
}
