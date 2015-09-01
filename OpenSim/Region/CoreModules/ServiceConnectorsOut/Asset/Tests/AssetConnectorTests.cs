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
using System.Reflection;
using System.Threading;
using log4net.Config;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.ServiceConnectorsOut.Asset;
using OpenSim.Tests.Common;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Asset.Tests
{
    [TestFixture]
    public class AssetConnectorTests : OpenSimTestCase
    {
        [Test]
        public void TestAddAsset()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("AssetServices", "LocalAssetServicesConnector");
            config.AddConfig("AssetService");
            config.Configs["AssetService"].Set("LocalServiceModule", "OpenSim.Services.AssetService.dll:AssetService");
            config.Configs["AssetService"].Set("StorageProvider", "OpenSim.Tests.Common.dll");

            LocalAssetServicesConnector lasc = new LocalAssetServicesConnector();
            lasc.Initialise(config);

            AssetBase a1 = AssetHelpers.CreateNotecardAsset();
            lasc.Store(a1);

            AssetBase retreivedA1 = lasc.Get(a1.ID);
            Assert.That(retreivedA1.ID, Is.EqualTo(a1.ID));
            Assert.That(retreivedA1.Metadata.ID, Is.EqualTo(a1.Metadata.ID));
            Assert.That(retreivedA1.Data.Length, Is.EqualTo(a1.Data.Length));

            AssetMetadata retrievedA1Metadata = lasc.GetMetadata(a1.ID);
            Assert.That(retrievedA1Metadata.ID, Is.EqualTo(a1.ID));

            byte[] retrievedA1Data = lasc.GetData(a1.ID);
            Assert.That(retrievedA1Data.Length, Is.EqualTo(a1.Data.Length));

            // TODO: Add cache and check that this does receive a copy of the asset
        }

        public void TestAddTemporaryAsset()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("AssetServices", "LocalAssetServicesConnector");
            config.AddConfig("AssetService");
            config.Configs["AssetService"].Set("LocalServiceModule", "OpenSim.Services.AssetService.dll:AssetService");
            config.Configs["AssetService"].Set("StorageProvider", "OpenSim.Tests.Common.dll");

            LocalAssetServicesConnector lasc = new LocalAssetServicesConnector();
            lasc.Initialise(config);

            // If it is remote, it should be stored
            AssetBase a2 = AssetHelpers.CreateNotecardAsset();
            a2.Local = false;
            a2.Temporary = true;

            lasc.Store(a2);

            AssetBase retreivedA2 = lasc.Get(a2.ID);
            Assert.That(retreivedA2.ID, Is.EqualTo(a2.ID));
            Assert.That(retreivedA2.Metadata.ID, Is.EqualTo(a2.Metadata.ID));
            Assert.That(retreivedA2.Data.Length, Is.EqualTo(a2.Data.Length));

            AssetMetadata retrievedA2Metadata = lasc.GetMetadata(a2.ID);
            Assert.That(retrievedA2Metadata.ID, Is.EqualTo(a2.ID));

            byte[] retrievedA2Data = lasc.GetData(a2.ID);
            Assert.That(retrievedA2Data.Length, Is.EqualTo(a2.Data.Length));

            // TODO: Add cache and check that this does receive a copy of the asset
        }

        [Test]
        public void TestAddLocalAsset()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("AssetServices", "LocalAssetServicesConnector");
            config.AddConfig("AssetService");
            config.Configs["AssetService"].Set("LocalServiceModule", "OpenSim.Services.AssetService.dll:AssetService");
            config.Configs["AssetService"].Set("StorageProvider", "OpenSim.Tests.Common.dll");

            LocalAssetServicesConnector lasc = new LocalAssetServicesConnector();
            lasc.Initialise(config);

            AssetBase a1 = AssetHelpers.CreateNotecardAsset();
            a1.Local = true;

            lasc.Store(a1);

            Assert.That(lasc.Get(a1.ID), Is.Null);
            Assert.That(lasc.GetData(a1.ID), Is.Null);
            Assert.That(lasc.GetMetadata(a1.ID), Is.Null);

            // TODO: Add cache and check that this does receive a copy of the asset
        }

        [Test]
        public void TestAddTemporaryLocalAsset()
        {
            TestHelpers.InMethod();
//            TestHelpers.EnableLogging();

            IConfigSource config = new IniConfigSource();
            config.AddConfig("Modules");
            config.Configs["Modules"].Set("AssetServices", "LocalAssetServicesConnector");
            config.AddConfig("AssetService");
            config.Configs["AssetService"].Set("LocalServiceModule", "OpenSim.Services.AssetService.dll:AssetService");
            config.Configs["AssetService"].Set("StorageProvider", "OpenSim.Tests.Common.dll");

            LocalAssetServicesConnector lasc = new LocalAssetServicesConnector();
            lasc.Initialise(config);

            // If it is local, it should not be stored
            AssetBase a1 = AssetHelpers.CreateNotecardAsset();
            a1.Local = true;
            a1.Temporary = true;

            lasc.Store(a1);

            Assert.That(lasc.Get(a1.ID), Is.Null);
            Assert.That(lasc.GetData(a1.ID), Is.Null);
            Assert.That(lasc.GetMetadata(a1.ID), Is.Null);

            // TODO: Add cache and check that this does receive a copy of the asset
        }
    }
}