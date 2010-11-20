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
using log4net.Config;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenSim.Framework;
using OpenSim.Framework.Serialization;
using OpenSim.Framework.Serialization.External;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;
using OpenSim.Tests.Common.Setup;
using ArchiveConstants = OpenSim.Framework.Serialization.ArchiveConstants;
using TarArchiveReader = OpenSim.Framework.Serialization.TarArchiveReader;
using TarArchiveWriter = OpenSim.Framework.Serialization.TarArchiveWriter;
using RegionSettings = OpenSim.Framework.RegionSettings;

namespace OpenSim.Region.CoreModules.World.Archiver.Tests
{
    [TestFixture]
    public class ArchiverTests
    {
        private Guid m_lastRequestId;
        private string m_lastErrorMessage;

        protected TestScene m_scene;
        protected ArchiverModule m_archiverModule;

        protected TaskInventoryItem m_soundItem;
        
        [SetUp]
        public void SetUp()
        {
            m_archiverModule = new ArchiverModule();
            SerialiserModule serialiserModule = new SerialiserModule();
            TerrainModule terrainModule = new TerrainModule();

            m_scene = SceneSetupHelpers.SetupScene();
            SceneSetupHelpers.SetupSceneModules(m_scene, m_archiverModule, serialiserModule, terrainModule);
        }
        
        private void LoadCompleted(Guid requestId, string errorMessage)
        {
            lock (this)
            {
                m_lastRequestId = requestId;
                m_lastErrorMessage = errorMessage;
                Console.WriteLine("About to pulse ArchiverTests on LoadCompleted");
                                
                Monitor.PulseAll(this);
            }
        }
        
        private void SaveCompleted(Guid requestId, string errorMessage)
        {
            lock (this)
            {
                m_lastRequestId = requestId;
                m_lastErrorMessage = errorMessage;
                Console.WriteLine("About to pulse ArchiverTests on SaveCompleted");
                Monitor.PulseAll(this);
            }
        }

        protected SceneObjectPart CreateSceneObjectPart1()
        {
            string partName = "My Little Pony";
            UUID ownerId = UUID.Parse("00000000-0000-0000-0000-000000000015");
            PrimitiveBaseShape shape = PrimitiveBaseShape.CreateSphere();
            Vector3 groupPosition = new Vector3(10, 20, 30);
            Quaternion rotationOffset = new Quaternion(20, 30, 40, 50);
            Vector3 offsetPosition = new Vector3(5, 10, 15);

            return new SceneObjectPart(ownerId, shape, groupPosition, rotationOffset, offsetPosition) { Name = partName };
        }

        protected SceneObjectPart CreateSceneObjectPart2()
        {
            string partName = "Action Man";
            UUID ownerId = UUID.Parse("00000000-0000-0000-0000-000000000016");
            PrimitiveBaseShape shape = PrimitiveBaseShape.CreateCylinder();
            Vector3 groupPosition = new Vector3(90, 80, 70);
            Quaternion rotationOffset = new Quaternion(60, 70, 80, 90);
            Vector3 offsetPosition = new Vector3(20, 25, 30);

            return new SceneObjectPart(ownerId, shape, groupPosition, rotationOffset, offsetPosition) { Name = partName };
        }

        /// <summary>
        /// Test saving a V0.2 OpenSim Region Archive.
        /// </summary>
        [Test]
        public void TestSaveOarV0_2()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            SceneObjectPart part1 = CreateSceneObjectPart1();
            SceneObjectGroup sog1 = new SceneObjectGroup(part1);
            m_scene.AddNewSceneObject(sog1, false);

            SceneObjectPart part2 = CreateSceneObjectPart2();
            
