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

using OpenSim.Framework;

namespace OpenSim.Data
{
    /// <summary>
    /// The severity of an individual log message
    /// </summary>
    public enum LogSeverity : int
    {
        /// <summary>
        /// Critical: systems failure
        /// </summary>
        CRITICAL = 1,
        /// <summary>
        /// Major: warning prior to systems failure
        /// </summary>
        MAJOR = 2,
        /// <summary>
        /// Medium: an individual non-critical task failed
        /// </summary>
        MEDIUM = 3,
        /// <summary>
        /// Low: Informational warning
        /// </summary>
        LOW = 4,
        /// <summary>
        /// Info: Information
        /// </summary>
        INFO = 5,
        /// <summary>
        /// Verbose: Debug Information
        /// </summary>
        VERBOSE = 6
    }

    /// <summary>
    /// An interface to a LogData storage system
    /// </summary>
    public interface ILogDataPlugin : IPlugin
    {
        void saveLog(string serverDaemon, string target, string methodCall, string arguments, int priority,
                     string logMessage);

        /// <summary>
        /// Initialises the interface
        /// </summary>
        void Initialise(string connect);
    }

    public class LogDataInitialiser : PluginInitialiserBase
    {
        private string connect;
        public LogDataInitialiser (string s) { connect = s; }
        public override void Initialise (IPlugin plugin)
        {
            ILogDataPlugin p = plugin as ILogDataPlugin;
            p.Initialise (connect);
        }
    }
}
