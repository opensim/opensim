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
using System.Linq;
using System.Text.RegularExpressions;
using Nini.Config;

namespace OpenSim.Framework.Console
{
    public class Commands : ICommands
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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

        public const string GeneralHelpText
            = "To enter an argument that contains spaces, surround the argument with double quotes.\nFor example, show object name \"My long object name\"\n";

        public const string ItemHelpText
            = """
              For more information, type 'help all' to get a list of all commands,
                            or type help <item>' where <item> is one of the following:
              """;

        /// <value>
        /// Commands organized by keyword in a tree
        /// </value>
        private Dictionary<string, object> tree =
                new Dictionary<string, object>();

        /// <summary>
        /// Commands organized by module
        /// </summary>
        private readonly Dictionary<string, List<CommandInfo>> m_modulesCommands = new Dictionary<string, List<CommandInfo>>();

        /// <summary>
        /// Get help for the given help string
        /// </summary>
        /// <param name="helpParts">Parsed parts of the help string.  If empty then general help is returned.</param>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public List<string> GetHelp(string[] cmd)
        {
            List<string> help = [];
            var helpParts = new List<string>(cmd);

            // Remove initial help keyword
            helpParts.RemoveAt(0);

            help.Add(""); // Will become a newline.

            switch (helpParts.Count)
            {
                // General help
                case 0:
                    help.Add(GeneralHelpText);
                    help.Add(ItemHelpText);
                    help.AddRange(CollectModulesHelp());
                    break;
                case 1 when helpParts[0] == "all":
                    help.AddRange(CollectAllCommandsHelp());
                    break;
                default:
                    help.AddRange(CollectHelp(helpParts));
                    break;
            }

            help.Add(""); // Will become a newline.

            return help;
        }

        /// <summary>
        /// Collects the help from all commands and return in alphabetical order.
        /// </summary>
        /// <returns></returns>
        private List<string> CollectAllCommandsHelp()
        {
            List<string> help = [];

            lock (m_modulesCommands)
            {
                foreach (var ourHelpText in m_modulesCommands.Values.Select(commands => commands.ConvertAll(c => $"{c.help_text} - {c.long_help}")))
                {
                    help.AddRange(ourHelpText);
                }
            }

            help.Sort();

            return help;
        }

        /// <summary>
        /// See if we can find the requested command in order to display longer help
        /// </summary>
        /// <param name="helpParts"></param>
        /// <returns></returns>
        private List<string> CollectHelp(List<string> helpParts)
        {
            var originalHelpRequest = string.Join(" ", helpParts.ToArray());
            List<string> help = [];

            // Check modules first to see if we just need to display a list of those commands
            if (TryCollectModuleHelp(originalHelpRequest, help))
            {
                help.Insert(0, ItemHelpText);
                return help;
            }

            var dict = tree;
            while (helpParts.Count > 0)
            {
                var helpPart = helpParts[0];

                if (!dict.ContainsKey(helpPart))
                    break;

                //m_log.Debug("Found {0}", helpParts[0]);

                if (dict[helpPart] is Dictionary<string, Object>)
                    dict = (Dictionary<string, object>)dict[helpPart];

                helpParts.RemoveAt(0);
            }

            // There was a command for the given help string
            if (dict.TryGetValue(string.Empty, out var value))
            {
                var commandInfo = (CommandInfo)value;
                help.Add(commandInfo.help_text);
                help.Add(commandInfo.long_help);

                var descriptiveHelp = commandInfo.descriptive_help;

                // If we do have some descriptive help then insert a spacing line before for readability.
                if (descriptiveHelp != string.Empty)
                    help.Add(string.Empty);

                help.Add(commandInfo.descriptive_help);
            }
            else
            {
                help.Add($"No help is available for {originalHelpRequest}");
            }

            return help;
        }

