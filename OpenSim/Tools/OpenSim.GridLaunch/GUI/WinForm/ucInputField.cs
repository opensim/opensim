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
using System.Diagnostics;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace OpenSim.GridLaunch.GUI.WinForm
{
    public partial class ucInputField : UserControl
    {
        public delegate void LineEnteredDelegate(string Text);
        public event LineEnteredDelegate LineEntered;

        public List<string> History = new List<string>();

        public ucInputField()
        {
            InitializeComponent();
        }

        private void ucInputField_Load(object sender, EventArgs e)
        {
            _resize();
        }

        private void ucInputField_Resize(object sender, EventArgs e)
        {
            _resize();
        }

        private void _resize()
        {
            Height = txtInput.Height + 10;
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            Send();
        }

        private void txtInput_KeyPress(object sender, KeyPressEventArgs e)
        {
            //Trace.WriteLine("KeyChar: " + ((int)e.KeyChar).ToString());
            if (e.KeyChar == 13)
            {
                e.Handled = true;
                Send();
            }

            // TODO: Add arrow up/down history functions
        }

        private void Send()
        {
            // Remove \r\n at end
            string txt = txtInput.Text.TrimEnd("\r\n".ToCharArray());

            // Fire event
            if (LineEntered != null)
                LineEntered(txt);

            // Add to history
            History.Add(txtInput.Text);

            txtInput.Text = "";
        }
    }
}
