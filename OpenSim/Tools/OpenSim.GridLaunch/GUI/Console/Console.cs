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
using System.Text;

namespace OpenSim.GridLaunch.GUI.Console
{
    internal class Console: IGUI
    {
        private List<string> Apps = new List<string>();
        public Console ()
        {
            Program.AppCreated += Program_AppCreated;
            Program.AppRemoved += Program_AppRemoved;
            Program.AppConsoleOutput += Program_AppConsoleOutput;
            Program.Command.CommandLine += Command_CommandLine;
        }

        private string currentApp = "";
        private bool quitTyped = false;
        void Command_CommandLine(string application, string command, string arguments)
        {

            // If command is a number then someone might be trying to change console: /1, /2, etc.
            int currentAppNum = 0;
            if (int.TryParse(command, out currentAppNum))
                if (currentAppNum <= Apps.Count)
                {
                    currentApp = Apps[currentAppNum - 1];
                    System.Console.WriteLine("Changed console to app: " + currentApp);
                } else
                    System.Console.WriteLine("Unable to change to app number: " + currentAppNum);

            // Has user typed quit?
            if (command.ToLower() == "quit")
                quitTyped = true;

            // Has user typed /list?
            if (command.ToLower() == "list")
            {
                System.Console.WriteLine("/0    Log console");
                for (int i = 1; i <= Apps.Count; i++)
                {
                    System.Console.WriteLine(string.Format("/{0}    {1}", i, Apps[i - 1]));
                }
            }
        }
        #region Module Start / Stop
        public void StartGUI()
        {
            // Console start
            System.Console.WriteLine("Console GUI");
            System.Console.WriteLine("Use commands /0, /1, /2, etc to switch between applications.");
            System.Console.WriteLine("Type /list for list of applications.");
            System.Console.WriteLine("Anything that doesn't start with a / will be sent to selected application");
            System.Console.WriteLine("type /quit to exit");

            
            while (quitTyped == false)
            {
                string line = System.Console.ReadLine().TrimEnd("\r\n".ToCharArray());
                Program.Write(currentApp, line);
            }
            // We are done
            System.Console.WriteLine("Console exit.");
        }

        public void StopGUI()
        {
            // Console stop
        }
        #endregion

        #region GridLaunch Events
        void Program_AppCreated(string App)
        {
            System.Console.WriteLine("Started: " + App);
            if (!Apps.Contains(App))
                Apps.Add(App);
        }

        void Program_AppRemoved(string App)
        {
            System.Console.WriteLine("Stopped: " + App);
            if (Apps.Contains(App))
                Apps.Remove(App);
        }

        void Program_AppConsoleOutput(string App, string Text)
        {
            System.Console.Write(App + ": " + Text);
        }
        #endregion


    }
}
