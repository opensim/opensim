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
using OpenSim.Framework.Console;

namespace OpenSim.Framework.Servers
{
    /// <summary>
    /// Common base for the main OpenSimServers (user, grid, inventory, region, etc)
    /// </summary>
    public abstract class BaseOpenSimServer
    {
        protected ConsoleBase m_console;
        protected DateTime m_startuptime;

        protected BaseHttpServer m_httpServer;
        public BaseHttpServer HttpServer
        {
            get { return m_httpServer; }
        }
        
        public BaseOpenSimServer()
        {
            m_startuptime = DateTime.Now;
        }    

        /// <summary>
        /// Should be overriden by descendents if they need to perform extra shutdown processing
        /// </summary>
        public virtual void Shutdown()
        {
            if (m_console != null)
            {
                m_console.Close();
            }
            Environment.Exit(0);
        }
        
        /// <summary>
        /// Runs commands issued by the server console from the operator
        /// </summary>
        /// <param name="command">The first argument of the parameter (the command)</param>
        /// <param name="cmdparams">Additional arguments passed to the command</param>
        public virtual void RunCmd(string command, string[] cmdparams)
        {
            switch (command)
            {      
                case "help":
                    Notice("quit - equivalent to shutdown.");
                    Notice("show uptime - show server startup and uptime.");
                    Notice("shutdown - shutdown the server.\n");
                    break;
                    
                case "show":
                    if (cmdparams.Length > 0)
                    {
                        Show(cmdparams[0]);
                    }
                    break;

                case "quit":
                case "shutdown":
                    Shutdown();
                    break;                   
            }
        }
        
        /// <summary>
        /// Outputs to the console information about the region
        /// </summary>
        /// <param name="ShowWhat">What information to display (valid arguments are "uptime", "users")</param>
        public virtual void Show(string ShowWhat)
        {
            switch (ShowWhat)
            {
                case "uptime":
                    Notice("Server has been running since " + m_startuptime.DayOfWeek + ", " + m_startuptime.ToString());
                    Notice("That is an elapsed time of " + (DateTime.Now - m_startuptime).ToString());
                    break;                    
            }
        }

        /// <summary>
        /// Console output is only possible if a console has been established.
        /// That is something that cannot be determined within this class. So
        /// all attempts to use the console MUST be verified.
        /// </summary>
        private void Notice(string msg)
        {
            if (m_console != null)
            {
                m_console.Notice(msg);
            }
        }

    }
}
