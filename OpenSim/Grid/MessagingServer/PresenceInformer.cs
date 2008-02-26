using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using libsecondlife;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Data;
using OpenSim.Framework.Servers;

namespace OpenSim.Grid.MessagingServer
{
    public class PresenceInformer
    {
        public UserPresenceData presence1 = null;
        public UserPresenceData presence2 = null;
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public PresenceInformer()
        {

        }
        public void go(object o)
        {
            if (presence1 != null && presence2 != null)
            {
                SendRegionPresenceUpdate(presence1, presence2);
            }

        }

        /// <summary>
        /// Informs a region about an Agent
        /// </summary>
        /// <param name="TalkingAbout">User to talk about</param>
        /// <param name="UserToUpdate">User we're sending this too (contains the region)</param>
        public void SendRegionPresenceUpdate(UserPresenceData TalkingAbout, UserPresenceData UserToUpdate)
        {
            // TODO: Fill in pertenant Presence Data from 'TalkingAbout'

            RegionProfileData whichRegion = UserToUpdate.regionData;
            //whichRegion.httpServerURI

            Hashtable PresenceParams = new Hashtable();
            ArrayList SendParams = new ArrayList();
            SendParams.Add(PresenceParams);

            m_log.Info("[PRESENCE]: Informing " + whichRegion.regionName + " at " + whichRegion.httpServerURI);
            // Send
            XmlRpcRequest RegionReq = new XmlRpcRequest("presence_update", SendParams);
            XmlRpcResponse RegionResp = RegionReq.Send(whichRegion.httpServerURI, 6000);
        }


    }
}
