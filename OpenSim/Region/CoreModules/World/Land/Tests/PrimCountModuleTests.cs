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
using System.Reflection;
using log4net.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Region.CoreModules.World.Land.Tests
{
    [TestFixture]
    public class PrimCountModuleTests
    {
        protected UUID m_userId = new UUID("00000000-0000-0000-0000-100000000000");
        protected UUID m_dummyUserId = new UUID("99999999-9999-9999-9999-999999999999");        
        protected TestScene m_scene;
        protected PrimCountModule m_pcm;
        protected ILandObject m_lo;
            
        [SetUp]
        public void SetUp()
        {
            m_pcm = new PrimCountModule();
            LandManagementModule lmm = new LandManagementModule();
            m_scene = SceneSetupHelpers.SetupScene();            
            SceneSetupHelpers.SetupSceneModules(m_scene, lmm, m_pcm);             
                        
            ILandObject lo = new LandObject(m_userId, false, m_scene);
            lo.SetLandBitmap(lo.GetSquareLandBitmap(0, 0, (int)Constants.RegionSize, (int)Constants.RegionSize));
            m_lo = lmm.AddLandObject(lo);
            //scene.loadAllLandObjectsFromStorage(scene.RegionInfo.originRegionID);            
        } 
        
        /// <summary>
        /// Test count after a parcel owner owned object is added.
        /// </summary>
        [Test]
        public void TestAddOwnerObject()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();                                  
                  
            IPrimCounts pc = m_lo.PrimCounts;
            
            Assert.That(pc.Owner, Is.EqualTo(0));
            Assert.That(pc.Group, Is.EqualTo(0));
            Assert.That(pc.Others, Is.EqualTo(0));
            Assert.That(pc.Total, Is.EqualTo(0));
            Assert.That(pc.Users[m_userId], Is.EqualTo(0));
            Assert.That(pc.Users[m_dummyUserId], Is.EqualTo(0));
            Assert.That(pc.Simulator, Is.EqualTo(0));            
            
            SceneObjectGroup sog = SceneSetupHelpers.CreateSceneObject(3, m_userId, 0x01);             
            m_scene.AddNewSceneObject(sog, false);
            
            Assert.That(pc.Owner, Is.EqualTo(3));
            Assert.That(pc.Group, Is.EqualTo(0));
            Assert.That(pc.Others, Is.EqualTo(0));
            Assert.That(pc.Total, Is.EqualTo(3));
            Assert.That(pc.Users[m_userId], Is.EqualTo(3));
            Assert.That(pc.Users[m_dummyUserId], Is.EqualTo(0));
            Assert.That(pc.Simulator, Is.EqualTo(3));            
            
            // Add a second object and retest
            SceneObjectGroup sog2 = SceneSetupHelpers.CreateSceneObject(2, m_userId, 0x10);             
            m_scene.AddNewSceneObject(sog2, false);   
            
            Assert.That(pc.Owner, Is.EqualTo(5));
            Assert.That(pc.Group, Is.EqualTo(0));
            Assert.That(pc.Others, Is.EqualTo(0));
            Assert.That(pc.Total, Is.EqualTo(5));
            Assert.That(pc.Users[m_userId], Is.EqualTo(5));
            Assert.That(pc.Users[m_dummyUserId], Is.EqualTo(0));
            Assert.That(pc.Simulator, Is.EqualTo(5));              
        }
        
        /// <summary>
        /// Test count after a parcel owner owned copied object is added.
        /// </summary>
        [Test]
        public void TestCopiedOwnerObject()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();                                  
                  
            IPrimCounts pc = m_lo.PrimCounts;
            
            SceneObjectGroup sog = SceneSetupHelpers.CreateSceneObject(3, m_userId, 0x01);             
            m_scene.AddNewSceneObject(sog, false);
            m_scene.SceneGraph.DuplicateObject(sog.LocalId, Vector3.Zero, 0, m_userId, UUID.Zero, Quaternion.Identity); 
            
            Assert.That(pc.Owner, Is.EqualTo(6));
            Assert.That(pc.Group, Is.EqualTo(0));
            Assert.That(pc.Others, Is.EqualTo(0));
            Assert.That(pc.Total, Is.EqualTo(6));
            Assert.That(pc.Users[m_userId], Is.EqualTo(6));
            Assert.That(pc.Users[m_dummyUserId], Is.EqualTo(0));
            Assert.That(pc.Simulator, Is.EqualTo(6));              
        }        
        
        /// <summary>
        /// Test count after a parcel owner owned object is removed.
        /// </summary>
        [Test]
        public void TestRemoveOwnerObject()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();
            
            IPrimCounts pc = m_lo.PrimCounts;
            
            m_scene.AddNewSceneObject(SceneSetupHelpers.CreateSceneObject(1, m_userId, 0x1), false);
            SceneObjectGroup sogToDelete = SceneSetupHelpers.CreateSceneObject(3, m_userId, 0x10);
            m_scene.AddNewSceneObject(sogToDelete, false);            
            m_scene.DeleteSceneObject(sogToDelete, false);
            
            Assert.That(pc.Owner, Is.EqualTo(1));
            Assert.That(pc.Group, Is.EqualTo(0));
            Assert.That(pc.Others, Is.EqualTo(0));
            Assert.That(pc.Total, Is.EqualTo(1));
            Assert.That(pc.Users[m_userId], Is.EqualTo(1));
            Assert.That(pc.Users[m_dummyUserId], Is.EqualTo(0));
            Assert.That(pc.Simulator, Is.EqualTo(1));            
        }     
        
        /// <summary>
        /// Test the count is correct after is has been tainted.
        /// </summary>
        [Test]
        public void TestTaint()
        {
            TestHelper.InMethod();
            IPrimCounts pc = m_lo.PrimCounts;
            
            SceneObjectGroup sog = SceneSetupHelpers.CreateSceneObject(3, m_userId, 0x01);             
            m_scene.AddNewSceneObject(sog, false); 
            
            m_pcm.TaintPrimCount();
            
            Assert.That(pc.Owner, Is.EqualTo(3));
            Assert.That(pc.Group, Is.EqualTo(0));
            Assert.That(pc.Others, Is.EqualTo(0));
            Assert.That(pc.Total, Is.EqualTo(3));
            Assert.That(pc.Users[m_userId], Is.EqualTo(3));
            Assert.That(pc.Users[m_dummyUserId], Is.EqualTo(0));
            Assert.That(pc.Simulator, Is.EqualTo(3));              
        }
    }
}