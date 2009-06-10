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
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Server.Base;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Tests.Common.Setup
{
    public class GetAssetStreamHandlerTestHelpers
    {
        private const string EXPECTED_CONTENT_TYPE = "application/x-metaverse-callingcard";

        public static void BaseFetchExistingAssetXmlTest(AssetBase asset, BaseGetAssetStreamHandler handler, OSHttpResponse response)
        {
            byte[] expected = BaseGetAssetStreamHandler.GetXml(asset);

            byte[] actual = handler.Handle("/assets/" + asset.ID , null, null, response);

            Assert.Greater(actual.Length, 10, "Too short xml on fetching xml without trailing slash.");
            Assert.AreEqual(expected, actual, "Failed on fetching xml without trailing slash.");            
            // Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode, "Wrong http response code on first fetch."); 

            actual = handler.Handle("/assets/" + asset.ID + "/", null, null, response);
            Assert.Greater(actual.Length, 10, "Too short xml on fetching xml with trailing slash.");
            Assert.AreEqual(expected, actual, "Failed on fetching xml with trailing slash.");
            // Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode, "Wrong http response code on second fetch.");
            
            actual = handler.Handle("/assets/" + asset.ID + "/badData", null, null, response);
            Assert.Greater(actual.Length, 10, "Too short xml on fetching xml with bad trailing data.");
            Assert.AreEqual(expected, actual, "Failed on fetching xml with bad trailing trailing slash.");
            // Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode, "Wrong http response code on second fetch.");
        }

        public static void BaseFetchExistingAssetDataTest(AssetBase asset, BaseGetAssetStreamHandler handler, OSHttpResponse response)
        {
            Assert.AreEqual(asset.Data, handler.Handle("/assets/" + asset.ID + "/data", null, null, response), "Failed on fetching data without trailing slash.");
            Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode, "Wrong http response code on first fetch.");
            Assert.AreEqual(EXPECTED_CONTENT_TYPE, response.ContentType, "Wrong http content type on first fetch.");

            Assert.AreEqual(asset.Data, handler.Handle("/assets/" + asset.ID + "/data/", null, null, response), "Failed on fetching data with trailing slash.");
            Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode, "Wrong http response code on second fetch.");
            Assert.AreEqual(EXPECTED_CONTENT_TYPE, response.ContentType, "Wrong http content type on second fetch.");
        }

        public static void BaseFetchExistingAssetMetaDataTest(AssetBase asset, BaseGetAssetStreamHandler handler, OSHttpResponse response)
        {
            XmlSerializer xs = new XmlSerializer(typeof(AssetMetadata));

            byte[] expected = ServerUtils.SerializeResult(xs, asset.Metadata);

            Assert.AreEqual(expected, handler.Handle("/assets/" + asset.ID + "/metadata", null, null, response), "Failed on fetching data without trailing slash.");
            Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode, "Wrong http response code on first fetch.");
            Assert.AreEqual(EXPECTED_CONTENT_TYPE, response.ContentType, "Wrong http content type on first fetch.");

            Assert.AreEqual(expected, handler.Handle("/assets/" + asset.ID + "/metadata/", null, null, response), "Failed on fetching data with trailing slash.");
            Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode, "Wrong http response code on second fetch.");
            Assert.AreEqual(EXPECTED_CONTENT_TYPE, response.ContentType, "Wrong http content type on second fetch.");
        }

        public static AssetBase CreateCommonTestResources(out OSHttpResponse response)
        {
            AssetBase asset = CreateTestAsset();
            response = new TestOSHttpResponse();
            return asset;
        }

        public static AssetBase CreateTestAsset()
        {
            byte[] expected = new byte[] { 1,2,3 };
            AssetBase asset = new AssetBase();
            asset.ID = Guid.NewGuid().ToString();
            asset.Data = expected;
            asset.Type = 2;
       
            return asset;
        }

        public static void BaseFetchMissingAsset(BaseGetAssetStreamHandler handler, OSHttpResponse response)
        {
            Assert.AreEqual(
                BaseRequestHandlerTestHelper.EmptyByteArray, 
                handler.Handle("/assets/" + Guid.NewGuid(), null, null, response), "Failed on bad guid.");
            Assert.AreEqual((int)HttpStatusCode.NotFound, response.StatusCode, "Response code wrong in BaseFetchMissingAsset");
        }
    }
}
