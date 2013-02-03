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
using System.Net;
using Nini.Config;
using OpenSim.Framework;

namespace OpenSim.Region.ClientStack.TCPJSONStream
{
    public sealed class TCPJsonWebSocketBase : IClientNetworkServer
    {
        private TCPJsonWebSocketServer m_tcpServer;

        public TCPJsonWebSocketBase()
        {
        }

        public void Initialise(IPAddress _listenIP, ref uint port, int proxyPortOffsetParm, bool allow_alternate_port, IConfigSource configSource, AgentCircuitManager authenticateClass)
        {
            m_tcpServer = new TCPJsonWebSocketServer(_listenIP,ref port, proxyPortOffsetParm, allow_alternate_port,configSource,authenticateClass);
        }

        public void NetworkStop()
        {
            m_tcpServer.Stop();
        }

        public bool HandlesRegion(Location x)
        {
            return m_tcpServer.HandlesRegion(x);
        }

        public void AddScene(IScene x)
        {
            m_tcpServer.AddScene(x);
        }

        public void Start()
        {
            m_tcpServer.Start();
        }

        public void Stop()
        {
            m_tcpServer.Stop();
        }
    }
}