using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;

using Nini.Config;
using log4net;

namespace OpenSim.Framework.ServiceAuth
{
    public class BasicHttpAuthentication : IServiceAuth
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string m_Username, m_Password;
        private string m_CredentialsB64;

        private string remove_me;

        public string Credentials
        {
            get { return m_CredentialsB64; }
        }

        public BasicHttpAuthentication(IConfigSource config, string section)
        {
            remove_me = section;
            m_Username = Util.GetConfigVarFromSections<string>(config, "HttpAuthUsername", new string[] { "Network", section }, string.Empty);
            m_Password = Util.GetConfigVarFromSections<string>(config, "HttpAuthPassword", new string[] { "Network", section }, string.Empty); 
            string str = m_Username + ":" + m_Password;
            byte[] encData_byte = Util.UTF8.GetBytes(str);

            m_CredentialsB64 = Convert.ToBase64String(encData_byte);
            m_log.DebugFormat("[HTTP BASIC AUTH]: {0} {1} [{2}]", m_Username, m_Password, section);
        }

        public void AddAuthorization(NameValueCollection headers)
        {
            //m_log.DebugFormat("[HTTP BASIC AUTH]: Adding authorization for {0}", remove_me);
            headers["Authorization"] = "Basic " + m_CredentialsB64;
        }

        public bool Authenticate(string data)
        {
            string recovered = Util.Base64ToString(data);
            if (!String.IsNullOrEmpty(recovered))
            {
                string[] parts = recovered.Split(new char[] { ':' });
                if (parts.Length >= 2)
                {
                    return m_Username.Equals(parts[0]) && m_Password.Equals(parts[1]);
                }
            }

            return false;
        }

        public bool Authenticate(NameValueCollection requestHeaders, AddHeaderDelegate d)
        {
            //m_log.DebugFormat("[HTTP BASIC AUTH]: Authenticate in {0}", remove_me);
            if (requestHeaders != null)
            {
                string value = requestHeaders.Get("Authorization");
                if (value != null)
                {
                    value = value.Trim();
                    if (value.StartsWith("Basic "))
                    {
                        value = value.Replace("Basic ", string.Empty);
                        if (Authenticate(value))
                            return true;
                    }
                }
            }
            d("WWW-Authenticate", "Basic realm = \"Asset Server\"");
            return false;
        }
    }
}
