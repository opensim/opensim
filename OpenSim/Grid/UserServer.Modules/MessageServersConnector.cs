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
using System.Net;
using System.Reflection;
using System.Threading;
using log4net;
using Nwc.XmlRpc;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Grid.Framework;

namespace OpenSim.Grid.UserServer.Modules
{
    public enum NotificationRequest : int
    {
        Login = 0,
        Logout = 1,
        Shutdown = 2
    }

    public struct PresenceNotification
    {
        public NotificationRequest request;
        public UUID agentID;
        public UUID sessionID;
        public UUID RegionID;
        public ulong regionhandle;
        public float positionX;
        public float positionY;
        public float positionZ;
        public string firstname;
        public string lastname;
    }

    public delegate void AgentLocationDelegate(UUID agentID, UUID regionID, ulong regionHandle);
    public delegate void AgentLeavingDelegate(UUID agentID, UUID regionID, ulong regionHandle);
    public delegate void RegionStartupDelegate(UUID regionID);
    public delegate void RegionShutdownDelegate(UUID regionID);


    public class MessageServersConnector
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public Dictionary<string, MessageServerInfo> MessageServers;

        private BaseHttpServer m_httpServer;

        private OpenSim.Framework.BlockingQueue<PresenceNotification> m_NotifyQueue =
                new OpenSim.Framework.BlockingQueue<PresenceNotification>();

        private IGridServiceCore m_core;

        public event AgentLocationDelegate OnAgentLocation;
        public event AgentLeavingDelegate OnAgentLeaving;
        public event RegionStartupDelegate OnRegionStartup;
        public event RegionShutdownDelegate OnRegionShutdown;

        public MessageServersConnector()
        {
            MessageServers = new Dictionary<string, MessageServerInfo>();
        }

        public void Initialise(IGridServiceCore core)
        {
            m_core = core;
            m_core.RegisterInterface<MessageServersConnector>(this);

            Watchdog.StartThread(NotifyQueueRunner, "NotifyQueueRunner", ThreadPriority.Normal, true);
        }

        public void PostInitialise()
        {
        }

        public void RegisterHandlers(BaseHttpServer httpServer)
        {
            m_httpServer = httpServer;

            m_httpServer.AddXmlRPCHandler("region_startup", RegionStartup);
            m_httpServer.AddXmlRPCHandler("region_shutdown", RegionShutdown);
            m_httpServer.AddXmlRPCHandler("agent_location", AgentLocation);
            m_httpServer.AddXmlRPCHandler("agent_leaving", AgentLeaving);
            // Message Server ---> User Server
            m_httpServer.AddXmlRPCHandler("register_messageserver", XmlRPCRegisterMessageServer);
            m_httpServer.AddXmlRPCHandler("agent_change_region", XmlRPCUserMovedtoRegion);
            m_httpServer.AddXmlRPCHandler("deregister_messageserver", XmlRPCDeRegisterMessageServer);
        }

        public void RegisterMessageServer(string URI, MessageServerInfo serverData)
        {
            lock (MessageServers)
            {
                if (!MessageServers.ContainsKey(URI))
                    MessageServers.Add(URI, serverData);
            }
        }

        public void DeRegisterMessageServer(string URI)
        {
            lock (MessageServers)
            {
                if (MessageServers.ContainsKey(URI))
                    MessageServers.Remove(URI);
            }
        }

        public void AddResponsibleRegion(string URI, ulong regionhandle)
        {
            if (!MessageServers.ContainsKey(URI))
            {
                m_log.Warn("[MSGSERVER]: Got addResponsibleRegion Request for a MessageServer that isn't registered");
            }
            else
            {
                MessageServerInfo msginfo = MessageServers["URI"];
                msginfo.responsibleForRegions.Add(regionhandle);
                MessageServers["URI"] = msginfo;
            }
        }
        public void RemoveResponsibleRegion(string URI, ulong regionhandle)
        {
            if (!MessageServers.ContainsKey(URI))
            {
                m_log.Warn("[MSGSERVER]: Got RemoveResponsibleRegion Request for a MessageServer that isn't registered");
            }
            else
            {
                MessageServerInfo msginfo = MessageServers["URI"];
                if (msginfo.responsibleForRegions.Contains(regionhandle))
                {
                    msginfo.responsibleForRegions.Remove(regionhandle);
                    MessageServers["URI"] = msginfo;
                }
            }

        }
        public XmlRpcResponse XmlRPCRegisterMessageServer(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            if (requestData.Contains("uri"))
            {
                string URI = (string)requestData["uri"];
                string sendkey=(string)requestData["sendkey"];
                string recvkey=(string)requestData["recvkey"];
                MessageServerInfo m = new MessageServerInfo();
                m.URI = URI;
                m.sendkey = sendkey;
                m.recvkey = recvkey;
                RegisterMessageServer(URI, m);
                responseData["responsestring"] = "TRUE";
                response.Value = responseData;
            }
            return response;
        }
        public XmlRpcResponse XmlRPCDeRegisterMessageServer(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            if (requestData.Contains("uri"))
            {
                string URI = (string)requestData["uri"];

                DeRegisterMessageServer(URI);
                responseData["responsestring"] = "TRUE";
                response.Value = responseData;
            }
            return response;
        }
        public XmlRpcResponse XmlRPCUserMovedtoRegion(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            XmlRpcResponse response = new XmlRpcResponse();
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable responseData = new Hashtable();

            if (requestData.Contains("fromuri"))
            {
                // string sURI = (string)requestData["fromuri"];
                // string sagentID = (string)requestData["agentid"];
                // string ssessionID = (string)requestData["sessionid"];
                // string scurrentRegionID = (string)requestData["regionid"];
                // string sregionhandle = (string)requestData["regionhandle"];
                // string scurrentpos = (string)requestData["currentpos"];
                //Vector3.TryParse((string)reader["currentPos"], out retval.currentPos);
                // TODO: Okay now raise event so the user server can pass this data to the Usermanager

                responseData["responsestring"] = "TRUE";
                response.Value = responseData;
            }
            return response;
        }

