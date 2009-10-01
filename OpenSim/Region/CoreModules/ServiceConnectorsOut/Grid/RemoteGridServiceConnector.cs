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

using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;
using Nini.Config;
using OpenMetaverse;

using OpenSim.Framework;
using OpenSim.Services.Connectors;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Grid
{
    public class RemoteGridServicesConnector :
            GridServicesConnector, ISharedRegionModule, IGridService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private bool m_Enabled = false;

        private IGridService m_LocalGridService;

        public RemoteGridServicesConnector()
        {
        }

        public RemoteGridServicesConnector(IConfigSource source)
        {
            InitialiseServices(source);
        }

        #region ISharedRegionmodule

        public Type ReplaceableInterface 
        {
            get { return null; }
        }

        public string Name
        {
            get { return "RemoteGridServicesConnector"; }
        }

        public override void Initialise(IConfigSource source)
        {
            IConfig moduleConfig = source.Configs["Modules"];
            if (moduleConfig != null)
            {
                string name = moduleConfig.GetString("GridServices", "");
                if (name == Name)
                {
                    InitialiseServices(source);
                    m_Enabled = true;
                    m_log.Info("[REMOTE GRID CONNECTOR]: Remote grid enabled");
                }
            }
        }

        private void InitialiseServices(IConfigSource source)
        {
            IConfig gridConfig = source.Configs["GridService"];
            if (gridConfig == null)
            {
                m_log.Error("[REMOTE GRID CONNECTOR]: GridService missing from OpenSim.ini");
                return;
            }

            base.Initialise(source);

            m_LocalGridService = new LocalGridServicesConnector(source);
        }

        public void PostInitialise()
        {
            if (m_LocalGridService != null)
                ((ISharedRegionModule)m_LocalGridService).PostInitialise();
        }

        public void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
            if (m_Enabled)
                scene.RegisterModuleInterface<IGridService>(this);

            if (m_LocalGridService != null)
                ((ISharedRegionModule)m_LocalGridService).AddRegion(scene);
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_LocalGridService != null)
                ((ISharedRegionModule)m_LocalGridService).RemoveRegion(scene);
        }

        public void RegionLoaded(Scene scene)
        {
        }

        #endregion

        #region IGridService

        public override bool RegisterRegion(UUID scopeID, GridRegion regionInfo)
        {
            if (m_LocalGridService.RegisterRegion(scopeID, regionInfo))
                return base.RegisterRegion(scopeID, regionInfo);

            return false;
        }

        public override bool DeregisterRegion(UUID regionID)
        {
            if (m_LocalGridService.DeregisterRegion(regionID))
                return base.DeregisterRegion(regionID);

            return false;
        }

        // Let's override GetNeighbours completely -- never go to the grid server
        // Neighbours are/should be cached locally
        // For retrieval from the DB, caller should call GetRegionByPosition
        public override List<GridRegion> GetNeighbours(UUID scopeID, UUID regionID)
        {
            return m_LocalGridService.GetNeighbours(scopeID, regionID);
        }

        public override GridRegion GetRegionByUUID(UUID scopeID, UUID regionID)
        {
            GridRegion rinfo = m_LocalGridService.GetRegionByUUID(scopeID, regionID);
            if (rinfo == null)
                rinfo = base.GetRegionByUUID(scopeID, regionID);

            return rinfo;
        }

        public override GridRegion GetRegionByPosition(UUID scopeID, int x, int y)
        {
            GridRegion rinfo = m_LocalGridService.GetRegionByPosition(scopeID, x, y);
            if (rinfo == null)
                rinfo = base.GetRegionByPosition(scopeID, x, y);

            return rinfo;
        }

        public override GridRegion GetRegionByName(UUID scopeID, string regionName)
        {
            GridRegion rinfo = m_LocalGridService.GetRegionByName(scopeID, regionName);
            if (rinfo == null)
                rinfo = base.GetRegionByName(scopeID, regionName);

            return rinfo;
        }

        // Let's not override GetRegionsByName -- let's get them all from the grid server
        // Let's not override GetRegionRange -- let's get them all from the grid server

        #endregion
    }
}
