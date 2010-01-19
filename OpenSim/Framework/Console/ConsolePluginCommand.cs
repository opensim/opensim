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

namespace OpenSim.Framework.Console
{
    public delegate void ConsoleCommand(string[] comParams);

    /// <summary>
    /// Holder object for a new console plugin command
    ///
    /// Override the methods like Run and IsHelpfull (but the defaults might work ok.)
    /// </summary>
    public class ConsolePluginCommand
    {
        /// <summary>
        /// command delegate used in running
        /// </summary>
        private ConsoleCommand m_commandDelegate;
        /// <summary>
        /// help text displayed
        /// </summary>
        private string m_helpText;
        /// <summary>
        /// command in the form of "showme new commands"
        /// </summary>
        private string[] m_cmdText;

        /// <summary>
        /// Construct a new ConsolePluginCommand
        ///
        /// for use with OpenSim.RegisterConsolePluginCommand(myCmd);
        ///
        /// </summary>
        /// <param name="command">in the form of "showme new commands"</param>
        /// <param name="dlg">ommand delegate used in running</param>
        /// <param name="help">the text displayed in "help showme new commands"</param>
        public ConsolePluginCommand(string command, ConsoleCommand dlg, string help)
        {
            m_cmdText = command.Split(new char[] { ' ' });
            m_commandDelegate = dlg;
            m_helpText = help;
        }

        /// <summary>
        /// Returns the match length this command has upon the 'cmdWithParams'
        /// At least a higher number for "show plugin status" then "show" would return
        /// This is used to have multi length command verbs
        ///
        /// @see OopenSim.RunPluginCommands
        /// It will only run the one with the highest number
        ///
        /// </summary>
        public int matchLength(string cmdWithParams)
        {
            // QUESTION: have a case insensitive flag?
            cmdWithParams = cmdWithParams.ToLower().Trim();
            string matchText = String.Join(" ",m_cmdText).ToLower().Trim();
            if (cmdWithParams.StartsWith(matchText))
            {
                // QUESTION Instead return cmdText.Length; ?
                return matchText.Length;
            }
            return 0;
        }

        /// <summary>
        /// Run the delegate the incomming string may contain the command, if so, it is chopped off the cmdParams[]
        /// </summary>
        public void Run(string cmd, string[] cmdParams)
        {
            int skipParams = 0;
            if (m_cmdText.Length > 1)
            {
                int currentParam = 1;
                while (currentParam < m_cmdText.Length)
                {
                    if (cmdParams[skipParams].ToLower().Equals(m_cmdText[currentParam].ToLower()))
                    {
                        skipParams++;
                    }
                    currentParam++;
                }

            }
            string[] sendCmdParams = cmdParams;
            if (skipParams > 0)
            {
                sendCmdParams = new string[cmdParams.Length-skipParams];
                for (int i=0;i<sendCmdParams.Length;i++) {
                    sendCmdParams[i] = cmdParams[skipParams++];
                }
            }
            m_commandDelegate(sendCmdParams);//.Trim().Split(new char[] { ' ' }));
        }

        /// <summary>
        /// Shows help information on the console's Notice method
        /// </summary>
        public void ShowHelp(ConsoleBase console)
        {
            console.Output(String.Join(" ", m_cmdText) + " - " + m_helpText + "\n");
        }

        /// <summary>
        /// return true if the ShowHelp(..) method might be helpfull
        /// </summary>
        public bool IsHelpfull(string cmdWithParams)
        {
            cmdWithParams = cmdWithParams.ToLower();
            return cmdWithParams.Contains(String.Join(" ", m_cmdText).ToLower()) || m_helpText.ToLower().Contains(cmdWithParams);
        }
    }
}
