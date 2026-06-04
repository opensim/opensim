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
*
*/

using System.Collections;
using System.Net;
using System.Reflection;
using System.Xml;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.DataSnapshot
{
    public class DataRequestHandler
    {
//        private Scene m_scene = null;
        private DataSnapshotManager m_externalData = null;
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ExpiringCache<string, int> throotleGen = new ExpiringCache<string, int>();

        public DataRequestHandler(Scene scene, DataSnapshotManager externalData)
        {
//            m_scene = scene;
            m_externalData = externalData;

            //Register HTTP handler
            MainServer.UnSecureInstance.AddGloblaMethodHandler("collector", OnGetSnapshot);
            // Register validation callback handler
            MainServer.UnSecureInstance.AddGloblaMethodHandler("validate", OnValidate);

            m_log.Info("[DATASNAPSHOT]: Set up snapshot service");
        }

        public void OnGetSnapshot(IOSHttpRequest req, IOSHttpResponse resp)
        {
            string reqtag;
            if(req.QueryAsDictionary.TryGetValue("region", out string snapObj) && !string.IsNullOrWhiteSpace(snapObj))
                reqtag = snapObj + req.RemoteIPEndPoint.Address.ToString();
            else
                reqtag = req.RemoteIPEndPoint.Address.ToString();

            if (!string.IsNullOrWhiteSpace(reqtag))
            {
                if(throotleGen.Contains(reqtag))
                {
                    resp.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    resp.StatusDescription = "Please try again later";
                    resp.ContentType = "text/plain";
                    m_log.Debug("[DATASNAPSHOT] Collection request spam. reply try later");
                    return;
                }

                throotleGen.AddOrUpdate(reqtag, 0, 60);
            }

            if(string.IsNullOrWhiteSpace(snapObj))
                m_log.DebugFormat("[DATASNAPSHOT] Received collection request for all");
            else
               m_log.DebugFormat("[DATASNAPSHOT] Received collection request for {0}", snapObj);

            XmlDocument response = m_externalData.GetSnapshot(snapObj);
            if(response == null)
            {
                resp.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                resp.ContentType = "text/plain";
                m_log.Debug("[DATASNAPSHOT] Collection request spam. reply try later");
                return;
            }

            resp.RawBuffer = Util.UTF8NBGetbytes(response.OuterXml);
            resp.ContentType = "text/xml";
            resp.StatusCode = (int)HttpStatusCode.OK;
        }

        public void OnValidate(IOSHttpRequest req, IOSHttpResponse resp)
        {
            m_log.Debug("[DATASNAPSHOT] Received validation request");
            resp.ContentType = "text/xml";
            resp.StatusCode = (int)HttpStatusCode.Forbidden;

            if(req.QueryAsDictionary.TryGetValue("secret", out string secret))
            {
                if (secret == m_externalData.Secret.ToString())
                    resp.StatusCode = (int)HttpStatusCode.OK;
            }
        }
    }
}
