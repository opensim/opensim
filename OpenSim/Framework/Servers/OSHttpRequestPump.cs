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
using HttpServer;

namespace OpenSim.Framework.Servers
{
    /// <summary>
    /// An OSHttpRequestPump fetches incoming OSHttpRequest objects
    /// from the OSHttpRequestQueue and feeds them to all subscribed
    /// parties. Each OSHttpRequestPump encapsulates one thread to do
    /// the work and there is a fixed number of pumps for each
    /// OSHttpServer object.
    /// </summary>
    public class OSHttpRequestPump
    {
        protected OSHttpServer _server;
        protected OSHttpRequestQueue _queue;

        public OSHttpRequestPump()
        {
        }

        public static OSHttpRequestPump[] Pumps(OSHttpServer server, OSHttpRequestQueue queue, int poolSize)
        {
            OSHttpRequestPump[] pumps = new OSHttpRequestPump[poolSize];
            for (int i = 0; i < pumps.Length; i++)
            {
                pumps[i]._server = server;
                pumps[i]._queue = queue;
            }

            return pumps;
        }
    }
}
