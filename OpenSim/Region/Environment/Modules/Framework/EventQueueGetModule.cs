using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Xml;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Communications.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Interfaces;
using OpenSim.Region.Environment.Scenes;

using LLSD = OpenMetaverse.StructuredData.LLSD;
using LLSDMap = OpenMetaverse.StructuredData.LLSDMap;
using LLSDArray = OpenMetaverse.StructuredData.LLSDArray;
using Caps = OpenSim.Framework.Communications.Capabilities.Caps;

namespace OpenSim.Region.Environment.Modules.Framework
{
    public class EventQueueGetModule : IEventQueue, IRegionModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        Scene m_scene = null;
        IConfigSource m_gConfig;

        #region IRegionModule methods
        public void Initialise(Scene scene, IConfigSource config)
        {
            m_gConfig = config;
            m_scene = scene;


            IConfig startupConfig = m_gConfig.Configs["Startup"];

            ReadConfigAndPopulate(scene, startupConfig, "Startup");
           
            scene.RegisterModuleInterface<IEventQueue>(this);

            scene.EventManager.OnNewClient += OnNewClient;
            scene.EventManager.OnClientClosed += ClientClosed;
            scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
            scene.EventManager.OnMakeChildAgent += MakeChildAgent;
            scene.EventManager.OnClientClosed += ClientLoggedOut;
            scene.EventManager.OnRegisterCaps += OnRegisterCaps;
        
        }

        private void ReadConfigAndPopulate(Scene scene, IConfig startupConfig, string p)
        {
            
        }



       

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "EventQueueGetModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }
        #endregion

        #region IEventQueue Members
        public bool Enqueue(object o, UUID avatarID)
        {

            return false;
        }
        #endregion

        private void OnNewClient(IClientAPI client)
        {
            
            client.OnLogout += ClientClosed;
        }


        public void ClientClosed(IClientAPI client)
        {
            ClientClosed(client.AgentId);
        }

        private void ClientLoggedOut(UUID AgentId)
        {
            
        }

        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {

        }

        public void ClientClosed(UUID AgentID)
        {
           
        }
        private void MakeChildAgent(ScenePresence avatar)
        {
        }
        public void OnRegisterCaps(UUID agentID, Caps caps)
        {
            m_log.DebugFormat("[EVENTQUEUE] OnRegisterCaps: agentID {0} caps {1}", agentID, caps);
            string capsBase = "/CAPS/";
            caps.RegisterHandler("EventQueueGet",
                                 new RestHTTPHandler("POST", capsBase + UUID.Random().ToString(),
                                                       delegate(Hashtable m_dhttpMethod)
                                                       {
                                                           return ProcessQueue(m_dhttpMethod,agentID, caps);
                                                       }));
        }
        public Hashtable ProcessQueue(Hashtable request,UUID agentID, Caps caps)
        {
            
            Hashtable responsedata = new Hashtable();
            responsedata["int_response_code"] = 502;
            responsedata["str_response_string"] = "Upstream error:";
            responsedata["content_type"] = "text/plain";
            responsedata["keepalive"] = true;

            /*
            responsedata["int_response_code"] = 200;
            responsedata["content_type"] = "application/xml";
            responsedata["keepalive"] = true;

            responsedata["str_response_string"] = @"<llsd><map><key>events</key><array><map><key>body</key><map><key>AgentData</key><map><key>AgentID</key>
 <uuid>0fd0e798-a54f-40b1-0000-000000000000</uuid><key>SessionID</key><uuid>cc91f1fe-9d52-435d-0000-000000000000
 </uuid></map><key>Info</key><map><key>LookAt</key><array><real>0.9869639873504638671875</real><real>
 -0.1609439998865127563476562</real><real>0</real></array><key>Position</key><array><real>1.43747997283935546875
 </real><real>95.30560302734375</real><real>57.3480987548828125</real></array></map><key>RegionData</key><map>
 <key>RegionHandle</key><binary encoding=" + "\"base64\"" + @">AAPnAAAD8AA=</binary><key>SeedCapability</key><string>
 https://sim7.aditi.lindenlab.com:12043/cap/64015fb3-6fee-9205-0000-000000000000</string><key>SimIP</key><binary
 encoding=" + "\"base64\"" + @">yA8FSA==</binary><key>SimPort</key><integer>13005</integer></map></map><key>message</key>
 <string>CrossedRegion</string></map></array><key>id</key><integer>1</integer></map></llsd>";

             */
            //string requestbody = (string)request["requestbody"];
            //LLSD llsdRequest = LLSDParser.DeserializeXml(request);
            //System.Console.WriteLine(requestbody);
            return responsedata;
            
        }
    }
}
