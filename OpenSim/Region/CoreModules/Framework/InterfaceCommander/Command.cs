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
using OpenSim.Region.Framework.Interfaces;
using OpenMetaverse;

namespace OpenSim.Region.CoreModules.Framework.InterfaceCommander
{
    /// <summary>
    /// A single function call encapsulated in a class which enforces arguments when passing around as Object[]'s.
    /// Used for console commands and script API generation
    /// </summary>
    public class Command : ICommand
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
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

        public string ShortHelp()
        {
            string help = m_name;

            foreach (CommandArgument arg in m_args)
            {
                help += " <" + arg.Name + ">";
            }

            return help;
        }

        public void ShowConsoleHelp()
        {
            Console.WriteLine("== " + Name + " ==");
            Console.WriteLine(m_help);
            Console.WriteLine("= Parameters =");
            foreach (CommandArgument arg in m_args)
            {
                Console.WriteLine("* " + arg.Name + " (" + arg.ArgumentType + ")");
                Console.WriteLine("\t" + arg.HelpText);
            }
        }

        public void Run(Object[] args)
        {
            Object[] cleanArgs = new Object[m_args.Count];

            if (args.Length < cleanArgs.Length)
            {
                Console.WriteLine("ERROR: Missing " + (cleanArgs.Length - args.Length) + " argument(s)");
                ShowConsoleHelp();
                return;
            }
            if (args.Length > cleanArgs.Length)
            {
                Console.WriteLine("ERROR: Too many arguments for this command. Type '<module> <command> help' for help.");
                return;
            }

            int i = 0;
            foreach (Object arg in args)
            {
                if (string.IsNullOrEmpty(arg.ToString()))
                {
                    Console.WriteLine("ERROR: Empty arguments are not allowed");
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
                        case "Float":
                            m_args[i].ArgumentValue = float.Parse(arg.ToString(), OpenSim.Framework.Culture.NumberFormatInfo);
                            break;
                        case "Double":
                            m_args[i].ArgumentValue = Double.Parse(arg.ToString(), OpenSim.Framework.Culture.NumberFormatInfo);
                            break;
                        case "Boolean":
                            m_args[i].ArgumentValue = Boolean.Parse(arg.ToString());
                            break;
                        case "UUID":
                            m_args[i].ArgumentValue = UUID.Parse(arg.ToString());
                            break;
                        default:
                            Console.WriteLine("ERROR: Unknown desired type for argument " + m_args[i].Name + " on command " + m_name);
                            break;
                    }
                }
                catch (FormatException)
                {
                    Console.WriteLine("ERROR: Argument number " + (i + 1) +
                                " (" + m_args[i].Name + ") must be a valid " +
                                m_args[i].ArgumentType.ToLower() + ".");
                    return;
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
}
