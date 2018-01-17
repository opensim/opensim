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

using Nini.Config;
using log4net;
using System.Reflection;
using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using OpenSim.Server.Base;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenMetaverse;

namespace OpenSim.ConsoleClient
{
    public class OpenSimConsoleClient
    {
        protected static ServicesServerBase m_Server = null;
        private static string m_Host;
        private static int m_Port;
        private static string m_User;
        private static string m_Pass;
        private static UUID m_SessionID;

        static int Main(string[] args)
        {
            m_Server = new ServicesServerBase("Client", args);

            IConfig serverConfig = m_Server.Config.Configs["Startup"];
            if (serverConfig == null)
            {
                System.Console.WriteLine("Startup config section missing in .ini file");
                throw new Exception("Configuration error");
            }

            ArgvConfigSource argvConfig = new ArgvConfigSource(args);

            argvConfig.AddSwitch("Startup", "host", "h");
            argvConfig.AddSwitch("Startup", "port", "p");
            argvConfig.AddSwitch("Startup", "user", "u");
            argvConfig.AddSwitch("Startup", "pass", "P");

            m_Server.Config.Merge(argvConfig);

            m_User = serverConfig.GetString("user", "Test");
            m_Host = serverConfig.GetString("host", "localhost");
            m_Port = serverConfig.GetInt("port", 8003);
            m_Pass = serverConfig.GetString("pass", "secret");

            Requester.MakeRequest("http://"+m_Host+":"+m_Port.ToString()+"/StartSession/", String.Format("USER={0}&PASS={1}", m_User, m_Pass), LoginReply);

            string pidFile = serverConfig.GetString("PIDFile", String.Empty);

            while (m_Server.Running)
            {
                System.Threading.Thread.Sleep(500);
                // MainConsole.Instance.Prompt();
            }

            if (pidFile != String.Empty)
                File.Delete(pidFile);

            Environment.Exit(0);

            return 0;
        }

        private static void SendCommand(string module, string[] cmd)
        {
            string sendCmd = "";
            string[] cmdlist = new string[cmd.Length - 1];

            sendCmd = cmd[0];

            if (cmd.Length > 1)
            {
                Array.Copy(cmd, 1, cmdlist, 0, cmd.Length - 1);
                sendCmd += " \"" + String.Join("\" \"", cmdlist) + "\"";
            }

            Requester.MakeRequest("http://"+m_Host+":"+m_Port.ToString()+"/SessionCommand/", String.Format("ID={0}&COMMAND={1}", m_SessionID, sendCmd), CommandReply);
        }

        public static void LoginReply(string requestUrl, string requestData, string replyData)
        {
            XmlDocument doc = new XmlDocument();

            doc.LoadXml(replyData);

            XmlNodeList rootL = doc.GetElementsByTagName("ConsoleSession");
            if (rootL.Count != 1)
            {
                MainConsole.Instance.Output("Connection data info was not valid");
                Environment.Exit(1);
            }
            XmlElement rootNode = (XmlElement)rootL[0];

            if (rootNode == null)
            {
                MainConsole.Instance.Output("Connection data info was not valid");
                Environment.Exit(1);
            }

            XmlNodeList helpNodeL = rootNode.GetElementsByTagName("HelpTree");
            if (helpNodeL.Count != 1)
            {
                MainConsole.Instance.Output("Connection data info was not valid");
                Environment.Exit(1);
            }

            XmlElement helpNode = (XmlElement)helpNodeL[0];
            if (helpNode == null)
            {
                MainConsole.Instance.Output("Connection data info was not valid");
                Environment.Exit(1);
            }

            XmlNodeList sessionL = rootNode.GetElementsByTagName("SessionID");
            if (sessionL.Count != 1)
            {
                MainConsole.Instance.Output("Connection data info was not valid");
                Environment.Exit(1);
            }

            XmlElement sessionNode = (XmlElement)sessionL[0];
            if (sessionNode == null)
            {
                MainConsole.Instance.Output("Connection data info was not valid");
                Environment.Exit(1);
            }

            if (!UUID.TryParse(sessionNode.InnerText, out m_SessionID))
            {
                MainConsole.Instance.Output("Connection data info was not valid");
                Environment.Exit(1);
            }

            MainConsole.Instance.Commands.FromXml(helpNode, SendCommand);

            Requester.MakeRequest("http://"+m_Host+":"+m_Port.ToString()+"/ReadResponses/"+m_SessionID.ToString()+"/", String.Empty, ReadResponses);
        }

        public static void ReadResponses(string requestUrl, string requestData, string replyData)
        {
            XmlDocument doc = new XmlDocument();

            doc.LoadXml(replyData);

            XmlNodeList rootNodeL = doc.GetElementsByTagName("ConsoleSession");
            if (rootNodeL.Count != 1 || rootNodeL[0] == null)
            {
                Requester.MakeRequest(requestUrl, requestData, ReadResponses);
                return;
            }

            List<string> lines = new List<string>();

            foreach (XmlNode part in rootNodeL[0].ChildNodes)
            {
                if (part.Name != "Line")
                    continue;

                lines.Add(part.InnerText);
            }

            // Cut down scrollback to 100 lines (4 screens)
            // for the command line client
            //
            while (lines.Count > 100)
                lines.RemoveAt(0);

            string prompt = String.Empty;

            foreach (string l in lines)
            {
                string[] parts = l.Split(new char[] {':'}, 3);
                if (parts.Length != 3)
                    continue;

                if (parts[2].StartsWith("+++") || parts[2].StartsWith("-++"))
                    prompt = parts[2];
                else
                    MainConsole.Instance.Output(parts[2].Trim(), parts[1]);
            }


            Requester.MakeRequest(requestUrl, requestData, ReadResponses);

            if (prompt.StartsWith("+++"))
                MainConsole.Instance.ReadLine(prompt.Substring(3), true, true);
            else if (prompt.StartsWith("-++"))
                SendCommand(String.Empty, new string[] { MainConsole.Instance.ReadLine(prompt.Substring(3), false, true) });
        }

        public static void CommandReply(string requestUrl, string requestData, string replyData)
        {
        }
    }
}
