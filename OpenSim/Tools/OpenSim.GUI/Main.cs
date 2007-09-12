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
            btnStart.Enabled = false;
            btnStop.Enabled = false;

            // Start UserServer
            proc_UserServer = new ProcessManager("OpenSim.Grid.UserServer.exe", "");
            txtMainLog.AppendText("Starting: UserServer");
            proc_UserServer.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_UserServer_DataReceived);
            proc_UserServer.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_UserServer_DataReceived);
            proc_UserServer.StartProcess();
            System.Threading.Thread.Sleep(2000);

            // Start GridServer
            proc_GridServer = new ProcessManager("OpenSim.Grid.GridServer.exe", "");
            txtMainLog.AppendText("Starting: GridServer");
            proc_GridServer.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_GridServer_DataReceived);
            proc_GridServer.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_GridServer_DataReceived);
            proc_GridServer.StartProcess();
            System.Threading.Thread.Sleep(2000);

            // Start AssetServer
            proc_AssetServer = new ProcessManager("OpenSim.Grid.AssetServer.exe", "");
            txtMainLog.AppendText("Starting: AssetServer");
            proc_AssetServer.OutputDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_AssetServer_DataReceived);
            proc_AssetServer.ErrorDataReceived += new System.Diagnostics.DataReceivedEventHandler(proc_AssetServer_DataReceived);
            proc_AssetServer.StartProcess();
            System.Threading.Thread.Sleep(2000);

            // Start OpenSim
            proc_OpenSim = new ProcessManager("OpenSim.EXE", "-gridmode=true");
            txtMainLog.AppendText("Starting: OpenSim");
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

            if (proc_UserServer != null)
            {
                txtMainLog.AppendText("Shutting down UserServer. CPU time used: " + proc_UserServer.TotalProcessorTime);
                proc_UserServer.StopProcess();
            }
            if (proc_GridServer != null)
            {
                txtMainLog.AppendText("Shutting down GridServer. CPU time used: " + proc_GridServer.TotalProcessorTime);
                proc_GridServer.StopProcess();
            }
            if (proc_AssetServer != null)
            {
                txtMainLog.AppendText("Shutting down AssetServer. CPU time used: " + proc_AssetServer.TotalProcessorTime);
                proc_AssetServer.StopProcess();
            }
            if (proc_OpenSim != null)
            {
                txtMainLog.AppendText("Shutting down OpenSim. CPU time used: " + proc_OpenSim.TotalProcessorTime);
                proc_OpenSim.StopProcess();
            }

            btnStart.Enabled = true;
            btnStop.Enabled = false;


        }


    }
}