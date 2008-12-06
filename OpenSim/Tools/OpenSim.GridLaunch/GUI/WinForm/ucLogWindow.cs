using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace OpenSim.GridLaunch.GUI.WinForm
{
    public partial class ucLogWindow : UserControl
    {
        // If text in window is more than this
        private static readonly int logWindowMaxTextLength = 20000;
        // Remove this much from start of it
        private static int logWindowTrunlTextLength = 10000;

        public ucLogWindow()
        {
            if (logWindowMaxTextLength < logWindowTrunlTextLength)
                logWindowTrunlTextLength = logWindowMaxTextLength / 2;

            InitializeComponent();
        }

        public delegate void textWriteDelegate(Color color, string LogText);
        public void Write(Color color, string LogText)
        {
            // Check if we to pass task on to GUI thread
            if (this.InvokeRequired)
            {
                this.Invoke(new textWriteDelegate(Write), color, LogText);
                return;
            }
            // Append to window
            try
            {
                if (!txtLog.IsDisposed)
                    txtLog.AppendText(LogText);
            } catch { }
        }

        private void txtLog_TextChanged(object sender, EventArgs e)
        {
            // Go to bottom of window
            txtLog.ScrollToCaret();

            // Make sure amount of text in window doesn't grow too big
            if (txtLog.Text.Length > logWindowMaxTextLength)
                txtLog.Text = txtLog.Text.Remove(0, logWindowTrunlTextLength);
        }

    }
}
