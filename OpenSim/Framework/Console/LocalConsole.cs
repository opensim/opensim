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
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.IO;
using Nini.Config;
using log4net;

namespace OpenSim.Framework.Console
{
    /// <summary>
    /// A console that uses cursor control and color
    /// </summary>
    public class LocalConsole : CommandConsole
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private string m_historyPath;
        private bool m_historyEnable;
        private bool m_historytimestamps;

        // private readonly object m_syncRoot = new object();
        private const string LOGLEVEL_NONE = "(none)";

        // Used to extract categories for colourization.
        private Regex m_categoryRegex
            = new Regex(
                @"^(?<Front>.*?)\[(?<Category>[^\]]+)\]:?(?<End>.*)", RegexOptions.Singleline | RegexOptions.Compiled);

        private int m_cursorYPosition = -1;
        private int m_cursorXPosition = 0;
        private StringBuilder m_commandLine = new StringBuilder();
        private bool m_echo = true;
        private List<string> m_history = new List<string>();

        private static readonly ConsoleColor[] Colors = {
            // the dark colors don't seem to be visible on some black background terminals like putty :(
            //ConsoleColor.DarkBlue,
            //ConsoleColor.DarkGreen,
            //ConsoleColor.DarkCyan,
            //ConsoleColor.DarkMagenta,
            //ConsoleColor.DarkYellow,
            ConsoleColor.Gray,
            //ConsoleColor.DarkGray,
            ConsoleColor.Blue,
            ConsoleColor.Green,
            ConsoleColor.Cyan,
            ConsoleColor.Magenta,
            ConsoleColor.Yellow
        };

        private static ConsoleColor DeriveColor(string input)
        {
            // it is important to do Abs, hash values can be negative
            return Colors[(Math.Abs(input.ToUpper().GetHashCode()) % Colors.Length)];
        }

        public LocalConsole(string defaultPrompt, IConfig startupConfig = null) : base(defaultPrompt)
        {

            if (startupConfig == null) return;

            m_historyEnable = startupConfig.GetBoolean("ConsoleHistoryFileEnabled", false);
            if (!m_historyEnable)
            {
                m_log.Info("[LOCAL CONSOLE]: Persistent command line history from file is Disabled");
                return;
            }

            string m_historyFile = startupConfig.GetString("ConsoleHistoryFile", "OpenSimConsoleHistory.txt");
            int m_historySize = startupConfig.GetInt("ConsoleHistoryFileLines", 100);
            m_historyPath = Path.GetFullPath(Path.Combine(Util.configDir(), m_historyFile));
            m_historytimestamps = startupConfig.GetBoolean("ConsoleHistoryTimeStamp", false);
            m_log.InfoFormat("[LOCAL CONSOLE]: Persistent command line history is Enabled, up to {0} lines from file {1} {2} timestamps",
                m_historySize, m_historyPath, m_historytimestamps?"with":"without");

            if (File.Exists(m_historyPath))
            {
                List<string> originallines = new List<string>();
                using (StreamReader history_file = new StreamReader(m_historyPath))
                {
                    string line;
                    while ((line = history_file.ReadLine()) != null)
                    {
                        originallines.Add(line);
                        if(line.StartsWith("["))
                        {
                            int indx = line.IndexOf("]:> ");
                            if(indx > 0)
                            {
                                if(indx + 4 >= line.Length)
                                    line = String.Empty;
                                else
                                   line = line.Substring(indx + 4);
                            }
                        }
                        m_history.Add(line);
                    }
                }

                if (m_history.Count > m_historySize)
                {
                    while (m_history.Count > m_historySize)
                    {
                        m_history.RemoveAt(0);
                        originallines.RemoveAt(0);
                    }

                    using (StreamWriter history_file = new StreamWriter(m_historyPath))
                    {
                        foreach (string line in originallines)
                        {
                            history_file.WriteLine(line);
                        }
                    }
                }
                m_log.InfoFormat("[LOCAL CONSOLE]: Read {0} lines of command line history from file {1}", m_history.Count, m_historyPath);
            }
            else
            {
                m_log.InfoFormat("[LOCAL CONSOLE]: Creating new empty command line history file {0}", m_historyPath);
                File.Create(m_historyPath).Dispose();
            }
        }

