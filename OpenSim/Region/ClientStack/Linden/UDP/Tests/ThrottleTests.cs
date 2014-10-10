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
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.ClientStack.LindenUDP.Tests
{
    [TestFixture]
    public class ThrottleTests : OpenSimTestCase
    {
        [TestFixtureSetUp]
        public void FixtureInit()
        {
            // Don't allow tests to be bamboozled by asynchronous events.  Execute everything on the same thread.
            Util.FireAndForgetMethod = FireAndForgetMethod.RegressionTest;
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            // We must set this back afterwards, otherwise later tests will fail since they're expecting multiple
            // threads.  Possibly, later tests should be rewritten so none of them require async stuff (which regression
            // tests really shouldn't).
            Util.FireAndForgetMethod = Util.DefaultFireAndForgetMethod;
        }

        [Test]
        public void TestClientThrottleSetNoLimit()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            Scene scene = new SceneHelpers().SetupScene();
            TestLLUDPServer udpServer = ClientStackHelpers.AddUdpServer(scene);

            ScenePresence sp 
                = ClientStackHelpers.AddChildClient(
                    scene, udpServer, TestHelpers.ParseTail(0x1), TestHelpers.ParseTail(0x2), 123456);

            LLUDPClient udpClient = ((LLClientView)sp.ControllingClient).UDPClient;

            udpServer.Throttle.DebugLevel = 1;
            udpClient.ThrottleDebugLevel = 1;

            int resendBytes = 1000;
            int landBytes = 2000;
            int windBytes = 3000;
            int cloudBytes = 4000;
            int taskBytes = 5000;
            int textureBytes = 6000;
            int assetBytes = 7000;

            SetThrottles(
                udpClient, resendBytes, landBytes, windBytes, cloudBytes, taskBytes, textureBytes, assetBytes);

            ClientInfo ci = udpClient.GetClientInfo();

            // We expect this to be lower because of the minimum bound set by MTU
            int totalBytes = LLUDPServer.MTU + landBytes + windBytes + cloudBytes + taskBytes + textureBytes + assetBytes;
            Assert.AreEqual(LLUDPServer.MTU, ci.resendThrottle);
            Assert.AreEqual(landBytes, ci.landThrottle);
            Assert.AreEqual(windBytes, ci.windThrottle);
            Assert.AreEqual(cloudBytes, ci.cloudThrottle);
            Assert.AreEqual(taskBytes, ci.taskThrottle);
            Assert.AreEqual(textureBytes, ci.textureThrottle);
            Assert.AreEqual(assetBytes, ci.assetThrottle);
            Assert.AreEqual(totalBytes, ci.totalThrottle);

            Assert.AreEqual(0, ci.maxThrottle);
        }

        [Test]
        public void TestClientThrottleAdaptiveNoLimit()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            Scene scene = new SceneHelpers().SetupScene();

            IniConfigSource ics = new IniConfigSource();
            IConfig config = ics.AddConfig("ClientStack.LindenUDP");
            config.Set("enable_adaptive_throttles", true);
            TestLLUDPServer udpServer = ClientStackHelpers.AddUdpServer(scene, ics);

            ScenePresence sp 
                = ClientStackHelpers.AddChildClient(
                    scene, udpServer, TestHelpers.ParseTail(0x1), TestHelpers.ParseTail(0x2), 123456);

            LLUDPClient udpClient = ((LLClientView)sp.ControllingClient).UDPClient;

            udpServer.Throttle.DebugLevel = 1;
            udpClient.ThrottleDebugLevel = 1;

            // Total is 28000
            int resendBytes = 10000;
            int landBytes = 20000;
            int windBytes = 30000;
            int cloudBytes = 40000;
            int taskBytes = 50000;
            int textureBytes = 60000;
            int assetBytes = 70000;

            SetThrottles(
                udpClient, resendBytes, landBytes, windBytes, cloudBytes, taskBytes, textureBytes, assetBytes);

            ClientInfo ci = udpClient.GetClientInfo();

            // We expect individual throttle changes to currently have no effect under adaptive, since this is managed
            // purely by that throttle.  However, we expect the max to change.
            // XXX: At the moment we check against defaults, but at some point there should be a better test to 
            // active see change over time.
            ThrottleRates defaultRates = udpServer.ThrottleRates;

            // Current total is 66750
            int totalBytes = defaultRates.Resend + defaultRates.Land + defaultRates.Wind + defaultRates.Cloud + defaultRates.Task + defaultRates.Texture + defaultRates.Asset;
            int totalMaxBytes = resendBytes + landBytes + windBytes + cloudBytes + taskBytes + textureBytes + assetBytes;

            Assert.AreEqual(0, ci.maxThrottle);
            Assert.AreEqual(totalMaxBytes, ci.targetThrottle);
            Assert.AreEqual(defaultRates.Resend, ci.resendThrottle);
            Assert.AreEqual(defaultRates.Land, ci.landThrottle);
            Assert.AreEqual(defaultRates.Wind, ci.windThrottle);
            Assert.AreEqual(defaultRates.Cloud, ci.cloudThrottle);
            Assert.AreEqual(defaultRates.Task, ci.taskThrottle);
            Assert.AreEqual(defaultRates.Texture, ci.textureThrottle);
            Assert.AreEqual(defaultRates.Asset, ci.assetThrottle);
            Assert.AreEqual(totalBytes, ci.totalThrottle);
        }

        /// <summary>
        /// Test throttle setttings where max client throttle has been limited server side.
        /// </summary>
        [Test]
        public void TestSingleClientThrottleRegionLimited()
        {
            TestHelpers.InMethod();
            //            TestHelpers.EnableLogging();

            int resendBytes = 6000;
            int landBytes = 8000;
            int windBytes = 10000;
            int cloudBytes = 12000;
            int taskBytes = 14000;
            int textureBytes = 16000;
            int assetBytes = 18000;
            int totalBytes 
                = (int)((resendBytes + landBytes + windBytes + cloudBytes + taskBytes + textureBytes + assetBytes) / 2);

            Scene scene = new SceneHelpers().SetupScene();
            TestLLUDPServer udpServer = ClientStackHelpers.AddUdpServer(scene);
            udpServer.Throttle.RequestedDripRate = totalBytes;

            ScenePresence sp1 
                = ClientStackHelpers.AddChildClient(
                    scene, udpServer, TestHelpers.ParseTail(0x1), TestHelpers.ParseTail(0x2), 123456);

            LLUDPClient udpClient1 = ((LLClientView)sp1.ControllingClient).UDPClient;

            SetThrottles(
                udpClient1, resendBytes, landBytes, windBytes, cloudBytes, taskBytes, textureBytes, assetBytes);

            {
                ClientInfo ci = udpClient1.GetClientInfo();

                //            Console.WriteLine(
                //                "Resend={0}, Land={1}, Wind={2}, Cloud={3}, Task={4}, Texture={5}, Asset={6}, TOTAL = {7}", 
                //                ci.resendThrottle, ci.landThrottle, ci.windThrottle, ci.cloudThrottle, ci.taskThrottle, ci.textureThrottle, ci.assetThrottle, ci.totalThrottle);

                Assert.AreEqual(resendBytes / 2, ci.resendThrottle);
                Assert.AreEqual(landBytes / 2, ci.landThrottle);
                Assert.AreEqual(windBytes / 2, ci.windThrottle);
                Assert.AreEqual(cloudBytes / 2, ci.cloudThrottle);
                Assert.AreEqual(taskBytes / 2, ci.taskThrottle);
                Assert.AreEqual(textureBytes / 2, ci.textureThrottle);
                Assert.AreEqual(assetBytes / 2, ci.assetThrottle);
                Assert.AreEqual(totalBytes, ci.totalThrottle);
            }

            // Now add another client
            ScenePresence sp2
                = ClientStackHelpers.AddChildClient(
                    scene, udpServer, TestHelpers.ParseTail(0x10), TestHelpers.ParseTail(0x20), 123457);

            LLUDPClient udpClient2 = ((LLClientView)sp2.ControllingClient).UDPClient;
            //            udpClient.ThrottleDebugLevel = 1;

            SetThrottles(
                udpClient2, resendBytes, landBytes, windBytes, cloudBytes, taskBytes, textureBytes, assetBytes);

            {
                ClientInfo ci = udpClient1.GetClientInfo();

//                            Console.WriteLine(
//                                "Resend={0}, Land={1}, Wind={2}, Cloud={3}, Task={4}, Texture={5}, Asset={6}, TOTAL = {7}", 
//                                ci.resendThrottle, ci.landThrottle, ci.windThrottle, ci.cloudThrottle, ci.taskThrottle, ci.textureThrottle, ci.assetThrottle, ci.totalThrottle);

                Assert.AreEqual(resendBytes / 4, ci.resendThrottle);
                Assert.AreEqual(landBytes / 4, ci.landThrottle);
                Assert.AreEqual(windBytes / 4, ci.windThrottle);
                Assert.AreEqual(cloudBytes / 4, ci.cloudThrottle);
                Assert.AreEqual(taskBytes / 4, ci.taskThrottle);
                Assert.AreEqual(textureBytes / 4, ci.textureThrottle);
                Assert.AreEqual(assetBytes / 4, ci.assetThrottle);
                Assert.AreEqual(totalBytes / 2, ci.totalThrottle);
            }

            {
                ClientInfo ci = udpClient2.GetClientInfo();

                //            Console.WriteLine(
                //                "Resend={0}, Land={1}, Wind={2}, Cloud={3}, Task={4}, Texture={5}, Asset={6}, TOTAL = {7}", 
                //                ci.resendThrottle, ci.landThrottle, ci.windThrottle, ci.cloudThrottle, ci.taskThrottle, ci.textureThrottle, ci.assetThrottle, ci.totalThrottle);

                Assert.AreEqual(resendBytes / 4, ci.resendThrottle);
                Assert.AreEqual(landBytes / 4, ci.landThrottle);
                Assert.AreEqual(windBytes / 4, ci.windThrottle);
                Assert.AreEqual(cloudBytes / 4, ci.cloudThrottle);
                Assert.AreEqual(taskBytes / 4, ci.taskThrottle);
                Assert.AreEqual(textureBytes / 4, ci.textureThrottle);
                Assert.AreEqual(assetBytes / 4, ci.assetThrottle);
                Assert.AreEqual(totalBytes / 2, ci.totalThrottle);
            }
        }

        /// <summary>
        /// Test throttle setttings where max client throttle has been limited server side.
        /// </summary>
        [Test]
        public void TestClientThrottlePerClientLimited()
        {
            TestHelpers.InMethod();
            //            TestHelpers.EnableLogging();

            int resendBytes = 4000;
            int landBytes = 6000;
            int windBytes = 8000;
            int cloudBytes = 10000;
            int taskBytes = 12000;
            int textureBytes = 14000;
            int assetBytes = 16000;
            int totalBytes 
                = (int)((resendBytes + landBytes + windBytes + cloudBytes + taskBytes + textureBytes + assetBytes) / 2);

            Scene scene = new SceneHelpers().SetupScene();
            TestLLUDPServer udpServer = ClientStackHelpers.AddUdpServer(scene);
            udpServer.ThrottleRates.Total = totalBytes;

            ScenePresence sp 
                = ClientStackHelpers.AddChildClient(
                    scene, udpServer, TestHelpers.ParseTail(0x1), TestHelpers.ParseTail(0x2), 123456);

            LLUDPClient udpClient = ((LLClientView)sp.ControllingClient).UDPClient;
            //            udpClient.ThrottleDebugLevel = 1;

            SetThrottles(
                udpClient, resendBytes, landBytes, windBytes, cloudBytes, taskBytes, textureBytes, assetBytes);

            ClientInfo ci = udpClient.GetClientInfo();

//            Console.WriteLine(
//                "Resend={0}, Land={1}, Wind={2}, Cloud={3}, Task={4}, Texture={5}, Asset={6}, TOTAL = {7}", 
//                ci.resendThrottle, ci.landThrottle, ci.windThrottle, ci.cloudThrottle, ci.taskThrottle, ci.textureThrottle, ci.assetThrottle, ci.totalThrottle);

            Assert.AreEqual(resendBytes / 2, ci.resendThrottle);
            Assert.AreEqual(landBytes / 2, ci.landThrottle);
            Assert.AreEqual(windBytes / 2, ci.windThrottle);
            Assert.AreEqual(cloudBytes / 2, ci.cloudThrottle);
            Assert.AreEqual(taskBytes / 2, ci.taskThrottle);
            Assert.AreEqual(textureBytes / 2, ci.textureThrottle);
            Assert.AreEqual(assetBytes / 2, ci.assetThrottle);
            Assert.AreEqual(totalBytes, ci.totalThrottle);
        }

        [Test]
        public void TestClientThrottlePerClientAndRegionLimited()
        {
            TestHelpers.InMethod();
            //TestHelpers.EnableLogging();

            int resendBytes = 4000;
            int landBytes = 6000;
            int windBytes = 8000;
            int cloudBytes = 10000;
            int taskBytes = 12000;
            int textureBytes = 14000;
            int assetBytes = 16000;

            // current total 70000
            int totalBytes = resendBytes + landBytes + windBytes + cloudBytes + taskBytes + textureBytes + assetBytes;

            Scene scene = new SceneHelpers().SetupScene();
            TestLLUDPServer udpServer = ClientStackHelpers.AddUdpServer(scene);
            udpServer.ThrottleRates.Total = (int)(totalBytes * 1.1);
            udpServer.Throttle.RequestedDripRate = (int)(totalBytes * 1.5);

            ScenePresence sp1 
                = ClientStackHelpers.AddChildClient(
                    scene, udpServer, TestHelpers.ParseTail(0x1), TestHelpers.ParseTail(0x2), 123456);

            LLUDPClient udpClient1 = ((LLClientView)sp1.ControllingClient).UDPClient;
            udpClient1.ThrottleDebugLevel = 1;

            SetThrottles(
                udpClient1, resendBytes, landBytes, windBytes, cloudBytes, taskBytes, textureBytes, assetBytes);

            {
                ClientInfo ci = udpClient1.GetClientInfo();
               
                            //                            Console.WriteLine(
                            //                                "Resend={0}, Land={1}, Wind={2}, Cloud={3}, Task={4}, Texture={5}, Asset={6}, TOTAL = {7}", 
                            //                                ci.resendThrottle, ci.landThrottle, ci.windThrottle, ci.cloudThrottle, ci.taskThrottle, ci.textureThrottle, ci.assetThrottle, ci.totalThrottle);

                Assert.AreEqual(resendBytes, ci.resendThrottle);
                Assert.AreEqual(landBytes, ci.landThrottle);
                Assert.AreEqual(windBytes, ci.windThrottle);
                Assert.AreEqual(cloudBytes, ci.cloudThrottle);
                Assert.AreEqual(taskBytes, ci.taskThrottle);
                Assert.AreEqual(textureBytes, ci.textureThrottle);
                Assert.AreEqual(assetBytes, ci.assetThrottle);
                Assert.AreEqual(totalBytes, ci.totalThrottle);
            }

            // Now add another client
            ScenePresence sp2
                = ClientStackHelpers.AddChildClient(
                    scene, udpServer, TestHelpers.ParseTail(0x10), TestHelpers.ParseTail(0x20), 123457);

            LLUDPClient udpClient2 = ((LLClientView)sp2.ControllingClient).UDPClient;
            udpClient2.ThrottleDebugLevel = 1;

            SetThrottles(
                udpClient2, resendBytes, landBytes, windBytes, cloudBytes, taskBytes, textureBytes, assetBytes);

            {
                ClientInfo ci = udpClient1.GetClientInfo();
            
//                Console.WriteLine(
//                    "Resend={0}, Land={1}, Wind={2}, Cloud={3}, Task={4}, Texture={5}, Asset={6}, TOTAL = {7}", 
//                    ci.resendThrottle, ci.landThrottle, ci.windThrottle, ci.cloudThrottle, ci.taskThrottle, ci.textureThrottle, ci.assetThrottle, ci.totalThrottle);
            
                Assert.AreEqual(resendBytes * 0.75, ci.resendThrottle);
                Assert.AreEqual(landBytes * 0.75, ci.landThrottle);
                Assert.AreEqual(windBytes * 0.75, ci.windThrottle);
                Assert.AreEqual(cloudBytes * 0.75, ci.cloudThrottle);
                Assert.AreEqual(taskBytes * 0.75, ci.taskThrottle);
                Assert.AreEqual(textureBytes * 0.75, ci.textureThrottle);
                Assert.AreEqual(assetBytes * 0.75, ci.assetThrottle);
                Assert.AreEqual(totalBytes * 0.75, ci.totalThrottle);
            }

            {
                ClientInfo ci = udpClient2.GetClientInfo();

//                Console.WriteLine(
//                    "Resend={0}, Land={1}, Wind={2}, Cloud={3}, Task={4}, Texture={5}, Asset={6}, TOTAL = {7}", 
//                    ci.resendThrottle, ci.landThrottle, ci.windThrottle, ci.cloudThrottle, ci.taskThrottle, ci.textureThrottle, ci.assetThrottle, ci.totalThrottle);

                Assert.AreEqual(resendBytes * 0.75, ci.resendThrottle);
                Assert.AreEqual(landBytes * 0.75, ci.landThrottle);
                Assert.AreEqual(windBytes * 0.75, ci.windThrottle);
                Assert.AreEqual(cloudBytes * 0.75, ci.cloudThrottle);
                Assert.AreEqual(taskBytes * 0.75, ci.taskThrottle);
                Assert.AreEqual(textureBytes * 0.75, ci.textureThrottle);
                Assert.AreEqual(assetBytes * 0.75, ci.assetThrottle);
                Assert.AreEqual(totalBytes * 0.75, ci.totalThrottle);
            }
        }

        private void SetThrottles(
            LLUDPClient udpClient, int resendBytes, int landBytes, int windBytes, int cloudBytes, int taskBytes, int textureBytes, int assetBytes)
        {
            byte[] throttles = new byte[28];

            Array.Copy(BitConverter.GetBytes((float)resendBytes * 8), 0, throttles, 0, 4);
            Array.Copy(BitConverter.GetBytes((float)landBytes * 8), 0, throttles, 4, 4);
            Array.Copy(BitConverter.GetBytes((float)windBytes * 8), 0, throttles, 8, 4);
            Array.Copy(BitConverter.GetBytes((float)cloudBytes * 8), 0, throttles, 12, 4);
            Array.Copy(BitConverter.GetBytes((float)taskBytes * 8), 0, throttles, 16, 4);
            Array.Copy(BitConverter.GetBytes((float)textureBytes * 8), 0, throttles, 20, 4);
            Array.Copy(BitConverter.GetBytes((float)assetBytes * 8), 0, throttles, 24, 4);

            udpClient.SetThrottles(throttles);
        }
    }
}