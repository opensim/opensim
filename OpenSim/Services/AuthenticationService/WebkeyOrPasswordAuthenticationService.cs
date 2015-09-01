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
using OpenMetaverse;
using OpenSim.Services.Interfaces;
using log4net;
using Nini.Config;
using System.Reflection;
using OpenSim.Data;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Server.Base;

namespace OpenSim.Services.AuthenticationService
{
    public class WebkeyOrPasswordAuthenticationService : AuthenticationServiceBase, IAuthenticationService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private Dictionary<string, IAuthenticationService> m_svcChecks 
            = new Dictionary<string, IAuthenticationService>();
        
        public WebkeyOrPasswordAuthenticationService(IConfigSource config)
            : base(config)
        {
            m_svcChecks["web_login_key"] = new WebkeyAuthenticationService(config);
            m_svcChecks["password"]      = new PasswordAuthenticationService(config);
        }

        public string Authenticate(UUID principalID, string password, int lifetime)
        {
            UUID realID;

            return Authenticate(principalID, password, lifetime, out realID);
        }

        public string Authenticate(UUID principalID, string password, int lifetime, out UUID realID)
        {
            AuthenticationData data = m_Database.Get(principalID);
            string result = String.Empty;
            realID = UUID.Zero;
            if (data != null && data.Data != null)
            {
                if (data.Data.ContainsKey("webLoginKey"))
                {
                    m_log.DebugFormat("[AUTH SERVICE]: Attempting web key authentication for PrincipalID {0}", principalID);
                    result = m_svcChecks["web_login_key"].Authenticate(principalID, password, lifetime, out realID);
                    if (result == String.Empty)
                    {
                        m_log.DebugFormat("[AUTH SERVICE]: Web Login failed for PrincipalID {0}", principalID);
                    }
                }
                if (result == string.Empty && data.Data.ContainsKey("passwordHash") && data.Data.ContainsKey("passwordSalt"))
                {
                    m_log.DebugFormat("[AUTH SERVICE]: Attempting password authentication for PrincipalID {0}", principalID);
                    result = m_svcChecks["password"].Authenticate(principalID, password, lifetime, out realID);
                    if (result == String.Empty)
                    {
                        m_log.DebugFormat("[AUTH SERVICE]: Password login failed for PrincipalID {0}", principalID);
                    }
                }

              

                if (result == string.Empty)
                {
                    m_log.DebugFormat("[AUTH SERVICE]: Both password and webLoginKey-based authentication failed for PrincipalID {0}", principalID);
                }
            }
            else
            {
                m_log.DebugFormat("[AUTH SERVICE]: PrincipalID {0} or its data not found", principalID);
            }
            
            
            return result;
        }
    }
}
