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
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public AssetDataBase db;
        public UUID uuid1;
        public UUID uuid2;
        public UUID uuid3;
        public byte[] asset1;

        public void SuperInit()
        {
            try
            {
                XmlConfigurator.Configure();
            }
            catch (Exception)
            {
                // I don't care, just leave log4net off
            }

            uuid1 = UUID.Random();
            uuid2 = UUID.Random();
            uuid3 = UUID.Random();
            asset1 = new byte[100];
            asset1.Initialize();
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
            
            ScrambleForTesting.Scramble(a1);
            ScrambleForTesting.Scramble(a2);
            ScrambleForTesting.Scramble(a3);
            
            a1.Data = asset1;
            a2.Data = asset1;
            a3.Data = asset1;
            
            a1.FullID = uuid1;
            a2.FullID = uuid2;
            a3.FullID = uuid3;

            db.CreateAsset(a1);
            db.CreateAsset(a2);
            db.CreateAsset(a3);
            
            AssetBase a1a = db.FetchAsset(uuid1);
            Assert.That(a1a, Constraints.PropertyCompareConstraint(a1));

            AssetBase a2a = db.FetchAsset(uuid2);
            Assert.That(a2a, Constraints.PropertyCompareConstraint(a2));

            AssetBase a3a = db.FetchAsset(uuid3);
            Assert.That(a3a, Constraints.PropertyCompareConstraint(a3));

            ScrambleForTesting.Scramble(a1a);
            ScrambleForTesting.Scramble(a2a);
            ScrambleForTesting.Scramble(a3a);

            a1a.Data = asset1;
            a2a.Data = asset1;
            a3a.Data = asset1;

            a1a.FullID = uuid1;
            a2a.FullID = uuid2;
            a3a.FullID = uuid3;

            db.UpdateAsset(a1a);
            db.UpdateAsset(a2a);
            db.UpdateAsset(a3a);

            AssetBase a1b = db.FetchAsset(uuid1);
            Assert.That(a1b, Constraints.PropertyCompareConstraint(a1a));

            AssetBase a2b = db.FetchAsset(uuid2);
            Assert.That(a2b, Constraints.PropertyCompareConstraint(a2a));

            AssetBase a3b = db.FetchAsset(uuid3);
            Assert.That(a3b, Constraints.PropertyCompareConstraint(a3a));
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
