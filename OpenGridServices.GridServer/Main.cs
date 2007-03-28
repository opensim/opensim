/*
Copyright (c) OpenSim project, http://osgrid.org/


* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.IO;
using System.Text;
using libsecondlife;
using OpenSim.Framework.Console;

namespace OpenGridServices.GridServer
{
    /// <summary>
    /// </summary>
    public class OpenGrid_Main : conscmd_callback
    {

        public static OpenGrid_Main thegrid;
        public string GridOwner;
        public string DefaultStartupMsg;
        public string DefaultAssetServer;
        public string AssetSendKey;
        public string AssetRecvKey;
        public string DefaultUserServer;
        public string UserSendKey;
        public string UserRecvKey;

        public GridHTTPServer _httpd;
        public SimProfileManager _regionmanager;

        private ConsoleBase m_console;
        
        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("Starting...\n");

            thegrid = new OpenGrid_Main();
            thegrid.Startup();

            thegrid.Work();
        }

        private void Work()
        {
            m_console.WriteLine("\nEnter help for a list of commands\n");

            while (true)
            {
                m_console.MainConsolePrompt();
            }
        }

        private OpenGrid_Main()
        {
            m_console = new ConsoleBase("opengrid-gridserver-console.log", "OpenGrid", this);
            MainConsole.Instance = m_console;
        }
        
        public void Startup()
        {
            m_console.WriteLine("Main.cs:Startup() - Please press enter to retain default settings");

            this.GridOwner = m_console.CmdPrompt("Grid owner [OGS development team]: ", "OGS development team");
            this.DefaultStartupMsg = m_console.CmdPrompt("Default startup message for clients [Welcome to OGS!]: ", "Welcome to OGS!");

            this.DefaultAssetServer = m_console.CmdPrompt("Default asset server [no default]: ");
            this.AssetSendKey = m_console.CmdPrompt("Key to send to asset server: ");
            this.AssetRecvKey = m_console.CmdPrompt("Key to expect from asset server: ");

            this.DefaultUserServer = m_console.CmdPrompt("Default user server [no default]: ");
            this.UserSendKey = m_console.CmdPrompt("Key to send to user server: ");
            this.UserRecvKey = m_console.CmdPrompt("Key to expect from user server: ");

            m_console.WriteLine("Main.cs:Startup() - Starting HTTP process");
            _httpd = new GridHTTPServer();

            this._regionmanager = new SimProfileManager();
            _regionmanager.CreateNewProfile("OpenSim Test", "http://there-is-no-caps.com", "4.78.190.75", 9000, 997, 996, this.UserSendKey, this.UserRecvKey);
        }

        public void RunCmd(string cmd, string[] cmdparams)
        {
            switch (cmd)
            {
                case "help":
                    m_console.WriteLine("shutdown - shutdown the grid (USE CAUTION!)");
                    break;

                case "shutdown":
                    m_console.Close();
                    Environment.Exit(0);
                    break;
            }
        }

        public void Show(string ShowWhat)
        {
        }
    }
}
