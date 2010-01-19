using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

using OpenSim.Services.Interfaces;

using OpenMetaverse;
using log4net;
using Nini.Config;

namespace OpenSim.Services.HypergridService
{
    /// <summary>
    /// This service is for HG1.5 only, to make up for the fact that clients don't
    /// keep any private information in themselves, and that their 'home service'
    /// needs to do it for them.
    /// Once we have better clients, this shouldn't be needed.
    /// </summary>
    public class HomeUsersSecurityService : IHomeUsersSecurityService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        //
        // This is a persistent storage wannabe for dealing with the
        // quirks of HG1.5. We don't really want to store this in a table.
        // But this is the necessary information for securing clients
        // coming home.
        //
        protected static Dictionary<UUID, IPEndPoint> m_ClientEndPoints = new Dictionary<UUID, IPEndPoint>();

        public HomeUsersSecurityService(IConfigSource config)
        {
            m_log.DebugFormat("[HOME USERS SECURITY]: Starting...");
        }

        public void SetEndPoint(UUID sessionID, IPEndPoint ep)
        {
            m_log.DebugFormat("[HOME USERS SECURITY]: Set EndPoint {0} for session {1}", ep.ToString(), sessionID);

            lock (m_ClientEndPoints)
                m_ClientEndPoints[sessionID] = ep;
        }

        public IPEndPoint GetEndPoint(UUID sessionID)
        {
            lock (m_ClientEndPoints)
                if (m_ClientEndPoints.ContainsKey(sessionID))
                {
                    m_log.DebugFormat("[HOME USERS SECURITY]: Get EndPoint {0} for session {1}", m_ClientEndPoints[sessionID].ToString(), sessionID);
                    return m_ClientEndPoints[sessionID];
                }

            return null;
        }

        public void RemoveEndPoint(UUID sessionID)
        {
            m_log.DebugFormat("[HOME USERS SECURITY]: Remove EndPoint for session {0}", sessionID);
            lock (m_ClientEndPoints)
                if (m_ClientEndPoints.ContainsKey(sessionID))
                    m_ClientEndPoints.Remove(sessionID);
        }
    }
}
