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
    // A console that uses cursor control and color
    //
    public class LocalConsole : CommandConsole
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly object m_syncRoot = new object();

        private int y = -1;
        private int cp = 0;
        private int h = 1;
        private StringBuilder cmdline = new StringBuilder();
        private bool echo = true;
        private List<string> history = new List<string>();

        public LocalConsole(string defaultPrompt) : base(defaultPrompt)
        {
        }

        private void AddToHistory(string text)
        {
            while (history.Count >= 100)
                history.RemoveAt(0);

            history.Add(text);
        }

        private int SetCursorTop(int top)
        {
            if (top >= 0 && top < System.Console.BufferHeight)
            {
                System.Console.CursorTop = top;
                return top;
            }
            else
            {
                return System.Console.CursorTop;
            }
        }

        private int SetCursorLeft(int left)
        {
            if (left >= 0 && left < System.Console.BufferWidth)
            {
                System.Console.CursorLeft = left;
                return left;
            }
            else
            {
                return System.Console.CursorLeft;
            }
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

                y=SetCursorTop(y);
                System.Console.CursorLeft = 0;

                if (echo)
                    System.Console.Write("{0}{1}", prompt, cmdline);
                else
                    System.Console.Write("{0}", prompt);

                SetCursorLeft(new_x);
                SetCursorTop(new_y);
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
                    System.Console.CursorLeft = 0;

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

        public override void Output(string text)
        {
            lock (cmdline)
            {
                if (y == -1)
                {
                    System.Console.WriteLine(text);

                    return;
                }

                y = SetCursorTop(y);
                System.Console.CursorLeft = 0;

                int count = cmdline.Length + prompt.Length;

                while (count-- > 0)
                    System.Console.Write(" ");

                y = SetCursorTop(y);
                System.Console.CursorLeft = 0;

                System.Console.WriteLine(text);

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

                        System.Console.CursorLeft = 0;
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
                        System.Console.CursorLeft = 0;
                        y = SetCursorTop(y);

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
