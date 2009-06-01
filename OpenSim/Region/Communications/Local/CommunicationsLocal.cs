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

using System.Collections.Generic;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Osp;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Region.Communications.Local
{
    public class CommunicationsLocal : CommunicationsManager
    {
        public CommunicationsLocal(
            ConfigSettings configSettings,                                   
            NetworkServersInfo serversInfo,
            BaseHttpServer httpServer,
            IAssetCache assetCache,
            LibraryRootFolder libraryRootFolder, 
            bool dumpAssetsToFile)
            : base(serversInfo, httpServer, assetCache, dumpAssetsToFile, libraryRootFolder)
        {
            LocalInventoryService inventoryService = new LocalInventoryService();
            List<IInventoryDataPlugin> plugins 
                = DataPluginFactory.LoadDataPlugins<IInventoryDataPlugin>(
                    configSettings.StandaloneInventoryPlugin, 
                    configSettings.StandaloneInventorySource);

            foreach (IInventoryDataPlugin plugin in plugins)
            {
                // Using the OSP wrapper plugin for database plugins should be made configurable at some point
                inventoryService.AddPlugin(new OspInventoryWrapperPlugin(plugin, this));
            }
            
            AddInventoryService(inventoryService);
            m_defaultInventoryHost = inventoryService.Host;
            m_interServiceInventoryService = inventoryService;
                        
            LocalUserServices lus 
                = new LocalUserServices(
                    serversInfo.DefaultHomeLocX, serversInfo.DefaultHomeLocY, this);
            lus.AddPlugin(new TemporaryUserProfilePlugin());
            lus.AddPlugin(configSettings.StandaloneUserPlugin, configSettings.StandaloneUserSource);            
            m_userService = lus;
            m_userAdminService = lus;            
            m_avatarService = lus;
            m_messageService = lus;

            m_gridService = new LocalBackEndServices();

            //LocalLoginService loginService = CreateLoginService(libraryRootFolder, inventoryService, userService, backendService);                      
        }
    }
}
