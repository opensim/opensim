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
    public class UserAccountsClient
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
            UserAccountServicesConnector m_Connector = new UserAccountServicesConnector(serverURI);

            UUID user1 = UUID.Random();
            string first = "Completely";
            string last = "Clueless";
            string email = "foo@bar.com";

            //UserAccount account = new UserAccount(user1);
            //account.ScopeID = UUID.Zero;
            //account.FirstName = first;
            //account.LastName = last;
            //account.Email = email;
            //account.ServiceURLs = new Dictionary<string, object>();
            //account.ServiceURLs.Add("InventoryServerURI", "http://cnn.com");
            //account.ServiceURLs.Add("AssetServerURI", "http://cnn.com");

            //bool success = m_Connector.StoreUserAccount(account);
            //if (success)
            //    m_log.InfoFormat("[USER CLIENT]: Successfully created account for user {0} {1}", account.FirstName, account.LastName);
            //else
            //    m_log.InfoFormat("[USER CLIENT]: failed to create user {0} {1}", account.FirstName, account.LastName);

            //System.Console.WriteLine("\n");

            //account = m_Connector.GetUserAccount(UUID.Zero, user1);
            //if (account == null)
            //    m_log.InfoFormat("[USER CLIENT]: Unable to retrieve accouny by UUID for {0}", user1);
            //else
            //{
            //    m_log.InfoFormat("[USER CLIENT]: Account retrieved correctly: userID={0}; FirstName={1}; LastName={2}; Email={3}",
            //                      account.PrincipalID, account.FirstName, account.LastName, account.Email);
            //    foreach (KeyValuePair<string, object> kvp in account.ServiceURLs)
            //        m_log.DebugFormat("\t {0} -> {1}", kvp.Key, kvp.Value);
            //}

            //System.Console.WriteLine("\n");

            UserAccount account = m_Connector.GetUserAccount(UUID.Zero, first, last);
            if (account == null)
                m_log.InfoFormat("[USER CLIENT]: Unable to retrieve accouny by name ");
            else
            {
                m_log.InfoFormat("[USER CLIENT]: Account retrieved correctly: userID={0}; FirstName={1}; LastName={2}; Email={3}",
                                  account.PrincipalID, account.FirstName, account.LastName, account.Email);
                foreach (KeyValuePair<string, object> kvp in account.ServiceURLs)
                    m_log.DebugFormat("\t {0} -> {1}", kvp.Key, kvp.Value);
            }

            System.Console.WriteLine("\n");
            account = m_Connector.GetUserAccount(UUID.Zero, email);
            if (account == null)
                m_log.InfoFormat("[USER CLIENT]: Unable to retrieve accouny by email");
            else
            {
                m_log.InfoFormat("[USER CLIENT]: Account retrieved correctly: userID={0}; FirstName={1}; LastName={2}; Email={3}",
                                  account.PrincipalID, account.FirstName, account.LastName, account.Email);
                foreach (KeyValuePair<string, object> kvp in account.ServiceURLs)
                    m_log.DebugFormat("\t {0} -> {1}", kvp.Key, kvp.Value);
            }

            System.Console.WriteLine("\n");
            account = m_Connector.GetUserAccount(UUID.Zero, user1);
            if (account == null)
                m_log.InfoFormat("[USER CLIENT]: Unable to retrieve accouny by UUID for {0}", user1);
            else
            {
                m_log.InfoFormat("[USER CLIENT]: Account retrieved correctly: userID={0}; FirstName={1}; LastName={2}; Email={3}",
                                  account.PrincipalID, account.FirstName, account.LastName, account.Email);
                foreach (KeyValuePair<string, object> kvp in account.ServiceURLs)
                    m_log.DebugFormat("\t {0} -> {1}", kvp.Key, kvp.Value);
            }

            System.Console.WriteLine("\n");
            account = m_Connector.GetUserAccount(UUID.Zero, "DoesNot", "Exist");
            if (account == null)
                m_log.InfoFormat("[USER CLIENT]: Unable to retrieve account 'DoesNot Exist'");
            else
            {
                m_log.InfoFormat("[USER CLIENT]: Account 'DoesNot Exist' retrieved correctly. REALLY??? userID={0}; FirstName={1}; LastName={2}; Email={3}",
                                  account.PrincipalID, account.FirstName, account.LastName, account.Email);
                foreach (KeyValuePair<string, object> kvp in account.ServiceURLs)
                    m_log.DebugFormat("\t {0} -> {1}", kvp.Key, kvp.Value);
            }
        }

    }
}
