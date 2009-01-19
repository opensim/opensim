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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Xml;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Scenes.Hypergrid;
using OpenSim.Region.Environment.Modules.Communications.Local;

namespace OpenSim.Region.Environment.Modules.Communications.REST
{
    public class RESTInterregionComms : IRegionModule, IInterregionCommsOut
    {
        private static bool initialized = false;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected bool m_enabled = false;
        protected Scene m_aScene;
        // RESTInterregionComms does not care about local regions; it delegates that to the Local module
        protected LocalInterregionComms m_localBackend;

        protected CommunicationsManager m_commsManager;

        #region IRegionModule

        public virtual void Initialise(Scene scene, IConfigSource config)
        {
            if (!initialized)
            {
                initialized = true;
                IConfig startupConfig = config.Configs["Communications"];
                
                if ((startupConfig == null) 
                    || (startupConfig != null) 
                    && (startupConfig.GetString("InterregionComms", "RESTComms") == "RESTComms"))
                {
                    m_log.Info("[REST COMMS]: Enabling InterregionComms RESTComms module");
                    m_enabled = true;
                    InitOnce(scene);
                }
            }

            if (!m_enabled)
                return;

            InitEach(scene);

        }

        public virtual void PostInitialise()
        {
            if (m_enabled)
                AddHTTPHandlers();
        }

        public virtual void Close()
        {
        }

        public virtual string Name
        {
            get { return "RESTInterregionCommsModule"; }
        }

        public virtual bool IsSharedModule
        {
            get { return true; }
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
        }

        protected virtual void AddHTTPHandlers()
        {
            m_aScene.CommsManager.HttpServer.AddHTTPHandler("/agent/", AgentHandler);
        }

        #endregion /* IRegionModule */

        #region IInterregionComms

        public bool SendCreateChildAgent(ulong regionHandle, AgentCircuitData aCircuit)
        {
            // Try local first
            if (m_localBackend.SendCreateChildAgent(regionHandle, aCircuit))
                return true;

            // else do the remote thing
            RegionInfo regInfo = m_commsManager.GridService.RequestNeighbourInfo(regionHandle);
            if (regInfo != null)
            {
                SendUserInformation(regInfo, aCircuit);

                return DoCreateChildAgentCall(regInfo, aCircuit);
            }
            //else
            //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            return false;
        }

        public bool SendChildAgentUpdate(ulong regionHandle, AgentData cAgentData)
        {
            // Try local first
            if (m_localBackend.SendChildAgentUpdate(regionHandle, cAgentData))
                return true;

            // else do the remote thing
            RegionInfo regInfo = m_commsManager.GridService.RequestNeighbourInfo(regionHandle);
            if (regInfo != null)
            {
                return DoChildAgentUpdateCall(regInfo, cAgentData);
            }
            //else
            //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            return false;

        }

        public bool SendChildAgentUpdate(ulong regionHandle, AgentPosition cAgentData)
        {
            // Try local first
            if (m_localBackend.SendChildAgentUpdate(regionHandle, cAgentData))
                return true;

            // else do the remote thing
            RegionInfo regInfo = m_commsManager.GridService.RequestNeighbourInfo(regionHandle);
            if (regInfo != null)
            {
                return DoChildAgentUpdateCall(regInfo, cAgentData);
            }
            //else
            //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            return false;

        }
        
        public bool SendReleaseAgent(ulong regionHandle, UUID id, string uri)
        {
            // Try local first
            if (m_localBackend.SendReleaseAgent(regionHandle, id, uri))
                return true;

            // else do the remote thing
            return DoReleaseAgentCall(regionHandle, id, uri);
        }

        public bool SendCloseAgent(ulong regionHandle, UUID id)
        {
            // Try local first
            if (m_localBackend.SendCloseAgent(regionHandle, id))
                return true;

            // else do the remote thing
            RegionInfo regInfo = m_commsManager.GridService.RequestNeighbourInfo(regionHandle);
            if (regInfo != null)
            {
                return DoCloseAgentCall(regInfo, id);
            }
            //else
            //    m_log.Warn("[REST COMMS]: Region not found " + regionHandle);
            return false;
        }              

        #endregion /* IInterregionComms */

        #region DoWork functions for the above public interface
        
        //-------------------------------------------------------------------
        // Internal  functions for the above public interface
        //-------------------------------------------------------------------

