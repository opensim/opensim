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

namespace OpenSim.Services.AuthenticationService
{
    public class WebkeyOrPasswordAuthenticationService : AuthenticationServiceBase, IAuthenticationService
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        public WebkeyOrPasswordAuthenticationService(IConfigSource config)
            : base(config)
        {
        }

        public string Authenticate(UUID principalID, string password, int lifetime)
        {
            AuthenticationData data = m_Database.Get(principalID);
            if (data != null && data.Data != null)
            {
                if (data.Data.ContainsKey("webLoginKey"))
                {
                    m_log.InfoFormat("[Authenticate]: Trying a web key authentication");
                    if (new UUID(password) == UUID.Zero)
                    {
                        m_log.InfoFormat("[Authenticate]: NULL_KEY is not a valid web_login_key");
                    }
                    else
                    {
                        string key = data.Data["webLoginKey"].ToString();
                        m_log.DebugFormat("[WEB LOGIN AUTH]: got {0} for key in db vs {1}", key, password);
                        if (key == password)
                        {
                            data.Data["webLoginKey"] = UUID.Zero.ToString();
                            m_Database.Store(data);
                            return GetToken(principalID, lifetime);
                        }
                    }
                }
                if (data.Data.ContainsKey("passwordHash") && data.Data.ContainsKey("passwordSalt"))
                {
                    m_log.InfoFormat("[Authenticate]: Trying a password authentication");
                    string hashed = Util.Md5Hash(password + ":" + data.Data["passwordSalt"].ToString());
                    m_log.DebugFormat("[PASS AUTH]: got {0}; hashed = {1}; stored = {2}", password, hashed, data.Data["passwordHash"].ToString());
                    if (data.Data["passwordHash"].ToString() == hashed)
                    {
                        return GetToken(principalID, lifetime);
                    }
                }
                m_log.DebugFormat("[AUTH SERVICE]: Both password and webLoginKey-based login failed for PrincipalID {0}", principalID);
            }
            else
            {
                m_log.DebugFormat("[AUTH SERVICE]: PrincipalID {0} or its data not found", principalID);
            }
            return string.Empty;
        }
    }
}
