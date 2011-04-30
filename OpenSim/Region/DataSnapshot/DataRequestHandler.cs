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
*
*/

using System.Collections;
using System.Reflection;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Scenes;
using Caps = OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.DataSnapshot
{
    public class DataRequestHandler
    {
        private Scene m_scene = null;
        private DataSnapshotManager m_externalData = null;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly string m_discoveryPath = "DS0001/";

        public DataRequestHandler(Scene scene, DataSnapshotManager externalData)
        {
            m_scene = scene;
            m_externalData = externalData;

            //Register HTTP handler
            if (MainServer.Instance.AddHTTPHandler("collector", OnGetSnapshot))
            {
                m_log.Info("[DATASNAPSHOT]: Set up snapshot service");
            }

            //Register CAPS handler event
            m_scene.EventManager.OnRegisterCaps += OnRegisterCaps;

            //harbl
        }

        public void OnRegisterCaps(UUID agentID, Caps caps)
        {
            m_log.Info("[DATASNAPSHOT]: Registering service discovery capability for " + agentID);
            string capsBase = "/CAPS/" + caps.CapsObjectPath;
            caps.RegisterHandler("PublicSnapshotDataInfo",
                new RestStreamHandler("POST", capsBase + m_discoveryPath, OnDiscoveryAttempt));
        }

        public string OnDiscoveryAttempt(string request, string path, string param,
                                         OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            //Very static for now, flexible enough to add new formats
            LLSDDiscoveryResponse llsd_response = new LLSDDiscoveryResponse();
            llsd_response.snapshot_resources = new OSDArray();

            LLSDDiscoveryDataURL llsd_dataurl = new LLSDDiscoveryDataURL();
            llsd_dataurl.snapshot_format = "os-datasnapshot-v1";
            llsd_dataurl.snapshot_url = "http://" + m_externalData.m_hostname + ":" + m_externalData.m_listener_port + "/?method=collector";

            llsd_response.snapshot_resources.Array.Add(llsd_dataurl);

            string response = LLSDHelpers.SerialiseLLSDReply(llsd_response);

            return response;
        }

        public Hashtable OnGetSnapshot(Hashtable keysvals)
        {
            m_log.Info("[DATASNAPSHOT] Received collection request");
            Hashtable reply = new Hashtable();
            int statuscode = 200;

            string snapObj = (string)keysvals["region"];

            XmlDocument response = m_externalData.GetSnapshot(snapObj);

            reply["str_response_string"] = response.OuterXml;
            reply["int_response_code"] = statuscode;
            reply["content_type"] = "text/xml";

            return reply;
        }
    }
}