        protected bool DoCreateChildAgentCall(RegionInfo region, AgentCircuitData aCircuit)
        {
            // Eventually, we want to use a caps url instead of the agentID
            string uri = "http://" + region.ExternalEndPoint.Address + ":" + region.HttpPort + "/agent/" + aCircuit.AgentID + "/";
            //Console.WriteLine("   >>> DoCreateChildAgentCall <<< " + uri);

            WebRequest AgentCreateRequest = WebRequest.Create(uri);
            AgentCreateRequest.Method = "POST";
            AgentCreateRequest.ContentType = "application/json";
            AgentCreateRequest.Timeout = 10000;

            // Fill it in
            OSDMap args = null;
            try
            {
                args = aCircuit.PackAgentCircuitData();
            }
            catch (Exception e)
            {
                m_log.Debug("[REST COMMS]: PackAgentCircuitData failed with exception: " + e.Message);
            }
            // Add the regionhandle of the destination region
            ulong regionHandle = GetRegionHandle(region.RegionHandle);
            args["destination_handle"] = OSD.FromString(regionHandle.ToString());

            string strBuffer = "";
            byte[] buffer = new byte[1];
            try
            {
                strBuffer = OSDParser.SerializeJsonString(args);
                System.Text.UTF8Encoding str = new System.Text.UTF8Encoding();
                buffer = str.GetBytes(strBuffer);

            }
            catch (Exception e)
            {
                m_log.WarnFormat("[OSG2]: Exception thrown on serialization of ChildCreate: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                AgentCreateRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = AgentCreateRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                os.Close();
                //m_log.InfoFormat("[REST COMMS]: Posted ChildAgentUpdate request to remote sim {0}", uri);
            }
            //catch (WebException ex)
            catch
            {
                //m_log.InfoFormat("[REST COMMS]: Bad send on ChildAgentUpdate {0}", ex.Message);

                return false;
            }

            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after DoCreateChildAgentCall");

            try
            {
                WebResponse webResponse = AgentCreateRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on DoCreateChildAgentCall post");
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REST COMMS]: DoCreateChildAgentCall reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of DoCreateChildAgentCall {0}", ex.Message);
                // ignore, really
            }

            return true;

        }

        protected bool DoChildAgentUpdateCall(RegionInfo region, IAgentData cAgentData)
        {
            // Eventually, we want to use a caps url instead of the agentID
            string uri = "http://" + region.ExternalEndPoint.Address + ":" + region.HttpPort + "/agent/" + cAgentData.AgentID + "/";
            //Console.WriteLine("   >>> DoChildAgentUpdateCall <<< " + uri);

            WebRequest ChildUpdateRequest = WebRequest.Create(uri);
            ChildUpdateRequest.Method = "PUT";
            ChildUpdateRequest.ContentType = "application/json";
            ChildUpdateRequest.Timeout = 10000;

            // Fill it in
            OSDMap args = null;
            try
            {
                args = cAgentData.PackUpdateMessage();
            }
            catch (Exception e)
            {
                m_log.Debug("[REST COMMS]: PackUpdateMessage failed with exception: " + e.Message);
            }
            // Add the regionhandle of the destination region
            ulong regionHandle = GetRegionHandle(region.RegionHandle);
            args["destination_handle"] = OSD.FromString(regionHandle.ToString());

            string strBuffer = "";
            byte[] buffer = new byte[1];
            try
            {
                strBuffer = OSDParser.SerializeJsonString(args);
                System.Text.UTF8Encoding str = new System.Text.UTF8Encoding();
                buffer = str.GetBytes(strBuffer);

            }
            catch (Exception e)
            {
                m_log.WarnFormat("[REST COMMS]: Exception thrown on serialization of ChildUpdate: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                ChildUpdateRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = ChildUpdateRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                os.Close();
                //m_log.InfoFormat("[REST COMMS]: Posted ChildAgentUpdate request to remote sim {0}", uri);
            }
            //catch (WebException ex)
            catch                
            {
                //m_log.InfoFormat("[REST COMMS]: Bad send on ChildAgentUpdate {0}", ex.Message);

                return false;
            }

            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after ChildAgentUpdate");
            
            try
            {
                WebResponse webResponse = ChildUpdateRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on ChilAgentUpdate post");
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REST COMMS]: ChilAgentUpdate reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of ChilAgentUpdate {0}", ex.Message);
                // ignore, really
            }

            return true;
        }

        protected bool DoReleaseAgentCall(ulong regionHandle, UUID id, string uri)
        {
            //Console.WriteLine("   >>> DoReleaseAgentCall <<< " + uri);

            WebRequest request = WebRequest.Create(uri);
            request.Method = "DELETE";
            request.Timeout = 10000;

            try
            {
                WebResponse webResponse = request.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on agent get ");
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REST COMMS]: ChilAgentUpdate reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of agent get {0}", ex.Message);
                // ignore, really
            }

            return true;
        }

