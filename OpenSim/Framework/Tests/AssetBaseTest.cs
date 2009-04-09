using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using OpenMetaverse;

namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class AssetBaseTest
    {
        [Test]
        public void TestContainsReferences()
        {
            TestContainsReferences(AssetType.Bodypart, true);
            TestContainsReferences(AssetType.Clothing, true);
            
            TestContainsReferences(AssetType.Animation, false);
            TestContainsReferences(AssetType.CallingCard, false);
            TestContainsReferences(AssetType.Folder     , false);
            TestContainsReferences(AssetType.Gesture    , false);
            TestContainsReferences(AssetType.ImageJPEG  , false);
            TestContainsReferences(AssetType.ImageTGA   , false);
            TestContainsReferences(AssetType.Landmark   , false);
            TestContainsReferences(AssetType.LostAndFoundFolder, false);
            TestContainsReferences(AssetType.LSLBytecode, false);
            TestContainsReferences(AssetType.LSLText, false);
            TestContainsReferences(AssetType.Notecard, false);
            TestContainsReferences(AssetType.Object, false);
            TestContainsReferences(AssetType.RootFolder, false);
            TestContainsReferences(AssetType.Simstate, false);
            TestContainsReferences(AssetType.SnapshotFolder, false);
            TestContainsReferences(AssetType.Sound, false);
            TestContainsReferences(AssetType.SoundWAV, false);
            TestContainsReferences(AssetType.Texture, false);
            TestContainsReferences(AssetType.TextureTGA, false);
            TestContainsReferences(AssetType.TrashFolder, false);
            TestContainsReferences(AssetType.Unknown, false);
        }

        private void TestContainsReferences(AssetType assetType, bool expected)
        {
            AssetBase asset = new AssetBase();
            asset.Type = (sbyte)assetType;
            bool actual = asset.ContainsReferences;
            Assert.AreEqual(expected, actual, "Expected "+assetType+".ContainsReferences to be "+expected+" but was "+actual+".");
        }
    }
}
