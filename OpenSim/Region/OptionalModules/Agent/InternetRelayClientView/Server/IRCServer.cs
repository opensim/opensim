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
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using log4net;
using OpenSim.Framework;
using OpenSim.Framework.Monitoring;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Agent.InternetRelayClientView.Server
{
    public delegate void OnNewIRCUserDelegate(IRCClientView user);

    /// <summary>
    /// Adam's completely hacked up not-probably-compliant RFC1459 server class.
    /// </summary>
    class IRCServer
    {
        public event OnNewIRCUserDelegate OnNewIRCClient;

        private readonly TcpListener m_listener;
        private readonly Scene m_baseScene;
        private bool m_running = true;

        public IRCServer(IPAddress listener, int port, Scene baseScene)
        {
            m_listener = new TcpListener(listener, port);

            m_listener.Start(50);

            WorkManager.StartThread(ListenLoop, "IRCServer", ThreadPriority.Normal, false, true);
            m_baseScene = baseScene;
        }

        public void Stop()
        {
            m_running = false;
            m_listener.Stop();
        }

        private void ListenLoop()
        {
            while (m_running)
            {
                AcceptClient(m_listener.AcceptTcpClient());
                Watchdog.UpdateThread();
            }

            Watchdog.RemoveThread();
        }

        private void AcceptClient(TcpClient client)
        {
            IRCClientView cv = new IRCClientView(client, m_baseScene);

            if (OnNewIRCClient != null)
                OnNewIRCClient(cv);
        }
    }
}
