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
