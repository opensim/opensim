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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenSim.Services.Connectors.Simulation;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nwc.XmlRpc;
using Nini.Config;

namespace OpenSim.Services.Connectors.Hypergrid
{
    public class UserAgentServiceConnector : SimulationServiceConnector, IUserAgentService
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURLHost;
        private string m_ServerURL;
        private GridRegion m_Gatekeeper;

        public UserAgentServiceConnector(string url) : this(url, true)
        {
        }

        public UserAgentServiceConnector(string url, bool dnsLookup)
        {
            m_ServerURL = m_ServerURLHost = url;

            if (dnsLookup)
            {
                // Doing this here, because XML-RPC or mono have some strong ideas about
                // caching DNS translations.
                try
                {
                    Uri m_Uri = new Uri(m_ServerURL);
                    IPAddress ip = Util.GetHostFromDNS(m_Uri.Host);
                    m_ServerURL = m_ServerURL.Replace(m_Uri.Host, ip.ToString());
                    if (!m_ServerURL.EndsWith("/"))
                        m_ServerURL += "/";
                }
                catch (Exception e)
                {
                    m_log.DebugFormat("[USER AGENT CONNECTOR]: Malformed Uri {0}: {1}", url, e.Message);
                }
            }

            //m_log.DebugFormat("[USER AGENT CONNECTOR]: new connector to {0} ({1})", url, m_ServerURL);
        }

        public UserAgentServiceConnector(IConfigSource config)
        {
            IConfig serviceConfig = config.Configs["UserAgentService"];
            if (serviceConfig == null)
            {
                m_log.Error("[USER AGENT CONNECTOR]: UserAgentService missing from ini");
                throw new Exception("UserAgent connector init error");
            }

            string serviceURI = serviceConfig.GetString("UserAgentServerURI",
                    String.Empty);

            if (serviceURI == String.Empty)
            {
                m_log.Error("[USER AGENT CONNECTOR]: No Server URI named in section UserAgentService");
                throw new Exception("UserAgent connector init error");
            }

            m_ServerURL = m_ServerURLHost = serviceURI;
            if (!m_ServerURL.EndsWith("/"))
                m_ServerURL += "/";

            //m_log.DebugFormat("[USER AGENT CONNECTOR]: new connector to {0}", m_ServerURL);
        }

        protected override string AgentPath()
        {
            return "homeagent/";
        }

        // The Login service calls this interface with fromLogin=true
        // Sims call it with fromLogin=false
        // Either way, this is verified by the handler
        public bool LoginAgentToGrid(GridRegion source, AgentCircuitData aCircuit, GridRegion gatekeeper, GridRegion destination, bool fromLogin, out string reason)
        {
            reason = String.Empty;

            if (destination == null)
            {
                reason = "Destination is null";
                m_log.Debug("[USER AGENT CONNECTOR]: Given destination is null");
                return false;
            }

            GridRegion home = new GridRegion();
            home.ServerURI = m_ServerURL;
            home.RegionID = destination.RegionID;
            home.RegionLocX = destination.RegionLocX;
            home.RegionLocY = destination.RegionLocY;

            m_Gatekeeper = gatekeeper;

            Console.WriteLine("   >>> LoginAgentToGrid <<< " + home.ServerURI);

            uint flags = fromLogin ? (uint)TeleportFlags.ViaLogin : (uint)TeleportFlags.ViaHome;
            EntityTransferContext ctx = new EntityTransferContext();
            return CreateAgent(source, home, aCircuit, flags, ctx, out reason);
        }


        // The simulators call this interface
        public bool LoginAgentToGrid(GridRegion source, AgentCircuitData aCircuit, GridRegion gatekeeper, GridRegion destination, out string reason)
        {
            return LoginAgentToGrid(source, aCircuit, gatekeeper, destination, false, out reason);
        }

        protected override void PackData(OSDMap args, GridRegion source, AgentCircuitData aCircuit, GridRegion destination, uint flags)
        {
            base.PackData(args, source, aCircuit, destination, flags);
            args["gatekeeper_serveruri"] = OSD.FromString(m_Gatekeeper.ServerURI);
            args["gatekeeper_host"] = OSD.FromString(m_Gatekeeper.ExternalHostName);
            args["gatekeeper_port"] = OSD.FromString(m_Gatekeeper.HttpPort.ToString());
            args["destination_serveruri"] = OSD.FromString(destination.ServerURI);
        }

