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

namespace OpenSim.GUI
{
    partial class Main
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            this.fileToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.clientBox1 = new System.Windows.Forms.TextBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.rbGridRegionMode = new System.Windows.Forms.RadioButton();
            this.rbStandAloneMode = new System.Windows.Forms.RadioButton();
            this.rbGridServer = new System.Windows.Forms.RadioButton();
            this.Launch1 = new System.Windows.Forms.Button();
            this.gbLog = new System.Windows.Forms.GroupBox();
            this.tabLogs = new System.Windows.Forms.TabControl();
            this.tabMainLog = new System.Windows.Forms.TabPage();
            this.txtMainLog = new System.Windows.Forms.TextBox();
            this.tabRegionServer = new System.Windows.Forms.TabPage();
            this.txtInputRegionServer = new OpenSim.GUI.InputTextBoxControl();
            this.label1 = new System.Windows.Forms.Label();
            this.txtOpenSim = new System.Windows.Forms.TextBox();
            this.tabUserServer = new System.Windows.Forms.TabPage();
            this.txtInputUserServer = new OpenSim.GUI.InputTextBoxControl();
            this.label2 = new System.Windows.Forms.Label();
            this.txtUserServer = new System.Windows.Forms.TextBox();
            this.tabAssetServer = new System.Windows.Forms.TabPage();
            this.txtInputAssetServer = new OpenSim.GUI.InputTextBoxControl();
            this.label3 = new System.Windows.Forms.Label();
            this.txtAssetServer = new System.Windows.Forms.TextBox();
            this.tabGridServer = new System.Windows.Forms.TabPage();
            this.txtInputGridServer = new OpenSim.GUI.InputTextBoxControl();
            this.label4 = new System.Windows.Forms.Label();
            this.txtGridServer = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.noProbe1 = new System.Windows.Forms.CheckBox();
            this.label6 = new System.Windows.Forms.Label();
            this.multiple1 = new System.Windows.Forms.CheckBox();
            this.label7 = new System.Windows.Forms.Label();
            this.noMultiple1 = new System.Windows.Forms.CheckBox();
            this.ignorepixeldepth1 = new System.Windows.Forms.CheckBox();
            this.nothread1 = new System.Windows.Forms.CheckBox();
            this.safe1 = new System.Windows.Forms.CheckBox();
            this.noconsole1 = new System.Windows.Forms.CheckBox();
            this.log1 = new System.Windows.Forms.CheckBox();
            this.helperuri1 = new System.Windows.Forms.CheckBox();
            this.autologin1 = new System.Windows.Forms.CheckBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.dialog1 = new System.Windows.Forms.CheckBox();
            this.previous1 = new System.Windows.Forms.CheckBox();
            this.simple1 = new System.Windows.Forms.CheckBox();
            this.noinvlib1 = new System.Windows.Forms.CheckBox();
            this.debugst1 = new System.Windows.Forms.CheckBox();
            this.spanish1 = new System.Windows.Forms.CheckBox();
            this.korean1 = new System.Windows.Forms.CheckBox();
            this.local1 = new System.Windows.Forms.CheckBox();
            this.purge1 = new System.Windows.Forms.CheckBox();
            this.nofmod1 = new System.Windows.Forms.CheckBox();
            this.noaudio1 = new System.Windows.Forms.CheckBox();
            this.nosound1 = new System.Windows.Forms.CheckBox();
            this.url1 = new System.Windows.Forms.CheckBox();
            this.port1 = new System.Windows.Forms.CheckBox();
            this.simBox1 = new System.Windows.Forms.TextBox();
            this.portBox1 = new System.Windows.Forms.TextBox();
            this.user1 = new System.Windows.Forms.CheckBox();
            this.quitAfter1 = new System.Windows.Forms.CheckBox();
            this.techTag1 = new System.Windows.Forms.CheckBox();
            this.yield1 = new System.Windows.Forms.CheckBox();
            this.logfile1 = new System.Windows.Forms.CheckBox();
            this.settings1 = new System.Windows.Forms.CheckBox();
            this.outbw1 = new System.Windows.Forms.CheckBox();
            this.inbw1 = new System.Windows.Forms.CheckBox();
            this.drop1 = new System.Windows.Forms.CheckBox();
            this.dropBox1 = new System.Windows.Forms.TextBox();
            this.inbwBox1 = new System.Windows.Forms.TextBox();
            this.outbwBox1 = new System.Windows.Forms.TextBox();
            this.settingsBox1 = new System.Windows.Forms.TextBox();
            this.logfileBox1 = new System.Windows.Forms.TextBox();
            this.yieldBox1 = new System.Windows.Forms.TextBox();
            this.techtagBox1 = new System.Windows.Forms.TextBox();
            this.quitafterBox1 = new System.Windows.Forms.TextBox();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.loginuri1 = new System.Windows.Forms.CheckBox();
            this.loginuriBox1 = new System.Windows.Forms.TextBox();
            this.set1 = new System.Windows.Forms.CheckBox();
            this.setBox1 = new System.Windows.Forms.TextBox();
            this.errmask1 = new System.Windows.Forms.CheckBox();
            this.skin1 = new System.Windows.Forms.CheckBox();
            this.login1 = new System.Windows.Forms.CheckBox();
            this.errmaskBox1 = new System.Windows.Forms.TextBox();
            this.skinBox1 = new System.Windows.Forms.TextBox();
            this.firstBox1 = new System.Windows.Forms.TextBox();
            this.lastBox1 = new System.Windows.Forms.TextBox();
            this.noutc1 = new System.Windows.Forms.CheckBox();
            this.passBox1 = new System.Windows.Forms.TextBox();
            this.raw1 = new System.Windows.Forms.CheckBox();
            this.rawBox1 = new System.Windows.Forms.TextBox();
            this.clear1 = new System.Windows.Forms.Button();
            this.nataddress1 = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.exeBox1 = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.label13 = new System.Windows.Forms.Label();
            this.menuStrip1.SuspendLayout();
            this.gbLog.SuspendLayout();
            this.tabLogs.SuspendLayout();
            this.tabMainLog.SuspendLayout();
            this.tabRegionServer.SuspendLayout();
            this.tabUserServer.SuspendLayout();
            this.tabAssetServer.SuspendLayout();
            this.tabGridServer.SuspendLayout();
            this.SuspendLayout();
            //
            // menuStrip1
            //
            this.menuStrip1.AutoSize = false;
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.fileToolStripMenuItem});
            this.menuStrip1.Location = new System.Drawing.Point(0, 0);
            this.menuStrip1.Name = "menuStrip1";
            this.menuStrip1.Size = new System.Drawing.Size(900, 20);
            this.menuStrip1.TabIndex = 7;
            this.menuStrip1.Text = "menuStrip1";
            //
            // fileToolStripMenuItem
            //
            this.fileToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exitToolStripMenuItem});
            this.fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            this.fileToolStripMenuItem.Size = new System.Drawing.Size(35, 16);
            this.fileToolStripMenuItem.Text = "File";
            //
            // exitToolStripMenuItem
            //
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(130, 22);
            this.exitToolStripMenuItem.Text = "Exit Cleanly";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            //
            // timer1
            //
            this.timer1.Enabled = true;
            //
            // clientBox1
            //
            this.clientBox1.Location = new System.Drawing.Point(680, 27);
            this.clientBox1.Name = "clientBox1";
            this.clientBox1.Size = new System.Drawing.Size(213, 20);
            this.clientBox1.TabIndex = 8;
            this.clientBox1.Text = "C://Secondlife//";
            //
            // btnStart
            //
            this.btnStart.Location = new System.Drawing.Point(7, 366);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(80, 23);
            this.btnStart.TabIndex = 2;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            //
            // btnStop
            //
            this.btnStop.Location = new System.Drawing.Point(92, 366);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(80, 23);
            this.btnStop.TabIndex = 3;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            //
            // rbGridRegionMode
            //
            this.rbGridRegionMode.AutoSize = true;
            this.rbGridRegionMode.Location = new System.Drawing.Point(96, 27);
            this.rbGridRegionMode.Name = "rbGridRegionMode";
            this.rbGridRegionMode.Size = new System.Drawing.Size(76, 17);
            this.rbGridRegionMode.TabIndex = 4;
            this.rbGridRegionMode.Text = "Grid region";
            this.rbGridRegionMode.UseVisualStyleBackColor = true;
            this.rbGridRegionMode.CheckedChanged += new System.EventHandler(this.rbGridRegionMode_CheckedChanged);
            //
            // rbStandAloneMode
            //
            this.rbStandAloneMode.AutoSize = true;
            this.rbStandAloneMode.Checked = true;
            this.rbStandAloneMode.Location = new System.Drawing.Point(8, 27);
            this.rbStandAloneMode.Name = "rbStandAloneMode";
            this.rbStandAloneMode.Size = new System.Drawing.Size(82, 17);
            this.rbStandAloneMode.TabIndex = 5;
            this.rbStandAloneMode.TabStop = true;
            this.rbStandAloneMode.Text = "Stand alone";
            this.rbStandAloneMode.UseVisualStyleBackColor = true;
            this.rbStandAloneMode.CheckedChanged += new System.EventHandler(this.rbStandAloneMode_CheckedChanged);
            //
            // rbGridServer
            //
            this.rbGridServer.AutoSize = true;
            this.rbGridServer.Location = new System.Drawing.Point(178, 27);
            this.rbGridServer.Name = "rbGridServer";
            this.rbGridServer.Size = new System.Drawing.Size(76, 17);
            this.rbGridServer.TabIndex = 6;
            this.rbGridServer.Text = "Grid server";
            this.rbGridServer.UseVisualStyleBackColor = true;
            this.rbGridServer.CheckedChanged += new System.EventHandler(this.rbGridServer_CheckedChanged);
            //
            // Launch1
            //
            this.Launch1.Location = new System.Drawing.Point(264, 366);
            this.Launch1.Name = "Launch1";
            this.Launch1.Size = new System.Drawing.Size(80, 23);
            this.Launch1.TabIndex = 9;
            this.Launch1.Text = "Client Launch";
            this.Launch1.UseVisualStyleBackColor = true;
            this.Launch1.Click += new System.EventHandler(this.Launch1_Click);
            //
            // gbLog
            //
            this.gbLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.gbLog.Controls.Add(this.tabLogs);
            this.gbLog.Location = new System.Drawing.Point(8, 50);
            this.gbLog.Name = "gbLog";
            this.gbLog.Size = new System.Drawing.Size(345, 310);
            this.gbLog.TabIndex = 1;
            this.gbLog.TabStop = false;
            this.gbLog.Text = "Logs";
            //
            // tabLogs
            //
            this.tabLogs.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tabLogs.Controls.Add(this.tabMainLog);
            this.tabLogs.Controls.Add(this.tabRegionServer);
            this.tabLogs.Controls.Add(this.tabUserServer);
            this.tabLogs.Controls.Add(this.tabAssetServer);
            this.tabLogs.Controls.Add(this.tabGridServer);
            this.tabLogs.Location = new System.Drawing.Point(6, 19);
            this.tabLogs.Name = "tabLogs";
            this.tabLogs.SelectedIndex = 0;
            this.tabLogs.Size = new System.Drawing.Size(333, 285);
            this.tabLogs.TabIndex = 0;
            //
            // tabMainLog
            //
            this.tabMainLog.Controls.Add(this.txtMainLog);
            this.tabMainLog.Location = new System.Drawing.Point(4, 22);
            this.tabMainLog.Name = "tabMainLog";
            this.tabMainLog.Size = new System.Drawing.Size(325, 259);
            this.tabMainLog.TabIndex = 4;
            this.tabMainLog.Text = "Main log";
            this.tabMainLog.UseVisualStyleBackColor = true;
            //
            // txtMainLog
            //
            this.txtMainLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtMainLog.Location = new System.Drawing.Point(0, 0);
            this.txtMainLog.Multiline = true;
            this.txtMainLog.Name = "txtMainLog";
            this.txtMainLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtMainLog.Size = new System.Drawing.Size(325, 259);
            this.txtMainLog.TabIndex = 1;
            //
            // tabRegionServer
            //
            this.tabRegionServer.Controls.Add(this.txtInputRegionServer);
            this.tabRegionServer.Controls.Add(this.label1);
            this.tabRegionServer.Controls.Add(this.txtOpenSim);
            this.tabRegionServer.Location = new System.Drawing.Point(4, 22);
            this.tabRegionServer.Name = "tabRegionServer";
            this.tabRegionServer.Padding = new System.Windows.Forms.Padding(3);
            this.tabRegionServer.Size = new System.Drawing.Size(325, 259);
            this.tabRegionServer.TabIndex = 0;
            this.tabRegionServer.Text = "Region server";
            this.tabRegionServer.UseVisualStyleBackColor = true;
            //
            // txtInputRegionServer
            //
            this.txtInputRegionServer.Location = new System.Drawing.Point(53, 239);
            this.txtInputRegionServer.Name = "txtInputRegionServer";
            this.txtInputRegionServer.Size = new System.Drawing.Size(272, 20);
            this.txtInputRegionServer.TabIndex = 5;
            //
            // label1
            //
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(0, 242);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(57, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "Command:";
            //
            // txtOpenSim
            //
            this.txtOpenSim.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtOpenSim.Location = new System.Drawing.Point(0, 0);
            this.txtOpenSim.Multiline = true;
            this.txtOpenSim.Name = "txtOpenSim";
            this.txtOpenSim.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtOpenSim.Size = new System.Drawing.Size(325, 236);
            this.txtOpenSim.TabIndex = 0;
            //
            // tabUserServer
            //
            this.tabUserServer.Controls.Add(this.txtInputUserServer);
            this.tabUserServer.Controls.Add(this.label2);
            this.tabUserServer.Controls.Add(this.txtUserServer);
            this.tabUserServer.Location = new System.Drawing.Point(4, 22);
            this.tabUserServer.Name = "tabUserServer";
            this.tabUserServer.Padding = new System.Windows.Forms.Padding(3);
            this.tabUserServer.Size = new System.Drawing.Size(325, 259);
            this.tabUserServer.TabIndex = 1;
            this.tabUserServer.Text = "User server";
            this.tabUserServer.UseVisualStyleBackColor = true;
            //
            // txtInputUserServer
            //
            this.txtInputUserServer.Location = new System.Drawing.Point(53, 239);
            this.txtInputUserServer.Name = "txtInputUserServer";
            this.txtInputUserServer.Size = new System.Drawing.Size(272, 20);
            this.txtInputUserServer.TabIndex = 7;
            //
            // label2
            //
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(0, 242);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(57, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "Command:";
            //
            // txtUserServer
            //
            this.txtUserServer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtUserServer.Location = new System.Drawing.Point(0, 0);
            this.txtUserServer.Multiline = true;
            this.txtUserServer.Name = "txtUserServer";
            this.txtUserServer.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtUserServer.Size = new System.Drawing.Size(325, 236);
            this.txtUserServer.TabIndex = 1;
            //
            // tabAssetServer
            //
            this.tabAssetServer.Controls.Add(this.txtInputAssetServer);
            this.tabAssetServer.Controls.Add(this.label3);
            this.tabAssetServer.Controls.Add(this.txtAssetServer);
            this.tabAssetServer.Location = new System.Drawing.Point(4, 22);
            this.tabAssetServer.Name = "tabAssetServer";
            this.tabAssetServer.Size = new System.Drawing.Size(325, 259);
            this.tabAssetServer.TabIndex = 2;
            this.tabAssetServer.Text = "Asset server";
            this.tabAssetServer.UseVisualStyleBackColor = true;
            //
            // txtInputAssetServer
            //
            this.txtInputAssetServer.Location = new System.Drawing.Point(53, 239);
            this.txtInputAssetServer.Name = "txtInputAssetServer";
            this.txtInputAssetServer.Size = new System.Drawing.Size(272, 20);
            this.txtInputAssetServer.TabIndex = 7;
            //
            // label3
            //
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(0, 242);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(57, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Command:";
            //
            // txtAssetServer
            //
            this.txtAssetServer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtAssetServer.Location = new System.Drawing.Point(0, 0);
            this.txtAssetServer.Multiline = true;
            this.txtAssetServer.Name = "txtAssetServer";
            this.txtAssetServer.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtAssetServer.Size = new System.Drawing.Size(325, 236);
            this.txtAssetServer.TabIndex = 1;
            //
            // tabGridServer
            //
            this.tabGridServer.Controls.Add(this.txtInputGridServer);
            this.tabGridServer.Controls.Add(this.label4);
            this.tabGridServer.Controls.Add(this.txtGridServer);
            this.tabGridServer.Location = new System.Drawing.Point(4, 22);
            this.tabGridServer.Name = "tabGridServer";
            this.tabGridServer.Size = new System.Drawing.Size(325, 259);
            this.tabGridServer.TabIndex = 3;
            this.tabGridServer.Text = "Grid server";
            this.tabGridServer.UseVisualStyleBackColor = true;
            //
            // txtInputGridServer
            //
            this.txtInputGridServer.Location = new System.Drawing.Point(53, 239);
            this.txtInputGridServer.Name = "txtInputGridServer";
            this.txtInputGridServer.Size = new System.Drawing.Size(272, 20);
            this.txtInputGridServer.TabIndex = 7;
            //
            // label4
            //
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(0, 242);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(57, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "Command:";
            //
            // txtGridServer
            //
            this.txtGridServer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtGridServer.Location = new System.Drawing.Point(0, 0);
            this.txtGridServer.Multiline = true;
            this.txtGridServer.Name = "txtGridServer";
            this.txtGridServer.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtGridServer.Size = new System.Drawing.Size(325, 236);
            this.txtGridServer.TabIndex = 1;
            //
            // label5
            //
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(460, 55);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(205, 20);
            this.label5.TabIndex = 11;
            this.label5.Text = "Command Line Switches";
            //
            // noProbe1
            //
            this.noProbe1.AutoSize = true;
            this.noProbe1.Location = new System.Drawing.Point(359, 275);
            this.noProbe1.Name = "noProbe1";
            this.noProbe1.Size = new System.Drawing.Size(68, 17);
            this.noProbe1.TabIndex = 12;
            this.noProbe1.Text = "-noprobe";
            this.toolTip1.SetToolTip(this.noProbe1, "disable hardware probe");
            this.noProbe1.UseVisualStyleBackColor = true;
            //
            // label6
            //
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(8, 415);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(0, 13);
            this.label6.TabIndex = 14;
            this.label6.Click += new System.EventHandler(this.label6_Click);
            //
            // multiple1
            //
            this.multiple1.AutoSize = true;
            this.multiple1.Location = new System.Drawing.Point(359, 185);
            this.multiple1.Name = "multiple1";
            this.multiple1.Size = new System.Drawing.Size(64, 17);
            this.multiple1.TabIndex = 15;
            this.multiple1.Text = "-multiple";
            this.toolTip1.SetToolTip(this.multiple1, "allow multiple viewers");
            this.multiple1.UseVisualStyleBackColor = true;
            //
            // label7
            //
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(8, 396);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(338, 13);
            this.label7.TabIndex = 16;
            this.label7.Text = "Client Command Line String Used and Program Messages :";
            //
            // noMultiple1
            //
            this.noMultiple1.AutoSize = true;
            this.noMultiple1.Location = new System.Drawing.Point(359, 260);
            this.noMultiple1.Name = "noMultiple1";
            this.noMultiple1.Size = new System.Drawing.Size(76, 17);
            this.noMultiple1.TabIndex = 17;
            this.noMultiple1.Text = "-nomultiple";
            this.toolTip1.SetToolTip(this.noMultiple1, "block multiple viewers (secondlife.exe instances)");
            this.noMultiple1.UseVisualStyleBackColor = true;
            //
            // ignorepixeldepth1
            //
            this.ignorepixeldepth1.AutoSize = true;
            this.ignorepixeldepth1.Location = new System.Drawing.Point(359, 125);
            this.ignorepixeldepth1.Name = "ignorepixeldepth1";
            this.ignorepixeldepth1.Size = new System.Drawing.Size(106, 17);
            this.ignorepixeldepth1.TabIndex = 18;
            this.ignorepixeldepth1.Text = "-ignorepixeldepth";
            this.toolTip1.SetToolTip(this.ignorepixeldepth1, "ignore pixel depth settings");
            this.ignorepixeldepth1.UseVisualStyleBackColor = true;
            //
            // nothread1
            //
            this.nothread1.AutoSize = true;
            this.nothread1.Location = new System.Drawing.Point(359, 305);
            this.nothread1.Name = "nothread1";
            this.nothread1.Size = new System.Drawing.Size(71, 17);
            this.nothread1.TabIndex = 19;
            this.nothread1.Text = "-nothread";
            this.toolTip1.SetToolTip(this.nothread1, "run VFS (Virtual File System) in single thread");
            this.nothread1.UseVisualStyleBackColor = true;
            //
            // safe1
            //
            this.safe1.AutoSize = true;
            this.safe1.Location = new System.Drawing.Point(359, 365);
            this.safe1.Name = "safe1";
            this.safe1.Size = new System.Drawing.Size(49, 17);
            this.safe1.TabIndex = 20;
            this.safe1.Text = "-safe";
            this.toolTip1.SetToolTip(this.safe1, "reset preferences, run in safe mode");
            this.safe1.UseVisualStyleBackColor = true;
            //
            // noconsole1
            //
            this.noconsole1.AutoSize = true;
            this.noconsole1.Location = new System.Drawing.Point(359, 215);
            this.noconsole1.Name = "noconsole1";
            this.noconsole1.Size = new System.Drawing.Size(78, 17);
            this.noconsole1.TabIndex = 21;
            this.noconsole1.Text = "-noconsole";
            this.toolTip1.SetToolTip(this.noconsole1, "hide the console if not already hidden");
            this.noconsole1.UseVisualStyleBackColor = true;
            //
            // log1
            //
            this.log1.AutoSize = true;
            this.log1.Location = new System.Drawing.Point(359, 170);
            this.log1.Name = "log1";
            this.log1.Size = new System.Drawing.Size(43, 17);
            this.log1.TabIndex = 22;
            this.log1.Text = "-log";
            this.toolTip1.SetToolTip(this.log1, "--no info avail--");
            this.log1.UseVisualStyleBackColor = true;
            //
            // helperuri1
            //
            this.helperuri1.AutoSize = true;
            this.helperuri1.Location = new System.Drawing.Point(359, 110);
            this.helperuri1.Name = "helperuri1";
            this.helperuri1.Size = new System.Drawing.Size(69, 17);
            this.helperuri1.TabIndex = 23;
            this.helperuri1.Text = "-helperuri";
            this.toolTip1.SetToolTip(this.helperuri1, "--no info avail--");
            this.helperuri1.UseVisualStyleBackColor = true;
            //
            // autologin1
            //
            this.autologin1.AutoSize = true;
            this.autologin1.Location = new System.Drawing.Point(359, 65);
            this.autologin1.Name = "autologin1";
            this.autologin1.Size = new System.Drawing.Size(75, 17);
            this.autologin1.TabIndex = 24;
            this.autologin1.Text = "--autologin";
            this.toolTip1.SetToolTip(this.autologin1, "--no info avail--");
            this.autologin1.UseVisualStyleBackColor = true;
            //
            // dialog1
            //
            this.dialog1.AutoSize = true;
            this.dialog1.Location = new System.Drawing.Point(359, 95);
            this.dialog1.Name = "dialog1";
            this.dialog1.Size = new System.Drawing.Size(57, 17);
            this.dialog1.TabIndex = 25;
            this.dialog1.Text = "-dialog";
            this.toolTip1.SetToolTip(this.dialog1, "some arcane dialog box that is impossible to raise");
            this.dialog1.UseVisualStyleBackColor = true;
            //
            // previous1
            //
            this.previous1.AutoSize = true;
            this.previous1.Location = new System.Drawing.Point(359, 335);
            this.previous1.Name = "previous1";
            this.previous1.Size = new System.Drawing.Size(69, 17);
            this.previous1.TabIndex = 26;
            this.previous1.Text = "-previous";
            this.toolTip1.SetToolTip(this.previous1, "--no info avail--");
            this.previous1.UseVisualStyleBackColor = true;
            //
            // simple1
            //
            this.simple1.AutoSize = true;
            this.simple1.Location = new System.Drawing.Point(359, 380);
            this.simple1.Name = "simple1";
            this.simple1.Size = new System.Drawing.Size(58, 17);
            this.simple1.TabIndex = 27;
            this.simple1.Text = "-simple";
            this.toolTip1.SetToolTip(this.simple1, "--no info avail--");
            this.simple1.UseVisualStyleBackColor = true;
            //
            // noinvlib1
            //
            this.noinvlib1.AutoSize = true;
            this.noinvlib1.Location = new System.Drawing.Point(359, 245);
            this.noinvlib1.Name = "noinvlib1";
            this.noinvlib1.Size = new System.Drawing.Size(65, 17);
            this.noinvlib1.TabIndex = 28;
            this.noinvlib1.Text = "-noinvlib";
            this.toolTip1.SetToolTip(this.noinvlib1, "do not request inventory library");
            this.noinvlib1.UseVisualStyleBackColor = true;
            //
            // debugst1
            //
            this.debugst1.AutoSize = true;
            this.debugst1.Location = new System.Drawing.Point(359, 80);
            this.debugst1.Name = "debugst1";
            this.debugst1.Size = new System.Drawing.Size(67, 17);
            this.debugst1.TabIndex = 30;
            this.debugst1.Text = "-debugst";
            this.toolTip1.SetToolTip(this.debugst1, "debug mask");
            this.debugst1.UseVisualStyleBackColor = true;
            //
            // spanish1
            //
            this.spanish1.AutoSize = true;
            this.spanish1.Location = new System.Drawing.Point(359, 395);
            this.spanish1.Name = "spanish1";
            this.spanish1.Size = new System.Drawing.Size(65, 17);
            this.spanish1.TabIndex = 31;
            this.spanish1.Text = "-spanish";
            this.toolTip1.SetToolTip(this.spanish1, "activate (incomplete) Spanish UI translation");
            this.spanish1.UseVisualStyleBackColor = true;
            //
            // korean1
            //
            this.korean1.AutoSize = true;
            this.korean1.Location = new System.Drawing.Point(359, 140);
            this.korean1.Name = "korean1";
            this.korean1.Size = new System.Drawing.Size(62, 17);
            this.korean1.TabIndex = 32;
            this.korean1.Text = "-korean";
            this.toolTip1.SetToolTip(this.korean1, "activate (incomplete) Korean UI translation");
            this.korean1.UseVisualStyleBackColor = true;
            //
            // local1
            //
            this.local1.AutoSize = true;
            this.local1.Location = new System.Drawing.Point(359, 155);
            this.local1.Name = "local1";
            this.local1.Size = new System.Drawing.Size(51, 17);
            this.local1.TabIndex = 46;
            this.local1.Text = "-local";
            this.toolTip1.SetToolTip(this.local1, "run without simulator");
            this.local1.UseVisualStyleBackColor = true;
            //
            // purge1
            //
            this.purge1.AutoSize = true;
            this.purge1.Location = new System.Drawing.Point(359, 350);
            this.purge1.Name = "purge1";
            this.purge1.Size = new System.Drawing.Size(56, 17);
            this.purge1.TabIndex = 56;
            this.purge1.Text = "-purge";
            this.toolTip1.SetToolTip(this.purge1, "delete files in cache");
            this.purge1.UseVisualStyleBackColor = true;
            //
            // nofmod1
            //
            this.nofmod1.AutoSize = true;
            this.nofmod1.Location = new System.Drawing.Point(359, 230);
            this.nofmod1.Name = "nofmod1";
            this.nofmod1.Size = new System.Drawing.Size(64, 17);
            this.nofmod1.TabIndex = 45;
            this.nofmod1.Text = "-nofmod";
            this.toolTip1.SetToolTip(this.nofmod1, "FMOD is the API used to distort sound while moving");
            this.nofmod1.UseVisualStyleBackColor = true;
            //
            // noaudio1
            //
            this.noaudio1.AutoSize = true;
            this.noaudio1.Location = new System.Drawing.Point(359, 200);
            this.noaudio1.Name = "noaudio1";
            this.noaudio1.Size = new System.Drawing.Size(67, 17);
            this.noaudio1.TabIndex = 44;
            this.noaudio1.Text = "-noaudio";
            this.toolTip1.SetToolTip(this.noaudio1, "no audio, different from -nosound?");
            this.noaudio1.UseVisualStyleBackColor = true;
            //
            // nosound1
            //
            this.nosound1.AutoSize = true;
            this.nosound1.Location = new System.Drawing.Point(359, 290);
            this.nosound1.Name = "nosound1";
            this.nosound1.Size = new System.Drawing.Size(70, 17);
            this.nosound1.TabIndex = 55;
            this.nosound1.Text = "-nosound";
            this.toolTip1.SetToolTip(this.nosound1, "no sound, different from -noaudio?");
            this.nosound1.UseVisualStyleBackColor = true;
            //
            // url1
            //
            this.url1.AutoSize = true;
            this.url1.Location = new System.Drawing.Point(488, 245);
            this.url1.Name = "url1";
            this.url1.Size = new System.Drawing.Size(40, 17);
            this.url1.TabIndex = 43;
            this.url1.Text = "-url";
            this.toolTip1.SetToolTip(this.url1, "handles secondlife://sim/x/y/z URLs");
            this.url1.UseVisualStyleBackColor = true;
            //
            // port1
            //
            this.port1.AutoSize = true;
            this.port1.Location = new System.Drawing.Point(488, 171);
            this.port1.Name = "port1";
            this.port1.Size = new System.Drawing.Size(47, 17);
            this.port1.TabIndex = 49;
            this.port1.Text = "-port";
            this.toolTip1.SetToolTip(this.port1, "Set the TCP port for the client; useful to run multiple instances of SL on the sa" +
                    "me local home network. Values that may work: 13000 and 13001 (Valid numbers are " +
                    "13000 to 13050)");
            this.port1.UseVisualStyleBackColor = true;
            //
            // simBox1
            //
            this.simBox1.Location = new System.Drawing.Point(549, 243);
            this.simBox1.Name = "simBox1";
            this.simBox1.Size = new System.Drawing.Size(344, 20);
            this.simBox1.TabIndex = 66;
            this.simBox1.Text = "secondlife://lutra/127/128/60";
            this.toolTip1.SetToolTip(this.simBox1, "type URL here");
            //
            // portBox1
            //
            this.portBox1.Location = new System.Drawing.Point(549, 169);
            this.portBox1.Name = "portBox1";
            this.portBox1.Size = new System.Drawing.Size(58, 20);
            this.portBox1.TabIndex = 67;
            this.portBox1.Text = "13000";
            this.toolTip1.SetToolTip(this.portBox1, "enter port number here");
            //
            // user1
            //
            this.user1.AutoSize = true;
            this.user1.Location = new System.Drawing.Point(488, 191);
            this.user1.Name = "user1";
            this.user1.Size = new System.Drawing.Size(49, 17);
            this.user1.TabIndex = 42;
            this.user1.Text = "-user";
            this.user1.ThreeState = true;
            this.toolTip1.SetToolTip(this.user1, "specify user server in dotted quad");
            this.user1.UseVisualStyleBackColor = true;
            //
            // quitAfter1
            //
            this.quitAfter1.AutoSize = true;
            this.quitAfter1.Location = new System.Drawing.Point(680, 65);
            this.quitAfter1.Name = "quitAfter1";
            this.quitAfter1.Size = new System.Drawing.Size(67, 17);
            this.quitAfter1.TabIndex = 41;
            this.quitAfter1.Text = "-quitafter";
            this.toolTip1.SetToolTip(this.quitAfter1, "SL quits after elapsed time in seconds");
            this.quitAfter1.UseVisualStyleBackColor = true;
            //
            // techTag1
            //
            this.techTag1.AutoSize = true;
            this.techTag1.Location = new System.Drawing.Point(488, 211);
            this.techTag1.Name = "techTag1";
            this.techTag1.Size = new System.Drawing.Size(65, 17);
            this.techTag1.TabIndex = 47;
            this.techTag1.Text = "-techtag";
            this.toolTip1.SetToolTip(this.techTag1, "unknown (but requires a parameter)");
            this.techTag1.UseVisualStyleBackColor = true;
            //
            // yield1
            //
            this.yield1.AutoSize = true;
            this.yield1.Location = new System.Drawing.Point(488, 91);
            this.yield1.Name = "yield1";
            this.yield1.Size = new System.Drawing.Size(50, 17);
            this.yield1.TabIndex = 48;
            this.yield1.Text = "-yield";
            this.toolTip1.SetToolTip(this.yield1, "yield some idle time to local host (changed from - cooperative)");
            this.yield1.UseVisualStyleBackColor = true;
            //
            // logfile1
            //
            this.logfile1.AutoSize = true;
            this.logfile1.Location = new System.Drawing.Point(680, 125);
            this.logfile1.Name = "logfile1";
            this.logfile1.Size = new System.Drawing.Size(56, 17);
            this.logfile1.TabIndex = 54;
            this.logfile1.Text = "-logfile";
            this.toolTip1.SetToolTip(this.logfile1, "change the log filename");
            this.logfile1.UseVisualStyleBackColor = true;
            //
            // settings1
            //
            this.settings1.AutoSize = true;
            this.settings1.Location = new System.Drawing.Point(680, 95);
            this.settings1.Name = "settings1";
            this.settings1.Size = new System.Drawing.Size(65, 17);
            this.settings1.TabIndex = 53;
            this.settings1.Text = "-settings";
            this.toolTip1.SetToolTip(this.settings1, "specify configuration filename; default is \"settings.ini\"");
            this.settings1.UseVisualStyleBackColor = true;
            //
            // outbw1
            //
            this.outbw1.AutoSize = true;
            this.outbw1.Location = new System.Drawing.Point(488, 111);
            this.outbw1.Name = "outbw1";
            this.outbw1.Size = new System.Drawing.Size(58, 17);
            this.outbw1.TabIndex = 52;
            this.outbw1.Text = "-outbw";
            this.toolTip1.SetToolTip(this.outbw1, "set outgoing bandwidth");
            this.outbw1.UseVisualStyleBackColor = true;
            //
            // inbw1
            //
            this.inbw1.AutoSize = true;
            this.inbw1.Location = new System.Drawing.Point(488, 131);
            this.inbw1.Name = "inbw1";
            this.inbw1.Size = new System.Drawing.Size(51, 17);
            this.inbw1.TabIndex = 51;
            this.inbw1.Text = "-inbw";
            this.toolTip1.SetToolTip(this.inbw1, "set incoming bandwidth");
            this.inbw1.UseVisualStyleBackColor = true;
            //
            // drop1
            //
            this.drop1.AutoSize = true;
            this.drop1.Location = new System.Drawing.Point(488, 151);
            this.drop1.Name = "drop1";
            this.drop1.Size = new System.Drawing.Size(50, 17);
            this.drop1.TabIndex = 50;
            this.drop1.Text = "-drop";
            this.toolTip1.SetToolTip(this.drop1, "drop number% of incoming network packets");
            this.drop1.UseVisualStyleBackColor = true;
            //
            // dropBox1
            //
            this.dropBox1.Location = new System.Drawing.Point(549, 149);
            this.dropBox1.Name = "dropBox1";
            this.dropBox1.Size = new System.Drawing.Size(58, 20);
            this.dropBox1.TabIndex = 68;
            this.dropBox1.Text = "0";
            this.toolTip1.SetToolTip(this.dropBox1, "enter percent of packets to drop");
            //
            // inbwBox1
            //
            this.inbwBox1.Location = new System.Drawing.Point(549, 129);
            this.inbwBox1.Name = "inbwBox1";
            this.inbwBox1.Size = new System.Drawing.Size(57, 20);
            this.inbwBox1.TabIndex = 69;
            this.toolTip1.SetToolTip(this.inbwBox1, "enter incoming cap");
            //
            // outbwBox1
            //
            this.outbwBox1.Location = new System.Drawing.Point(549, 109);
            this.outbwBox1.Name = "outbwBox1";
            this.outbwBox1.Size = new System.Drawing.Size(58, 20);
            this.outbwBox1.TabIndex = 70;
            this.toolTip1.SetToolTip(this.outbwBox1, "enter outgoing cap");
            //
            // settingsBox1
            //
            this.settingsBox1.Location = new System.Drawing.Point(741, 93);
            this.settingsBox1.Name = "settingsBox1";
            this.settingsBox1.Size = new System.Drawing.Size(152, 20);
            this.settingsBox1.TabIndex = 71;
            this.settingsBox1.Text = "settings.ini";
            this.toolTip1.SetToolTip(this.settingsBox1, "enter settings file name");
            //
            // logfileBox1
            //
            this.logfileBox1.Location = new System.Drawing.Point(733, 123);
            this.logfileBox1.Name = "logfileBox1";
            this.logfileBox1.Size = new System.Drawing.Size(160, 20);
            this.logfileBox1.TabIndex = 72;
            this.logfileBox1.Text = "mylogfile.txt";
            this.toolTip1.SetToolTip(this.logfileBox1, "enter log file name here");
            //
            // yieldBox1
            //
            this.yieldBox1.Location = new System.Drawing.Point(549, 89);
            this.yieldBox1.Name = "yieldBox1";
            this.yieldBox1.Size = new System.Drawing.Size(58, 20);
            this.yieldBox1.TabIndex = 73;
            this.toolTip1.SetToolTip(this.yieldBox1, "enter time to yield in <ms>");
            //
            // techtagBox1
            //
            this.techtagBox1.Location = new System.Drawing.Point(549, 209);
            this.techtagBox1.Name = "techtagBox1";
            this.techtagBox1.Size = new System.Drawing.Size(58, 20);
            this.techtagBox1.TabIndex = 74;
            this.toolTip1.SetToolTip(this.techtagBox1, "enter unknown param here");
            //
            // quitafterBox1
            //
            this.quitafterBox1.Location = new System.Drawing.Point(745, 63);
            this.quitafterBox1.Name = "quitafterBox1";
            this.quitafterBox1.Size = new System.Drawing.Size(148, 20);
            this.quitafterBox1.TabIndex = 75;
            this.toolTip1.SetToolTip(this.quitafterBox1, "enter time in seconds");
            //
            // comboBox1
            //
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Items.AddRange(new object[] {
            "agni",
            "colo",
            "dmz",
            "durga",
            "siva"});
            this.comboBox1.Location = new System.Drawing.Point(549, 189);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(58, 21);
            this.comboBox1.TabIndex = 76;
            this.comboBox1.Text = "agni";
            this.toolTip1.SetToolTip(this.comboBox1, "select LL user server");
            //
            // loginuri1
            //
            this.loginuri1.AutoSize = true;
            this.loginuri1.Location = new System.Drawing.Point(488, 275);
            this.loginuri1.Name = "loginuri1";
            this.loginuri1.Size = new System.Drawing.Size(62, 17);
            this.loginuri1.TabIndex = 77;
            this.loginuri1.Text = "-loginuri";
            this.toolTip1.SetToolTip(this.loginuri1, "login server and CGI script to use");
            this.loginuri1.UseVisualStyleBackColor = true;
            //
            // loginuriBox1
            //
            this.loginuriBox1.Location = new System.Drawing.Point(549, 273);
            this.loginuriBox1.Name = "loginuriBox1";
            this.loginuriBox1.Size = new System.Drawing.Size(344, 20);
            this.loginuriBox1.TabIndex = 78;
            this.loginuriBox1.Text = "localhost:9000";
            this.toolTip1.SetToolTip(this.loginuriBox1, "enter login url here");
            //
            // set1
            //
            this.set1.AutoSize = true;
            this.set1.Location = new System.Drawing.Point(636, 185);
            this.set1.Name = "set1";
            this.set1.Size = new System.Drawing.Size(43, 17);
            this.set1.TabIndex = 79;
            this.set1.Text = "-set";
            this.toolTip1.SetToolTip(this.set1, "specify value of a particular configuration variable; can be used multiple times " +
                    "in a single command-line");
            this.set1.UseVisualStyleBackColor = true;
            //
            // setBox1
            //
            this.setBox1.Location = new System.Drawing.Point(680, 183);
            this.setBox1.Name = "setBox1";
            this.setBox1.Size = new System.Drawing.Size(213, 20);
            this.setBox1.TabIndex = 80;
            this.setBox1.Text = "SystemLanguage en-us";
            this.toolTip1.SetToolTip(this.setBox1, "enter params");
            //
            // errmask1
            //
            this.errmask1.AutoSize = true;
            this.errmask1.Location = new System.Drawing.Point(636, 154);
            this.errmask1.Name = "errmask1";
            this.errmask1.Size = new System.Drawing.Size(66, 17);
            this.errmask1.TabIndex = 81;
            this.errmask1.Text = "-errmask";
            this.toolTip1.SetToolTip(this.errmask1, "32-bit bitmask for error type mask");
            this.errmask1.UseVisualStyleBackColor = true;
            //
            // skin1
            //
            this.skin1.AutoSize = true;
            this.skin1.Location = new System.Drawing.Point(635, 215);
            this.skin1.Name = "skin1";
            this.skin1.Size = new System.Drawing.Size(48, 17);
            this.skin1.TabIndex = 82;
            this.skin1.Text = "-skin";
            this.toolTip1.SetToolTip(this.skin1, "load skins/<directory>/skin.xml as the default UI appearance (incomplete)");
            this.skin1.UseVisualStyleBackColor = true;
            //
            // login1
            //
            this.login1.AutoSize = true;
            this.login1.Location = new System.Drawing.Point(457, 304);
            this.login1.Name = "login1";
            this.login1.Size = new System.Drawing.Size(51, 17);
            this.login1.TabIndex = 83;
            this.login1.Text = "-login";
            this.toolTip1.SetToolTip(this.login1, "log in as a user");
            this.login1.UseVisualStyleBackColor = true;
            //
            // errmaskBox1
            //
            this.errmaskBox1.Location = new System.Drawing.Point(704, 153);
            this.errmaskBox1.Name = "errmaskBox1";
            this.errmaskBox1.Size = new System.Drawing.Size(189, 20);
            this.errmaskBox1.TabIndex = 84;
            this.toolTip1.SetToolTip(this.errmaskBox1, "32-bit bitmask for error type mask");
            //
            // skinBox1
            //
            this.skinBox1.Location = new System.Drawing.Point(679, 213);
            this.skinBox1.Name = "skinBox1";
            this.skinBox1.Size = new System.Drawing.Size(214, 20);
            this.skinBox1.TabIndex = 85;
            this.skinBox1.Text = "C://Secondlife//";
            this.toolTip1.SetToolTip(this.skinBox1, "enter directory where skin.xml is");
            //
            // firstBox1
            //
            this.firstBox1.Location = new System.Drawing.Point(549, 303);
            this.firstBox1.Name = "firstBox1";
            this.firstBox1.Size = new System.Drawing.Size(80, 20);
            this.firstBox1.TabIndex = 86;
            this.firstBox1.Text = "Test";
            this.toolTip1.SetToolTip(this.firstBox1, "firstname");
            //
            // lastBox1
            //
            this.lastBox1.Location = new System.Drawing.Point(668, 303);
            this.lastBox1.Name = "lastBox1";
            this.lastBox1.Size = new System.Drawing.Size(80, 20);
            this.lastBox1.TabIndex = 92;
            this.lastBox1.Text = "User";
            this.toolTip1.SetToolTip(this.lastBox1, "lastname");
            //
            // noutc1
            //
            this.noutc1.AutoSize = true;
            this.noutc1.Location = new System.Drawing.Point(359, 320);
            this.noutc1.Name = "noutc1";
            this.noutc1.Size = new System.Drawing.Size(56, 17);
            this.noutc1.TabIndex = 29;
            this.noutc1.Text = "-noutc";
            this.toolTip1.SetToolTip(this.noutc1, "logs in local time, not UTC");
            this.noutc1.UseVisualStyleBackColor = true;
            //
            // passBox1
            //
            this.passBox1.Location = new System.Drawing.Point(790, 303);
            this.passBox1.Name = "passBox1";
            this.passBox1.Size = new System.Drawing.Size(103, 20);
            this.passBox1.TabIndex = 93;
            this.passBox1.Text = "test";
            this.toolTip1.SetToolTip(this.passBox1, "password");
            //
            // raw1
            //
            this.raw1.AutoSize = true;
            this.raw1.Location = new System.Drawing.Point(457, 336);
            this.raw1.Name = "raw1";
            this.raw1.Size = new System.Drawing.Size(81, 17);
            this.raw1.TabIndex = 94;
            this.raw1.Text = "Raw CMD :";
            this.toolTip1.SetToolTip(this.raw1, "Raw CMD options, may crash everything");
            this.raw1.UseVisualStyleBackColor = true;
            //
            // rawBox1
            //
            this.rawBox1.Location = new System.Drawing.Point(549, 333);
            this.rawBox1.Name = "rawBox1";
            this.rawBox1.Size = new System.Drawing.Size(344, 20);
            this.rawBox1.TabIndex = 95;
            this.toolTip1.SetToolTip(this.rawBox1, "Raw CMD options, may crash everything");
            //
            // clear1
            //
            this.clear1.Location = new System.Drawing.Point(178, 366);
            this.clear1.Name = "clear1";
            this.clear1.Size = new System.Drawing.Size(80, 23);
            this.clear1.TabIndex = 96;
            this.clear1.Text = "Clear";
            this.toolTip1.SetToolTip(this.clear1, "clear all switch boxes");
            this.clear1.UseVisualStyleBackColor = true;
            this.clear1.Click += new System.EventHandler(this.clear1_Click);
            //
            // nataddress1
            //
            this.nataddress1.Location = new System.Drawing.Point(457, 389);
            this.nataddress1.Name = "nataddress1";
            this.nataddress1.Size = new System.Drawing.Size(436, 20);
            this.nataddress1.TabIndex = 58;
            this.nataddress1.Text = "UNUSED ATM";
            this.nataddress1.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            //
            // label8
            //
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.Location = new System.Drawing.Point(588, 360);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(175, 20);
            this.label8.TabIndex = 59;
            this.label8.Text = "World/NAT Address :";
            //
            // label9
            //
            this.label9.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label9.Location = new System.Drawing.Point(633, 27);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(47, 20);
            this.label9.TabIndex = 60;
            this.label9.Text = "Path :";
            this.label9.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // exeBox1
            //
            this.exeBox1.Location = new System.Drawing.Point(530, 27);
            this.exeBox1.Name = "exeBox1";
            this.exeBox1.Size = new System.Drawing.Size(100, 20);
            this.exeBox1.TabIndex = 61;
            this.exeBox1.Text = "Secondlife.exe";
            //
            // label10
            //
            this.label10.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label10.Location = new System.Drawing.Point(392, 27);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(138, 20);
            this.label10.TabIndex = 62;
            this.label10.Text = "Executable Name :";
            this.label10.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // label11
            //
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(514, 306);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(32, 13);
            this.label11.TabIndex = 89;
            this.label11.Text = "First :";
            //
            // label12
            //
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(632, 306);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(33, 13);
            this.label12.TabIndex = 90;
            this.label12.Text = "Last :";
            //
            // label13
            //
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(751, 306);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(36, 13);
            this.label13.TabIndex = 91;
            this.label13.Text = "Pass :";
            //
            // Main
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 431);
            this.Controls.Add(this.clear1);
            this.Controls.Add(this.rawBox1);
            this.Controls.Add(this.raw1);
            this.Controls.Add(this.passBox1);
            this.Controls.Add(this.lastBox1);
            this.Controls.Add(this.label13);
            this.Controls.Add(this.label12);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.firstBox1);
            this.Controls.Add(this.skinBox1);
            this.Controls.Add(this.errmaskBox1);
            this.Controls.Add(this.login1);
            this.Controls.Add(this.skin1);
            this.Controls.Add(this.errmask1);
            this.Controls.Add(this.setBox1);
            this.Controls.Add(this.set1);
            this.Controls.Add(this.loginuriBox1);
            this.Controls.Add(this.loginuri1);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.quitafterBox1);
            this.Controls.Add(this.techtagBox1);
            this.Controls.Add(this.yieldBox1);
            this.Controls.Add(this.logfileBox1);
            this.Controls.Add(this.settingsBox1);
            this.Controls.Add(this.outbwBox1);
            this.Controls.Add(this.inbwBox1);
            this.Controls.Add(this.dropBox1);
            this.Controls.Add(this.portBox1);
            this.Controls.Add(this.simBox1);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.exeBox1);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.nataddress1);
            this.Controls.Add(this.purge1);
            this.Controls.Add(this.nosound1);
            this.Controls.Add(this.logfile1);
            this.Controls.Add(this.settings1);
            this.Controls.Add(this.outbw1);
            this.Controls.Add(this.inbw1);
            this.Controls.Add(this.drop1);
            this.Controls.Add(this.port1);
            this.Controls.Add(this.yield1);
            this.Controls.Add(this.techTag1);
            this.Controls.Add(this.local1);
            this.Controls.Add(this.nofmod1);
            this.Controls.Add(this.noaudio1);
            this.Controls.Add(this.url1);
            this.Controls.Add(this.user1);
            this.Controls.Add(this.quitAfter1);
            this.Controls.Add(this.korean1);
            this.Controls.Add(this.spanish1);
            this.Controls.Add(this.debugst1);
            this.Controls.Add(this.noutc1);
            this.Controls.Add(this.noinvlib1);
            this.Controls.Add(this.simple1);
            this.Controls.Add(this.previous1);
            this.Controls.Add(this.dialog1);
            this.Controls.Add(this.autologin1);
            this.Controls.Add(this.helperuri1);
            this.Controls.Add(this.log1);
            this.Controls.Add(this.noconsole1);
            this.Controls.Add(this.safe1);
            this.Controls.Add(this.nothread1);
            this.Controls.Add(this.ignorepixeldepth1);
            this.Controls.Add(this.noMultiple1);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.multiple1);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.noProbe1);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.Launch1);
            this.Controls.Add(this.clientBox1);
            this.Controls.Add(this.rbGridServer);
            this.Controls.Add(this.rbStandAloneMode);
            this.Controls.Add(this.rbGridRegionMode);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.gbLog);
            this.Controls.Add(this.menuStrip1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
            this.MainMenuStrip = this.menuStrip1;
            this.MaximizeBox = false;
            this.Name = "Main";
            this.Text = "OpenSim";
            this.toolTip1.SetToolTip(this, "logs in local time, not UTC");
            this.Load += new System.EventHandler(this.Main_Load);
            this.menuStrip1.ResumeLayout(false);
            this.menuStrip1.PerformLayout();
            this.gbLog.ResumeLayout(false);
            this.tabLogs.ResumeLayout(false);
            this.tabMainLog.ResumeLayout(false);
            this.tabMainLog.PerformLayout();
            this.tabRegionServer.ResumeLayout(false);
            this.tabRegionServer.PerformLayout();
            this.tabUserServer.ResumeLayout(false);
            this.tabUserServer.PerformLayout();
            this.tabAssetServer.ResumeLayout(false);
            this.tabAssetServer.PerformLayout();
            this.tabGridServer.ResumeLayout(false);
            this.tabGridServer.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.TextBox clientBox1;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.RadioButton rbGridRegionMode;
        private System.Windows.Forms.RadioButton rbStandAloneMode;
        private System.Windows.Forms.RadioButton rbGridServer;
        private System.Windows.Forms.Button Launch1;
        private System.Windows.Forms.GroupBox gbLog;
        private System.Windows.Forms.TabControl tabLogs;
        private System.Windows.Forms.TabPage tabMainLog;
        private System.Windows.Forms.TabPage tabRegionServer;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtOpenSim;
        private System.Windows.Forms.TabPage tabUserServer;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtUserServer;
        private System.Windows.Forms.TabPage tabAssetServer;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtAssetServer;
        private System.Windows.Forms.TabPage tabGridServer;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtGridServer;
        private System.Windows.Forms.TextBox txtMainLog;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox noProbe1;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.CheckBox multiple1;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.CheckBox noMultiple1;
        private System.Windows.Forms.CheckBox ignorepixeldepth1;
        private System.Windows.Forms.CheckBox nothread1;
        private System.Windows.Forms.CheckBox safe1;
        private System.Windows.Forms.CheckBox noconsole1;
        private System.Windows.Forms.CheckBox log1;
        private System.Windows.Forms.CheckBox helperuri1;
        private System.Windows.Forms.CheckBox autologin1;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.CheckBox dialog1;
        private System.Windows.Forms.CheckBox previous1;
        private System.Windows.Forms.CheckBox simple1;
        private System.Windows.Forms.CheckBox noinvlib1;
        private System.Windows.Forms.CheckBox noutc1;
        private System.Windows.Forms.CheckBox debugst1;
        private System.Windows.Forms.CheckBox spanish1;
        private System.Windows.Forms.CheckBox korean1;
        private System.Windows.Forms.CheckBox local1;
        private System.Windows.Forms.CheckBox nofmod1;
        private System.Windows.Forms.CheckBox noaudio1;
        private System.Windows.Forms.CheckBox url1;
        private System.Windows.Forms.CheckBox user1;
        private System.Windows.Forms.CheckBox quitAfter1;
        private System.Windows.Forms.CheckBox techTag1;
        private System.Windows.Forms.CheckBox yield1;
        private System.Windows.Forms.CheckBox purge1;
        private System.Windows.Forms.CheckBox nosound1;
        private System.Windows.Forms.CheckBox logfile1;
        private System.Windows.Forms.CheckBox settings1;
        private System.Windows.Forms.CheckBox outbw1;
        private System.Windows.Forms.CheckBox inbw1;
        private System.Windows.Forms.CheckBox drop1;
        private System.Windows.Forms.CheckBox port1;
        private System.Windows.Forms.TextBox nataddress1;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox exeBox1;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox simBox1;
        private System.Windows.Forms.TextBox portBox1;
        private System.Windows.Forms.TextBox dropBox1;
        private System.Windows.Forms.TextBox inbwBox1;
        private System.Windows.Forms.TextBox outbwBox1;
        private System.Windows.Forms.TextBox settingsBox1;
        private System.Windows.Forms.TextBox logfileBox1;
        private System.Windows.Forms.TextBox yieldBox1;
        private System.Windows.Forms.TextBox techtagBox1;
        private System.Windows.Forms.TextBox quitafterBox1;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.CheckBox loginuri1;
        private System.Windows.Forms.TextBox loginuriBox1;
        private System.Windows.Forms.CheckBox set1;
        private System.Windows.Forms.TextBox setBox1;
        private System.Windows.Forms.CheckBox errmask1;
        private System.Windows.Forms.CheckBox skin1;
        private System.Windows.Forms.CheckBox login1;
        private System.Windows.Forms.TextBox errmaskBox1;
        private System.Windows.Forms.TextBox skinBox1;
        private System.Windows.Forms.TextBox firstBox1;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.TextBox lastBox1;
        private System.Windows.Forms.TextBox passBox1;
        private InputTextBoxControl txtInputUserServer;
        private InputTextBoxControl txtInputAssetServer;
        private InputTextBoxControl txtInputRegionServer;
        private InputTextBoxControl txtInputGridServer;
        private System.Windows.Forms.CheckBox raw1;
        private System.Windows.Forms.TextBox rawBox1;
        private System.Windows.Forms.Button clear1;
    }
}
