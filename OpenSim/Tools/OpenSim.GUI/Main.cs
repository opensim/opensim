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

            if (proc_UserServer != null)
            {
                txtMainLog.AppendText("Shutting down UserServer. CPU time used: " + proc_UserServer.TotalProcessorTime.ToString() + "\r\n");
                proc_UserServer.StopProcess();
            }
            if (proc_GridServer != null)
            {
                txtMainLog.AppendText("Shutting down GridServer. CPU time used: " + proc_GridServer.TotalProcessorTime.ToString() + "\r\n");
                proc_GridServer.StopProcess();
            }
            if (proc_AssetServer != null)
            {
                txtMainLog.AppendText("Shutting down AssetServer. CPU time used: " + proc_AssetServer.TotalProcessorTime.ToString() + "\r\n");
                proc_AssetServer.StopProcess();
            }
            if (proc_OpenSim != null)
            {
                txtMainLog.AppendText("Shutting down OpenSim. CPU time used: " + proc_OpenSim.TotalProcessorTime.ToString() + "\r\n");
                proc_OpenSim.StopProcess();
            }

            btnStart.Enabled = true;
            btnStop.Enabled = false;


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

    }
}