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
    public class BasicUserTest
    {
        public UserDataBase db;
        public UUID uuid1;
        public UUID uuid2;
        public UUID uuid3;

        public void SuperInit()
        {
            try
            {
                log4net.Config.XmlConfigurator.Configure();
            }
            catch (Exception)
            {
                // I don't care, just leave log4net off
            }

            uuid1 = UUID.Random();
            uuid2 = UUID.Random();
            uuid3 = UUID.Random();
        }

        [Test]
        public void T001_LoadEmpty()
        {
            Assert.That(db.GetUserByUUID(UUID.Zero), Is.Null);
            Assert.That(db.GetUserByUUID(uuid1), Is.Null);
            Assert.That(db.GetUserByUUID(uuid2), Is.Null);
            Assert.That(db.GetUserByUUID(uuid3), Is.Null);
            Assert.That(db.GetUserByUUID(UUID.Random()), Is.Null);
        }
    }
}