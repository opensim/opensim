/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Net;
using OpenSim.Assets;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Types;
using OpenSim.Physics.Manager;
using OpenSim.Region.Caches;
using OpenSim.Region.Environment;
using libsecondlife;

namespace OpenSim.Region.ClientStack
{
    public abstract class RegionApplicationBase
    {
        protected AssetCache m_assetCache;
        protected InventoryCache m_inventoryCache;
        protected Dictionary<EndPoint, uint> m_clientCircuits = new Dictionary<EndPoint, uint>();
        protected DateTime m_startuptime;
        protected NetworkServersInfo m_networkServersInfo;

        protected List<UDPServer> m_udpServer = new List<UDPServer>();
        protected List<RegionInfo> m_regionData = new List<RegionInfo>();
        protected List<IWorld> m_localWorld = new List<IWorld>();
        protected BaseHttpServer m_httpServer;
        protected List<AuthenticateSessionsBase> AuthenticateSessionsHandler = new List<AuthenticateSessionsBase>();

        protected LogBase m_log;

        public RegionApplicationBase( )
        {
            m_startuptime = DateTime.Now;          
        }

        virtual public void StartUp()
        {
            ClientView.TerrainManager = new TerrainManager(new SecondLife());
            m_networkServersInfo = new NetworkServersInfo();
            RegionInfo m_regionInfo = new RegionInfo();

            Initialize();

            StartLog();

        }

        protected abstract void Initialize();

        private void StartLog()
        {
            LogBase logBase = CreateLog();
            m_log = logBase;
            MainLog.Instance = m_log;
        }

        protected abstract LogBase CreateLog();
        protected abstract PhysicsScene GetPhysicsScene( );

        protected PhysicsScene GetPhysicsScene(string engine)
        {
            PhysicsPluginManager physicsPluginManager;
            physicsPluginManager = new PhysicsPluginManager();
            physicsPluginManager.LoadPlugins();
            return physicsPluginManager.GetPhysicsScene( engine );
        }

    }
}
