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
using System.Reflection;
using System.Threading;
using System.Xml;
using System.Linq;
using Nini.Config;
using NUnit.Framework;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Serialization.External;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;
using OpenSim.Tests.Common;

namespace OpenSim.Region.Framework.Scenes.Tests
{
    /// <summary>
    /// Basic scene object serialization tests.
    /// </summary>
    [TestFixture]
    public class SceneObjectSerializationTests : OpenSimTestCase
    {

        /// <summary>
        /// Serialize and deserialize.
        /// </summary>
        [Test]
        public void TestSerialDeserial()
        {
            TestHelpers.InMethod();

            Scene scene = new SceneHelpers().SetupScene();
            int partsToTestCount = 3;

            SceneObjectGroup so
                = SceneHelpers.CreateSceneObject(partsToTestCount, TestHelpers.ParseTail(0x1), "obj1", 0x10);
            SceneObjectPart[] parts = so.Parts;
            so.Name = "obj1";
            so.Description = "xpto";

            string xml = SceneObjectSerializer.ToXml2Format(so);
            Assert.That(!string.IsNullOrEmpty(xml), "SOG serialization resulted in empty or null string");

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            XmlNodeList nodes = doc.GetElementsByTagName("SceneObjectPart");
            Assert.That(nodes.Count, Is.EqualTo(3), "SOG serialization resulted in wrong number of SOPs");

            SceneObjectGroup so2 = SceneObjectSerializer.FromXml2Format(xml);
            Assert.IsNotNull(so2, "SOG deserialization resulted in null object");
            Assert.That(so2.Name == so.Name, "Name of deserialized object does not match original name");
            Assert.That(so2.Description == so.Description, "Description of deserialized object does not match original name");
        }

        /// <summary>
        /// This checks for a bug reported in mantis #7514
        /// </summary>
        [Test]
        public void TestNamespaceAttribute()
        {
            TestHelpers.InMethod();

            Scene scene = new SceneHelpers().SetupScene();
            UserAccount account = new UserAccount(UUID.Zero, UUID.Random(), "Test", "User", string.Empty);
            scene.UserAccountService.StoreUserAccount(account);
            int partsToTestCount = 1;

            SceneObjectGroup so
                = SceneHelpers.CreateSceneObject(partsToTestCount, TestHelpers.ParseTail(0x1), "obj1", 0x10);
            SceneObjectPart[] parts = so.Parts;
            so.Name = "obj1";
            so.Description = "xpto";
            so.OwnerID = account.PrincipalID;
            so.RootPart.CreatorID = so.OwnerID;

            string xml = SceneObjectSerializer.ToXml2Format(so);
            Assert.That(!string.IsNullOrEmpty(xml), "SOG serialization resulted in empty or null string");

            xml = ExternalRepresentationUtils.RewriteSOP(xml, "Test Scene", "http://localhost", scene.UserAccountService, UUID.Zero);
            //Console.WriteLine(xml);

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            XmlNodeList nodes = doc.GetElementsByTagName("SceneObjectPart");
            Assert.That(nodes.Count, Is.GreaterThan(0), "SOG serialization resulted in no SOPs");
            foreach (XmlAttribute a in nodes[0].Attributes)
            {
                int count = a.Name.Count(c => c == ':');
                Assert.That(count, Is.EqualTo(1), "Cannot have multiple ':' in attribute name in SOP");
            }
            nodes = doc.GetElementsByTagName("CreatorData");
            Assert.That(nodes.Count, Is.GreaterThan(0), "SOG serialization resulted in no CreatorData");
            foreach (XmlAttribute a in nodes[0].Attributes)
            {
                int count = a.Name.Count(c => c == ':');
                Assert.That(count, Is.EqualTo(1), "Cannot have multiple ':' in attribute name in CreatorData");
            }

            SceneObjectGroup so2 = SceneObjectSerializer.FromXml2Format(xml);
            Assert.IsNotNull(so2, "SOG deserialization resulted in null object");
            Assert.AreNotEqual(so.RootPart.CreatorIdentification, so2.RootPart.CreatorIdentification, "RewriteSOP failed to transform CreatorData.");
            Assert.That(so2.RootPart.CreatorIdentification.Contains("http://"), "RewriteSOP failed to add the homeURL to CreatorData");
        }
    }
}