        /// <summary>
        /// Try to collect help for the given module if that module exists.
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="helpText"></param>
        /// <returns>true if there was the module existed, false otherwise.</returns>
        private bool TryCollectModuleHelp(string moduleName, List<string> helpText)
        {
            lock (m_modulesCommands)
            {
                foreach (var ourHelpText in 
                         from key in m_modulesCommands.Keys 
                         where moduleName.Equals(key, StringComparison.CurrentCultureIgnoreCase)
                         select m_modulesCommands[key] 
                         into commands 
                         select commands.ConvertAll(c => $"{c.help_text} - {c.long_help}"))
                {
                    ourHelpText.Sort();
                    helpText.AddRange(ourHelpText);

                    return true;
                }

                return false;
            }
        }

        private List<string> CollectModulesHelp()
        {
            lock (m_modulesCommands)
            {
                var helpText = new List<string>(m_modulesCommands.Keys);
                helpText.Sort();
                return helpText;
            }
        }

//        private List<string> CollectHelp(Dictionary<string, object> dict)
//        {
//            List<string> result = new List<string>();
//
//            foreach (KeyValuePair<string, object> kvp in dict)
//            {
//                if (kvp.Value is Dictionary<string, Object>)
//                {
//                    result.AddRange(CollectHelp((Dictionary<string, Object>)kvp.Value));
//                }
//                else
//                {
//                    if (((CommandInfo)kvp.Value).long_help != String.Empty)
//                        result.Add(((CommandInfo)kvp.Value).help_text+" - "+
//                                ((CommandInfo)kvp.Value).long_help);
//                }
//            }
//            return result;
//        }

        /// <summary>
        /// Add a command to those which can be invoked from the console.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="shared"></param>
        /// <param name="command"></param>
        /// <param name="help"></param>
        /// <param name="longhelp"></param>
        /// <param name="fn"></param>
        public void AddCommand(string module, bool shared, string command,
                string help, string longhelp, CommandDelegate fn)
        {
            AddCommand(module, shared, command, help, longhelp, string.Empty, fn);
        }

