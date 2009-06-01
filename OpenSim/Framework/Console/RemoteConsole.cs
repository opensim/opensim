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
using System.Threading;
using Nini.Config;
using OpenSim.Framework.Servers.HttpServer;
using log4net;

namespace OpenSim.Framework.Console
{
    // A console that uses REST interfaces
    //
    public class RemoteConsole : CommandConsole
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // private IHttpServer m_Server = null;
        // private IConfigSource m_Config = null;

        private List<string> m_Scrollback = new List<string>();
        private ManualResetEvent m_DataEvent = new ManualResetEvent(false);
        private List<string> m_InputData = new List<string>();
        private uint m_LineNumber = 1;

        public RemoteConsole(string defaultPrompt) : base(defaultPrompt)
        {
        }

        public void ReadConfig(IConfigSource config)
        {
            // m_Config = config;
        }

        public void SetServer(IHttpServer server)
        {
            // m_Server = server;
        }

        public override void Output(string text, string level)
        {
            lock (m_Scrollback)
            {
                while (m_Scrollback.Count >= 1000)
                    m_Scrollback.RemoveAt(0);
                m_Scrollback.Add(String.Format("{0}", m_LineNumber)+":"+level+":"+text);
                m_LineNumber++;
            }
            System.Console.Write(text);
        }

        public override string ReadLine(string p, bool isCommand, bool e)
        {
            System.Console.Write("{0}", prompt);
            
            m_DataEvent.WaitOne();

            lock (m_InputData)
            {
                if (m_InputData.Count == 0)
                {
                    m_DataEvent.Reset();
                    return "";
                }

                string cmdinput = m_InputData[0];
                m_InputData.RemoveAt(0);
                if (m_InputData.Count == 0)
                    m_DataEvent.Reset();

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
}