        public void TellMessageServersAboutUser(UUID agentID, UUID sessionID, UUID RegionID,
                                                ulong regionhandle, float positionX, float positionY,
                                                float positionZ, string firstname, string lastname)
        {
            PresenceNotification notification = new PresenceNotification();

            notification.request = NotificationRequest.Login;
            notification.agentID = agentID;
            notification.sessionID = sessionID;
            notification.RegionID = RegionID;
            notification.regionhandle = regionhandle;
            notification.positionX = positionX;
            notification.positionY = positionY;
            notification.positionZ = positionZ;
            notification.firstname = firstname;
            notification.lastname = lastname;

            m_NotifyQueue.Enqueue(notification);
        }

        private void TellMessageServersAboutUserInternal(UUID agentID, UUID sessionID, UUID RegionID,
                                                ulong regionhandle, float positionX, float positionY,
                                                float positionZ, string firstname, string lastname)
        {
            // Loop over registered Message Servers (AND THERE WILL BE MORE THEN ONE :D)
            lock (MessageServers)
            {
                if (MessageServers.Count > 0)
                {
                    m_log.Info("[MSGCONNECTOR]: Sending login notice to registered message servers");
                }
//                else
//                {
//                    m_log.Debug("[MSGCONNECTOR]: No Message Servers registered, ignoring");
//                }
                foreach (MessageServerInfo serv in MessageServers.Values)
                {
                    NotifyMessageServerAboutUser(serv, agentID, sessionID, RegionID,
                                                regionhandle, positionX, positionY, positionZ,
                                                firstname, lastname);
                }
            }
        }

        private void TellMessageServersAboutUserLogoffInternal(UUID agentID)
        {
            lock (MessageServers)
            {
                if (MessageServers.Count > 0)
                {
                    m_log.Info("[MSGCONNECTOR]: Sending logoff notice to registered message servers");
                }
                else
                {
//                    m_log.Debug("[MSGCONNECTOR]: No Message Servers registered, ignoring");
                }
                foreach (MessageServerInfo serv in MessageServers.Values)
                {
                    NotifyMessageServerAboutUserLogoff(serv,agentID);
                }
            }
        }

        private void TellMessageServersAboutRegionShutdownInternal(UUID regionID)
        {
            lock (MessageServers)
            {
                if (MessageServers.Count > 0)
                {
                    m_log.Info("[MSGCONNECTOR]: Sending region down notice to registered message servers");
                }
                else
                {
//                    m_log.Debug("[MSGCONNECTOR]: No Message Servers registered, ignoring");
                }
                foreach (MessageServerInfo serv in MessageServers.Values)
                {
                    NotifyMessageServerAboutRegionShutdown(serv,regionID);
                }
            }
        }

        public void TellMessageServersAboutUserLogoff(UUID agentID)
        {
            PresenceNotification notification = new PresenceNotification();

            notification.request = NotificationRequest.Logout;
            notification.agentID = agentID;

            m_NotifyQueue.Enqueue(notification);
        }

        public void TellMessageServersAboutRegionShutdown(UUID regionID)
        {
            PresenceNotification notification = new PresenceNotification();

            notification.request = NotificationRequest.Shutdown;
            notification.RegionID = regionID;

            m_NotifyQueue.Enqueue(notification);
        }

        private void NotifyMessageServerAboutUserLogoff(MessageServerInfo serv, UUID agentID)
        {
            Hashtable reqparams = new Hashtable();
            reqparams["sendkey"] = serv.sendkey;
            reqparams["agentid"] = agentID.ToString();
            ArrayList SendParams = new ArrayList();
            SendParams.Add(reqparams);

            XmlRpcRequest GridReq = new XmlRpcRequest("logout_of_simulator", SendParams);
            try
            {
                GridReq.Send(serv.URI, 6000);
            }
            catch (WebException)
            {
                m_log.Warn("[MSGCONNECTOR]: Unable to notify Message Server about log out.  Other users might still think this user is online");
            }
            m_log.Info("[LOGOUT]: Notified : " + serv.URI + " about user logout");
        }

