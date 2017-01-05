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
using System.Reflection;
using NUnit.Framework;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Tests.Common;
using Animation = OpenSim.Framework.Animation;

namespace OpenSim.Framework.Tests
{
    [TestFixture]
    public class AnimationTests : OpenSimTestCase
    {
        private Animation anim1 = null;
        private Animation anim2 = null;
        private UUID animUUID1 = UUID.Zero;
        private UUID objUUID1 = UUID.Zero;
        private UUID animUUID2 = UUID.Zero;
        private UUID objUUID2 = UUID.Zero;

        [SetUp]
        public void Setup()
        {
            animUUID1 = UUID.Random();
            animUUID2 = UUID.Random();
            objUUID1 = UUID.Random();
            objUUID2 = UUID.Random();

            anim1 = new Animation(animUUID1, 1, objUUID1);
            anim2 = new Animation(animUUID2, 1, objUUID2);
        }

        [Test]
        public void AnimationOSDTest()
        {
            Assert.That(anim1.AnimID==animUUID1 && anim1.ObjectID == objUUID1 && anim1.SequenceNum ==1, "The Animation Constructor didn't set the fields correctly");
            OSD updateMessage = anim1.PackUpdateMessage();
            Assert.That(updateMessage is OSDMap, "Packed UpdateMessage isn't an OSDMap");
            OSDMap updateMap = (OSDMap) updateMessage;
            Assert.That(updateMap.ContainsKey("animation"), "Packed Message doesn't contain an animation element");
            Assert.That(updateMap.ContainsKey("object_id"), "Packed Message doesn't contain an object_id element");
            Assert.That(updateMap.ContainsKey("seq_num"), "Packed Message doesn't contain a seq_num element");
            Assert.That(updateMap["animation"].AsUUID() == animUUID1);
            Assert.That(updateMap["object_id"].AsUUID() == objUUID1);
            Assert.That(updateMap["seq_num"].AsInteger() == 1);

            Animation anim3 = new Animation(updateMap);

            Assert.That(anim3.ObjectID == anim1.ObjectID && anim3.AnimID == anim1.AnimID && anim3.SequenceNum == anim1.SequenceNum, "OSDMap Constructor failed to set the properties correctly.");

            anim3.UnpackUpdateMessage(anim2.PackUpdateMessage());

            Assert.That(anim3.ObjectID == objUUID2 && anim3.AnimID == animUUID2 && anim3.SequenceNum == 1, "Animation.UnpackUpdateMessage failed to set the properties correctly.");

            Animation anim4 = new Animation();
            anim4.AnimID = anim2.AnimID;
            anim4.ObjectID = anim2.ObjectID;
            anim4.SequenceNum = anim2.SequenceNum;

            Assert.That(anim4.ObjectID == objUUID2 && anim4.AnimID == animUUID2 && anim4.SequenceNum == 1, "void constructor and manual field population failed to set the properties correctly.");
        }
    }
}