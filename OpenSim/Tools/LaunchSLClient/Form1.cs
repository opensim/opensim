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
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LaunchSLClient
{
    public partial class Form1 : Form
    {
        const string deepGridUrl = "http://user.deepgrid.com:8002/";
        const string osGridUrl = "http://www.osgrid.org:8002/";
        const string openLifeGridUrl = "http://logingrid.net:8002";

        string gridUrl = "";
        string sandboxUrl = "";
        string runUrl = "";
        string runLine = "";
        string exeFlags = "";
        string exePath = "";

        private void addLocalSandbox(ref ArrayList menuItems)
        {
            // build sandbox URL from Regions\default.xml
            // this is highly dependant on a standard default.xml
            if (File.Exists(@"Regions\default.xml"))
            {
                string sandboxHostName = "";
                string sandboxPort = "";
                string text;
                
                Regex myRegex = new Regex(".*internal_ip_port=\\\"(?<port>.*?)\\\".*external_host_name=\\\"(?<name>.*?)\\\".*");

                FileInfo defaultFile = new FileInfo(@"Regions\default.xml");
                StreamReader stream = defaultFile.OpenText();
                do
                {
                    text = stream.ReadLine();
                    if (text == null)
                    {
                        break;
                    }
                    MatchCollection theMatches = myRegex.Matches(text);
                    foreach (Match theMatch in theMatches)
                    {
                        if (theMatch.Length != 0)
                        {
                            sandboxHostName = theMatch.Groups["name"].ToString();
                            sandboxPort = theMatch.Groups["port"].ToString();
                        }
                    }
                } while (text != null);

                stream.Close();
                sandboxUrl = "http:\\" + sandboxHostName + ":" + sandboxPort;
                menuItems.Add("Local Sandbox");
            }
        }

        private void addLocalGrid(ref ArrayList menuItems)
        {
            //build local grid URL from network_servers_information.xml
            // this is highly dependant on a standard default.xml
            if (File.Exists(@"network_servers_information.xml"))
            {
                string text;
                FileInfo defaultFile = new FileInfo(@"network_servers_information.xml");
                Regex myRegex = new Regex(".*UserServerURL=\\\"(?<url>.*?)\\\".*");
                StreamReader stream = defaultFile.OpenText();

                do
                {
                    text = stream.ReadLine();
                    if (text == null)
                    {
                        break;
                    }
                    foreach (Match theMatch in myRegex.Matches(text))
                    {
                        if (theMatch.Length != 0)
                        {
                            gridUrl = theMatch.Groups["url"].ToString();
                        }
                    }
                } while (text != null);
                stream.Close();
                if (gridUrl != null)
                {
                    menuItems.Add("Local Grid Server");
                }
            }
        }

        private void addLocalSims(ref ArrayList menuItems)
        {
            // find opensim directory
            RegistryKey exeKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\OpenSim\OpenSim");
            if (exeKey != null)
            {
                Object simPath = exeKey.GetValue("Path");

                Directory.SetCurrentDirectory(simPath.ToString());  //this should be set to wherever we decide to put the binaries

                addLocalSandbox(ref menuItems);
                addLocalGrid(ref menuItems);
            }
            else
            {
                MessageBox.Show("No OpenSim installed. Showing public grids only", "No OpenSim");
            }
        }

        private void getClient(ref string exePath, ref string runLine, ref string exeFlags)
        {
            // get executable path from registry
            RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Linden Research, Inc.\SecondLife");
            if (regKey == null)
            {
                regKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Linden Research, Inc.\SecondLife");
                if (regKey == null)
                {
                    throw new LauncherException("Can't find Second Life. Are you sure it is installed?", "LauncherException.Form1");
                }
            }
            string exe = regKey.GetValue("Exe").ToString();
            exeFlags = regKey.GetValue("Flags").ToString();
            exePath = regKey.GetValue("").ToString();
            runLine = exePath + "\\" + exe;
            Registry.LocalMachine.Flush();
            Registry.LocalMachine.Close();
        }

        public Form1()
        {
            InitializeComponent();
            ArrayList menuItems = new ArrayList();

            getClient(ref exePath, ref runLine, ref exeFlags);

            menuItems.Add("Please select one:");

            addLocalSims(ref menuItems);

            menuItems.Add("OSGrid - www.osgrid.org");
            menuItems.Add("DeepGrid - www.deepgrid.com");
            menuItems.Add("OpenlifeGrid - www.openlifegrid.com");
            menuItems.Add("Linden Labs - www.secondlife.com");

            comboBox1.DataSource = menuItems;
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.Text == "Please select one:") { return; }
            if (comboBox1.Text == "Local Sandbox") { runUrl=" -loginuri " + sandboxUrl;}
            if (comboBox1.Text == "Local Grid Server") { runUrl = " -loginuri " + gridUrl; }
            if (comboBox1.Text == "DeepGrid - www.deepgrid.com") { runUrl = " -loginuri " + deepGridUrl; }
            if (comboBox1.Text == "OSGrid - www.osgrid.org") { runUrl = " -loginuri " + osGridUrl; }
            if (comboBox1.Text == "OpenlifeGrid - www.openlifegrid.com") { runUrl = " -loginuri " + openLifeGridUrl; }
            if (comboBox1.Text == "Linden Labs - www.secondlife.com") { runUrl = ""; }

            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo.FileName = runLine;
            proc.StartInfo.Arguments = exeFlags.ToString() + " " + runUrl;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = false;
            proc.StartInfo.WorkingDirectory = exePath.ToString();
            proc.Start();
            proc.WaitForExit();
        }
    }
}
