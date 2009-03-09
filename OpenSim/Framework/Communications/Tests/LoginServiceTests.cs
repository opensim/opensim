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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using Nwc.XmlRpc;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Communications.Local;
using OpenSim.Tests.Common.Mock;
using OpenSim.Client.Linden;

namespace OpenSim.Framework.Communications.Tests
{
    /// <summary>
    /// Test the login service.  For now, most of this will be done through the LocalLoginService as LoginService
    /// is abstract
    /// </summary>
    [TestFixture]
    public class LoginServiceTests
    {
        /// <summary>
        /// Test the normal response to a login.  Does not test authentication.
        /// </summary>
        [Test]
        public void T010_NormalLoginResponse()
        {
            //log4net.Config.XmlConfigurator.Configure();

            string firstName = "Timmy";
            string lastName = "Mallet";
            string regionExternalName = "localhost";
            IPEndPoint capsEndPoint = new IPEndPoint(IPAddress.Loopback, 9123);

            CommunicationsManager commsManager
                = new TestCommunicationsManager(new NetworkServersInfo(42, 43));
           
            //commsManager.GridService.RegisterRegion(
            //    new RegionInfo(42, 43, capsEndPoint, regionExternalName));
            //commsManager.GridService.RegionLoginsEnabled = true;

            //LoginService loginService
            //    = new LocalLoginService(
            //        (UserManagerBase)commsManager.UserService, "Hello folks", commsManager.InterServiceInventoryService,
            //        (LocalBackEndServices)commsManager.GridService,
            //        commsManager.NetworkServersInfo, false, new LibraryRootFolder(String.Empty));

            TestLoginToRegionConnector regionConnector = new TestLoginToRegionConnector();
            regionConnector.AddRegion(new RegionInfo(42, 43, capsEndPoint, regionExternalName));

            LoginService loginService = new LLStandaloneLoginService((UserManagerBase)commsManager.UserService, "Hello folks", commsManager.InterServiceInventoryService,
                 commsManager.NetworkServersInfo, false, new LibraryRootFolder(String.Empty), regionConnector);

            Hashtable loginParams = new Hashtable();
            loginParams["first"] = firstName;
            loginParams["last"] = lastName;
            loginParams["passwd"] = "boingboing";

            ArrayList sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            XmlRpcRequest request = new XmlRpcRequest("login_to_simulator", sendParams);

            XmlRpcResponse response = loginService.XmlRpcLoginMethod(request);
            Hashtable responseData = (Hashtable)response.Value;

            Assert.That(responseData["first_name"], Is.EqualTo(firstName));
            Assert.That(responseData["last_name"], Is.EqualTo(lastName));
            Assert.That(
                responseData["circuit_code"], Is.GreaterThanOrEqualTo(0) & Is.LessThanOrEqualTo(Int32.MaxValue));

            Regex capsSeedPattern
                = new Regex("^http://"
                    + regionExternalName
                    + ":9000/CAPS/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{8}0000/$");

            Assert.That(capsSeedPattern.IsMatch((string)responseData["seed_capability"]), Is.True);
        }

