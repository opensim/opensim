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
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    /// Scene presence tests
    /// </summary>
    [TestFixture]
    public class SceneTests
    {
        private class FakeStorageManager : StorageManager
        {
            private class FakeRegionDataStore : IRegionDataStore
            {
                public void Initialise(string filename)
                {
                }

                public void Dispose()
                {
                }

                public void StoreObject(SceneObjectGroup obj, UUID regionUUID)
                {
                    throw new NotImplementedException();
                }

                public void RemoveObject(UUID uuid, UUID regionUUID)
                {
                    throw new NotImplementedException();
                }

                public void StorePrimInventory(UUID primID, ICollection<TaskInventoryItem> items)
                {
                    throw new NotImplementedException();
                }

                public List<SceneObjectGroup> LoadObjects(UUID regionUUID)
                {
                    throw new NotImplementedException();
                }

                public void StoreTerrain(double[,] terrain, UUID regionID)
                {
                    throw new NotImplementedException();
                }

                public double[,] LoadTerrain(UUID regionID)
                {
                    throw new NotImplementedException();
                }

                public void StoreLandObject(ILandObject Parcel)
                {
                    throw new NotImplementedException();
                }

                public void RemoveLandObject(UUID globalID)
                {
                    throw new NotImplementedException();
                }

                public List<LandData> LoadLandObjects(UUID regionUUID)
                {
                    throw new NotImplementedException();
                }

                public void StoreRegionSettings(RegionSettings rs)
                {
                    throw new NotImplementedException();
                }

                public RegionSettings LoadRegionSettings(UUID regionUUID)
                {
                    return null;
                }

                public void Shutdown()
                {
                    throw new NotImplementedException();
                }
            }

            public FakeStorageManager() : base(new FakeRegionDataStore())
            {
            }

            public FakeStorageManager(IRegionDataStore storage) : this()
            {
            }

            public FakeStorageManager(string dllName, string connectionstring, string estateconnectionstring) : this()
            {
            }
        }

        [Test]
        public void TestConstructor()
        {
            RegionInfo regionInfo = new RegionInfo(0,0,null,null);
            FakeStorageManager storageManager = new FakeStorageManager();

            new Scene(regionInfo, null, null, null, storageManager, null, false, false, false, null, null);
        }
    }
}
