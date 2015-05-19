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
using System.Threading;

using Nini.Config;
using log4net;
using NUnit.Framework;

using OpenSim.Server;

namespace Robust.Tests
{
    [SetUpFixture]
    public class DemonServer : OpenSimServer
    {
        private Thread m_demon;

        public static string Address = "http://localhost:8888";

        [SetUp]
        public void StartDemon()
        {
            if (File.Exists("Robust.Tests.log"))
                File.Delete("Robust.Tests.log");

            Console.WriteLine("**** Starting demon Robust server ****");
            m_demon = new Thread( () => Main(new string[] {"-inifile=Robust.Tests.ini"}));
            m_demon.Start();
            // Give some time for the server to instantiate all services
            Thread.Sleep(3000);
            Console.WriteLine("**** Setup Finished ****");
        }

        [TearDown]
        public void StopDemon()
        {
            Console.WriteLine("**** Killing demon Robust Server ****");
            m_Server.Shutdown();
        }
    }
}
