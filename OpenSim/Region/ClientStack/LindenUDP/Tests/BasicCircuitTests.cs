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

using System.Net;
using log4net;
using NUnit.Framework;
using OpenMetaverse.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.ClientStack;
using OpenSim.Region.ClientStack.LindenUDP;

namespace OpenSim.Region.ClientStack.LindenUDP.Tests
{
    /// <summary>
    /// This will contain basic tests for the LindenUDP client stack
    /// </summary>
    [TestFixture]
    public class BasicCircuitTests
    {
        [Test]
        public void TestAddClient()
        {
            try
            {
                log4net.Config.XmlConfigurator.Configure();
            }
            catch
            {
                // I don't care, just leave log4net off
            }
            
            TestLLUDPServer testLLUDPServer = new TestLLUDPServer();            
            ClientStackUserSettings userSettings = new ClientStackUserSettings();
            
            uint port = 666;            
            testLLUDPServer.Initialise(null, ref port, 0, false, userSettings, null, null);
            LLPacketServer packetServer = new LLPacketServer(testLLUDPServer, userSettings);
            testLLUDPServer.LocalScene = new MockScene();
            
            UseCircuitCodePacket uccp = new UseCircuitCodePacket();
            UseCircuitCodePacket.CircuitCodeBlock uccpCcBlock 
                = new OpenMetaverse.Packets.UseCircuitCodePacket.CircuitCodeBlock();
            uccpCcBlock.Code = 123456;
            uccp.CircuitCode = uccpCcBlock;
            
            EndPoint testEp = new IPEndPoint(IPAddress.Loopback, 999);
            
            testLLUDPServer.LoadReceive(uccp, testEp);            
            testLLUDPServer.ReceiveData(null);        
            
            //Assert.IsTrue(testLLUDPServer.HasCircuit(123456));
        }
    }
}
