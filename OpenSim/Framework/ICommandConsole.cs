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

using Nini.Config;
using System;
using System.Collections.Generic;
using System.Xml;

namespace OpenSim.Framework
{
    public delegate void CommandDelegate(string module, string[] cmd);

    public interface ICommands
    {
        void FromXml(XmlElement root, CommandDelegate fn);

        /// <summary>
        /// Get help for the given help string
        /// </summary>
        /// <param name="cmd">Parsed parts of the help string.  If empty then general help is returned.</param>
        /// <returns></returns>
        List<string> GetHelp(string[] cmd);

        /// <summary>
        /// Add a command to those which can be invoked from the console.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="command"></param>
        /// <param name="help"></param>
        /// <param name="longhelp"></param>
        /// <param name="fn"></param>
        void AddCommand(string module, bool shared, string command, string help, string longhelp, CommandDelegate fn);

        /// <summary>
        /// Add a command to those which can be invoked from the console.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="command"></param>
        /// <param name="help"></param>
        /// <param name="longhelp"></param>
        /// <param name="descriptivehelp"></param>
        /// <param name="fn"></param>
        void AddCommand(string module, bool shared, string command,
                string help, string longhelp, string descriptivehelp,
                CommandDelegate fn);

        /// <summary>
        /// Has the given command already been registered?
        /// </summary>
        /// <returns></returns>
        /// <param name="command">Command.</param>
        bool HasCommand(string command);

        string[] FindNextOption(string[] command, bool term);

        string[] Resolve(string[] command);

        XmlElement GetXml(XmlDocument doc);
    }

    public delegate void OnOutputDelegate(string message);

    public interface ICommandConsole : IConsole
    {
        event OnOutputDelegate OnOutput;

        ICommands Commands { get; }

        /// <summary>
        /// The default prompt text.
        /// </summary>
        string DefaultPrompt { get; set; }

        /// <summary>
        /// Display a command prompt on the console and wait for user input
        /// </summary>
        void Prompt();

        void RunCommand(string cmd);

        string ReadLine(string p, bool isCommand, bool e);

        void ReadConfig(IConfigSource configSource);
    }
}
