using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Hypergrid;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

using OpenMetaverse;

using log4net;

namespace OpenSim.Region.CoreModules.Avatar.Friends
{
    public class HGStatusNotifier
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private HGFriendsModule m_FriendsModule;

        public HGStatusNotifier(HGFriendsModule friendsModule)
        {
            m_FriendsModule = friendsModule;
        }

        public void Notify(UUID userID, Dictionary<string, List<FriendInfo>> friendsPerDomain, bool online)
        {
            if(m_FriendsModule is null)
                return;

            foreach (KeyValuePair<string, List<FriendInfo>> kvp in friendsPerDomain)
            {
                // For the others, call the user agent service
                List<string> ids = new(kvp.Value.Count);
                foreach (FriendInfo f in kvp.Value)
                    ids.Add(f.Friend);

                if (ids.Count == 0)
                    continue; // no one to notify. caller don't do this

                //m_log.DebugFormat("[HG STATUS NOTIFIER]: Notifying {0} friends in {1}", ids.Count, kvp.Key);
                // ASSUMPTION: we assume that all users for one home domain
                // have exactly the same set of service URLs.
                // If this is ever not true, we need to change this.
                if (Util.ParseUniversalUserIdentifier(ids[0], out UUID friendID))
                {
                    string friendsServerURI = m_FriendsModule.UserManagementModule.GetUserServerURL(friendID, "FriendsServerURI");
                    if (!string.IsNullOrEmpty(friendsServerURI))
                    {
                        HGFriendsServicesConnector fConn = new(friendsServerURI);

                        List<UUID> friendsOnline = fConn.StatusNotification(ids, userID, online);
                        if (friendsOnline.Count > 0)
                        {
                            IClientAPI client = m_FriendsModule.LocateClientObject(userID);
                            if(client is not null)
                            {
                                m_FriendsModule.CacheFriendsOnline(userID, friendsOnline, online);
                                if(online)
                                    client?.SendAgentOnline(friendsOnline.ToArray());
                                else
                                    client?.SendAgentOffline(friendsOnline.ToArray());
                            }
                        }
                    }
                }
            }
        }
    }
}
