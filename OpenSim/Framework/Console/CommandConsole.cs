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
using System.Xml;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;

namespace OpenSim.Framework.Console
{
    public delegate void CommandDelegate(string module, string[] cmd);

    public class Commands
    {
        /// <summary>
        /// Encapsulates a command that can be invoked from the console
        /// </summary>
        private class CommandInfo
        {
            /// <value>
            /// The module from which this command comes
            /// </value>
            public string module;
            
            /// <value>
            /// Whether the module is shared
            /// </value>
            public bool shared;
            
            /// <value>
            /// Very short BNF description
            /// </value>
            public string help_text;
            
            /// <value>
            /// Longer one line help text
            /// </value>
            public string long_help;
            
            /// <value>
            /// Full descriptive help for this command
            /// </value>
            public string descriptive_help;
            
            /// <value>
            /// The method to invoke for this command
            /// </value>
            public List<CommandDelegate> fn;
        }

        /// <value>
        /// Commands organized by keyword in a tree
        /// </value>
        private Dictionary<string, object> tree =
                new Dictionary<string, object>();

        /// <summary>
        /// Get help for the given help string
        /// </summary>
        /// <param name="helpParts">Parsed parts of the help string.  If empty then general help is returned.</param>
        /// <returns></returns>
        public List<string> GetHelp(string[] cmd)
        {
            List<string> help = new List<string>();
            List<string> helpParts = new List<string>(cmd);
            
            // Remove initial help keyword
            helpParts.RemoveAt(0);

            // General help
            if (helpParts.Count == 0)
            {
                help.AddRange(CollectHelp(tree));
                help.Sort();
            }
            else
            {
                help.AddRange(CollectHelp(helpParts));
            }

            return help;
        }
        
        /// <summary>
        /// See if we can find the requested command in order to display longer help
        /// </summary>
        /// <param name="helpParts"></param>
        /// <returns></returns>
        private List<string> CollectHelp(List<string> helpParts)
        {
            string originalHelpRequest = string.Join(" ", helpParts.ToArray());
            List<string> help = new List<string>();
            
            Dictionary<string, object> dict = tree;
            while (helpParts.Count > 0)
            {
                string helpPart = helpParts[0];
                
                if (!dict.ContainsKey(helpPart))
                    break;
                
                //m_log.Debug("Found {0}", helpParts[0]);
                
                if (dict[helpPart] is Dictionary<string, Object>)
                    dict = (Dictionary<string, object>)dict[helpPart]; 
                
                helpParts.RemoveAt(0);
            }
        
            // There was a command for the given help string
            if (dict.ContainsKey(String.Empty))
            {
                CommandInfo commandInfo = (CommandInfo)dict[String.Empty];
                help.Add(commandInfo.help_text);
                help.Add(commandInfo.long_help);
                help.Add(commandInfo.descriptive_help);
            }
            else
            {
                help.Add(string.Format("No help is available for {0}", originalHelpRequest));
            }
            
            return help;
        }

        private List<string> CollectHelp(Dictionary<string, object> dict)
        {
            List<string> result = new List<string>();

            foreach (KeyValuePair<string, object> kvp in dict)
            {
                if (kvp.Value is Dictionary<string, Object>)
                {
                    result.AddRange(CollectHelp((Dictionary<string, Object>)kvp.Value));
                }
                else
                {
                    if (((CommandInfo)kvp.Value).long_help != String.Empty)
                        result.Add(((CommandInfo)kvp.Value).help_text+" - "+
                                ((CommandInfo)kvp.Value).long_help);
                }
            }
            return result;
        }
        
        /// <summary>
        /// Add a command to those which can be invoked from the console.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="command"></param>
        /// <param name="help"></param>
        /// <param name="longhelp"></param>
        /// <param name="fn"></param>
        public void AddCommand(string module, bool shared, string command,
                string help, string longhelp, CommandDelegate fn)
        {
            AddCommand(module, shared, command, help, longhelp,
                    String.Empty, fn);
        }

        /// <summary>
        /// Add a command to those which can be invoked from the console.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="command"></param>
        /// <param name="help"></param>
        /// <param name="longhelp"></param>
        /// <param name="descriptivehelp"></param>
        /// <param name="fn"></param>
        public void AddCommand(string module, bool shared, string command,
                string help, string longhelp, string descriptivehelp,
                CommandDelegate fn)
        {
            string[] parts = Parser.Parse(command);

            Dictionary<string, Object> current = tree;
            
            foreach (string s in parts)
            {
                if (current.ContainsKey(s))
                {
                    if (current[s] is Dictionary<string, Object>)
                    {
                        current = (Dictionary<string, Object>)current[s];
                    }
                    else
                        return;
                }
                else
                {
                    current[s] = new Dictionary<string, Object>();
                    current = (Dictionary<string, Object>)current[s];
                }
            }

            CommandInfo info;

            if (current.ContainsKey(String.Empty))
            {
                info = (CommandInfo)current[String.Empty];
                if (!info.shared && !info.fn.Contains(fn))
                    info.fn.Add(fn);

                return;
            }
            
            info = new CommandInfo();
            info.module = module;
            info.shared = shared;
            info.help_text = help;
            info.long_help = longhelp;
            info.descriptive_help = descriptivehelp;
            info.fn = new List<CommandDelegate>();
            info.fn.Add(fn);
            current[String.Empty] = info;
        }

