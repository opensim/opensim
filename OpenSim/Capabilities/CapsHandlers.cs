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
using System.Collections.Generic;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Framework.Capabilities
{
    /// <summary>
    /// CapsHandlers is a cap handler container but also takes
    /// care of adding and removing cap handlers to and from the
    /// supplied BaseHttpServer.
    /// </summary>
    public class CapsHandlers
    {
        private Dictionary<string, IRequestHandler> m_capsHandlers = new Dictionary<string, IRequestHandler>();
        private IHttpServer m_httpListener;
        private string m_httpListenerHostName;
        private uint m_httpListenerPort;
        private bool m_useSSL = false;

        /// <summary></summary>
        /// CapsHandlers is a cap handler container but also takes
        /// care of adding and removing cap handlers to and from the
        /// supplied BaseHttpServer.
        /// </summary>
        /// <param name="httpListener">base HTTP server</param>
        /// <param name="httpListenerHostname">host name of the HTTP server</param>
        /// <param name="httpListenerPort">HTTP port</param>
        public CapsHandlers(IHttpServer httpListener, string httpListenerHostname, uint httpListenerPort)
           {
            m_httpListener = httpListener;
            m_httpListenerHostName = httpListenerHostname;
            m_httpListenerPort = httpListenerPort;
            if (httpListener != null && httpListener.UseSSL)
                m_useSSL = true;
            else
                m_useSSL = false;
        }

        /// <summary>
        /// Remove the cap handler for a capability.
        /// </summary>
        /// <param name="capsName">name of the capability of the cap
        /// handler to be removed</param>
        public void Remove(string capsName)
        {
            lock (m_capsHandlers)
            {
                m_httpListener.RemoveStreamHandler("POST", m_capsHandlers[capsName].Path);
                m_httpListener.RemoveStreamHandler("PUT", m_capsHandlers[capsName].Path);
                m_httpListener.RemoveStreamHandler("GET", m_capsHandlers[capsName].Path);
                m_httpListener.RemoveStreamHandler("DELETE", m_capsHandlers[capsName].Path);
                m_capsHandlers.Remove(capsName);
            }
        }

        public bool ContainsCap(string cap)
        {
            lock (m_capsHandlers)
                return m_capsHandlers.ContainsKey(cap);
        }

        /// <summary>
        /// The indexer allows us to treat the CapsHandlers object
        /// in an intuitive dictionary like way.
        /// </summary>
        /// <remarks>
        /// The indexer will throw an exception when you try to
        /// retrieve a cap handler for a cap that is not contained in
        /// CapsHandlers.
        /// </remarks>
        public IRequestHandler this[string idx]
        {
            get
            {
                lock (m_capsHandlers)
                    return m_capsHandlers[idx];
            }

            set
            {
                lock (m_capsHandlers)
                {
                    if (m_capsHandlers.ContainsKey(idx))
                    {
                        m_httpListener.RemoveStreamHandler("POST", m_capsHandlers[idx].Path);
                        m_httpListener.RemoveStreamHandler("PUT", m_capsHandlers[idx].Path);
                        m_httpListener.RemoveStreamHandler("GET", m_capsHandlers[idx].Path);
                        m_httpListener.RemoveStreamHandler("DELETE", m_capsHandlers[idx].Path);
                        m_capsHandlers.Remove(idx);
                    }

                    if (null == value) return;

                    m_capsHandlers[idx] = value;
                    m_httpListener.AddStreamHandler(value);
                }
            }
        }

        /// <summary>
        /// Return the list of cap names for which this CapsHandlers
        /// object contains cap handlers.
        /// </summary>
        public string[] Caps
        {
            get
            {
                lock (m_capsHandlers)
                {
                    string[] __keys = new string[m_capsHandlers.Keys.Count];
                    m_capsHandlers.Keys.CopyTo(__keys, 0);
                    return __keys;
                }
            }
        }

        /// <summary>
        /// Return an LLSD-serializable Hashtable describing the
        /// capabilities and their handler details.
        /// </summary>
        /// <param name="excludeSeed">If true, then exclude the seed cap.</param>
        public Hashtable GetCapsDetails(bool excludeSeed, List<string> requestedCaps)
        {
            Hashtable caps = new Hashtable();
            string protocol = "http://";

            if (m_useSSL)
                protocol = "https://";

            string baseUrl = protocol + m_httpListenerHostName + ":" + m_httpListenerPort.ToString();

            lock (m_capsHandlers)
            {
                foreach (string capsName in m_capsHandlers.Keys)
                {
                    if (excludeSeed && "SEED" == capsName)
                        continue;

                    if (requestedCaps != null && !requestedCaps.Contains(capsName))
                        continue;

                    caps[capsName] = baseUrl + m_capsHandlers[capsName].Path;
                }
            }

            return caps;
        }

        /// <summary>
        /// Returns a copy of the dictionary of all the HTTP cap handlers
        /// </summary>
        /// <returns>
        /// The dictionary copy.  The key is the capability name, the value is the HTTP handler.
        /// </returns>
        public Dictionary<string, IRequestHandler> GetCapsHandlers()
        {
            lock (m_capsHandlers)
                return new Dictionary<string, IRequestHandler>(m_capsHandlers);
        }
    }
}