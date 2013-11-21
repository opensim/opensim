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
using System.Collections;
using System.Net;

using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Services.Interfaces;
using OpenMetaverse;

namespace OpenSim.Server.Handlers.Base
{
    public class RestHandlerUtils
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

        public static bool GetAuthentication(IOSHttpRequest httpRequest, out string authority, out string authKey)
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
