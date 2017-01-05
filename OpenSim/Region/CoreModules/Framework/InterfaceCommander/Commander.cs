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
using System.Text;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;

namespace OpenSim.Region.CoreModules.Framework.InterfaceCommander
{
    /// <summary>
    /// A class to enable modules to register console and script commands, which enforces typing and valid input.
    /// </summary>
    public class Commander : ICommander
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <value>
        /// Used in runtime class generation
        /// </summary>
        private string m_generatedApiClassName;

        public string Name
        {
            get { return m_name; }
        }
        private string m_name;

        public string Help
        {
            get
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("=== " + m_name + " ===");

                foreach (ICommand com in m_commands.Values)
                {
                    sb.AppendLine("* " + Name + " " + com.Name + " - " + com.Help);
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="name"></param>
        public Commander(string name)
        {
            m_name = name;
            m_generatedApiClassName = m_name[0].ToString().ToUpper();

            if (m_name.Length > 1)
                m_generatedApiClassName += m_name.Substring(1);
        }

        public Dictionary<string, ICommand> Commands
        {
            get { return m_commands; }
        }
        private Dictionary<string, ICommand> m_commands = new Dictionary<string, ICommand>();

        #region ICommander Members

        public void RegisterCommand(string commandName, ICommand command)
        {
            m_commands[commandName] = command;
        }

        /// <summary>
        /// Generates a runtime C# class which can be compiled and inserted via reflection to enable modules to register new script commands
        /// </summary>
        /// <returns>Returns C# source code to create a binding</returns>
        public string GenerateRuntimeAPI()
        {
            string classSrc = "\n\tpublic class " + m_generatedApiClassName + " {\n";
            foreach (ICommand com in m_commands.Values)
            {
                classSrc += "\tpublic void " + EscapeRuntimeAPICommand(com.Name) + "( ";
                foreach (KeyValuePair<string, string> arg in com.Arguments)
                {
                    classSrc += arg.Value + " " + Util.Md5Hash(arg.Key) + ",";
                }
                classSrc = classSrc.Remove(classSrc.Length - 1); // Delete the last comma
                classSrc += " )\n\t{\n";
                classSrc += "\t\tObject[] args = new Object[" + com.Arguments.Count.ToString() + "];\n";
                int i = 0;
                foreach (KeyValuePair<string, string> arg in com.Arguments)
                {
                    classSrc += "\t\targs[" + i.ToString() + "] = " + Util.Md5Hash(arg.Key) + "  " + ";\n";
                    i++;
                }
                classSrc += "\t\tGetCommander(\"" + m_name + "\").Run(\"" + com.Name + "\", args);\n";
                classSrc += "\t}\n";
            }
            classSrc += "}\n";

            return classSrc;
        }

        /// <summary>
        /// Runs a specified function with attached arguments
        /// *** <b>DO NOT CALL DIRECTLY.</b> ***
        /// Call ProcessConsoleCommand instead if handling human input.
        /// </summary>
        /// <param name="function">The function name to call</param>
        /// <param name="args">The function parameters</param>
        public void Run(string function, object[] args)
        {
            m_commands[function].Run(args);
        }

        public void ProcessConsoleCommand(string function, string[] args)
        {
            if (m_commands.ContainsKey(function))
            {
                if (args.Length > 0 && args[0] == "help")
                {
                    m_commands[function].ShowConsoleHelp();
                }
                else
                {
                    m_commands[function].Run(args);
                }
            }
            else
            {
                if (function == "api")
                {
                    m_log.Info(GenerateRuntimeAPI());
                }
                else
                {
                    if (function != "help")
                        Console.WriteLine("ERROR: Invalid command - No such command exists");

                    Console.Write(Help);
                }
            }
        }

        #endregion

        private string EscapeRuntimeAPICommand(string command)
        {
            command = command.Replace('-', '_');
            StringBuilder tmp = new StringBuilder(command);
            tmp[0] = tmp[0].ToString().ToUpper().ToCharArray()[0];

            return tmp.ToString();
        }
    }
}