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
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using Nini.Config;

namespace OpenSim.Framework.Console
{
    /// <summary>
    /// This is a Fake console that's used when setting up the Scene in Unit Tests
    /// Don't use this except for Unit Testing or you're in for a world of hurt when the
    /// sim gets to ReadLine
    /// </summary>
    public class MockConsole : ICommandConsole
    {
#pragma warning disable 0067
        public event OnOutputDelegate OnOutput;
#pragma warning restore 0067

        private MockCommands m_commands = new MockCommands();

        public ICommands Commands { get { return m_commands; } }

        public string DefaultPrompt { get; set; }

        public void Prompt() {}

        public void RunCommand(string cmd) {}

        public string ReadLine(string p, bool isCommand, bool e) { return ""; }

        public IScene ConsoleScene {
            get { return null; }
            set {}
        }

        public void Output(string format, string level, params object[] components) {}

        public string Prompt(string p) { return ""; }
        public string Prompt(string p, string def, List<char> excludedCharacters, bool echo) { return ""; }

        public string Prompt(string prompt, string defaultresponse, List<string> options) { return ""; }

        public string PasswdPrompt(string p) { return ""; }

        public void ReadConfig(IConfigSource configSource) { }
    }

    public class MockCommands : ICommands
    {
        public void FromXml(XmlElement root, CommandDelegate fn) {}
        public List<string> GetHelp(string[] cmd) { return null; }
        public void AddCommand(string module, bool shared, string command, string help, string longhelp, CommandDelegate fn) {}
        public void AddCommand(string module, bool shared, string command, string help, string longhelp, string descriptivehelp, CommandDelegate fn) {}
        public string[] FindNextOption(string[] cmd, bool term) { return null; }
        public bool HasCommand(string cmd) { return false; }
        public string[] Resolve(string[] cmd) { return null; }
        public XmlElement GetXml(XmlDocument doc) { return null; }
    }
}
