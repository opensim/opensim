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
    /*
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
        public void TestSetRequestDripRate()
        {

            TestHelpers.InMethod();

            TokenBucket tb = new TokenBucket(null, 5000f,10000f);
            AssertRates(tb, 5000, 0, 5000, 0);

            tb.RequestedDripRate = 4000f;
            AssertRates(tb, 4000, 0, 4000, 0);

            tb.RequestedDripRate = 6000;
            AssertRates(tb, 6000, 0, 6000, 0);

        }

        [Test]
        public void TestSetRequestDripRateWithMax()
        {
            TestHelpers.InMethod();

            TokenBucket tb = new TokenBucket(null, 5000,15000);
            AssertRates(tb, 5000, 0, 5000, 10000);

            tb.RequestedDripRate = 4000;
            AssertRates(tb, 4000, 0, 4000, 10000);

            tb.RequestedDripRate = 6000;
            AssertRates(tb, 6000, 0, 6000, 10000);

            tb.RequestedDripRate = 12000;
            AssertRates(tb, 10000, 0, 10000, 10000);
        }

        [Test]
        public void TestSetRequestDripRateWithChildren()
        {
            TestHelpers.InMethod();

            TokenBucket tbParent = new TokenBucket("tbParent", null, 0);
            TokenBucket tbChild1 = new TokenBucket("tbChild1", tbParent, 3000);
            TokenBucket tbChild2 = new TokenBucket("tbChild2", tbParent, 5000);

            AssertRates(tbParent, 8000, 8000, 8000, 0);
            AssertRates(tbChild1, 3000, 0, 3000, 0);
            AssertRates(tbChild2, 5000, 0, 5000, 0);

            // Test: Setting a parent request greater than total children requests.
            tbParent.RequestedDripRate = 10000;

            AssertRates(tbParent, 10000, 8000, 8000, 0);
            AssertRates(tbChild1, 3000, 0, 3000, 0);
            AssertRates(tbChild2, 5000, 0, 5000, 0);

            // Test: Setting a parent request lower than total children requests.
            tbParent.RequestedDripRate = 6000;

            AssertRates(tbParent, 6000, 8000, 6000, 0);
            AssertRates(tbChild1, 3000, 0, 6000 / 8 * 3, 0);
            AssertRates(tbChild2, 5000, 0, 6000 / 8 * 5, 0);

        }

        private void AssertRates(
            TokenBucket tb, double requestedDripRate, double totalDripRequest, double dripRate, double maxDripRate)
        {
            Assert.AreEqual((int)requestedDripRate, tb.RequestedDripRate, "Requested drip rate");
            Assert.AreEqual((int)totalDripRequest, tb.TotalDripRequest, "Total drip request");
            Assert.AreEqual((int)dripRate, tb.DripRate, "Drip rate");
            Assert.AreEqual((int)maxDripRate, tb.MaxDripRate, "Max drip rate");
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

            // We expect this to be lower because of the minimum bound set by MTU
            int totalBytes = LLUDPServer.MTU + landBytes + windBytes + cloudBytes + taskBytes + textureBytes + assetBytes;

            AssertThrottles(
                udpClient,
                LLUDPServer.MTU, landBytes, windBytes, cloudBytes, taskBytes,
                textureBytes, assetBytes, totalBytes, 0, 0);
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
            config.Set("adaptive_throttle_min_bps", 32000);

            TestLLUDPServer udpServer = ClientStackHelpers.AddUdpServer(scene, ics);

            ScenePresence sp
                = ClientStackHelpers.AddChildClient(
                    scene, udpServer, TestHelpers.ParseTail(0x1), TestHelpers.ParseTail(0x2), 123456);

            LLUDPClient udpClient = ((LLClientView)sp.ControllingClient).UDPClient;

            udpServer.Throttle.DebugLevel = 1;
            udpClient.ThrottleDebugLevel = 1;

            // Total is 275000
            int resendBytes = 5000; // this is set low to test the minimum throttle override
            int landBytes = 20000;
            int windBytes = 30000;
            int cloudBytes = 40000;
            int taskBytes = 50000;
            int textureBytes = 60000;
            int assetBytes = 70000;
            int totalBytes = resendBytes + landBytes + windBytes + cloudBytes + taskBytes + textureBytes + assetBytes;

            SetThrottles(
                udpClient, resendBytes, landBytes, windBytes, cloudBytes, taskBytes, textureBytes, assetBytes);

            // Ratio of current adaptive drip rate to requested bytes, minimum rate is 32000
            double commitRatio = 32000.0 / totalBytes;

            AssertThrottles(
                udpClient,
                LLUDPServer.MTU, landBytes * commitRatio, windBytes * commitRatio, cloudBytes * commitRatio, taskBytes * commitRatio,
                textureBytes * commitRatio, assetBytes * commitRatio, udpClient.FlowThrottle.AdjustedDripRate, totalBytes, 0);

            // Test an increase in target throttle, ack of 20 packets adds 20 * LLUDPServer.MTU bytes
            // to the throttle, recompute commitratio from those numbers
            udpClient.FlowThrottle.AcknowledgePackets(20);
            commitRatio = (32000.0 + 20.0 * LLUDPServer.MTU) / totalBytes;

            AssertThrottles(
                udpClient,
                LLUDPServer.MTU, landBytes * commitRatio, windBytes * commitRatio, cloudBytes * commitRatio, taskBytes * commitRatio,
                textureBytes * commitRatio, assetBytes * commitRatio, udpClient.FlowThrottle.AdjustedDripRate, totalBytes, 0);

            // Test a decrease in target throttle, adaptive throttle should cut the rate by 50% with a floor
            // set by the minimum adaptive rate
            udpClient.FlowThrottle.ExpirePackets(1);
            commitRatio = (32000.0 + (20.0 * LLUDPServer.MTU)/Math.Pow(2,1)) / totalBytes;

            AssertThrottles(
                udpClient,
                LLUDPServer.MTU, landBytes * commitRatio, windBytes * commitRatio, cloudBytes * commitRatio, taskBytes * commitRatio,
                textureBytes * commitRatio, assetBytes * commitRatio, udpClient.FlowThrottle.AdjustedDripRate, totalBytes, 0);
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

            AssertThrottles(
                udpClient1,
                resendBytes / 2, landBytes / 2, windBytes / 2, cloudBytes / 2, taskBytes / 2,
                textureBytes / 2, assetBytes / 2, totalBytes, 0, 0);

            // Test: Now add another client
            ScenePresence sp2
                = ClientStackHelpers.AddChildClient(
                    scene, udpServer, TestHelpers.ParseTail(0x10), TestHelpers.ParseTail(0x20), 123457);

            LLUDPClient udpClient2 = ((LLClientView)sp2.ControllingClient).UDPClient;
            //            udpClient.ThrottleDebugLevel = 1;

            SetThrottles(
                udpClient2, resendBytes, landBytes, windBytes, cloudBytes, taskBytes, textureBytes, assetBytes);

            AssertThrottles(
                udpClient1,
                resendBytes / 4, landBytes / 4, windBytes / 4, cloudBytes / 4, taskBytes / 4,
                textureBytes / 4, assetBytes / 4, totalBytes / 2, 0, 0);

            AssertThrottles(
                udpClient2,
                resendBytes / 4, landBytes / 4, windBytes / 4, cloudBytes / 4, taskBytes / 4,
                textureBytes / 4, assetBytes / 4, totalBytes / 2, 0, 0);
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

            AssertThrottles(
                udpClient,
                resendBytes / 2, landBytes / 2, windBytes / 2, cloudBytes / 2, taskBytes / 2,
                textureBytes / 2, assetBytes / 2, totalBytes, 0, totalBytes);
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

            AssertThrottles(
                udpClient1,
                resendBytes, landBytes, windBytes, cloudBytes, taskBytes,
                textureBytes, assetBytes, totalBytes, 0, totalBytes * 1.1);

            // Now add another client
            ScenePresence sp2
                = ClientStackHelpers.AddChildClient(
                    scene, udpServer, TestHelpers.ParseTail(0x10), TestHelpers.ParseTail(0x20), 123457);

            LLUDPClient udpClient2 = ((LLClientView)sp2.ControllingClient).UDPClient;
            udpClient2.ThrottleDebugLevel = 1;

            SetThrottles(
                udpClient2, resendBytes, landBytes, windBytes, cloudBytes, taskBytes, textureBytes, assetBytes);

            AssertThrottles(
                udpClient1,
                resendBytes * 0.75, landBytes * 0.75, windBytes * 0.75, cloudBytes * 0.75, taskBytes * 0.75,
                textureBytes * 0.75, assetBytes * 0.75, totalBytes * 0.75, 0, totalBytes * 1.1);

            AssertThrottles(
                udpClient2,
                resendBytes * 0.75, landBytes * 0.75, windBytes * 0.75, cloudBytes * 0.75, taskBytes * 0.75,
                textureBytes * 0.75, assetBytes * 0.75, totalBytes * 0.75, 0, totalBytes * 1.1);
        }

        private void AssertThrottles(
            LLUDPClient udpClient,
            double resendBytes, double landBytes, double windBytes, double cloudBytes, double taskBytes, double textureBytes, double assetBytes,
            double totalBytes, double targetBytes, double maxBytes)
        {
            ClientInfo ci = udpClient.GetClientInfo();

//                            Console.WriteLine(
//                                "Resend={0}, Land={1}, Wind={2}, Cloud={3}, Task={4}, Texture={5}, Asset={6}, TOTAL = {7}",
//                                ci.resendThrottle, ci.landThrottle, ci.windThrottle, ci.cloudThrottle, ci.taskThrottle, ci.textureThrottle, ci.assetThrottle, ci.totalThrottle);

            Assert.AreEqual((int)resendBytes, ci.resendThrottle, "Resend");
            Assert.AreEqual((int)landBytes, ci.landThrottle, "Land");
            Assert.AreEqual((int)windBytes, ci.windThrottle, "Wind");
            Assert.AreEqual((int)cloudBytes, ci.cloudThrottle, "Cloud");
            Assert.AreEqual((int)taskBytes, ci.taskThrottle, "Task");
            Assert.AreEqual((int)textureBytes, ci.textureThrottle, "Texture");
            Assert.AreEqual((int)assetBytes, ci.assetThrottle, "Asset");
            Assert.AreEqual((int)totalBytes, ci.totalThrottle, "Total");
            Assert.AreEqual((int)targetBytes, ci.targetThrottle, "Target");
            Assert.AreEqual((int)maxBytes, ci.maxThrottle, "Max");
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
     */
}