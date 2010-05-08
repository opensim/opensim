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
using System.Text;
using System.Reflection;

using OpenMetaverse;
using log4net;
using log4net.Appender;
using log4net.Layout;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors;

namespace OpenSim.Tests.Clients.PresenceClient
{
    public class PresenceClient
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);
        
        public static void Main(string[] args)
        {
            ConsoleAppender consoleAppender = new ConsoleAppender();
            consoleAppender.Layout =
                new PatternLayout("%date [%thread] %-5level %logger [%property{NDC}] - %message%newline");
            log4net.Config.BasicConfigurator.Configure(consoleAppender);

            string serverURI = "http://127.0.0.1:8003";
            PresenceServicesConnector m_Connector = new PresenceServicesConnector(serverURI);

            UUID user1 = UUID.Random();
            UUID session1 = UUID.Random();
            UUID region1 = UUID.Random();

            bool success = m_Connector.LoginAgent(user1.ToString(), session1, UUID.Zero);
            if (success)
                m_log.InfoFormat("[PRESENCE CLIENT]: Successfully logged in user {0} with session {1}", user1, session1);
            else
                m_log.InfoFormat("[PRESENCE CLIENT]: failed to login user {0}", user1);

            System.Console.WriteLine("\n");

            PresenceInfo pinfo = m_Connector.GetAgent(session1);
            if (pinfo == null)
                m_log.InfoFormat("[PRESENCE CLIENT]: Unable to retrieve presence for {0}", user1);
            else
                m_log.InfoFormat("[PRESENCE CLIENT]: Presence retrieved correctly: userID={0}; regionID={1}", 
                    pinfo.UserID, pinfo.RegionID);

            System.Console.WriteLine("\n");
            success = m_Connector.ReportAgent(session1, region1);
            if (success)
                m_log.InfoFormat("[PRESENCE CLIENT]: Successfully reported session {0} in region {1}", user1, region1);
            else
                m_log.InfoFormat("[PRESENCE CLIENT]: failed to report session {0}", session1);
            pinfo = m_Connector.GetAgent(session1);
            if (pinfo == null)
                m_log.InfoFormat("[PRESENCE CLIENT]: Unable to retrieve presence for {0} for second time", user1);
            else
                m_log.InfoFormat("[PRESENCE CLIENT]: Presence retrieved correctly: userID={0}; regionID={2}",
                    pinfo.UserID, pinfo.RegionID);

            System.Console.WriteLine("\n");
            success = m_Connector.LogoutAgent(session1);
            if (success)
                m_log.InfoFormat("[PRESENCE CLIENT]: Successfully logged out user {0}", user1);
            else
                m_log.InfoFormat("[PRESENCE CLIENT]: failed to logout user {0}", user1);
            pinfo = m_Connector.GetAgent(session1);
            if (pinfo == null)
                m_log.InfoFormat("[PRESENCE CLIENT]: Unable to retrieve presence for {0} for fourth time", user1);
            else
                m_log.InfoFormat("[PRESENCE CLIENT]: Presence retrieved correctly: userID={0}; regionID={1}",
                    pinfo.UserID, pinfo.RegionID);

            System.Console.WriteLine("\n");
            success = m_Connector.ReportAgent(session1, UUID.Random());
            if (success)
                m_log.InfoFormat("[PRESENCE CLIENT]: Report agent succeeded, but this is wrong");
            else
                m_log.InfoFormat("[PRESENCE CLIENT]: failed to report agent, as it should because user is not logged in");

        }

    }
}