        [Test]
        public void T011_Auth_Login()
        {
            string firstName = "Adam";
            string lastName = "West";
            string regionExternalName = "localhost";
            IPEndPoint capsEndPoint = new IPEndPoint(IPAddress.Loopback, 9123);

            CommunicationsManager commsManager
                = new TestCommunicationsManager(new NetworkServersInfo(42, 43));

            LocalUserServices lus = (LocalUserServices)commsManager.UserService;

            lus.AddUser(firstName,lastName,"boingboing","abc@ftw.com",42,43);

            //commsManager.GridService.RegisterRegion(
            //   new RegionInfo(42, 43, capsEndPoint, regionExternalName));
            //commsManager.GridService.RegionLoginsEnabled = true;

            //LoginService loginService
            //    = new LocalLoginService(
            //        (UserManagerBase)lus, "Hello folks", commsManager.InterServiceInventoryService,
            //        (LocalBackEndServices)commsManager.GridService,
            //        commsManager.NetworkServersInfo, true, new LibraryRootFolder(String.Empty));

            TestLoginToRegionConnector regionConnector = new TestLoginToRegionConnector();
            regionConnector.AddRegion(new RegionInfo(42, 43, capsEndPoint, regionExternalName));

            LoginService loginService = new LLStandaloneLoginService((UserManagerBase) lus, "Hello folks", commsManager.InterServiceInventoryService,
                  commsManager.NetworkServersInfo, true, new LibraryRootFolder(String.Empty), regionConnector);

            // TODO: Not check inventory part of response yet.
            // TODO: Not checking all of login response thoroughly yet.

            // 1) Test for positive authentication

            Hashtable loginParams = new Hashtable();
            loginParams["first"] = firstName;
            loginParams["last"] = lastName;
            loginParams["passwd"] = "boingboing";

            ArrayList sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            XmlRpcRequest request = new XmlRpcRequest("login_to_simulator", sendParams);

            XmlRpcResponse response = loginService.XmlRpcLoginMethod(request);
            Hashtable responseData = (Hashtable)response.Value;

            UserProfileData uprof = lus.GetUserProfile(firstName,lastName);

            UserAgentData uagent = uprof.CurrentAgent;
            Assert.That(uagent,Is.Not.Null);

            Assert.That(responseData["first_name"], Is.Not.Null);
            Assert.That(responseData["first_name"], Is.EqualTo(firstName));
            Assert.That(responseData["last_name"], Is.EqualTo(lastName));
            Assert.That(responseData["agent_id"], Is.EqualTo(uagent.ProfileID.ToString()));
            Assert.That(responseData["session_id"], Is.EqualTo(uagent.SessionID.ToString()));
            Assert.That(responseData["secure_session_id"], Is.EqualTo(uagent.SecureSessionID.ToString()));
            ArrayList invlibroot = (ArrayList) responseData["inventory-lib-root"];
            Hashtable invlibroothash = (Hashtable) invlibroot[0];
            Assert.That(invlibroothash["folder_id"],Is.EqualTo("00000112-000f-0000-0000-000100bba000"));
            Assert.That(
                responseData["circuit_code"], Is.GreaterThanOrEqualTo(0) & Is.LessThanOrEqualTo(Int32.MaxValue));
            Assert.That(responseData["message"], Is.EqualTo("Hello folks"));
            Assert.That(responseData["buddy-list"], Is.Empty);
            Assert.That(responseData["start_location"], Is.EqualTo("last"));

            Regex capsSeedPattern
                = new Regex("^http://"
                    + regionExternalName
                    + ":9000/CAPS/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{8}0000/$");

            Assert.That(capsSeedPattern.IsMatch((string)responseData["seed_capability"]), Is.True);

            // 1.1) Test for budddies!
            lus.AddUser("Friend","Number1","boingboing","abc@ftw.com",42,43);
            lus.AddUser("Friend","Number2","boingboing","abc@ftw.com",42,43);

            UserProfileData friend1 = lus.GetUserProfile("Friend","Number1");
            UserProfileData friend2 = lus.GetUserProfile("Friend","Number2");
            lus.AddNewUserFriend(friend1.ID,uprof.ID,1);
            lus.AddNewUserFriend(friend1.ID,friend2.ID,2);

            loginParams = new Hashtable();
            loginParams["first"] = "Friend";
            loginParams["last"] = "Number1";
            loginParams["passwd"] = "boingboing";

            sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            request = new XmlRpcRequest("login_to_simulator", sendParams);

            response = loginService.XmlRpcLoginMethod(request);
            responseData = (Hashtable)response.Value;

            ArrayList friendslist = (ArrayList) responseData["buddy-list"];

            Assert.That(friendslist,Is.Not.Null);


            Hashtable buddy1 = (Hashtable) friendslist[0];
            Hashtable buddy2 = (Hashtable) friendslist[1];
            Assert.That(friendslist.Count, Is.EqualTo(2));
            Assert.That(uprof.ID.ToString(), Is.EqualTo(buddy1["buddy_id"]) | Is.EqualTo(buddy2["buddy_id"]));
            Assert.That(friend2.ID.ToString(), Is.EqualTo(buddy1["buddy_id"]) | Is.EqualTo(buddy2["buddy_id"]));

            // 2) Test for negative authentication
            //
            string error_auth_message = "Could not authenticate your avatar. Please check your username and password, and check the grid if problems persist.";
            string error_xml_message = "Error connecting to grid. Could not percieve credentials from login XML.";
            string error_already_logged = "You appear to be already logged in. " +
                                         "If this is not the case please wait for your session to timeout. " +
                                         "If this takes longer than a few minutes please contact the grid owner. " +
                                         "Please wait 5 minutes if you are going to connect to a region nearby to the region you were at previously.";
            //string error_region_unavailable = "The region you are attempting to log into is not responding. Please select another region and try again.";
            // 2.1) Test for wrong user name
            loginParams = new Hashtable();
            loginParams["first"] = lastName;
            loginParams["last"] = firstName;
            loginParams["passwd"] = "boingboing";

            sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            request = new XmlRpcRequest("login_to_simulator", sendParams);

            response = loginService.XmlRpcLoginMethod(request);
            responseData = (Hashtable)response.Value;
            Assert.That(responseData["message"], Is.EqualTo(error_auth_message));

            // 2.2) Test for wrong password
            loginParams = new Hashtable();
            loginParams["first"] = "Friend";
            loginParams["last"] = "Number2";
            loginParams["passwd"] = "boing";

            sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            request = new XmlRpcRequest("login_to_simulator", sendParams);

            response = loginService.XmlRpcLoginMethod(request);
            responseData = (Hashtable)response.Value;
            Assert.That(responseData["message"], Is.EqualTo(error_auth_message));

            // 2.3) Bad XML
            loginParams = new Hashtable();
            loginParams["first"] = "Friend";
            loginParams["banana"] = "Banana";
            loginParams["passwd"] = "boingboing";
 
            sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            request = new XmlRpcRequest("login_to_simulator", sendParams);

            response = loginService.XmlRpcLoginMethod(request);
            responseData = (Hashtable)response.Value;
            Assert.That(responseData["message"], Is.EqualTo(error_xml_message));
            
            // 2.4) Already logged in and sucessfull post login
            loginParams = new Hashtable();
            loginParams["first"] = "Adam";
            loginParams["last"] = "West";
            loginParams["passwd"] = "boingboing";

            sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            request = new XmlRpcRequest("login_to_simulator", sendParams);

            response = loginService.XmlRpcLoginMethod(request);
            responseData = (Hashtable)response.Value;
            Assert.That(responseData["message"], Is.EqualTo(error_already_logged));

            request = new XmlRpcRequest("login_to_simulator", sendParams);

            response = loginService.XmlRpcLoginMethod(request);
            responseData = (Hashtable)response.Value;
            Assert.That(responseData["message"], Is.EqualTo("Hello folks"));
        }

