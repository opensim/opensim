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
    public class FreeSwitchDialplan 
    {
        private static readonly ILog m_log = 
            LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
            
         
        public Hashtable HandleDialplanRequest(string Context, string Realm, Hashtable request)
        {
             m_log.DebugFormat("[FreeSwitchVoice] HandleDialplanRequest called with {0}",request.ToString());
             
             Hashtable response = new Hashtable();
             
             foreach (DictionaryEntry item in request)
             {
                m_log.InfoFormat("[FreeSwitchDirectory] requestBody item {0} {1}",item.Key, item.Value);
             }

             string requestcontext = (string) request["Hunt-Context"];
             response["content_type"] = "text/xml";
             response["keepalive"] = false;
            response["int_response_code"] = 200;
            if (Context != String.Empty && Context != requestcontext)
            {
                m_log.Debug("[FreeSwitchDirectory] returning empty as it's for another context");
                response["str_response_string"] = "";
            } else {
                response["str_response_string"] = String.Format(@"<?xml version=""1.0"" encoding=""utf-8""?>
                   <document type=""freeswitch/xml"">
                     <section name=""dialplan"">
                     <context name=""{0}"">" + 
               
/*                           <!-- dial via SIP uri -->
                            <extension name=""sip_uri"">
                                   <condition field=""destination_number"" expression=""^sip:(.*)$"">
                                   <action application=""bridge"" data=""sofia/${use_profile}/$1""/>
                                   <!--<action application=""bridge"" data=""$1""/>-->
                                   </condition>
                           </extension>*/
               
                           @"<extension name=""opensim_conferences"">
                                   <condition field=""destination_number"" expression=""^confctl-(.*)$"">
                                           <action application=""answer""/>
                                           <action application=""conference"" data=""$1-{1}@{0}""/>
                                   </condition>
                           </extension>
                       
                           <extension name=""opensim_conf"">
                                   <condition field=""destination_number"" expression=""^conf-(.*)$"">
                                           <action application=""answer""/>
                                           <action application=""conference"" data=""$1-{1}@{0}""/>
                                   </condition>
                           </extension>
               
                           <extension name=""avatar"">
                                   <condition field=""destination_number"" expression=""^(x.*)$"">
                                           <action application=""bridge"" data=""user/$1""/>
                                   </condition>
                           </extension>
               
                     </context>
                   </section>
                   </document>", Context, Realm);
            }
             
            return response;
        }
    }
    
}