        public string[] FindNextOption(string[] cmd, bool term)
        {
            Dictionary<string, object> current = tree;

            int remaining = cmd.Length;

            foreach (string s in cmd)
            {
                remaining--;

                List<string> found = new List<string>();

                foreach (string opt in current.Keys)
                {
                    if (remaining > 0 && opt == s)
                    {
                        found.Clear();
                        found.Add(opt);
                        break;
                    }
                    if (opt.StartsWith(s))
                    {
                        found.Add(opt);
                    }
                }

                if (found.Count == 1 && (remaining != 0 || term))
                {
                    current = (Dictionary<string, object>)current[found[0]];
                }
                else if (found.Count > 0)
                {
                    return found.ToArray();
                }
                else
                {
                    break;
//                    return new string[] {"<cr>"};
                }
            }

            if (current.Count > 1)
            {
                List<string> choices = new List<string>();

                bool addcr = false;
                foreach (string s in current.Keys)
                {
                    if (s == String.Empty)
                    {
                        CommandInfo ci = (CommandInfo)current[String.Empty];
                        if (ci.fn.Count != 0)
                            addcr = true;
                    }
                    else
                        choices.Add(s);
                }
                if (addcr)
                    choices.Add("<cr>");
                return choices.ToArray();
            }

            if (current.ContainsKey(String.Empty))
                return new string[] { "Command help: "+((CommandInfo)current[String.Empty]).help_text};

            return new string[] { new List<string>(current.Keys)[0] };
        }

        public string[] Resolve(string[] cmd)
        {
            string[] result = cmd;
            int index = -1;

            Dictionary<string, object> current = tree;

            foreach (string s in cmd)
            {
                index++;

                List<string> found = new List<string>();

                foreach (string opt in current.Keys)
                {
                    if (opt == s)
                    {
                        found.Clear();
                        found.Add(opt);
                        break;
                    }
                    if (opt.StartsWith(s))
                    {
                        found.Add(opt);
                    }
                }

                if (found.Count == 1)
                {
                    result[index] = found[0];
                    current = (Dictionary<string, object>)current[found[0]];
                }
                else if (found.Count > 0)
                {
                    return new string[0];
                }
                else
                {
                    break;
                }
            }

            if (current.ContainsKey(String.Empty))
            {
                CommandInfo ci = (CommandInfo)current[String.Empty];
                if (ci.fn.Count == 0)
                    return new string[0];
                foreach (CommandDelegate fn in ci.fn)
                {
                    if (fn != null)
                        fn(ci.module, result);
                    else
                        return new string[0];
                }
                return result;
            }
            
            return new string[0];
        }

        public XmlElement GetXml(XmlDocument doc)
        {
            CommandInfo help = (CommandInfo)((Dictionary<string, object>)tree["help"])[String.Empty];
            ((Dictionary<string, object>)tree["help"]).Remove(string.Empty);
            if (((Dictionary<string, object>)tree["help"]).Count == 0)
                tree.Remove("help");

            CommandInfo quit = (CommandInfo)((Dictionary<string, object>)tree["quit"])[String.Empty];
            ((Dictionary<string, object>)tree["quit"]).Remove(string.Empty);
            if (((Dictionary<string, object>)tree["quit"]).Count == 0)
                tree.Remove("quit");

            XmlElement root = doc.CreateElement("", "HelpTree", "");

            ProcessTreeLevel(tree, root, doc);

            if (!tree.ContainsKey("help"))
                tree["help"] = (object) new Dictionary<string, object>();
            ((Dictionary<string, object>)tree["help"])[String.Empty] = help;

            if (!tree.ContainsKey("quit"))
                tree["quit"] = (object) new Dictionary<string, object>();
            ((Dictionary<string, object>)tree["quit"])[String.Empty] = quit;

            return root;
        }

        private void ProcessTreeLevel(Dictionary<string, object> level, XmlElement xml, XmlDocument doc)
        {
            foreach (KeyValuePair<string, object> kvp in level)
            {
                if (kvp.Value is Dictionary<string, Object>)
                {
                    XmlElement next = doc.CreateElement("", "Level", "");
                    next.SetAttribute("Name", kvp.Key);

                    xml.AppendChild(next);

                    ProcessTreeLevel((Dictionary<string, object>)kvp.Value, next, doc);
                }
                else
                {
                    CommandInfo c = (CommandInfo)kvp.Value;

                    XmlElement cmd = doc.CreateElement("", "Command", "");

                    XmlElement e;

                    e = doc.CreateElement("", "Module", "");
                    cmd.AppendChild(e);
                    e.AppendChild(doc.CreateTextNode(c.module));

                    e = doc.CreateElement("", "Shared", "");
                    cmd.AppendChild(e);
                    e.AppendChild(doc.CreateTextNode(c.shared.ToString()));

                    e = doc.CreateElement("", "HelpText", "");
                    cmd.AppendChild(e);
                    e.AppendChild(doc.CreateTextNode(c.help_text));

                    e = doc.CreateElement("", "LongHelp", "");
                    cmd.AppendChild(e);
                    e.AppendChild(doc.CreateTextNode(c.long_help));

                    e = doc.CreateElement("", "Description", "");
                    cmd.AppendChild(e);
                    e.AppendChild(doc.CreateTextNode(c.descriptive_help));

                    xml.AppendChild(cmd);
                }
            }
        }

