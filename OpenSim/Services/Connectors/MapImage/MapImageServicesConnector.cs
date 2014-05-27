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

using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;

using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Communications;
using OpenSim.Framework.ServiceAuth;
using OpenSim.Server.Base;
using OpenSim.Services.Interfaces;
using OpenMetaverse;
using OpenMetaverse.StructuredData;

namespace OpenSim.Services.Connectors
{
    public class MapImageServicesConnector : BaseServiceConnector, IMapImageService
    {
        private static readonly ILog m_log =
                LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType);

        private string m_ServerURI = String.Empty;

        public MapImageServicesConnector()
        {
        }

        public MapImageServicesConnector(string serverURI)
        {
            m_ServerURI = serverURI.TrimEnd('/');
        }

        public MapImageServicesConnector(IConfigSource source)
        {
            Initialise(source);
        }

        public virtual void Initialise(IConfigSource source)
        {
            IConfig config = source.Configs["MapImageService"];
            if (config == null)
            {
                m_log.Error("[MAP IMAGE CONNECTOR]: MapImageService missing");
                throw new Exception("MapImage connector init error");
            }

            string serviceURI = config.GetString("MapImageServerURI",
                    String.Empty);

            if (serviceURI == String.Empty)
            {
                m_log.Error("[MAP IMAGE CONNECTOR]: No Server URI named in section MapImageService");
                throw new Exception("MapImage connector init error");
            }
            m_ServerURI = serviceURI;
            m_ServerURI = serviceURI.TrimEnd('/');
            base.Initialise(source, "MapImageService");
        }

        public bool RemoveMapTile(int x, int y, out string reason)
        {
            reason = string.Empty;
            int tickstart = Util.EnvironmentTickCount();
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["X"] = x.ToString();
            sendData["Y"] = y.ToString();

            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/removemap";

            try
            {
                string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString,
                        m_Auth);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.ContainsKey("Result") && (replyData["Result"].ToString().ToLower() == "success"))
                    {
                        return true;
                    }
                    else if (replyData.ContainsKey("Result") && (replyData["Result"].ToString().ToLower() == "failure"))
                    {
                        m_log.DebugFormat("[MAP IMAGE CONNECTOR]: Delete failed: {0}", replyData["Message"].ToString());
                        reason = replyData["Message"].ToString();
                        return false;
                    }
                    else if (!replyData.ContainsKey("Result"))
                    {
                        m_log.DebugFormat("[MAP IMAGE CONNECTOR]: reply data does not contain result field");
                    }
                    else
                    {
                        m_log.DebugFormat("[MAP IMAGE CONNECTOR]: unexpected result {0}", replyData["Result"].ToString());
                        reason = "Unexpected result " + replyData["Result"].ToString();
                    }

                }
                else
                {
                    m_log.DebugFormat("[MAP IMAGE CONNECTOR]: Map post received null reply");
                }
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[MAP IMAGE CONNECTOR]: Exception when contacting map server at {0}: {1}", uri, e.Message);
            }
            finally
            {
                // This just dumps a warning for any operation that takes more than 100 ms
                int tickdiff = Util.EnvironmentTickCountSubtract(tickstart);
                m_log.DebugFormat("[MAP IMAGE CONNECTOR]: map tile deleted in {0}ms", tickdiff);
            }

            return false;
        }

        public bool AddMapTile(int x, int y, byte[] jpgData, out string reason)
        {
            reason = string.Empty;
            int tickstart = Util.EnvironmentTickCount();
            Dictionary<string, object> sendData = new Dictionary<string, object>();
            sendData["X"] = x.ToString();
            sendData["Y"] = y.ToString();
            sendData["TYPE"] = "image/jpeg";
            sendData["DATA"] = Convert.ToBase64String(jpgData);

            string reqString = ServerUtils.BuildQueryString(sendData);
            string uri = m_ServerURI + "/map";

            try
            {
                string reply = SynchronousRestFormsRequester.MakeRequest("POST",
                        uri,
                        reqString,
                        m_Auth);
                if (reply != string.Empty)
                {
                    Dictionary<string, object> replyData = ServerUtils.ParseXmlResponse(reply);

                    if (replyData.ContainsKey("Result") && (replyData["Result"].ToString().ToLower() == "success"))
                    {
                        return true;
                    }
                    else if (replyData.ContainsKey("Result") && (replyData["Result"].ToString().ToLower() == "failure"))
                    {
                        reason = string.Format("Map post to {0} failed: {1}", uri, replyData["Message"].ToString());
                        m_log.WarnFormat("[MAP IMAGE CONNECTOR]: {0}", reason);

                        return false;
                    }
                    else if (!replyData.ContainsKey("Result"))
                    {
                        reason = string.Format("Reply data from {0} does not contain result field", uri);
                        m_log.WarnFormat("[MAP IMAGE CONNECTOR]: {0}", reason);
                    }
                    else
                    {
                        reason = string.Format("Unexpected result {0} from {1}" + replyData["Result"].ToString(), uri);
                        m_log.WarnFormat("[MAP IMAGE CONNECTOR]: {0}", reason);
                    }
                }
                else
                {
                    reason = string.Format("Map post received null reply from {0}", uri);
                    m_log.WarnFormat("[MAP IMAGE CONNECTOR]: {0}", reason);
                }
            }
            catch (Exception e)
            {
                reason = string.Format("Exception when posting to map server at {0}: {1}", uri, e.Message);
                m_log.WarnFormat("[MAP IMAGE CONNECTOR]: {0}", reason);
            }
            finally
            {
                // This just dumps a warning for any operation that takes more than 100 ms
                int tickdiff = Util.EnvironmentTickCountSubtract(tickstart);
                m_log.DebugFormat("[MAP IMAGE CONNECTOR]: map tile uploaded in {0}ms", tickdiff);
            }

            return false;

        }

        public byte[] GetMapTile(string fileName, out string format)
        {
            format = string.Empty;
            new Exception("GetMapTile method not Implemented");
            return null;
        }
    }
}
