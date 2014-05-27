using System;
using System.Collections.Generic;

using Nini.Config;

namespace OpenSim.Framework.ServiceAuth
{
    public class ServiceAuth
    {
        public static IServiceAuth Create(IConfigSource config, string section)
        {
            string authType = Util.GetConfigVarFromSections<string>(config, "AuthType", new string[] { "Network", section }, "None");

            switch (authType)
            {
                case "BasicHttpAuthentication":
                    return new BasicHttpAuthentication(config, section);
            }

            return null;
        }
    }
}
