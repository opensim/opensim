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
 *     * Neither the name of the OpenSim Project nor the
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
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;
using OpenMetaverse;

namespace OpenSim.Data.Tests
{
    public class BasicAssetTest
    {
        public AssetDataBase db;
        public UUID uuid1;
        public UUID uuid2;
        public UUID uuid3;

        public void SuperInit()
        {
            uuid1 = UUID.Random();
            uuid2 = UUID.Random();
            uuid3 = UUID.Random();
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
            AssetBase a1 = new AssetBase(uuid1, "asset one");
            AssetBase a2 = new AssetBase(uuid2, "asset two");
            AssetBase a3 = new AssetBase(uuid3, "asset three");
            db.CreateAsset(a1);
            db.CreateAsset(a2);
            db.CreateAsset(a3);

            AssetBase a1a = db.FetchAsset(uuid1);
            Assert.That(a1.ID, Is.EqualTo(a1a.ID));
            Assert.That(a1.Name, Text.Matches(a1a.Name));

            AssetBase a2a = db.FetchAsset(uuid2);
            Assert.That(a2.ID, Is.EqualTo(a2a.ID));
            Assert.That(a2.Name, Text.Matches(a2a.Name));

            AssetBase a3a = db.FetchAsset(uuid3);
            Assert.That(a3.ID, Is.EqualTo(a3a.ID));
            Assert.That(a3.Name, Text.Matches(a3a.Name));
        }

        [Test]
        public void T011_ExistsSimpleAsset()
        {
            Assert.That(db.ExistsAsset(uuid1), Is.True);
            Assert.That(db.ExistsAsset(uuid2), Is.True);
            Assert.That(db.ExistsAsset(uuid3), Is.True);
        }

        // this has questionable use, but it is in the interface at the moment.
        // [Test]
        // public void T012_DeleteAsset()
        // {
        //     db.DeleteAsset(uuid1);
        //     db.DeleteAsset(uuid2);
        //     db.DeleteAsset(uuid3);
        //     Assert.That(db.ExistsAsset(uuid1), Is.False);
        //     Assert.That(db.ExistsAsset(uuid2), Is.False);
        //     Assert.That(db.ExistsAsset(uuid3), Is.False);
        // }
    }
}