        protected bool DoCloseAgentCall(RegionInfo region, UUID id)
        {
            string uri = "http://" + region.ExternalEndPoint.Address + ":" + region.HttpPort + "/agent/" + id + "/" + region.RegionHandle.ToString() +"/";

            //Console.WriteLine("   >>> DoCloseAgentCall <<< " + uri);

            WebRequest request = WebRequest.Create(uri);
            request.Method = "DELETE";
            request.Timeout = 10000;

            try
            {
                WebResponse webResponse = request.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on agent get ");
                }

                StreamReader sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REST COMMS]: ChilAgentUpdate reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of agent get {0}", ex.Message);
                // ignore, really
            }

            return true;
        }
        
        #endregion /* Do Work */

        #region Incoming calls from remote instances

        public Hashtable AgentHandler(Hashtable request)
        {
            //m_log.Debug("[CONNECTION DEBUGGING]: AgentHandler Called");

            //Console.WriteLine("---------------------------");
            //Console.WriteLine(" >> uri=" + request["uri"]);
            //Console.WriteLine(" >> content-type=" + request["content-type"]);
            //Console.WriteLine(" >> http-method=" + request["http-method"]);
            //Console.WriteLine("---------------------------\n");

            Hashtable responsedata = new Hashtable();
            responsedata["content_type"] = "text/html";

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
                DoPut(request, responsedata);
                return responsedata;
            }
            else if (method.Equals("POST"))
            {
                DoPost(request, responsedata, agentID);
                return responsedata;
            }
            else if (method.Equals("DELETE"))
            {
                DoDelete(request, responsedata, agentID, action, regionHandle);

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

        protected OSDMap GetOSDMap(Hashtable request)
        {
            OSDMap args = null;
            try
            {
                OSD buffer;
                // We should pay attention to the content-type, but let's assume we know it's Json
                buffer = OSDParser.DeserializeJson((string)request["body"]);
                if (buffer.Type == OSDType.Map)
                {
                    args = (OSDMap)buffer;
                    return args;
                }
                else
                {
                    // uh?
                    m_log.Debug("[REST COMMS]: Got OSD of type " + buffer.Type.ToString());
                    return null;
                }
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on parse of ChildAgentUpdate message {0}", ex.Message);
                return null;
            }
        }

        protected virtual void DoPost(Hashtable request, Hashtable responsedata, UUID id)
        {
            OSDMap args = GetOSDMap(request);
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

            // This is the meaning of POST agent
            AdjustUserInformation(aCircuit);
            bool result = m_localBackend.SendCreateChildAgent(regionhandle, aCircuit);

            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = result.ToString();
        }

        protected virtual void DoPut(Hashtable request, Hashtable responsedata)
        {
            OSDMap args = GetOSDMap(request);
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
                    agent.UnpackUpdateMessage(args);
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
                    agent.UnpackUpdateMessage(args);
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

        protected virtual void DoDelete(Hashtable request, Hashtable responsedata, UUID id, string action, ulong regionHandle)
        {
            //Console.WriteLine(" >>> DoDelete action:" + action + "; regionHandle:" + regionHandle);
            
            if (action.Equals("release"))
                m_localBackend.SendReleaseAgent(regionHandle, id, "");
            else
                m_localBackend.SendCloseAgent(regionHandle, id);

            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = "OpenSim agent " + id.ToString();
        }
        
        #endregion 

        #region Misc
        
        /// <summary>
        /// Extract the param from an uri.
        /// </summary>
        /// <param name="uri">Something like this: /agent/uuid/ or /agent/uuid/handle/release</param>
        /// <param name="uri">uuid on uuid field</param>
        /// <param name="action">optional action</param>
        protected bool GetParams(string uri, out UUID uuid, out ulong regionHandle, out string action)
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

        #endregion Misc 

        #region Hyperlinks

        protected virtual ulong GetRegionHandle(ulong handle)
        {
            if (m_aScene.SceneGridService is HGSceneCommunicationService)
                return ((HGSceneCommunicationService)(m_aScene.SceneGridService)).m_hg.FindRegionHandle(handle);

            return handle;
        }

        protected virtual bool IsHyperlink(ulong handle)
        {
            if (m_aScene.SceneGridService is HGSceneCommunicationService)
                return ((HGSceneCommunicationService)(m_aScene.SceneGridService)).m_hg.IsHyperlinkRegion(handle);

            return false;
        }

        protected virtual void SendUserInformation(RegionInfo regInfo, AgentCircuitData aCircuit)
        {
            try
            {
                //if (IsHyperlink(regInfo.RegionHandle))
                if (m_aScene.SceneGridService is HGSceneCommunicationService)
                {
                    ((HGSceneCommunicationService)(m_aScene.SceneGridService)).m_hg.SendUserInformation(regInfo, aCircuit);
                }
            }
            catch // Bad cast
            { }

        }

        protected virtual void AdjustUserInformation(AgentCircuitData aCircuit)
        {
            if (m_aScene.SceneGridService is HGSceneCommunicationService)
                ((HGSceneCommunicationService)(m_aScene.SceneGridService)).m_hg.AdjustUserInformation(aCircuit);
        }
        #endregion /* Hyperlinks */

    }
}
