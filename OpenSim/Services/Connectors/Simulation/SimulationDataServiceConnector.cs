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
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Server.Base;

namespace OpenSim.Services.Connectors.Simulation
{
    public class SimulationDataServiceConnector : ISimulationDataService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ISimulationDataStore m_simDataStore;

        public SimulationDataServiceConnector()
        {
        }

        public SimulationDataServiceConnector(IConfigSource config)
        {
            Initialise(config);
        }

        public virtual void Initialise(IConfigSource config)
        {
            IConfig serverConfig = config.Configs["SimulationDataStore"];
            if (serverConfig == null)
                throw new Exception("No section 'SimulationDataStore' in config file");

            string simDataStore = serverConfig.GetString("StoreModule", String.Empty);

            Object[] args = new Object[] { config };
            m_simDataStore = ServerUtils.LoadPlugin<ISimulationDataStore>(simDataStore, args);
        }

        public void StoreObject(SceneObjectGroup obj, UUID regionUUID)
        {
        }

        public void RemoveObject(UUID uuid, UUID regionUUID)
        {
        }

        public void StorePrimInventory(UUID primID, ICollection<TaskInventoryItem> items)
        {
        }

        public List<SceneObjectGroup> LoadObjects(UUID regionUUID)
        {
            return new List<SceneObjectGroup>(0);
        }

        public void StoreTerrain(double[,] terrain, UUID regionID)
        {
        }

        public double[,] LoadTerrain(UUID regionID)
        {
            return new double[Constants.RegionSize, Constants.RegionSize];
        }

        public void StoreLandObject(ILandObject Parcel)
        {
        }

        public void RemoveLandObject(UUID globalID)
        {
        }

        public List<LandData> LoadLandObjects(UUID regionUUID)
        {
            return new List<LandData>(0);
        }

        public void StoreRegionSettings(RegionSettings rs)
        {
        }

        public RegionSettings LoadRegionSettings(UUID regionUUID)
        {
            return null;
        }

        public RegionLightShareData LoadRegionWindlightSettings(UUID regionUUID)
        {
            return null;
        }

        public void StoreRegionWindlightSettings(RegionLightShareData wl)
        {
        }
    }
}