        public void FromXml(XmlElement root, CommandDelegate fn)
        {
            CommandInfo help = (CommandInfo)((Dictionary<string, object>)tree["help"])[String.Empty];
            ((Dictionary<string, object>)tree["help"]).Remove(string.Empty);
            if (((Dictionary<string, object>)tree["help"]).Count == 0)
                tree.Remove("help");

            CommandInfo quit = (CommandInfo)((Dictionary<string, object>)tree["quit"])[String.Empty];
            ((Dictionary<string, object>)tree["quit"]).Remove(string.Empty);
            if (((Dictionary<string, object>)tree["quit"]).Count == 0)
                tree.Remove("quit");

            tree.Clear();

            ReadTreeLevel(tree, root, fn);

            if (!tree.ContainsKey("help"))
                tree["help"] = (object) new Dictionary<string, object>();
            ((Dictionary<string, object>)tree["help"])[String.Empty] = help;

            if (!tree.ContainsKey("quit"))
                tree["quit"] = (object) new Dictionary<string, object>();
            ((Dictionary<string, object>)tree["quit"])[String.Empty] = quit;
        }

        private void ReadTreeLevel(Dictionary<string, object> level, XmlNode node, CommandDelegate fn)
        {
            Dictionary<string, object> next;
            string name;

            XmlNodeList nodeL = node.ChildNodes;
            XmlNodeList cmdL;
            CommandInfo c;

            foreach (XmlNode part in nodeL)
            {
                switch (part.Name)
                {
                case "Level":
                    name = ((XmlElement)part).GetAttribute("Name");
                    next = new Dictionary<string, object>();
                    level[name] = next;
                    ReadTreeLevel(next, part, fn);
                    break;
                case "Command":
                    cmdL = part.ChildNodes;
                    c = new CommandInfo();
                    foreach (XmlNode cmdPart in cmdL)
                    {
                        switch (cmdPart.Name)
                        {
                        case "Module":
                            c.module = cmdPart.InnerText;
                            break;
                        case "Shared":
                            c.shared = Convert.ToBoolean(cmdPart.InnerText);
                            break;
                        case "HelpText":
                            c.help_text = cmdPart.InnerText;
                            break;
                        case "LongHelp":
                            c.long_help = cmdPart.InnerText;
                            break;
                        case "Description":
                            c.descriptive_help = cmdPart.InnerText;
                            break;
                        }
                    }
                    c.fn = new List<CommandDelegate>();
                    c.fn.Add(fn);
                    level[String.Empty] = c;
                    break;
                }
            }
        }
    }

    public class Parser
    {
        public static string[] Parse(string text)
        {
            List<string> result = new List<string>();

            int index;

            string[] unquoted = text.Split(new char[] {'"'});

            for (index = 0 ; index < unquoted.Length ; index++)
            {
                if (index % 2 == 0)
                {
                    string[] words = unquoted[index].Split(new char[] {' '});
                    foreach (string w in words)
                    {
                        if (w != String.Empty)
                            result.Add(w);
                    }
                }
                else
                {
                    result.Add(unquoted[index]);
                }
            }

            return result.ToArray();
        }
    }

    // A console that processes commands internally
    //
    public class CommandConsole : ConsoleBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Commands Commands = new Commands();

        public CommandConsole(string defaultPrompt) : base(defaultPrompt)
        {
            Commands.AddCommand("console", false, "help", "help [<command>]", 
                    "Get general command list or more detailed help on a specific command", Help);
        }

        private void Help(string module, string[] cmd)
        {
            List<string> help = Commands.GetHelp(cmd);

            foreach (string s in help)
                Output(s);
        }

        public void Prompt()
        {
            string line = ReadLine(m_defaultPrompt + "# ", true, true);

            if (line != String.Empty)
            {
                m_log.Info("[CONSOLE] Invalid command");
            }
        }

        public void RunCommand(string cmd)
        {
            string[] parts = Parser.Parse(cmd);
            Commands.Resolve(parts);
        }

        public override string ReadLine(string p, bool isCommand, bool e)
        {
            System.Console.Write("{0}", p);
            string cmdinput = System.Console.ReadLine();

            if (isCommand)
            {
                string[] cmd = Commands.Resolve(Parser.Parse(cmdinput));

                if (cmd.Length != 0)
                {
                    int i;

                    for (i=0 ; i < cmd.Length ; i++)
                    {
                        if (cmd[i].Contains(" "))
                            cmd[i] = "\"" + cmd[i] + "\"";
                    }
                    return String.Empty;
                }
            }
            return cmdinput;
        }
    }
}