        private void NotifyMessageServerAboutRegionShutdown(MessageServerInfo serv, UUID regionID)
        {
            Hashtable reqparams = new Hashtable();
            reqparams["sendkey"] = serv.sendkey;
            reqparams["regionid"] = regionID.ToString();
            ArrayList SendParams = new ArrayList();
            SendParams.Add(reqparams);

            XmlRpcRequest GridReq = new XmlRpcRequest("process_region_shutdown", SendParams);
            try
            {
                GridReq.Send(serv.URI, 6000);
            }
            catch (WebException)
            {
                m_log.Warn("[MSGCONNECTOR]: Unable to notify Message Server about region shutdown.");
            }
            m_log.Info("[REGION UPDOWN]: Notified : " + serv.URI + " about region state change");
        }

        private void NotifyMessageServerAboutUser(MessageServerInfo serv,
                                                    UUID agentID, UUID sessionID, UUID RegionID,
                                                    ulong regionhandle, float positionX, float positionY, float positionZ,
                                                    string firstname, string lastname)
        {
            Hashtable reqparams = new Hashtable();
            reqparams["sendkey"] = serv.sendkey;
            reqparams["agentid"] = agentID.ToString();
            reqparams["sessionid"] = sessionID.ToString();
            reqparams["regionid"] = RegionID.ToString();
            reqparams["regionhandle"] = regionhandle.ToString();
            reqparams["positionx"] = positionX.ToString();
            reqparams["positiony"] = positionY.ToString();
            reqparams["positionz"] = positionZ.ToString();
            reqparams["firstname"] = firstname;
            reqparams["lastname"] = lastname;

            //reqparams["position"] = Position.ToString();

            ArrayList SendParams = new ArrayList();
            SendParams.Add(reqparams);

            XmlRpcRequest GridReq = new XmlRpcRequest("login_to_simulator", SendParams);
            try
            {
                GridReq.Send(serv.URI, 6000);
                m_log.Info("[LOGIN]: Notified : " + serv.URI + " about user login");
            }
            catch (WebException)
            {
                m_log.Warn("[MSGCONNECTOR]: Unable to notify Message Server about login.  Presence might be borked for this user");
            }

        }

        private void NotifyQueueRunner()
        {
            while (true)
            {
                PresenceNotification presence = m_NotifyQueue.Dequeue();

                if (presence.request == NotificationRequest.Shutdown)
                {
                    TellMessageServersAboutRegionShutdownInternal(presence.RegionID);
                }

                if (presence.request == NotificationRequest.Login)
                {
                    TellMessageServersAboutUserInternal(presence.agentID,
                            presence.sessionID, presence.RegionID,
                            presence.regionhandle, presence.positionX,
                            presence.positionY, presence.positionZ,
                            presence.firstname, presence.lastname);
                }

                if (presence.request == NotificationRequest.Logout)
                {
                    TellMessageServersAboutUserLogoffInternal(presence.agentID);
                }

                Watchdog.UpdateThread();
            }
        }

        public XmlRpcResponse RegionStartup(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable result = new Hashtable();

            UUID regionID;
            if (UUID.TryParse((string)requestData["RegionUUID"], out regionID))
            {
                if (OnRegionStartup != null)
                    OnRegionStartup(regionID);

                result["responsestring"] = "TRUE";
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = result;
            return response;
        }

        public XmlRpcResponse RegionShutdown(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable result = new Hashtable();

            UUID regionID;
            if (UUID.TryParse((string)requestData["RegionUUID"], out regionID))
            {
                if (OnRegionShutdown != null)
                    OnRegionShutdown(regionID);

                result["responsestring"] = "TRUE";
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = result;
            return response;
        }

        public XmlRpcResponse AgentLocation(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable result = new Hashtable();

            UUID agentID;
            UUID regionID;
            ulong regionHandle;
            if (UUID.TryParse((string)requestData["AgentID"], out agentID) && UUID.TryParse((string)requestData["RegionUUID"], out regionID) && ulong.TryParse((string)requestData["RegionHandle"], out regionHandle))
            {
                if (OnAgentLocation != null)
                    OnAgentLocation(agentID, regionID, regionHandle);

                result["responsestring"] = "TRUE";
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = result;
            return response;
        }

        public XmlRpcResponse AgentLeaving(XmlRpcRequest request, IPEndPoint remoteClient)
        {
            Hashtable requestData = (Hashtable)request.Params[0];
            Hashtable result = new Hashtable();

            UUID agentID;
            UUID regionID;
            ulong regionHandle;
            if (UUID.TryParse((string)requestData["AgentID"], out agentID) && UUID.TryParse((string)requestData["RegionUUID"], out regionID) && ulong.TryParse((string)requestData["RegionHandle"], out regionHandle))
            {
                if (OnAgentLeaving != null)
                    OnAgentLeaving(agentID, regionID, regionHandle);

                result["responsestring"] = "TRUE";
            }

            XmlRpcResponse response = new XmlRpcResponse();
            response.Value = result;
            return response;
        }
    }
}
