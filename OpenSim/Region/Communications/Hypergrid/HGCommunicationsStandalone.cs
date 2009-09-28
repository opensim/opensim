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
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Communications.Local;
using OpenSim.Region.Communications.OGS1;

namespace OpenSim.Region.Communications.Hypergrid
{
    public class HGCommunicationsStandalone : CommunicationsManager
    {
        public HGCommunicationsStandalone(
            ConfigSettings configSettings,                          
            NetworkServersInfo serversInfo,
            BaseHttpServer httpServer,
            LibraryRootFolder libraryRootFolder, 
            bool dumpAssetsToFile)
            : base(serversInfo, libraryRootFolder)
        {           
            LocalUserServices localUserService =
                new LocalUserServices(
                    serversInfo.DefaultHomeLocX, serversInfo.DefaultHomeLocY, this);
            localUserService.AddPlugin(configSettings.StandaloneUserPlugin, configSettings.StandaloneUserSource); 
            
            HGUserServices hgUserService = new HGUserServices(this, localUserService);
            // This plugin arrangement could eventually be configurable rather than hardcoded here.
            hgUserService.AddPlugin(new TemporaryUserProfilePlugin());
            hgUserService.AddPlugin(new HGUserDataPlugin(this, hgUserService));
            
            m_userService = hgUserService;            
            m_userAdminService = hgUserService;            
            m_avatarService = hgUserService;
            m_messageService = hgUserService;
            
        }
    }
}
