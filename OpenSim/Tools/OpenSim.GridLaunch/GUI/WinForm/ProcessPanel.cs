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
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace OpenSim.GridLaunch.GUI.WinForm
{
    public partial class ProcessPanel : Form, IGUI
    {
        public ProcessPanel()
        {
            Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);

            InitializeComponent();
            Program.AppCreated += Program_AppCreated;
            Program.AppRemoved += Program_AppRemoved;
            Program.AppConsoleOutput += Program_AppConsoleOutput;
            Program.AppConsoleError += Program_AppConsoleError;
            log4netAppender.LogLine += log4netAppender_LogLine;
        }

        #region Module Start / Stop
        public void StartGUI()
        {
            Application.Run(this);
        }

        public void StopGUI()
        {
            this.Close();
        }
        #endregion

        #region Main log tab
        void log4netAppender_LogLine(Color color, string LogText)
        {
            ucLogWindow1.Write(color, LogText);
        }
        #endregion

        #region Form events
        private void btnShutdown_Click(object sender, EventArgs e)
        {
            Program.Shutdown();
        }
        #endregion

        #region GridLaunch Events
        public delegate void Program_AppCreatedDelegate(string App);
        public void Program_AppCreated(string App)
        {
            if (this.InvokeRequired) {
                this.Invoke(new Program_AppCreatedDelegate(Program_AppCreated), App);
                return;
            }

            Trace.WriteLine("Start: " + App);

            // Do we already have app window for that app?
            if (AppWindow_Get(App) != null)
                return;

            // New log window
            ucAppWindow aw = new ucAppWindow();
            // New tab page
            TabPage tp = new TabPage(App);
            // Add log window into tab page
            tp.Controls.Add(aw);
            // Add tab page into tab control
            tabControl1.TabPages.Add(tp);
            // Add it all to our internal list
            AppWindow_Add(App, aw);
            // Hook up events
            aw.LineEntered += AppWindow_LineEntered;

            // Fill log window fully inside tab page
            aw.Dock = DockStyle.Fill;
        }


        public delegate void Program_AppRemovedDelegate(string App);
        public void Program_AppRemoved(string App)
        {
            if (this.InvokeRequired) {
                this.Invoke(new Program_AppRemovedDelegate(Program_AppRemoved), App);
                return;
            }

            Trace.WriteLine("Stop: " + App);

            // Get app window
            ucAppWindow aw = AppWindow_Get(App);
            if (aw == null)
                return;

            // Get its tab page
            TabPage tp = aw.Parent as TabPage;

            if (tp != null)
            {
                // Remove tab page from tab control
                tabControl1.TabPages.Remove(tp);
                // Remove app window from tab
                tp.Controls.Remove(aw);
            }

            // Dispose of app window
            aw.Dispose();

            // Dispose of tab page
            if (tp != null)
                tp.Dispose();

            // Remove from our internal list
            AppWindow_Remove(App);
        }


        public delegate void Program_AppConsoleOutputDelegate(string App, string LogText);
        void Program_AppConsoleOutput(string App, string LogText)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Program_AppConsoleOutputDelegate(Program_AppConsoleOutput), App, LogText);
                return;
            }

            // Get app window
            ucAppWindow aw = AppWindow_Get(App);
            // Write text to it
            if (aw != null)
                aw.Write(System.Drawing.Color.Black, LogText);
        }

        public delegate void Program_AppConsoleErrorDelegate(string App, string LogText);
        void Program_AppConsoleError(string App, string LogText)
        {
            if (this.InvokeRequired) {
                this.Invoke(new Program_AppConsoleErrorDelegate(Program_AppConsoleError), App, LogText);
                return;
            }

            // Get app window
            ucAppWindow aw = AppWindow_Get(App);
            // Write text to it
            if (aw != null)
                aw.Write(System.Drawing.Color.Red, LogText);

        }
        #endregion

        #region App Window events
        private void AppWindow_LineEntered(ucAppWindow AppWindow, string LogText)
        {
            Program.WriteLine(AppWindow_Get(AppWindow), LogText);
        }
        #endregion

        private void ProcessPanel_Load(object sender, EventArgs e)
        {
            string[] arr = new string[Program.Settings.Components.Keys.Count];
            Program.Settings.Components.Keys.CopyTo(arr, 0);
            cblStartupComponents.Items.AddRange(arr);

            // Now correct all check states
            for (int i = 0; i < cblStartupComponents.Items.Count; i++)
            {
                string _name = cblStartupComponents.Items[i] as string;
                bool _checked = Program.Settings.Components[_name];

                cblStartupComponents.SetItemChecked(i, _checked);
            }
            
            
        }




        #region Internal App Window list and functions
        private Dictionary<string, ucAppWindow> _appWindows = new Dictionary<string, ucAppWindow>();
        private Dictionary<ucAppWindow, string> _appWindows_rev = new Dictionary<ucAppWindow, string>();
        private void AppWindow_Add(string AppWindowName, ucAppWindow AppWindow)
        {
            lock (_appWindows)
            {
                _appWindows.Add(AppWindowName, AppWindow);
                _appWindows_rev.Add(AppWindow, AppWindowName);
                // Hook events
                AppWindow.LineEntered += AppWindow_LineEntered;
            }
        }
        private void AppWindow_Remove(ucAppWindow AppWindow)
        {
            lock (_appWindows)
            {
                if (_appWindows_rev.ContainsKey(AppWindow))
                {
                    // Unhook events
                    AppWindow.LineEntered -= AppWindow_LineEntered;
                    // Delete from list
                    string name = _appWindows_rev[AppWindow];
                    _appWindows.Remove(name);
                    _appWindows_rev.Remove(AppWindow);
                }
            }
        }
        private void AppWindow_Remove(string AppWindowName)
        {
            lock (_appWindows)
            {
                if (_appWindows.ContainsKey(AppWindowName))
                {
                    ucAppWindow AppWindow = _appWindows[AppWindowName];
                    // Unhook events
                    AppWindow.LineEntered -= AppWindow_LineEntered;
                    // Delete from list
                    _appWindows.Remove(AppWindowName);
                    _appWindows_rev.Remove(AppWindow);
                }
            }
        }
        private string AppWindow_Get(ucAppWindow AppWindow)
        {
            lock (_appWindows)
            {
                if (_appWindows_rev.ContainsKey(AppWindow))
                    return _appWindows_rev[AppWindow];
            }
            return null;
        }
        private ucAppWindow AppWindow_Get(string AppWindowName)
        {
            lock (_appWindows)
            {
                if (_appWindows.ContainsKey(AppWindowName))
                    return _appWindows[AppWindowName];
            }
            return null;
        }
        #endregion

        private void btnSave_Click(object sender, EventArgs e)
        {
            Program.Settings.Components.Clear();
            for (int i = 0; i < cblStartupComponents.Items.Count; i++)
            {
                string _name = cblStartupComponents.Items[i] as string;
                bool _checked = cblStartupComponents.GetItemChecked(i);

                Program.Settings.Components.Add(_name, _checked);
                Program.Settings.SaveConfig();
            }
        }

    }
}
