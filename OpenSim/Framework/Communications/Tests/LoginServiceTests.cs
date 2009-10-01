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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using Nwc.XmlRpc;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Services;
using OpenSim.Region.Communications.Local;
using OpenSim.Tests.Common.Setup;
using OpenSim.Tests.Common.Mock;
using OpenSim.Client.Linden;
using OpenSim.Tests.Common;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Framework.Communications.Tests
{
    /// <summary>
    /// Test the login service.  For now, most of this will be done through the LocalLoginService as LoginService
    /// is abstract
    /// </summary>

    [TestFixture]
    public class LoginServiceTests
    {
        private string m_firstName = "Adam";
        private string m_lastName = "West";
        private string m_regionExternalName = "localhost";

        private IPEndPoint m_capsEndPoint;
        private TestCommunicationsManager m_commsManager;
        private TestLoginToRegionConnector m_regionConnector;
        private LocalUserServices m_localUserServices;
        private LoginService m_loginService;
        private UserProfileData m_userProfileData;
        private TestScene m_testScene;

        [SetUp]
        public void SetUpLoginEnviroment()
        {
            m_capsEndPoint = new IPEndPoint(IPAddress.Loopback, 9123);
            m_commsManager = new TestCommunicationsManager(new NetworkServersInfo(42, 43));
            m_regionConnector = new TestLoginToRegionConnector();
            m_testScene = SceneSetupHelpers.SetupScene(m_commsManager, "");

            m_regionConnector.AddRegion(new RegionInfo(42, 43, m_capsEndPoint, m_regionExternalName));

            //IInventoryService m_inventoryService = new TestInventoryService();

            m_localUserServices = (LocalUserServices) m_commsManager.UserService;
            m_localUserServices.AddUser(m_firstName,m_lastName,"boingboing","abc@ftw.com",42,43);

            m_loginService = new LLStandaloneLoginService((UserManagerBase) m_localUserServices, "Hello folks", m_testScene.InventoryService,
                  m_commsManager.NetworkServersInfo, true, new LibraryRootFolder(String.Empty), m_regionConnector);

            m_userProfileData = m_localUserServices.GetUserProfile(m_firstName, m_lastName);
        }

        /// <summary>
        /// Test the normal response to a login.  Does not test authentication.
        /// </summary>
        [Test]
        public void T010_TestUnauthenticatedLogin()
        {
            TestHelper.InMethod();
            // We want to use our own LoginService for this test, one that
            // doesn't require authentication.
            new LLStandaloneLoginService((UserManagerBase)m_commsManager.UserService, "Hello folks", new TestInventoryService(),
                m_commsManager.NetworkServersInfo, false, new LibraryRootFolder(String.Empty), m_regionConnector);

            Hashtable loginParams = new Hashtable();
            loginParams["first"] = m_firstName;
            loginParams["last"] = m_lastName;
            loginParams["passwd"] = "boingboing";

            ArrayList sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(m_capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            XmlRpcRequest request = new XmlRpcRequest("login_to_simulator", sendParams);

            IPAddress tmpLocal = Util.GetLocalHost();
            IPEndPoint tmpEnd = new IPEndPoint(tmpLocal, 80);
            XmlRpcResponse response = m_loginService.XmlRpcLoginMethod(request, tmpEnd);

            Hashtable responseData = (Hashtable)response.Value;

            Assert.That(responseData["first_name"], Is.EqualTo(m_firstName));
            Assert.That(responseData["last_name"], Is.EqualTo(m_lastName));
            Assert.That(
                responseData["circuit_code"], Is.GreaterThanOrEqualTo(0) & Is.LessThanOrEqualTo(Int32.MaxValue));

            Regex capsSeedPattern
                = new Regex("^http://"
                    + NetworkUtil.GetHostFor(tmpLocal, m_regionExternalName)
                    + ":9000/CAPS/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}0000/$");

            Assert.That(capsSeedPattern.IsMatch((string)responseData["seed_capability"]), Is.True);
        }

        [Test]
        public void T011_TestAuthenticatedLoginSuccess()
        {
            TestHelper.InMethod();
            // TODO: Not check inventory part of response yet.
            // TODO: Not checking all of login response thoroughly yet.

            // 1) Test for positive authentication

            Hashtable loginParams = new Hashtable();
            loginParams["first"] = m_firstName;
            loginParams["last"] = m_lastName;
            loginParams["passwd"] = "boingboing";

            ArrayList sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(m_capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            XmlRpcRequest request = new XmlRpcRequest("login_to_simulator", sendParams);

            IPAddress tmpLocal = Util.GetLocalHost();
            IPEndPoint tmpEnd = new IPEndPoint(tmpLocal, 80);
            XmlRpcResponse response = m_loginService.XmlRpcLoginMethod(request, tmpEnd);

            Hashtable responseData = (Hashtable)response.Value;

            UserAgentData uagent = m_userProfileData.CurrentAgent;
            Assert.That(uagent,Is.Not.Null);

            Assert.That(responseData["first_name"], Is.Not.Null);
            Assert.That(responseData["first_name"], Is.EqualTo(m_firstName));
            Assert.That(responseData["last_name"], Is.EqualTo(m_lastName));
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
                    + NetworkUtil.GetHostFor(tmpLocal, m_regionExternalName)
                    + ":9000/CAPS/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}0000/$");

            Assert.That(capsSeedPattern.IsMatch((string)responseData["seed_capability"]), Is.True);
        }

        [Test]
        public void T012_TestAuthenticatedLoginForBuddies()
        {
            TestHelper.InMethod();
            // 1.1) Test for budddies!
            m_localUserServices.AddUser("Friend","Number1","boingboing","abc@ftw.com",42,43);
            m_localUserServices.AddUser("Friend","Number2","boingboing","abc@ftw.com",42,43);

            UserProfileData friend1 = m_localUserServices.GetUserProfile("Friend","Number1");
            UserProfileData friend2 = m_localUserServices.GetUserProfile("Friend","Number2");
            m_localUserServices.AddNewUserFriend(friend1.ID,m_userProfileData.ID,1);
            m_localUserServices.AddNewUserFriend(friend1.ID,friend2.ID,2);

            Hashtable loginParams = new Hashtable();
            loginParams["first"] = "Friend";
            loginParams["last"] = "Number1";
            loginParams["passwd"] = "boingboing";

            ArrayList sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(m_capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            XmlRpcRequest request = new XmlRpcRequest("login_to_simulator", sendParams);

            IPAddress tmpLocal = Util.GetLocalHost();
            IPEndPoint tmpEnd = new IPEndPoint(tmpLocal, 80);
            XmlRpcResponse response = m_loginService.XmlRpcLoginMethod(request, tmpEnd);

            Hashtable responseData = (Hashtable)response.Value;

            ArrayList friendslist = (ArrayList) responseData["buddy-list"];

            Assert.That(friendslist,Is.Not.Null);

            Hashtable buddy1 = (Hashtable) friendslist[0];
            Hashtable buddy2 = (Hashtable) friendslist[1];
            Assert.That(friendslist.Count, Is.EqualTo(2));
            Assert.That(m_userProfileData.ID.ToString(), Is.EqualTo(buddy1["buddy_id"]) | Is.EqualTo(buddy2["buddy_id"]));
            Assert.That(friend2.ID.ToString(), Is.EqualTo(buddy1["buddy_id"]) | Is.EqualTo(buddy2["buddy_id"]));
        }

        [Test]
        public void T020_TestAuthenticatedLoginBadUsername()
        {
            TestHelper.InMethod();

            // 2) Test for negative authentication
            //
            string error_auth_message = "Could not authenticate your avatar. Please check your username and password, and check the grid if problems persist.";
            //string error_region_unavailable = "The region you are attempting to log into is not responding. Please select another region and try again.";
            // 2.1) Test for wrong user name
            Hashtable loginParams = new Hashtable();
            loginParams["first"] = m_lastName;
            loginParams["last"] = m_firstName;
            loginParams["passwd"] = "boingboing";

            ArrayList sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(m_capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            XmlRpcRequest request = new XmlRpcRequest("login_to_simulator", sendParams);

            IPAddress tmpLocal = Util.GetLocalHost();
            IPEndPoint tmpEnd = new IPEndPoint(tmpLocal, 80);
            XmlRpcResponse response = m_loginService.XmlRpcLoginMethod(request, tmpEnd);

            Hashtable responseData = (Hashtable)response.Value;
            Assert.That(responseData["message"], Is.EqualTo(error_auth_message));

        }

        [Test]
        public void T021_TestAuthenticatedLoginBadPassword()
        {
            TestHelper.InMethod();

            string error_auth_message = "Could not authenticate your avatar. Please check your username and password, and check the grid if problems persist.";
            // 2.2) Test for wrong password
            Hashtable loginParams = new Hashtable();
            loginParams["first"] = "Friend";
            loginParams["last"] = "Number2";
            loginParams["passwd"] = "boing";

            ArrayList sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(m_capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            XmlRpcRequest request = new XmlRpcRequest("login_to_simulator", sendParams);

            IPAddress tmpLocal = Util.GetLocalHost();
            IPEndPoint tmpEnd = new IPEndPoint(tmpLocal, 80);
            XmlRpcResponse response = m_loginService.XmlRpcLoginMethod(request, tmpEnd);

            Hashtable responseData = (Hashtable)response.Value;
            Assert.That(responseData["message"], Is.EqualTo(error_auth_message));

        }

        [Test]
        public void T022_TestAuthenticatedLoginBadXml()
        {
            TestHelper.InMethod();

            string error_xml_message = "Error connecting to grid. Could not percieve credentials from login XML.";
            // 2.3) Bad XML
            Hashtable loginParams = new Hashtable();
            loginParams["first"] = "Friend";
            loginParams["banana"] = "Banana";
            loginParams["passwd"] = "boingboing";

            ArrayList sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(m_capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            XmlRpcRequest request = new XmlRpcRequest("login_to_simulator", sendParams);

            IPAddress tmpLocal = Util.GetLocalHost();
            IPEndPoint tmpEnd = new IPEndPoint(tmpLocal, 80);
            XmlRpcResponse response = m_loginService.XmlRpcLoginMethod(request, tmpEnd);

            Hashtable responseData = (Hashtable)response.Value;
            Assert.That(responseData["message"], Is.EqualTo(error_xml_message));

        }

        // [Test]
        // Commenting out test now that LLStandAloneLoginService no longer replies with message in this case.
        // Kept the code for future test with grid mode, which will keep this behavior.
        public void T023_TestAuthenticatedLoginAlreadyLoggedIn()
        {
            TestHelper.InMethod();

            //Console.WriteLine("Starting T023_TestAuthenticatedLoginAlreadyLoggedIn()");
            //log4net.Config.XmlConfigurator.Configure();
            
            string error_already_logged = "You appear to be already logged in. " +
                                         "If this is not the case please wait for your session to timeout. " +
                                         "If this takes longer than a few minutes please contact the grid owner. " +
                                         "Please wait 5 minutes if you are going to connect to a region nearby to the region you were at previously.";
            // 2.4) Already logged in and sucessfull post login
            Hashtable loginParams = new Hashtable();
            loginParams["first"] = "Adam";
            loginParams["last"] = "West";
            loginParams["passwd"] = "boingboing";

            ArrayList sendParams = new ArrayList();
            sendParams.Add(loginParams);
            sendParams.Add(m_capsEndPoint); // is this parameter correct?
            sendParams.Add(new Uri("http://localhost:8002/")); // is this parameter correct?

            // First we log in.
            XmlRpcRequest request = new XmlRpcRequest("login_to_simulator", sendParams);

            IPAddress tmpLocal = Util.GetLocalHost();
            IPEndPoint tmpEnd = new IPEndPoint(tmpLocal, 80);
            XmlRpcResponse response = m_loginService.XmlRpcLoginMethod(request, tmpEnd);

            Hashtable responseData = (Hashtable)response.Value;
            Assert.That(responseData["message"], Is.EqualTo("Hello folks"));

            // Then we try again, this time expecting failure.
            request = new XmlRpcRequest("login_to_simulator", sendParams);
            response = m_loginService.XmlRpcLoginMethod(request, tmpEnd);
            responseData = (Hashtable)response.Value;
            Assert.That(responseData["message"], Is.EqualTo(error_already_logged));

            // Finally the third time we should be able to get right back in.
            request = new XmlRpcRequest("login_to_simulator", sendParams);

            response = m_loginService.XmlRpcLoginMethod(request, tmpEnd);
            responseData = (Hashtable)response.Value;
            Assert.That(responseData["message"], Is.EqualTo("Hello folks"));
            
            //Console.WriteLine("Finished T023_TestAuthenticatedLoginAlreadyLoggedIn()");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (MainServer.Instance != null) MainServer.Instance.Stop();
            } catch (NullReferenceException)
            {}
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

            public bool NewUserConnection(ulong regionHandle, AgentCircuitData agent, out string reason)
            {
                reason = String.Empty;
                lock (m_regionsList)
                {
                    foreach (RegionInfo regInfo in m_regionsList)
                    {
                        if (regInfo.RegionHandle == regionHandle)
                            return true;
                    }
                }
                reason = "Region not found";
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

    class TestInventoryService : IInventoryService
    {
        public TestInventoryService()
        {
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInterServiceInventoryServices"/>
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public bool CreateUserInventory(UUID userId)
        {
            return false;
        }

        /// <summary>
        /// <see cref="OpenSim.Framework.Communications.IInterServiceInventoryServices"/>
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public List<InventoryFolderBase> GetInventorySkeleton(UUID userId)
        {
            List<InventoryFolderBase> folders = new List<InventoryFolderBase>();
            InventoryFolderBase folder = new InventoryFolderBase();
            folder.ID = UUID.Random();
            folder.Owner = userId;
            folders.Add(folder);
            return folders;
        }

        /// <summary>
        /// Returns a list of all the active gestures in a user's inventory.
        /// </summary>
        /// <param name="userId">
        /// The <see cref="UUID"/> of the user
        /// </param>
        /// <returns>
        /// A flat list of the gesture items.
        /// </returns>
        public List<InventoryItemBase> GetActiveGestures(UUID userId)
        {
            return null;
        }

        public InventoryCollection GetUserInventory(UUID userID)
        {
            return null;
        }

        public void GetUserInventory(UUID userID, OpenSim.Services.Interfaces.InventoryReceiptCallback callback)
        {
        }

        public InventoryFolderBase GetFolderForType(UUID userID, AssetType type)
        {
            return null;
        }

        public InventoryCollection GetFolderContent(UUID userID, UUID folderID)
        {
            return null;
        }

        public List<InventoryItemBase> GetFolderItems(UUID userID, UUID folderID)
        {
            return null;
        }

        public bool AddFolder(InventoryFolderBase folder)
        {
            return false;
        }

        public bool UpdateFolder(InventoryFolderBase folder)
        {
            return false;
        }

        public bool MoveFolder(InventoryFolderBase folder)
        {
            return false;
        }

        public bool DeleteFolders(UUID ownerID, List<UUID> ids)
        {
            return false;
        }

        public bool PurgeFolder(InventoryFolderBase folder)
        {
            return false;
        }

        public bool AddItem(InventoryItemBase item)
        {
            return false;
        }

        public bool UpdateItem(InventoryItemBase item)
        {
            return false;
        }

        public bool MoveItems(UUID owner, List<InventoryItemBase> items)
        {
            return false;
        }

        public bool DeleteItems(UUID owner, List<UUID> items)
        {
            return false;
        }

        public InventoryItemBase GetItem(InventoryItemBase item)
        {
            return null;
        }

        public InventoryFolderBase GetFolder(InventoryFolderBase folder)
        {
            return null;
        }

        public bool HasInventoryForUser(UUID userID)
        {
            return false;
        }

        public InventoryFolderBase GetRootFolder(UUID userID)
        {
            InventoryFolderBase root = new InventoryFolderBase();
            root.ID = UUID.Random();
            root.Owner = userID;
            root.ParentID = UUID.Zero;
            return root;
        }

        public int GetAssetPermissions(UUID userID, UUID assetID)
        {
            return 1;
        }
    }
}
