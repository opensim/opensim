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
using System.Collections.Specialized;
using System.Linq;
using System.Net;

namespace OpenSim.Framework.ServiceAuth
{
    public class CompoundAuthentication : IServiceAuth
    {
        public string Name { get { return "Compound"; } }

        private List<IServiceAuth> m_authentications = new List<IServiceAuth>();

        public int Count { get { return m_authentications.Count; } }

        public List<IServiceAuth> GetAuthentors()
        {
            return new List<IServiceAuth>(m_authentications);
        }

        public void AddAuthenticator(IServiceAuth auth)
        {
            m_authentications.Add(auth);
        }

        public void RemoveAuthenticator(IServiceAuth auth)
        {
            m_authentications.Remove(auth);
        }

        public void AddAuthorization(NameValueCollection headers) 
        {
            foreach (IServiceAuth auth in m_authentications)
                auth.AddAuthorization(headers);
        }

        public bool Authenticate(string data)
        {
            return m_authentications.TrueForAll(a => a.Authenticate(data));
        }

        public bool Authenticate(NameValueCollection requestHeaders, AddHeaderDelegate d, out HttpStatusCode statusCode)
        {
            foreach (IServiceAuth auth in m_authentications)
            {
                if (!auth.Authenticate(requestHeaders, d, out statusCode))
                    return false;
            }

            statusCode = HttpStatusCode.OK;
            return true;
        }
    }
}