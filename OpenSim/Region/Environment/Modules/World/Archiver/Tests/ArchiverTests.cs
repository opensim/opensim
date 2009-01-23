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

using System.IO;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.World.Archiver;
using OpenSim.Region.Environment.Modules.World.Terrain;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Tests.Common.Setup;

namespace OpenSim.Region.Environment.Modules.World.Archiver.Tests
{
    [TestFixture]
    public class ArchiverTests
    {
        /// <summary>
        /// Test saving a V0.2 OpenSim Region Archive.  Does not yet do what it says on the tin
        /// </summary>
        [Test]        
        public void TestSaveOarV0p2()
        {        
            //log4net.Config.XmlConfigurator.Configure();
            
            ArchiverModule archiverModule = new ArchiverModule();
            TerrainModule terrainModule = new TerrainModule();
            
            Scene scene = SceneSetupHelpers.SetupScene();
            SceneSetupHelpers.SetupSceneModules(scene, archiverModule, terrainModule);

            MemoryStream archiveWriteStream = new MemoryStream();
            archiverModule.ArchiveRegion(archiveWriteStream);

            // If there are no assets to fetch, then the entire archive region code path will execute in this thread,
            // so no need to worry about signalling.
            MemoryStream archiveReadStream = new MemoryStream(archiveWriteStream.ToArray());
            TarArchiveReader tar = new TarArchiveReader(archiveReadStream);
        
            bool gotControlFile = false;            
            
            string filePath;
            TarArchiveReader.TarEntryType tarEntryType;
            
            while (tar.ReadEntry(out filePath, out tarEntryType) != null)
            {
                if (ArchiveConstants.CONTROL_FILE_PATH == filePath)
                    gotControlFile = true;
            }

            Assert.That(gotControlFile, Is.True, "No control file in archive");
            
            // TODO: Test presence of more files and contents of files.
        }
    }
}