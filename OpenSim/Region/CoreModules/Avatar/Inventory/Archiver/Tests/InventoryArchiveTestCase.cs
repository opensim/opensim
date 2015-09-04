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
using System.IO;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Serialization;
using OpenSim.Framework.Serialization.External;
using OpenSim.Region.CoreModules.Avatar.Inventory.Archiver;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver.Tests
{
    [TestFixture]
    public class InventoryArchiveTestCase : OpenSimTestCase
    {
        protected ManualResetEvent mre = new ManualResetEvent(false);
        
        /// <summary>
        /// A raw array of bytes that we'll use to create an IAR memory stream suitable for isolated use in each test.
        /// </summary>
        protected byte[] m_iarStreamBytes;
                
        /// <summary>
        /// Stream of data representing a common IAR for load tests.
        /// </summary>
        protected MemoryStream m_iarStream;
        
        protected UserAccount m_uaMT 
            = new UserAccount { 
                PrincipalID = UUID.Parse("00000000-0000-0000-0000-000000000555"),
                FirstName = "Mr",
                LastName = "Tiddles" };
        
        protected UserAccount m_uaLL1
            = new UserAccount { 
                PrincipalID = UUID.Parse("00000000-0000-0000-0000-000000000666"),
                FirstName = "Lord",
                LastName = "Lucan" }; 
        
        protected UserAccount m_uaLL2
            = new UserAccount { 
                PrincipalID = UUID.Parse("00000000-0000-0000-0000-000000000777"),
                FirstName = "Lord",
                LastName = "Lucan" };       
        
        protected string m_item1Name = "Ray Gun Item";
        protected string m_coaItemName = "Coalesced Item";

        [TestFixtureSetUp]
        public void FixtureSetup()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            Util.FireAndForgetMethod = FireAndForgetMethod.RegressionTest;

            ConstructDefaultIarBytesForTestLoad();
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten so none of them require async stuff (which regression
            // tests really shouldn't).
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
            m_iarStream = new MemoryStream(m_iarStreamBytes);
        }
        
        protected void ConstructDefaultIarBytesForTestLoad()
        {
//            log4net.Config.XmlConfigurator.Configure();
            
            InventoryArchiverModule archiverModule = new InventoryArchiverModule();
            Scene scene = new SceneHelpers().SetupScene();
            SceneHelpers.SetupSceneModules(scene, archiverModule);            
            
            UserAccountHelpers.CreateUserWithInventory(scene, m_uaLL1, "hampshire");

            MemoryStream archiveWriteStream = new MemoryStream();
            
            // Create scene object asset
            UUID ownerId = UUID.Parse("00000000-0000-0000-0000-000000000040");
            SceneObjectGroup object1 = SceneHelpers.CreateSceneObject(1, ownerId, "Ray Gun Object", 0x50);         

            UUID asset1Id = UUID.Parse("00000000-0000-0000-0000-000000000060");
            AssetBase asset1 = AssetHelpers.CreateAsset(asset1Id, object1);
            scene.AssetService.Store(asset1);            

            // Create scene object item
            InventoryItemBase item1 = new InventoryItemBase();
            item1.Name = m_item1Name;
            item1.ID = UUID.Parse("00000000-0000-0000-0000-000000000020");            
            item1.AssetID = asset1.FullID;
            item1.GroupID = UUID.Random();
            item1.CreatorId = m_uaLL1.PrincipalID.ToString();
            item1.Owner = m_uaLL1.PrincipalID;
            item1.Folder = scene.InventoryService.GetRootFolder(m_uaLL1.PrincipalID).ID;            
            scene.AddInventoryItem(item1);
            
            // Create coalesced objects asset
            SceneObjectGroup cobj1 = SceneHelpers.CreateSceneObject(1, m_uaLL1.PrincipalID, "Object1", 0x120);
            cobj1.AbsolutePosition = new Vector3(15, 30, 45);
            
            SceneObjectGroup cobj2 = SceneHelpers.CreateSceneObject(1, m_uaLL1.PrincipalID, "Object2", 0x140);
            cobj2.AbsolutePosition = new Vector3(25, 50, 75);               
            
            CoalescedSceneObjects coa = new CoalescedSceneObjects(m_uaLL1.PrincipalID, cobj1, cobj2);
            
            AssetBase coaAsset = AssetHelpers.CreateAsset(0x160, coa);
            scene.AssetService.Store(coaAsset);            
            
            // Create coalesced objects inventory item
            InventoryItemBase coaItem = new InventoryItemBase();
            coaItem.Name = m_coaItemName;
            coaItem.ID = UUID.Parse("00000000-0000-0000-0000-000000000180");            
            coaItem.AssetID = coaAsset.FullID;
            coaItem.GroupID = UUID.Random();
            coaItem.CreatorId = m_uaLL1.PrincipalID.ToString();
            coaItem.Owner = m_uaLL1.PrincipalID;
            coaItem.Folder = scene.InventoryService.GetRootFolder(m_uaLL1.PrincipalID).ID;            
            scene.AddInventoryItem(coaItem);            
            
            archiverModule.ArchiveInventory(
                UUID.Random(), m_uaLL1.FirstName, m_uaLL1.LastName, "/*", "hampshire", archiveWriteStream);            
            
            m_iarStreamBytes = archiveWriteStream.ToArray();
        }
        
        protected void SaveCompleted(
            UUID id, bool succeeded, UserAccount userInfo, string invPath, Stream saveStream, 
            Exception reportedException, int SaveCount, int FilterCount)
        {
            mre.Set();
        }        
    }
}