            AssetNotecard nc = new AssetNotecard();
            nc.BodyText = "Hello World!";
            nc.Encode();
            UUID ncAssetUuid = new UUID("00000000-0000-0000-1000-000000000000");
            UUID ncItemUuid = new UUID("00000000-0000-0000-1100-000000000000");
            AssetBase ncAsset 
                = AssetHelpers.CreateAsset(ncAssetUuid, AssetType.Notecard, nc.AssetData, UUID.Zero);
            m_scene.AssetService.Store(ncAsset);
            SceneObjectGroup sog2 = new SceneObjectGroup(part2);
            TaskInventoryItem ncItem 
                = new TaskInventoryItem { Name = "ncItem", AssetID = ncAssetUuid, ItemID = ncItemUuid };
            part2.Inventory.AddInventoryItem(ncItem, true);
            
            m_scene.AddNewSceneObject(sog2, false);

            MemoryStream archiveWriteStream = new MemoryStream();
            m_scene.EventManager.OnOarFileSaved += SaveCompleted;

            Guid requestId = new Guid("00000000-0000-0000-0000-808080808080");
            
            lock (this)
            {
                m_archiverModule.ArchiveRegion(archiveWriteStream, requestId);
                //AssetServerBase assetServer = (AssetServerBase)scene.CommsManager.AssetCache.AssetServer;
                //while (assetServer.HasWaitingRequests())
                //    assetServer.ProcessNextRequest();
                
                Monitor.Wait(this, 60000);
            }
            
            Assert.That(m_lastRequestId, Is.EqualTo(requestId));

            byte[] archive = archiveWriteStream.ToArray();
            MemoryStream archiveReadStream = new MemoryStream(archive);
            TarArchiveReader tar = new TarArchiveReader(archiveReadStream);

            bool gotControlFile = false;
            bool gotNcAssetFile = false;
            
            string expectedNcAssetFileName = string.Format("{0}_{1}", ncAssetUuid, "notecard.txt");

            List<string> foundPaths = new List<string>();
            List<string> expectedPaths = new List<string>();
            expectedPaths.Add(ArchiveHelpers.CreateObjectPath(sog1));
            expectedPaths.Add(ArchiveHelpers.CreateObjectPath(sog2));

            string filePath;
            TarArchiveReader.TarEntryType tarEntryType;

            while (tar.ReadEntry(out filePath, out tarEntryType) != null)
            {
                if (ArchiveConstants.CONTROL_FILE_PATH == filePath)
                {
                    gotControlFile = true;
                }
                else if (filePath.StartsWith(ArchiveConstants.ASSETS_PATH))
                {
                    string fileName = filePath.Remove(0, ArchiveConstants.ASSETS_PATH.Length);

                    Assert.That(fileName, Is.EqualTo(expectedNcAssetFileName));
                    gotNcAssetFile = true;
                }
                else if (filePath.StartsWith(ArchiveConstants.OBJECTS_PATH))
                {
                    foundPaths.Add(filePath);
                }
            }

            Assert.That(gotControlFile, Is.True, "No control file in archive");
            Assert.That(gotNcAssetFile, Is.True, "No notecard asset file in archive");
            Assert.That(foundPaths, Is.EquivalentTo(expectedPaths));

            // TODO: Test presence of more files and contents of files.
        }

        /// <summary>
        /// Test loading a V0.2 OpenSim Region Archive.
        /// </summary>
        [Test]
        public void TestLoadOarV0_2()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            MemoryStream archiveWriteStream = new MemoryStream();
            TarArchiveWriter tar = new TarArchiveWriter(archiveWriteStream);
            
            // Put in a random blank directory to check that this doesn't upset the load process
            tar.WriteDir("ignoreme");
            
            // Also check that direct entries which will also have a file entry containing that directory doesn't 
            // upset load
            tar.WriteDir(ArchiveConstants.TERRAINS_PATH);
            
            tar.WriteFile(ArchiveConstants.CONTROL_FILE_PATH, ArchiveWriteRequestPreparation.CreateControlFile(new Dictionary<string, Object>()));

            SceneObjectPart part1 = CreateSceneObjectPart1();
            SceneObjectGroup object1 = new SceneObjectGroup(part1);

