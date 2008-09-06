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

using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface IRegionDataStore
    {
        /// <summary>
        /// Initialises the data storage engine
        /// </summary>
        /// <param name="filename">The file to save the database to (may not be applicable).  Alternatively,
        /// a connection string for the database</param>
        void Initialise(string filename);

        /// <summary>
        /// Stores all object's details apart from inventory
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="regionUUID"></param>
        void StoreObject(SceneObjectGroup obj, UUID regionUUID);

        /// <summary>
        /// Entirely removes the object, including inventory
        /// </summary>
        /// <param name="uuid"></param>
        /// <param name="regionUUID"></param>
        /// <returns></returns>
        void RemoveObject(UUID uuid, UUID regionUUID);

        /// <summary>
        /// Store a prim's inventory
        /// </summary>
        /// <returns></returns>
        void StorePrimInventory(UUID primID, ICollection<TaskInventoryItem> items);

        List<SceneObjectGroup> LoadObjects(UUID regionUUID);

        void StoreTerrain(double[,] terrain, UUID regionID);
        double[,] LoadTerrain(UUID regionID);

        void StoreLandObject(ILandObject Parcel);
        void RemoveLandObject(UUID globalID);
        List<LandData> LoadLandObjects(UUID regionUUID);

        void StoreRegionSettings(RegionSettings rs);
        RegionSettings LoadRegionSettings(UUID regionUUID);

        void Shutdown();
    }
}
