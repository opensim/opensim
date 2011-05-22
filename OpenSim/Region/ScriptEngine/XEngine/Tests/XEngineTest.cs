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
using Nini.Config;
using NUnit.Framework;
using OpenSim.Tests.Common.Mock;
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.ScriptEngine.XEngine.Tests
{
    /// <summary>
    /// Scene presence tests
    /// </summary>
    /// Commented out XEngineTests that don't do anything
    /*
    [TestFixture]
    public class XEngineTest
    {
        public Scene scene;
        
        public static Random random;
        public TestClient testclient;
        //TestCommunicationsManager cm;

        [TestFixtureSetUp]
        public void Init()
        {
            TestCommunicationsManager cm = new TestCommunicationsManager();
            scene = SceneSetupHelpers.SetupScene("My Test", UUID.Random(), 1000, 1000, cm);
            random = new Random();
        }
        
        [Test]
        public void T001_XStart()
        {
            INonSharedRegionModule xengine = new XEngine();
            SceneSetupHelpers.SetupSceneModules(scene, new IniConfigSource(), xengine);
            xengine.RegionLoaded(scene);
        }
    }
    */
}
