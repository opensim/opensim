using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NUnit.Framework;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Tests.Common.Mock;

namespace OpenSim.Tests.Common.Setup
{
    public class GetAssetStreamHandlerTestHelpers
    {
        public static void BaseFetchExistingAssetXmlTest(AssetBase asset, BaseGetAssetStreamHandler handler, OSHttpResponse response)
        {
            byte[] expected = BaseGetAssetStreamHandler.GetXml(asset);

            byte[] actual = handler.Handle("/assets/" + asset.ID , null, null, response);

            Assert.Greater(actual.Length, 10, "Too short xml on fetching xml without trailing slash.");
            Assert.AreEqual(expected, actual, "Failed on fetching xml without trailing slash.");
            // Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode, "Wrong http response code on first fetch."); 

            byte[] actual1 = handler.Handle("/assets/" + asset.ID + "/", null, null, response);
            Assert.Greater(actual1.Length, 10, "Too short xml on fetching xml with trailing slash.");
            Assert.AreEqual(expected, actual1, "Failed on fetching xml with trailing slash.");
            // Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode, "Wrong http response code on second fetch.");
        }

        public static void BaseFetchExistingAssetDataTest(AssetBase asset, BaseGetAssetStreamHandler handler, OSHttpResponse response)
        {
            Assert.AreEqual(asset.Data, handler.Handle("/assets/" + asset.ID + "/data", null, null, response), "Failed on fetching data without trailing slash.");
            Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode, "Wrong http response code on first fetch.");

            Assert.AreEqual(asset.Data, handler.Handle("/assets/" + asset.ID + "/data/", null, null, response), "Failed on fetching data with trailing slash.");
            Assert.AreEqual((int)HttpStatusCode.OK, response.StatusCode, "Wrong http response code on second fetch.");
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
            AssetBase asset = new AssetBase( );
            asset.ID = Guid.NewGuid().ToString();
            asset.Data = expected;
            return asset;
        }

        public static void BaseFetchMissingAsset(BaseGetAssetStreamHandler handler)
        {
            Assert.AreEqual(BaseRequestHandlerTestHelper.EmptyByteArray, handler.Handle("/assets/" + Guid.NewGuid(), null, null, null), "Failed on bad guid.");
        }
    }
}
