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

namespace OpenSim.Framework.Communications.Cache
{
    public class AuthedSessionCache
    {
        public class CacheData
        {
            private static readonly DateTime UNIX_EPOCH = new DateTime(1970, 1, 1);
            private string m_session_id;
            private string m_agent_id;
            private int m_expire;

            private int get_current_unix_time()
            {
                return (int)(DateTime.UtcNow - UNIX_EPOCH).TotalSeconds;
            }

            public CacheData(string sid, string aid)
            {
                m_session_id = sid;
                m_agent_id = aid;
                m_expire = get_current_unix_time() + DEFAULT_LIFETIME;
            }

            public CacheData(string sid, string aid, int time_now)
            {
                m_session_id = sid;
                m_agent_id = aid;
                m_expire = time_now + DEFAULT_LIFETIME;
            }

            public string SessionID
            {
                get { return m_session_id; }
                set { m_session_id = value; }
            }

            public string AgentID
            {
                get { return m_agent_id; }
                set { m_agent_id = value; }
            }

            public bool isExpired
            {
                get { return m_expire < get_current_unix_time(); }
            }

            public void Renew()
            {
                m_expire = get_current_unix_time() + DEFAULT_LIFETIME;
            }
        }

        private static readonly int DEFAULT_LIFETIME = 30;
        private Dictionary<string, CacheData> m_authed_sessions = new Dictionary<string,CacheData>();
        // private int m_session_lifetime = DEFAULT_LIFETIME;

        public AuthedSessionCache()
        {
            // m_session_lifetime = DEFAULT_LIFETIME;
        }

        public AuthedSessionCache(int timeout)
        {
            // m_session_lifetime = timeout;
        }

        public CacheData getCachedSession(string session_id, string agent_id)
        {
            CacheData ret = null;
            lock (m_authed_sessions)
            {
                if (m_authed_sessions.ContainsKey(session_id))
                {
                    CacheData cached_session = m_authed_sessions[session_id];
                    if (!cached_session.isExpired && cached_session.AgentID == agent_id)
                    {
                        ret = m_authed_sessions[session_id];
                        // auto renew
                        m_authed_sessions[session_id].Renew();
                    }
                }
            }
            return ret;
        }

        public void Add(string session_id, string agent_id)
        {
            CacheData data = new CacheData(session_id, agent_id);
            lock (m_authed_sessions)
            {
                if (m_authed_sessions.ContainsKey(session_id))
                {
                    m_authed_sessions[session_id] = data;
                }
                else
                {
                    m_authed_sessions.Add(session_id, data);
                }
            }
        }
    }
}
