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
    /// <summary>
    /// Mock region data plugin.  This obeys the api contract for persistence but stores everything in memory, so that
    /// tests can check correct persistence.
    /// </summary>
    public class NullDataStore : IRegionDataStore
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        protected Dictionary<UUID, RegionSettings> m_regionSettings = new Dictionary<UUID, RegionSettings>();
        protected Dictionary<UUID, SceneObjectGroup> m_sceneObjects = new Dictionary<UUID, SceneObjectGroup>();
        protected Dictionary<UUID, ICollection<TaskInventoryItem>> m_primItems 
            = new Dictionary<UUID, ICollection<TaskInventoryItem>>();
        protected Dictionary<UUID, double[,]> m_terrains = new Dictionary<UUID, double[,]>();
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
        
        public void StoreRegionWindlightSettings(RegionLightShareData wl)
        {
            //This connector doesn't support the windlight module yet
        }
        
        public RegionSettings LoadRegionSettings(UUID regionUUID)
        {
            RegionSettings rs = null;
            m_regionSettings.TryGetValue(regionUUID, out rs);
            return rs;
        }

        public void StoreObject(SceneObjectGroup obj, UUID regionUUID)
        {
            m_log.DebugFormat(
                "[MOCK REGION DATA PLUGIN]: Storing object {0} {1} in {2}", obj.Name, obj.UUID, regionUUID);
            m_sceneObjects[obj.UUID] = obj;
        }

        public void RemoveObject(UUID obj, UUID regionUUID)
        {
            m_log.DebugFormat(
                "[MOCK REGION DATA PLUGIN]: Removing object {0} from {1}", obj, regionUUID);
            
            if (m_sceneObjects.ContainsKey(obj))
                m_sceneObjects.Remove(obj);
        }

        // see IRegionDatastore
        public void StorePrimInventory(UUID primID, ICollection<TaskInventoryItem> items)
        {
            m_primItems[primID] = items;
        }

        public List<SceneObjectGroup> LoadObjects(UUID regionUUID)
        {
            m_log.DebugFormat(
                "[MOCK REGION DATA PLUGIN]: Loading objects from {0}", regionUUID);
            
            return new List<SceneObjectGroup>(m_sceneObjects.Values);
        }

        public void StoreTerrain(double[,] ter, UUID regionID)
        {
            m_terrains[regionID] = ter;
        }

        public double[,] LoadTerrain(UUID regionID)
        {
            if (m_terrains.ContainsKey(regionID))
                return m_terrains[regionID];
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
    }
}