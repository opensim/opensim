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
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Server.Handlers.Asset;
using OpenSim.Services.AssetService;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Server.Handlers.Asset.Test
{
    [TestFixture]
    public class AssetServerPostHandlerTests : OpenSimTestCase
    {
        [Test]
        public void TestGoodAssetStoreRequest()
        {
            TestHelpers.InMethod();

            UUID assetId = TestHelpers.ParseTail(0x1);

            IConfigSource config = new IniConfigSource();         
            config.AddConfig("AssetService");           
            config.Configs["AssetService"].Set("StorageProvider", "OpenSim.Tests.Common.dll");

            AssetService assetService = new AssetService(config);

            AssetServerPostHandler asph = new AssetServerPostHandler(assetService);

            AssetBase asset = AssetHelpers.CreateNotecardAsset(assetId, "Hello World");

            MemoryStream buffer = new MemoryStream();

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = Encoding.UTF8;

            using (XmlWriter writer = XmlWriter.Create(buffer, settings))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(AssetBase));
                serializer.Serialize(writer, asset);
                writer.Flush();
            }            

            buffer.Position = 0;
            asph.Handle(null, buffer, null, null);

            AssetBase retrievedAsset = assetService.Get(assetId.ToString());

            Assert.That(retrievedAsset, Is.Not.Null);
        }

        [Test]
        public void TestBadXmlAssetStoreRequest()
        {
            TestHelpers.InMethod();

            IConfigSource config = new IniConfigSource();         
            config.AddConfig("AssetService");           
            config.Configs["AssetService"].Set("StorageProvider", "OpenSim.Tests.Common.dll");

            AssetService assetService = new AssetService(config);

            AssetServerPostHandler asph = new AssetServerPostHandler(assetService);          

            MemoryStream buffer = new MemoryStream();
            byte[] badData = new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f };
            buffer.Write(badData, 0, badData.Length);
            buffer.Position = 0;

            TestOSHttpResponse response = new TestOSHttpResponse();
            asph.Handle(null, buffer, null, response);

            Assert.That(response.StatusCode, Is.EqualTo((int)HttpStatusCode.BadRequest));
        }
    }
}