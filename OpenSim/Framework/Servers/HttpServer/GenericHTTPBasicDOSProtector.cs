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

using System.Collections;

namespace OpenSim.Framework.Servers.HttpServer
{
    public class GenericHTTPDOSProtector
    {
        private readonly GenericHTTPMethod _normalMethod;
        private readonly GenericHTTPMethod _throttledMethod;

        private readonly BasicDosProtectorOptions _options;
        private readonly BasicDOSProtector _dosProtector;

        public GenericHTTPDOSProtector(GenericHTTPMethod normalMethod, GenericHTTPMethod throttledMethod, BasicDosProtectorOptions options)
        {
            _normalMethod = normalMethod;
            _throttledMethod = throttledMethod;

            _options = options;
            _dosProtector = new BasicDOSProtector(_options);
        }
        public Hashtable Process(Hashtable request)
        {
            Hashtable process = null;
            string clientstring= GetClientString(request);
            string endpoint = GetRemoteAddr(request);
            if (_dosProtector.Process(clientstring, endpoint))
                process =  _normalMethod(request);
            else
                process = _throttledMethod(request);

            if (_options.MaxConcurrentSessions>0)
                _dosProtector.ProcessEnd(clientstring, endpoint);

            return process;
        }

        private string GetRemoteAddr(Hashtable request)
        {
            string remoteaddr = "";
            if (!request.ContainsKey("headers"))
                return remoteaddr;
            Hashtable requestinfo = (Hashtable)request["headers"];
            if (!requestinfo.ContainsKey("remote_addr"))
                return remoteaddr;
            object remote_addrobj = requestinfo["remote_addr"];
            if (remote_addrobj != null)
            {
                if (!string.IsNullOrEmpty(remote_addrobj.ToString()))
                {
                    remoteaddr = remote_addrobj.ToString();
                }

            }
            return remoteaddr;
        }

        private string GetClientString(Hashtable request)
        {
            string clientstring = "";
            if (!request.ContainsKey("headers"))
                return clientstring;

            Hashtable requestinfo = (Hashtable)request["headers"];
            if (_options.AllowXForwardedFor && requestinfo.ContainsKey("x-forwarded-for"))
            {
                object str = requestinfo["x-forwarded-for"];
                if (str != null)
                {
                    if (!string.IsNullOrEmpty(str.ToString()))
                    {
                        return str.ToString();
                    }
                }
            }
            if (!requestinfo.ContainsKey("remote_addr"))
                return clientstring;

            object remote_addrobj = requestinfo["remote_addr"];
            if (remote_addrobj != null)
            {
                if (!string.IsNullOrEmpty(remote_addrobj.ToString()))
                {
                    clientstring = remote_addrobj.ToString();
                }
            }

            return clientstring;

        }

    }
}