        public void SetClientToken(UUID sessionID, string token)
        {
            // no-op
        }

        private Hashtable CallServer(string methodName, Hashtable hash)
        {
            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest(methodName, paramList);

            // Send and get reply
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(m_ServerURL, 10000);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[USER AGENT CONNECTOR]: {0} call to {1} failed: {2}", methodName, m_ServerURLHost, e.Message);
                throw;
            }

            if (response.IsFault)
            {
                throw new Exception(string.Format("[USER AGENT CONNECTOR]: {0} call to {1} returned an error: {2}", methodName, m_ServerURLHost, response.FaultString));
            }

            hash = (Hashtable)response.Value;

            if (hash == null)
            {
                throw new Exception(string.Format("[USER AGENT CONNECTOR]: {0} call to {1} returned null", methodName, m_ServerURLHost));
            }

            return hash;
        }

        public GridRegion GetHomeRegion(UUID userID, out Vector3 position, out Vector3 lookAt)
        {
            position = Vector3.UnitY; lookAt = Vector3.UnitY;

            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();

            hash = CallServer("get_home_region", hash);

            bool success;
            if (!Boolean.TryParse((string)hash["result"], out success) || !success)
                return null;

            GridRegion region = new GridRegion();

            UUID.TryParse((string)hash["uuid"], out region.RegionID);
            //m_log.Debug(">> HERE, uuid: " + region.RegionID);
            int n = 0;
            if (hash["x"] != null)
            {
                Int32.TryParse((string)hash["x"], out n);
                region.RegionLocX = n;
                //m_log.Debug(">> HERE, x: " + region.RegionLocX);
            }
            if (hash["y"] != null)
            {
                Int32.TryParse((string)hash["y"], out n);
                region.RegionLocY = n;
                //m_log.Debug(">> HERE, y: " + region.RegionLocY);
            }
            if (hash["size_x"] != null)
            {
                Int32.TryParse((string)hash["size_x"], out n);
                region.RegionSizeX = n;
                //m_log.Debug(">> HERE, x: " + region.RegionLocX);
            }
            if (hash["size_y"] != null)
            {
                Int32.TryParse((string)hash["size_y"], out n);
                region.RegionSizeY = n;
                //m_log.Debug(">> HERE, y: " + region.RegionLocY);
            }
            if (hash["region_name"] != null)
            {
                region.RegionName = (string)hash["region_name"];
                //m_log.Debug(">> HERE, name: " + region.RegionName);
            }
            if (hash["hostname"] != null)
                region.ExternalHostName = (string)hash["hostname"];
            if (hash["http_port"] != null)
            {
                uint p = 0;
                UInt32.TryParse((string)hash["http_port"], out p);
                region.HttpPort = p;
            }
            if (hash.ContainsKey("server_uri") && hash["server_uri"] != null)
                region.ServerURI = (string)hash["server_uri"];

            if (hash["internal_port"] != null)
            {
                int p = 0;
                Int32.TryParse((string)hash["internal_port"], out p);
                region.InternalEndPoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), p);
            }
            if (hash["position"] != null)
                Vector3.TryParse((string)hash["position"], out position);
            if (hash["lookAt"] != null)
                Vector3.TryParse((string)hash["lookAt"], out lookAt);

            // Successful return
            return region;
        }

        public bool IsAgentComingHome(UUID sessionID, string thisGridExternalName)
        {
            Hashtable hash = new Hashtable();
            hash["sessionID"] = sessionID.ToString();
            hash["externalName"] = thisGridExternalName;

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("agent_is_coming_home", paramList);
            string reason = string.Empty;
            return GetBoolResponse(request, out reason);
        }

        public bool VerifyAgent(UUID sessionID, string token)
        {
            Hashtable hash = new Hashtable();
            hash["sessionID"] = sessionID.ToString();
            hash["token"] = token;

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("verify_agent", paramList);
            string reason = string.Empty;
            return GetBoolResponse(request, out reason);
        }

        public bool VerifyClient(UUID sessionID, string token)
        {
            Hashtable hash = new Hashtable();
            hash["sessionID"] = sessionID.ToString();
            hash["token"] = token;

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("verify_client", paramList);
            string reason = string.Empty;
            return GetBoolResponse(request, out reason);
        }

        public void LogoutAgent(UUID userID, UUID sessionID)
        {
            Hashtable hash = new Hashtable();
            hash["sessionID"] = sessionID.ToString();
            hash["userID"] = userID.ToString();

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("logout_agent", paramList);
            string reason = string.Empty;
            GetBoolResponse(request, out reason);
        }

        [Obsolete]
        public List<UUID> StatusNotification(List<string> friends, UUID userID, bool online)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();
            hash["online"] = online.ToString();
            int i = 0;
            foreach (string s in friends)
            {
                hash["friend_" + i.ToString()] = s;
                i++;
            }

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("status_notification", paramList);
//            string reason = string.Empty;

            // Send and get reply
            List<UUID> friendsOnline = new List<UUID>();
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(m_ServerURL, 6000);
            }
            catch
            {
                m_log.DebugFormat("[USER AGENT CONNECTOR]: Unable to contact remote server {0} for StatusNotification", m_ServerURLHost);
//                reason = "Exception: " + e.Message;
                return friendsOnline;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: remote call to {0} for StatusNotification returned an error: {1}", m_ServerURLHost, response.FaultString);
//                reason = "XMLRPC Fault";
                return friendsOnline;
            }

            hash = (Hashtable)response.Value;
            //foreach (Object o in hash)
            //    m_log.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
            try
            {
                if (hash == null)
                {
                    m_log.ErrorFormat("[USER AGENT CONNECTOR]: GetOnlineFriends Got null response from {0}! THIS IS BAAAAD", m_ServerURLHost);
//                    reason = "Internal error 1";
                    return friendsOnline;
                }

                // Here is the actual response
                foreach (object key in hash.Keys)
                {
                    if (key is string && ((string)key).StartsWith("friend_") && hash[key] != null)
                    {
                        UUID uuid;
                        if (UUID.TryParse(hash[key].ToString(), out uuid))
                            friendsOnline.Add(uuid);
                    }
                }

            }
            catch
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: Got exception on GetOnlineFriends response.");
//                reason = "Exception: " + e.Message;
            }

            return friendsOnline;
        }

        [Obsolete]
        public List<UUID> GetOnlineFriends(UUID userID, List<string> friends)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();
            int i = 0;
            foreach (string s in friends)
            {
                hash["friend_" + i.ToString()] = s;
                i++;
            }

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("get_online_friends", paramList);
//            string reason = string.Empty;

            // Send and get reply
            List<UUID> online = new List<UUID>();
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(m_ServerURL, 10000);
            }
            catch
            {
                m_log.DebugFormat("[USER AGENT CONNECTOR]: Unable to contact remote server {0} for GetOnlineFriends", m_ServerURLHost);
//                reason = "Exception: " + e.Message;
                return online;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: remote call to {0} for GetOnlineFriends returned an error: {1}", m_ServerURLHost, response.FaultString);
//                reason = "XMLRPC Fault";
                return online;
            }

            hash = (Hashtable)response.Value;
            //foreach (Object o in hash)
            //    m_log.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
            try
            {
                if (hash == null)
                {
                    m_log.ErrorFormat("[USER AGENT CONNECTOR]: GetOnlineFriends Got null response from {0}! THIS IS BAAAAD", m_ServerURLHost);
//                    reason = "Internal error 1";
                    return online;
                }

                // Here is the actual response
                foreach (object key in hash.Keys)
                {
                    if (key is string && ((string)key).StartsWith("friend_") && hash[key] != null)
                    {
                        UUID uuid;
                        if (UUID.TryParse(hash[key].ToString(), out uuid))
                            online.Add(uuid);
                    }
                }

            }
            catch
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: Got exception on GetOnlineFriends response.");
//                reason = "Exception: " + e.Message;
            }

            return online;
        }

        public Dictionary<string,object> GetUserInfo (UUID userID)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();

            hash = CallServer("get_user_info", hash);

            Dictionary<string, object> info = new Dictionary<string, object>();

            foreach (object key in hash.Keys)
            {
                if (hash[key] != null)
                {
                    info.Add(key.ToString(), hash[key]);
                }
            }

            return info;
        }

        public Dictionary<string, object> GetServerURLs(UUID userID)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();

            hash = CallServer("get_server_urls", hash);

            Dictionary<string, object> serverURLs = new Dictionary<string, object>();
            foreach (object key in hash.Keys)
            {
                if (key is string && ((string)key).StartsWith("SRV_") && hash[key] != null)
                {
                    string serverType = key.ToString().Substring(4); // remove "SRV_"
                    serverURLs.Add(serverType, hash[key].ToString());
                }
            }

            return serverURLs;
        }

        public string LocateUser(UUID userID)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();

            hash = CallServer("locate_user", hash);

            string url = string.Empty;

            // Here's the actual response
            if (hash.ContainsKey("URL"))
                url = hash["URL"].ToString();

            return url;
        }

        public string GetUUI(UUID userID, UUID targetUserID)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();
            hash["targetUserID"] = targetUserID.ToString();

            hash = CallServer("get_uui", hash);

            string uui = string.Empty;

            // Here's the actual response
            if (hash.ContainsKey("UUI"))
                uui = hash["UUI"].ToString();

            return uui;
        }

        public UUID GetUUID(String first, String last)
        {
            Hashtable hash = new Hashtable();
            hash["first"] = first;
            hash["last"] = last;

            hash = CallServer("get_uuid", hash);

            if (!hash.ContainsKey("UUID"))
            {
                throw new Exception(string.Format("[USER AGENT CONNECTOR]: get_uuid call to {0} didn't return a UUID", m_ServerURLHost));
            }

            UUID uuid;
            if (!UUID.TryParse(hash["UUID"].ToString(), out uuid))
            {
                throw new Exception(string.Format("[USER AGENT CONNECTOR]: get_uuid call to {0} returned an invalid UUID: {1}", m_ServerURLHost, hash["UUID"].ToString()));
            }

            return uuid;
        }

        private bool GetBoolResponse(XmlRpcRequest request, out string reason)
        {
            //m_log.Debug("[USER AGENT CONNECTOR]: GetBoolResponse from/to " + m_ServerURLHost);
            XmlRpcResponse response = null;
            try
            {
                // We can not use m_ServerURL here anymore because it causes
                // the HTTP request to be built without a host name. This messes
                // with OSGrid's NGINX and can make OSGrid avatars unable to TP
                // to other grids running recent mono.
                response = request.Send(m_ServerURLHost, 10000);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[USER AGENT CONNECTOR]: Unable to contact remote server {0} for GetBoolResponse", m_ServerURLHost);
                reason = "Exception: " + e.Message;
                return false;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: remote call to {0} for GetBoolResponse returned an error: {1}", m_ServerURLHost, response.FaultString);
                reason = "XMLRPC Fault";
                return false;
            }

            Hashtable hash = (Hashtable)response.Value;
            //foreach (Object o in hash)
            //    m_log.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
            try
            {
                if (hash == null)
                {
                    m_log.ErrorFormat("[USER AGENT CONNECTOR]: Got null response from {0}! THIS IS BAAAAD", m_ServerURLHost);
                    reason = "Internal error 1";
                    return false;
                }
                bool success = false;
                reason = string.Empty;
                if (hash.ContainsKey("result"))
                    Boolean.TryParse((string)hash["result"], out success);
                else
                {
                    reason = "Internal error 2";
                    m_log.WarnFormat("[USER AGENT CONNECTOR]: response from {0} does not have expected key 'result'", m_ServerURLHost);
                }

                return success;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: Got exception on GetBoolResponse response.");
                if (hash.ContainsKey("result") && hash["result"] != null)
                    m_log.ErrorFormat("Reply was ", (string)hash["result"]);
                reason = "Exception: " + e.Message;
                return false;
            }

        }

    }
}
