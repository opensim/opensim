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
        private IConfigSource config;
        private Dictionary<string, IAuthenticationService> svc_checks;
        public WebkeyOrPasswordAuthenticationService(IConfigSource config)
            : base(config)
        {
            this.config = config;
            svc_checks["web_login_key"] = new WebkeyAuthenticationService(config);
            svc_checks["password"]      = new PasswordAuthenticationService(config);
        }

        public string Authenticate(UUID principalID, string password, int lifetime)
        {
            AuthenticationData data = m_Database.Get(principalID);
            string result = String.Empty;
            if (data != null && data.Data != null)
            {
                if (data.Data.ContainsKey("webLoginKey"))
                {
                    m_log.DebugFormat("[AUTH SERVICE]: Attempting web key authentication for PrincipalID {0}", principalID);
                    result = svc_checks["web_login_key"].Authenticate(principalID, password, lifetime);
                    if (result == String.Empty)
                    {
                        m_log.DebugFormat("[AUTH SERVICE]: Web Login failed for PrincipalID {0}", principalID);
                    }
                }
                if (result == string.Empty && data.Data.ContainsKey("passwordHash") && data.Data.ContainsKey("passwordSalt"))
                {
                    m_log.DebugFormat("[AUTH SERVICE]: Attempting password authentication for PrincipalID {0}", principalID);
                    result = svc_checks["password"].Authenticate(principalID, password, lifetime);
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
