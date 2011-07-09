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
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Region.CoreModules.Avatar.AvatarFactory
{
    [TestFixture]
    public class AvatarFactoryModuleTests
    {
        /// <summary>
        /// Only partial right now since we don't yet test that it's ended up in the avatar appearance service.
        /// </summary>
        [Test]
        public void TestSetAppearance()
        {
            TestHelper.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID userId = TestHelper.ParseTail(0x1);

            AvatarFactoryModule afm = new AvatarFactoryModule();
            TestScene scene = SceneSetupHelpers.SetupScene();
            SceneSetupHelpers.SetupSceneModules(scene, afm);

            AgentCircuitData acd = new AgentCircuitData();
            acd.AgentID = userId;
            TestClient tc = SceneSetupHelpers.AddClient(scene, acd);

            byte[] visualParams = new byte[AvatarAppearance.VISUALPARAM_COUNT];
            for (byte i = 0; i < visualParams.Length; i++)
                visualParams[i] = i;

            afm.SetAppearance(tc, new Primitive.TextureEntry(TestHelper.ParseTail(0x10)), visualParams);

            ScenePresence sp = scene.GetScenePresence(userId);

            // TODO: Check baked texture
            Assert.AreEqual(visualParams, sp.Appearance.VisualParams);
        }
    }
}