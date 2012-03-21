using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Hypergrid;
using FriendInfo = OpenSim.Services.Interfaces.FriendInfo;

using OpenMetaverse;

namespace OpenSim.Region.CoreModules.Avatar.Friends
{
    public class HGStatusNotifier
    {
        private HGFriendsModule m_FriendsModule;

        public HGStatusNotifier(HGFriendsModule friendsModule)
        {
            m_FriendsModule = friendsModule;
        }

        public void Notify(UUID userID, Dictionary<string, List<FriendInfo>> friendsPerDomain, bool online)
        {
            foreach (KeyValuePair<string, List<FriendInfo>> kvp in friendsPerDomain)
            {
                if (kvp.Key != "local")
                {
                    // For the others, call the user agent service 
                    List<string> ids = new List<string>();
                    foreach (FriendInfo f in kvp.Value)
                        ids.Add(f.Friend);
                    UserAgentServiceConnector uConn = new UserAgentServiceConnector(kvp.Key);
                    List<UUID> friendsOnline = uConn.StatusNotification(ids, userID, online);

                    if (online && friendsOnline.Count > 0)
                    {
                        IClientAPI client = m_FriendsModule.LocateClientObject(userID);
                        if (client != null)
                            client.SendAgentOnline(friendsOnline.ToArray());
                    }
                }
            }
        }
    }
}
