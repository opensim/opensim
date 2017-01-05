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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Services.Connectors
{
    public class AuthorizationServicesConnector
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;
        private bool m_ResponseOnFailure = true;

        public AuthorizationServicesConnector()
        {
        }

        public AuthorizationServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        public AuthorizationServicesConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig authorizationConfig = source.Configs["AuthorizationService"];
            if (authorizationConfig == null)
            {
                //m_log.Info("[AUTHORIZATION CONNECTOR]: AuthorizationService missing from OpenSim.ini");
                throw new Exception("Authorization connector init error");
            }

            string serviceURI = authorizationConfig.GetString("AuthorizationServerURI",
                    String.Empty);

            if (serviceURI == String.Empty)
            {
                m_log.Error("[AUTHORIZATION CONNECTOR]: No Server URI named in section AuthorizationService");
                throw new Exception("Authorization connector init error");
            }
            m_ServerURI = serviceURI;

            // this dictates what happens if the remote service fails, if the service fails and the value is true
            // the user is authorized for the region.
            bool responseOnFailure = authorizationConfig.GetBoolean("ResponseOnFailure",true);

            m_ResponseOnFailure = responseOnFailure;
            m_log.Info("[AUTHORIZATION CONNECTOR]: AuthorizationService initialized");
        }

        public bool IsAuthorizedForRegion(string userID, string firstname, string surname, string email, string regionName, string regionID, out string message)
        {
            // do a remote call to the authorization server specified in the AuthorizationServerURI
            m_log.InfoFormat("[AUTHORIZATION CONNECTOR]: IsAuthorizedForRegion checking {0} at remote server {1}", userID, m_ServerURI);

            string uri = m_ServerURI;

            AuthorizationRequest req = new AuthorizationRequest(userID, firstname, surname, email, regionName, regionID);

            AuthorizationResponse response;
            try
            {
                response = SynchronousRestObjectRequester.MakeRequest<AuthorizationRequest, AuthorizationResponse>("POST", uri, req);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[AUTHORIZATION CONNECTOR]: Unable to send authorize {0} for region {1} error thrown during comms with remote server. Reason: {2}", userID, regionID, e.Message);
                message = e.Message;
                return m_ResponseOnFailure;
            }
            if (response == null)
            {
                message = "Null response";
                return m_ResponseOnFailure;
            }
            m_log.DebugFormat("[AUTHORIZATION CONNECTOR] response from remote service was {0}", response.Message);
            message = response.Message;

            return response.IsAuthorized;
        }

    }
}
