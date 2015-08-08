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
using System.Text;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Tests.Common;

namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class AssetBaseTest : OpenSimTestCase
    {
        [Test]
        public void TestContainsReferences()
        {
            CheckContainsReferences(AssetType.Bodypart, true);
            CheckContainsReferences(AssetType.Clothing, true);

            CheckContainsReferences(AssetType.Animation, false);
            CheckContainsReferences(AssetType.CallingCard, false);
            CheckContainsReferences(AssetType.Folder     , false);
            CheckContainsReferences(AssetType.Gesture    , false);
            CheckContainsReferences(AssetType.ImageJPEG  , false);
            CheckContainsReferences(AssetType.ImageTGA   , false);
            CheckContainsReferences(AssetType.Landmark   , false);
            CheckContainsReferences(AssetType.LSLBytecode, false);
            CheckContainsReferences(AssetType.LSLText, false);
            CheckContainsReferences(AssetType.Notecard, false);
            CheckContainsReferences(AssetType.Object, false);
            CheckContainsReferences(AssetType.Simstate, false);
            CheckContainsReferences(AssetType.Sound, false);
            CheckContainsReferences(AssetType.SoundWAV, false);
            CheckContainsReferences(AssetType.Texture, false);
            CheckContainsReferences(AssetType.TextureTGA, false);
            CheckContainsReferences(AssetType.Unknown, false);
        }

        private void CheckContainsReferences(AssetType assetType, bool expected)
        {
            AssetBase asset = new AssetBase(UUID.Zero, String.Empty, (sbyte)assetType, UUID.Zero.ToString());
            bool actual = asset.ContainsReferences;
            Assert.AreEqual(expected, actual, "Expected "+assetType+".ContainsReferences to be "+expected+" but was "+actual+".");
        }
    }
}
