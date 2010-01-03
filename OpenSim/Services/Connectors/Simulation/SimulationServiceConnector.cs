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

namespace OpenSim.Services.Connectors.Simulation
{
    public class SimulationServiceConnector : ISimulationService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //private GridRegion m_Region;

        public SimulationServiceConnector()
        {
        }

        public SimulationServiceConnector(GridRegion region)
        {
            //m_Region = region;
        }

        public IScene GetScene(ulong regionHandle)
        {
            return null;
        }

        #region Agents

        public bool CreateAgent(GridRegion destination, AgentCircuitData aCircuit, uint flags, out string reason)
        {
            reason = String.Empty;

            // Eventually, we want to use a caps url instead of the agentID
            string uri = string.Empty;
            try
            {
                uri = "http://" + destination.ExternalEndPoint.Address + ":" + destination.HttpPort + "/agent/" + aCircuit.AgentID + "/";
            }
            catch (Exception e)
            {
                m_log.Debug("[REMOTE SIMULATION CONNECTOR]: Unable to resolve external endpoint on agent create. Reason: " + e.Message);
                reason = e.Message;
                return false;
            }

            //Console.WriteLine("   >>> DoCreateChildAgentCall <<< " + uri);

            HttpWebRequest AgentCreateRequest = (HttpWebRequest)WebRequest.Create(uri);
            AgentCreateRequest.Method = "POST";
            AgentCreateRequest.ContentType = "application/json";
            AgentCreateRequest.Timeout = 10000;
            //AgentCreateRequest.KeepAlive = false;
            //AgentCreateRequest.Headers.Add("Authorization", authKey);

            // Fill it in
            OSDMap args = null;
            try
            {
                args = aCircuit.PackAgentCircuitData();
            }
            catch (Exception e)
            {
                m_log.Debug("[REMOTE SIMULATION CONNECTOR]: PackAgentCircuitData failed with exception: " + e.Message);
            }
            // Add the input arguments
            args["destination_x"] = OSD.FromString(destination.RegionLocX.ToString());
            args["destination_y"] = OSD.FromString(destination.RegionLocY.ToString());
            args["destination_name"] = OSD.FromString(destination.RegionName);
            args["destination_uuid"] = OSD.FromString(destination.RegionID.ToString());
            args["teleport_flags"] = OSD.FromString(flags.ToString());

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
                //m_log.InfoFormat("[REMOTE SIMULATION CONNECTOR]: Posted CreateChildAgent request to remote sim {0}", uri);
            }
            //catch (WebException ex)
            catch
            {
                //m_log.InfoFormat("[REMOTE SIMULATION CONNECTOR]: Bad send on ChildAgentUpdate {0}", ex.Message);
                reason = "cannot contact remote region";
                return false;
            }
            finally
            {
                if (os != null)
                    os.Close();
            }

            // Let's wait for the response
            //m_log.Info("[REMOTE SIMULATION CONNECTOR]: Waiting for a reply after DoCreateChildAgentCall");

