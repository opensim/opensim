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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Timers;
using Timer=System.Timers.Timer;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.CoreModules.World.Serialiser;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Simulation;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    /// Scene presence tests
    /// </summary>
    [TestFixture]
    public class SceneTests
    {
        /// <summary>
        /// Very basic scene update test.  Should become more elaborate with time.
        /// </summary>
        [Test]
        public void TestUpdateScene()
        {
            TestHelpers.InMethod();

            Scene scene = new SceneHelpers().SetupScene();
            scene.Update(1);
            
            Assert.That(scene.Frame, Is.EqualTo(1));
        }

        [Test]
        public void TestShutdownScene()
        {
            TestHelpers.InMethod();

            Scene scene = new SceneHelpers().SetupScene();
            scene.Close();

            Assert.That(scene.ShuttingDown, Is.True);
            Assert.That(scene.Active, Is.False);

            // Trying to update a shutdown scene should result in no update
            scene.Update(1);

            Assert.That(scene.Frame, Is.EqualTo(0));
        }
    }
}