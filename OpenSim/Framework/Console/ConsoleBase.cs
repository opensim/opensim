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
                
                //System.Console.WriteLine("Found {0}", helpParts[0]);
                
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
                    fn(ci.module, result);
                return result;
            }
            return new string[0];
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

    public class ConsoleBase
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly object m_syncRoot = new object();

        private int y = -1;
        private int cp = 0;
        private int h = 1;
        private string prompt = "# ";
        private StringBuilder cmdline = new StringBuilder();
        public Commands Commands = new Commands();
        private bool echo = true;
        private List<string> history = new List<string>();
        private bool gui = false;

        public object ConsoleScene = null;

        /// <summary>
        /// The default prompt text.
        /// </summary>
        public string DefaultPrompt
        {
            set { m_defaultPrompt = value + "# "; }
            get { return m_defaultPrompt; }
        }
        protected string m_defaultPrompt;

        public ConsoleBase(string defaultPrompt)
        {
            DefaultPrompt = defaultPrompt;

            Commands.AddCommand("console", false, "help", "help [<command>]", 
                    "Get general command list or more detailed help on a specific command", Help);
        }

        public void SetGuiMode(bool mode)
        {
            gui = mode;
        }

        private void AddToHistory(string text)
        {
            while (history.Count >= 100)
                history.RemoveAt(0);

            history.Add(text);
        }

        /// <summary>
        /// derive an ansi color from a string, ignoring the darker colors.
        /// This is used to help automatically bin component tags with colors
        /// in various print functions.
        /// </summary>
        /// <param name="input">arbitrary string for input</param>
        /// <returns>an ansii color</returns>
        private static ConsoleColor DeriveColor(string input)
        {
            int colIdx = (input.ToUpper().GetHashCode() % 6) + 9;
            return (ConsoleColor) colIdx;
        }

        /// <summary>
        /// Sends a warning to the current console output
        /// </summary>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        public void Warn(string format, params object[] args)
        {
            WriteNewLine(ConsoleColor.Yellow, format, args);
        }

        /// <summary>
        /// Sends a warning to the current console output
        /// </summary>
        /// <param name="sender">The module that sent this message</param>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        public void Warn(string sender, string format, params object[] args)
        {
            WriteNewLine(DeriveColor(sender), sender, ConsoleColor.Yellow, format, args);
        }

        /// <summary>
        /// Sends a notice to the current console output
        /// </summary>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        public void Notice(string format, params object[] args)
        {
            WriteNewLine(ConsoleColor.White, format, args);
        }

        /// <summary>
        /// Sends a notice to the current console output
        /// </summary>
        /// <param name="sender">The module that sent this message</param>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        public void Notice(string sender, string format, params object[] args)
        {
            WriteNewLine(DeriveColor(sender), sender, ConsoleColor.White, format, args);
        }
        /// <summary>
        /// Sends an error to the current console output
        /// </summary>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        public void Error(string format, params object[] args)
        {
            WriteNewLine(ConsoleColor.Red, format, args);
        }

        /// <summary>
        /// Sends an error to the current console output
        /// </summary>
        /// <param name="sender">The module that sent this message</param>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        public void Error(string sender, string format, params object[] args)
        {
            WriteNewLine(DeriveColor(sender), sender, ConsoleColor.Red, format, args);
        }

        /// <summary>
        /// Sends a status message to the current console output
        /// </summary>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        public void Status(string format, params object[] args)
        {
            WriteNewLine(ConsoleColor.Blue, format, args);
        }

        /// <summary>
        /// Sends a status message to the current console output
        /// </summary>
        /// <param name="sender">The module that sent this message</param>
        /// <param name="format">The message to send</param>
        /// <param name="args">WriteLine-style message arguments</param>
        public void Status(string sender, string format, params object[] args)
        {
            WriteNewLine(DeriveColor(sender), sender, ConsoleColor.Blue, format, args);
        }

        [Conditional("DEBUG")]
        public void Debug(string format, params object[] args)
        {
            WriteNewLine(ConsoleColor.Gray, format, args);
        }

        [Conditional("DEBUG")]
        public void Debug(string sender, string format, params object[] args)
        {
            WriteNewLine(DeriveColor(sender), sender, ConsoleColor.Gray, format, args);
        }

        private void WriteNewLine(ConsoleColor senderColor, string sender, ConsoleColor color, string format, params object[] args)
        {
            lock (cmdline)
            {
                if (y != -1)
                {
                    System.Console.CursorTop = y;
                    System.Console.CursorLeft = 0;

                    int count = cmdline.Length;

                    System.Console.Write("  ");
                    while (count-- > 0)
                        System.Console.Write(" ");

                    System.Console.CursorTop = y;
                    System.Console.CursorLeft = 0;
                }
                WritePrefixLine(senderColor, sender);
                WriteConsoleLine(color, format, args);
                if (y != -1)
                    y = System.Console.CursorTop;
            }
        }

        private void WriteNewLine(ConsoleColor color, string format, params object[] args)
        {
            lock (cmdline)
            {
                if (y != -1)
                {
                    System.Console.CursorTop = y;
                    System.Console.CursorLeft = 0;

                    int count = cmdline.Length;

                    System.Console.Write("  ");
                    while (count-- > 0)
                        System.Console.Write(" ");

                    System.Console.CursorTop = y;
                    System.Console.CursorLeft = 0;
                }
                WriteConsoleLine(color, format, args);
                if (y != -1)
                    y = System.Console.CursorTop;
            }
        }

        private void WriteConsoleLine(ConsoleColor color, string format, params object[] args)
        {
            try
            {
                lock (m_syncRoot)
                {
                    try
                    {
                        if (color != ConsoleColor.White)
                            System.Console.ForegroundColor = color;

                        System.Console.WriteLine(format, args);
                        System.Console.ResetColor();
                    }
                    catch (ArgumentNullException)
                    {
                        // Some older systems dont support coloured text.
                        System.Console.WriteLine(format, args);
                    }
                    catch (FormatException)
                    {
                        System.Console.WriteLine(args);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void WritePrefixLine(ConsoleColor color, string sender)
        {
            try
            {
                lock (m_syncRoot)
                {
                    sender = sender.ToUpper();

                    System.Console.WriteLine("[" + sender + "] ");

                    System.Console.Write("[");

                    try
                    {
                        System.Console.ForegroundColor = color;
                        System.Console.Write(sender);
                        System.Console.ResetColor();
                    }
                    catch (ArgumentNullException)
                    {
                        // Some older systems dont support coloured text.
                        System.Console.WriteLine(sender);
                    }

                    System.Console.Write("] \t");
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void Help(string module, string[] cmd)
        {
            List<string> help = Commands.GetHelp(cmd);

            foreach (string s in help)
                Output(s);
        }

        private void Show()
        {
            lock (cmdline)
            {
                if (y == -1 || System.Console.BufferWidth == 0)
                    return;

                int xc = prompt.Length + cp;
                int new_x = xc % System.Console.BufferWidth;
                int new_y = y + xc / System.Console.BufferWidth;
                int end_y = y + (cmdline.Length + prompt.Length) / System.Console.BufferWidth;
                if (end_y / System.Console.BufferWidth >= h)
                    h++;
                if (end_y >= System.Console.BufferHeight) // wrap
                {
                    y--;
                    new_y--;
                    System.Console.CursorLeft = 0;
                    System.Console.CursorTop = System.Console.BufferHeight-1;
                    System.Console.WriteLine(" ");
                }

                System.Console.CursorTop = y;
                System.Console.CursorLeft = 0;

                if (echo)
                    System.Console.Write("{0}{1}", prompt, cmdline);
                else
                    System.Console.Write("{0}", prompt);

                System.Console.CursorLeft = new_x;
                System.Console.CursorTop = new_y;
            }
        }

        public void LockOutput()
        {
            Monitor.Enter(cmdline);
            try
            {
                if (y != -1)
                {
                    System.Console.CursorTop = y;
                    System.Console.CursorLeft = 0;

                    int count = cmdline.Length + prompt.Length;

                    while (count-- > 0)
                        System.Console.Write(" ");

                    System.Console.CursorTop = y;
                    System.Console.CursorLeft = 0;

                }
            }
            catch (Exception)
            {
            }
        }

        public void UnlockOutput()
        {
            if (y != -1)
            {
                y = System.Console.CursorTop;
                Show();
            }
            Monitor.Exit(cmdline);
        }

        public void Output(string text)
        {
            lock (cmdline)
            {
                if (y == -1)
                {
                    System.Console.WriteLine(text);

                    return;
                }

                System.Console.CursorTop = y;
                System.Console.CursorLeft = 0;

                int count = cmdline.Length + prompt.Length;

                while (count-- > 0)
                    System.Console.Write(" ");

                System.Console.CursorTop = y;
                System.Console.CursorLeft = 0;

                System.Console.WriteLine(text);

                y = System.Console.CursorTop;

                Show();
            }
        }

        private void ContextHelp()
        {
            string[] words = Parser.Parse(cmdline.ToString());

            string[] opts = Commands.FindNextOption(words, cmdline.ToString().EndsWith(" "));

            if (opts[0].StartsWith("Command help:"))
                Output(opts[0]);
            else
                Output(String.Format("Options: {0}", String.Join(" ", opts)));
        }

        public void Prompt()
        {
            string line = ReadLine(m_defaultPrompt, true, true);

            if (line != String.Empty)
            {
                m_log.Info("Invalid command");
            }
        }

        public string CmdPrompt(string p)
        {
            return ReadLine(String.Format("{0}: ", p), false, true);
        }

        public string CmdPrompt(string p, string def)
        {
            string ret = ReadLine(String.Format("{0} [{1}]: ", p, def), false, true);
            if (ret == String.Empty)
                ret = def;

            return ret;
        }

        // Displays a command prompt and returns a default value, user may only enter 1 of 2 options
        public string CmdPrompt(string prompt, string defaultresponse, string OptionA, string OptionB)
        {
            bool itisdone = false;
            string temp = CmdPrompt(prompt, defaultresponse);
            while (itisdone == false)
            {
                if ((temp == OptionA) || (temp == OptionB))
                {
                    itisdone = true;
                }
                else
                {
                    System.Console.WriteLine("Valid options are " + OptionA + " or " + OptionB);
                    temp = CmdPrompt(prompt, defaultresponse);
                }
            }
            return temp;
        }

        // Displays a prompt and waits for the user to enter a string, then returns that string
        // (Done with no echo and suitable for passwords)
        public string PasswdPrompt(string p)
        {
            return ReadLine(p, false, false);
        }

        public void RunCommand(string cmd)
        {
            string[] parts = Parser.Parse(cmd);
            Commands.Resolve(parts);
        }

        public string ReadLine(string p, bool isCommand, bool e)
        {
            h = 1;
            cp = 0;
            prompt = p;
            echo = e;
            int historyLine = history.Count;

            if (gui)
            {
                System.Console.Write("{0}", prompt);
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

            System.Console.CursorLeft = 0; // Needed for mono
            System.Console.Write(" "); // Needed for mono

            lock (cmdline)
            {
                y = System.Console.CursorTop;
                cmdline.Remove(0, cmdline.Length);
            }

            while (true)
            {
                Show();

                ConsoleKeyInfo key = System.Console.ReadKey(true);
                char c = key.KeyChar;

                if (!Char.IsControl(c))
                {
                    if (cp >= 318)
                        continue;

                    if (c == '?' && isCommand)
                    {
                        ContextHelp();
                        continue;
                    }

                    cmdline.Insert(cp, c);
                    cp++;
                }
                else
                {
                    switch (key.Key)
                    {
                    case ConsoleKey.Backspace:
                        if (cp == 0)
                            break;
                        cmdline.Remove(cp-1, 1);
                        cp--;

                        System.Console.CursorLeft = 0;
                        System.Console.CursorTop = y;

                        System.Console.Write("{0}{1} ", prompt, cmdline);

                        break;
                    case ConsoleKey.End:
                        cp = cmdline.Length;
                        break;
                    case ConsoleKey.Home:
                        cp = 0;
                        break;
                    case ConsoleKey.UpArrow:
                        if (historyLine < 1)
                            break;
                        historyLine--;
                        LockOutput();
                        cmdline.Remove(0, cmdline.Length);
                        cmdline.Append(history[historyLine]);
                        cp = cmdline.Length;
                        UnlockOutput();
                        break;
                    case ConsoleKey.DownArrow:
                        if (historyLine >= history.Count)
                            break;
                        historyLine++;
                        LockOutput();
                        if (historyLine == history.Count)
                        {
                            cmdline.Remove(0, cmdline.Length);
                        }
                        else
                        {
                            cmdline.Remove(0, cmdline.Length);
                            cmdline.Append(history[historyLine]);
                        }
                        cp = cmdline.Length;
                        UnlockOutput();
                        break;
                    case ConsoleKey.LeftArrow:
                        if (cp > 0)
                            cp--;
                        break;
                    case ConsoleKey.RightArrow:
                        if (cp < cmdline.Length)
                            cp++;
                        break;
                    case ConsoleKey.Enter:
                        System.Console.CursorLeft = 0;
                        System.Console.CursorTop = y;

                        System.Console.WriteLine("{0}{1}", prompt, cmdline);

                        lock (cmdline)
                        {
                            y = -1;
                        }

                        if (isCommand)
                        {
                            string[] cmd = Commands.Resolve(Parser.Parse(cmdline.ToString()));

                            if (cmd.Length != 0)
                            {
                                int i;

                                for (i=0 ; i < cmd.Length ; i++)
                                {
                                    if (cmd[i].Contains(" "))
                                        cmd[i] = "\"" + cmd[i] + "\"";
                                }
                                AddToHistory(String.Join(" ", cmd));
                                return String.Empty;
                            }
                        }

                        AddToHistory(cmdline.ToString());
                        return cmdline.ToString();
                    default:
                        break;
                    }
                }
            }
        }
    }
}
