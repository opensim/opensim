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
* 
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace OpenSim.Framework.Console
{
    public class ConsoleBase
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private object m_syncRoot = new object();

        public conscmd_callback m_cmdParser;
        public string m_componentName;

        public ConsoleBase(string componentname, conscmd_callback cmdparser)
        {
            m_componentName = componentname;
            m_cmdParser = cmdparser;

            System.Console.WriteLine("Creating new local console");

            m_log.Info("[" + m_componentName + "]: Started at " + DateTime.Now.ToString());
        }

        public void Close()
        {
            m_log.Info("[" + m_componentName + "]: Shutdown at " + DateTime.Now.ToString());
        }

        /// <summary>
        /// derive an ansi color from a string, ignoring the darker colors.  
        /// This is used to help automatically bin component tags with colors
        /// in various print functions.
        /// </summary>
        /// <param name="input">arbitrary string for input</param>
        /// <returns>an ansii color</returns>
        private ConsoleColor DeriveColor(string input)
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
            WritePrefixLine(DeriveColor(sender), sender);
            WriteNewLine(ConsoleColor.Yellow, format, args);
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
            WritePrefixLine(DeriveColor(sender), sender);
            WriteNewLine(ConsoleColor.White, format, args);
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
            WritePrefixLine(DeriveColor(sender), sender);
            Error(format, args);
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
            WritePrefixLine(DeriveColor(sender), sender);
            WriteNewLine(ConsoleColor.Blue, format, args);
        }

        [Conditional("DEBUG")]
        public void Debug(string format, params object[] args)
        {
            WriteNewLine(ConsoleColor.Gray, format, args);
        }

        [Conditional("DEBUG")]
        public void Debug(string sender, string format, params object[] args)
        {
            WritePrefixLine(DeriveColor(sender), sender);
            WriteNewLine(ConsoleColor.Gray, format, args);
        }

        private void WriteNewLine(ConsoleColor color, string format, params object[] args)
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

        public string ReadLine()
        {
            try
            {
                return System.Console.ReadLine();
            }
            catch (Exception e)
            {
                m_log.Error("[Console]: System.Console.ReadLine exception " + e.ToString());
                return String.Empty;
            }
        }

        public int Read()
        {
            return System.Console.Read();
        }

        public IPAddress CmdPromptIPAddress(string prompt, string defaultvalue)
        {
            IPAddress address;
            string addressStr;

            while (true)
            {
                addressStr = CmdPrompt(prompt, defaultvalue);
                if (IPAddress.TryParse(addressStr, out address))
                {
                    break;
                }
                else
                {
                    m_log.Error("Illegal address. Please re-enter.");
                }
            }

            return address;
        }

        public uint CmdPromptIPPort(string prompt, string defaultvalue)
        {
            uint port;
            string portStr;

            while (true)
            {
                portStr = CmdPrompt(prompt, defaultvalue);
                if (uint.TryParse(portStr, out port))
                {
                    if (port >= IPEndPoint.MinPort && port <= IPEndPoint.MaxPort)
                    {
                        break;
                    }
                }

                m_log.Error("Illegal address. Please re-enter.");
            }

            return port;
        }

        // Displays a prompt and waits for the user to enter a string, then returns that string
        // Done with no echo and suitable for passwords
        public string PasswdPrompt(string prompt)
        {
            // FIXME: Needs to be better abstracted
            ConsoleColor oldfg = System.Console.ForegroundColor;
            System.Console.ForegroundColor = System.Console.BackgroundColor;
            string temp = System.Console.ReadLine();
            System.Console.ForegroundColor = oldfg;
            return temp;
        }

        // Displays a command prompt and waits for the user to enter a string, then returns that string
        public string CmdPrompt(string prompt)
        {
            System.Console.WriteLine(String.Format("{0}: ", prompt));
            return ReadLine();
        }

        // Displays a command prompt and returns a default value if the user simply presses enter
        public string CmdPrompt(string prompt, string defaultresponse)
        {
            string temp = CmdPrompt(String.Format("{0} [{1}]", prompt, defaultresponse));
            if (temp == String.Empty)
            {
                return defaultresponse;
            }
            else
            {
                return temp;
            }
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

        // Runs a command with a number of parameters
        public Object RunCmd(string Cmd, string[] cmdparams)
        {
            m_cmdParser.RunCmd(Cmd, cmdparams);
            return null;
        }

        // Shows data about something
        public void ShowCommands(string ShowWhat)
        {
            m_cmdParser.Show(ShowWhat);
        }

        public void Prompt()
        {
            string tempstr = CmdPrompt(m_componentName + "# ");
            RunCommand(tempstr);
        }

        public void RunCommand(string command)
        {
            string[] tempstrarray;
            tempstrarray = command.Split(' ');
            string cmd = tempstrarray[0];
            Array.Reverse(tempstrarray);
            Array.Resize<string>(ref tempstrarray, tempstrarray.Length - 1);
            Array.Reverse(tempstrarray);
            string[] cmdparams = (string[]) tempstrarray;
            try
            {
                RunCmd(cmd, cmdparams);
            }
            catch (Exception e)
            {
                m_log.Error("[Console]: Command failed with exception " + e.ToString());
            }
        }

        public string LineInfo
        {
            get
            {
                string result = String.Empty;

                string stacktrace = Environment.StackTrace;
                List<string> lines = new List<string>(stacktrace.Split(new string[] {"at "}, StringSplitOptions.None));

                if (lines.Count > 4)
                {
                    lines.RemoveRange(0, 4);

                    string tmpLine = lines[0];

                    int inIndex = tmpLine.IndexOf(" in ");

                    if (inIndex > -1)
                    {
                        result = tmpLine.Substring(0, inIndex);

                        int lineIndex = tmpLine.IndexOf(":line ");

                        if (lineIndex > -1)
                        {
                            lineIndex += 6;
                            result += ", line " + tmpLine.Substring(lineIndex, (tmpLine.Length - lineIndex) - 5);
                        }
                    }
                }
                return result;
            }
        }
    }
}
