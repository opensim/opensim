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

        private GridRegion m_Region;

        public SimulationServiceConnector()
        {
        }

        public SimulationServiceConnector(GridRegion region)
        {
            m_Region = region;
        }

        public IScene GetScene(ulong regionHandle)
        {
            return null;
        }

        #region Agents

        public bool CreateAgent(ulong regionHandle, AgentCircuitData aCircuit, uint flags, out string reason)
        {
            reason = String.Empty;

            // Eventually, we want to use a caps url instead of the agentID
            string uri = string.Empty;
            try
            {
                uri = "http://" + m_Region.ExternalEndPoint.Address + ":" + m_Region.HttpPort + "/agent/" + aCircuit.AgentID + "/";
            }
            catch (Exception e)
            {
                m_log.Debug("[REST COMMS]: Unable to resolve external endpoint on agent create. Reason: " + e.Message);
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
                m_log.Debug("[REST COMMS]: PackAgentCircuitData failed with exception: " + e.Message);
            }
            // Add the regionhandle and the name of the destination region
            args["destination_handle"] = OSD.FromString(m_Region.RegionHandle.ToString());
            args["destination_name"] = OSD.FromString(m_Region.RegionName);
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
                m_log.WarnFormat("[REST COMMS]: Exception thrown on serialization of ChildCreate: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                AgentCreateRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = AgentCreateRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                //m_log.InfoFormat("[REST COMMS]: Posted CreateChildAgent request to remote sim {0}", uri);
            }
            //catch (WebException ex)
            catch
            {
                //m_log.InfoFormat("[REST COMMS]: Bad send on ChildAgentUpdate {0}", ex.Message);
                reason = "cannot contact remote region";
                return false;
            }
            finally
            {
                if (os != null)
                    os.Close();
            }

            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after DoCreateChildAgentCall");

            WebResponse webResponse = null;
            StreamReader sr = null;
            try
            {
                webResponse = AgentCreateRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on DoCreateChildAgentCall post");
                }
                else
                {

                    sr = new StreamReader(webResponse.GetResponseStream());
                    string response = sr.ReadToEnd().Trim();
                    m_log.InfoFormat("[REST COMMS]: DoCreateChildAgentCall reply was {0} ", response);

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
                            m_log.InfoFormat("[REST COMMS]: exception on reply of DoCreateChildAgentCall {0}", e.Message);

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
                m_log.InfoFormat("[REST COMMS]: exception on reply of DoCreateChildAgentCall {0}", ex.Message);
                // ignore, really
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            return true;
        }

        public bool UpdateAgent(ulong regionHandle, AgentData data)
        {
            return false;
        }

        public bool UpdateAgent(ulong regionHandle, AgentPosition data)
        {
            return false;
        }

        public bool RetrieveAgent(ulong regionHandle, UUID id, out IAgentData agent)
        {
            agent = null;
            return false;
        }

        public bool ReleaseAgent(ulong regionHandle, UUID id, string uri)
        {
            return false;
        }

        public bool CloseAgent(ulong regionHandle, UUID id)
        {
            return false;
        }

        #endregion Agents

        #region Objects

        public bool CreateObject(ulong regionHandle, ISceneObject sog, bool isLocalCall)
        {
            return false;
        }

        public bool CreateObject(ulong regionHandle, UUID userID, UUID itemID)
        {
            return false;
        }

        #endregion Objects
    }
}