        private void AddToHistory(string text)
        {
            while (m_history.Count >= 100)
                m_history.RemoveAt(0);

            m_history.Add(text);
            if (m_historyEnable)
            {
                if (m_historytimestamps)
                    text = String.Format("[{0} {1}]:> {2}", DateTime.Now.ToShortDateString(), DateTime.Now.ToShortTimeString(), text);
                File.AppendAllText(m_historyPath, text + Environment.NewLine);
            }
        }

        /// <summary>
        /// Set the cursor row.
        /// </summary>
        ///
        /// <param name="top">
        /// Row to set.  If this is below 0, then the row is set to 0.  If it is equal to the buffer height or greater
        /// then it is set to one less than the height.
        /// </param>
        /// <returns>
        /// The new cursor row.
        /// </returns>
        private int SetCursorTop(int top)
        {
            // From at least mono 2.4.2.3, window resizing can give mono an invalid row and column values.  If we try
            // to set a cursor row position with a currently invalid column, mono will throw an exception.
            // Therefore, we need to make sure that the column position is valid first.
            int left = System.Console.CursorLeft;

            if (left < 0)
            {
                System.Console.CursorLeft = 0;
            }
            else
            {
                int bufferWidth = System.Console.BufferWidth;

                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bufferWidth > 0 && left >= bufferWidth)
                    System.Console.CursorLeft = bufferWidth - 1;
            }

