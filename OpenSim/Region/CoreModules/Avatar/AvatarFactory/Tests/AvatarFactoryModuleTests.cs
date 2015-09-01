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
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.CoreModules.Asset;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.Avatar.AvatarFactory
{
    [TestFixture]
    public class AvatarFactoryModuleTests : OpenSimTestCase
    {
        /// <summary>
        /// Only partial right now since we don't yet test that it's ended up in the avatar appearance service.
        /// </summary>
        [Test]
        public void TestSetAppearance()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);
            UUID bakedTextureID = TestHelpers.ParseTail(0x2);

            // We need an asset cache because otherwise the LocalAssetServiceConnector will short-circuit directly
            // to the AssetService, which will then store temporary and local assets permanently
            CoreAssetCache assetCache = new CoreAssetCache();
            
            AvatarFactoryModule afm = new AvatarFactoryModule();
            TestScene scene = new SceneHelpers(assetCache).SetupScene();
            SceneHelpers.SetupSceneModules(scene, afm);
            ScenePresence sp = SceneHelpers.AddScenePresence(scene, userId);

            // TODO: Use the actual BunchOfCaps functionality once we slot in the CapabilitiesModules
            AssetBase bakedTextureAsset;
            bakedTextureAsset 
                = new AssetBase(
                    bakedTextureID, "Test Baked Texture", (sbyte)AssetType.Texture, userId.ToString());
            bakedTextureAsset.Data = new byte[] { 2 }; // Not necessary to have a genuine JPEG2000 asset here yet
            bakedTextureAsset.Temporary = true;
            bakedTextureAsset.Local = true;
            scene.AssetService.Store(bakedTextureAsset);

            byte[] visualParams = new byte[AvatarAppearance.VISUALPARAM_COUNT];
            for (byte i = 0; i < visualParams.Length; i++)
                visualParams[i] = i;

            Primitive.TextureEntry bakedTextureEntry = new Primitive.TextureEntry(TestHelpers.ParseTail(0x10));
            uint eyesFaceIndex = (uint)AppearanceManager.BakeTypeToAgentTextureIndex(BakeType.Eyes);
            Primitive.TextureEntryFace eyesFace = bakedTextureEntry.CreateFace(eyesFaceIndex);

            int rebakeRequestsReceived = 0;
            ((TestClient)sp.ControllingClient).OnReceivedSendRebakeAvatarTextures += id => rebakeRequestsReceived++;

            // This is the alpha texture
            eyesFace.TextureID = bakedTextureID;
            afm.SetAppearance(sp, bakedTextureEntry, visualParams, null);
    
            Assert.That(rebakeRequestsReceived, Is.EqualTo(0));

            AssetBase eyesBake = scene.AssetService.Get(bakedTextureID.ToString());
            Assert.That(eyesBake, Is.Not.Null);
            Assert.That(eyesBake.Temporary, Is.True);
            Assert.That(eyesBake.Local, Is.True);
        }

        /// <summary>
        /// Test appearance setting where the baked texture UUID are library alpha textures.
        /// </summary>
        /// <remarks>
        /// For a mesh avatar, it appears these 'baked textures' are used.  So these should not trigger a request to 
        /// rebake.
        /// </remarks>
        [Test]
        public void TestSetAppearanceAlphaBakedTextures()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            UUID userId = TestHelpers.ParseTail(0x1);
            UUID alphaTextureID = new UUID("3a367d1c-bef1-6d43-7595-e88c1e3aadb3");

            // We need an asset cache because otherwise the LocalAssetServiceConnector will short-circuit directly
            // to the AssetService, which will then store temporary and local assets permanently
            CoreAssetCache assetCache = new CoreAssetCache();
            
            AvatarFactoryModule afm = new AvatarFactoryModule();
            TestScene scene = new SceneHelpers(assetCache).SetupScene();
            SceneHelpers.SetupSceneModules(scene, afm);
            ScenePresence sp = SceneHelpers.AddScenePresence(scene, userId);

            AssetBase libraryAsset;
            libraryAsset 
                = new AssetBase(
                    alphaTextureID, "Default Alpha Layer Texture", (sbyte)AssetType.Texture, userId.ToString());
            libraryAsset.Data = new byte[] { 2 }; // Not necessary to have a genuine JPEG2000 asset here yet
            libraryAsset.Temporary = false;
            libraryAsset.Local = false;
            scene.AssetService.Store(libraryAsset);

            byte[] visualParams = new byte[AvatarAppearance.VISUALPARAM_COUNT];
            for (byte i = 0; i < visualParams.Length; i++)
                visualParams[i] = i;

            Primitive.TextureEntry bakedTextureEntry = new Primitive.TextureEntry(TestHelpers.ParseTail(0x10));
            uint eyesFaceIndex = (uint)AppearanceManager.BakeTypeToAgentTextureIndex(BakeType.Eyes);
            Primitive.TextureEntryFace eyesFace = bakedTextureEntry.CreateFace(eyesFaceIndex);

            int rebakeRequestsReceived = 0;
            ((TestClient)sp.ControllingClient).OnReceivedSendRebakeAvatarTextures += id => rebakeRequestsReceived++;

            // This is the alpha texture
            eyesFace.TextureID = alphaTextureID;
            afm.SetAppearance(sp, bakedTextureEntry, visualParams, null);
    
            Assert.That(rebakeRequestsReceived, Is.EqualTo(0));
        }

        [Test]
        public void TestSaveBakedTextures()
        {
            TestHelpers.InMethod();
//            log4net.Config.XmlConfigurator.Configure();

            UUID userId = TestHelpers.ParseTail(0x1);
            UUID eyesTextureId = TestHelpers.ParseTail(0x2);

            // We need an asset cache because otherwise the LocalAssetServiceConnector will short-circuit directly
            // to the AssetService, which will then store temporary and local assets permanently
            CoreAssetCache assetCache = new CoreAssetCache();
            
            AvatarFactoryModule afm = new AvatarFactoryModule();
            TestScene scene = new SceneHelpers(assetCache).SetupScene();
            SceneHelpers.SetupSceneModules(scene, afm);
            ScenePresence sp = SceneHelpers.AddScenePresence(scene, userId);

            // TODO: Use the actual BunchOfCaps functionality once we slot in the CapabilitiesModules
            AssetBase uploadedAsset;
            uploadedAsset = new AssetBase(eyesTextureId, "Baked Texture", (sbyte)AssetType.Texture, userId.ToString());
            uploadedAsset.Data = new byte[] { 2 };
            uploadedAsset.Temporary = true;
            uploadedAsset.Local = true; // Local assets aren't persisted, non-local are
            scene.AssetService.Store(uploadedAsset);

            byte[] visualParams = new byte[AvatarAppearance.VISUALPARAM_COUNT];
            for (byte i = 0; i < visualParams.Length; i++)
                visualParams[i] = i;

            Primitive.TextureEntry bakedTextureEntry = new Primitive.TextureEntry(TestHelpers.ParseTail(0x10));
            uint eyesFaceIndex = (uint)AppearanceManager.BakeTypeToAgentTextureIndex(BakeType.Eyes);
            Primitive.TextureEntryFace eyesFace = bakedTextureEntry.CreateFace(eyesFaceIndex);
            eyesFace.TextureID = eyesTextureId;

            afm.SetAppearance(sp, bakedTextureEntry, visualParams, null);
            afm.SaveBakedTextures(userId);
//            Dictionary<BakeType, Primitive.TextureEntryFace> bakedTextures = afm.GetBakedTextureFaces(userId);

            // We should also inpsect the asset data store layer directly, but this is difficult to get at right now.
            assetCache.Clear();

            AssetBase eyesBake = scene.AssetService.Get(eyesTextureId.ToString());
            Assert.That(eyesBake, Is.Not.Null);
            Assert.That(eyesBake.Temporary, Is.False);
            Assert.That(eyesBake.Local, Is.False);
        }
    }
}