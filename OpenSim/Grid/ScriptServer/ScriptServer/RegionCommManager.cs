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

using System.Collections.Generic;
using System.Threading;
using OpenSim.Framework;
using OpenSim.Grid.ScriptServer.ScriptServer.Region;

namespace OpenSim.Grid.ScriptServer.ScriptServer
{
    internal class RegionCommManager
    {
        private Thread listenThread;

        private List<RegionConnectionManager> Regions = new List<RegionConnectionManager>();

        private ScriptServerMain m_ScriptServerMain;

        public RegionCommManager(ScriptServerMain scm)
        {
            m_ScriptServerMain = scm;
        }

        ~RegionCommManager()
        {
            Stop();
        }

        /// <summary>
        /// Starts listening for region requests
        /// </summary>
        public void Start()
        {
            // Start listener
            Stop();
            listenThread = new Thread(ListenThreadLoop);
            listenThread.Name = "ListenThread";
            listenThread.IsBackground = true;
            listenThread.Start();
            ThreadTracker.Add(listenThread);
        }

        /// <summary>
        /// Stops listening for region requests
        /// </summary>
        public void Stop()
        {
            // Stop listener, clean up
            if (listenThread != null)
            {
                try
                {
                    if (listenThread.IsAlive)
                        listenThread.Abort();
                    listenThread.Join(1000); // Wait 1 second for thread to shut down
                }
                catch
                {
                }
                listenThread = null;
            }
        }

        private void ListenThreadLoop()
        {
            // * Listen for requests from regions
            // * When a request is received:
            //  - Authenticate region
            //  - Authenticate user
            //  - Have correct scriptengine load script
            //   ~ ask scriptengines if they will accept script?
            //  - Add script to shared communication channel towards that region

            // TODO: FAKING A CONNECTION
            Regions.Add(new RegionConnectionManager(m_ScriptServerMain, null));
        }
    }
}