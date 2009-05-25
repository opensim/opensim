using System;
using System.Collections;
using System.Net;

using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Simulation
{
    public class Utils
    {
        /// <summary>
        /// Extract the param from an uri.
        /// </summary>
        /// <param name="uri">Something like this: /uuid/ or /uuid/handle/release</param>
        /// <param name="uri">uuid on uuid field</param>
        /// <param name="action">optional action</param>
        public static bool GetParams(string path, out UUID uuid, out ulong regionHandle, out string action)
        {
            uuid = UUID.Zero;
            action = "";
            regionHandle = 0;

            path = path.Trim(new char[] { '/' });
            string[] parts = path.Split('/');
            if (parts.Length <= 1)
            {
                return false;
            }
            else
            {
                if (!UUID.TryParse(parts[0], out uuid))
                    return false;

                if (parts.Length >= 2)
                    UInt64.TryParse(parts[1], out regionHandle);
                if (parts.Length >= 3)
                    action = parts[2];

                return true;
            }
        }

        public static bool GetAuthentication(OSHttpRequest httpRequest, out string authority, out string authKey)
        {
            authority = string.Empty;
            authKey = string.Empty;

            Uri authUri;

            string auth = httpRequest.Headers["authentication"];
            // Authentication keys look like this:
            // http://orgrid.org:8002/<uuid>
            if ((auth != null) && (!string.Empty.Equals(auth)) && auth != "None")
            {
                if (Uri.TryCreate(auth, UriKind.Absolute, out authUri))
                {
                    authority = authUri.Authority;
                    authKey = authUri.PathAndQuery.Trim('/');
                    return true;
                }
            }

            return false;
        }

    }
}
