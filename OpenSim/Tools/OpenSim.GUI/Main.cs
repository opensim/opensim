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
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace OpenSim.GUI
{
    public partial class Main : Form
    {

        public ProcessManager proc_OpenSim;
        public ProcessManager proc_UserServer;
        public ProcessManager proc_GridServer;
        public ProcessManager proc_AssetServer;

        public Main()
        {
            InitializeComponent();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            txtInputUserServer.KeyPress += new KeyPressEventHandler(txtInputUserServer_KeyPress);
            txtInputGridServer.KeyPress += new KeyPressEventHandler(txtInputGridServer_KeyPress);
            txtInputAssetServer.KeyPress += new KeyPressEventHandler(txtInputAssetServer_KeyPress);
            txtInputRegionServer.KeyPress += new KeyPressEventHandler(txtInputRegionServer_KeyPress);

            tabLogs.Selected += new TabControlEventHandler(tabLogs_Selected);

            UpdateTabVisibility();
        }

        void tabLogs_Selected(object sender, TabControlEventArgs e)
        {
            if (e.TabPage == tabUserServer)
                txtInputUserServer.Focus();
            if (e.TabPage == tabGridServer)
                txtInputGridServer.Focus();
            if (e.TabPage == tabAssetServer)
                txtInputAssetServer.Focus();
            if (e.TabPage == tabRegionServer)
                txtInputRegionServer.Focus();
        }

        void txtInputUserServer_KeyPress(object sender, KeyPressEventArgs e)
        {

            if (e.KeyChar == 13)
            {
                // We got a command
                e.Handled = true;
                proc_UserServer.StandardInput.WriteLine(txtInputUserServer.Text + "\r\n");
                txtInputUserServer.Text = "";
            }
        }

        void txtInputGridServer_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                // We got a command
                e.Handled = true;
                proc_GridServer.StandardInput.WriteLine(txtInputGridServer.Text + "\r\n");
                txtInputGridServer.Text = "";
            }
        }

        void txtInputAssetServer_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                // We got a command
                e.Handled = true;
                proc_AssetServer.StandardInput.WriteLine(txtInputAssetServer.Text + "\r\n");
                txtInputAssetServer.Text = "";
            }
        }

        void txtInputRegionServer_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == 13)
            {
                // We got a command
                e.Handled = true;
                proc_OpenSim.StandardInput.WriteLine(txtInputRegionServer.Text + "\r\n");
                txtInputRegionServer.Text = "";
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            noProbe1.Checked = true;
            multiple1.Checked = true;
            loginuri1.Checked = true;
            login1.Checked = true;
            //
            // THIS PART NEEDS TO BE MOVED TO A SEPARATE THREAD OR A TIMER OF SOME SORT
            // should not block on wait
            // ALSO - IF SOME SERVICES ARE NOT CONFIGURED, POP UP CONFIGURATION BOX FOR THAT SERVICE!
            //

            btnStart.Enabled = false;
            btnStop.Enabled = false;



            if (rbGridServer.Checked)
            {
                // Start UserServer
                proc_UserServer = new ProcessManager("OpenSim.Grid.UserServer.exe", "");
                txtMainLog.AppendText("Starting: User server" + "\r\n");
                proc_UserServer.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_UserServer_DataReceived);
                proc_UserServer.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_UserServer_DataReceived);
                proc_UserServer.StartProcess();
                System.Threading.Thread.Sleep(3000);

                // Start GridServer
                proc_GridServer = new ProcessManager("OpenSim.Grid.GridServer.exe", "");
                txtMainLog.AppendText("Starting: Grid server" + "\r\n");
                proc_GridServer.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_GridServer_DataReceived);
                proc_GridServer.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_GridServer_DataReceived);
                proc_GridServer.StartProcess();
                System.Threading.Thread.Sleep(3000);

                // Start AssetServer
                proc_AssetServer = new ProcessManager("OpenSim.Grid.AssetServer.exe", "");
                txtMainLog.AppendText("Starting: Asset server" + "\r\n");
                proc_AssetServer.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_AssetServer_DataReceived);
                proc_AssetServer.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_AssetServer_DataReceived);
                proc_AssetServer.StartProcess();
                System.Threading.Thread.Sleep(3000);
            }

            // Start OpenSim
            string p = "";
            if (rbGridServer.Checked)
                p = "-gridmode=true";

            proc_OpenSim = new ProcessManager("OpenSim.EXE", p);
            txtMainLog.AppendText("Starting: OpenSim (Region server)" + "\r\n");
            proc_OpenSim.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_OpenSim_DataReceived);
            proc_OpenSim.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_OpenSim_DataReceived);
            proc_OpenSim.StartProcess();

            btnStart.Enabled = false;
            btnStop.Enabled = true;

        }
        public delegate void AppendText(string Text);
        void proc_UserServer_DataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            this.Invoke(new AppendText(txtUserServer.AppendText), new object[] { e.Data + "\r\n" });
            this.Invoke(new AppendText(txtMainLog.AppendText), new object[] { "UserServer: " + e.Data + "\r\n" });
        }
        void proc_GridServer_DataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            this.Invoke(new AppendText(txtGridServer.AppendText), new object[] { e.Data + "\r\n" });
            this.Invoke(new AppendText(txtMainLog.AppendText), new object[] { "GridServer: " + e.Data + "\r\n" });
        }
        void proc_AssetServer_DataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            this.Invoke(new AppendText(txtAssetServer.AppendText), new object[] { e.Data + "\r\n" });
            this.Invoke(new AppendText(txtMainLog.AppendText), new object[] { "AssetServer: " + e.Data + "\r\n" });
        }
        void proc_OpenSim_DataReceived(object sender, System.Diagnostics.DataReceivedEventArgs e)
        {
            this.Invoke(new AppendText(txtOpenSim.AppendText), new object[] { e.Data + "\r\n" });
            this.Invoke(new AppendText(txtMainLog.AppendText), new object[] { "OpenSim: " + e.Data + "\r\n" });
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            btnStop.Enabled = false;
            Stop();
            btnStart.Enabled = true;
            btnStop.Enabled = false;
        }

        private void clear1_Click(object sender, EventArgs e)
        {
            noProbe1.Checked = false; multiple1.Checked = false; loginuri1.Checked = false;
            noMultiple1.Checked = false; korean1.Checked = false; spanish1.Checked = false;
            ignorepixeldepth1.Checked = false; nothread1.Checked = false; safe1.Checked = false;
            noconsole1.Checked = false; log1.Checked = false; helperuri1.Checked = false;
            autologin1.Checked = false; dialog1.Checked = false; previous1.Checked = false;
            simple1.Checked = false; noinvlib1.Checked = false; noutc1.Checked = false;
            debugst1.Checked = false; local1.Checked = false; purge1.Checked = false;
            nofmod1.Checked = false; nosound1.Checked = false; noaudio1.Checked = false;
            url1.Checked = false; port1.Checked = false; drop1.Checked = false;
            inbw1.Checked = false; outbw1.Checked = false; settings1.Checked = false;
            logfile1.Checked = false; yield1.Checked = false; techTag1.Checked = false;
            quitAfter1.Checked = false; loginuri1.Checked = false; set1.Checked = false;
            errmask1.Checked = false; raw1.Checked = false; skin1.Checked = false;
            user1.Checked = false; login1.Checked = false;
        }

        private void Stop()
        {
            if (proc_UserServer != null)
            {
                txtMainLog.AppendText("Shutting down UserServer. CPU time used: " + proc_UserServer.TotalProcessorTime.ToString() + "\r\n");
                proc_UserServer.StopProcess();
                proc_UserServer = null;
            }
            if (proc_GridServer != null)
            {
                txtMainLog.AppendText("Shutting down GridServer. CPU time used: " + proc_GridServer.TotalProcessorTime.ToString() + "\r\n");
                proc_GridServer.StopProcess();
                proc_GridServer = null;
            }
            if (proc_AssetServer != null)
            {
                txtMainLog.AppendText("Shutting down AssetServer. CPU time used: " + proc_AssetServer.TotalProcessorTime.ToString() + "\r\n");
                proc_AssetServer.StopProcess();
                proc_AssetServer = null;
            }
            if (proc_OpenSim != null)
            {
                txtMainLog.AppendText("Shutting down OpenSim. CPU time used: " + proc_OpenSim.TotalProcessorTime.ToString() + "\r\n");
                proc_OpenSim.StopProcess();
                proc_OpenSim = null;
            }
        }
        private void UpdateTabVisibility()
        {
            if (rbStandAloneMode.Checked)
            {
                if (tabLogs.TabPages.Contains(tabUserServer))
                    tabLogs.TabPages.Remove(tabUserServer);
                if (tabLogs.TabPages.Contains(tabGridServer))
                    tabLogs.TabPages.Remove(tabGridServer);
                if (tabLogs.TabPages.Contains(tabAssetServer))
                    tabLogs.TabPages.Remove(tabAssetServer);
            }
            else
            {
                if (!tabLogs.TabPages.Contains(tabUserServer))
                    tabLogs.TabPages.Add(tabUserServer);
                if (!tabLogs.TabPages.Contains(tabGridServer))
                    tabLogs.TabPages.Add(tabGridServer);
                if (!tabLogs.TabPages.Contains(tabAssetServer))
                    tabLogs.TabPages.Add(tabAssetServer);
            }
        }

        private void rbStandAloneMode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateTabVisibility();
        }

        private void rbGridRegionMode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateTabVisibility();
        }

        private void rbGridServer_CheckedChanged(object sender, EventArgs e)
        {
            UpdateTabVisibility();
        }

        private int counter;

        private void Exit()
        {
            counter = 0;
            timer1.Interval = 600;
            timer1.Enabled = true;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
        }

        private void timer1_Tick(object sender, System.EventArgs e)
        {
            if (counter >= 10)
            {
                timer1.Enabled = false;
                counter = 0;
                Application.Exit();
            }
            else
            {
                counter = counter + 1;
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (proc_UserServer != null || proc_GridServer != null || proc_AssetServer != null || proc_OpenSim != null)
            {
                label6.Text = "Stopping server(s) and waiting to safely close..............";
                Stop();
                Exit();
            }
            else
            {
                Application.Exit();
            }
        }
        /// <summary>
        /// CLIENT SECTION
        /// </summary>
        string exeString;
        string exeArgs;
        string usrsvr;
        string error = "Reconsider your commandline choices, you have opposing values selected!";

        private void label6_Click(object sender, EventArgs e)
        {
            label6.Text = clientBox1.Text;
        }
        private void errorSwitches()
        {
            MessageBox.Show(error);
            label6.Text = error;
        }
        bool exists;
        private void Launch1_Click(object sender, EventArgs e)
        {
            if (exists = System.IO.File.Exists(clientBox1.Text + exeBox1.Text))
            {
                executeClient();
            }
            else
            {
                MessageBox.Show("FILE DOES NOT EXIST!!!");
                label6.Text = "FILE DOES NOT EXIST!!!";
            }
        }
        private void NATfix()
        {
        }
        private void executeClient()
        {
            label6.Text = "";
            exeArgs = "";
            exeString = clientBox1.Text;
            exeString = exeString += exeBox1.Text;

            if (multiple1.Checked == true && noMultiple1.Checked == true) errorSwitches();
            else if (korean1.Checked == true && spanish1.Checked == true) errorSwitches();
            else
            {
                if (noProbe1.Checked == true) exeArgs = exeArgs += " -noprobe";
                if (multiple1.Checked == true) exeArgs = exeArgs += " -multiple";
                if (noMultiple1.Checked == true) exeArgs = exeArgs += " -nomultiple";
                if (korean1.Checked == true) exeArgs = exeArgs += " -korean";
                if (spanish1.Checked == true) exeArgs = exeArgs += " -spanish";
                if (ignorepixeldepth1.Checked == true) exeArgs = exeArgs += " -ignorepixeldepth";
                if (nothread1.Checked == true) exeArgs = exeArgs += " -nothread";
                if (safe1.Checked == true) exeArgs = exeArgs += " -safe";
                if (noconsole1.Checked == true) exeArgs = exeArgs += " -noconsole";
                if (log1.Checked == true) exeArgs = exeArgs += " -log";
                if (helperuri1.Checked == true) exeArgs = exeArgs += " -helperuri";
                if (autologin1.Checked == true) exeArgs = exeArgs += " --autologin";
                if (dialog1.Checked == true) exeArgs = exeArgs += " -dialog";
                if (previous1.Checked == true) exeArgs = exeArgs += " -previous";
                if (simple1.Checked == true) exeArgs = exeArgs += " -simple";
                if (noinvlib1.Checked == true) exeArgs = exeArgs += " -noinvlib";
                if (noutc1.Checked == true) exeArgs = exeArgs += " -noutc";
                if (debugst1.Checked == true) exeArgs = exeArgs += " -debugst";
                if (local1.Checked == true) exeArgs = exeArgs += " -local";
                if (purge1.Checked == true) exeArgs = exeArgs += " -purge";
                if (nofmod1.Checked == true) exeArgs = exeArgs += " -nofmod";
                if (nosound1.Checked == true) exeArgs = exeArgs += " -nosound";
                if (noaudio1.Checked == true) exeArgs = exeArgs += " -noaudio";
                if (url1.Checked == true)
                {
                    exeArgs = exeArgs += " -url ";
                    exeArgs = exeArgs += simBox1.Text;
                }
                if (port1.Checked == true)
                {
                    int aPort;
                    aPort = Convert.ToInt32(portBox1.Text);
                    if (aPort > 13050)
                    {
                        portBox1.Text = "13050";
                        MessageBox.Show("Enter Usable port number, defaulting to 13050.");
                    }
                    if (aPort < 13000)
                    {
                        portBox1.Text = "13000";
                        MessageBox.Show("Enter Usable port number, defaulting to 13000.");
                    }
                    else
                    {
                    }
                    exeArgs = exeArgs += " -port ";
                    exeArgs = exeArgs += portBox1.Text;
                }
                if (drop1.Checked == true)
                {
                    int aPct;
                    aPct = Convert.ToInt32(dropBox1.Text);
                    if (aPct > 100)
                    {
                        dropBox1.Text = "100";
                        MessageBox.Show("Enter Usable port number, defaulting to 100.");
                    }
                    if (aPct < 0)
                    {
                        dropBox1.Text = "0";
                        MessageBox.Show("Enter Usable port number, defaulting to 0.");
                    }
                    else
                    {
                    }
                    exeArgs = exeArgs += " -drop ";
                    exeArgs = exeArgs += dropBox1.Text;
                }
                if (inbw1.Checked == true)
                {
                    exeArgs = exeArgs += " -inbw ";
                    exeArgs = exeArgs += inbwBox1.Text;
                }
                if (outbw1.Checked == true)
                {
                    exeArgs = exeArgs += " -outbw ";
                    exeArgs = exeArgs += outbwBox1.Text;
                }
                if (settings1.Checked == true)
                {
                    exeArgs = exeArgs += " -settings ";
                    exeArgs = exeArgs += settingsBox1.Text;
                }
                if (logfile1.Checked == true)
                {
                    exeArgs = exeArgs += " -logfile ";
                    exeArgs = exeArgs += logfileBox1.Text;
                }
                if (yield1.Checked == true)
                {
                    exeArgs = exeArgs += " -yield ";
                    exeArgs = exeArgs += yieldBox1.Text;
                }
                if (techTag1.Checked == true)
                {
                    exeArgs = exeArgs += " -techtag ";
                    exeArgs = exeArgs += techtagBox1.Text;
                }
                if (quitAfter1.Checked == true)
                {
                    exeArgs = exeArgs += " -quitafter ";
                    exeArgs = exeArgs += quitafterBox1.Text;
                }
                if (loginuri1.Checked == true)
                {
                    exeArgs = exeArgs += " -loginuri ";
                    exeArgs = exeArgs += loginuriBox1.Text;
                }
                if (set1.Checked == true)
                {
                    exeArgs = exeArgs += " -set ";
                    exeArgs = exeArgs += setBox1.Text;
                }
                if (errmask1.Checked == true)
                {
                    exeArgs = exeArgs += " -errmask ";
                    exeArgs = exeArgs += errmaskBox1.Text;
                }
                if (raw1.Checked == true)
                {
                    exeArgs = exeArgs += " " + rawBox1.Text;
                }
                if (skin1.Checked == true)
                {
                    bool exists;
                    if (exists = System.IO.File.Exists(skinBox1.Text + "skin.xml"))
                    {
                        exeArgs = exeArgs += " -skin ";
                        exeArgs = exeArgs += skinBox1.Text + "skin.xml";
                    }
                    else
                    {
                        MessageBox.Show("SKIN FILE DOES NOT EXIST AT SPECIFIED LOCATION!!!");
                        skin1.Checked = false;
                        executeClient();
                    }
                }
                if (user1.Checked == true)
                {
                    //find actual login urls
                    if (comboBox1.Text == "agni") { usrsvr = " -user " + "--agni"; }
                    if (comboBox1.Text == "colo") { usrsvr = " -user " + "--colo"; }
                    if (comboBox1.Text == "dmz") { usrsvr = " -user " + "--dmz"; }
                    if (comboBox1.Text == "durga") { usrsvr = " -user " + "--Durga"; }
                    if (comboBox1.Text == "siva") { usrsvr = " -user " + "--siva"; }
                    exeArgs = exeArgs += usrsvr;
                }
                if (login1.Checked == true)
                {
                    exeArgs = exeArgs += " -login ";
                    exeArgs = exeArgs += firstBox1.Text + " " + lastBox1.Text + " " + passBox1.Text;
                }
                label6.Text = exeString + exeArgs;
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo.FileName = exeString;
                proc.StartInfo.Arguments = exeArgs;
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardOutput = false;
                proc.StartInfo.WorkingDirectory = clientBox1.Text;
                proc.Start();
            }
        }
    }
}
