using System;
using System.Collections.Generic;
using System.Text;

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
        private int m_session_lifetime = DEFAULT_LIFETIME;

        public AuthedSessionCache()
        {
            m_session_lifetime = DEFAULT_LIFETIME;
        }

        public AuthedSessionCache(int timeout)
        {
            m_session_lifetime = timeout;
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
