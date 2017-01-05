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
using System.IO;
using System.Net;
using System.Net.Security;
using System.Web;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;
using Nini.Config;
using Nwc.XmlRpc;
using OpenSim.Framework;
using Mono.Addins;

using OpenSim.Framework.Capabilities;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps = OpenSim.Framework.Capabilities.Caps;
using System.Text.RegularExpressions;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OSDMap = OpenMetaverse.StructuredData.OSDMap;

namespace OpenSim.Region.OptionalModules.Avatar.Voice.FreeSwitchVoice
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "FreeSwitchVoiceModule")]
    public class FreeSwitchVoiceModule : ISharedRegionModule, IVoiceModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        // Capability string prefixes
        private static readonly string m_parcelVoiceInfoRequestPath = "0207/";
        private static readonly string m_provisionVoiceAccountRequestPath = "0208/";
        //private static readonly string m_chatSessionRequestPath = "0209/";

        // Control info
        private static bool   m_Enabled  = false;

        // FreeSwitch server is going to contact us and ask us all
        // sorts of things.

        // SLVoice client will do a GET on this prefix
        private static string m_freeSwitchAPIPrefix;

        // We need to return some information to SLVoice
        // figured those out via curl
        // http://vd1.vivox.com/api2/viv_get_prelogin.php
        //
        // need to figure out whether we do need to return ALL of
        // these...
        private static string m_freeSwitchRealm;
        private static string m_freeSwitchSIPProxy;
        private static bool m_freeSwitchAttemptUseSTUN;
        private static string m_freeSwitchEchoServer;
        private static int m_freeSwitchEchoPort;
        private static string m_freeSwitchDefaultWellKnownIP;
        private static int m_freeSwitchDefaultTimeout;
        private static string m_freeSwitchUrlResetPassword;
        private uint m_freeSwitchServicePort;
        private string m_openSimWellKnownHTTPAddress;
