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
using System.Text;
using System.Threading;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.CoreModules.Avatar.Inventory.Archiver;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Region.CoreModules.Avatar.Inventory.Archiver.Tests
{
    [TestFixture]
    public class InventoryArchiverTests
    {
        private void SaveCompleted(
            bool succeeded, CachedUserInfo userInfo, string invPath, Stream saveStream, Exception reportedException)
        {
            lock (this)
            {
                Monitor.PulseAll(this);
            }            
        }
        
        /// <summary>
        /// Test saving a V0.1 OpenSim Inventory Archive (subject to change since there is no fixed format yet).
        /// </summary>
        [Test]
        public void TestSaveIarV0p1()
        {        
            //log4net.Config.XmlConfigurator.Configure();
            
            InventoryArchiverModule archiverModule = new InventoryArchiverModule();
            
            Scene scene = SceneSetupHelpers.SetupScene();
            SceneSetupHelpers.SetupSceneModules(scene, archiverModule);
            CommunicationsManager cm = scene.CommsManager;
                        
            // Create user            
            string userFirstName = "Jock";
            string userLastName = "Stirrup";
            UUID userId = UUID.Parse("00000000-0000-0000-0000-000000000020");
            cm.UserAdminService.AddUser(userFirstName, userLastName, string.Empty, string.Empty, 1000, 1000, userId);
            CachedUserInfo userInfo = cm.UserProfileCacheService.GetUserDetails(userId);
            userInfo.FetchInventory();
            
            // Create asset
            SceneObjectGroup object1;
            SceneObjectPart part1;
            {
                string partName = "My Little Dog Object";
                UUID ownerId = UUID.Parse("00000000-0000-0000-0000-000000000040");
                PrimitiveBaseShape shape = PrimitiveBaseShape.CreateSphere();
                Vector3 groupPosition = new Vector3(10, 20, 30);
                Quaternion rotationOffset = new Quaternion(20, 30, 40, 50);
                Vector3 offsetPosition = new Vector3(5, 10, 15);
                
                part1 
                    = new SceneObjectPart(
                        ownerId, shape, groupPosition, rotationOffset, offsetPosition);
                part1.Name = partName;
                
                object1 = new SceneObjectGroup(part1);
            }

            UUID asset1Id = UUID.Parse("00000000-0000-0000-0000-000000000060");           
            AssetBase asset1 = new AssetBase();
            asset1.FullID = asset1Id;
            asset1.Data = Encoding.ASCII.GetBytes(object1.ToXmlString2());            
            cm.AssetCache.AddAsset(asset1);
            
            // Create item
            InventoryItemBase item1 = new InventoryItemBase();
            item1.Name = "My Little Dog";
            item1.AssetID = asset1.FullID;
            item1.Folder = userInfo.RootFolder.FindFolderByPath("Objects").ID;            
            scene.AddInventoryItem(userId, item1);            
            
            MemoryStream archiveWriteStream = new MemoryStream();
            archiverModule.OnInventoryArchiveSaved += SaveCompleted;                
             
            archiverModule.ArchiveInventory(userFirstName, userLastName, "Objects", archiveWriteStream);
            
            lock (this)
            {
                archiverModule.ArchiveInventory(userFirstName, userLastName, "Objects", archiveWriteStream);                            
                Monitor.Wait(this, 60000);
            } 

            /*
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
            */
        }        
    }
}