        /// <summary>
        /// Add a command to those which can be invoked from the console.
        /// </summary>
        /// <param name="module"></param>
        /// <param name="shared"></param>
        /// <param name="command"></param>
        /// <param name="help"></param>
        /// <param name="longhelp"></param>
        /// <param name="descriptivehelp"></param>
        /// <param name="fn"></param>
        public void AddCommand(string module, bool shared, string command,
                string help, string longhelp, string descriptivehelp,
                CommandDelegate fn)
        {
            var parts = Parser.Parse(command);

            var current = tree;

            foreach (var part in parts)
            {
                if (current.TryGetValue(part, out object value))
                {
                    if (value is Dictionary<string, Object>)
                        current = (Dictionary<string, Object>)current[part];
                    else
                        return;
                }
                else
                {
                    current[part] = new Dictionary<string, Object>();
                    current = (Dictionary<string, Object>)current[part];
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

            info = new CommandInfo
            {
                module = module,
                shared = shared,
                help_text = help,
                long_help = longhelp,
                descriptive_help = descriptivehelp,
                fn =
                [
                    fn
                ]
            };
            current[string.Empty] = info;

            // Now add command to modules dictionary
            lock (m_modulesCommands)
            {
                List<CommandInfo> commands;
                if (m_modulesCommands.TryGetValue(module, out var modulesCommand))
                {
                    commands = modulesCommand;
                }
                else
                {
                    commands = [];
                    m_modulesCommands[module] = commands;
                }

//                m_log.DebugFormat("[COMMAND CONSOLE]: Adding to category {0} command {1}", module, command);
                commands.Add(info);
            }
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

                var addcr = false;
                foreach (var s in current.Keys)
                {
                    if (s.Length == 0)
                    {
                        var ci = (CommandInfo)current[string.Empty];
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

            return current.TryGetValue(string.Empty, out var value) ? ["Command help: "+((CommandInfo)value).help_text]
                :
            [
                new List<string>(current.Keys)[0]
            ];
        }

        private CommandInfo ResolveCommand(string[] cmd, out string[] result)
        {
            result = cmd;
            var index = -1;

            var current = tree;

            foreach (var s in cmd)
            {
                index++;

                var found = new List<string>();

                foreach (var opt in current.Keys)
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
                    return null;
                }
                else
                {
                    break;
                }
            }

            if (current.TryGetValue(string.Empty, out object value))
                return (CommandInfo)value;

            return null;
        }

        public bool HasCommand(string command)
        {
            return ResolveCommand(Parser.Parse(command), out _) != null;
        }

        public string[] Resolve(string[] cmd)
        {
            var ci = ResolveCommand(cmd, out var result);

            if (ci == null || ci.fn.Count == 0)
                return [];

            foreach (var fn in ci.fn)
            {
                if (fn != null)
                    fn(ci.module, result);
                else
                    return [];
            }

            return result;
        }

        public XmlElement GetXml(XmlDocument doc)
        {
            var help = (CommandInfo)((Dictionary<string, object>)tree["help"])[string.Empty];
            ((Dictionary<string, object>)tree["help"]).Remove(string.Empty);
            if (((Dictionary<string, object>)tree["help"]).Count == 0)
                tree.Remove("help");

            var quit = (CommandInfo)((Dictionary<string, object>)tree["quit"])[string.Empty];
            ((Dictionary<string, object>)tree["quit"]).Remove(string.Empty);
            if (((Dictionary<string, object>)tree["quit"]).Count == 0)
                tree.Remove("quit");

            var root = doc.CreateElement("", "HelpTree", "");

            ProcessTreeLevel(tree, root, doc);

            if (!tree.ContainsKey("help"))
                tree["help"] = new Dictionary<string, object>();
            ((Dictionary<string, object>)tree["help"])[string.Empty] = help;

            if (!tree.ContainsKey("quit"))
                tree["quit"] = new Dictionary<string, object>();
            ((Dictionary<string, object>)tree["quit"])[string.Empty] = quit;

            return root;
        }

        private static void ProcessTreeLevel(Dictionary<string, object> level, XmlElement xml, XmlDocument doc)
        {
            foreach (var kvp in level)
            {
                if (kvp.Value is Dictionary<string, object> value)
                {
                    var next = doc.CreateElement("", "Level", "");
                    next.SetAttribute("Name", kvp.Key);

                    xml.AppendChild(next);

                    ProcessTreeLevel(value, next, doc);
                }
                else
                {
                    var c = (CommandInfo)kvp.Value;

                    var cmd = doc.CreateElement("", "Command", "");

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
            var help = (CommandInfo)((Dictionary<string, object>)tree["help"])[string.Empty];
            ((Dictionary<string, object>)tree["help"]).Remove(string.Empty);
            if (((Dictionary<string, object>)tree["help"]).Count == 0)
                tree.Remove("help");

            var quit = (CommandInfo)((Dictionary<string, object>)tree["quit"])[string.Empty];
            ((Dictionary<string, object>)tree["quit"]).Remove(string.Empty);
            if (((Dictionary<string, object>)tree["quit"]).Count == 0)
                tree.Remove("quit");

            tree.Clear();

            ReadTreeLevel(tree, root, fn);

            if (!tree.ContainsKey("help"))
                tree["help"] = new Dictionary<string, object>();
            ((Dictionary<string, object>)tree["help"])[string.Empty] = help;

            if (!tree.ContainsKey("quit"))
                tree["quit"] = new Dictionary<string, object>();
            ((Dictionary<string, object>)tree["quit"])[string.Empty] = quit;
        }

        private static void ReadTreeLevel(Dictionary<string, object> level, XmlNode node, CommandDelegate fn)
        {
            var nodeL = node.ChildNodes;

            foreach (XmlNode part in nodeL)
            {
                switch (part.Name)
                {
                    case "Level":
                        var name = ((XmlElement)part).GetAttribute("Name");
                        var next = new Dictionary<string, object>();
                        level[name] = next;
                        ReadTreeLevel(next, part, fn);
                        break;
                    case "Command":
                        var cmdL = part.ChildNodes;
                        var c = new CommandInfo();
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
                        c.fn =
                        [
                            fn
                        ];
                        level[string.Empty] = c;
                        break;
                }
            }
        }
    }

    public partial class Parser
    {
        // If an unquoted portion ends with an element matching this regex
        // and the next element contains a space, then we have stripped
        // embedded quotes that should not have been stripped
        [GeneratedRegex("^--[a-zA-Z0-9-]+=$")]
        private static partial Regex OptionParserRegex();
        
        private static readonly Regex optionRegex = OptionParserRegex();

        public static string[] Parse(string text)
        {
            List<string> result = [];

            int index;

            var unquoted = text.Split(['"']);

            for (index = 0 ; index < unquoted.Length ; index++)
            {
                if (index % 2 == 0)
                {
                    var words = unquoted[index].Split();

                    var option = false;
                    foreach (var w in words)
                    {
                        if (w == string.Empty) continue;
                        option = optionRegex.Match(w) != Match.Empty;
                        result.Add(w);
                    }
                    // The last item matched the regex, put the quotes back
                    if (!option) continue;
                    // If the line ended with it, don't do anything
                    if (index >= (unquoted.Length - 1)) continue;
                    // Get and remove the option name
                    var optionText = result[result.Count - 1];
                    result.RemoveAt(result.Count - 1);

                    // Add the quoted value back
                    optionText += "\"" + unquoted[index + 1] + "\"";

                    // Push the result into our return array
                    result.Add(optionText);

                    // Skip the already used value
                    index++;
                }
                else
                {
                    result.Add(unquoted[index]);
                }
            }

            return result.ToArray();
        }
        
    }

    /// <summary>
    /// A console that processes commands internally
    /// </summary>
    public class CommandConsole : ConsoleBase, ICommandConsole
    {
        //        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public event OnOutputDelegate OnOutput;
        public static event OnCntrCCelegate OnCntrC;

        public ICommands Commands { get; private set; }

        public CommandConsole(string defaultPrompt) : base(defaultPrompt)
        {
            Commands = new Commands();

            Commands.AddCommand(
                "Help", false, "help", "help [<item>]",
                "Display help on a particular command or on a list of commands in a category", Help);
        }

        private void Help(string module, string[] cmd)
        {
            var help = Commands.GetHelp(cmd);

            foreach (var s in help)
                Output(s);
        }

        protected void FireOnOutput(string text)
        {
            OnOutput?.Invoke(text);
        }

        /// <summary>
        /// Display a command prompt on the console and wait for user input
        /// </summary>
        public void Prompt()
        {
            var line = ReadLine(DefaultPrompt + "# ", true, true);

            if (line != string.Empty)
                Output("Invalid command");
        }

        public void RunCommand(string cmd)
        {
            var parts = Parser.Parse(cmd);
            Commands.Resolve(parts);
        }

        public override string ReadLine(string p, bool isCommand, bool e)
        {
            System.Console.Write("{0}", p);
            var cmdinput = System.Console.ReadLine();

            if (!isCommand) return cmdinput;
            var cmd = Commands.Resolve(Parser.Parse(cmdinput));

            if (cmd.Length == 0) return cmdinput;
            int i;

            for (i=0 ; i < cmd.Length ; i++)
            {
                if (cmd[i].Contains(' '))
                    cmd[i] = "\"" + cmd[i] + "\"";
            }
            return string.Empty;
        }

        public virtual void ReadConfig(IConfigSource configSource)
        {
        }

        public virtual void SetCntrCHandler(OnCntrCCelegate handler)
        {
            if (OnCntrC != null) return;
            OnCntrC += handler;
            System.Console.CancelKeyPress += CancelKeyPressed;
        }

        protected static void CancelKeyPressed(object sender, ConsoleCancelEventArgs args)
        {
            if (OnCntrC == null || args.SpecialKey != ConsoleSpecialKey.ControlC) return;
            OnCntrC?.Invoke();
            args.Cancel = false;
        }

        protected static void LocalCancelKeyPressed()
        {
            OnCntrC?.Invoke();
        }
    }
}
