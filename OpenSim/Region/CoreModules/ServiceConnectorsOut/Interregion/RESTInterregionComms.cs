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
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Clients;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Hypergrid;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

namespace OpenSim.Region.CoreModules.ServiceConnectorsOut.Interregion
{
    public class RESTInterregionComms : ISharedRegionModule, IInterregionCommsOut
    {
        private bool initialized = false;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_enabled = false;
        protected Scene m_aScene;
        // RESTInterregionComms does not care about local regions; it delegates that to the Local module
        protected LocalInterregionComms m_localBackend;

        protected CommunicationsManager m_commsManager;

        protected RegionToRegionClient m_regionClient;

        protected IHyperlinkService m_hyperlinkService;

        protected bool m_safemode;
        protected IPAddress m_thisIP;

        #region IRegionModule

        public virtual void Initialise(IConfigSource config)
        {
            IConfig startupConfig = config.Configs["Communications"];

            if ((startupConfig == null) || ((startupConfig != null)
                && (startupConfig.GetString("InterregionComms", "RESTComms") == "RESTComms")))
            {
                m_log.Info("[REST COMMS]: Enabling InterregionComms RESTComms module");
                m_enabled = true;
                if (config.Configs["Hypergrid"] != null)
                    m_safemode = config.Configs["Hypergrid"].GetBoolean("safemode", false);
            }
        }

        public virtual void PostInitialise()
        {
        }

        public virtual void Close()
        {
        }

        public void AddRegion(Scene scene)
        {
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_enabled)
            {
                m_localBackend.RemoveScene(scene);
                scene.UnregisterModuleInterface<IInterregionCommsOut>(this);
            }
        }

