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
using System.IO;
using System.Reflection;
using log4net;
using OpenMetaverse;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Grid.Framework
{
    public class XMPPHTTPStreamHandler : BaseStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);


        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="assetManager"></param>
        /// <param name="assetProvider"></param>
        public XMPPHTTPStreamHandler()
            : base("GET", "/presence")
        {
            m_log.Info("[REST]: In Get Request");

        }

        public override byte[] Handle(string path, Stream request,
                                      OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            string param = GetParam(path);
            byte[] result = new byte[] {};
            try
            {
                string[] p = param.Split(new char[] {'/', '?', '&'}, StringSplitOptions.RemoveEmptyEntries);

                if (p.Length > 0)
                {
                    UUID assetID = UUID.Zero;

                    if (!UUID.TryParse(p[0], out assetID))
                    {
                        m_log.InfoFormat(
                            "[REST]: GET:/presence ignoring request with malformed UUID {0}", p[0]);
                        return result;
                    }

                }
            }
            catch (Exception e)
            {
                m_log.Error(e.ToString());
            }
            return result;
        }
    }

    public class PostXMPPStreamHandler : BaseStreamHandler
    {
        // private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public override byte[] Handle(string path, Stream request,
                                      OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            string param = GetParam(path);

            UUID assetId;
            if (param.Length > 0)
                UUID.TryParse(param, out assetId);
            // byte[] txBuffer = new byte[4096];

            // TODO: Read POST serialize XMPP stanzas

            return new byte[] {};
        }

        public PostXMPPStreamHandler()
            : base("POST", "/presence")
        {

        }

    }
}
