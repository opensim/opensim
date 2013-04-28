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
    public class UserAgentServiceConnector : IUserAgentService
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(
            MethodBase.GetCurrentMethod().DeclaringType);

        string m_ServerURL;

        public UserAgentServiceConnector(string url) : this(url, true)
        {
        }

        public UserAgentServiceConnector(string url, bool dnsLookup)
        {
            m_ServerURL = url;

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
                    m_log.DebugFormat("[USER AGENT CONNECTOR]: Malformed Uri {0}: {1}", m_ServerURL, e.Message);
                }
            }
            m_log.DebugFormat("[USER AGENT CONNECTOR]: new connector to {0} ({1})", url, m_ServerURL);
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
            m_ServerURL = serviceURI;

            m_log.DebugFormat("[USER AGENT CONNECTOR]: UserAgentServiceConnector started for {0}", m_ServerURL);
        }


        // The Login service calls this interface with a non-null [client] ipaddress 
        public bool LoginAgentToGrid(AgentCircuitData aCircuit, GridRegion gatekeeper, GridRegion destination, IPEndPoint ipaddress, out string reason)
        {
            reason = String.Empty;

            if (destination == null)
            {
                reason = "Destination is null";
                m_log.Debug("[USER AGENT CONNECTOR]: Given destination is null");
                return false;
            }

            string uri = m_ServerURL + "homeagent/" + aCircuit.AgentID + "/";

            Console.WriteLine("   >>> LoginAgentToGrid <<< " + uri);

            HttpWebRequest AgentCreateRequest = (HttpWebRequest)WebRequest.Create(uri);
            AgentCreateRequest.Method = "POST";
            AgentCreateRequest.ContentType = "application/json";
            AgentCreateRequest.Timeout = 10000;
            //AgentCreateRequest.KeepAlive = false;
            //AgentCreateRequest.Headers.Add("Authorization", authKey);

            // Fill it in
            OSDMap args = PackCreateAgentArguments(aCircuit, gatekeeper, destination, ipaddress);

            string strBuffer = "";
            byte[] buffer = new byte[1];
            try
            {
                strBuffer = OSDParser.SerializeJsonString(args);
                Encoding str = Util.UTF8;
                buffer = str.GetBytes(strBuffer);

            }
            catch (Exception e)
            {
                m_log.WarnFormat("[USER AGENT CONNECTOR]: Exception thrown on serialization of ChildCreate: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                AgentCreateRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = AgentCreateRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                m_log.InfoFormat("[USER AGENT CONNECTOR]: Posted CreateAgent request to remote sim {0}, region {1}, x={2} y={3}",
                    uri, destination.RegionName, destination.RegionLocX, destination.RegionLocY);
            }
            //catch (WebException ex)
            catch
            {
                //m_log.InfoFormat("[USER AGENT CONNECTOR]: Bad send on ChildAgentUpdate {0}", ex.Message);
                reason = "cannot contact remote region";
                return false;
            }
            finally
            {
                if (os != null)
                    os.Close();
            }

            // Let's wait for the response
            //m_log.Info("[USER AGENT CONNECTOR]: Waiting for a reply after DoCreateChildAgentCall");

            try
            {
                using (WebResponse webResponse = AgentCreateRequest.GetResponse())
                {
                    if (webResponse == null)
                    {
                        m_log.Info("[USER AGENT CONNECTOR]: Null reply on DoCreateChildAgentCall post");
                    }
                    else
                    {
                        using (Stream s = webResponse.GetResponseStream())
                        {
                            using (StreamReader sr = new StreamReader(s))
                            {
                                string response = sr.ReadToEnd().Trim();
                                m_log.InfoFormat("[USER AGENT CONNECTOR]: DoCreateChildAgentCall reply was {0} ", response);

                                if (!String.IsNullOrEmpty(response))
                                {
                                    try
                                    {
                                        // we assume we got an OSDMap back
                                        OSDMap r = Util.GetOSDMap(response);
                                        bool success = r["success"].AsBoolean();
                                        reason = r["reason"].AsString();
                                        return success;
                                    }
                                    catch (NullReferenceException e)
                                    {
                                        m_log.InfoFormat("[USER AGENT CONNECTOR]: exception on reply of DoCreateChildAgentCall {0}", e.Message);

                                        // check for old style response
                                        if (response.ToLower().StartsWith("true"))
                                            return true;

                                        return false;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[USER AGENT CONNECTOR]: exception on reply of DoCreateChildAgentCall {0}", ex.Message);
                reason = "Destination did not reply";
                return false;
            }

            return true;

        }


        // The simulators call this interface
        public bool LoginAgentToGrid(AgentCircuitData aCircuit, GridRegion gatekeeper, GridRegion destination, out string reason)
        {
            return LoginAgentToGrid(aCircuit, gatekeeper, destination, null, out reason);
        }

        protected OSDMap PackCreateAgentArguments(AgentCircuitData aCircuit, GridRegion gatekeeper, GridRegion destination, IPEndPoint ipaddress)
        {
            OSDMap args = null;
            try
            {
                args = aCircuit.PackAgentCircuitData();
            }
            catch (Exception e)
            {
                m_log.Debug("[USER AGENT CONNECTOR]: PackAgentCircuitData failed with exception: " + e.Message);
            }

            // Add the input arguments
            args["gatekeeper_serveruri"] = OSD.FromString(gatekeeper.ServerURI);
            args["gatekeeper_host"] = OSD.FromString(gatekeeper.ExternalHostName);
            args["gatekeeper_port"] = OSD.FromString(gatekeeper.HttpPort.ToString());
            args["destination_x"] = OSD.FromString(destination.RegionLocX.ToString());
            args["destination_y"] = OSD.FromString(destination.RegionLocY.ToString());
            args["destination_name"] = OSD.FromString(destination.RegionName);
            args["destination_uuid"] = OSD.FromString(destination.RegionID.ToString());
            args["destination_serveruri"] = OSD.FromString(destination.ServerURI);

            // 10/3/2010
            // I added the client_ip up to the regular AgentCircuitData, so this doesn't need to be here.
            // This need cleaning elsewhere...
            //if (ipaddress != null)
            //    args["client_ip"] = OSD.FromString(ipaddress.Address.ToString());

            return args;
        }

        public void SetClientToken(UUID sessionID, string token)
        { 
            // no-op
        }

        public GridRegion GetHomeRegion(UUID userID, out Vector3 position, out Vector3 lookAt)
        {
            position = Vector3.UnitY; lookAt = Vector3.UnitY;

            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("get_home_region", paramList);
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(m_ServerURL, 10000);
            }
            catch (Exception)
            {
                return null;
            }

            if (response.IsFault)
            {
                return null;
            }

            hash = (Hashtable)response.Value;
            //foreach (Object o in hash)
            //    m_log.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
            try
            {
                bool success = false;
                Boolean.TryParse((string)hash["result"], out success);
                if (success)
                {
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

            }
            catch (Exception)
            {
                return null;
            }

            return null;
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
                m_log.DebugFormat("[USER AGENT CONNECTOR]: Unable to contact remote server {0} for StatusNotification", m_ServerURL);
//                reason = "Exception: " + e.Message;
                return friendsOnline;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: remote call to {0} for StatusNotification returned an error: {1}", m_ServerURL, response.FaultString);
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
                    m_log.ErrorFormat("[USER AGENT CONNECTOR]: GetOnlineFriends Got null response from {0}! THIS IS BAAAAD", m_ServerURL);
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
                m_log.DebugFormat("[USER AGENT CONNECTOR]: Unable to contact remote server {0} for GetOnlineFriends", m_ServerURL);
//                reason = "Exception: " + e.Message;
                return online;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: remote call to {0} for GetOnlineFriends returned an error: {1}", m_ServerURL, response.FaultString);
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
                    m_log.ErrorFormat("[USER AGENT CONNECTOR]: GetOnlineFriends Got null response from {0}! THIS IS BAAAAD", m_ServerURL);
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

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("get_user_info", paramList);

            Dictionary<string, object> info = new Dictionary<string, object>();
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(m_ServerURL, 10000);
            }
            catch
            {
                m_log.DebugFormat("[USER AGENT CONNECTOR]: Unable to contact remote server {0} for GetUserInfo", m_ServerURL);
                return info;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: remote call to {0} for GetServerURLs returned an error: {1}", m_ServerURL, response.FaultString);
                return info;
            }

            hash = (Hashtable)response.Value;
            try
            {
                if (hash == null)
                {
                    m_log.ErrorFormat("[USER AGENT CONNECTOR]: GetUserInfo Got null response from {0}! THIS IS BAAAAD", m_ServerURL);
                    return info;
                }

                // Here is the actual response
                foreach (object key in hash.Keys)
                {
                    if (hash[key] != null)
                    {
                        info.Add(key.ToString(), hash[key]);
                    }
                }
            }
            catch
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: Got exception on GetOnlineFriends response.");
            }

            return info;
        }

        public Dictionary<string, object> GetServerURLs(UUID userID)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("get_server_urls", paramList);
//            string reason = string.Empty;

            // Send and get reply
            Dictionary<string, object> serverURLs = new Dictionary<string,object>();
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(m_ServerURL, 10000);
            }
            catch
            {
                m_log.DebugFormat("[USER AGENT CONNECTOR]: Unable to contact remote server {0} for GetServerURLs", m_ServerURL);
//                reason = "Exception: " + e.Message;
                return serverURLs;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: remote call to {0} for GetServerURLs returned an error: {1}", m_ServerURL, response.FaultString);
//                reason = "XMLRPC Fault";
                return serverURLs;
            }

            hash = (Hashtable)response.Value;
            //foreach (Object o in hash)
            //    m_log.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
            try
            {
                if (hash == null)
                {
                    m_log.ErrorFormat("[USER AGENT CONNECTOR]: GetServerURLs Got null response from {0}! THIS IS BAAAAD", m_ServerURL);
//                    reason = "Internal error 1";
                    return serverURLs;
                }

                // Here is the actual response
                foreach (object key in hash.Keys)
                {
                    if (key is string && ((string)key).StartsWith("SRV_") && hash[key] != null)
                    {
                        string serverType = key.ToString().Substring(4); // remove "SRV_"
                        serverURLs.Add(serverType, hash[key].ToString());
                    }
                }

            }
            catch
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: Got exception on GetOnlineFriends response.");
//                reason = "Exception: " + e.Message;
            }

            return serverURLs;
        }

        public string LocateUser(UUID userID)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("locate_user", paramList);
//            string reason = string.Empty;

            // Send and get reply
            string url = string.Empty;
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(m_ServerURL, 10000);
            }
            catch
            {
                m_log.DebugFormat("[USER AGENT CONNECTOR]: Unable to contact remote server {0} for LocateUser", m_ServerURL);
//                reason = "Exception: " + e.Message;
                return url;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: remote call to {0} for LocateUser returned an error: {1}", m_ServerURL, response.FaultString);
//                reason = "XMLRPC Fault";
                return url;
            }

            hash = (Hashtable)response.Value;
            //foreach (Object o in hash)
            //    m_log.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
            try
            {
                if (hash == null)
                {
                    m_log.ErrorFormat("[USER AGENT CONNECTOR]: LocateUser Got null response from {0}! THIS IS BAAAAD", m_ServerURL);
//                    reason = "Internal error 1";
                    return url;
                }

                // Here's the actual response
                if (hash.ContainsKey("URL"))
                    url = hash["URL"].ToString();

            }
            catch
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: Got exception on LocateUser response.");
//                reason = "Exception: " + e.Message;
            }

            return url;
        }

        public string GetUUI(UUID userID, UUID targetUserID)
        {
            Hashtable hash = new Hashtable();
            hash["userID"] = userID.ToString();
            hash["targetUserID"] = targetUserID.ToString();

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("get_uui", paramList);
//            string reason = string.Empty;

            // Send and get reply
            string uui = string.Empty;
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(m_ServerURL, 10000);
            }
            catch
            {
                m_log.DebugFormat("[USER AGENT CONNECTOR]: Unable to contact remote server {0} for GetUUI", m_ServerURL);
//                reason = "Exception: " + e.Message;
                return uui;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: remote call to {0} for GetUUI returned an error: {1}", m_ServerURL, response.FaultString);
//                reason = "XMLRPC Fault";
                return uui;
            }

            hash = (Hashtable)response.Value;
            //foreach (Object o in hash)
            //    m_log.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
            try
            {
                if (hash == null)
                {
                    m_log.ErrorFormat("[USER AGENT CONNECTOR]: GetUUI Got null response from {0}! THIS IS BAAAAD", m_ServerURL);
//                    reason = "Internal error 1";
                    return uui;
                }

                // Here's the actual response
                if (hash.ContainsKey("UUI"))
                    uui = hash["UUI"].ToString();

            }
            catch
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: Got exception on GetUUI response.");
//                reason = "Exception: " + e.Message;
            }

            return uui;
        }

        public UUID GetUUID(String first, String last)
        {
            Hashtable hash = new Hashtable();
            hash["first"] = first;
            hash["last"] = last;

            IList paramList = new ArrayList();
            paramList.Add(hash);

            XmlRpcRequest request = new XmlRpcRequest("get_uuid", paramList);
            //            string reason = string.Empty;

            // Send and get reply
            UUID uuid = UUID.Zero;
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(m_ServerURL, 10000);
            }
            catch
            {
                m_log.DebugFormat("[USER AGENT CONNECTOR]: Unable to contact remote server {0} for GetUUID", m_ServerURL);
                //                reason = "Exception: " + e.Message;
                return uuid;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: remote call to {0} for GetUUID returned an error: {1}", m_ServerURL, response.FaultString);
                //                reason = "XMLRPC Fault";
                return uuid;
            }

            hash = (Hashtable)response.Value;
            //foreach (Object o in hash)
            //    m_log.Debug(">> " + ((DictionaryEntry)o).Key + ":" + ((DictionaryEntry)o).Value);
            try
            {
                if (hash == null)
                {
                    m_log.ErrorFormat("[USER AGENT CONNECTOR]: GetUUDI Got null response from {0}! THIS IS BAAAAD", m_ServerURL);
                    //                    reason = "Internal error 1";
                    return uuid;
                }

                // Here's the actual response
                if (hash.ContainsKey("UUID"))
                    UUID.TryParse(hash["UUID"].ToString(), out uuid);

            }
            catch
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: Got exception on UUID response.");
                //                reason = "Exception: " + e.Message;
            }

            return uuid;
        }

        private bool GetBoolResponse(XmlRpcRequest request, out string reason)
        {
            //m_log.Debug("[USER AGENT CONNECTOR]: GetBoolResponse from/to " + m_ServerURL);
            XmlRpcResponse response = null;
            try
            {
                response = request.Send(m_ServerURL, 10000);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[USER AGENT CONNECTOR]: Unable to contact remote server {0} for GetBoolResponse", m_ServerURL);
                reason = "Exception: " + e.Message;
                return false;
            }

            if (response.IsFault)
            {
                m_log.ErrorFormat("[USER AGENT CONNECTOR]: remote call to {0} for GetBoolResponse returned an error: {1}", m_ServerURL, response.FaultString);
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
                    m_log.ErrorFormat("[USER AGENT CONNECTOR]: Got null response from {0}! THIS IS BAAAAD", m_ServerURL);
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
                    m_log.WarnFormat("[USER AGENT CONNECTOR]: response from {0} does not have expected key 'result'", m_ServerURL);
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
