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

using log4net;
using System;
using System.Net;
using System.Reflection;
using Nini.Config;

namespace OpenSim.Services.Connectors
{
    public class HeloServicesConnector
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;

        public HeloServicesConnector()
        {
        }

        public HeloServicesConnector(string serverURI)
        {
            try
            {
                Uri uri;

                if (!serverURI.EndsWith("="))
                {
                    // Let's check if this is a valid URI, because it may not be
                    uri = new Uri(serverURI);
                    m_ServerURI = serverURI.TrimEnd('/') + "/helo/";
                }
                else
                {
                    // Simian sends malformed urls like this:
                    // http://valley.virtualportland.org/simtest/Grid/?id=
                    //
                    uri = new Uri(serverURI + "xxx");
                    if (uri.Query == string.Empty)
                        m_ServerURI = serverURI.TrimEnd('/') + "/helo/";
                    else
                    {
                        serverURI = serverURI + "xxx";
                        m_ServerURI = serverURI.Replace(uri.Query, "");
                        m_ServerURI = m_ServerURI.TrimEnd('/') + "/helo/";
                    }
                }

            }
            catch (UriFormatException)
            {
                m_log.WarnFormat("[HELO SERVICE]: Malformed URL {0}", serverURI);
            }
        }

        public virtual string Helo()
        {
            if (String.IsNullOrEmpty(m_ServerURI))
            {
                m_log.WarnFormat("[HELO SERVICE]: Unable to invoke HELO due to empty URL");
                return String.Empty;
            }

            try
            {
                HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(m_ServerURI);
                // Eventually we need to switch to HEAD
                /* req.Method = "HEAD"; */

                using (WebResponse response = req.GetResponse())
                {
                    if (response.Headers.Get("X-Handlers-Provided") == null) // just in case this ever returns a null
                        return string.Empty;
                    return response.Headers.Get("X-Handlers-Provided");
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[HELO SERVICE]: Unable to perform HELO request to {0}: {1}", m_ServerURI, e.Message);
            }

            // fail
            return string.Empty;
        }
    }
}