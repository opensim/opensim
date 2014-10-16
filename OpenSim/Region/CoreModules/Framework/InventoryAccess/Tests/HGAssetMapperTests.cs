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
using System.Xml;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Framework.InventoryAccess;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.Framework.InventoryAccess.Tests
{
    [TestFixture]
    public class HGAssetMapperTests : OpenSimTestCase
    {
        [Test]
        public void TestPostAssetRewrite()
        {
            TestHelpers.InMethod();

            string homeUrl = "http://hg.HomeTestPostAssetRewriteGrid.com";
            string foreignUrl = "http://hg.ForeignTestPostAssetRewriteGrid.com";
            UUID assetId = TestHelpers.ParseTail(0x1);
            UUID userId = TestHelpers.ParseTail(0x10);
            string userFirstName = "TestPostAsset";
            string userLastName = "Rewrite";
            int soPartsCount = 3;

            Scene scene = new SceneHelpers().SetupScene();
            HGAssetMapper hgam = new HGAssetMapper(scene, homeUrl);
            UserAccount ua 
                = UserAccountHelpers.CreateUserWithInventory(scene, userFirstName, userLastName, userId, "password");

            //AssetBase ncAssetSet = AssetHelpers.CreateNotecardAsset(assetId, "TestPostAssetRewriteNotecard");
            SceneObjectGroup so = SceneHelpers.CreateSceneObject(soPartsCount, ua.PrincipalID);
            AssetBase ncAssetSet = AssetHelpers.CreateAsset(assetId, so);
            ncAssetSet.CreatorID = foreignUrl;
            hgam.PostAsset(foreignUrl, ncAssetSet);

            AssetBase ncAssetGet = scene.AssetService.Get(assetId.ToString());
            Assert.AreEqual(foreignUrl, ncAssetGet.CreatorID);
            string xmlData = Utils.BytesToString(ncAssetGet.Data);
            XmlDocument ncAssetGetXmlDoc = new XmlDocument();
            ncAssetGetXmlDoc.LoadXml(xmlData);
            XmlNodeList creatorDataNodes = ncAssetGetXmlDoc.GetElementsByTagName("CreatorData");

            Assert.AreEqual(soPartsCount, creatorDataNodes.Count);
            //Console.WriteLine("creatorDataNodes {0}", creatorDataNodes.Count);

            foreach (XmlNode creatorDataNode in creatorDataNodes)
            {
                Assert.AreEqual(
                    string.Format("{0};{1} {2}", homeUrl, ua.FirstName, ua.LastName), creatorDataNode.InnerText);
            }
        }
    }
}