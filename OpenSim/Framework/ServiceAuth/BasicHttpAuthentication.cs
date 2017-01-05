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
using System.Net;
using System.Reflection;

using Nini.Config;
using log4net;

namespace OpenSim.Framework.ServiceAuth
{
    public class BasicHttpAuthentication : IServiceAuth
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public string Name { get { return "BasicHttp"; } }

        private string m_Username, m_Password;
        private string m_CredentialsB64;

//        private string remove_me;

        public string Credentials
        {
            get { return m_CredentialsB64; }
        }

        public BasicHttpAuthentication(IConfigSource config, string section)
        {
//            remove_me = section;
            m_Username = Util.GetConfigVarFromSections<string>(config, "HttpAuthUsername", new string[] { "Network", section }, string.Empty);
            m_Password = Util.GetConfigVarFromSections<string>(config, "HttpAuthPassword", new string[] { "Network", section }, string.Empty);
            string str = m_Username + ":" + m_Password;
            byte[] encData_byte = Util.UTF8.GetBytes(str);

            m_CredentialsB64 = Convert.ToBase64String(encData_byte);
//            m_log.DebugFormat("[HTTP BASIC AUTH]: {0} {1} [{2}]", m_Username, m_Password, section);
        }

        public void AddAuthorization(NameValueCollection headers)
        {
            //m_log.DebugFormat("[HTTP BASIC AUTH]: Adding authorization for {0}", remove_me);
            headers["Authorization"] = "Basic " + m_CredentialsB64;
        }

        public bool Authenticate(string data)
        {
            string recovered = Util.Base64ToString(data);
            if (!String.IsNullOrEmpty(recovered))
            {
                string[] parts = recovered.Split(new char[] { ':' });
                if (parts.Length >= 2)
                {
                    return m_Username.Equals(parts[0]) && m_Password.Equals(parts[1]);
                }
            }

            return false;
        }

        public bool Authenticate(NameValueCollection requestHeaders, AddHeaderDelegate d, out HttpStatusCode statusCode)
        {
//            m_log.DebugFormat("[HTTP BASIC AUTH]: Authenticate in {0}", "BasicHttpAuthentication");

            string value = requestHeaders.Get("Authorization");
            if (value != null)
            {
                value = value.Trim();
                if (value.StartsWith("Basic "))
                {
                    value = value.Replace("Basic ", string.Empty);
                    if (Authenticate(value))
                    {
                        statusCode = HttpStatusCode.OK;
                        return true;
                    }
                }
            }

            d("WWW-Authenticate", "Basic realm = \"Asset Server\"");

            statusCode = HttpStatusCode.Unauthorized;
            return false;
        }
    }
}
