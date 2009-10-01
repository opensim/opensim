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
 
using log4net;
using System;
using System.Reflection;
using System.Text;
using System.Collections;
 
namespace OpenSim.Region.OptionalModules.Avatar.Voice.FreeSwitchVoice
{
    public class FreeSwitchDirectory 
    {
        private static readonly ILog m_log =
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            
        public Hashtable HandleDirectoryRequest(string Context, string Realm, Hashtable request)
        {
            Hashtable response = new Hashtable();
            string domain = (string) request["domain"];
            if (domain != Realm) {
                response["content_type"] = "text/xml";
                response["keepalive"] = false;
                response["int_response_code"] = 200;
                response["str_response_string"] = "";
            } else {
                 m_log.DebugFormat("[FreeSwitchDirectory] HandleDirectoryRequest called with {0}",request.ToString());
            
                 // information in the request we might be interested in
             
                 // Request 1 sip_auth for users account
             
                 //Event-Calling-Function=sofia_reg_parse_auth
                 //Event-Calling-Line-Number=1494
                 //action=sip_auth
                 //sip_user_agent=Vivox-SDK-2.1.3010.6151-Mac%20(Feb-11-2009/16%3A42%3A41)
                 //sip_auth_username=xhZuXKmRpECyr2AARJYyGgg%3D%3D  (==)
                 //sip_auth_realm=9.20.151.43
                 //sip_contact_user=xhZuXKmRpECyr2AARJYyGgg%3D%3D (==)
                 //sip_contact_host=192.168.0.3    // this shouldnt really be a local IP, investigate STUN servers
                 //sip_to_user=xhZuXKmRpECyr2AARJYyGgg%3D%3D
                 //sip_to_host=9.20.151.43
                 //sip_auth_method=REGISTER
                 //user=xhZuXKmRpECyr2AARJYyGgg%3D%3D
                 //domain=9.20.151.43
                 //ip=9.167.220.137    // this is the correct IP rather than sip_contact_host above when through a vpn or NAT setup
             
                 foreach (DictionaryEntry item in request)
                 {
                    m_log.InfoFormat("[FreeSwitchDirectory] requestBody item {0} {1}", item.Key, item.Value);
                 }
             
                 string eventCallingFunction = (string) request["Event-Calling-Function"];
                 if (eventCallingFunction == null)
                 {
                     eventCallingFunction = "sofia_reg_parse_auth";
                 }

                 if (eventCallingFunction.Length == 0)
                 {
                     eventCallingFunction = "sofia_reg_parse_auth";
                 }
             
                 if (eventCallingFunction == "sofia_reg_parse_auth")
                 {
                     string sipAuthMethod = (string)request["sip_auth_method"];
                 
                     if (sipAuthMethod == "REGISTER")
                     {
                         response = HandleRegister(Context, Realm, request);
                     } 
                     else if (sipAuthMethod == "INVITE")
                     {
                          response = HandleInvite(Context, Realm, request);
                     }
                     else
                     {
                         m_log.ErrorFormat("[FreeSwitchVoice] HandleDirectoryRequest unknown sip_auth_method {0}",sipAuthMethod);
                         response["int_response_code"] = 404;
                         response["content_type"] = "text/xml";
                         response["str_response_string"] = "";
                     }
                 }
                 else if (eventCallingFunction == "switch_xml_locate_user")
                 {
                     response = HandleLocateUser(Realm, request);
                 }
                 else if (eventCallingFunction == "user_data_function") // gets called when an avatar to avatar call is made
                 {
                      response = HandleLocateUser(Realm, request);
                 }
                 else if (eventCallingFunction == "user_outgoing_channel")
                 {
                     response = HandleRegister(Context, Realm, request);
                 }
                 else if (eventCallingFunction == "config_sofia") // happens once on freeswitch startup
                 {
                     response = HandleConfigSofia(Context, Realm, request);
                 }
                 else if (eventCallingFunction == "switch_load_network_lists")
                 {
                     //response = HandleLoadNetworkLists(request);
                     response["int_response_code"] = 404;
                     response["keepalive"] = false;
                     response["content_type"] = "text/xml";
                     response["str_response_string"] = "";
                 }
                 else
                 {
                     m_log.ErrorFormat("[FreeSwitchVoice] HandleDirectoryRequest unknown Event-Calling-Function {0}",eventCallingFunction);
                     response["int_response_code"] = 404;
                     response["keepalive"] = false;
                     response["content_type"] = "text/xml";
                     response["str_response_string"] = "";
                 }
            }
            return response;
        }
        
