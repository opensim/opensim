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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface ISimulationDataService
    {
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

        /// <summary>
        /// Load persisted objects from region storage.
        /// </summary>
        /// <param name="regionUUID">the Region UUID</param>
        /// <returns>List of loaded groups</returns>
        List<SceneObjectGroup> LoadObjects(UUID regionUUID);

        /// <summary>
        /// Store a terrain revision in region storage
        /// </summary>
        /// <param name="ter">HeightField data</param>
        /// <param name="regionID">region UUID</param>
        void StoreTerrain(TerrainData terrain, UUID regionID);

        // Legacy version kept for downward compabibility
        void StoreTerrain(double[,] terrain, UUID regionID);

        /// <summary>
        /// Load the latest terrain revision from region storage
        /// </summary>
        /// <param name="regionID">the region UUID</param>
        /// <param name="sizeX">the X dimension of the region being filled</param>
        /// <param name="sizeY">the Y dimension of the region being filled</param>
        /// <param name="sizeZ">the Z dimension of the region being filled</param>
        /// <returns>Heightfield data</returns>
        TerrainData LoadTerrain(UUID regionID, int pSizeX, int pSizeY, int pSizeZ);

        // Legacy version kept for downward compabibility
        double[,] LoadTerrain(UUID regionID);

        void StoreLandObject(ILandObject Parcel);

        /// <summary>
        /// <list type="bullet">
        /// <item>delete from land where UUID=globalID</item>
        /// <item>delete from landaccesslist where LandUUID=globalID</item>
        /// </list>
        /// </summary>
        /// <param name="globalID"></param>
        void RemoveLandObject(UUID globalID);

        List<LandData> LoadLandObjects(UUID regionUUID);

        void StoreRegionSettings(RegionSettings rs);
        RegionSettings LoadRegionSettings(UUID regionUUID);
        RegionLightShareData LoadRegionWindlightSettings(UUID regionUUID);
        void StoreRegionWindlightSettings(RegionLightShareData wl);
        void RemoveRegionWindlightSettings(UUID regionID);

        /// <summary>
        /// Load Environment settings from region storage
        /// </summary>
        /// <param name="regionUUID">the region UUID</param>
        /// <returns>LLSD string for viewer</returns>
        string LoadRegionEnvironmentSettings(UUID regionUUID);

        /// <summary>
        /// Store Environment settings into region storage
        /// </summary>
        /// <param name="regionUUID">the region UUID</param>
        /// <param name="settings">LLSD string from viewer</param>
        void StoreRegionEnvironmentSettings(UUID regionUUID, string settings);

        /// <summary>
        /// Delete Environment settings from region storage
        /// </summary>
        /// <param name="regionUUID">the region UUID</param>
        void RemoveRegionEnvironmentSettings(UUID regionUUID);

        UUID[] GetObjectIDs(UUID regionID);

        void SaveExtra(UUID regionID, string name, string value);

        void RemoveExtra(UUID regionID, string name);

        Dictionary<string, string> GetExtra(UUID regionID);
    }
}
