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
using System.Net;
using System.Reflection;
using log4net;
using Nwc.XmlRpc;
using OpenSim.Data;

namespace OpenSim.Grid.MessagingServer.Modules
{
    public delegate RegionProfileData GetRegionData(ulong region_handle);
    public delegate void Done(PresenceInformer obj);


    public class PresenceInformer
    {
        public event GetRegionData OnGetRegionData;
        public event Done OnDone;

        private GetRegionData handlerGetRegionData = null;
        private Done handlerDone = null;

        public UserPresenceData presence1 = null;
        public UserPresenceData presence2 = null;
        public string gridserverurl, gridserversendkey, gridserverrecvkey;
        public bool lookupRegion = true;
        //public methodGroup

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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
            RegionProfileData whichRegion = new RegionProfileData();
            if (lookupRegion)
            {
                handlerGetRegionData = OnGetRegionData;
                if (handlerGetRegionData != null)
                {
                    whichRegion = handlerGetRegionData(UserToUpdate.regionData.regionHandle);
                }
                //RegionProfileData rp = RegionProfileData.RequestSimProfileData(UserToUpdate.regionData.regionHandle, gridserverurl, gridserversendkey, gridserverrecvkey);

                //whichRegion = rp;
            }
            else
            {
                whichRegion = UserToUpdate.regionData;
            }
            //whichRegion.httpServerURI

            if (whichRegion != null)
            {
                Hashtable PresenceParams = new Hashtable();
                PresenceParams.Add("agent_id",TalkingAbout.agentData.AgentID.ToString());
                PresenceParams.Add("notify_id",UserToUpdate.agentData.AgentID.ToString());
                if (TalkingAbout.OnlineYN)
                    PresenceParams.Add("status","TRUE");
                else
                    PresenceParams.Add("status","FALSE");

                ArrayList SendParams = new ArrayList();
                SendParams.Add(PresenceParams);

                m_log.InfoFormat("[PRESENCE]: Informing {0}@{1} at {2} about {3}", TalkingAbout.agentData.firstname + " " + TalkingAbout.agentData.lastname, whichRegion.regionName, whichRegion.httpServerURI, UserToUpdate.agentData.firstname + " " + UserToUpdate.agentData.lastname);
                // Send
                XmlRpcRequest RegionReq = new XmlRpcRequest("presence_update", SendParams);
                try
                {
                    // XmlRpcResponse RegionResp = RegionReq.Send(whichRegion.httpServerURI, 6000);
                    RegionReq.Send(whichRegion.httpServerURI, 6000);
                }
                catch (WebException)
                {
                    m_log.WarnFormat("[INFORM]: failed notifying region {0} containing user {1} about {2}", whichRegion.regionName, UserToUpdate.agentData.firstname + " " + UserToUpdate.agentData.lastname, TalkingAbout.agentData.firstname + " " + TalkingAbout.agentData.lastname);
                }
            }
            else
            {
                m_log.Info("[PRESENCEUPDATER]: Region data was null skipping");

            }

            handlerDone = OnDone;
            if (handlerDone != null)
            {
                handlerDone(this);
            }
        }
    }
}