        private Hashtable HandleRegister(string Context, string Realm, Hashtable request)
        {
            m_log.Info("[FreeSwitchDirectory] HandleRegister called");
            
            // TODO the password we return needs to match that sent in the request, this is hard coded for now
            string password = "1234";
            string domain = (string) request["domain"];
            string user = (string) request["user"];
            
            Hashtable response = new Hashtable();
            response["content_type"] = "text/xml";
            response["keepalive"] = false;
            response["int_response_code"] = 200;

            response["str_response_string"] = String.Format(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<document type=\"freeswitch/xml\">\r\n" +
                    "<section name=\"directory\" description=\"User Directory\">\r\n" +
                        "<domain name=\"{0}\">\r\n" +
                            "<user id=\"{1}\">\r\n" +
                                "<params>\r\n" +
                                    "<param name=\"password\" value=\"{2}\" />\r\n" +
                                    "<param name=\"dial-string\" value=\"{{sip_contact_user={1}}}{{presence_id=${{dialed_user}}@${{dialed_domain}}}}${{sofia_contact(${{dialed_user}}@${{dialed_domain}})}}\"/>\r\n" +
                                "</params>\r\n" +
                                "<variables>\r\n" +
                                    "<variable name=\"user_context\" value=\"{3}\" />\r\n" +
                                    "<variable name=\"presence_id\" value=\"{1}@{0}\"/>"+
                                "</variables>\r\n" +
                            "</user>\r\n" +
                        "</domain>\r\n" +
                    "</section>\r\n" +
                "</document>\r\n",
                domain , user, password, Context);
                
            return response;
        }
        
        private Hashtable HandleInvite(string Context, string Realm, Hashtable request)
        {
            m_log.Info("[FreeSwitchDirectory] HandleInvite called");
            
            // TODO the password we return needs to match that sent in the request, this is hard coded for now
            string password = "1234";
            string domain = (string) request["domain"];
            string user = (string) request["user"];
            string sipRequestUser = (string) request["sip_request_user"];
            
            Hashtable response = new Hashtable();
            response["content_type"] = "text/xml";
            response["keepalive"] = false;
            response["int_response_code"] = 200;
            response["str_response_string"] = String.Format(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<document type=\"freeswitch/xml\">\r\n" +
                    "<section name=\"directory\" description=\"User Directory\">\r\n" +
                        "<domain name=\"{0}\">\r\n" +
                            "<user id=\"{1}\">\r\n" +
                                "<params>\r\n" +
                                    "<param name=\"password\" value=\"{2}\" />\r\n" +
                                    "<param name=\"dial-string\" value=\"{{sip_contact_user={1}}}{{presence_id=${1}@${{dialed_domain}}}}${{sofia_contact(${1}@${{dialed_domain}})}}\"/>\r\n" +
                                "</params>\r\n" +
                                "<variables>\r\n" +
                                    "<variable name=\"user_context\" value=\"{4}\" />\r\n" +
                                    "<variable name=\"presence_id\" value=\"{1}@$${{domain}}\"/>"+
                                "</variables>\r\n" +
                            "</user>\r\n" +
                            "<user id=\"{3}\">\r\n" +
                                "<params>\r\n" +
                                    "<param name=\"password\" value=\"{2}\" />\r\n" +
                                    "<param name=\"dial-string\" value=\"{{sip_contact_user={1}}}{{presence_id=${3}@${{dialed_domain}}}}${{sofia_contact(${3}@${{dialed_domain}})}}\"/>\r\n" +
                                "</params>\r\n" +
                                "<variables>\r\n" +
                                    "<variable name=\"user_context\" value=\"{4}\" />\r\n" +
                                    "<variable name=\"presence_id\" value=\"{3}@$${{domain}}\"/>"+
                                "</variables>\r\n" +
                            "</user>\r\n" +
                        "</domain>\r\n" +
                    "</section>\r\n" +
                "</document>\r\n",
                domain , user, password,sipRequestUser, Context);
                
            return response;
        }

        private Hashtable HandleLocateUser(String Realm, Hashtable request)
        {
            m_log.Info("[FreeSwitchDirectory] HandleLocateUser called");
            
            // TODO the password we return needs to match that sent in the request, this is hard coded for now
            string domain = (string) request["domain"];
            string user = (string) request["user"];
            
            Hashtable response = new Hashtable();
            response["content_type"] = "text/xml";
            response["keepalive"] = false;
            response["int_response_code"] = 200;
            response["str_response_string"] = String.Format(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<document type=\"freeswitch/xml\">\r\n" +
                    "<section name=\"directory\" description=\"User Directory\">\r\n" +
                        "<domain name=\"{0}\">\r\n" +
                            "<params>\r\n" +
                                "<param name=\"dial-string\" value=\"{{sip_contact_user=${{dialed_user}}}}{{presence_id=${{dialed_user}}@${{dialed_domain}}}}${{sofia_contact(${{dialed_user}}@${{dialed_domain}})}}\"/>\r\n" +
                            "</params>\r\n" +
                            "<user id=\"{1}\">\r\n" +
                            "<variables>\r\n"+
                              "<variable name=\"default_gateway\" value=\"$${{default_provider}}\"/>\r\n"+
                              "<variable name=\"presence_id\" value=\"{1}@$${{domain}}\"/>"+
                            "</variables>\r\n"+
                            "</user>\r\n" +
                        "</domain>\r\n" +
                    "</section>\r\n" +
                "</document>\r\n",
                domain , user);
            
            return response;
        }
       