            WebResponse webResponse = null;
            StreamReader sr = null;
            try
            {
                webResponse = AgentCreateRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REMOTE SIMULATION CONNECTOR]: Null reply on DoCreateChildAgentCall post");
                }
                else
                {

                    sr = new StreamReader(webResponse.GetResponseStream());
                    string response = sr.ReadToEnd().Trim();
                    m_log.InfoFormat("[REMOTE SIMULATION CONNECTOR]: DoCreateChildAgentCall reply was {0} ", response);

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
                            m_log.InfoFormat("[REMOTE SIMULATION CONNECTOR]: exception on reply of DoCreateChildAgentCall {0}", e.Message);

                            // check for old style response
                            if (response.ToLower().StartsWith("true"))
                                return true;

                            return false;
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REMOTE SIMULATION CONNECTOR]: exception on reply of DoCreateChildAgentCall {0}", ex.Message);
                // ignore, really
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            return true;
        }

        public bool UpdateAgent(GridRegion destination, AgentData data)
        {
            return UpdateAgent(destination, data);
        }

        public bool UpdateAgent(GridRegion destination, AgentPosition data)
        {
            return UpdateAgent(destination, data);
        }

        private bool UpdateAgent(GridRegion destination, IAgentData cAgentData)
        {
            // Eventually, we want to use a caps url instead of the agentID
            string uri = string.Empty;
            try
            {
                uri = "http://" + destination.ExternalEndPoint.Address + ":" + destination.HttpPort + "/agent/" + cAgentData.AgentID + "/";
            }
            catch (Exception e)
            {
                m_log.Debug("[REMOTE SIMULATION CONNECTOR]: Unable to resolve external endpoint on agent update. Reason: " + e.Message);
                return false;
            }
            //Console.WriteLine("   >>> DoChildAgentUpdateCall <<< " + uri);

            HttpWebRequest ChildUpdateRequest = (HttpWebRequest)WebRequest.Create(uri);
            ChildUpdateRequest.Method = "PUT";
            ChildUpdateRequest.ContentType = "application/json";
            ChildUpdateRequest.Timeout = 10000;
            //ChildUpdateRequest.KeepAlive = false;

            // Fill it in
            OSDMap args = null;
            try
            {
                args = cAgentData.Pack();
            }
            catch (Exception e)
            {
                m_log.Debug("[REMOTE SIMULATION CONNECTOR]: PackUpdateMessage failed with exception: " + e.Message);
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
                //m_log.InfoFormat("[REMOTE SIMULATION CONNECTOR]: Posted ChildAgentUpdate request to remote sim {0}", uri);
            }
            //catch (WebException ex)
            catch
            {
                //m_log.InfoFormat("[REMOTE SIMULATION CONNECTOR]: Bad send on ChildAgentUpdate {0}", ex.Message);

                return false;
            }
            finally
            {
                if (os != null)
                    os.Close();
            }

            // Let's wait for the response
            //m_log.Info("[REMOTE SIMULATION CONNECTOR]: Waiting for a reply after ChildAgentUpdate");

            WebResponse webResponse = null;
            StreamReader sr = null;
            try
            {
                webResponse = ChildUpdateRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REMOTE SIMULATION CONNECTOR]: Null reply on ChilAgentUpdate post");
                }

                sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REMOTE SIMULATION CONNECTOR]: ChilAgentUpdate reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REMOTE SIMULATION CONNECTOR]: exception on reply of ChilAgentUpdate {0}", ex.Message);
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
            string uri = "http://" + destination.ExternalEndPoint.Address + ":" + destination.HttpPort + "/agent/" + id + "/" + destination.RegionID.ToString() + "/";
            //Console.WriteLine("   >>> DoRetrieveRootAgentCall <<< " + uri);

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
                    m_log.Info("[REMOTE SIMULATION CONNECTOR]: Null reply on agent get ");
                }

                sr = new StreamReader(webResponse.GetResponseStream());
                reply = sr.ReadToEnd().Trim();

                //Console.WriteLine("[REMOTE SIMULATION CONNECTOR]: ChilAgentUpdate reply was " + reply);

            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REMOTE SIMULATION CONNECTOR]: exception on reply of agent get {0}", ex.Message);
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
                    //Console.WriteLine("[REMOTE SIMULATION CONNECTOR]: Error getting OSDMap from reply");
                    return false;
                }

                agent = new CompleteAgentData();
                agent.Unpack(args);
                return true;
            }

            //Console.WriteLine("[REMOTE SIMULATION CONNECTOR]: DoRetrieveRootAgentCall returned status " + webResponse.StatusCode);
            return false;
        }

        public bool ReleaseAgent(GridRegion destination, UUID id, string uri)
        {
            return false;
        }

        public bool CloseAgent(GridRegion destination, UUID id)
        {
            return false;
        }

        #endregion Agents

        #region Objects

        public bool CreateObject(GridRegion destination, ISceneObject sog, bool isLocalCall)
        {
            return false;
        }

        public bool CreateObject(GridRegion destination, UUID userID, UUID itemID)
        {
            return false;
        }

        #endregion Objects
    }
}
