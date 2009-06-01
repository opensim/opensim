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
using System.IO;
using System.Reflection;
using log4net;
using log4net.Config;
using OpenMetaverse;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Framework.Statistics;
using OpenSim.Grid.Communications.OGS1;
using OpenSim.Grid.Framework;
using OpenSim.Grid.UserServer.Modules;

namespace OpenSim.Grid.UserServer
{
    //Do we actually need these event dispatchers? 
    //shouldn't the other modules just directly register event handlers to each other?
    public class UserServerEventDispatchModule
    {
        protected UserManager m_userManager;
        protected MessageServersConnector m_messagesService;
        protected UserLoginService m_loginService;

        public UserServerEventDispatchModule(UserManager userManager, MessageServersConnector messagesService, UserLoginService loginService)
        {
            m_userManager = userManager;
            m_messagesService = messagesService;
            m_loginService = loginService;
        }

        public void Initialise(IGridServiceCore core)
        {
        }

        public void PostInitialise()
        {
            m_loginService.OnUserLoggedInAtLocation += NotifyMessageServersUserLoggedInToLocation;
            m_userManager.OnLogOffUser += NotifyMessageServersUserLoggOff;

            m_messagesService.OnAgentLocation += HandleAgentLocation;
            m_messagesService.OnAgentLeaving += HandleAgentLeaving;
            m_messagesService.OnRegionStartup += HandleRegionStartup;
            m_messagesService.OnRegionShutdown += HandleRegionShutdown;
        }

        public void RegisterHandlers(BaseHttpServer httpServer)
        {

        }

        public void Close()
        {
            m_loginService.OnUserLoggedInAtLocation -= NotifyMessageServersUserLoggedInToLocation;
        }

        #region Event Handlers
        public void NotifyMessageServersUserLoggOff(UUID agentID)
        {
            m_messagesService.TellMessageServersAboutUserLogoff(agentID);
        }

        public void NotifyMessageServersUserLoggedInToLocation(UUID agentID, UUID sessionID, UUID RegionID,
                                                               ulong regionhandle, float positionX, float positionY,
                                                               float positionZ, string firstname, string lastname)
        {
            m_messagesService.TellMessageServersAboutUser(agentID, sessionID, RegionID, regionhandle, positionX,
                                                          positionY, positionZ, firstname, lastname);
        }

        public void HandleAgentLocation(UUID agentID, UUID regionID, ulong regionHandle)
        {
            m_userManager.HandleAgentLocation(agentID, regionID, regionHandle);
        }

        public void HandleAgentLeaving(UUID agentID, UUID regionID, ulong regionHandle)
        {
            m_userManager.HandleAgentLeaving(agentID, regionID, regionHandle);
        }

        public void HandleRegionStartup(UUID regionID)
        {
            // This might seem strange, that we send this back to the
            // server it came from. But there is method to the madness.
            // There can be multiple user servers on the same database,
            // and each can have multiple messaging servers. So, we send
            // it to all known user servers, who send it to all known
            // message servers. That way, we should be able to finally
            // update presence to all regions and thereby all friends
            //
            m_userManager.HandleRegionStartup(regionID);
            m_messagesService.TellMessageServersAboutRegionShutdown(regionID);
        }

        public void HandleRegionShutdown(UUID regionID)
        {
            // This might seem strange, that we send this back to the
            // server it came from. But there is method to the madness.
            // There can be multiple user servers on the same database,
            // and each can have multiple messaging servers. So, we send
            // it to all known user servers, who send it to all known
            // message servers. That way, we should be able to finally
            // update presence to all regions and thereby all friends
            //
            m_userManager.HandleRegionShutdown(regionID);
            m_messagesService.TellMessageServersAboutRegionShutdown(regionID);
        }
        #endregion
    }
}
