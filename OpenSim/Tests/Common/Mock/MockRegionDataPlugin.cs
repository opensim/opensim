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

using System.Reflection;
using System.Collections.Generic;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Data.Null
{
    public class NullDataService : ISimulationDataService
    {
        private NullDataStore m_store;

        public NullDataService()
        {
            m_store = new NullDataStore();
        }

        public void StoreObject(SceneObjectGroup obj, UUID regionUUID)
        {
            m_store.StoreObject(obj, regionUUID);
        }

        public void RemoveObject(UUID uuid, UUID regionUUID)
        {
            m_store.RemoveObject(uuid, regionUUID);
        }

        public void StorePrimInventory(UUID primID, ICollection<TaskInventoryItem> items)
        {
            m_store.StorePrimInventory(primID, items);
        }

        public List<SceneObjectGroup> LoadObjects(UUID regionUUID)
        {
            return m_store.LoadObjects(regionUUID);
        }

        public void StoreTerrain(double[,] terrain, UUID regionID)
        {
            m_store.StoreTerrain(terrain, regionID);
        }

        public void StoreTerrain(TerrainData terrain, UUID regionID)
        {
            m_store.StoreTerrain(terrain, regionID);
        }

        public void StoreBakedTerrain(TerrainData terrain, UUID regionID)
        {
            m_store.StoreBakedTerrain(terrain, regionID);
        }

        public double[,] LoadTerrain(UUID regionID)
        {
            return m_store.LoadTerrain(regionID);
        }

        public TerrainData LoadTerrain(UUID regionID, int pSizeX, int pSizeY, int pSizeZ)
        {
            return m_store.LoadTerrain(regionID, pSizeX, pSizeY, pSizeZ);
        }

        public TerrainData LoadBakedTerrain(UUID regionID, int pSizeX, int pSizeY, int pSizeZ)
        {
            return m_store.LoadBakedTerrain(regionID, pSizeX, pSizeY, pSizeZ);
        }

        public void StoreLandObject(ILandObject Parcel)
        {
            m_store.StoreLandObject(Parcel);
        }

        public void RemoveLandObject(UUID globalID)
        {
            m_store.RemoveLandObject(globalID);
        }

        public List<LandData> LoadLandObjects(UUID regionUUID)
        {
            return m_store.LoadLandObjects(regionUUID);
        }

        public void StoreRegionSettings(RegionSettings rs)
        {
            m_store.StoreRegionSettings(rs);
        }

        public RegionSettings LoadRegionSettings(UUID regionUUID)
        {
            return m_store.LoadRegionSettings(regionUUID);
        }

        public RegionLightShareData LoadRegionWindlightSettings(UUID regionUUID)
        {
            return m_store.LoadRegionWindlightSettings(regionUUID);
        }

        public void RemoveRegionWindlightSettings(UUID regionID)
        {
        }

        public void StoreRegionWindlightSettings(RegionLightShareData wl)
        {
            m_store.StoreRegionWindlightSettings(wl);
        }

        public string LoadRegionEnvironmentSettings(UUID regionUUID)
        {
            return m_store.LoadRegionEnvironmentSettings(regionUUID);
        }

        public void StoreRegionEnvironmentSettings(UUID regionUUID, string settings)
        {
            m_store.StoreRegionEnvironmentSettings(regionUUID, settings);
        }

        public void RemoveRegionEnvironmentSettings(UUID regionUUID)
        {
            m_store.RemoveRegionEnvironmentSettings(regionUUID);
        }

        public UUID[] GetObjectIDs(UUID regionID)
        {
            return new UUID[0];
        }

        public void SaveExtra(UUID regionID, string name, string value)
        {
        }

        public void RemoveExtra(UUID regionID, string name)
        {
        }

        public Dictionary<string, string> GetExtra(UUID regionID)
        {
            return null;
        }
    }

    /// <summary>
    /// Mock region data plugin.  This obeys the api contract for persistence but stores everything in memory, so that
    /// tests can check correct persistence.
    /// </summary>
    public class NullDataStore : ISimulationDataStore
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected Dictionary<UUID, RegionSettings> m_regionSettings = new Dictionary<UUID, RegionSettings>();
        protected Dictionary<UUID, SceneObjectPart> m_sceneObjectParts = new Dictionary<UUID, SceneObjectPart>();
        protected Dictionary<UUID, ICollection<TaskInventoryItem>> m_primItems
            = new Dictionary<UUID, ICollection<TaskInventoryItem>>();
        protected Dictionary<UUID, TerrainData> m_terrains = new Dictionary<UUID, TerrainData>();
        protected Dictionary<UUID, TerrainData> m_bakedterrains = new Dictionary<UUID, TerrainData>();
        protected Dictionary<UUID, LandData> m_landData = new Dictionary<UUID, LandData>();

        public void Initialise(string dbfile)
        {
            return;
        }

        public void Dispose()
        {
        }

        public void StoreRegionSettings(RegionSettings rs)
        {
            m_regionSettings[rs.RegionUUID] = rs;
        }

        public RegionLightShareData LoadRegionWindlightSettings(UUID regionUUID)
        {
            //This connector doesn't support the windlight module yet
            //Return default LL windlight settings
            return new RegionLightShareData();
        }

        public void RemoveRegionWindlightSettings(UUID regionID)
        {
        }

        public void StoreRegionWindlightSettings(RegionLightShareData wl)
        {
            //This connector doesn't support the windlight module yet
        }

        #region Environment Settings
        public string LoadRegionEnvironmentSettings(UUID regionUUID)
        {
            //This connector doesn't support the Environment module yet
            return string.Empty;
        }

        public void StoreRegionEnvironmentSettings(UUID regionUUID, string settings)
        {
            //This connector doesn't support the Environment module yet
        }

        public void RemoveRegionEnvironmentSettings(UUID regionUUID)
        {
            //This connector doesn't support the Environment module yet
        }
        #endregion

        public RegionSettings LoadRegionSettings(UUID regionUUID)
        {
            RegionSettings rs = null;
            m_regionSettings.TryGetValue(regionUUID, out rs);

            if (rs == null)
                rs = new RegionSettings();

            return rs;
        }

        public void StoreObject(SceneObjectGroup obj, UUID regionUUID)
        {
            // We can't simply store groups here because on delinking, OpenSim will not update the original group
            // directly.  Rather, the newly delinked parts will be updated to be in their own scene object group
            // Therefore, we need to store parts rather than groups.
            foreach (SceneObjectPart prim in obj.Parts)
            {
//                m_log.DebugFormat(
//                    "[MOCK REGION DATA PLUGIN]: Storing part {0} {1} in object {2} {3} in region {4}",
//                    prim.Name, prim.UUID, obj.Name, obj.UUID, regionUUID);

                m_sceneObjectParts[prim.UUID] = prim;
            }
        }

        public void RemoveObject(UUID obj, UUID regionUUID)
        {
            // All parts belonging to the object with the uuid are removed.
            List<SceneObjectPart> parts = new List<SceneObjectPart>(m_sceneObjectParts.Values);
            foreach (SceneObjectPart part in parts)
            {
                if (part.ParentGroup.UUID == obj)
                {
//                    m_log.DebugFormat(
//                        "[MOCK REGION DATA PLUGIN]: Removing part {0} {1} as part of object {2} from {3}",
//                        part.Name, part.UUID, obj, regionUUID);
                    m_sceneObjectParts.Remove(part.UUID);
                }
            }
        }

        public void StorePrimInventory(UUID primID, ICollection<TaskInventoryItem> items)
        {
            m_primItems[primID] = items;
        }

        public List<SceneObjectGroup> LoadObjects(UUID regionUUID)
        {
            Dictionary<UUID, SceneObjectGroup> objects = new Dictionary<UUID, SceneObjectGroup>();

            // Create all of the SOGs from the root prims first
            foreach (SceneObjectPart prim in m_sceneObjectParts.Values)
            {
                if (prim.IsRoot)
                {
//                    m_log.DebugFormat(
//                        "[MOCK REGION DATA PLUGIN]: Loading root part {0} {1} in {2}", prim.Name, prim.UUID, regionUUID);
                    objects[prim.UUID] = new SceneObjectGroup(prim);
                }
            }

            // Add all of the children objects to the SOGs
            foreach (SceneObjectPart prim in m_sceneObjectParts.Values)
            {
                SceneObjectGroup sog;
                if (prim.UUID != prim.ParentUUID)
                {
                    if (objects.TryGetValue(prim.ParentUUID, out sog))
                    {
                        int originalLinkNum = prim.LinkNum;

                        sog.AddPart(prim);

                        // SceneObjectGroup.AddPart() tries to be smart and automatically set the LinkNum.
                        // We override that here
                        if (originalLinkNum != 0)
                            prim.LinkNum = originalLinkNum;
                    }
                    else
                    {
//                        m_log.WarnFormat(
//                            "[MOCK REGION DATA PLUGIN]: Database contains an orphan child prim {0} {1} in region {2} pointing to missing parent {3}.  This prim will not be loaded.",
//                            prim.Name, prim.UUID, regionUUID, prim.ParentUUID);
                    }
                }
            }

            // TODO: Load items.  This is assymetric - we store items as a separate method but don't retrieve them that
            // way!

            return new List<SceneObjectGroup>(objects.Values);
        }

        public void StoreTerrain(TerrainData ter, UUID regionID)
        {
            m_terrains[regionID] = ter;
        }

        public void StoreBakedTerrain(TerrainData ter, UUID regionID)
        {
            m_bakedterrains[regionID] = ter;
        }

        public void StoreTerrain(double[,] ter, UUID regionID)
        {
            m_terrains[regionID] = new HeightmapTerrainData(ter);
        }

        public TerrainData LoadTerrain(UUID regionID, int pSizeX, int pSizeY, int pSizeZ)
        {
            if (m_terrains.ContainsKey(regionID))
                return m_terrains[regionID];
            else
                return null;
        }

        public TerrainData LoadBakedTerrain(UUID regionID, int pSizeX, int pSizeY, int pSizeZ)
        {
            if (m_bakedterrains.ContainsKey(regionID))
                return m_bakedterrains[regionID];
            else
                return null;
        }

        public double[,] LoadTerrain(UUID regionID)
        {
            if (m_terrains.ContainsKey(regionID))
                return m_terrains[regionID].GetDoubles();
            else
                return null;
        }

        public void RemoveLandObject(UUID globalID)
        {
            if (m_landData.ContainsKey(globalID))
                m_landData.Remove(globalID);
        }

        public void StoreLandObject(ILandObject land)
        {
            m_landData[land.LandData.GlobalID] = land.LandData;
        }

        public List<LandData> LoadLandObjects(UUID regionUUID)
        {
            return new List<LandData>(m_landData.Values);
        }

        public void Shutdown()
        {
        }

        public UUID[] GetObjectIDs(UUID regionID)
        {
            return new UUID[0];
        }

        public void SaveExtra(UUID regionID, string name, string value)
        {
        }

        public void RemoveExtra(UUID regionID, string name)
        {
        }

        public Dictionary<string, string> GetExtra(UUID regionID)
        {
            return null;
        }
    }
}