            // Let's put some inventory items into our object
            string soundItemName = "sound-item1";
            UUID soundItemUuid = UUID.Parse("00000000-0000-0000-0000-000000000002");
            Type type = GetType();
            Assembly assembly = type.Assembly;
            string soundDataResourceName = null;
            string[] names = assembly.GetManifestResourceNames();
            foreach (string name in names)
            {
                if (name.EndsWith(".Resources.test-sound.wav"))
                    soundDataResourceName = name;
            }
            Assert.That(soundDataResourceName, Is.Not.Null);

            byte[] soundData;
            Console.WriteLine("Loading " + soundDataResourceName);
            using (Stream resource = assembly.GetManifestResourceStream(soundDataResourceName))
            {
                using (BinaryReader br = new BinaryReader(resource))
                {
                    // FIXME: Use the inspector insteadthere are so many forums and lists already, though admittedly none of them are suitable for cross virtual-enivornemnt discussion
                    soundData = br.ReadBytes(99999999);
                    UUID soundUuid = UUID.Parse("00000000-0000-0000-0000-000000000001");
                    string soundAssetFileName 
                       = ArchiveConstants.ASSETS_PATH + soundUuid 
                            + ArchiveConstants.ASSET_TYPE_TO_EXTENSION[(sbyte)AssetType.SoundWAV];
                    tar.WriteFile(soundAssetFileName, soundData);
                    
                    /*
                    AssetBase soundAsset = AssetHelpers.CreateAsset(soundUuid, soundData);
                    scene.AssetService.Store(soundAsset);
                    asset1FileName = ArchiveConstants.ASSETS_PATH + soundUuid + ".wav";
                    */
                    
                    TaskInventoryItem item1 
                        = new TaskInventoryItem { AssetID = soundUuid, ItemID = soundItemUuid, Name = soundItemName };
                    part1.Inventory.AddInventoryItem(item1, true);
                }
            }
            
            m_scene.AddNewSceneObject(object1, false);

            string object1FileName = string.Format(
                "{0}_{1:000}-{2:000}-{3:000}__{4}.xml",
                part1.Name,
                Math.Round(part1.GroupPosition.X), Math.Round(part1.GroupPosition.Y), Math.Round(part1.GroupPosition.Z),
                part1.UUID);
            tar.WriteFile(ArchiveConstants.OBJECTS_PATH + object1FileName, SceneObjectSerializer.ToXml2Format(object1));
            
            tar.Close();

            MemoryStream archiveReadStream = new MemoryStream(archiveWriteStream.ToArray());

            lock (this)
            {
                m_scene.EventManager.OnOarFileLoaded += LoadCompleted;
                m_archiverModule.DearchiveRegion(archiveReadStream);
            }
            
            Assert.That(m_lastErrorMessage, Is.Null);

            SceneObjectPart object1PartLoaded = m_scene.GetSceneObjectPart(part1.Name);

            Assert.That(object1PartLoaded, Is.Not.Null, "object1 was not loaded");
            Assert.That(object1PartLoaded.Name, Is.EqualTo(part1.Name), "object1 names not identical");
            Assert.That(object1PartLoaded.GroupPosition, Is.EqualTo(part1.GroupPosition), "object1 group position not equal");
            Assert.That(
                object1PartLoaded.RotationOffset, Is.EqualTo(part1.RotationOffset), "object1 rotation offset not equal");
            Assert.That(
                object1PartLoaded.OffsetPosition, Is.EqualTo(part1.OffsetPosition), "object1 offset position not equal");

            TaskInventoryItem loadedSoundItem = object1PartLoaded.Inventory.GetInventoryItems(soundItemName)[0];
            Assert.That(loadedSoundItem, Is.Not.Null, "loaded sound item was null");
            AssetBase loadedSoundAsset = m_scene.AssetService.Get(loadedSoundItem.AssetID.ToString());
            Assert.That(loadedSoundAsset, Is.Not.Null, "loaded sound asset was null");
            Assert.That(loadedSoundAsset.Data, Is.EqualTo(soundData), "saved and loaded sound data do not match");

