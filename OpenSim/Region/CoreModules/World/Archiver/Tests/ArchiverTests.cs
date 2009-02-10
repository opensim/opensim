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
using System.IO;
using System.Threading;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.CoreModules.World.Archiver;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.CoreModules.World.Terrain;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Region.CoreModules.World.Archiver.Tests
{
    [TestFixture]
    public class ArchiverTests
    {
        private EventWaitHandle m_waitHandle = new AutoResetEvent(false);
        
        private void SaveCompleted(string errorMessage)
        {
            m_waitHandle.Set();
        }
        
        /// <summary>
        /// Test saving a V0.2 OpenSim Region Archive.
        /// </summary>
        [Test]        
        public void TestSaveOarV0p2()
        {        
            log4net.Config.XmlConfigurator.Configure();
            
            ArchiverModule archiverModule = new ArchiverModule();
            SerialiserModule serialiserModule = new SerialiserModule();
            TerrainModule terrainModule = new TerrainModule();
            
            Scene scene = SceneSetupHelpers.SetupScene();
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
            archiverModule.ArchiveRegion(archiveWriteStream);            
            m_waitHandle.WaitOne(60000, true);

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
        /// Test loading a V0.2 OpenSim Region Archive.  Does not yet do what it says on the tin.
        /// </summary>        
        [Test]
        public void TestLoadOarV0p2()
        {
            MemoryStream archiveWriteStream = new MemoryStream();
            TarArchiveWriter tar = new TarArchiveWriter();
            
            tar.AddFile(ArchiveConstants.CONTROL_FILE_PATH, ArchiveWriteRequestExecution.Create0p2ControlFile());
            tar.WriteTar(archiveWriteStream);
            
            MemoryStream archiveReadStream = new MemoryStream(archiveWriteStream.ToArray());
            
            ArchiverModule archiverModule = new ArchiverModule();
            
            Scene scene = SceneSetupHelpers.SetupScene();
            SceneSetupHelpers.SetupSceneModules(scene, archiverModule);          
            
            archiverModule.DearchiveRegion(archiveReadStream);
            
            // TODO: Okay, so nothing is tested yet apart from the fact that it doesn't blow up
        }
    }
}