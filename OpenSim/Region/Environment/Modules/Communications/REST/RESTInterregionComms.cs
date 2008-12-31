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
                if ((startupConfig == null) || 
                    (startupConfig != null) && (startupConfig.GetString("InterregionComms", "RESTCommms") == "RESTComms"))
                {
                    m_log.Debug("[REST COMMS]: Enabling InterregionComms RESTComms module");
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
            m_aScene.AddHTTPHandler("/ChildAgentUpdate/", ChildAgentUpdateHandler);
        }

        #endregion /* IRegionModule */

        #region IInterregionComms

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

            return false;

        }

        protected bool DoChildAgentUpdateCall(RegionInfo region, AgentData cAgentData)
        {
            string uri = "http://" + region.ExternalEndPoint.Address + ":" + region.HttpPort + "/ChildAgentUpdate/";
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
            ulong regionHandle = GetRegionHandle(region);
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
                m_log.WarnFormat("[OSG2]: Exception thrown on serialization of ChildUpdate: {0}", e.Message);
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

        #endregion /* IInterregionComms */

        #region Called from remote instances on this instance

        public Hashtable ChildAgentUpdateHandler(Hashtable request)
        {
            //m_log.Debug("[CONNECTION DEBUGGING]: ChildDataUpdateHandler Called");

            Hashtable responsedata = new Hashtable();
            responsedata["content_type"] = "text/html";

            OSDMap args = null;
            try
            {
                OSD buffer;
                // We should pay attention to the content-type, but let's assume we know it's Json
                buffer = OSDParser.DeserializeJson((string)request["body"]);
                if (buffer.Type == OSDType.Map)
                    args = (OSDMap)buffer;
                else
                {
                    // uh?
                    m_log.Debug("[REST COMMS]: Got OSD of type " + buffer.Type.ToString());
                }
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on parse of ChildAgentUpdate message {0}", ex.Message);
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

            // retrieve the regionhandle
            ulong regionhandle = 0;
            if (args["destination_handle"] != null)
                UInt64.TryParse(args["destination_handle"].AsString(), out regionhandle);

            AgentData agent = new AgentData();
            try
            {
                agent.UnpackUpdateMessage(args);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on unpacking ChildAgentUpdate message {0}", ex.Message);
            }
            //agent.Dump();

            bool result = m_localBackend.SendChildAgentUpdate(regionhandle, agent);


            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = result.ToString();
            return responsedata;
        }

        #endregion 

        #region Misc

        protected virtual ulong GetRegionHandle(RegionInfo region)
        {
            if (m_aScene.SceneGridService is HGSceneCommunicationService)
                return ((HGSceneCommunicationService)(m_aScene.SceneGridService)).m_hg.FindRegionHandle(region.RegionHandle);

            return region.RegionHandle;
        }

        #endregion /* Misc */

    }
}
