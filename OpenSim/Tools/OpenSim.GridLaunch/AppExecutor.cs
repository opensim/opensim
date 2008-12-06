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
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;

namespace OpenSim.GridLaunch
{
    internal partial class AppExecutor : IDisposable
    {
        // How long to wait for process to shut down by itself
        private static readonly int shutdownWaitSeconds = 10;

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //private StreamWriter Input { get { return process.StandardInput; } }
        //private StreamReader Output { get { return process.StandardOutput; } }
        //private StreamReader Error { get { return process.StandardError; } }

        private StreamWriter Input { get; set; }
        private StreamReader Output { get; set; }
        private StreamReader Error { get; set; }

        private object processLock = new object();

        private bool isRunning = false;
        public bool IsRunning { get { return isRunning; } }

        private string file;
        public string File { get { return file; } }

        Process process;

        public AppExecutor(string File)
        {
            file = File;
        }

        #region Dispose of unmanaged resources
        ~AppExecutor()
        {
            Dispose();
        }
        private bool isDisposed = false;
        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                Stop();
            }
        }
        #endregion

        #region Start / Stop process
        public void Start()
        {
            if (isDisposed)
                throw new ApplicationException("Attempt to start process in Disposed instance of AppExecutor.");
            // Stop before starting
            Stop();

            lock (processLock)
            {
                isRunning = true;

                m_log.InfoFormat("Starting \"{0}\".", file);

                // Start the process
                process = new Process();
                process.StartInfo.FileName = file;
                process.StartInfo.Arguments = "";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.ErrorDialog = false;
                process.EnableRaisingEvents = true;


                // Redirect all standard input/output/errors
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                // Start process
                process.Start();

                Input = process.StandardInput;
                Output = process.StandardOutput;
                Error = process.StandardError;

                // Start data copying
                timer_Start();

                // We will flush manually
                //Input.AutoFlush = false;

            }
        }

        public void Stop()
        {
            // Shut down process
            // We will ignore some exceptions here, against good programming practice... :)

            lock (processLock)
            {
                // Running?
                if (!isRunning)
                    return;
                isRunning = false;

                timer_Stop();

                m_log.InfoFormat("Stopping \"{0}\".", file);

                // Send exit command to console
                try
                {
                    if (Input != null)
                    {
                        _writeLine("");
                        _writeLine("exit");
                        _writeLine("quit");
                        // Wait for process to exit
                        process.WaitForExit(1000 * shutdownWaitSeconds);
                    }
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("Exeption asking \"{0}\" to shut down: {1}", file, ex.ToString());
                }

                try
                {
                    // Forcefully kill it
                    if (process.HasExited != true)
                        process.Kill();
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("Exeption killing \"{0}\": {1}", file, ex.ToString());
                }

                try
                {
                    // Free resources
                    process.Close();
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("Exeption freeing resources for \"{0}\": {1}", file, ex.ToString());
                }

                // Dispose of stream and process object
                //SafeDisposeOf(Input);
                //SafeDisposeOf(Output);
                //SafeDisposeOf(Error);
                Program.SafeDisposeOf(process);
            }

            // Done stopping process
        }

        #endregion

        #region Write to stdInput
        public void Write(string Text)
        {
            // Lock so process won't shut down while we write, and that we won't write while proc is shutting down
            lock (processLock)
            {
                _write(Text);
            }
        }
        public void _write(string Text)
        {
            if (Input != null)
            {
                try
                {
                    Input.Write(Text);
                    Input.Flush();
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("Exeption sending text \"{0}\" to \"{1}\": {2}", file, Text, ex.ToString());
                }

            }
        }
        public void WriteLine(string Text)
        {
            // Lock so process won't shut down while we write, and that we won't write while proc is shutting down
            lock (processLock)
            {
                _writeLine(Text);
            }
        }
        public void _writeLine(string Text)
        {
            if (Input != null)
            {
                try
                {
                    m_log.DebugFormat("\"{0}\": Sending: \"{1}\"", file, Text);
                    Input.WriteLine(Text);
                    Input.Flush();
                }
                catch (Exception ex)
                {
                    m_log.ErrorFormat("Exeption sending text \"{0}\" to \"{1}\": {2}", file, Text, ex.ToString());
                }
            }
        }
        #endregion

    }
}
