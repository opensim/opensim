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
//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Reflection;
//using System.Text;
//using System.Threading;
//using log4net;

//namespace OpenSim.GridLaunch
//{
//    internal class AppExecutor2
//    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
//        private static readonly int consoleReadIntervalMilliseconds = 50;
//        //private static readonly Timer readTimer = new Timer(readConsole, null, Timeout.Infinite, Timeout.Infinite);
//        private static Thread timerThread;
//        private static object timerThreadLock = new object();
        
//        #region Start / Stop timer thread
//        private static void timer_Start()
//        {
//            //readTimer.Change(0, consoleReadIntervalMilliseconds);
//            lock (timerThreadLock)
//            {
//                if (timerThread == null)
//                {
//                    m_log.Debug("Starting timer thread.");
//                    timerThread = new Thread(timerThreadLoop);
//                    timerThread.Name = "StdOutputStdErrorReadThread";
//                    timerThread.IsBackground = true;
//                    timerThread.Start();
//                }
//            }
//        }
//        private static void timer_Stop()
//        {
//            //readTimer.Change(Timeout.Infinite, Timeout.Infinite);
//            lock (timerThreadLock)
//            {
//                if (timerThread != null)
//                {
//                    m_log.Debug("Stopping timer thread.");
//                    try
//                    {
//                        if (timerThread.IsAlive)
//                            timerThread.Abort();
//                        timerThread.Join(2000);
//                        timerThread = null;
//                    }
//                    catch (Exception ex)
//                    {
//                        m_log.Error("Exception stopping timer thread: " + ex.ToString());
//                    }
//                }
//            }
//        }
//        #endregion

//        #region Timer read from consoles and fire event

//        private static void timerThreadLoop()
//        {
//            try
//            {
//                while (true)
//                {
//                    readConsole();
//                    Thread.Sleep(consoleReadIntervalMilliseconds);
//                }
//            }
//            catch (ThreadAbortException) { } // Expected on thread shutdown
//        }

//        private static void readConsole()
//        {
//            try
//            {

//                // Lock so we don't collide with any startup or shutdown
//                lock (Program.AppList)
//                {
//                    foreach (AppExecutor app in new ArrayList(Program.AppList.Values))
//                    {
//                        try
//                        {
//                            string txt = app.GetStdOutput();
//                            // Fire event with received text
//                            if (!string.IsNullOrEmpty(txt))
//                                Program.FireAppConsoleOutput(app.File, txt);
//                        }
//                        catch (Exception ex)
//                        {
//                            m_log.ErrorFormat("Exception reading standard output from \"{0}\": {1}", app.File, ex.ToString());
//                        }
//                        try
//                        {
//                            string txt = app.GetStdError();
//                            // Fire event with received text
//                            if (!string.IsNullOrEmpty(txt))
//                                Program.FireAppConsoleOutput(app.File, txt);
//                        }
//                        catch (Exception ex)
//                        {
//                            m_log.ErrorFormat("Exception reading standard error from \"{0}\": {1}", app.File, ex.ToString());
//                        }
//                    }
//                }
//            }
//            finally
//            {
//            }
//        }
//        #endregion


//        #region Read stdOutput and stdError
//        public string GetStdOutput()
//        {
//            return GetStreamData(Output);
//        }
//        public string GetStdError()
//        {
//            return GetStreamData(Error);
//        }

//        private static int num = 0;
//        // Gets any data from StreamReader object, non-blocking
//        private static string GetStreamData(StreamReader sr)
//        {
//            // Can't read?
//            if (!sr.BaseStream.CanRead)
//                return "";

//            // Read a chunk
//            //sr.BaseStream.ReadTimeout = 100;
//            byte[] buffer = new byte[4096];
//            num++;
//            Trace.WriteLine("Start read " + num);
//            int len = sr.BaseStream.Read(buffer, 0, buffer.Length);
//            Trace.WriteLine("End read " + num + ": " + len);

//            // Nothing?
//            if (len <= 0)
//                return "";

//            // Return data
//            StringBuilder sb = new StringBuilder();
//            sb.Append(System.Text.Encoding.ASCII.GetString(buffer, 0, len));

//            //while (sr.Peek() >= 0)
//            //{
//            //    sb.Append(Convert.ToChar(sr.Read()));
//            //}

//            return sb.ToString();
//        }
//        #endregion


//    }
//}
