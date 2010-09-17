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

using NUnit.Framework;
using OpenSim.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class MundaneFrameworkTests
    {
        [Test]
        public void ChildAgentDataUpdate01()
        {
            // code coverage
            ChildAgentDataUpdate cadu = new ChildAgentDataUpdate();
            Assert.IsFalse(cadu.alwaysrun, "Default is false");
        }

        [Test]
        public void AgentPositionTest01()
        {
            UUID AgentId1 = UUID.Random();
            UUID SessionId1 = UUID.Random();
            uint CircuitCode1 = uint.MinValue;
            Vector3 Size1 = Vector3.UnitZ;
            Vector3 Position1 = Vector3.UnitX;
            Vector3 LeftAxis1 = Vector3.UnitY;
            Vector3 UpAxis1 = Vector3.UnitZ;
            Vector3 AtAxis1 = Vector3.UnitX;

            ulong RegionHandle1 = ulong.MinValue;
            byte[] Throttles1 = new byte[] {0, 1, 0};

            Vector3 Velocity1 = Vector3.Zero;
            float Far1 = 256;

            bool ChangedGrid1 = false;
            Vector3 Center1 = Vector3.Zero;

            AgentPosition position1 = new AgentPosition();
            position1.AgentID = AgentId1;
            position1.SessionID = SessionId1;
            position1.CircuitCode = CircuitCode1;
            position1.Size = Size1;
            position1.Position = Position1;
            position1.LeftAxis = LeftAxis1;
            position1.UpAxis = UpAxis1;
            position1.AtAxis = AtAxis1;
            position1.RegionHandle = RegionHandle1;
            position1.Throttles = Throttles1;
            position1.Velocity = Velocity1;
            position1.Far = Far1;
            position1.ChangedGrid = ChangedGrid1;
            position1.Center = Center1;

            ChildAgentDataUpdate cadu = new ChildAgentDataUpdate();
            cadu.AgentID = AgentId1.Guid;
            cadu.ActiveGroupID = UUID.Zero.Guid;
            cadu.throttles = Throttles1;
            cadu.drawdistance = Far1;
            cadu.Position = Position1;
            cadu.Velocity = Velocity1;
            cadu.regionHandle = RegionHandle1;
            cadu.cameraPosition = Center1;
            cadu.AVHeight = Size1.Z;

            AgentPosition position2 = new AgentPosition();
            position2.CopyFrom(cadu);

            Assert.IsTrue(
                position2.AgentID == position1.AgentID
                && position2.Size == position1.Size
                && position2.Position == position1.Position
                && position2.Velocity == position1.Velocity
                && position2.Center == position1.Center
                && position2.RegionHandle == position1.RegionHandle
                && position2.Far == position1.Far
               
                ,"Copy From ChildAgentDataUpdate failed");

            position2 = new AgentPosition();

            Assert.IsFalse(position2.AgentID == position1.AgentID, "Test Error, position2 should be a blank uninitialized AgentPosition");
            position2.Unpack(position1.Pack());

            Assert.IsTrue(position2.AgentID == position1.AgentID, "Agent ID didn't unpack the same way it packed");
            Assert.IsTrue(position2.Position == position1.Position, "Position didn't unpack the same way it packed");
            Assert.IsTrue(position2.Velocity == position1.Velocity, "Velocity didn't unpack the same way it packed");
            Assert.IsTrue(position2.SessionID == position1.SessionID, "SessionID didn't unpack the same way it packed");
            Assert.IsTrue(position2.CircuitCode == position1.CircuitCode, "CircuitCode didn't unpack the same way it packed");
            Assert.IsTrue(position2.LeftAxis == position1.LeftAxis, "LeftAxis didn't unpack the same way it packed");
            Assert.IsTrue(position2.UpAxis == position1.UpAxis, "UpAxis didn't unpack the same way it packed");
            Assert.IsTrue(position2.AtAxis == position1.AtAxis, "AtAxis didn't unpack the same way it packed");
            Assert.IsTrue(position2.RegionHandle == position1.RegionHandle, "RegionHandle didn't unpack the same way it packed");
            Assert.IsTrue(position2.ChangedGrid == position1.ChangedGrid, "ChangedGrid didn't unpack the same way it packed");
            Assert.IsTrue(position2.Center == position1.Center, "Center didn't unpack the same way it packed");
            Assert.IsTrue(position2.Size == position1.Size, "Size didn't unpack the same way it packed");

        }
        
        
    }
}

