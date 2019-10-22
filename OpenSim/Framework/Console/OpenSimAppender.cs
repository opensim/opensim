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
using log4net.Appender;
using log4net.Core;

namespace OpenSim.Framework.Console
{
    /// <summary>
    /// Writes log information out onto the console
    /// </summary>
    public class OpenSimAppender : AnsiColorTerminalAppender
    {
        private ConsoleBase m_console = null;

        public ConsoleBase Console
        {
            get { return m_console; }
            set { m_console = value; }
        }

        override protected void Append(LoggingEvent le)
        {
            if (m_console != null)
                m_console.LockOutput();

            string loggingMessage = RenderLoggingEvent(le);

            try
            {
                if (m_console != null)
                {
                    ConsoleLevel level;

                    if (le.Level == Level.Error)
                        level = "error";
                    else if (le.Level == Level.Warn)
                        level = "warn";
                    else
                        level = "normal";

                    m_console.Output(loggingMessage, level);
                }
                else
                {
                    if (!loggingMessage.EndsWith("\n"))
                        System.Console.WriteLine(loggingMessage);
                    else
                        System.Console.Write(loggingMessage);
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine("Couldn't write out log message: {0}", e.ToString());
            }
            finally
            {
                if (m_console != null)
                    m_console.UnlockOutput();
            }
        }
    }
}
