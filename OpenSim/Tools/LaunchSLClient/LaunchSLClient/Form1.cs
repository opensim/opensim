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

namespace LaunchSLClient
{
    public partial class Form1 : Form
    {
        const string deepGridUrl = "http://user.deepgrid.com:8002/";
        const string osGridUrl = "http://www.osgrid.org:8002/";
        const string openLifeGridUrl = "http://logingrid.net:8002/";

        string gridUrl = "";
        string sandboxUrl = "";
        string runUrl = "";
        string runLine = "";
        string exeFlags = "";
        string exePath = "";

        private MachineConfig m_machineConfig;

        public Form1()
        {
            InitializeComponent();
            ArrayList menuItems = new ArrayList();

            m_machineConfig = getMachineConfig();
            m_machineConfig.GetClient(ref exePath, ref runLine, ref exeFlags);

            menuItems.Add("Please select one:");

            addLocalSims(ref menuItems);

            menuItems.Add("OSGrid - www.osgrid.org");
            menuItems.Add("DeepGrid - www.deepgrid.com");
            menuItems.Add("OpenlifeGrid - www.openlifegrid.com");
            menuItems.Add("Linden Labs - www.secondlife.com");

            comboBox1.DataSource = menuItems;
        }

        private MachineConfig getMachineConfig()
        {
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                if (File.Exists("/System/Library/Frameworks/Cocoa.framework/Cocoa"))
                {
                    return new OSXConfig();
                }
                else
                {
                    return new UnixConfig();
                }
            }
            else
            {
                return new WindowsConfig();
            }
        }

        private void addLocalSandbox(ref ArrayList menuItems)
        {
            // build sandbox URL from Regions/default.xml
            // this is highly dependant on a standard default.xml
            if (File.Exists("Regions/default.xml"))
            {
                string sandboxHostName = "";
                string sandboxPort = "";
                string text;
                
                Regex myRegex = new Regex(".*internal_ip_port=\\\"(?<port>.*?)\\\".*external_host_name=\\\"(?<name>.*?)\\\".*");

                FileInfo defaultFile = new FileInfo("Regions/default.xml");
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
            if (File.Exists("network_servers_information.xml"))
            {
                string text;
                FileInfo defaultFile = new FileInfo("network_servers_information.xml");
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
            string configDir = m_machineConfig.GetConfigDir();

            if (!string.IsNullOrEmpty(configDir))
            {
                Directory.SetCurrentDirectory(configDir);

                addLocalSandbox(ref menuItems);
                addLocalGrid(ref menuItems);
            }
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
            proc.StartInfo.Arguments = exeFlags + " " + runUrl;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardOutput = false;
            proc.StartInfo.WorkingDirectory = exePath;
            proc.Start();
            proc.WaitForExit();
        }
    }
}