            // Temporary
            Console.WriteLine("Successfully completed {0}", MethodBase.GetCurrentMethod());
        }

        /// <summary>
        /// Test loading the region settings of a V0.2 OpenSim Region Archive.
        /// </summary>
        [Test]
        public void TestLoadOarV0_2RegionSettings()
        {
            TestHelper.InMethod();
            //log4net.Config.XmlConfigurator.Configure();

            MemoryStream archiveWriteStream = new MemoryStream();
            TarArchiveWriter tar = new TarArchiveWriter(archiveWriteStream);
            
            tar.WriteDir(ArchiveConstants.TERRAINS_PATH);
            tar.WriteFile(ArchiveConstants.CONTROL_FILE_PATH, ArchiveWriteRequestPreparation.CreateControlFile(new Dictionary<string, Object>()));

            RegionSettings rs = new RegionSettings();
            rs.AgentLimit = 17;
            rs.AllowDamage = true;
            rs.AllowLandJoinDivide = true;
            rs.AllowLandResell = true;
            rs.BlockFly = true;
            rs.BlockShowInSearch = true;
            rs.BlockTerraform = true;
            rs.DisableCollisions = true;
            rs.DisablePhysics = true;
            rs.DisableScripts = true;
            rs.Elevation1NW = 15.9;
            rs.Elevation1NE = 45.3;
            rs.Elevation1SE = 49;
            rs.Elevation1SW = 1.9;
            rs.Elevation2NW = 4.5;
            rs.Elevation2NE = 19.2;
            rs.Elevation2SE = 9.2;
            rs.Elevation2SW = 2.1;
            rs.FixedSun = true;
            rs.ObjectBonus = 1.4;
            rs.RestrictPushing = true;
            rs.TerrainLowerLimit = 0.4;
            rs.TerrainRaiseLimit = 17.9;
            rs.TerrainTexture1 = UUID.Parse("00000000-0000-0000-0000-000000000020");
            rs.TerrainTexture2 = UUID.Parse("00000000-0000-0000-0000-000000000040");
            rs.TerrainTexture3 = UUID.Parse("00000000-0000-0000-0000-000000000060");
            rs.TerrainTexture4 = UUID.Parse("00000000-0000-0000-0000-000000000080");
            rs.UseEstateSun = true;
            rs.WaterHeight = 23;

            tar.WriteFile(ArchiveConstants.SETTINGS_PATH + "region1.xml", RegionSettingsSerializer.Serialize(rs));
            
            tar.Close();

            MemoryStream archiveReadStream = new MemoryStream(archiveWriteStream.ToArray());

            lock (this)
            {
                m_scene.EventManager.OnOarFileLoaded += LoadCompleted;
                m_archiverModule.DearchiveRegion(archiveReadStream);
            }
            
            Assert.That(m_lastErrorMessage, Is.Null);
            RegionSettings loadedRs = m_scene.RegionInfo.RegionSettings;

            Assert.That(loadedRs.AgentLimit, Is.EqualTo(17));
            Assert.That(loadedRs.AllowDamage, Is.True);
            Assert.That(loadedRs.AllowLandJoinDivide, Is.True);
            Assert.That(loadedRs.AllowLandResell, Is.True);
            Assert.That(loadedRs.BlockFly, Is.True);
            Assert.That(loadedRs.BlockShowInSearch, Is.True);
            Assert.That(loadedRs.BlockTerraform, Is.True);
            Assert.That(loadedRs.DisableCollisions, Is.True);
            Assert.That(loadedRs.DisablePhysics, Is.True);
            Assert.That(loadedRs.DisableScripts, Is.True);
            Assert.That(loadedRs.Elevation1NW, Is.EqualTo(15.9));
            Assert.That(loadedRs.Elevation1NE, Is.EqualTo(45.3));
            Assert.That(loadedRs.Elevation1SE, Is.EqualTo(49));
            Assert.That(loadedRs.Elevation1SW, Is.EqualTo(1.9));
            Assert.That(loadedRs.Elevation2NW, Is.EqualTo(4.5));
            Assert.That(loadedRs.Elevation2NE, Is.EqualTo(19.2));
            Assert.That(loadedRs.Elevation2SE, Is.EqualTo(9.2));
            Assert.That(loadedRs.Elevation2SW, Is.EqualTo(2.1));
            Assert.That(loadedRs.FixedSun, Is.True);
            Assert.That(loadedRs.ObjectBonus, Is.EqualTo(1.4));
            Assert.That(loadedRs.RestrictPushing, Is.True);
            Assert.That(loadedRs.TerrainLowerLimit, Is.EqualTo(0.4));
            Assert.That(loadedRs.TerrainRaiseLimit, Is.EqualTo(17.9));
            Assert.That(loadedRs.TerrainTexture1, Is.EqualTo(UUID.Parse("00000000-0000-0000-0000-000000000020")));
            Assert.That(loadedRs.TerrainTexture2, Is.EqualTo(UUID.Parse("00000000-0000-0000-0000-000000000040")));
            Assert.That(loadedRs.TerrainTexture3, Is.EqualTo(UUID.Parse("00000000-0000-0000-0000-000000000060")));
            Assert.That(loadedRs.TerrainTexture4, Is.EqualTo(UUID.Parse("00000000-0000-0000-0000-000000000080")));
            Assert.That(loadedRs.UseEstateSun, Is.True);
            Assert.That(loadedRs.WaterHeight, Is.EqualTo(23));
        }
        
        /// <summary>
        /// Test merging a V0.2 OpenSim Region Archive into an existing scene
        /// </summary>
        //[Test]
        public void TestMergeOarV0_2()
        {
            TestHelper.InMethod();
            //XmlConfigurator.Configure();

            MemoryStream archiveWriteStream = new MemoryStream();

//            string part2Name = "objectMerge";
//            PrimitiveBaseShape part2Shape = PrimitiveBaseShape.CreateCylinder();
//            Vector3 part2GroupPosition = new Vector3(90, 80, 70);
//            Quaternion part2RotationOffset = new Quaternion(60, 70, 80, 90);
//            Vector3 part2OffsetPosition = new Vector3(20, 25, 30);

            SceneObjectPart part2 = CreateSceneObjectPart2();

            // Create an oar file that we can use for the merge
            {
                ArchiverModule archiverModule = new ArchiverModule();
                SerialiserModule serialiserModule = new SerialiserModule();
                TerrainModule terrainModule = new TerrainModule();

                Scene scene = SceneSetupHelpers.SetupScene();
                SceneSetupHelpers.SetupSceneModules(scene, archiverModule, serialiserModule, terrainModule);

                m_scene.AddNewSceneObject(new SceneObjectGroup(part2), false);

                // Write out this scene
                scene.EventManager.OnOarFileSaved += SaveCompleted;

                lock (this)
                {
                    m_archiverModule.ArchiveRegion(archiveWriteStream);
                    Monitor.Wait(this, 60000);
                }
            }

            {
                SceneObjectPart part1 = CreateSceneObjectPart1();
                m_scene.AddNewSceneObject(new SceneObjectGroup(part1), false);

                // Merge in the archive we created earlier
                byte[] archive = archiveWriteStream.ToArray();
                MemoryStream archiveReadStream = new MemoryStream(archive);

                m_archiverModule.DearchiveRegion(archiveReadStream, true, false, Guid.Empty);

                SceneObjectPart object1Existing = m_scene.GetSceneObjectPart(part1.Name);
                Assert.That(object1Existing, Is.Not.Null, "object1 was not present after merge");
                Assert.That(object1Existing.Name, Is.EqualTo(part1.Name), "object1 names not identical after merge");
                Assert.That(object1Existing.GroupPosition, Is.EqualTo(part1.GroupPosition), "object1 group position not equal after merge");

                SceneObjectPart object2PartMerged = m_scene.GetSceneObjectPart(part2.Name);
                Assert.That(object2PartMerged, Is.Not.Null, "object2 was not present after merge");
                Assert.That(object2PartMerged.Name, Is.EqualTo(part2.Name), "object2 names not identical after merge");
                Assert.That(object2PartMerged.GroupPosition, Is.EqualTo(part2.GroupPosition), "object2 group position not equal after merge");
            }
        }
    }
}
