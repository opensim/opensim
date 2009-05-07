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
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.CoreModules.Agent.TextureSender
{
    [TestFixture]
    public class UserTextureSenderTests
    {
        public UUID uuid1;
        public UUID uuid2;
        public UUID uuid3;
        public UUID uuid4;
        public int npackets, testsize;
        public TestClient client;
        public TextureSender ts;
        public static Random random = new Random();

        [TestFixtureSetUp]
        public void Init()
        {
            AgentCircuitData agent = new AgentCircuitData();
            agent.AgentID = UUID.Random();
            agent.firstname = "testfirstname";
            agent.lastname = "testlastname";
            agent.SessionID = UUID.Zero;
            agent.SecureSessionID = UUID.Zero;
            agent.circuitcode = 123;
            agent.BaseFolder = UUID.Zero;
            agent.InventoryFolder = UUID.Zero;
            agent.startpos = Vector3.Zero;
            agent.CapsPath = "http://wibble.com";
            client = new TestClient(agent, null);
            ts = new TextureSender(client, 0, 0);
            testsize = random.Next(5000,15000);
            npackets = CalculateNumPackets(testsize);
            uuid1 = UUID.Random();
            uuid2 = UUID.Random();
            uuid3 = UUID.Random();
            uuid4 = UUID.Random();
        }

        /// <summary>
        /// Test sending package
        /// </summary>
        [Test]
        public void T010_SendPkg()
        {
            TestHelper.InMethod();
            // Normal sending
            AssetBase abase = new AssetBase(uuid1, "asset one");
            byte[] abdata = new byte[testsize];
            random.NextBytes(abdata);
            abase.Data = abdata;
            bool isdone = false;
            ts.TextureReceived(abase);
            for (int i = 0; i < npackets; i++) {
                isdone = ts.SendTexturePacket();
            }

            Assert.That(isdone,Is.False);
            isdone = ts.SendTexturePacket();
            Assert.That(isdone,Is.True);
        }

        [Test]
        public void T011_UpdateReq()
        {
            TestHelper.InMethod();
            // Test packet number start
            AssetBase abase = new AssetBase(uuid2, "asset two");
            byte[] abdata = new byte[testsize];
            random.NextBytes(abdata);
            abase.Data = abdata;

            bool isdone = false;
            ts.TextureReceived(abase);
            ts.UpdateRequest(0,3);

            for (int i = 0; i < npackets-3; i++) {
                isdone = ts.SendTexturePacket();
            }

            Assert.That(isdone,Is.False);
            isdone = ts.SendTexturePacket();
            Assert.That(isdone,Is.True);

            // Test discard level
            abase = new AssetBase(uuid3, "asset three");
            abdata = new byte[testsize];
            random.NextBytes(abdata);
            abase.Data = abdata;
            isdone = false;
            ts.TextureReceived(abase);
            ts.UpdateRequest(-1,0);

            Assert.That(ts.SendTexturePacket(),Is.True);

            abase = new AssetBase(uuid4, "asset four");
            abdata = new byte[testsize];
            random.NextBytes(abdata);
            abase.Data = abdata;
            isdone = false;
            ts.TextureReceived(abase);
            ts.UpdateRequest(0,5);

            for (int i = 0; i < npackets-5; i++) {
                isdone = ts.SendTexturePacket();
            }
            Assert.That(isdone,Is.False);
            isdone = ts.SendTexturePacket();
            Assert.That(isdone,Is.True);
        }

        [Test]
        public void T999_FinishStatus()
        {
            TestHelper.InMethod();
            // Of the 4 assets "sent", only 2 sent the first part.
            Assert.That(client.sentdatapkt.Count,Is.EqualTo(2));

            // Sum of all packets sent:
            int totalpkts = (npackets) + (npackets - 2) + (npackets - 4);
            Assert.That(client.sentpktpkt.Count,Is.EqualTo(totalpkts));
        }
        
        /// <summary>
        /// Calculate the number of packets that will be required to send the texture loaded into this sender
        /// This is actually the number of 1000 byte packets not including an initial 600 byte packet...
        /// Borrowed from TextureSender.cs
        /// </summary>
        /// <param name="length"></param>
        /// <returns></returns>
        private int CalculateNumPackets(int length)
        {
            int numPackets = 0;

            if (length > 600)
            {
                //over 600 bytes so split up file
                int restData = (length - 600);
                int restPackets = ((restData + 999) / 1000);
                numPackets = restPackets;
            }

            return numPackets;
        }
    }
}
