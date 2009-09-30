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
using System.IO;
using System.Reflection;
using System.Threading;
using log4net.Config;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Serialization;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Region.CoreModules.World.Archiver.Tests
{
    [TestFixture]
    public class ArchiverTests
    {
        private Guid m_lastRequestId;
        private string m_lastErrorMessage;
        
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

        /// <summary>
        /// Test saving a V0.2 OpenSim Region Archive.
        /// </summary>
        [Test]
        public void TestSaveOarV0_2()
        {
            TestHelper.InMethod();
            //log4net.Config.XmlConfigurator.Configure();

            ArchiverModule archiverModule = new ArchiverModule();
            SerialiserModule serialiserModule = new SerialiserModule();
            TerrainModule terrainModule = new TerrainModule();

            Scene scene = SceneSetupHelpers.SetupScene("asset");
            SceneSetupHelpers.SetupSceneModules(scene, archiverModule, serialiserModule, terrainModule);

            SceneObjectPart part1;

            // Create and add prim 1
            {
                string partName = "My Little Pony";
                UUID ownerId = UUID.Parse("00000000-0000-0000-0000-000000000015");
                PrimitiveBaseShape shape = PrimitiveBaseShape.CreateSphere();
                Vector3 groupPosition = new Vector3(10, 20, 30);
                Quaternion rotationOffset = new Quaternion(20, 30, 40, 50);
                Vector3 offsetPosition = new Vector3(5, 10, 15);

                part1
                    = new SceneObjectPart(
                        ownerId, shape, groupPosition, rotationOffset, offsetPosition);
                part1.Name = partName;

                scene.AddNewSceneObject(new SceneObjectGroup(part1), false);
            }

            SceneObjectPart part2;

            // Create and add prim 2
            {
                string partName = "Action Man";
                UUID ownerId = UUID.Parse("00000000-0000-0000-0000-000000000016");
                PrimitiveBaseShape shape = PrimitiveBaseShape.CreateCylinder();
                Vector3 groupPosition = new Vector3(90, 80, 70);
                Quaternion rotationOffset = new Quaternion(60, 70, 80, 90);
                Vector3 offsetPosition = new Vector3(20, 25, 30);

                part2
                    = new SceneObjectPart(
                        ownerId, shape, groupPosition, rotationOffset, offsetPosition);
                part2.Name = partName;

                scene.AddNewSceneObject(new SceneObjectGroup(part2), false);
            }

            MemoryStream archiveWriteStream = new MemoryStream();
            scene.EventManager.OnOarFileSaved += SaveCompleted;

            Guid requestId = new Guid("00000000-0000-0000-0000-808080808080");
            
            lock (this)
            {
                archiverModule.ArchiveRegion(archiveWriteStream, requestId);
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
            bool gotObject1File = false;
            bool gotObject2File = false;
            string expectedObject1FileName = string.Format(
                "{0}_{1:000}-{2:000}-{3:000}__{4}.xml",
                part1.Name,
                Math.Round(part1.GroupPosition.X), Math.Round(part1.GroupPosition.Y), Math.Round(part1.GroupPosition.Z),
                part1.UUID);
            string expectedObject2FileName = string.Format(
                "{0}_{1:000}-{2:000}-{3:000}__{4}.xml",
                part2.Name,
                Math.Round(part2.GroupPosition.X), Math.Round(part2.GroupPosition.Y), Math.Round(part2.GroupPosition.Z),
                part2.UUID);

            string filePath;
            TarArchiveReader.TarEntryType tarEntryType;

            while (tar.ReadEntry(out filePath, out tarEntryType) != null)
            {
                if (ArchiveConstants.CONTROL_FILE_PATH == filePath)
                {
                    gotControlFile = true;
                }
                else if (filePath.StartsWith(ArchiveConstants.OBJECTS_PATH))
                {
                    string fileName = filePath.Remove(0, ArchiveConstants.OBJECTS_PATH.Length);

                    if (fileName.StartsWith(part1.Name))
                    {
                        Assert.That(fileName, Is.EqualTo(expectedObject1FileName));
                        gotObject1File = true;
                    }
                    else if (fileName.StartsWith(part2.Name))
                    {
                        Assert.That(fileName, Is.EqualTo(expectedObject2FileName));
                        gotObject2File = true;
                    }
                }
            }

            Assert.That(gotControlFile, Is.True, "No control file in archive");
            Assert.That(gotObject1File, Is.True, "No object1 file in archive");
            Assert.That(gotObject2File, Is.True, "No object2 file in archive");

            // TODO: Test presence of more files and contents of files.
        }

        /// <summary>
        /// Test loading a V0.2 OpenSim Region Archive.
        /// </summary>
        [Test]
        public void TestLoadOarV0_2()
        {
            TestHelper.InMethod();
            //log4net.Config.XmlConfigurator.Configure();

            MemoryStream archiveWriteStream = new MemoryStream();
            TarArchiveWriter tar = new TarArchiveWriter(archiveWriteStream);
            
            // Put in a random blank directory to check that this doesn't upset the load process
            tar.WriteDir("ignoreme");
            
            // Also check that direct entries which will also have a file entry containing that directory doesn't 
            // upset load
            tar.WriteDir(ArchiveConstants.TERRAINS_PATH);

            tar.WriteFile(ArchiveConstants.CONTROL_FILE_PATH, ArchiveWriteRequestExecution.Create0p2ControlFile());

            string part1Name = "object1";
            PrimitiveBaseShape shape = PrimitiveBaseShape.CreateCylinder();
            Vector3 groupPosition = new Vector3(90, 80, 70);
            Quaternion rotationOffset = new Quaternion(60, 70, 80, 90);
            Vector3 offsetPosition = new Vector3(20, 25, 30);

            SerialiserModule serialiserModule = new SerialiserModule();
            ArchiverModule archiverModule = new ArchiverModule();

            Scene scene = SceneSetupHelpers.SetupScene();
            SceneSetupHelpers.SetupSceneModules(scene, serialiserModule, archiverModule);

            SceneObjectPart part1
                = new SceneObjectPart(
                    UUID.Zero, shape, groupPosition, rotationOffset, offsetPosition);
            part1.Name = part1Name;
            SceneObjectGroup object1 = new SceneObjectGroup(part1);
            scene.AddNewSceneObject(object1, false);

            string object1FileName = string.Format(
                "{0}_{1:000}-{2:000}-{3:000}__{4}.xml",
                part1Name,
                Math.Round(groupPosition.X), Math.Round(groupPosition.Y), Math.Round(groupPosition.Z),
                part1.UUID);
            tar.WriteFile(ArchiveConstants.OBJECTS_PATH + object1FileName, SceneObjectSerializer.ToXml2Format(object1));
            
            tar.Close();

            MemoryStream archiveReadStream = new MemoryStream(archiveWriteStream.ToArray());

            lock (this)
            {
                scene.EventManager.OnOarFileLoaded += LoadCompleted;
                archiverModule.DearchiveRegion(archiveReadStream);
            }
            
            Assert.That(m_lastErrorMessage, Is.Null);

            SceneObjectPart object1PartLoaded = scene.GetSceneObjectPart(part1Name);

            Assert.That(object1PartLoaded, Is.Not.Null, "object1 was not loaded");
            Assert.That(object1PartLoaded.Name, Is.EqualTo(part1Name), "object1 names not identical");
            Assert.That(object1PartLoaded.GroupPosition, Is.EqualTo(groupPosition), "object1 group position not equal");
            Assert.That(
                object1PartLoaded.RotationOffset, Is.EqualTo(rotationOffset), "object1 rotation offset not equal");
            Assert.That(
                object1PartLoaded.OffsetPosition, Is.EqualTo(offsetPosition), "object1 offset position not equal");

            // Temporary
            Console.WriteLine("Successfully completed {0}", MethodBase.GetCurrentMethod());
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

            string part2Name = "objectMerge";
            PrimitiveBaseShape part2Shape = PrimitiveBaseShape.CreateCylinder();
            Vector3 part2GroupPosition = new Vector3(90, 80, 70);
            Quaternion part2RotationOffset = new Quaternion(60, 70, 80, 90);
            Vector3 part2OffsetPosition = new Vector3(20, 25, 30);

            // Create an oar file that we can use for the merge
            {
                ArchiverModule archiverModule = new ArchiverModule();
                SerialiserModule serialiserModule = new SerialiserModule();
                TerrainModule terrainModule = new TerrainModule();

                Scene scene = SceneSetupHelpers.SetupScene();
                SceneSetupHelpers.SetupSceneModules(scene, archiverModule, serialiserModule, terrainModule);

                SceneObjectPart part2
                    = new SceneObjectPart(
                        UUID.Zero, part2Shape, part2GroupPosition, part2RotationOffset, part2OffsetPosition);
                part2.Name = part2Name;
                SceneObjectGroup object2 = new SceneObjectGroup(part2);

                scene.AddNewSceneObject(object2, false);

                // Write out this scene
                scene.EventManager.OnOarFileSaved += SaveCompleted;

                lock (this)
                {
                    archiverModule.ArchiveRegion(archiveWriteStream);
                    Monitor.Wait(this, 60000);
                }
            }

            {
                ArchiverModule archiverModule = new ArchiverModule();
                SerialiserModule serialiserModule = new SerialiserModule();
                TerrainModule terrainModule = new TerrainModule();

                Scene scene = SceneSetupHelpers.SetupScene();
                SceneSetupHelpers.SetupSceneModules(scene, archiverModule, serialiserModule, terrainModule);

                string part1Name = "objectExisting";
                PrimitiveBaseShape part1Shape = PrimitiveBaseShape.CreateCylinder();
                Vector3 part1GroupPosition = new Vector3(80, 70, 60);
                Quaternion part1RotationOffset = new Quaternion(50, 60, 70, 80);
                Vector3 part1OffsetPosition = new Vector3(15, 20, 25);

                SceneObjectPart part1
                    = new SceneObjectPart(
                        UUID.Zero, part1Shape, part1GroupPosition, part1RotationOffset, part1OffsetPosition);
                part1.Name = part1Name;
                SceneObjectGroup object1 = new SceneObjectGroup(part1);

                scene.AddNewSceneObject(object1, false);

                // Merge in the archive we created earlier
                byte[] archive = archiveWriteStream.ToArray();
                MemoryStream archiveReadStream = new MemoryStream(archive);

                archiverModule.DearchiveRegion(archiveReadStream, true, Guid.Empty);

                SceneObjectPart object1Existing = scene.GetSceneObjectPart(part1Name);
                Assert.That(object1Existing, Is.Not.Null, "object1 was not present after merge");
                Assert.That(object1Existing.Name, Is.EqualTo(part1Name), "object1 names not identical after merge");
                Assert.That(object1Existing.GroupPosition, Is.EqualTo(part1GroupPosition), "object1 group position not equal after merge");

                SceneObjectPart object2PartMerged = scene.GetSceneObjectPart(part2Name);
                Assert.That(object2PartMerged, Is.Not.Null, "object2 was not present after merge");
                Assert.That(object2PartMerged.Name, Is.EqualTo(part2Name), "object2 names not identical after merge");
                Assert.That(object2PartMerged.GroupPosition, Is.EqualTo(part2GroupPosition), "object2 group position not equal after merge");
            }
        }
    }
}
