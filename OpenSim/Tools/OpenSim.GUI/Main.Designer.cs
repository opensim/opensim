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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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
            this.tabLogs = new System.Windows.Forms.TabControl();
            this.tabMainLog = new System.Windows.Forms.TabPage();
            this.txtMainLog = new System.Windows.Forms.TextBox();
            this.tabRegionServer = new System.Windows.Forms.TabPage();
            this.label1 = new System.Windows.Forms.Label();
            this.txtInputRegionServer = new OpenSim.GUI.InputTextBoxControl();
            this.txtOpenSim = new System.Windows.Forms.TextBox();
            this.tabUserServer = new System.Windows.Forms.TabPage();
            this.label2 = new System.Windows.Forms.Label();
            this.txtInputUserServer = new OpenSim.GUI.InputTextBoxControl();
            this.txtUserServer = new System.Windows.Forms.TextBox();
            this.tabAssetServer = new System.Windows.Forms.TabPage();
            this.label3 = new System.Windows.Forms.Label();
            this.txtInputAssetServer = new OpenSim.GUI.InputTextBoxControl();
            this.txtAssetServer = new System.Windows.Forms.TextBox();
            this.tabGridServer = new System.Windows.Forms.TabPage();
            this.label4 = new System.Windows.Forms.Label();
            this.txtInputGridServer = new OpenSim.GUI.InputTextBoxControl();
            this.txtGridServer = new System.Windows.Forms.TextBox();
            this.gbLog = new System.Windows.Forms.GroupBox();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.rbGridRegionMode = new System.Windows.Forms.RadioButton();
            this.rbStandAloneMode = new System.Windows.Forms.RadioButton();
            this.rbGridServer = new System.Windows.Forms.RadioButton();
            this.tabLogs.SuspendLayout();
            this.tabMainLog.SuspendLayout();
            this.tabRegionServer.SuspendLayout();
            this.tabUserServer.SuspendLayout();
            this.tabAssetServer.SuspendLayout();
            this.tabGridServer.SuspendLayout();
            this.gbLog.SuspendLayout();
            this.SuspendLayout();
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
            this.tabLogs.Size = new System.Drawing.Size(562, 230);
            this.tabLogs.TabIndex = 0;
            // 
            // tabMainLog
            // 
            this.tabMainLog.Controls.Add(this.txtMainLog);
            this.tabMainLog.Location = new System.Drawing.Point(4, 22);
            this.tabMainLog.Name = "tabMainLog";
            this.tabMainLog.Size = new System.Drawing.Size(554, 204);
            this.tabMainLog.TabIndex = 4;
            this.tabMainLog.Text = "Main log";
            this.tabMainLog.UseVisualStyleBackColor = true;
            // 
            // txtMainLog
            // 
            this.txtMainLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtMainLog.Location = new System.Drawing.Point(6, 5);
            this.txtMainLog.Multiline = true;
            this.txtMainLog.Name = "txtMainLog";
            this.txtMainLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtMainLog.Size = new System.Drawing.Size(542, 195);
            this.txtMainLog.TabIndex = 1;
            // 
            // tabRegionServer
            // 
            this.tabRegionServer.Controls.Add(this.label1);
            this.tabRegionServer.Controls.Add(this.txtInputRegionServer);
            this.tabRegionServer.Controls.Add(this.txtOpenSim);
            this.tabRegionServer.Location = new System.Drawing.Point(4, 22);
            this.tabRegionServer.Name = "tabRegionServer";
            this.tabRegionServer.Padding = new System.Windows.Forms.Padding(3);
            this.tabRegionServer.Size = new System.Drawing.Size(554, 204);
            this.tabRegionServer.TabIndex = 0;
            this.tabRegionServer.Text = "Region server";
            this.tabRegionServer.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(6, 183);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(57, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "Command:";
            // 
            // txtInputRegionServer
            // 
            this.txtInputRegionServer.Location = new System.Drawing.Point(69, 180);
            this.txtInputRegionServer.Name = "txtInputRegionServer";
            this.txtInputRegionServer.Size = new System.Drawing.Size(479, 20);
            this.txtInputRegionServer.TabIndex = 0;
            // 
            // txtOpenSim
            // 
            this.txtOpenSim.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtOpenSim.Location = new System.Drawing.Point(6, 6);
            this.txtOpenSim.Multiline = true;
            this.txtOpenSim.Name = "txtOpenSim";
            this.txtOpenSim.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtOpenSim.Size = new System.Drawing.Size(542, 168);
            this.txtOpenSim.TabIndex = 0;
            // 
            // tabUserServer
            // 
            this.tabUserServer.Controls.Add(this.label2);
            this.tabUserServer.Controls.Add(this.txtInputUserServer);
            this.tabUserServer.Controls.Add(this.txtUserServer);
            this.tabUserServer.Location = new System.Drawing.Point(4, 22);
            this.tabUserServer.Name = "tabUserServer";
            this.tabUserServer.Padding = new System.Windows.Forms.Padding(3);
            this.tabUserServer.Size = new System.Drawing.Size(554, 204);
            this.tabUserServer.TabIndex = 1;
            this.tabUserServer.Text = "User server";
            this.tabUserServer.UseVisualStyleBackColor = true;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(6, 181);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(57, 13);
            this.label2.TabIndex = 6;
            this.label2.Text = "Command:";
            // 
            // txtInputUserServer
            // 
            this.txtInputUserServer.Location = new System.Drawing.Point(69, 178);
            this.txtInputUserServer.Name = "txtInputUserServer";
            this.txtInputUserServer.Size = new System.Drawing.Size(479, 20);
            this.txtInputUserServer.TabIndex = 5;
            // 
            // txtUserServer
            // 
            this.txtUserServer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtUserServer.Location = new System.Drawing.Point(6, 5);
            this.txtUserServer.Multiline = true;
            this.txtUserServer.Name = "txtUserServer";
            this.txtUserServer.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtUserServer.Size = new System.Drawing.Size(542, 168);
            this.txtUserServer.TabIndex = 1;
            // 
            // tabAssetServer
            // 
            this.tabAssetServer.Controls.Add(this.label3);
            this.tabAssetServer.Controls.Add(this.txtInputAssetServer);
            this.tabAssetServer.Controls.Add(this.txtAssetServer);
            this.tabAssetServer.Location = new System.Drawing.Point(4, 22);
            this.tabAssetServer.Name = "tabAssetServer";
            this.tabAssetServer.Size = new System.Drawing.Size(554, 204);
            this.tabAssetServer.TabIndex = 2;
            this.tabAssetServer.Text = "Asset server";
            this.tabAssetServer.UseVisualStyleBackColor = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(6, 182);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(57, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Command:";
            // 
            // txtInputAssetServer
            // 
            this.txtInputAssetServer.Location = new System.Drawing.Point(69, 179);
            this.txtInputAssetServer.Name = "txtInputAssetServer";
            this.txtInputAssetServer.Size = new System.Drawing.Size(479, 20);
            this.txtInputAssetServer.TabIndex = 5;
            // 
            // txtAssetServer
            // 
            this.txtAssetServer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtAssetServer.Location = new System.Drawing.Point(6, 5);
            this.txtAssetServer.Multiline = true;
            this.txtAssetServer.Name = "txtAssetServer";
            this.txtAssetServer.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtAssetServer.Size = new System.Drawing.Size(542, 168);
            this.txtAssetServer.TabIndex = 1;
            // 
            // tabGridServer
            // 
            this.tabGridServer.Controls.Add(this.label4);
            this.tabGridServer.Controls.Add(this.txtInputGridServer);
            this.tabGridServer.Controls.Add(this.txtGridServer);
            this.tabGridServer.Location = new System.Drawing.Point(4, 22);
            this.tabGridServer.Name = "tabGridServer";
            this.tabGridServer.Size = new System.Drawing.Size(554, 204);
            this.tabGridServer.TabIndex = 3;
            this.tabGridServer.Text = "Grid server";
            this.tabGridServer.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(6, 182);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(57, 13);
            this.label4.TabIndex = 6;
            this.label4.Text = "Command:";
            // 
            // txtInputGridServer
            // 
            this.txtInputGridServer.Location = new System.Drawing.Point(69, 179);
            this.txtInputGridServer.Name = "txtInputGridServer";
            this.txtInputGridServer.Size = new System.Drawing.Size(479, 20);
            this.txtInputGridServer.TabIndex = 5;
            // 
            // txtGridServer
            // 
            this.txtGridServer.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtGridServer.Location = new System.Drawing.Point(6, 5);
            this.txtGridServer.Multiline = true;
            this.txtGridServer.Name = "txtGridServer";
            this.txtGridServer.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtGridServer.Size = new System.Drawing.Size(542, 168);
            this.txtGridServer.TabIndex = 1;
            // 
            // gbLog
            // 
            this.gbLog.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.gbLog.Controls.Add(this.tabLogs);
            this.gbLog.Location = new System.Drawing.Point(2, 41);
            this.gbLog.Name = "gbLog";
            this.gbLog.Size = new System.Drawing.Size(574, 255);
            this.gbLog.TabIndex = 1;
            this.gbLog.TabStop = false;
            this.gbLog.Text = "Logs";
            // 
            // btnStart
            // 
            this.btnStart.Location = new System.Drawing.Point(8, 12);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(75, 23);
            this.btnStart.TabIndex = 2;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnStop
            // 
            this.btnStop.Location = new System.Drawing.Point(89, 12);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(75, 23);
            this.btnStop.TabIndex = 3;
            this.btnStop.Text = "Stop";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);
            // 
            // rbGridRegionMode
            // 
            this.rbGridRegionMode.AutoSize = true;
            this.rbGridRegionMode.Location = new System.Drawing.Point(407, 18);
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
            this.rbStandAloneMode.Location = new System.Drawing.Point(319, 18);
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
            this.rbGridServer.Location = new System.Drawing.Point(484, 18);
            this.rbGridServer.Name = "rbGridServer";
            this.rbGridServer.Size = new System.Drawing.Size(76, 17);
            this.rbGridServer.TabIndex = 6;
            this.rbGridServer.Text = "Grid server";
            this.rbGridServer.UseVisualStyleBackColor = true;
            this.rbGridServer.CheckedChanged += new System.EventHandler(this.rbGridServer_CheckedChanged);
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(583, 299);
            this.Controls.Add(this.rbGridServer);
            this.Controls.Add(this.rbStandAloneMode);
            this.Controls.Add(this.rbGridRegionMode);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.gbLog);
            this.Name = "Main";
            this.Text = "OpenSim";
            this.Load += new System.EventHandler(this.Main_Load);
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
            this.gbLog.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TabControl tabLogs;
        private System.Windows.Forms.TabPage tabRegionServer;
        private System.Windows.Forms.TabPage tabUserServer;
        private System.Windows.Forms.GroupBox gbLog;
        private System.Windows.Forms.TextBox txtOpenSim;
        private System.Windows.Forms.TextBox txtUserServer;
        private System.Windows.Forms.TabPage tabAssetServer;
        private System.Windows.Forms.TextBox txtAssetServer;
        private System.Windows.Forms.TabPage tabGridServer;
        private System.Windows.Forms.TextBox txtGridServer;
        private System.Windows.Forms.TabPage tabMainLog;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.TextBox txtMainLog;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private InputTextBoxControl txtInputRegionServer;
        private InputTextBoxControl txtInputUserServer;
        private InputTextBoxControl txtInputAssetServer;
        private InputTextBoxControl txtInputGridServer;
        private System.Windows.Forms.RadioButton rbGridRegionMode;
        private System.Windows.Forms.RadioButton rbStandAloneMode;
        private System.Windows.Forms.RadioButton rbGridServer;
    }
}


