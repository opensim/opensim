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
using log4net;

namespace OpenSim.Framework.Console
{
    /// <summary>
    /// A console that uses cursor control and color
    /// </summary>
    public class LocalConsole : CommandConsole
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // private readonly object m_syncRoot = new object();
        private const string LOGLEVEL_NONE = "(none)";

        private int y = -1;
        private int cp = 0;
        private int h = 1;
        private StringBuilder cmdline = new StringBuilder();
        private bool echo = true;
        private List<string> history = new List<string>();

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

        public LocalConsole(string defaultPrompt) : base(defaultPrompt)
        {
        }

        private void AddToHistory(string text)
        {
            while (history.Count >= 100)
                history.RemoveAt(0);

            history.Add(text);
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
                int bw = System.Console.BufferWidth;
                
                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bw > 0 && left >= bw)
                    System.Console.CursorLeft = bw - 1;
            }
            
            if (top < 0)
            {
                top = 0;
            }
            else
            {
                int bh = System.Console.BufferHeight;
                
                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bh > 0 && top >= bh)
                    top = bh - 1;
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
                int bh = System.Console.BufferHeight;
                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bh > 0 && top >= bh)
                    System.Console.CursorTop = bh - 1;
            }
            
            if (left < 0)
            {
                left = 0;
            }
            else
            {
                int bw = System.Console.BufferWidth;

                // On Mono 2.4.2.3 (and possibly above), the buffer value is sometimes erroneously zero (Mantis 4657)
                if (bw > 0 && left >= bw)
                    left = bw - 1;
            }

            System.Console.CursorLeft = left;

            return left;
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
                    SetCursorLeft(0);
                    SetCursorTop(System.Console.BufferHeight - 1);
                    System.Console.WriteLine(" ");
                }

                y = SetCursorTop(y);
                SetCursorLeft(0);

                if (echo)
                    System.Console.Write("{0}{1}", prompt, cmdline);
                else
                    System.Console.Write("{0}", prompt);

                SetCursorTop(new_y);
                SetCursorLeft(new_x);
            }
        }

        public override void LockOutput()
        {
            Monitor.Enter(cmdline);
            try
            {
                if (y != -1)
                {
                    y = SetCursorTop(y);
                    System.Console.CursorLeft = 0;

                    int count = cmdline.Length + prompt.Length;

                    while (count-- > 0)
                        System.Console.Write(" ");

                    y = SetCursorTop(y);
                    SetCursorLeft(0);
                }
            }
            catch (Exception)
            {
            }
        }

        public override void UnlockOutput()
        {
            if (y != -1)
            {
                y = System.Console.CursorTop;
                Show();
            }
            Monitor.Exit(cmdline);
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

            if (level != LOGLEVEL_NONE)
            {
                string regex = @"^(?<Front>.*?)\[(?<Category>[^\]]+)\]:?(?<End>.*)";

                Regex RE = new Regex(regex, RegexOptions.Multiline);
                MatchCollection matches = RE.Matches(text);

                if (matches.Count == 1)
                {
                    outText = matches[0].Groups["End"].Value;
                    System.Console.Write(matches[0].Groups["Front"].Value);

                    System.Console.Write("[");
                    WriteColorText(DeriveColor(matches[0].Groups["Category"].Value),
                            matches[0].Groups["Category"].Value);
                    System.Console.Write("]:");
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

        public override void Output(string text)
        {
            Output(text, LOGLEVEL_NONE);
        }

        public override void Output(string text, string level)
        {
            lock (cmdline)
            {
                if (y == -1)
                {
                    WriteLocalText(text, level);

                    return;
                }

                y = SetCursorTop(y);
                SetCursorLeft(0);

                int count = cmdline.Length + prompt.Length;

                while (count-- > 0)
                    System.Console.Write(" ");

                y = SetCursorTop(y);
                SetCursorLeft(0);

                WriteLocalText(text, level);

                y = System.Console.CursorTop;

                Show();
            }
        }

        private bool ContextHelp()
        {
            string[] words = Parser.Parse(cmdline.ToString());

            bool trailingSpace = cmdline.ToString().EndsWith(" ");

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
            h = 1;
            cp = 0;
            prompt = p;
            echo = e;
            int historyLine = history.Count;

            SetCursorLeft(0); // Needed for mono
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
                        if (ContextHelp())
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

                        SetCursorLeft(0);
                        y = SetCursorTop(y);

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
                        SetCursorLeft(0);
                        y = SetCursorTop(y);

                        System.Console.WriteLine();
                        //Show();

                        lock (cmdline)
                        {
                            y = -1;
                        }

                        string commandLine = cmdline.ToString();
                        
                        if (isCommand)
                        {
                            string[] cmd = Commands.Resolve(Parser.Parse(commandLine));

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

                        // If we're not echoing to screen (e.g. a password) then we probably don't want it in history
                        if (echo && commandLine != "")
                            AddToHistory(commandLine);
                        
                        return cmdline.ToString();
                    default:
                        break;
                    }
                }
            }
        }
    }
}