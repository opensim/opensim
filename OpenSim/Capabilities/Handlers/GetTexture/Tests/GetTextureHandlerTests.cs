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
using System.Net;
using log4net;
using log4net.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Capabilities.Handlers;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

/*
namespace OpenSim.Capabilities.Handlers.GetTexture.Tests
{
    [TestFixture]
    public class GetTextureHandlerTests : OpenSimTestCase
    {
        [Test]
        public void TestTextureNotFound()
        {
            TestHelpers.InMethod();

            // Overkill - we only really need the asset service, not a whole scene.
            Scene scene = new SceneHelpers().SetupScene();

            GetTextureHandler handler = new GetTextureHandler("/gettexture", scene.AssetService, "TestGetTexture", null, null);
            TestOSHttpRequest req = new TestOSHttpRequest();
            TestOSHttpResponse resp = new TestOSHttpResponse();
            req.Url = new Uri("http://localhost/?texture_id=00000000-0000-1111-9999-000000000012");
            handler.Handle(null, null, req, resp);
            Assert.That(resp.StatusCode, Is.EqualTo((int)System.Net.HttpStatusCode.NotFound));
        }
    }
}
*/