//        private string m_freeSwitchContext;

        private readonly Dictionary<string, string> m_UUIDName = new Dictionary<string, string>();
        private Dictionary<string, string> m_ParcelAddress = new Dictionary<string, string>();

        private IConfig m_Config;

        private IFreeswitchService m_FreeswitchService;

        public void Initialise(IConfigSource config)
        {
            m_Config = config.Configs["FreeSwitchVoice"];

            if (m_Config == null)
                return;

            if (!m_Config.GetBoolean("Enabled", false))
                return;

            try
            {
                string serviceDll = m_Config.GetString("LocalServiceModule",
                        String.Empty);

                if (serviceDll == String.Empty)
                {
                    m_log.Error("[FreeSwitchVoice]: No LocalServiceModule named in section FreeSwitchVoice.  Not starting.");
                    return;
                }

                Object[] args = new Object[] { config };
                m_FreeswitchService = ServerUtils.LoadPlugin<IFreeswitchService>(serviceDll, args);

                string jsonConfig = m_FreeswitchService.GetJsonConfig();
                //m_log.Debug("[FreeSwitchVoice]: Configuration string: " + jsonConfig);
                OSDMap map = (OSDMap)OSDParser.DeserializeJson(jsonConfig);

                m_freeSwitchAPIPrefix = map["APIPrefix"].AsString();
                m_freeSwitchRealm = map["Realm"].AsString();
                m_freeSwitchSIPProxy = map["SIPProxy"].AsString();
                m_freeSwitchAttemptUseSTUN = map["AttemptUseSTUN"].AsBoolean();
                m_freeSwitchEchoServer = map["EchoServer"].AsString();
                m_freeSwitchEchoPort = map["EchoPort"].AsInteger();
                m_freeSwitchDefaultWellKnownIP = map["DefaultWellKnownIP"].AsString();
                m_freeSwitchDefaultTimeout = map["DefaultTimeout"].AsInteger();
                m_freeSwitchUrlResetPassword = String.Empty;
//                m_freeSwitchContext = map["Context"].AsString();

                if (String.IsNullOrEmpty(m_freeSwitchRealm) ||
                    String.IsNullOrEmpty(m_freeSwitchAPIPrefix))
                {
                    m_log.Error("[FreeSwitchVoice]: Freeswitch service mis-configured.  Not starting.");
                    return;
                }

                // set up http request handlers for
                // - prelogin: viv_get_prelogin.php
                // - signin: viv_signin.php
                // - buddies: viv_buddy.php
                // - ???: viv_watcher.php
                // - signout: viv_signout.php
                MainServer.Instance.AddHTTPHandler(String.Format("{0}/viv_get_prelogin.php", m_freeSwitchAPIPrefix),
                                                     FreeSwitchSLVoiceGetPreloginHTTPHandler);

                MainServer.Instance.AddHTTPHandler(String.Format("{0}/freeswitch-config", m_freeSwitchAPIPrefix), FreeSwitchConfigHTTPHandler);

                // RestStreamHandler h = new
                // RestStreamHandler("GET",
                // String.Format("{0}/viv_get_prelogin.php", m_freeSwitchAPIPrefix), FreeSwitchSLVoiceGetPreloginHTTPHandler);
                //  MainServer.Instance.AddStreamHandler(h);

                MainServer.Instance.AddHTTPHandler(String.Format("{0}/viv_signin.php", m_freeSwitchAPIPrefix),
                                 FreeSwitchSLVoiceSigninHTTPHandler);

                MainServer.Instance.AddHTTPHandler(String.Format("{0}/viv_buddy.php", m_freeSwitchAPIPrefix),
                                 FreeSwitchSLVoiceBuddyHTTPHandler);

                MainServer.Instance.AddHTTPHandler(String.Format("{0}/viv_watcher.php", m_freeSwitchAPIPrefix),
                                 FreeSwitchSLVoiceWatcherHTTPHandler);

                m_log.InfoFormat("[FreeSwitchVoice]: using FreeSwitch server {0}", m_freeSwitchRealm);

                m_Enabled = true;

                m_log.Info("[FreeSwitchVoice]: plugin enabled");
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[FreeSwitchVoice]: plugin initialization failed: {0} {1}", e.Message, e.StackTrace);
                return;
            }

            // This here is a region module trying to make a global setting.
            // Not really a good idea but it's Windows only, so I can't test.
            try
            {
                ServicePointManager.ServerCertificateValidationCallback += CustomCertificateValidation;
            }
            catch (NotImplementedException)
            {
                try
                {
#pragma warning disable 0612, 0618
                    // Mono does not implement the ServicePointManager.ServerCertificateValidationCallback yet!  Don't remove this!
                    ServicePointManager.CertificatePolicy = new MonoCert();
#pragma warning restore 0612, 0618
                }
                catch (Exception)
                {
                    // COmmented multiline spam log message
                    //m_log.Error("[FreeSwitchVoice]: Certificate validation handler change not supported.  You may get ssl certificate validation errors teleporting from your region to some SSL regions.");
                }
            }
        }

        public void PostInitialise()
        {
        }

        public void AddRegion(Scene scene)
        {
            // We generate these like this: The region's external host name
            // as defined in Regions.ini is a good address to use. It's a
            // dotted quad (or should be!) and it can reach this host from
            // a client. The port is grabbed from the region's HTTP server.
            m_openSimWellKnownHTTPAddress = scene.RegionInfo.ExternalHostName;
            m_freeSwitchServicePort = MainServer.Instance.Port;

            if (m_Enabled)
            {
                // we need to capture scene in an anonymous method
                // here as we need it later in the callbacks
                scene.EventManager.OnRegisterCaps += delegate(UUID agentID, Caps caps)
                    {
                        OnRegisterCaps(scene, agentID, caps);
                    };
            }
        }

        public void RemoveRegion(Scene scene)
        {
        }

        public void RegionLoaded(Scene scene)
        {
            if (m_Enabled)
            {
                m_log.Info("[FreeSwitchVoice]: registering IVoiceModule with the scene");

                // register the voice interface for this module, so the script engine can call us
                scene.RegisterModuleInterface<IVoiceModule>(this);
            }
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "FreeSwitchVoiceModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        // <summary>
        // implementation of IVoiceModule, called by osSetParcelSIPAddress script function
        // </summary>
        public void setLandSIPAddress(string SIPAddress,UUID GlobalID)
        {
            m_log.DebugFormat("[FreeSwitchVoice]: setLandSIPAddress parcel id {0}: setting sip address {1}",
                                  GlobalID, SIPAddress);

            lock (m_ParcelAddress)
            {
                if (m_ParcelAddress.ContainsKey(GlobalID.ToString()))
                {
                    m_ParcelAddress[GlobalID.ToString()] = SIPAddress;
                }
                else
                {
                    m_ParcelAddress.Add(GlobalID.ToString(), SIPAddress);
                }
            }
        }

        // <summary>
        // OnRegisterCaps is invoked via the scene.EventManager
        // everytime OpenSim hands out capabilities to a client
        // (login, region crossing). We contribute two capabilities to
        // the set of capabilities handed back to the client:
        // ProvisionVoiceAccountRequest and ParcelVoiceInfoRequest.
        //
        // ProvisionVoiceAccountRequest allows the client to obtain
        // the voice account credentials for the avatar it is
        // controlling (e.g., user name, password, etc).
        //
        // ParcelVoiceInfoRequest is invoked whenever the client
        // changes from one region or parcel to another.
        //
        // Note that OnRegisterCaps is called here via a closure
        // delegate containing the scene of the respective region (see
        // Initialise()).
        // </summary>
        public void OnRegisterCaps(Scene scene, UUID agentID, Caps caps)
        {
            m_log.DebugFormat(
                "[FreeSwitchVoice]: OnRegisterCaps() called with agentID {0} caps {1} in scene {2}",
                agentID, caps, scene.RegionInfo.RegionName);

            string capsBase = "/CAPS/" + caps.CapsObjectPath;
            caps.RegisterHandler(
                "ProvisionVoiceAccountRequest",
                new RestStreamHandler(
                    "POST",
                    capsBase + m_provisionVoiceAccountRequestPath,
                    (request, path, param, httpRequest, httpResponse)
                        => ProvisionVoiceAccountRequest(scene, request, path, param, agentID, caps),
                    "ProvisionVoiceAccountRequest",
                    agentID.ToString()));

            caps.RegisterHandler(
                "ParcelVoiceInfoRequest",
                new RestStreamHandler(
                    "POST",
                    capsBase + m_parcelVoiceInfoRequestPath,
                        (request, path, param, httpRequest, httpResponse)
                            => ParcelVoiceInfoRequest(scene, request, path, param, agentID, caps),
                    "ParcelVoiceInfoRequest",
                    agentID.ToString()));

            //caps.RegisterHandler(
            //    "ChatSessionRequest",
            //    new RestStreamHandler(
            //        "POST",
            //        capsBase + m_chatSessionRequestPath,
            //                (request, path, param, httpRequest, httpResponse)
            //                    => ChatSessionRequest(scene, request, path, param, agentID, caps),
            //        "ChatSessionRequest",
            //        agentID.ToString()));
        }

        /// <summary>
        /// Callback for a client request for Voice Account Details
        /// </summary>
        /// <param name="scene">current scene object of the client</param>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="agentID"></param>
        /// <param name="caps"></param>
        /// <returns></returns>
        public string ProvisionVoiceAccountRequest(Scene scene, string request, string path, string param,
                                                   UUID agentID, Caps caps)
        {
            m_log.DebugFormat(
                "[FreeSwitchVoice][PROVISIONVOICE]: ProvisionVoiceAccountRequest() request: {0}, path: {1}, param: {2}", request, path, param);

            ScenePresence avatar = scene.GetScenePresence(agentID);
            if (avatar == null)
            {
                System.Threading.Thread.Sleep(2000);
                avatar = scene.GetScenePresence(agentID);

                if (avatar == null)
                    return "<llsd>undef</llsd>";
            }
            string avatarName = avatar.Name;

            try
            {
                //XmlElement    resp;
                string agentname = "x" + Convert.ToBase64String(agentID.GetBytes());
                string password  = "1234";//temp hack//new UUID(Guid.NewGuid()).ToString().Replace('-','Z').Substring(0,16);

                // XXX: we need to cache the voice credentials, as
                // FreeSwitch is later going to come and ask us for
                // those
                agentname = agentname.Replace('+', '-').Replace('/', '_');

                lock (m_UUIDName)
                {
                    if (m_UUIDName.ContainsKey(agentname))
                    {
                        m_UUIDName[agentname] = avatarName;
                    }
                    else
                    {
                        m_UUIDName.Add(agentname, avatarName);
                    }
                }

                // LLSDVoiceAccountResponse voiceAccountResponse =
               //     new LLSDVoiceAccountResponse(agentname, password, m_freeSwitchRealm, "http://etsvc02.hursley.ibm.com/api");
               LLSDVoiceAccountResponse voiceAccountResponse =
                   new LLSDVoiceAccountResponse(agentname, password, m_freeSwitchRealm,
                                                String.Format("http://{0}:{1}{2}/", m_openSimWellKnownHTTPAddress,
                                                              m_freeSwitchServicePort, m_freeSwitchAPIPrefix));

                string r = LLSDHelpers.SerialiseLLSDReply(voiceAccountResponse);

//                m_log.DebugFormat("[FreeSwitchVoice][PROVISIONVOICE]: avatar \"{0}\": {1}", avatarName, r);

                return r;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[FreeSwitchVoice][PROVISIONVOICE]: avatar \"{0}\": {1}, retry later", avatarName, e.Message);
                m_log.DebugFormat("[FreeSwitchVoice][PROVISIONVOICE]: avatar \"{0}\": {1} failed", avatarName, e.ToString());

                return "<llsd>undef</llsd>";
            }
        }

        /// <summary>
        /// Callback for a client request for ParcelVoiceInfo
        /// </summary>
        /// <param name="scene">current scene object of the client</param>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="agentID"></param>
        /// <param name="caps"></param>
        /// <returns></returns>
        public string ParcelVoiceInfoRequest(Scene scene, string request, string path, string param,
                                             UUID agentID, Caps caps)
        {
            m_log.DebugFormat(
                "[FreeSwitchVoice][PARCELVOICE]: ParcelVoiceInfoRequest() on {0} for {1}",
                scene.RegionInfo.RegionName, agentID);

            ScenePresence avatar = scene.GetScenePresence(agentID);
            string avatarName = avatar.Name;

            // - check whether we have a region channel in our cache
            // - if not:
            //       create it and cache it
            // - send it to the client
            // - send channel_uri: as "sip:regionID@m_sipDomain"
            try
            {
                LLSDParcelVoiceInfoResponse parcelVoiceInfo;
                string channelUri;

                if (null == scene.LandChannel)
                    throw new Exception(String.Format("region \"{0}\": avatar \"{1}\": land data not yet available",
                                                      scene.RegionInfo.RegionName, avatarName));

                // get channel_uri: check first whether estate
                // settings allow voice, then whether parcel allows
                // voice, if all do retrieve or obtain the parcel
                // voice channel
                LandData land = scene.GetLandData(avatar.AbsolutePosition);

                //m_log.DebugFormat("[FreeSwitchVoice][PARCELVOICE]: region \"{0}\": Parcel \"{1}\" ({2}): avatar \"{3}\": request: {4}, path: {5}, param: {6}",
                //                  scene.RegionInfo.RegionName, land.Name, land.LocalID, avatarName, request, path, param);

                // TODO: EstateSettings don't seem to get propagated...
                // if (!scene.RegionInfo.EstateSettings.AllowVoice)
                // {
                //     m_log.DebugFormat("[FreeSwitchVoice][PARCELVOICE]: region \"{0}\": voice not enabled in estate settings",
                //                       scene.RegionInfo.RegionName);
                //     channel_uri = String.Empty;
                // }
                // else

                if ((land.Flags & (uint)ParcelFlags.AllowVoiceChat) == 0)
                {
//                    m_log.DebugFormat("[FreeSwitchVoice][PARCELVOICE]: region \"{0}\": Parcel \"{1}\" ({2}): avatar \"{3}\": voice not enabled for parcel",
//                                      scene.RegionInfo.RegionName, land.Name, land.LocalID, avatarName);
                    channelUri = String.Empty;
                }
                else
                {
                    channelUri = ChannelUri(scene, land);
                }

                // fill in our response to the client
                Hashtable creds = new Hashtable();
                creds["channel_uri"] = channelUri;

                parcelVoiceInfo = new LLSDParcelVoiceInfoResponse(scene.RegionInfo.RegionName, land.LocalID, creds);
                string r = LLSDHelpers.SerialiseLLSDReply(parcelVoiceInfo);

//                m_log.DebugFormat("[FreeSwitchVoice][PARCELVOICE]: region \"{0}\": Parcel \"{1}\" ({2}): avatar \"{3}\": {4}",
//                                  scene.RegionInfo.RegionName, land.Name, land.LocalID, avatarName, r);
                return r;
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[FreeSwitchVoice][PARCELVOICE]: region \"{0}\": avatar \"{1}\": {2}, retry later",
                                  scene.RegionInfo.RegionName, avatarName, e.Message);
                m_log.DebugFormat("[FreeSwitchVoice][PARCELVOICE]: region \"{0}\": avatar \"{1}\": {2} failed",
                                  scene.RegionInfo.RegionName, avatarName, e.ToString());

                return "<llsd>undef</llsd>";
            }
        }

        /// <summary>
        /// Callback for a client request for ChatSessionRequest
        /// </summary>
        /// <param name="scene">current scene object of the client</param>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <param name="agentID"></param>
        /// <param name="caps"></param>
        /// <returns></returns>
        public string ChatSessionRequest(Scene scene, string request, string path, string param,
                                         UUID agentID, Caps caps)
        {
            ScenePresence avatar = scene.GetScenePresence(agentID);
            string        avatarName = avatar.Name;

            m_log.DebugFormat("[FreeSwitchVoice][CHATSESSION]: avatar \"{0}\": request: {1}, path: {2}, param: {3}",
                              avatarName, request, path, param);

            return "<llsd>true</llsd>";
        }

        public Hashtable ForwardProxyRequest(Hashtable request)
        {
            m_log.Debug("[PROXYING]: -------------------------------proxying request");
            Hashtable response = new Hashtable();
            response["content_type"] = "text/xml";
            response["str_response_string"] = "";
            response["int_response_code"] = 200;

            string forwardaddress = "https://www.bhr.vivox.com/api2/";
            string body = (string)request["body"];
            string method = (string) request["http-method"];
            string contenttype = (string) request["content-type"];
            string uri = (string) request["uri"];
            uri = uri.Replace("/api/", "");
            forwardaddress += uri;


            string fwdresponsestr = "";
            int fwdresponsecode = 200;
            string fwdresponsecontenttype = "text/xml";

            HttpWebRequest forwardreq = (HttpWebRequest)WebRequest.Create(forwardaddress);
            forwardreq.Method = method;
            forwardreq.ContentType = contenttype;
            forwardreq.KeepAlive = false;

            if (method == "POST")
            {
                byte[] contentreq = Util.UTF8.GetBytes(body);
                forwardreq.ContentLength = contentreq.Length;
                Stream reqStream = forwardreq.GetRequestStream();
                reqStream.Write(contentreq, 0, contentreq.Length);
                reqStream.Close();
            }

            using (HttpWebResponse fwdrsp = (HttpWebResponse)forwardreq.GetResponse())
            {
                Encoding encoding = Util.UTF8;

                using (Stream s = fwdrsp.GetResponseStream())
                {
                    using (StreamReader fwdresponsestream = new StreamReader(s))
                    {
                        fwdresponsestr = fwdresponsestream.ReadToEnd();
                        fwdresponsecontenttype = fwdrsp.ContentType;
                        fwdresponsecode = (int)fwdrsp.StatusCode;
                    }
                }
            }

            response["content_type"] = fwdresponsecontenttype;
            response["str_response_string"] = fwdresponsestr;
            response["int_response_code"] = fwdresponsecode;

            return response;
        }

        public Hashtable FreeSwitchSLVoiceGetPreloginHTTPHandler(Hashtable request)
        {
//            m_log.Debug("[FreeSwitchVoice] FreeSwitchSLVoiceGetPreloginHTTPHandler called");

            Hashtable response = new Hashtable();
            response["content_type"] = "text/xml";
            response["keepalive"] = false;

            response["str_response_string"] = String.Format(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<VCConfiguration>\r\n"+
                    "<DefaultRealm>{0}</DefaultRealm>\r\n" +
                    "<DefaultSIPProxy>{1}</DefaultSIPProxy>\r\n"+
                    "<DefaultAttemptUseSTUN>{2}</DefaultAttemptUseSTUN>\r\n"+
                    "<DefaultEchoServer>{3}</DefaultEchoServer>\r\n"+
                    "<DefaultEchoPort>{4}</DefaultEchoPort>\r\n"+
                    "<DefaultWellKnownIP>{5}</DefaultWellKnownIP>\r\n"+
                    "<DefaultTimeout>{6}</DefaultTimeout>\r\n"+
                    "<UrlResetPassword>{7}</UrlResetPassword>\r\n"+
                    "<UrlPrivacyNotice>{8}</UrlPrivacyNotice>\r\n"+
                    "<UrlEulaNotice/>\r\n"+
                    "<App.NoBottomLogo>false</App.NoBottomLogo>\r\n"+
                "</VCConfiguration>",
                m_freeSwitchRealm, m_freeSwitchSIPProxy, m_freeSwitchAttemptUseSTUN,
                m_freeSwitchEchoServer, m_freeSwitchEchoPort,
                m_freeSwitchDefaultWellKnownIP, m_freeSwitchDefaultTimeout,
                m_freeSwitchUrlResetPassword, "");

            response["int_response_code"] = 200;

            //m_log.DebugFormat("[FreeSwitchVoice] FreeSwitchSLVoiceGetPreloginHTTPHandler return {0}",response["str_response_string"]);
            return response;
        }

        public Hashtable FreeSwitchSLVoiceBuddyHTTPHandler(Hashtable request)
        {
            m_log.Debug("[FreeSwitchVoice]: FreeSwitchSLVoiceBuddyHTTPHandler called");

            Hashtable response = new Hashtable();
            response["int_response_code"] = 200;
            response["str_response_string"] = string.Empty;
            response["content-type"] = "text/xml";

            Hashtable requestBody = ParseRequestBody((string)request["body"]);

            if (!requestBody.ContainsKey("auth_token"))
                return response;

            string auth_token = (string)requestBody["auth_token"];
            //string[] auth_tokenvals = auth_token.Split(':');
            //string username = auth_tokenvals[0];
            int strcount = 0;

            string[] ids = new string[strcount];

            int iter = -1;
            lock (m_UUIDName)
            {
                strcount = m_UUIDName.Count;
                ids = new string[strcount];
                foreach (string s in m_UUIDName.Keys)
                {
                    iter++;
                    ids[iter] = s;
                }
            }
            StringBuilder resp = new StringBuilder();
            resp.Append("<?xml version=\"1.0\" encoding=\"iso-8859-1\" ?><response xmlns=\"http://www.vivox.com\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation= \"/xsd/buddy_list.xsd\">");

            resp.Append(string.Format(@"<level0>
                        <status>OK</status>
                        <cookie_name>lib_session</cookie_name>
                        <cookie>{0}</cookie>
                        <auth_token>{0}</auth_token>
                        <body>
                            <buddies>",auth_token));
            /*
                        <cookie_name>lib_session</cookie_name>
                        <cookie>{0}:{1}:9303959503950::</cookie>
                        <auth_token>{0}:{1}:9303959503950::</auth_token>
            */
            for (int i=0;i<ids.Length;i++)
            {
                DateTime currenttime = DateTime.Now;
                string dt = currenttime.ToString("yyyy-MM-dd HH:mm:ss.0zz");
                resp.Append(
                    string.Format(@"<level3>
                                    <bdy_id>{1}</bdy_id>
                                    <bdy_data></bdy_data>
                                    <bdy_uri>sip:{0}@{2}</bdy_uri>
                                    <bdy_nickname>{0}</bdy_nickname>
                                    <bdy_username>{0}</bdy_username>
                                    <bdy_domain>{2}</bdy_domain>
                                    <bdy_status>A</bdy_status>
                                    <modified_ts>{3}</modified_ts>
                                    <b2g_group_id></b2g_group_id>
                                </level3>", ids[i], i ,m_freeSwitchRealm, dt));
            }

            resp.Append("</buddies><groups></groups></body></level0></response>");

            response["str_response_string"] = resp.ToString();
//            Regex normalizeEndLines = new Regex(@"(\r\n|\n)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);
//
//            m_log.DebugFormat(
//                "[FREESWITCH]: FreeSwitchSLVoiceBuddyHTTPHandler() response {0}",
//                normalizeEndLines.Replace((string)response["str_response_string"],""));

            return response;
        }

        public Hashtable FreeSwitchSLVoiceWatcherHTTPHandler(Hashtable request)
        {
            m_log.Debug("[FreeSwitchVoice]: FreeSwitchSLVoiceWatcherHTTPHandler called");

            Hashtable response = new Hashtable();
            response["int_response_code"] = 200;
            response["content-type"] = "text/xml";

            Hashtable requestBody = ParseRequestBody((string)request["body"]);

            string auth_token = (string)requestBody["auth_token"];
            //string[] auth_tokenvals = auth_token.Split(':');
            //string username = auth_tokenvals[0];

            StringBuilder resp = new StringBuilder();
            resp.Append("<?xml version=\"1.0\" encoding=\"iso-8859-1\" ?><response xmlns=\"http://www.vivox.com\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xsi:schemaLocation= \"/xsd/buddy_list.xsd\">");

            // FIXME: This is enough of a response to stop viewer 2 complaining about a login failure and get voice to work.  If we don't
            // give an OK response, then viewer 2 engages in an continuous viv_signin.php, viv_buddy.php, viv_watcher.php loop
            // Viewer 1 appeared happy to ignore the lack of reply and still works with this reply.
            //
            // However, really we need to fill in whatever watcher data should be here (whatever that is).
            resp.Append(string.Format(@"<level0>
                        <status>OK</status>
                        <cookie_name>lib_session</cookie_name>
                        <cookie>{0}</cookie>
                        <auth_token>{0}</auth_token>
                        <body/></level0></response>", auth_token));

            response["str_response_string"] = resp.ToString();

//            Regex normalizeEndLines = new Regex(@"(\r\n|\n)", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);
//
//            m_log.DebugFormat(
//                "[FREESWITCH]: FreeSwitchSLVoiceWatcherHTTPHandler() response {0}",
//                normalizeEndLines.Replace((string)response["str_response_string"],""));

            return response;
        }

        public Hashtable FreeSwitchSLVoiceSigninHTTPHandler(Hashtable request)
        {
            //m_log.Debug("[FreeSwitchVoice] FreeSwitchSLVoiceSigninHTTPHandler called");
//            string requestbody = (string)request["body"];
//            string uri = (string)request["uri"];
//            string contenttype = (string)request["content-type"];

            Hashtable requestBody = ParseRequestBody((string)request["body"]);

            //string pwd = (string) requestBody["pwd"];
            string userid = (string) requestBody["userid"];

            string avatarName = string.Empty;
            int pos = -1;
            lock (m_UUIDName)
            {
                if (m_UUIDName.ContainsKey(userid))
                {
                    avatarName = m_UUIDName[userid];
                    foreach (string s in m_UUIDName.Keys)
                    {
                        pos++;
                        if (s == userid)
                            break;
                    }
                }
            }

            //m_log.DebugFormat("[FreeSwitchVoice]: AUTH, URI: {0}, Content-Type:{1}, Body{2}", uri, contenttype,
            //                  requestbody);
            Hashtable response = new Hashtable();
            response["str_response_string"] = string.Format(@"<response xsi:schemaLocation=""/xsd/signin.xsd"">
                    <level0>
                        <status>OK</status>
                        <body>
                        <code>200</code>
                        <cookie_name>lib_session</cookie_name>
                        <cookie>{0}:{1}:9303959503950::</cookie>
                        <auth_token>{0}:{1}:9303959503950::</auth_token>
                        <primary>1</primary>
                        <account_id>{1}</account_id>
                        <displayname>{2}</displayname>
                        <msg>auth successful</msg>
                        </body>
                    </level0>
                </response>", userid, pos, avatarName);

            response["int_response_code"] = 200;

//            m_log.DebugFormat("[FreeSwitchVoice]: Sending FreeSwitchSLVoiceSigninHTTPHandler response");

            return response;
        }

        public Hashtable ParseRequestBody(string body)
        {
            Hashtable bodyParams = new Hashtable();
            // split string
            string [] nvps = body.Split(new Char [] {'&'});

            foreach (string s in nvps)
            {
                if (s.Trim() != "")
                {
                    string [] nvp = s.Split(new Char [] {'='});
                    bodyParams.Add(HttpUtility.UrlDecode(nvp[0]), HttpUtility.UrlDecode(nvp[1]));
                }
            }

            return bodyParams;
        }

        private string ChannelUri(Scene scene, LandData land)
        {
            string channelUri = null;

            string landUUID;
            string landName;

            // Create parcel voice channel. If no parcel exists, then the voice channel ID is the same
            // as the directory ID. Otherwise, it reflects the parcel's ID.

            lock (m_ParcelAddress)
            {
                if (m_ParcelAddress.ContainsKey(land.GlobalID.ToString()))
                {
                    m_log.DebugFormat("[FreeSwitchVoice]: parcel id {0}: using sip address {1}",
                                      land.GlobalID, m_ParcelAddress[land.GlobalID.ToString()]);
                    return m_ParcelAddress[land.GlobalID.ToString()];
                }
            }

            if (land.LocalID != 1 && (land.Flags & (uint)ParcelFlags.UseEstateVoiceChan) == 0)
            {
                landName = String.Format("{0}:{1}", scene.RegionInfo.RegionName, land.Name);
                landUUID = land.GlobalID.ToString();
                m_log.DebugFormat("[FreeSwitchVoice]: Region:Parcel \"{0}\": parcel id {1}: using channel name {2}",
                                  landName, land.LocalID, landUUID);
            }
            else
            {
                landName = String.Format("{0}:{1}", scene.RegionInfo.RegionName, scene.RegionInfo.RegionName);
                landUUID = scene.RegionInfo.RegionID.ToString();
                m_log.DebugFormat("[FreeSwitchVoice]: Region:Parcel \"{0}\": parcel id {1}: using channel name {2}",
                                  landName, land.LocalID, landUUID);
            }

            // slvoice handles the sip address differently if it begins with confctl, hiding it from the user in the friends list. however it also disables
            // the personal speech indicators as well unless some siren14-3d codec magic happens. we dont have siren143d so we'll settle for the personal speech indicator.
            channelUri = String.Format("sip:conf-{0}@{1}", "x" + Convert.ToBase64String(Encoding.ASCII.GetBytes(landUUID)), m_freeSwitchRealm);

            lock (m_ParcelAddress)
            {
                if (!m_ParcelAddress.ContainsKey(land.GlobalID.ToString()))
                {
                    m_ParcelAddress.Add(land.GlobalID.ToString(),channelUri);
                }
            }

            return channelUri;
        }

        private static bool CustomCertificateValidation(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors error)
        {
            return true;
        }

        public Hashtable FreeSwitchConfigHTTPHandler(Hashtable request)
        {
            Hashtable response = new Hashtable();
            response["str_response_string"] = string.Empty;
            response["content_type"] = "text/plain";
            response["keepalive"] = false;
            response["int_response_code"] = 500;

            Hashtable requestBody = ParseRequestBody((string)request["body"]);

            string section = (string) requestBody["section"];

            if (section == "directory")
            {
                string eventCallingFunction = (string)requestBody["Event-Calling-Function"];
                m_log.DebugFormat(
                    "[FreeSwitchVoice]: Received request for config section directory, event calling function '{0}'",
                    eventCallingFunction);

                response = m_FreeswitchService.HandleDirectoryRequest(requestBody);
            }
            else if (section == "dialplan")
            {
                m_log.DebugFormat("[FreeSwitchVoice]: Received request for config section dialplan");

                response = m_FreeswitchService.HandleDialplanRequest(requestBody);
            }
            else
                m_log.WarnFormat("[FreeSwitchVoice]: Unknown section {0} was requested from config.", section);

            return response;
        }
    }

    public class MonoCert : ICertificatePolicy
    {
        #region ICertificatePolicy Members

        public bool CheckValidationResult(ServicePoint srvPoint, X509Certificate certificate, WebRequest request, int certificateProblem)
        {
            return true;
        }

        #endregion
    }
}