        public void RegionLoaded(Scene scene)
        {
            if (m_enabled)
            {
                if (!initialized)
                {
                    InitOnce(scene);
                    initialized = true;
                    AddHTTPHandlers();
                }
                InitEach(scene);
            }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public virtual string Name
        {
            get { return "RESTInterregionCommsModule"; }
        }

        protected virtual void InitEach(Scene scene)
        {
            m_localBackend.Init(scene);
            scene.RegisterModuleInterface<IInterregionCommsOut>(this);
        }

        protected virtual void InitOnce(Scene scene)
        {
            m_localBackend = new LocalInterregionComms();
            m_commsManager = scene.CommsManager;
            m_aScene = scene;
            m_hyperlinkService = m_aScene.RequestModuleInterface<IHyperlinkService>();
            m_regionClient = new RegionToRegionClient(m_aScene, m_hyperlinkService);
            m_thisIP = Util.GetHostFromDNS(scene.RegionInfo.ExternalHostName);
        }

        protected virtual void AddHTTPHandlers()
        {
            MainServer.Instance.AddHTTPHandler("/agent/",  AgentHandler);
            MainServer.Instance.AddHTTPHandler("/object/", ObjectHandler);
        }

        #endregion /* IRegionModule */

        #region IInterregionComms

        /**
         * Agent-related communications
         */

        public bool SendCreateChildAgent(ulong regionHandle, AgentCircuitData aCircuit, out string reason)
        {
            // Try local first
            if (m_localBackend.SendCreateChildAgent(regionHandle, aCircuit, out reason))
                return true;

            // else do the remote thing
            if (!m_localBackend.IsLocalRegion(regionHandle))
            {
                uint x = 0, y = 0;
                Utils.LongToUInts(regionHandle, out x, out y);
                GridRegion regInfo = m_aScene.GridService.GetRegionByPosition(UUID.Zero, (int)x, (int)y);
                if (regInfo != null)
                {
                    m_regionClient.SendUserInformation(regInfo, aCircuit);

                    return m_regionClient.DoCreateChildAgentCall(regInfo, aCircuit, "None", out reason);
                }
                //else
                //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            }
            return false;
        }

        public bool SendChildAgentUpdate(ulong regionHandle, AgentData cAgentData)
        {
            // Try local first
            if (m_localBackend.SendChildAgentUpdate(regionHandle, cAgentData))
                return true;

            // else do the remote thing
            if (!m_localBackend.IsLocalRegion(regionHandle))
            {
                uint x = 0, y = 0;
                Utils.LongToUInts(regionHandle, out x, out y);
                GridRegion regInfo = m_aScene.GridService.GetRegionByPosition(UUID.Zero, (int)x, (int)y);
                if (regInfo != null)
                {
                    return m_regionClient.DoChildAgentUpdateCall(regInfo, cAgentData);
                }
                //else
                //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            }
            return false;

        }

        public bool SendChildAgentUpdate(ulong regionHandle, AgentPosition cAgentData)
        {
            // Try local first
            if (m_localBackend.SendChildAgentUpdate(regionHandle, cAgentData))
                return true;

            // else do the remote thing
            if (!m_localBackend.IsLocalRegion(regionHandle))
            {
                uint x = 0, y = 0;
                Utils.LongToUInts(regionHandle, out x, out y);
                GridRegion regInfo = m_aScene.GridService.GetRegionByPosition(UUID.Zero, (int)x, (int)y);
                if (regInfo != null)
                {
                    return m_regionClient.DoChildAgentUpdateCall(regInfo, cAgentData);
                }
                //else
                //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            }
            return false;

        }

        public bool SendRetrieveRootAgent(ulong regionHandle, UUID id, out IAgentData agent)
        {
            // Try local first
            if (m_localBackend.SendRetrieveRootAgent(regionHandle, id, out agent))
                return true;

            // else do the remote thing
            if (!m_localBackend.IsLocalRegion(regionHandle))
            {
                uint x = 0, y = 0;
                Utils.LongToUInts(regionHandle, out x, out y);
                GridRegion regInfo = m_aScene.GridService.GetRegionByPosition(UUID.Zero, (int)x, (int)y);
                if (regInfo != null)
                {
                    return m_regionClient.DoRetrieveRootAgentCall(regInfo, id, out agent);
                }
                //else
                //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            }
            return false;

        }

        public bool SendReleaseAgent(ulong regionHandle, UUID id, string uri)
        {
            // Try local first
            if (m_localBackend.SendReleaseAgent(regionHandle, id, uri))
                return true;

            // else do the remote thing
            return m_regionClient.DoReleaseAgentCall(regionHandle, id, uri);
        }


        public bool SendCloseAgent(ulong regionHandle, UUID id)
        {
            // Try local first
            if (m_localBackend.SendCloseAgent(regionHandle, id))
                return true;

            // else do the remote thing
            if (!m_localBackend.IsLocalRegion(regionHandle))
            {
                uint x = 0, y = 0;
                Utils.LongToUInts(regionHandle, out x, out y);
                GridRegion regInfo = m_aScene.GridService.GetRegionByPosition(UUID.Zero, (int)x, (int)y);
                if (regInfo != null)
                {
                    return m_regionClient.DoCloseAgentCall(regInfo, id);
                }
                //else
                //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            }
            return false;
        }

        /**
         * Object-related communications
         */

        public bool SendCreateObject(ulong regionHandle, SceneObjectGroup sog, bool isLocalCall)
        {
            // Try local first
            if (m_localBackend.SendCreateObject(regionHandle, sog, true))
            {
                //m_log.Debug("[REST COMMS]: LocalBackEnd SendCreateObject succeeded");
                return true;
            }

            // else do the remote thing
            if (!m_localBackend.IsLocalRegion(regionHandle))
            {
                uint x = 0, y = 0;
                Utils.LongToUInts(regionHandle, out x, out y);
                GridRegion regInfo = m_aScene.GridService.GetRegionByPosition(UUID.Zero, (int)x, (int)y);
                if (regInfo != null)
                {
                    return m_regionClient.DoCreateObjectCall(
                        regInfo, sog, SceneObjectSerializer.ToXml2Format(sog), m_aScene.m_allowScriptCrossings);
                }
                //else
                //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            }
            return false;
        }

        public bool SendCreateObject(ulong regionHandle, UUID userID, UUID itemID)
        {
            // Not Implemented
            return false;
        }

        #endregion /* IInterregionComms */

        #region Incoming calls from remote instances

        /**
         * Agent-related incoming calls
         */

        public Hashtable AgentHandler(Hashtable request)
        {
            //m_log.Debug("[CONNECTION DEBUGGING]: AgentHandler Called");

            m_log.Debug("---------------------------");
            m_log.Debug(" >> uri=" + request["uri"]);
            m_log.Debug(" >> content-type=" + request["content-type"]);
            m_log.Debug(" >> http-method=" + request["http-method"]);
            m_log.Debug("---------------------------\n");

            Hashtable responsedata = new Hashtable();
            responsedata["content_type"] = "text/html";
            responsedata["keepalive"] = false;


            UUID agentID;
            string action;
            ulong regionHandle;
            if (!GetParams((string)request["uri"], out agentID, out regionHandle, out action))
            {
                m_log.InfoFormat("[REST COMMS]: Invalid parameters for agent message {0}", request["uri"]);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

            // Next, let's parse the verb
            string method = (string)request["http-method"];
            if (method.Equals("PUT"))
            {
                DoAgentPut(request, responsedata);
                return responsedata;
            }
            else if (method.Equals("POST"))
            {
                DoAgentPost(request, responsedata, agentID);
                return responsedata;
            }
            else if (method.Equals("GET"))
            {
                DoAgentGet(request, responsedata, agentID, regionHandle);
                return responsedata;
            }
            else if (method.Equals("DELETE"))
            {
                DoAgentDelete(request, responsedata, agentID, action, regionHandle);
                return responsedata;
            }
            else
            {
                m_log.InfoFormat("[REST COMMS]: method {0} not supported in agent message", method);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

        }

        protected virtual void DoAgentPost(Hashtable request, Hashtable responsedata, UUID id)
        {
            if (m_safemode)
            {
                // Authentication
                string authority = string.Empty;
                string authToken = string.Empty;
                if (!GetAuthentication(request, out authority, out authToken))
                {
                    m_log.InfoFormat("[REST COMMS]: Authentication failed for agent message {0}", request["uri"]);
                    responsedata["int_response_code"] = 403;
                    responsedata["str_response_string"] = "Forbidden";
                    return ;
                }
                if (!VerifyKey(id, authority, authToken))
                {
                    m_log.InfoFormat("[REST COMMS]: Authentication failed for agent message {0}", request["uri"]);
                    responsedata["int_response_code"] = 403;
                    responsedata["str_response_string"] = "Forbidden";
                    return ;
                }
                m_log.DebugFormat("[REST COMMS]: Authentication succeeded for {0}", id);
            }

            OSDMap args = RegionClient.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";
                return;
            }

            // retrieve the regionhandle
            ulong regionhandle = 0;
            if (args["destination_handle"] != null)
                UInt64.TryParse(args["destination_handle"].AsString(), out regionhandle);

            AgentCircuitData aCircuit = new AgentCircuitData();
            try
            {
                aCircuit.UnpackAgentCircuitData(args);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on unpacking ChildCreate message {0}", ex.Message);
                return;
            }

            OSDMap resp = new OSDMap(2);
            string reason = String.Empty;

            // This is the meaning of POST agent
            m_regionClient.AdjustUserInformation(aCircuit);
            bool result = m_localBackend.SendCreateChildAgent(regionhandle, aCircuit, out reason);

            resp["reason"] = OSD.FromString(reason);
            resp["success"] = OSD.FromBoolean(result);

            // TODO: add reason if not String.Empty?
            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = OSDParser.SerializeJsonString(resp);
        }

        protected virtual void DoAgentPut(Hashtable request, Hashtable responsedata)
        {
            OSDMap args = RegionClient.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";
                return;
            }

            // retrieve the regionhandle
            ulong regionhandle = 0;
            if (args["destination_handle"] != null)
                UInt64.TryParse(args["destination_handle"].AsString(), out regionhandle);

            string messageType;
            if (args["message_type"] != null)
                messageType = args["message_type"].AsString();
            else
            {
                m_log.Warn("[REST COMMS]: Agent Put Message Type not found. ");
                messageType = "AgentData";
            }

            bool result = true;
            if ("AgentData".Equals(messageType))
            {
                AgentData agent = new AgentData();
                try
                {
                    agent.Unpack(args);
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[REST COMMS]: exception on unpacking ChildAgentUpdate message {0}", ex.Message);
                    return;
                }

                //agent.Dump();
                // This is one of the meanings of PUT agent
                result = m_localBackend.SendChildAgentUpdate(regionhandle, agent);

            }
            else if ("AgentPosition".Equals(messageType))
            {
                AgentPosition agent = new AgentPosition();
                try
                {
                    agent.Unpack(args);
                }
                catch (Exception ex)
                {
                    m_log.InfoFormat("[REST COMMS]: exception on unpacking ChildAgentUpdate message {0}", ex.Message);
                    return;
                }
                //agent.Dump();
                // This is one of the meanings of PUT agent
                result = m_localBackend.SendChildAgentUpdate(regionhandle, agent);

            }

            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = result.ToString();
        }

        protected virtual void DoAgentGet(Hashtable request, Hashtable responsedata, UUID id, ulong regionHandle)
        {
            IAgentData agent = null;
            bool result = m_localBackend.SendRetrieveRootAgent(regionHandle, id, out agent);
            OSDMap map = null;
            if (result)
            {
                if (agent != null) // just to make sure
                {
                    map = agent.Pack();
                    string strBuffer = "";
                    try
                    {
                        strBuffer = OSDParser.SerializeJsonString(map);
                    }
                    catch (Exception e)
                    {
                        m_log.WarnFormat("[REST COMMS]: Exception thrown on serialization of CreateObject: {0}", e.Message);
                        // ignore. buffer will be empty, caller should check.
                    }

                    responsedata["content_type"] = "application/json";
                    responsedata["int_response_code"] = 200;
                    responsedata["str_response_string"] = strBuffer;
                }
                else
                {
                    responsedata["int_response_code"] = 500;
                    responsedata["str_response_string"] = "Internal error";
                }
            }
            else
            {
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "Not Found";
            }
        }

        protected virtual void DoAgentDelete(Hashtable request, Hashtable responsedata, UUID id, string action, ulong regionHandle)
        {
            //m_log.Debug(" >>> DoDelete action:" + action + "; regionHandle:" + regionHandle);

            if (action.Equals("release"))
                m_localBackend.SendReleaseAgent(regionHandle, id, "");
            else
                m_localBackend.SendCloseAgent(regionHandle, id);

            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = "OpenSim agent " + id.ToString();

            m_log.Debug("[REST COMMS]: Agent Deleted.");
        }

        /**
         * Object-related incoming calls
         */

        public Hashtable ObjectHandler(Hashtable request)
        {
            m_log.Debug("[CONNECTION DEBUGGING]: ObjectHandler Called");

            m_log.Debug("---------------------------");
            m_log.Debug(" >> uri=" + request["uri"]);
            m_log.Debug(" >> content-type=" + request["content-type"]);
            m_log.Debug(" >> http-method=" + request["http-method"]);
            m_log.Debug("---------------------------\n");

            Hashtable responsedata = new Hashtable();
            responsedata["content_type"] = "text/html";

            UUID objectID;
            string action;
            ulong regionHandle;
            if (!GetParams((string)request["uri"], out objectID, out regionHandle, out action))
            {
                m_log.InfoFormat("[REST COMMS]: Invalid parameters for object message {0}", request["uri"]);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

            // Next, let's parse the verb
            string method = (string)request["http-method"];
            if (method.Equals("POST"))
            {
                DoObjectPost(request, responsedata, regionHandle);
                return responsedata;
            }
            else if (method.Equals("PUT"))
            {
                DoObjectPut(request, responsedata, regionHandle);
                return responsedata;
            }
            //else if (method.Equals("DELETE"))
            //{
            //    DoObjectDelete(request, responsedata, agentID, action, regionHandle);
            //    return responsedata;
            //}
            else
            {
                m_log.InfoFormat("[REST COMMS]: method {0} not supported in object message", method);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

        }

        protected virtual void DoObjectPost(Hashtable request, Hashtable responsedata, ulong regionhandle)
        {
            OSDMap args = RegionClient.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";
                return;
            }

            string sogXmlStr = "", extraStr = "", stateXmlStr = "";
            if (args["sog"] != null)
                sogXmlStr = args["sog"].AsString();
            if (args["extra"] != null)
                extraStr = args["extra"].AsString();

            UUID regionID = m_localBackend.GetRegionID(regionhandle);
            SceneObjectGroup sog = null;
            try
            {
                sog = SceneObjectSerializer.FromXml2Format(sogXmlStr);
                sog.ExtraFromXmlString(extraStr);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on deserializing scene object {0}", ex.Message);
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";
                return;
            }

            if ((args["state"] != null) && m_aScene.m_allowScriptCrossings)
            {
                stateXmlStr = args["state"].AsString();
                if (stateXmlStr != "")
                {
                    try
                    {
                        sog.SetState(stateXmlStr, regionID);
                    }
                    catch (Exception ex)
                    {
                        m_log.InfoFormat("[REST COMMS]: exception on setting state for scene object {0}", ex.Message);

                    }
                }
            }
            // This is the meaning of POST object
            bool result = m_localBackend.SendCreateObject(regionhandle, sog, false);

            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = result.ToString();
        }

        protected virtual void DoObjectPut(Hashtable request, Hashtable responsedata, ulong regionhandle)
        {
            OSDMap args = RegionClient.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";
                return;
            }

            UUID userID = UUID.Zero, itemID = UUID.Zero;
            if (args["userid"] != null)
                userID = args["userid"].AsUUID();
            if (args["itemid"] != null)
                itemID = args["itemid"].AsUUID();

            //UUID regionID = m_localBackend.GetRegionID(regionhandle);

            // This is the meaning of PUT object
            bool result = m_localBackend.SendCreateObject(regionhandle, userID, itemID);

            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = result.ToString();
        }

        #endregion

        #region Misc


        /// <summary>
        /// Extract the param from an uri.
        /// </summary>
        /// <param name="uri">Something like this: /agent/uuid/ or /agent/uuid/handle/release</param>
        /// <param name="uri">uuid on uuid field</param>
        /// <param name="action">optional action</param>
        public static bool GetParams(string uri, out UUID uuid, out ulong regionHandle, out string action)
        {
            uuid = UUID.Zero;
            action = "";
            regionHandle = 0;

            uri = uri.Trim(new char[] { '/' });
            string[] parts = uri.Split('/');
            if (parts.Length <= 1)
            {
                return false;
            }
            else
            {
                if (!UUID.TryParse(parts[1], out uuid))
                    return false;

                if (parts.Length >= 3)
                    UInt64.TryParse(parts[2], out regionHandle);
                if (parts.Length >= 4)
                    action = parts[3];

                return true;
            }
        }

        public static bool GetAuthentication(Hashtable request, out string authority, out string authKey)
        {
            authority = string.Empty;
            authKey = string.Empty;

            Uri authUri;
            Hashtable headers = (Hashtable)request["headers"];

            // Authorization keys look like this:
            // http://orgrid.org:8002/<uuid>
            if (headers.ContainsKey("authorization") && (string)headers["authorization"] != "None")
            {
                if (Uri.TryCreate((string)headers["authorization"], UriKind.Absolute, out authUri))
                {
                    authority = authUri.Authority;
                    authKey = authUri.PathAndQuery.Trim('/');
                    m_log.DebugFormat("[REST COMMS]: Got authority {0} and key {1}", authority, authKey);
                    return true;
                }
                else
                    m_log.Debug("[REST COMMS]: Wrong format for Authorization header: " + (string)headers["authorization"]);
            }
            else
                m_log.Debug("[REST COMMS]: Authorization header not found");

            return false;
        }

        bool VerifyKey(UUID userID, string authority, string key)
        {
            string[] parts = authority.Split(':');
            IPAddress ipaddr = IPAddress.None;
            uint port = 0;
            if (parts.Length <= 2)
                ipaddr = Util.GetHostFromDNS(parts[0]);
            if (parts.Length == 2)
                UInt32.TryParse(parts[1], out port);

            // local authority (standalone), local call
            if (m_thisIP.Equals(ipaddr) && (m_aScene.RegionInfo.HttpPort == port))
                return ((IAuthentication)m_aScene.CommsManager.UserAdminService).VerifyKey(userID, key);
            // remote call
            else
                return AuthClient.VerifyKey("http://" + authority, userID, key);
        }


        #endregion Misc

        protected class RegionToRegionClient : RegionClient
        {
            Scene m_aScene = null;
            IHyperlinkService m_hyperlinkService;

            public RegionToRegionClient(Scene s, IHyperlinkService hyperService)
            {
                m_aScene = s;
                m_hyperlinkService = hyperService;
            }

            public override ulong GetRegionHandle(ulong handle)
            {
                if (m_aScene.SceneGridService is HGSceneCommunicationService)
                {
                    if (m_hyperlinkService != null)
                        return m_hyperlinkService.FindRegionHandle(handle);
                }

                return handle;
            }

            public override bool IsHyperlink(ulong handle)
            {
                if (m_aScene.SceneGridService is HGSceneCommunicationService)
                {
                    if ((m_hyperlinkService != null) && (m_hyperlinkService.GetHyperlinkRegion(handle) != null))
                        return true;
                }
                return false;
            }

            public override void SendUserInformation(GridRegion regInfo, AgentCircuitData aCircuit)
            {
                if (m_hyperlinkService != null)
                    m_hyperlinkService.SendUserInformation(regInfo, aCircuit);

            }

            public override void AdjustUserInformation(AgentCircuitData aCircuit)
            {
                if (m_hyperlinkService != null)
                    m_hyperlinkService.AdjustUserInformation(aCircuit);
            }
        }

    }
}
