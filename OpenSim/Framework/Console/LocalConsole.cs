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
    public partial class LocalConsole : CommandConsole
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType);
        private string m_historyPath;
        private readonly bool m_historyEnable;
        private bool m_historytimestamps;

        // private readonly object m_syncRoot = new object();
        private const string LOGLEVEL_NONE = "(none)";

        // Used to extract categories for colourization.
        private readonly Regex m_categoryRegex = CategoryRegex();

        private int m_cursorYPosition = -1;
        private int m_cursorXPosition = 0;
        private readonly StringBuilder m_commandLine = new();
        private bool m_echo = true;
        private readonly List<string> m_history = [];

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

            var m_historyFile = startupConfig.GetString("ConsoleHistoryFile", "OpenSimConsoleHistory.txt");
            var m_historySize = startupConfig.GetInt("ConsoleHistoryFileLines", 100);
            m_historyPath = Path.GetFullPath(Path.Combine(Util.configDir(), m_historyFile));
            m_historytimestamps = startupConfig.GetBoolean("ConsoleHistoryTimeStamp", false);
            m_log.InfoFormat("[LOCAL CONSOLE]: Persistent command line history is Enabled, up to {0} lines from file {1} {2} timestamps",
                m_historySize, m_historyPath, m_historytimestamps?"with":"without");

            if (File.Exists(m_historyPath))
            {
                var originallines = new List<string>();
                using (var history_file = new StreamReader(m_historyPath))
                {
                    string line;
                    while ((line = history_file.ReadLine()) != null)
                    {
                        originallines.Add(line);
                        if (line.StartsWith("["))
                        {
                            var indx = line.IndexOf("]:> ", StringComparison.Ordinal);
                            if (indx > 0)
                            {
                                if (indx + 4 >= line.Length)
                                    line = String.Empty;
                                else
                                   line = line[(indx + 4)..];
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

                    using var history_file = new StreamWriter(m_historyPath);
                    foreach (var line in originallines)
                    {
                        history_file.WriteLine(line);
                    }
                }
                m_log.InfoFormat("[LOCAL CONSOLE]: Read {0} lines of command line history from file {1}", m_history.Count, m_historyPath);
            }
            else
            {
                m_log.InfoFormat("[LOCAL CONSOLE]: Creating new empty command line history file {0}", m_historyPath);
                File.Create(m_historyPath).Dispose();
            }

            System.Console.TreatControlCAsInput = true;
        }

        private void AddToHistory(string text)
        {
            while (m_history.Count >= 100)
                m_history.RemoveAt(0);

            m_history.Add(text);
            if (m_historyEnable)
            {
                if (m_historytimestamps)
                    text = $"[{DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()}]:> {text}";
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
            // mono seems to fail unless we do check both left and top ranges, even current
            var left = System.Console.CursorLeft;
            if (left <= 0)
                left = 0;
            else
            {
                var bufferWidth = System.Console.BufferWidth;
                // @todo: mono hacks
                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bufferWidth > 0 && left >= bufferWidth)
                    left = bufferWidth - 1;
            }

            if (top <= 0)
                top = 0;
            else
            {
                var bufferHeight = System.Console.BufferHeight;
                // @todo: mono hacks
                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bufferHeight > 0 && top >= bufferHeight)
                    top = bufferHeight - 1;
            }

            System.Console.SetCursorPosition(left, top);
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
            var top = System.Console.CursorTop;
            if (top <= 0)
                top = 0;
            else
            {
                var bufferHeight = System.Console.BufferHeight;
                if (bufferHeight > 0 && top >= bufferHeight)
                    top = bufferHeight - 1;
            }

            if (left <= 0)
                left = 0;
            else
            {
                var bufferWidth = System.Console.BufferWidth;
                // @todo: mono hacks
                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bufferWidth > 0 && left >= bufferWidth)
                    left = bufferWidth - 1;
            }

            System.Console.SetCursorPosition(left, top);
            return left;
        }

        private void SetCursorTopLeft(int top, int left)
        {
            if (top <= 0)
                top = 0;
            else
            {
                var bufferHeight = System.Console.BufferHeight;
                if (bufferHeight > 0 && top >= bufferHeight)
                    top = bufferHeight - 1;
            }

            if (left <= 0)
                left = 0;
            else
            {
                var bufferWidth = System.Console.BufferWidth;
                if (bufferWidth > 0 && left >= bufferWidth)
                    left = bufferWidth - 1;
            }
            System.Console.SetCursorPosition(left, top);
        }

        private int SetCursorZeroLeft(int top)
        {
            if (top <= 0)
            {
                System.Console.SetCursorPosition(0, 0);
                return 0;
            }

            var bufferHeight = System.Console.BufferHeight;
            if (bufferHeight > 0 && top >= bufferHeight)
            {
                top = bufferHeight - 1;
            }

            System.Console.SetCursorPosition(0, top);
            return top;
        }

        private void Show()
        {
            lock (m_commandLine)
            {
                if (m_cursorYPosition == -1 || System.Console.BufferWidth == 0)
                    return;

                var xc = prompt.Length + m_cursorXPosition;
                var new_x = xc % System.Console.BufferWidth;

                var new_y = m_cursorYPosition + xc / System.Console.BufferWidth;
                var end_y = m_cursorYPosition + (m_commandLine.Length + prompt.Length) / System.Console.BufferWidth;

                if (end_y >= System.Console.BufferHeight) // wrap
                {
                    m_cursorYPosition--;
                    new_y--;
                    SetCursorZeroLeft(System.Console.BufferHeight - 1);
                    System.Console.WriteLine(" ");
                }

                m_cursorYPosition = SetCursorZeroLeft(m_cursorYPosition);

                if (m_echo)
                    System.Console.Write("{0}{1}", prompt, m_commandLine);
                else
                    System.Console.Write("{0}", prompt);

                SetCursorTopLeft(new_y, new_x);
            }
        }

        public override void LockOutput()
        {
            Monitor.Enter(m_commandLine);
            try
            {
                if (m_cursorYPosition == -1) return;
                m_cursorYPosition = SetCursorZeroLeft(m_cursorYPosition);

                var count = m_commandLine.Length + prompt.Length;
                if (count > 0)
                    System.Console.Write(new string(' ', count));

                m_cursorYPosition = SetCursorZeroLeft(m_cursorYPosition);
            }
            catch (Exception)
            {
                // ignored
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
            var outText = text;

            if (level != null)
            {
                var matches = m_categoryRegex.Matches(text);

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

            switch (level)
            {
                case "error":
                    WriteColorText(ConsoleColor.Red, outText);
                    break;
                case "warn":
                    WriteColorText(ConsoleColor.Yellow, outText);
                    break;
                default:
                    System.Console.Write(outText);
                    break;
            }
        }

        public override void Output(string format)
        {
            Output(format, null);
        }

        public override void Output(string format, params object[] components)
        {
            string level = null;
            if (components is { Length: > 0 })
            {
                var cl = components[0] as ConsoleLevel;
                if (cl != null)
                {
                    level = cl.ToString();
                    if (components.Length > 1)
                    {
                        var tmp = new object[components.Length - 1];
                        Array.Copy(components, 1, tmp, 0, components.Length - 1);
                        components = tmp;
                    }
                    else
                        components = null;
                }
            }

            var text = (components == null || components.Length == 0) ? format : string.Format(format, components);

            FireOnOutput(text);

            lock (m_commandLine)
            {
                if (m_cursorYPosition == -1)
                {
                    WriteLocalText(text, level);
                    System.Console.WriteLine();
                    return;
                }

                m_cursorYPosition = SetCursorZeroLeft(m_cursorYPosition);

                var count = m_commandLine.Length + prompt.Length - text.Length;
                WriteLocalText(text, level);
                if (count > 0)
                    System.Console.WriteLine(new string(' ', count));
                else
                    System.Console.WriteLine();

                m_cursorYPosition = System.Console.CursorTop;
                Show();
            }
        }

        private bool ContextHelp()
        {
            var words = Parser.Parse(m_commandLine.ToString());

            var trailingSpace = m_commandLine.ToString().EndsWith(" ");

            // Allow ? through while typing a URI
            //
            if (words.Length > 0 && words[words.Length-1].StartsWith("http") && !trailingSpace)
                return false;

            var opts = Commands.FindNextOption(words, trailingSpace);

            Output(opts[0].StartsWith("Command help:")
                ? opts[0]
                : $"Options: {string.Join(" ", opts)}");

            return true;
        }

        public override string ReadLine(string p, bool isCommand, bool e)
        {
            m_cursorXPosition = 0;
            prompt = p;
            m_echo = e;
            var historyLine = m_history.Count;

            lock (m_commandLine)
            {
                // @todo: This is a hack to get around a bug in mono. needed??
                SetCursorLeft(0); // Needed for mono
                m_cursorYPosition = System.Console.CursorTop;
                // mono is silly
                if (m_cursorYPosition >= System.Console.BufferHeight)
                    m_cursorYPosition = System.Console.BufferHeight - 1;
                m_commandLine.Clear();
            }

            while (true)
            {
                Show();
                //Reduce collisions with internal read terminal information like cursor position on linux
                while(!System.Console.KeyAvailable)
                    Thread.Sleep(100);

                ConsoleKeyInfo key = System.Console.ReadKey(true);

                if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.C)
                {
                    System.Console.Write(Environment.NewLine);
                    LocalCancelKeyPressed();
                    return string.Empty;
                }
                var enteredChar = key.KeyChar;

                if (!char.IsControl(enteredChar))
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

                            m_cursorYPosition = SetCursorZeroLeft(m_cursorYPosition);

                            if (m_echo)
                                System.Console.Write("{0}{1} ", prompt, m_commandLine);
                            else
                                System.Console.Write("{0}", prompt);

                            break;
                        case ConsoleKey.Delete:
                            if (m_cursorXPosition == m_commandLine.Length)
                                break;

                            m_commandLine.Remove(m_cursorXPosition, 1);

                            m_cursorYPosition = SetCursorZeroLeft(m_cursorYPosition);

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
                            m_commandLine.Remove(0, m_commandLine.Length);
                            if (historyLine != m_history.Count)
                                m_commandLine.Append(m_history[historyLine]);
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

                            var commandLine = m_commandLine.ToString();

                            if (isCommand)
                            {
                                var cmd = Commands.Resolve(Parser.Parse(commandLine));

                                if (cmd.Length != 0)
                                {
                                    int index;

                                    for (index=0 ; index < cmd.Length ; index++)
                                    {
                                        if (cmd[index].Contains(' '))
                                            cmd[index] = "\"" + cmd[index] + "\"";
                                    }
                                    AddToHistory(string.Join(" ", cmd));
                                    return string.Empty;
                                }
                            }

                            // If we're not echoing to screen (e.g. a password) then we probably don't want it in history
                            if (m_echo && commandLine != "")
                                AddToHistory(commandLine);

                            return commandLine;
                    }
                }
            }
        }

        [GeneratedRegex(@"^(?<Front>.*?)\[(?<Category>[^\]]+)\]:?(?<End>.*)", RegexOptions.Compiled | RegexOptions.Singleline)]
        private static partial Regex CategoryRegex();
    }
}
