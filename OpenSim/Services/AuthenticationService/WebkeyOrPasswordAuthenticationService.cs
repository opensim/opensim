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
        public WebkeyOrPasswordAuthenticationService(IConfigSource config)
            : base(config)
        {
            this.config = config;
        }

        public string Authenticate(UUID principalID, string password, int lifetime)
        {
            AuthenticationData data = m_Database.Get(principalID);
            IAuthenticationService svc;
            Object[] args = new Object[] { config };
            string result = String.Empty;
            if (data != null && data.Data != null)
            {
                if (data.Data.ContainsKey("webLoginKey"))
                {
                    svc = ServerUtils.LoadPlugin<IAuthenticationService>("OpenSim.Services.AuthenticationService.dll", "WebkeyAuthenticationService", args);
                    result = svc.Authenticate(principalID, password, lifetime);
                    if (result == String.Empty)
                    {
                        m_log.DebugFormat("[Authenticate]: Web Login failed for PrincipalID {0}", principalID);
                    }
                }
                if (data.Data.ContainsKey("passwordHash") && data.Data.ContainsKey("passwordSalt"))
                {
                    svc = ServerUtils.LoadPlugin<IAuthenticationService>("OpenSim.Services.AuthenticationService.dll", "PasswordAuthenticationService", args);
                    result = svc.Authenticate(principalID, password, lifetime);
                    if (result == String.Empty)
                    {
                        m_log.DebugFormat("[Authenticate]: Password login failed for PrincipalID {0}", principalID);
                    }
                }
                if (result == string.Empty)
                {
                    m_log.DebugFormat("[AUTH SERVICE]: Both password and webLoginKey-based login failed for PrincipalID {0}", principalID);
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
