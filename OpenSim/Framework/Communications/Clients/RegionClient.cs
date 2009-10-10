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

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using GridRegion = OpenSim.Services.Interfaces.GridRegion;

using log4net;

namespace OpenSim.Framework.Communications.Clients
{
    public class RegionClient
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public bool DoCreateChildAgentCall(GridRegion region, AgentCircuitData aCircuit, string authKey, out string reason)
        {
            reason = String.Empty;

            // Eventually, we want to use a caps url instead of the agentID
            string uri = string.Empty;
            try
            {
                uri = "http://" + region.ExternalEndPoint.Address + ":" + region.HttpPort + "/agent/" + aCircuit.AgentID + "/";
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
            AgentCreateRequest.Headers.Add("Authorization", authKey);

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
                            OSDMap r = GetOSDMap(response);
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

        public bool DoChildAgentUpdateCall(GridRegion region, IAgentData cAgentData)
        {
            // Eventually, we want to use a caps url instead of the agentID
            string uri = string.Empty;
            try
            {
                uri = "http://" + region.ExternalEndPoint.Address + ":" + region.HttpPort + "/agent/" + cAgentData.AgentID + "/";
            }
            catch (Exception e)
            {
                m_log.Debug("[REST COMMS]: Unable to resolve external endpoint on agent update. Reason: " + e.Message);
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
                Encoding str = Util.UTF8;
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
                //m_log.InfoFormat("[REST COMMS]: Posted ChildAgentUpdate request to remote sim {0}", uri);
            }
            //catch (WebException ex)
            catch
            {
                //m_log.InfoFormat("[REST COMMS]: Bad send on ChildAgentUpdate {0}", ex.Message);

                return false;
            }
            finally
            {
                if (os != null)
                    os.Close();
            }

            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after ChildAgentUpdate");

            WebResponse webResponse = null;
            StreamReader sr = null;
            try
            {
                webResponse = ChildUpdateRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on ChilAgentUpdate post");
                }

                sr = new StreamReader(webResponse.GetResponseStream());
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
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            return true;
        }

        public bool DoRetrieveRootAgentCall(GridRegion region, UUID id, out IAgentData agent)
        {
            agent = null;
            // Eventually, we want to use a caps url instead of the agentID
            string uri = "http://" + region.ExternalEndPoint.Address + ":" + region.HttpPort + "/agent/" + id + "/" + region.RegionHandle.ToString() + "/";
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
                    m_log.Info("[REST COMMS]: Null reply on agent get ");
                }

                sr = new StreamReader(webResponse.GetResponseStream());
                reply = sr.ReadToEnd().Trim();

                //Console.WriteLine("[REST COMMS]: ChilAgentUpdate reply was " + reply);

            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of agent get {0}", ex.Message);
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
                OSDMap args = GetOSDMap(reply);
                if (args == null)
                {
                    //Console.WriteLine("[REST COMMS]: Error getting OSDMap from reply");
                    return false;
                }

                agent = new CompleteAgentData();
                agent.Unpack(args);
                return true;
            }

            //Console.WriteLine("[REST COMMS]: DoRetrieveRootAgentCall returned status " + webResponse.StatusCode);
            return false;
        }

