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
using log4net.Config;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenMetaverse;
using OpenSim.Framework;
using log4net;

namespace OpenSim.Data.Tests
{
    public class BasicAssetTest
    {
        public IAssetDataPlugin db;
        public UUID uuid1;
        public UUID uuid2;
        public UUID uuid3;
        public byte[] asset1;
        PropertyScrambler<AssetBase> scrambler;

        public void SuperInit()
        {
            OpenSim.Tests.Common.TestLogging.LogToConsole();

            uuid1 = UUID.Random();
            uuid2 = UUID.Random();
            uuid3 = UUID.Random();
            asset1 = new byte[100];
            asset1.Initialize();

            scrambler = new PropertyScrambler<AssetBase>()
                .DontScramble(x => x.ID)
                .DontScramble(x => x.FullID)
                .DontScramble(x => x.Metadata.ID)
                .DontScramble(x => x.Metadata.Type)
                .DontScramble(x => x.Metadata.CreatorID)
                .DontScramble(x => x.Metadata.ContentType)
                .DontScramble(x => x.Metadata.FullID);
        }

        [Test]
        public void T001_LoadEmpty()
        {
            Assert.That(db.ExistsAsset(uuid1), Is.False);
            Assert.That(db.ExistsAsset(uuid2), Is.False);
            Assert.That(db.ExistsAsset(uuid3), Is.False);
        }

        [Test]
        public void T010_StoreSimpleAsset()
        {
            AssetBase a1 = new AssetBase(uuid1, "asset one", (sbyte)AssetType.Texture, UUID.Zero.ToString());
            AssetBase a2 = new AssetBase(uuid2, "asset two", (sbyte)AssetType.Texture, UUID.Zero.ToString());
            AssetBase a3 = new AssetBase(uuid3, "asset three", (sbyte)AssetType.Texture, UUID.Zero.ToString());
            a1.Data = asset1;
            a2.Data = asset1;
            a3.Data = asset1;

            scrambler.Scramble(a1);
            scrambler.Scramble(a2);
            scrambler.Scramble(a3);


            db.StoreAsset(a1);
            db.StoreAsset(a2);
            db.StoreAsset(a3);
            
            AssetBase a1a = db.GetAsset(uuid1);
            Assert.That(a1a, Constraints.PropertyCompareConstraint(a1));

            AssetBase a2a = db.GetAsset(uuid2);
            Assert.That(a2a, Constraints.PropertyCompareConstraint(a2));

            AssetBase a3a = db.GetAsset(uuid3);
            Assert.That(a3a, Constraints.PropertyCompareConstraint(a3));

            scrambler.Scramble(a1a);
            scrambler.Scramble(a2a);
            scrambler.Scramble(a3a);

            db.StoreAsset(a1a);
            db.StoreAsset(a2a);
            db.StoreAsset(a3a);

            AssetBase a1b = db.GetAsset(uuid1);
            Assert.That(a1b, Constraints.PropertyCompareConstraint(a1a));

            AssetBase a2b = db.GetAsset(uuid2);
            Assert.That(a2b, Constraints.PropertyCompareConstraint(a2a));

            AssetBase a3b = db.GetAsset(uuid3);
            Assert.That(a3b, Constraints.PropertyCompareConstraint(a3a));

            Assert.That(db.ExistsAsset(uuid1), Is.True);
            Assert.That(db.ExistsAsset(uuid2), Is.True);
            Assert.That(db.ExistsAsset(uuid3), Is.True);

            List<AssetMetadata> metadatas = db.FetchAssetMetadataSet(0, 1000);

            AssetMetadata metadata = metadatas.Find(x => x.FullID == uuid1);
            Assert.That(metadata.Name, Is.EqualTo(a1b.Name));
            Assert.That(metadata.Description, Is.EqualTo(a1b.Description));
            Assert.That(metadata.Type, Is.EqualTo(a1b.Type));
            Assert.That(metadata.Temporary, Is.EqualTo(a1b.Temporary));
            Assert.That(metadata.FullID, Is.EqualTo(a1b.FullID));
        }
    }
}
