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
using System.IO;
using System.Text;
using log4net;

namespace OpenSim.Region.CoreModules.Framework.Statistics.Logging
{
    /// <summary>
    /// Class for writing a high performance, high volume log file.
    /// Sometimes, to debug, one has a high volume logging to do and the regular
    /// log file output is not appropriate.
    /// Create a new instance with the parameters needed and
    /// call Write() to output a line. Call Close() when finished.
    /// If created with no parameters, it will not log anything.
    /// </summary>
    public class LogWriter : IDisposable
    {
        public bool Enabled { get; private set; }

        private string m_logDirectory = ".";
        private int m_logMaxFileTimeMin = 5;    // 5 minutes
        public String LogFileHeader { get; set; }

        private StreamWriter m_logFile = null;
        private TimeSpan m_logFileLife;
        private DateTime m_logFileEndTime;
        private Object m_logFileWriteLock = new Object();

        // set externally when debugging. If let 'null', this does not write any error messages.
        public ILog ErrorLogger = null;
        private string LogHeader = "[LOG WRITER]";

        /// <summary>
        /// Create a log writer that will not write anything. Good for when not enabled
        /// but the write statements are still in the code.
        /// </summary>
        public LogWriter()
        {
            Enabled = false;
            m_logFile = null;
        }

        /// <summary>
        /// Create a log writer instance.
        /// </summary>
        /// <param name="dir">The directory to create the log file in. May be 'null' for default.</param>
        /// <param name="headr">The characters that begin the log file name. May be 'null' for default.</param>
        /// <param name="maxFileTime">Maximum age of a log file in minutes. If zero, will set default.</param>
        public LogWriter(string dir, string headr, int maxFileTime)
        {
            m_logDirectory = dir == null ? "." : dir;

            LogFileHeader = headr == null ? "log-" : headr;

            m_logMaxFileTimeMin = maxFileTime;
            if (m_logMaxFileTimeMin < 1)
                m_logMaxFileTimeMin = 5;

            m_logFileLife = new TimeSpan(0, m_logMaxFileTimeMin, 0);
            m_logFileEndTime = DateTime.Now + m_logFileLife;

            Enabled = true;
        }

        public void Dispose()
        {
            this.Close();
        }

        public void Close()
        {
            Enabled = false;
            if (m_logFile != null)
            {
                m_logFile.Close();
                m_logFile.Dispose();
                m_logFile = null;
            }
        }

        public void Write(string line, params object[] args)
        {
            if (!Enabled) return;
            Write(String.Format(line, args));
        }

        public void Flush()
        {
            if (!Enabled) return;
            if (m_logFile != null)
            {
                m_logFile.Flush();
            }
        }

        public void Write(string line)
        {
            if (!Enabled) return;
            try
            {
                lock (m_logFileWriteLock)
                {
                    DateTime now = DateTime.Now;
                    if (m_logFile == null || now > m_logFileEndTime)
                    {
                        if (m_logFile != null)
                        {
                            m_logFile.Close();
                            m_logFile.Dispose();
                            m_logFile = null;
                        }

                        // First log file or time has expired, start writing to a new log file
                        m_logFileEndTime = now + m_logFileLife;
                        string path = (m_logDirectory.Length > 0 ? m_logDirectory
                                    + System.IO.Path.DirectorySeparatorChar.ToString() : "")
                                + String.Format("{0}{1}.log", LogFileHeader, now.ToString("yyyyMMddHHmmss"));
                        m_logFile = new StreamWriter(File.Open(path, FileMode.Append, FileAccess.Write));
                    }
                    if (m_logFile != null)
                    {
                        StringBuilder buff = new StringBuilder(line.Length + 25);
                        buff.Append(now.ToString("yyyyMMddHHmmssfff"));
                        // buff.Append(now.ToString("yyyyMMddHHmmss"));
                        buff.Append(",");
                        buff.Append(line);
                        buff.Append("\r\n");
                        m_logFile.Write(buff.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                if (ErrorLogger != null)
                {
                    ErrorLogger.ErrorFormat("{0}: FAILURE WRITING TO LOGFILE: {1}", LogHeader, e);
                }
                Enabled = false;
            }
            return;
        }
    }
}
