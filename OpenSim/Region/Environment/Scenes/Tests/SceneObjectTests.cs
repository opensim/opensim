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

using System;
using Nini.Config;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Scenes.Tests
{
    /// <summary>
    /// Scene object tests
    /// </summary>
    [TestFixture]
    public class SceneObjectTests
    {
        [SetUp]
        public void Init()
        {
            try
            {
                log4net.Config.XmlConfigurator.Configure();
            }
            catch
            {
                // I don't care, just leave log4net off
            }            
        }
        
        /// <summary>
        /// Set up a test scene
        /// </summary>
        private TestScene SetupScene()
        {
            RegionInfo regInfo = new RegionInfo(1000, 1000, null, null);
            regInfo.RegionName = "Unit test region";
            AgentCircuitManager acm = new AgentCircuitManager();
            //CommunicationsManager cm = new CommunicationsManager(null, null, null, false, null);
            CommunicationsManager cm = null;
            //SceneCommunicationService scs = new SceneCommunicationService(cm);
            SceneCommunicationService scs = null;
            StorageManager sm = new OpenSim.Region.Environment.StorageManager("OpenSim.Data.Null.dll", "", "");
            IConfigSource configSource = new IniConfigSource();
            
            return new TestScene(regInfo, acm, cm, scs, null, sm, null, null, false, false, false, configSource, null);            
        }
        
        /// <summary>
        /// Add a test object
        /// </summary>
        /// <param name="scene"></param>
        /// <returns></returns>
        private SceneObjectPart AddSceneObject(Scene scene)
        {
            SceneObjectGroup sceneObject = new SceneObjectGroup();
            SceneObjectPart part 
                = new SceneObjectPart(UUID.Zero, PrimitiveBaseShape.Default, Vector3.Zero, Quaternion.Identity, Vector3.Zero);
            //part.UpdatePrimFlags(false, false, true);           
            part.ObjectFlags |= (uint)PrimFlags.Phantom;            
            sceneObject.SetRootPart(part);
            
            scene.AddNewSceneObject(sceneObject, false);
            
            return part;
        }
                
        /// <summary>
        /// Test adding an object to a scene.
        /// </summary>
        [Test]        
        public void TestAddSceneObject()
        {              
            Scene scene = SetupScene();
            SceneObjectPart part = AddSceneObject(scene);
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(part.LocalId);
            
            //System.Console.WriteLine("retrievedPart : {0}", retrievedPart);
            // If the parts have the same UUID then we will consider them as one and the same
            Assert.That(retrievedPart.UUID, Is.EqualTo(part.UUID));         
        }
        
        /// <summary>
        /// Test removing an object from a scene.
        /// </summary>
        public void TestRemoveSceneObject()
        {
            TestScene scene = SetupScene();;            
            SceneObjectPart part = AddSceneObject(scene);
            scene.DeleteSceneObject(part.ParentGroup, false);
            
            SceneObjectPart retrievedPart = scene.GetSceneObjectPart(part.LocalId);
            
            Assert.That(retrievedPart, Is.Null);
        }
    }
}