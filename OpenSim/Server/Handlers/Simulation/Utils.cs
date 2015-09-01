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
using System.Reflection;

using OpenMetaverse;
using OpenMetaverse.StructuredData;

using log4net;

namespace OpenSim.Server.Handlers.Simulation
{
    public class Utils
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Extract the param from an uri.
        /// </summary>
        /// <param name="uri">Something like this: /agent/uuid/ or /agent/uuid/handle/release</param>
        /// <param name="uri">uuid on uuid field</param>
        /// <param name="action">optional action</param>
        public static bool GetParams(string uri, out UUID uuid, out UUID regionID, out string action)
        {
            uuid = UUID.Zero;
            regionID = UUID.Zero;
            action = "";

            uri = uri.Trim(new char[] { '/' });
            string[] parts = uri.Split('/');
            if (parts.Length <= 1)
            {
                return false;
            }
            else
            {
                if (!UUID.TryParse(parts[1], out uuid))
                    return false;

                if (parts.Length >= 3)
                    UUID.TryParse(parts[2], out regionID);
                if (parts.Length >= 4)
                    action = parts[3];

                return true;
            }
        }

        public static OSDMap GetOSDMap(string data)
        {
            OSDMap args = null;
            try
            {
                OSD buffer;
                // We should pay attention to the content-type, but let's assume we know it's Json
                buffer = OSDParser.DeserializeJson(data);
                if (buffer.Type == OSDType.Map)
                {
                    args = (OSDMap)buffer;
                    return args;
                }
                else
                {
                    // uh?
                    m_log.Debug(("[REST COMMS]: Got OSD of unexpected type " + buffer.Type.ToString()));
                    return null;
                }
            }
            catch (Exception ex)
            {
                m_log.Debug("[REST COMMS]: exception on parse of REST message " + ex.Message);
                return null;
            }
        }

    }
}
