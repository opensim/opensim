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
using System.Reflection;
using System.Text;
using log4net;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;

namespace OpenSim.Region.Environment.Modules.Framework.InterfaceCommander
{
    /// <summary>
    /// A single function call encapsulated in a class which enforces arguments when passing around as Object[]'s.
    /// Used for console commands and script API generation
    /// </summary>
    public class Command : ICommand
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private List<CommandArgument> m_args = new List<CommandArgument>();

        private Action<object[]> m_command;
        private string m_help;
        private string m_name;
        private CommandIntentions m_intentions; //A permission type system could implement this and know what a command intends on doing.

        public Command(string name, CommandIntentions intention, Action<Object[]> command, string help)
        {
            m_name = name;
            m_command = command;
            m_help = help;
            m_intentions = intention;
        }

        #region ICommand Members

        public void AddArgument(string name, string helptext, string type)
        {
            m_args.Add(new CommandArgument(name, helptext, type));
        }

        public string Name
        {
            get { return m_name; }
        }

        public CommandIntentions Intentions
        {
            get { return m_intentions; }
        }

        public string Help
        {
            get { return m_help; }
        }

        public Dictionary<string, string> Arguments
        {
            get
            {
                Dictionary<string, string> tmp = new Dictionary<string, string>();
                foreach (CommandArgument arg in m_args)
                {
                    tmp.Add(arg.Name, arg.ArgumentType);
                }
                return tmp;
            }
        }

        public void ShowConsoleHelp()
        {
            m_log.Info("== " + Name + " ==");
            m_log.Info(m_help);
            m_log.Info("= Parameters =");
            foreach (CommandArgument arg in m_args)
            {
                m_log.Info("* " + arg.Name + " (" + arg.ArgumentType + ")");
                m_log.Info("\t" + arg.HelpText);
            }
        }

        public void Run(Object[] args)
        {
            Object[] cleanArgs = new Object[m_args.Count];

            if (args.Length < cleanArgs.Length)
            {
                m_log.Error("Missing " + (cleanArgs.Length - args.Length) + " argument(s)");
                ShowConsoleHelp();
                return;
            }
            if (args.Length > cleanArgs.Length)
            {
                m_log.Error("Too many arguments for this command. Type '<module> <command> help' for help.");
                return;
            }

            int i = 0;
            foreach (Object arg in args)
            {
                if (string.IsNullOrEmpty(arg.ToString()))
                {
                    m_log.Error("Empty arguments are not allowed");
                    return;
                }
                try
                {
                    switch (m_args[i].ArgumentType)
                    {
                        case "String":
                            m_args[i].ArgumentValue = arg.ToString();
                            break;
                        case "Integer":
                            m_args[i].ArgumentValue = Int32.Parse(arg.ToString());
                            break;
                        case "Double":
                            m_args[i].ArgumentValue = Double.Parse(arg.ToString());
                            break;
                        case "Boolean":
                            m_args[i].ArgumentValue = Boolean.Parse(arg.ToString());
                            break;
                        default:
                            m_log.Error("Unknown desired type for argument " + m_args[i].Name + " on command " + m_name);
                            break;
                    }
                }
                catch (FormatException)
                {
                    m_log.Error("Argument number " + (i + 1) +
                                " (" + m_args[i].Name + ") must be a valid " +
                                m_args[i].ArgumentType.ToLower() + ".");
                }
                cleanArgs[i] = m_args[i].ArgumentValue;

                i++;
            }

            m_command.Invoke(cleanArgs);
        }

        #endregion
    }

    /// <summary>
    /// A single command argument, contains name, type and at runtime, value.
    /// </summary>
    public class CommandArgument
    {
        private string m_help;
        private string m_name;
        private string m_type;
        private Object m_val;

        public CommandArgument(string name, string help, string type)
        {
            m_name = name;
            m_help = help;
            m_type = type;
        }

        public string Name
        {
            get { return m_name; }
        }

        public string HelpText
        {
            get { return m_help; }
        }

        public string ArgumentType
        {
            get { return m_type; }
        }

        public Object ArgumentValue
        {
            get { return m_val; }
            set { m_val = value; }
        }
    }

    /// <summary>
    /// A class to enable modules to register console and script commands, which enforces typing and valid input.
    /// </summary>
    public class Commander : ICommander
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Dictionary<string, ICommand> m_commands = new Dictionary<string, ICommand>();
        private string m_name;

        public Commander(string name)
        {
            m_name = name;
        }

        public Dictionary<string, ICommand> Commands
        {
            get { return m_commands; }
        }

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
            string classSrc = "\n\tpublic class " + m_name + " {\n";
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
                if (function != "help")
                    m_log.Error("Invalid command - No such command exists");
                if (function == "api")
                    m_log.Info(GenerateRuntimeAPI());
                ShowConsoleHelp();
            }
        }

        #endregion

        private void ShowConsoleHelp()
        {
            m_log.Info("===" + m_name + "===");
            foreach (ICommand com in m_commands.Values)
            {
                m_log.Info("* " + com.Name + " - " + com.Help);
            }
        }

        private string EscapeRuntimeAPICommand(string command)
        {
            command = command.Replace('-', '_');
            StringBuilder tmp = new StringBuilder(command);
            tmp[0] = tmp[0].ToString().ToUpper().ToCharArray()[0];

            return tmp.ToString();
        }
    }
}