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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;

using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nini.Config;

namespace OpenSim.Services.Connectors.Simulation
{
    public class SimulationServiceConnector : ISimulationService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //private GridRegion m_Region;

        public SimulationServiceConnector()
        {
        }

        public SimulationServiceConnector(IConfigSource config)
        {
            //m_Region = region;
        }

        public IScene GetScene(ulong regionHandle)
        {
            return null;
        }

        public ISimulationService GetInnerService()
        {
            return null;
        }

        #region Agents

        protected virtual string AgentPath()
        {
            return "agent/";
        }

        public bool CreateAgent(GridRegion destination, AgentCircuitData aCircuit, uint flags, out string reason)
        {
            HttpWebRequest AgentCreateRequest = null;
            reason = String.Empty;

            if (SendRequest(destination, aCircuit, flags, out reason, out AgentCreateRequest))
            {
                string response = GetResponse(AgentCreateRequest, out reason);
                bool success = true;
                UnpackResponse(response, out success, out reason);
                return success;
            }

            return false;
        }


        protected bool SendRequest(GridRegion destination, AgentCircuitData aCircuit, uint flags, out string reason, out HttpWebRequest AgentCreateRequest)
        {
            reason = String.Empty;
            AgentCreateRequest = null;

            if (destination == null)
            {
                reason = "Destination is null";
                m_log.Debug("[REMOTE SIMULATION CONNECTOR]: Given destination is null");
                return false;
            }

            string uri = destination.ServerURI + AgentPath() + aCircuit.AgentID + "/";

            AgentCreateRequest = (HttpWebRequest)WebRequest.Create(uri);
            AgentCreateRequest.Method = "POST";
            AgentCreateRequest.ContentType = "application/json";
            AgentCreateRequest.Timeout = 10000;
            //AgentCreateRequest.KeepAlive = false;
            //AgentCreateRequest.Headers.Add("Authorization", authKey);

            // Fill it in
            OSDMap args = PackCreateAgentArguments(aCircuit, destination, flags);
            if (args == null)
                return false;

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
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR]: Exception thrown on serialization of ChildCreate: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                AgentCreateRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = AgentCreateRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: Posted CreateAgent request to remote sim {0}, region {1}, x={2} y={3}",
                    uri, destination.RegionName, destination.RegionLocX, destination.RegionLocY);
            }
            //catch (WebException ex)
            catch
            {
                //m_log.ErrorFormat("[REMOTE SIMULATION CONNECTOR]: Bad send on ChildAgentUpdate {0}", ex.Message);
                reason = "cannot contact remote region";
                return false;
            }
            finally
            {
                if (os != null)
                    os.Close();
            }

            return true;
        }

        protected string GetResponse(HttpWebRequest AgentCreateRequest, out string reason)
        {
            // Let's wait for the response
            //m_log.Info("[REMOTE SIMULATION CONNECTOR]: Waiting for a reply after DoCreateChildAgentCall");
            reason = string.Empty;

            WebResponse webResponse = null;
            StreamReader sr = null;
            string response = string.Empty;
            try
            {
                webResponse = AgentCreateRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Debug("[REMOTE SIMULATION CONNECTOR]: Null reply on DoCreateChildAgentCall post");
                }
                else
                {

                    sr = new StreamReader(webResponse.GetResponseStream());
                    response = sr.ReadToEnd().Trim();
                    m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: DoCreateChildAgentCall reply was {0} ", response);
                }
            }
            catch (WebException ex)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR]: exception on reply of DoCreateChildAgentCall {0}", ex.Message);
                reason = "Destination did not reply";
                return string.Empty;
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            return response;
        }

        protected void UnpackResponse(string response, out bool result, out string reason)
        {
            result = true;
            reason = string.Empty;
            if (!String.IsNullOrEmpty(response))
            {
                try
                {
                    // we assume we got an OSDMap back
                    OSDMap r = Util.GetOSDMap(response);
                    result = r["success"].AsBoolean();
                    reason = r["reason"].AsString();
                }
                catch (NullReferenceException e)
                {
                    m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR]: exception on reply of DoCreateChildAgentCall {0}", e.Message);

                    // check for old style response
                    if (response.ToLower().StartsWith("true"))
                        result = true;

                    result = false;
                }
            }
        }

        protected virtual OSDMap PackCreateAgentArguments(AgentCircuitData aCircuit, GridRegion destination, uint flags)
        {
            OSDMap args = null;
            try
            {
                args = aCircuit.PackAgentCircuitData();
            }
            catch (Exception e)
            {
                m_log.Warn("[REMOTE SIMULATION CONNECTOR]: PackAgentCircuitData failed with exception: " + e.Message);
                return null;
            }
            // Add the input arguments
            args["destination_x"] = OSD.FromString(destination.RegionLocX.ToString());
            args["destination_y"] = OSD.FromString(destination.RegionLocY.ToString());
            args["destination_name"] = OSD.FromString(destination.RegionName);
            args["destination_uuid"] = OSD.FromString(destination.RegionID.ToString());
            args["teleport_flags"] = OSD.FromString(flags.ToString());

            return args;
        }

        public bool UpdateAgent(GridRegion destination, AgentData data)
        {
            return UpdateAgent(destination, (IAgentData)data);
        }

        public bool UpdateAgent(GridRegion destination, AgentPosition data)
        {
            return UpdateAgent(destination, (IAgentData)data);
        }

        private bool UpdateAgent(GridRegion destination, IAgentData cAgentData)
        {
            // Eventually, we want to use a caps url instead of the agentID

            string uri = destination.ServerURI + AgentPath() + cAgentData.AgentID + "/";

            HttpWebRequest ChildUpdateRequest = (HttpWebRequest)WebRequest.Create(uri);
            ChildUpdateRequest.Method = "PUT";
            ChildUpdateRequest.ContentType = "application/json";
            ChildUpdateRequest.Timeout = 30000;
            //ChildUpdateRequest.KeepAlive = false;

            // Fill it in
            OSDMap args = null;
            try
            {
                args = cAgentData.Pack();
            }
            catch (Exception e)
            {
                m_log.Warn("[REMOTE SIMULATION CONNECTOR]: PackUpdateMessage failed with exception: " + e.Message);
            }
            // Add the input arguments
            args["destination_x"] = OSD.FromString(destination.RegionLocX.ToString());
            args["destination_y"] = OSD.FromString(destination.RegionLocY.ToString());
            args["destination_name"] = OSD.FromString(destination.RegionName);
            args["destination_uuid"] = OSD.FromString(destination.RegionID.ToString());

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
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR]: Exception thrown on serialization of ChildUpdate: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                ChildUpdateRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = ChildUpdateRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                //m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: Posted AgentUpdate request to remote sim {0}", uri);
            }
            catch (WebException ex)
            //catch
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR]: Bad send on AgentUpdate {0}", ex.Message);

                return false;
            }
            finally
            {
                if (os != null)
                    os.Close();
            }

            // Let's wait for the response
            //m_log.Debug("[REMOTE SIMULATION CONNECTOR]: Waiting for a reply after ChildAgentUpdate");

            WebResponse webResponse = null;
            StreamReader sr = null;
            try
            {
                webResponse = ChildUpdateRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Debug("[REMOTE SIMULATION CONNECTOR]: Null reply on ChilAgentUpdate post");
                }

                sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: ChilAgentUpdate reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR]: exception on reply of ChilAgentUpdate from {0}: {1}", uri, ex.Message);
                // ignore, really
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            return true;
        }

        public bool RetrieveAgent(GridRegion destination, UUID id, out IAgentData agent)
        {
            agent = null;
            // Eventually, we want to use a caps url instead of the agentID
            string uri = destination.ServerURI + AgentPath() + id + "/" + destination.RegionID.ToString() + "/";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.Timeout = 10000;
            //request.Headers.Add("authorization", ""); // coming soon

            HttpWebResponse webResponse = null;
            string reply = string.Empty;
            StreamReader sr = null;
            try
            {
                webResponse = (HttpWebResponse)request.GetResponse();
                if (webResponse == null)
                {
                    m_log.Debug("[REMOTE SIMULATION CONNECTOR]: Null reply on agent get ");
                }

                sr = new StreamReader(webResponse.GetResponseStream());
                reply = sr.ReadToEnd().Trim();


            }
            catch (WebException ex)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR]: exception on reply of agent get {0}", ex.Message);
                // ignore, really
                return false;
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            if (webResponse.StatusCode == HttpStatusCode.OK)
            {
                // we know it's jason
                OSDMap args = Util.GetOSDMap(reply);
                if (args == null)
                {
                    return false;
                }

                agent = new CompleteAgentData();
                agent.Unpack(args);
                return true;
            }

            return false;
        }

        public bool ReleaseAgent(UUID origin, UUID id, string uri)
        {
            WebRequest request = WebRequest.Create(uri);
            request.Method = "DELETE";
            request.Timeout = 10000;

            StreamReader sr = null;
            try
            {
                WebResponse webResponse = request.GetResponse();
                if (webResponse == null)
                {
                    m_log.Debug("[REMOTE SIMULATION CONNECTOR]: Null reply on ReleaseAgent");
                }

                sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REMOTE SIMULATION CONNECTOR]: ChilAgentUpdate reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR]: exception on reply of ReleaseAgent {0}", ex.Message);
                return false;
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            return true;
        }

        public bool CloseAgent(GridRegion destination, UUID id)
        {
            string uri = destination.ServerURI + AgentPath() + id + "/" + destination.RegionID.ToString() + "/";

            WebRequest request = WebRequest.Create(uri);
            request.Method = "DELETE";
            request.Timeout = 10000;

            StreamReader sr = null;
            try
            {
                WebResponse webResponse = request.GetResponse();
                if (webResponse == null)
                {
                    m_log.Debug("[REMOTE SIMULATION CONNECTOR]: Null reply on agent delete ");
                }

                sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REMOTE SIMULATION CONNECTOR]: ChilAgentUpdate reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR]: exception on reply of agent delete from {0}: {1}", destination.RegionName, ex.Message);
                return false;
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            return true;
        }

        #endregion Agents

        #region Objects

        protected virtual string ObjectPath()
        {
            return "/object/";
        }

        public bool CreateObject(GridRegion destination, ISceneObject sog, bool isLocalCall)
        {
            string uri
                = destination.ServerURI + ObjectPath() + sog.UUID + "/";
            //m_log.Debug("   >>> DoCreateObjectCall <<< " + uri);

            WebRequest ObjectCreateRequest = WebRequest.Create(uri);
            ObjectCreateRequest.Method = "POST";
            ObjectCreateRequest.ContentType = "application/json";
            ObjectCreateRequest.Timeout = 10000;

            OSDMap args = new OSDMap(2);
            args["sog"] = OSD.FromString(sog.ToXml2());
            args["extra"] = OSD.FromString(sog.ExtraToXmlString());
            args["modified"] = OSD.FromBoolean(sog.HasGroupChanged);
            string state = sog.GetStateSnapshot();
            if (state.Length > 0)
                args["state"] = OSD.FromString(state);
            // Add the input general arguments
            args["destination_x"] = OSD.FromString(destination.RegionLocX.ToString());
            args["destination_y"] = OSD.FromString(destination.RegionLocY.ToString());
            args["destination_name"] = OSD.FromString(destination.RegionName);
            args["destination_uuid"] = OSD.FromString(destination.RegionID.ToString());

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
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR]: Exception thrown on serialization of CreateObject: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                ObjectCreateRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = ObjectCreateRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                m_log.DebugFormat("[REMOTE SIMULATION CONNECTOR]: Posted CreateObject request to remote sim {0}", uri);
            }
            catch (WebException ex)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR]: Bad send on CreateObject {0}", ex.Message);
                return false;
            }
            finally
            {
                if (os != null)
                    os.Close();
            }

            // Let's wait for the response
            //m_log.Info("[REMOTE SIMULATION CONNECTOR]: Waiting for a reply after DoCreateChildAgentCall");

            StreamReader sr = null;
            try
            {
                WebResponse webResponse = ObjectCreateRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Warn("[REMOTE SIMULATION CONNECTOR]: Null reply on CreateObject post");
                    return false;
                }

                sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                //m_log.InfoFormat("[REMOTE SIMULATION CONNECTOR]: DoCreateChildAgentCall reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.WarnFormat("[REMOTE SIMULATION CONNECTOR]: exception on reply of CreateObject {0}", ex.Message);
                return false;
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            return true;
        }

        public bool CreateObject(GridRegion destination, UUID userID, UUID itemID)
        {
            // TODO, not that urgent
            return false;
        }

        #endregion Objects
    }
}