            if (top < 0)
            {
                top = 0;
            }
            else
            {
                int bufferHeight = System.Console.BufferHeight;

                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bufferHeight > 0 && top >= bufferHeight)
                    top = bufferHeight - 1;
            }

            System.Console.CursorTop = top;

            return top;
        }

        /// <summary>
        /// Set the cursor column.
        /// </summary>
        ///
        /// <param name="left">
        /// Column to set.  If this is below 0, then the column is set to 0.  If it is equal to the buffer width or greater
        /// then it is set to one less than the width.
        /// </param>
        /// <returns>
        /// The new cursor column.
        /// </returns>
        private int SetCursorLeft(int left)
        {
            // From at least mono 2.4.2.3, window resizing can give mono an invalid row and column values.  If we try
            // to set a cursor column position with a currently invalid row, mono will throw an exception.
            // Therefore, we need to make sure that the row position is valid first.
            int top = System.Console.CursorTop;

            if (top < 0)
            {
                System.Console.CursorTop = 0;
            }
            else
            {
                int bufferHeight = System.Console.BufferHeight;
                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bufferHeight > 0 && top >= bufferHeight)
                    System.Console.CursorTop = bufferHeight - 1;
            }

            if (left < 0)
            {
                left = 0;
            }
            else
            {
                int bufferWidth = System.Console.BufferWidth;

                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bufferWidth > 0 && left >= bufferWidth)
                    left = bufferWidth - 1;
            }

            System.Console.CursorLeft = left;

            return left;
        }

        private void Show()
        {
            lock (m_commandLine)
            {
                if (m_cursorYPosition == -1 || System.Console.BufferWidth == 0)
                    return;

                int xc = prompt.Length + m_cursorXPosition;
                int new_x = xc % System.Console.BufferWidth;
                int new_y = m_cursorYPosition + xc / System.Console.BufferWidth;
                int end_y = m_cursorYPosition + (m_commandLine.Length + prompt.Length) / System.Console.BufferWidth;

                if (end_y >= System.Console.BufferHeight) // wrap
                {
                    m_cursorYPosition--;
                    new_y--;
                    SetCursorLeft(0);
                    SetCursorTop(System.Console.BufferHeight - 1);
                    System.Console.WriteLine(" ");
                }

                m_cursorYPosition = SetCursorTop(m_cursorYPosition);
                SetCursorLeft(0);

                if (m_echo)
                    System.Console.Write("{0}{1}", prompt, m_commandLine);
                else
                    System.Console.Write("{0}", prompt);

                SetCursorTop(new_y);
                SetCursorLeft(new_x);
            }
        }

        public override void LockOutput()
        {
            Monitor.Enter(m_commandLine);
            try
            {
                if (m_cursorYPosition != -1)
                {
                    m_cursorYPosition = SetCursorTop(m_cursorYPosition);
                    System.Console.CursorLeft = 0;

                    int count = m_commandLine.Length + prompt.Length;

                    while (count-- > 0)
                        System.Console.Write(" ");

                    m_cursorYPosition = SetCursorTop(m_cursorYPosition);
                    SetCursorLeft(0);
                }
            }
            catch (Exception)
            {
            }
        }

        public override void UnlockOutput()
        {
            if (m_cursorYPosition != -1)
            {
                m_cursorYPosition = System.Console.CursorTop;
                Show();
            }
            Monitor.Exit(m_commandLine);
        }

        private void WriteColorText(ConsoleColor color, string sender)
        {
            try
            {
                lock (this)
                {
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
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void WriteLocalText(string text, string level)
        {
            string outText = text;

            if (level != null)
            {
                MatchCollection matches = m_categoryRegex.Matches(text);

                if (matches.Count == 1)
                {
                    outText = matches[0].Groups["End"].Value;
                    System.Console.Write(matches[0].Groups["Front"].Value);

                    System.Console.Write("[");
                    WriteColorText(DeriveColor(matches[0].Groups["Category"].Value),
                            matches[0].Groups["Category"].Value);
                    System.Console.Write("]:");
                }
                else
                {
                    outText = outText.Trim();
                }
            }

            if (level == "error")
                WriteColorText(ConsoleColor.Red, outText);
            else if (level == "warn")
                WriteColorText(ConsoleColor.Yellow, outText);
            else
                System.Console.Write(outText);

            System.Console.WriteLine();
        }

        public override void Output(string format, params object[] components)
        {
            string level = null;
            if(components != null && components.Length > 0)
            {
                if(components[0] == null || components[0] is ConsoleLevel)
                {
                    if(components[0] is ConsoleLevel)
                        level = ((ConsoleLevel)components[0]).ToString();

                    if (components.Length > 1)
                    {
                        object[] tmp = new object[components.Length - 1];
                        Array.Copy(components, 1, tmp, 0, components.Length - 1);
                        components = tmp;
                    }
                    else
                        components = null;
                }

            }
            string text;
            if (components == null || components.Length == 0)
                text = format;
            else
                text = String.Format(format, components);

            FireOnOutput(text);

            lock (m_commandLine)
            {
                if (m_cursorYPosition == -1)
                {
                    WriteLocalText(text, level);
                    return;
                }

                m_cursorYPosition = SetCursorTop(m_cursorYPosition);
                SetCursorLeft(0);

                int count = m_commandLine.Length + prompt.Length;

                while (count-- > 0)
                    System.Console.Write(" ");

                m_cursorYPosition = SetCursorTop(m_cursorYPosition);
                SetCursorLeft(0);

                WriteLocalText(text, level);

                m_cursorYPosition = System.Console.CursorTop;

                Show();
            }
        }

        private bool ContextHelp()
        {
            string[] words = Parser.Parse(m_commandLine.ToString());

            bool trailingSpace = m_commandLine.ToString().EndsWith(" ");

            // Allow ? through while typing a URI
            //
            if (words.Length > 0 && words[words.Length-1].StartsWith("http") && !trailingSpace)
                return false;

            string[] opts = Commands.FindNextOption(words, trailingSpace);

            if (opts[0].StartsWith("Command help:"))
                Output(opts[0]);
            else
                Output(String.Format("Options: {0}", String.Join(" ", opts)));

            return true;
        }

        public override string ReadLine(string p, bool isCommand, bool e)
        {
            m_cursorXPosition = 0;
            prompt = p;
            m_echo = e;
            int historyLine = m_history.Count;

            SetCursorLeft(0); // Needed for mono
            System.Console.Write(" "); // Needed for mono

            lock (m_commandLine)
            {
                m_cursorYPosition = System.Console.CursorTop;
                m_commandLine.Remove(0, m_commandLine.Length);
            }

            while (true)
            {
                Show();

                ConsoleKeyInfo key = System.Console.ReadKey(true);
                char enteredChar = key.KeyChar;

                if (!Char.IsControl(enteredChar))
                {
                    if (m_cursorXPosition >= 318)
                        continue;

                    if (enteredChar == '?' && isCommand)
                    {
                        if (ContextHelp())
                            continue;
                    }

                    m_commandLine.Insert(m_cursorXPosition, enteredChar);
                    m_cursorXPosition++;
                }
                else
                {
                    switch (key.Key)
                    {
                    case ConsoleKey.Backspace:
                        if (m_cursorXPosition == 0)
                            break;
                        m_commandLine.Remove(m_cursorXPosition-1, 1);
                        m_cursorXPosition--;

                        SetCursorLeft(0);
                        m_cursorYPosition = SetCursorTop(m_cursorYPosition);

                        if (m_echo)
                            System.Console.Write("{0}{1} ", prompt, m_commandLine);
                        else
                            System.Console.Write("{0}", prompt);

                        break;
                    case ConsoleKey.Delete:
                        if (m_cursorXPosition == m_commandLine.Length)
                            break;

                        m_commandLine.Remove(m_cursorXPosition, 1);

                        SetCursorLeft(0);
                        m_cursorYPosition = SetCursorTop(m_cursorYPosition);

                        if (m_echo)
                            System.Console.Write("{0}{1} ", prompt, m_commandLine);
                        else
                            System.Console.Write("{0}", prompt);

                        break;
                    case ConsoleKey.End:
                        m_cursorXPosition = m_commandLine.Length;
                        break;
                    case ConsoleKey.Home:
                        m_cursorXPosition = 0;
                        break;
                    case ConsoleKey.UpArrow:
                        if (historyLine < 1)
                            break;
                        historyLine--;
                        LockOutput();
                        m_commandLine.Remove(0, m_commandLine.Length);
                        m_commandLine.Append(m_history[historyLine]);
                        m_cursorXPosition = m_commandLine.Length;
                        UnlockOutput();
                        break;
                    case ConsoleKey.DownArrow:
                        if (historyLine >= m_history.Count)
                            break;
                        historyLine++;
                        LockOutput();
                        if (historyLine == m_history.Count)
                        {
                            m_commandLine.Remove(0, m_commandLine.Length);
                        }
                        else
                        {
                            m_commandLine.Remove(0, m_commandLine.Length);
                            m_commandLine.Append(m_history[historyLine]);
                        }
                        m_cursorXPosition = m_commandLine.Length;
                        UnlockOutput();
                        break;
                    case ConsoleKey.LeftArrow:
                        if (m_cursorXPosition > 0)
                            m_cursorXPosition--;
                        break;
                    case ConsoleKey.RightArrow:
                        if (m_cursorXPosition < m_commandLine.Length)
                            m_cursorXPosition++;
                        break;
                    case ConsoleKey.Enter:
                        SetCursorLeft(0);
                        m_cursorYPosition = SetCursorTop(m_cursorYPosition);

                        System.Console.WriteLine();
                        //Show();

                        lock (m_commandLine)
                        {
                            m_cursorYPosition = -1;
                        }

                        string commandLine = m_commandLine.ToString();

                        if (isCommand)
                        {
                            string[] cmd = Commands.Resolve(Parser.Parse(commandLine));

                            if (cmd.Length != 0)
                            {
                                int index;

                                for (index=0 ; index < cmd.Length ; index++)
                                {
                                    if (cmd[index].Contains(" "))
                                        cmd[index] = "\"" + cmd[index] + "\"";
                                }
                                AddToHistory(String.Join(" ", cmd));
                                return String.Empty;
                            }
                        }

                        // If we're not echoing to screen (e.g. a password) then we probably don't want it in history
                        if (m_echo && commandLine != "")
                            AddToHistory(commandLine);

                        return commandLine;
                    default:
                        break;
                    }
                }
            }
        }
    }
}
