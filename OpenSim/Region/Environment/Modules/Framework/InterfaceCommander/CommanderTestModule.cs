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
using Nini.Config;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.Framework.InterfaceCommander;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Framework.InterfaceCommander
{
    public class CommanderTestModule : IRegionModule, ICommandableModule
    {
        private readonly Commander m_commander = new Commander("CommanderTest");
        private Scene m_scene;

        #region ICommandableModule Members

        public ICommander CommandInterface
        {
            get { throw new NotImplementedException(); }
        }

        #endregion

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
        }

        public void PostInitialise()
        {
            Command testCommand = new Command("hello", CommandIntentions.COMMAND_STATISTICAL, InterfaceHelloWorld, "Says a simple debugging test string");
            testCommand.AddArgument("world", "Write world here", "string");

            m_commander.RegisterCommand("hello", testCommand);

            // Register me
            m_scene.RegisterModuleCommander("commandertest", m_commander);
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "CommanderTestModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion

        private void InterfaceHelloWorld(Object[] args)
        {
            Console.WriteLine("Hello World");
        }
    }
}