        public bool DoReleaseAgentCall(ulong regionHandle, UUID id, string uri)
        {
            //m_log.Debug("   >>> DoReleaseAgentCall <<< " + uri);

            WebRequest request = WebRequest.Create(uri);
            request.Method = "DELETE";
            request.Timeout = 10000;

            StreamReader sr = null;
            try
            {
                WebResponse webResponse = request.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on agent delete ");
                }

                sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REST COMMS]: ChilAgentUpdate reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of agent delete {0}", ex.Message);
                // ignore, really
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            return true;
        }


        public bool DoCloseAgentCall(GridRegion region, UUID id)
        {
            string uri = string.Empty;
            try
            {
                uri = "http://" + region.ExternalEndPoint.Address + ":" + region.HttpPort + "/agent/" + id + "/" + region.RegionHandle.ToString() + "/";
            }
            catch (Exception e)
            {
                m_log.Debug("[REST COMMS]: Unable to resolve external endpoint on agent close. Reason: " + e.Message);
                return false;
            }

            //Console.WriteLine("   >>> DoCloseAgentCall <<< " + uri);

            WebRequest request = WebRequest.Create(uri);
            request.Method = "DELETE";
            request.Timeout = 10000;

            StreamReader sr = null;
            try
            {
                WebResponse webResponse = request.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on agent delete ");
                }

                sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                sr.Close();
                //m_log.InfoFormat("[REST COMMS]: ChilAgentUpdate reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of agent delete {0}", ex.Message);
                // ignore, really
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            return true;
        }

        public bool DoCreateObjectCall(GridRegion region, ISceneObject sog, string sogXml2, bool allowScriptCrossing)
        {
            ulong regionHandle = GetRegionHandle(region.RegionHandle);
            string uri 
                = "http://" + region.ExternalEndPoint.Address + ":" + region.HttpPort 
                    + "/object/" + sog.UUID + "/" + regionHandle.ToString() + "/";
            //m_log.Debug("   >>> DoCreateChildAgentCall <<< " + uri);

            WebRequest ObjectCreateRequest = WebRequest.Create(uri);
            ObjectCreateRequest.Method = "POST";
            ObjectCreateRequest.ContentType = "application/json";
            ObjectCreateRequest.Timeout = 10000;

            OSDMap args = new OSDMap(2);
            args["sog"] = OSD.FromString(sogXml2);
            args["extra"] = OSD.FromString(sog.ExtraToXmlString());
            if (allowScriptCrossing)
            {
                string state = sog.GetStateSnapshot();
                if (state.Length > 0)
                    args["state"] = OSD.FromString(state);
            }

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
                m_log.WarnFormat("[REST COMMS]: Exception thrown on serialization of CreateObject: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                ObjectCreateRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = ObjectCreateRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                m_log.InfoFormat("[REST COMMS]: Posted ChildAgentUpdate request to remote sim {0}", uri);
            }
            //catch (WebException ex)
            catch
            {
                // m_log.InfoFormat("[REST COMMS]: Bad send on CreateObject {0}", ex.Message);

                return false;
            }
            finally
            {
                if (os != null)
                    os.Close();
            }

            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after DoCreateChildAgentCall");

            StreamReader sr = null;
            try
            {
                WebResponse webResponse = ObjectCreateRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on DoCreateObjectCall post");
                }

                sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                //m_log.InfoFormat("[REST COMMS]: DoCreateChildAgentCall reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of DoCreateObjectCall {0}", ex.Message);
                // ignore, really
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            return true;

        }

        public bool DoCreateObjectCall(GridRegion region, UUID userID, UUID itemID)
        {
            ulong regionHandle = GetRegionHandle(region.RegionHandle);
            string uri = "http://" + region.ExternalEndPoint.Address + ":" + region.HttpPort + "/object/" + UUID.Zero + "/" + regionHandle.ToString() + "/";
            //m_log.Debug("   >>> DoCreateChildAgentCall <<< " + uri);

            WebRequest ObjectCreateRequest = WebRequest.Create(uri);
            ObjectCreateRequest.Method = "PUT";
            ObjectCreateRequest.ContentType = "application/json";
            ObjectCreateRequest.Timeout = 10000;

            OSDMap args = new OSDMap(2);
            args["userid"] = OSD.FromUUID(userID);
            args["itemid"] = OSD.FromUUID(itemID);

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
                m_log.WarnFormat("[REST COMMS]: Exception thrown on serialization of CreateObject: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                ObjectCreateRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = ObjectCreateRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                //m_log.InfoFormat("[REST COMMS]: Posted CreateObject request to remote sim {0}", uri);
            }
            //catch (WebException ex)
            catch
            {
                // m_log.InfoFormat("[REST COMMS]: Bad send on CreateObject {0}", ex.Message);

                return false;
            }
            finally
            {
                if (os != null)
                    os.Close();
            }

            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after DoCreateChildAgentCall");

            StreamReader sr = null;
            try
            {
                WebResponse webResponse = ObjectCreateRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on DoCreateObjectCall post");
                }

                sr = new StreamReader(webResponse.GetResponseStream());
                sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();

                //m_log.InfoFormat("[REST COMMS]: DoCreateChildAgentCall reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of DoCreateObjectCall {0}", ex.Message);
                // ignore, really
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            return true;

        }

        public bool DoHelloNeighbourCall(RegionInfo region, RegionInfo thisRegion)
        {
            string uri = "http://" + region.ExternalEndPoint.Address + ":" + region.HttpPort + "/region/" + thisRegion.RegionID + "/";
            //m_log.Debug("   >>> DoHelloNeighbourCall <<< " + uri);

            WebRequest HelloNeighbourRequest = WebRequest.Create(uri);
            HelloNeighbourRequest.Method = "POST";
            HelloNeighbourRequest.ContentType = "application/json";
            HelloNeighbourRequest.Timeout = 10000;

            // Fill it in
            OSDMap args = null;
            try
            {
                args = thisRegion.PackRegionInfoData();
            }
            catch (Exception e)
            {
                m_log.Debug("[REST COMMS]: PackRegionInfoData failed with exception: " + e.Message);
            }
            // Add the regionhandle of the destination region
            ulong regionHandle = GetRegionHandle(region.RegionHandle);
            args["destination_handle"] = OSD.FromString(regionHandle.ToString());

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
                m_log.WarnFormat("[REST COMMS]: Exception thrown on serialization of HelloNeighbour: {0}", e.Message);
                // ignore. buffer will be empty, caller should check.
            }

            Stream os = null;
            try
            { // send the Post
                HelloNeighbourRequest.ContentLength = buffer.Length;   //Count bytes to send
                os = HelloNeighbourRequest.GetRequestStream();
                os.Write(buffer, 0, strBuffer.Length);         //Send it
                //m_log.InfoFormat("[REST COMMS]: Posted HelloNeighbour request to remote sim {0}", uri);
            }
            //catch (WebException ex)
            catch
            {
                //m_log.InfoFormat("[REST COMMS]: Bad send on HelloNeighbour {0}", ex.Message);

                return false;
            }
            finally
            {
                if (os != null)
                    os.Close();
            }
            // Let's wait for the response
            //m_log.Info("[REST COMMS]: Waiting for a reply after DoHelloNeighbourCall");

            StreamReader sr = null;
            try
            {
                WebResponse webResponse = HelloNeighbourRequest.GetResponse();
                if (webResponse == null)
                {
                    m_log.Info("[REST COMMS]: Null reply on DoHelloNeighbourCall post");
                }

                sr = new StreamReader(webResponse.GetResponseStream());
                //reply = sr.ReadToEnd().Trim();
                sr.ReadToEnd().Trim();
                //m_log.InfoFormat("[REST COMMS]: DoHelloNeighbourCall reply was {0} ", reply);

            }
            catch (WebException ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on reply of DoHelloNeighbourCall {0}", ex.Message);
                // ignore, really
            }
            finally
            {
                if (sr != null)
                    sr.Close();
            }

            return true;

        }

        #region Hyperlinks

        public virtual ulong GetRegionHandle(ulong handle)
        {
            return handle;
        }

        public virtual bool IsHyperlink(ulong handle)
        {
            return false;
        }

        public virtual void SendUserInformation(GridRegion regInfo, AgentCircuitData aCircuit)
        {
        }

        public virtual void AdjustUserInformation(AgentCircuitData aCircuit)
        {
        }

        #endregion /* Hyperlinks */

        public static OSDMap GetOSDMap(string data)
        {
            OSDMap args = null;
            try
            {
                OSD buffer;
                // We should pay attention to the content-type, but let's assume we know it's Json
                buffer = OSDParser.DeserializeJson(data);
                if (buffer.Type == OSDType.Map)
                {
                    args = (OSDMap)buffer;
                    return args;
                }
                else
                {
                    // uh?
                    System.Console.WriteLine("[REST COMMS]: Got OSD of type " + buffer.Type.ToString());
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("[REST COMMS]: exception on parse of REST message " + ex.Message);
                return null;
            }
        }


    }
}
