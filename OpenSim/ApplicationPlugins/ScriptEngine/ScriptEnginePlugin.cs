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
using System.Reflection;
using log4net;

namespace OpenSim.ApplicationPlugins.ScriptEngine
{
    /// <summary>
    /// Loads all Script Engine Components
    /// </summary>
    public class ScriptEnginePlugin : IApplicationPlugin
    {
        internal static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        internal OpenSimBase m_OpenSim;



        public ScriptEnginePlugin()
        {
            // Application startup
#if DEBUG
            m_log.InfoFormat("[{0}] ##################################", Name);
            m_log.InfoFormat("[{0}] # Script Engine Component System #", Name);
            m_log.InfoFormat("[{0}] ##################################", Name);
#else
            m_log.InfoFormat("[{0}] Script Engine Component System", Name);
#endif

            // Load all modules from current directory
            // We only want files named OpenSim.ScriptEngine.*.dll
            ComponentFactory.Load(".", "OpenSim.ScriptEngine.*.dll");
        }

        public void Initialise(OpenSimBase openSim)
        {

            // Our objective: Load component .dll's
            m_OpenSim = openSim;
            //m_OpenSim.Shutdown();
        }

        public void PostInitialise()
        {
        }


        #region IApplicationPlugin stuff
        /// <summary>
        /// Returns the plugin version
        /// </summary>
        /// <returns>Plugin version in MAJOR.MINOR.REVISION.BUILD format</returns>
        public string Version
        {
            get { return "1.0.0.0"; }
        }

        /// <summary>
        /// Returns the plugin name
        /// </summary>
        /// <returns>Plugin name, eg MySQL User Provider</returns>
        public string Name
        {
            get { return "SECS"; }
        }

        /// <summary>
        /// Default-initialises the plugin
        /// </summary>
        public void Initialise() { }

        ///<summary>
        ///Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        ///</summary>
        ///<filterpriority>2</filterpriority>
        public void Dispose()
        {
            //throw new NotImplementedException();
        }
        #endregion

    }
}