        private Hashtable HandleConfigSofia(string Context, string Realm, Hashtable request)
        {
            m_log.Info("[FreeSwitchDirectory] HandleConfigSofia called");
            
            // TODO the password we return needs to match that sent in the request, this is hard coded for now
            string domain = (string) request["domain"];
            
            Hashtable response = new Hashtable();
            response["content_type"] = "text/xml";
            response["keepalive"] = false;
            response["int_response_code"] = 200;
            response["str_response_string"] = String.Format(
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
                "<document type=\"freeswitch/xml\">\r\n" +
                    "<section name=\"directory\" description=\"User Directory\">\r\n" +
                        "<domain name=\"{0}\">\r\n" +
                            "<params>\r\n" +
                                "<param name=\"dial-string\" value=\"{{sip_contact_user=${{dialed_user}}}}{{presence_id=${{dialed_user}}@${{dialed_domain}}}}${{sofia_contact(${{dialed_user}}@${{dialed_domain}})}}\"/>\r\n" +
                            "</params>\r\n" +
                            "<groups name=\"default\">\r\n"+
                                "<users>\r\n"+
                                    "<user id=\"$${{default_provider}}\">\r\n"+
                                        "<gateways>\r\n"+
                                          "<gateway name=\"$${{default_provider}}\">\r\n"+
                                            "<param name=\"username\" value=\"$${{default_provider_username}}\"/>\r\n"+
                                            "<param name=\"password\" value=\"$${{default_provider_password}}\"/>\r\n"+
                                            "<param name=\"from-user\" value=\"$${{default_provider_username}}\"/>\r\n"+
                                            "<param name=\"from-domain\" value=\"$${{default_provider_from_domain}}\"/>\r\n"+
                                            "<param name=\"expire-seconds\" value=\"600\"/>\r\n"+
                                            "<param name=\"register\" value=\"$${{default_provider_register}}\"/>\r\n"+
                                            "<param name=\"retry-seconds\" value=\"30\"/>\r\n"+
                                            "<param name=\"extension\" value=\"$${{default_provider_contact}}\"/>\r\n"+
                                            "<param name=\"contact-params\" value=\"domain_name=$${{domain}}\"/>\r\n"+
                                            "<param name=\"context\" value=\"{1}\"/>\r\n"+
                                          "</gateway>\r\n"+
                                        "</gateways>\r\n"+
                                        "<params>\r\n"+
                                          "<param name=\"password\" value=\"$${{default_provider_password}}\"/>\r\n"+
                                        "</params>\r\n"+
                                      "</user>\r\n"+
                                "</users>"+
                            "</groups>\r\n" +
                            "<variables>\r\n"+
                              "<variable name=\"default_gateway\" value=\"$${{default_provider}}\"/>\r\n"+
                            "</variables>\r\n"+
                        "</domain>\r\n" +
                    "</section>\r\n" +
                "</document>\r\n", 
                domain, Context); 
             
            return response;
        }

        
//        private Hashtable HandleLoadNetworkLists(Hashtable request)
//        {
//            m_log.Info("[FreeSwitchDirectory] HandleLoadNetworkLists called");
//
//            // TODO the password we return needs to match that sent in the request, this is hard coded for now
//            string domain = (string) request["domain"];
//
//            Hashtable response = new Hashtable();
//            response["content_type"] = "text/xml";
//            response["keepalive"] = false;
//            response["int_response_code"] = 200;
//            response["str_response_string"] = String.Format(
//                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
//                "<document type=\"freeswitch/xml\">\r\n" +
//                    "<section name=\"directory\" description=\"User Directory\">\r\n" +
//                        "<domain name=\"{0}\">\r\n" +
//                            "<params>\r\n" +
//                                "<param name=\"dial-string\" value=\"{{presence_id=${{dialed_user}}@${{dialed_domain}}}}${{sofia_contact(${{dialed_user}}@${{dialed_domain}})}}\"/>\r\n" +
//                            "</params>\r\n" +
//                            "<groups name=\"default\"><users/></groups>\r\n" +
//                            "<variables>\r\n"+
//                              "<variable name=\"default_gateway\" value=\"$${{default_provider}}\"/>\r\n"+
//                            "</variables>\r\n"+
//                        "</domain>\r\n" +
//                    "</section>\r\n" +
//                "</document>\r\n",
//                domain); 
//
//
//            return response;
//        }
    }
}