        public class TestLoginToRegionConnector : ILoginServiceToRegionsConnector
        {

            private List<RegionInfo> m_regionsList = new List<RegionInfo>();

            public void AddRegion(RegionInfo regionInfo)
            {
                lock (m_regionsList)
                {
                    if (!m_regionsList.Contains(regionInfo))
                    {
                        m_regionsList.Add(regionInfo);
                    }
                }
            }

            #region ILoginRegionsConnector Members
            public bool RegionLoginsEnabled
            {
                get { return true; }
            }

            public void LogOffUserFromGrid(ulong regionHandle, OpenMetaverse.UUID AvatarID, OpenMetaverse.UUID RegionSecret, string message)
            {
            }

            public bool NewUserConnection(ulong regionHandle, AgentCircuitData agent)
            {
                lock (m_regionsList)
                {
                    foreach (RegionInfo regInfo in m_regionsList)
                    {
                        if (regInfo.RegionHandle == regionHandle)
                            return true;
                    }
                }
                return false;
            }

            public RegionInfo RequestClosestRegion(string region)
            {
                lock (m_regionsList)
                {
                    foreach (RegionInfo regInfo in m_regionsList)
                    {
                        if (regInfo.RegionName == region)
                            return regInfo;
                    }
                }

                return null;
            }

            public RegionInfo RequestNeighbourInfo(OpenMetaverse.UUID regionID)
            {
                lock (m_regionsList)
                {
                    foreach (RegionInfo regInfo in m_regionsList)
                    {
                        if (regInfo.RegionID == regionID)
                            return regInfo;
                    }
                }

                return null;
            }

            public RegionInfo RequestNeighbourInfo(ulong regionHandle)
            {
                lock (m_regionsList)
                {
                    foreach (RegionInfo regInfo in m_regionsList)
                    {
                        if (regInfo.RegionHandle == regionHandle)
                            return regInfo;
                    }
                }

                return null;
            }

            #endregion
        }
    }
}
