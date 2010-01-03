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
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
// THIS MODULE USES CODE FROM SIRIKATA, THE SIRIKATA LICENSE IS BELOW
/*
Sirikata is Licensed using the revised BSD license.
If you have any questions, contact Patrick Horn at <patrick.horn@gmail.com>


 Copyright (c) 2008, the Sirikata developers (see AUTHORS file for credit).
 All rights reserved.

 Redistribution and use in source and binary forms, with or without
 modification, are permitted provided that the following conditions are
 met:
 * Redistributions of source code must retain the above copyright
   notice, this list of conditions and the following disclaimer.
 * Redistributions in binary form must reproduce the above copyright
   notice, this list of conditions and the following disclaimer in
   the documentation and/or other materials provided with the
   distribution.
 * Neither the name of Sirikata nor the names of its contributors may
   be used to endorse or promote products derived from this software
   without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS
IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER
OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL,
EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO,
PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Client.Sirikata.ClientStack;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Client.Sirikata
{
    class SirikataModule : IRegionModule
    {
        private bool m_enabled = false;

        private TcpListener m_listener;
        private bool m_running = true;

        private List<Scene> m_scenes = new List<Scene>();
        private Dictionary<UUID,SirikataClientView> m_clients = new Dictionary<UUID, SirikataClientView>();

        #region Implementation of IRegionModule

        public void Initialise(Scene scene, IConfigSource source)
        {
            lock (m_scenes)
                m_scenes.Add(scene);
        }

        public void PostInitialise()
        {
            if (!m_enabled)
                return;

            m_listener = new TcpListener(IPAddress.Any, 5943);
            
        }

        private void ListenLoop()
        {
            while (m_running)
            {
                m_listener.BeginAcceptTcpClient(AcceptSocket, m_listener);
            }
        }

        private void AcceptSocket(IAsyncResult ar)
        {
            TcpListener listener = (TcpListener) ar.AsyncState;
            TcpClient client = listener.EndAcceptTcpClient(ar);

            SirikataClientView clientView = new SirikataClientView(client);

            lock (m_clients)
                m_clients.Add(clientView.SessionId, clientView);
        }

        public void Close()
        {
            
        }

        public string Name
        {
            get { return "Sirikata ClientStack Module"; }
        }

        public bool IsSharedModule
        {
            get { return true; }
        }

        #endregion
    }
}
