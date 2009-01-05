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
using System.Net;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using Nwc.XmlRpc;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Communications.Local;
using OpenSim.Tests.Common.Mock;

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
        /// Test the normal response to a login.  Does not test authentication.  Doesn't yet do what it says on the tin.
        /// </summary>        
        [Test]
        public void TestNormalLoginResponse()
        {
            //log4net.Config.XmlConfigurator.Configure();
            
            string firstName = "Timmy";
            string lastName = "Mallet";

            CommunicationsManager commsManager 
                = new TestCommunicationsManager(new OpenSim.Framework.NetworkServersInfo(42, 43));
            
            commsManager.GridService.RegisterRegion(
                new RegionInfo(42, 43, new IPEndPoint(IPAddress.Loopback, 9000), "localhost"));
            commsManager.GridService.RegionLoginsEnabled = true;
            
            LoginService loginService 
                = new LocalLoginService(
                    (UserManagerBase)commsManager.UserService, "Hello folks", commsManager.InterServiceInventoryService, 
                    (LocalBackEndServices)commsManager.GridService, 
                    commsManager.NetworkServersInfo, false, new LibraryRootFolder(String.Empty));
            
            Hashtable loginParams = new Hashtable();
            loginParams["first"] = firstName;
            loginParams["last"] = lastName;
            loginParams["passwd"] = "boingboing";

            ArrayList sendParams = new ArrayList();
            sendParams.Add(loginParams);

            XmlRpcRequest request = new XmlRpcRequest("login_to_simulator", sendParams);

            XmlRpcResponse response = loginService.XmlRpcLoginMethod(request);
            Hashtable responseData = (Hashtable)response.Value;
            
            // TODO: Not check inventory part of response yet.
            // TODO: Not checking all of login response thoroughly yet.
            
            Assert.That(
                responseData["circuit_code"], Is.GreaterThanOrEqualTo(0) & Is.LessThanOrEqualTo(System.Int32.MaxValue));
            Assert.That(responseData["first_name"], Is.EqualTo(firstName));
            Assert.That(responseData["last_name"], Is.EqualTo(lastName));            
        }
    }
}
